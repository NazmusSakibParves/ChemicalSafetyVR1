using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ChemicalSafety.Results
{
    /// <summary>One answer of the server (or a network failure).</summary>
    public struct UploadReply
    {
        public int code;          // HTTP status, 0 when there was none
        public bool network;      // no connection / DNS / reset / timeout: nothing reached the server (or no answer came back)
        public bool timeout;
        public float retryAfter;  // seconds from a Retry-After header, 0 = none
        public string text;
    }

    public interface IResultTransport
    {
        /// <summary>Posts the body once and calls <paramref name="done"/> exactly once (possibly later, on the main thread).</summary>
        void Post(byte[] body, int timeoutSeconds, Action<UploadReply> done);
    }

    public enum UploadStatus { None, Saved, Waiting, Sent, Rejected }

    /// <summary>Where result files live: results/&lt;TrainingID&gt;/&lt;yyyy-MM-dd&gt;/&lt;Name&gt;_&lt;yyyyMMdd-HHmmss&gt;.json</summary>
    public static class ResultPaths
    {
        public static string Root => Path.Combine(Application.persistentDataPath, "results");

        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>();

        /// <summary>The result file of a session, chosen once (the first save) and then kept, even if the name changes.</summary>
        public static string For(Submission s) => For(Root, s);

        public static string For(string root, Submission s)
        {
            if (Map.TryGetValue(s.client_submission_id, out var known))
                return known;
            var when = s.startedLocal == default ? DateTime.Now : s.startedLocal;
            var dir = Path.Combine(Path.Combine(root, Math.Max(0, s.training_id).ToString()), when.ToString("yyyy-MM-dd"));
            var name = Safe(s.participant_name) + "_" + when.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(dir, name + ".json");
            if (File.Exists(path)) // same player in the same second on another session: keep both
                path = Path.Combine(dir, name + "_" + s.client_submission_id.Substring(0, 6) + ".json");
            Map[s.client_submission_id] = path;
            return path;
        }

        public static void ForgetAll() => Map.Clear();

        /// <summary>Player name as a safe file name part (Bangla letters kept, no path characters), at most 40 characters.</summary>
        public static string Safe(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "player";
            var bad = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var c in name.Trim())
            {
                if (char.IsControl(c) || Array.IndexOf(bad, c) >= 0)
                    continue;
                sb.Append(char.IsWhiteSpace(c) ? '_' : c);
                if (sb.Length >= 40)
                    break;
            }

            var r = sb.ToString().Trim('.', '_', ' ');
            return r.Length == 0 ? "player" : r;
        }

        public static void WriteAtomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }
    }

    /// <summary>
    /// Offline-first upload queue, plain C# (no Unity objects) so it can be tested with a fake server and a fake clock.
    /// Every finished result has a file <c>X.json</c> and a sidecar <c>X.json.pending</c> (the API body). The sidecar is removed only
    /// when the server answered 200/201 (then <c>X.json.sent</c> is written). Nothing is ever deleted on an error.
    /// At most <see cref="MaxInFlight"/> requests run at once; every item backs off with full jitter; a server that answers 429/5xx
    /// repeatedly pauses the whole queue (circuit breaker). A repeat of the same client_submission_id is harmless (the backend answers 200).
    /// </summary>
    public sealed class ResultOutbox
    {
        public const int MaxInFlight = 2;
        public const double RejectedRetrySeconds = 24 * 3600;
        public const double KeepSentSeconds = 90 * 24 * 3600;

        private sealed class Item
        {
            public string path;
            public int attempts;
            public int timeouts;
            public double due;
            public bool inFlight;
        }

        private readonly Dictionary<string, Item> _items = new Dictionary<string, Item>();
        private readonly string _root;
        private readonly IResultTransport _transport;
        private readonly Func<double> _clock; // unix seconds
        private readonly System.Random _rng;
        private double _pausedUntil;
        private int _serverErrors;

        public Action<string> Log = s => Debug.Log(s);
        public Action<string> LogWarn = s => Debug.LogWarning(s);
        public Action<string> LogError = s => Debug.LogError(s);

        /// <summary>Highest number of requests that were in the air together (tests).</summary>
        public int PeakInFlight { get; private set; }
        public int InFlight { get; private set; }
        public int PendingCount => _items.Count;
        public double PausedUntil => _pausedUntil;

        public ResultOutbox(string root, IResultTransport transport, Func<double> clock, System.Random rng = null)
        {
            _root = root;
            _transport = transport;
            _clock = clock;
            _rng = rng ?? new System.Random();
        }

        public static string PendingOf(string json) => json + ".pending";
        public static string SentOf(string json) => json + ".sent";
        public static string RejectedOf(string json) => json + ".rejected";

        /// <summary>Queue one result file now (it must have its .pending sidecar).</summary>
        public void Enqueue(string jsonPath)
        {
            if (!File.Exists(PendingOf(jsonPath)) || File.Exists(SentOf(jsonPath)))
                return;
            if (!_items.TryGetValue(jsonPath, out var it))
                _items[jsonPath] = it = new Item { path = jsonPath };
            if (!it.inFlight)
                it.due = Math.Min(it.due == 0 ? double.MaxValue : it.due, _clock());
        }

        /// <summary>Finds every result file that still has to be sent (all Training ID / date folders).</summary>
        public int Scan()
        {
            if (!Directory.Exists(_root))
                return 0;
            var added = 0;
            foreach (var f in Directory.GetFiles(_root, "*.pending", SearchOption.AllDirectories))
            {
                var json = f.Substring(0, f.Length - ".pending".Length);
                if (!json.EndsWith(".json", StringComparison.Ordinal) || File.Exists(SentOf(json)) || _items.ContainsKey(json))
                    continue;
                _items[json] = new Item { path = json, due = _clock() };
                added++;
            }

            return added;
        }

        /// <summary>The network came back: every waiting item is tried soon, spread over <paramref name="spreadSeconds"/> so a room full of headsets does not hit the server together.</summary>
        public void NetworkRegained(double spreadSeconds = 20)
        {
            var now = _clock();
            _pausedUntil = Math.Min(_pausedUntil, now);
            foreach (var it in _items.Values)
            {
                if (!it.inFlight && it.due > now && !IsRejectedWaiting(it.path, now))
                    it.due = now + _rng.NextDouble() * spreadSeconds;
            }
        }

        public UploadStatus Status(string jsonPath)
        {
            if (File.Exists(SentOf(jsonPath)))
                return UploadStatus.Sent;
            if (File.Exists(RejectedOf(jsonPath)))
                return UploadStatus.Rejected;
            if (File.Exists(PendingOf(jsonPath)))
                return _items.TryGetValue(jsonPath, out var it) && it.attempts > 0 ? UploadStatus.Waiting : UploadStatus.Saved;
            return UploadStatus.None;
        }

        /// <summary>Starts the due items (never more than MaxInFlight). Call every frame or every second.</summary>
        public void Tick()
        {
            var now = _clock();
            if (now < _pausedUntil || InFlight >= MaxInFlight)
                return;
            List<Item> due = null;
            foreach (var it in _items.Values)
            {
                if (it.inFlight || it.due > now)
                    continue;
                if (IsRejectedWaiting(it.path, now))
                {
                    it.due = RejectedNext(it.path);
                    continue;
                }

                (due ?? (due = new List<Item>())).Add(it);
            }

            if (due == null)
                return;
            due.Sort((a, b) => a.due.CompareTo(b.due));
            foreach (var it in due)
            {
                if (InFlight >= MaxInFlight)
                    break;
                Start(it);
            }
        }

        private void Start(Item it)
        {
            byte[] body;
            try
            {
                body = File.ReadAllBytes(PendingOf(it.path));
            }
            catch (Exception e)
            {
                LogError("[Upload] cannot read " + PendingOf(it.path) + ": " + e.Message);
                _items.Remove(it.path);
                return;
            }

            it.inFlight = true;
            InFlight++;
            PeakInFlight = Math.Max(PeakInFlight, InFlight);
            var timeout = it.timeouts >= 2 ? 60 : 30; // slow link: wait longer instead of failing again
            _transport.Post(body, timeout, r => OnReply(it, r));
        }

        private void OnReply(Item it, UploadReply r)
        {
            it.inFlight = false;
            InFlight = Math.Max(0, InFlight - 1);
            var now = _clock();
            if (r.code == 200 || r.code == 201)
            {
                try
                {
                    File.WriteAllText(SentOf(it.path), now.ToString("0") + "\n" + r.code + " " + (r.text ?? string.Empty));
                    File.Delete(PendingOf(it.path));
                    if (File.Exists(RejectedOf(it.path)))
                        File.Delete(RejectedOf(it.path));
                }
                catch (Exception e)
                {
                    LogError("[Upload] sent, but the marker could not be written: " + e.Message);
                }

                _items.Remove(it.path);
                _serverErrors = 0;
                Log("[Upload] OK " + r.code + " " + Path.GetFileName(it.path));
                return;
            }

            if (r.code == 401 || r.code == 403)
            {
                _pausedUntil = now + 600;
                it.due = now + 600;
                LogError("[Upload] the server refused the API key (" + r.code + "): results stay on the device, paused 10 min");
                return;
            }

            var transient = r.network || r.code == 408 || r.code == 429 || r.code >= 500 || r.code == 0;
            if (!transient)
            {
                // 400 / 422 ...: the server will not take this body (for example the Training ID is not live). Keep the data, try again in a day.
                var next = now + RejectedRetrySeconds;
                try
                {
                    File.WriteAllText(RejectedOf(it.path), "code=" + r.code + "\nnext=" + next.ToString("0") + "\ntext=" + (r.text ?? string.Empty));
                }
                catch (Exception)
                {
                    // marker only
                }

                it.due = next;
                it.attempts++;
                LogError("[Upload] rejected " + r.code + " " + Path.GetFileName(it.path) + ": " + r.text + " (kept, retried in 24 h)");
                return;
            }

            it.attempts++;
            if (r.timeout)
                it.timeouts++;
            double delay;
            if (r.retryAfter > 0)
                delay = r.retryAfter;
            else
                delay = 2 + _rng.NextDouble() * Math.Min(900, 5 * Math.Pow(2, Math.Min(it.attempts, 10))); // full jitter, cap 15 min
            it.due = now + delay;
            if (r.code == 429 || r.code >= 500)
            {
                _serverErrors++;
                if (r.retryAfter > 0)
                    _pausedUntil = Math.Max(_pausedUntil, now + r.retryAfter);
                else if (_serverErrors >= 3)
                    _pausedUntil = Math.Max(_pausedUntil, now + Math.Min(900, 20 * Math.Pow(2, Math.Min(_serverErrors - 3, 6))) * (0.5 + 0.5 * _rng.NextDouble()));
            }

            LogWarn("[Upload] try " + it.attempts + " failed (" + (r.network ? (r.timeout ? "timeout" : "network") : r.code.ToString()) + "), next in " + delay.ToString("0") + " s: " + Path.GetFileName(it.path));
        }

        private static bool IsRejectedWaiting(string json, double now) => File.Exists(RejectedOf(json)) && RejectedNext(json) > now;

        private static double RejectedNext(string json)
        {
            try
            {
                foreach (var line in File.ReadAllLines(RejectedOf(json)))
                {
                    if (line.StartsWith("next=", StringComparison.Ordinal) && double.TryParse(line.Substring(5), out var v))
                        return v;
                }
            }
            catch (Exception)
            {
                // fall through
            }

            return 0;
        }

        /// <summary>Removes results that were accepted more than 90 days ago (the server has them) and empty folders.</summary>
        public int PruneSent()
        {
            if (!Directory.Exists(_root))
                return 0;
            var now = _clock();
            var n = 0;
            foreach (var f in Directory.GetFiles(_root, "*.sent", SearchOption.AllDirectories))
            {
                try
                {
                    var first = File.ReadAllLines(f)[0];
                    if (!double.TryParse(first, out var t) || now - t < KeepSentSeconds)
                        continue;
                    var json = f.Substring(0, f.Length - ".sent".Length);
                    File.Delete(json);
                    File.Delete(f);
                    n++;
                }
                catch (Exception)
                {
                    // leave it
                }
            }

            foreach (var d in Directory.GetDirectories(_root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (Directory.Exists(d) && Directory.GetFileSystemEntries(d).Length == 0)
                        Directory.Delete(d);
                }
                catch (Exception)
                {
                    // leave it
                }
            }

            return n;
        }
    }
}
