using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ChemicalSafety.Results
{
    /// <summary>
    /// Sends the finished submission to POST {server}/results/batch (the server comes from <see cref="ResultApiConfig"/>: stage or production),
    /// all scenarios done, or the 40-minute time-out), offline first: the result is saved on the device (results/TrainingID/date/Name_time.json,
    /// the Excel copy .csv, and a .pending sidecar with the API body) BEFORE anything else, then <see cref="ResultOutbox"/> sends the waiting
    /// results in the background in small batches (1 person at first, up to 5), one request at a time, jittered back-off, a pause when
    /// the server is overloaded, and a re-scan every 45 s and when the connection comes back. The game never waits for the network.
    /// A session that was cut off (app closed before the end) is kept as a .partial body at every save and is sent as an interrupted result at the next start.
    /// The same client_submission_id is re-sent on every retry; the backend answers 200 for a repeat and stores nothing twice.
    /// </summary>
    public sealed class ResultUploader : MonoBehaviour
    {
        /// <summary>false = no network call at all.</summary>
        public static bool Enabled = true;

        /// <summary>false = only the sample fields go to the API. true = also email, phone, totals and per-answer extras (when the backend accepts them).</summary>
        public const bool SendExtras = false;

        private const float ScanEvery = 45f;

        private static ResultUploader _inst;

        private static ResultUploader Inst
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("ResultUploader");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<ResultUploader>();
                    _inst._outbox = new ResultOutbox(ResultPaths.Root, new UnityTransport(_inst), () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
                }

                return _inst;
            }
        }

        private ResultOutbox _outbox;
        private float _nextScan = -1f;
        private NetworkReachability _lastReach = NetworkReachability.NotReachable;
        private bool _reachInit;

        /// <summary>Writes the .pending sidecar (the API body) next to the result file. Called when the session is completed, so a crash right after loses nothing.</summary>
        public static void Stage(Submission s)
        {
            if (!Enabled || !SessionResults.UploadAllowed || s == null || s.training_id <= 0)
                return;
            try
            {
                var json = ResultPaths.For(s);
                if (!File.Exists(json))
                    ResultPaths.WriteAtomic(json, SubmissionExport.ToJson(s));
                ResultPaths.WriteAtomic(ResultOutbox.PendingOf(json), SubmissionExport.ToApiJson(s, SendExtras));
                var partial = ResultOutbox.PartialOf(json);
                if (File.Exists(partial))
                    File.Delete(partial); // the finished body replaces the "cut off" one
            }
            catch (Exception e)
            {
                Debug.LogError("[Upload] could not stage the request body: " + e.Message);
            }
        }

        /// <summary>Called once per participant, right after SessionResults.CompleteAll. Never blocks.</summary>
        public static void Send(Submission s)
        {
            if (!Enabled || !SessionResults.UploadAllowed || s == null || s.training_id <= 0)
            {
                Debug.Log("[Upload] skipped (disabled / test session / no training id)");
                return;
            }

            Stage(s);
            Inst._outbox.Enqueue(ResultPaths.For(s));
        }

        /// <summary>App start: moves old flat files into the new folders, then finds everything that was never accepted and sends it (spread over 0-30 s).</summary>
        public static void RetryPending()
        {
            if (!Enabled)
                return;
            var i = Inst;
            try
            {
                var recovered = ResultPaths.RecoverTemps(ResultPaths.Root); // a write that was cut by a crash
                if (recovered > 0)
                    Debug.Log("[Upload] recovered " + recovered + " result file(s) from an interrupted write");
                Migrate(ResultPaths.Root);
                var promoted = PromoteCutOffSessions(ResultPaths.Root);
                if (promoted > 0)
                    Debug.Log("[Upload] " + promoted + " cut-off session(s) will be sent as interrupted results");
                var pruned = i._outbox.PruneSent();
                if (pruned > 0)
                    Debug.Log("[Upload] removed " + pruned + " old accepted results");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Upload] housekeeping: " + e.Message);
            }

            var found = i._outbox.Scan();
            if (found > 0)
            {
                Debug.Log("[Upload] " + found + " result(s) waiting to be sent");
                i._outbox.NetworkRegained(30f); // spread the first tries
            }

            i._nextScan = Time.realtimeSinceStartup + ScanEvery;
        }

        /// <summary>State of this session's result for the scorecard line.</summary>
        public static UploadStatus StatusOf(Submission s)
        {
            if (!Enabled || s == null || s.training_id <= 0 || !SessionResults.UploadAllowed)
                return UploadStatus.None;
            if (_inst == null)
                return UploadStatus.Saved;
            return _inst._outbox.Status(ResultPaths.For(s));
        }

        /// <summary>
        /// A session whose app was closed before the end leaves X.json.partial (the API body of what was answered). At the next start it becomes
        /// X.json.pending, so the answers reach the server as an interrupted result. The session that is running now is never touched.
        /// </summary>
        public static int PromoteCutOffSessions(string root)
        {
            if (!Directory.Exists(root))
                return 0;
            string live = null;
            var cur = SessionResults.Current;
            if (cur != null && cur.training_id > 0)
                live = ResultPaths.For(cur);
            var n = 0;
            foreach (var f in Directory.GetFiles(root, "*.partial", SearchOption.AllDirectories))
            {
                try
                {
                    var json = f.Substring(0, f.Length - ".partial".Length);
                    if (File.Exists(ResultOutbox.PendingOf(json)) || File.Exists(ResultOutbox.SentOf(json)))
                    {
                        File.Delete(f); // the finished (or already sent) result wins
                        continue;
                    }

                    if (live != null && string.Equals(live, json, StringComparison.Ordinal))
                        continue;
                    File.Move(f, ResultOutbox.PendingOf(json));
                    n++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Upload] cut-off session " + f + ": " + e.Message);
                }
            }

            return n;
        }

        // ------------------------------------------------------------------ old layout (results/<guid>.json) -> new layout

        [Serializable]
        private sealed class Probe
        {
            public string client_submission_id;
            public int training_id;
            public string participant_name;
        }

        private static void Migrate(string root)
        {
            if (!Directory.Exists(root))
                return;
            foreach (var f in Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = System.IO.Path.GetFileName(f);
                if (name.EndsWith(".api.json", StringComparison.Ordinal))
                    continue;
                try
                {
                    var p = JsonUtility.FromJson<Probe>(File.ReadAllText(f));
                    if (p == null || string.IsNullOrEmpty(p.client_submission_id))
                        continue;
                    var when = File.GetLastWriteTime(f);
                    var sub = new Submission { client_submission_id = p.client_submission_id, training_id = p.training_id, participant_name = p.participant_name, startedLocal = when };
                    var target = ResultPaths.For(root, sub);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target) ?? root);
                    var old = System.IO.Path.Combine(root, p.client_submission_id);
                    File.Move(f, target);
                    if (File.Exists(old + ".api.json"))
                        File.Move(old + ".api.json", ResultOutbox.PendingOf(target));
                    if (File.Exists(old + ".sent"))
                        File.Move(old + ".sent", ResultOutbox.SentOf(target));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Upload] migrate " + name + ": " + e.Message);
                }
            }

            // old bodies whose result file is gone: keep them as a result of their own so they are still sent
            foreach (var f in Directory.GetFiles(root, "*.api.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var id = System.IO.Path.GetFileName(f);
                    id = id.Substring(0, id.Length - ".api.json".Length);
                    var target = System.IO.Path.Combine(root, "0", File.GetLastWriteTime(f).ToString("yyyy-MM-dd"), "recovered_" + id.Substring(0, Math.Min(8, id.Length)) + ".json");
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target) ?? root);
                    File.Copy(f, target, true);
                    File.Move(f, ResultOutbox.PendingOf(target));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Upload] recover " + f + ": " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------ pump

        private void Update()
        {
            if (_outbox == null)
                return;
            var reach = Application.internetReachability;
            if (!_reachInit)
            {
                _reachInit = true;
                _lastReach = reach;
            }
            else if (reach != _lastReach)
            {
                if (_lastReach == NetworkReachability.NotReachable && reach != NetworkReachability.NotReachable)
                {
                    Debug.Log("[Upload] connection is back: waiting results are tried");
                    _outbox.Scan();
                    _outbox.NetworkRegained(20f);
                }

                _lastReach = reach;
            }

            if (_nextScan > 0f && Time.realtimeSinceStartup >= _nextScan)
            {
                _nextScan = Time.realtimeSinceStartup + ScanEvery;
                _outbox.Scan();
            }

            _outbox.Tick();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus && _outbox != null)
            {
                _outbox.Scan();
                _outbox.NetworkRegained(10f); // the headset was asleep: try again soon
            }
        }

        // ------------------------------------------------------------------ real transport

        private sealed class UnityTransport : IResultTransport
        {
            private readonly MonoBehaviour _host;

            public UnityTransport(MonoBehaviour host) => _host = host;

            public void Post(byte[] body, int timeoutSeconds, Action<UploadReply> done) => _host.StartCoroutine(Run(body, timeoutSeconds, done));

            private static IEnumerator Run(byte[] body, int timeoutSeconds, Action<UploadReply> done)
            {
                var url = ResultApiConfig.BatchUrl;
                if (string.IsNullOrEmpty(url))
                {
                    // no valid server profile: nothing is sent, the results stay on the device and are retried later
                    done(new UploadReply { network = true, text = "no api config" });
                    yield break;
                }

                using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("x-api-key", ResultApiConfig.ApiKey);
                    req.timeout = timeoutSeconds;
                    yield return req.SendWebRequest();
                    var r = new UploadReply { code = (int)req.responseCode, text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty };
                    if (req.result == UnityWebRequest.Result.ConnectionError)
                    {
                        r.network = true;
                        r.timeout = req.error != null && req.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    var ra = req.GetResponseHeader("Retry-After");
                    if (!string.IsNullOrEmpty(ra) && float.TryParse(ra, out var secs))
                        r.retryAfter = Mathf.Clamp(secs, 1f, 900f);
                    done(r);
                }
            }
        }
    }
}
