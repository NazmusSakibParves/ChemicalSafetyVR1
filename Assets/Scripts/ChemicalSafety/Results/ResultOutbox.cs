using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

    /// <summary>Where result files live: results/&lt;TrainingID&gt;/&lt;yyyy-MM-dd&gt;/&lt;Name&gt;_&lt;yyyyMMdd-HHmmss&gt;.json (+ .csv, .pending, .sent ...)</summary>
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

        /// <summary>The Excel copy of a result file: same folder, same name, .csv.</summary>
        public static string CsvOf(string jsonPath) => Path.ChangeExtension(jsonPath, ".csv");

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

        /// <summary>Writes the text to a .tmp file and swaps it in (File.Replace keeps the old copy until the new one is complete). <paramref name="bom"/> = UTF-8 with BOM (Excel).</summary>
        public static void WriteAtomic(string path, string text, bool bom = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(bom));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, null);
                    return;
                }
                catch (Exception)
                {
                    if (!File.Exists(tmp))
                        throw;
                    // platform without File.Replace: the old way
                }

                File.Delete(path);
            }

            File.Move(tmp, path);
        }

        /// <summary>After a crash in the middle of a write a .tmp file can be left behind: it is the newest complete copy when the target is missing, else it is stale.</summary>
        public static int RecoverTemps(string root)
        {
            if (!Directory.Exists(root))
                return 0;
            var n = 0;
            foreach (var tmp in Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories))
            {
                try
                {
                    var target = tmp.Substring(0, tmp.Length - ".tmp".Length);
                    if (File.Exists(target))
                        File.Delete(tmp);
                    else
                    {
                        File.Move(tmp, target);
                        n++;
                    }
                }
                catch (Exception)
                {
                    // leave it
                }
            }

            return n;
        }
    }

    /// <summary>
    /// Offline-first upload queue, plain C# (no Unity objects) so it can be tested with a fake server and a fake clock.
    /// Every finished result has a file <c>X.json</c> and a sidecar <c>X.json.pending</c> (the API body). Several pending files are sent
    /// together in ONE request, a JSON array built from the .pending bytes unchanged. The sidecar is removed only when the server accepted
    /// that result (then <c>X.json.sent</c> is written). Nothing is ever deleted on an error.
    /// The batch size starts at 1 and grows by one after every accepted batch, up to <see cref="MaxBatchItems"/> persons or
    /// <see cref="MaxBatchBytes"/> bytes; a batch that times out, is too large (413) or is refused shrinks it, and one bad result can never
    /// hold back the others (a failed batch is split until the bad one is alone). One request at a time, jittered back-off, circuit breaker.
    /// A repeat of the same client_submission_id is harmless (the backend answers 200).
    /// </summary>
    public sealed class ResultOutbox
    {
        public const int MaxInFlight = 1;
        public const int MaxBatchItems = 5;
        public const long MaxBatchBytes = 200 * 1024;
        public const double RejectedRetrySeconds = 24 * 3600;
        public const double KeepSentSeconds = 90 * 24 * 3600;
        private const string BatchStateFile = "_batch.txt";

        private sealed class Item
        {
            public string path;
            public string id;
            public int attempts;
            public int timeouts;
            public double due;
            public bool inFlight;
            public bool solo; // one result of a failed batch: it is tried alone
        }

        private readonly Dictionary<string, Item> _items = new Dictionary<string, Item>();
        private readonly string _root;
        private readonly IResultTransport _transport;
        private readonly Func<double> _clock; // unix seconds
        private readonly System.Random _rng;
        private double _pausedUntil;
        private int _serverErrors;
        private int _limit = 1;                  // persons in the next batch (slow start)
        private int _ceiling = MaxBatchItems;    // the most that worked on this link / server
        private int _okAtCeiling;

        public Action<string> Log = s => Debug.Log(s);
        public Action<string> LogWarn = s => Debug.LogWarning(s);
        public Action<string> LogError = s => Debug.LogError(s);

        /// <summary>Highest number of requests that were in the air together (tests).</summary>
        public int PeakInFlight { get; private set; }
        public int InFlight { get; private set; }
        public int PendingCount => _items.Count;
        public double PausedUntil => _pausedUntil;
        public int BatchLimit => _limit;
        public int BatchCeiling => _ceiling;
        /// <summary>Largest number of results that went in one request (tests).</summary>
        public int PeakBatch { get; private set; }

        public ResultOutbox(string root, IResultTransport transport, Func<double> clock, System.Random rng = null)
        {
            _root = root;
            _transport = transport;
            _clock = clock;
            _rng = rng ?? new System.Random();
            LoadBatchState();
        }

        public static string PendingOf(string json) => json + ".pending";
        public static string PartialOf(string json) => json + ".partial";
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

        /// <summary>Starts one batch of the due items (never more than one request at a time). Call every frame or every second.</summary>
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
            due.Sort((a, b) =>
            {
                var c = a.due.CompareTo(b.due);
                return c != 0 ? c : string.CompareOrdinal(a.path, b.path);
            });
            StartBatch(due, now);
        }

        private void StartBatch(List<Item> due, double now)
        {
            var batch = new List<Item>(MaxBatchItems);
            var bodies = new List<byte[]>(MaxBatchItems);
            long bytes = 0;
            foreach (var it in due)
            {
                if (batch.Count >= _limit)
                    break;
                if (it.solo && batch.Count > 0)
                    continue; // it goes alone, on its own turn
                byte[] body;
                try
                {
                    body = Trim(File.ReadAllBytes(PendingOf(it.path)));
                    if (body.Length == 0 || body[0] != (byte)'{')
                        throw new InvalidDataException("not a JSON object");
                    it.id = ExtractId(body);
                }
                catch (Exception e)
                {
                    LogError("[Upload] cannot read " + PendingOf(it.path) + ": " + e.Message + " (kept, looked at again in 10 min)");
                    it.due = now + 600; // never dropped from the queue
                    continue;
                }

                if (batch.Count > 0 && bytes + body.Length > MaxBatchBytes)
                    break;
                batch.Add(it);
                bodies.Add(body);
                bytes += body.Length;
                if (it.solo)
                    break;
            }

            if (batch.Count == 0)
                return;
            var payload = Join(bodies, bytes);
            var timeout = (int)Math.Min(90, 30 + payload.Length / 10240); // bigger body: longer wait
            foreach (var it in batch)
            {
                it.inFlight = true;
                if (it.timeouts >= 2)
                    timeout = 90; // slow link: wait longer instead of failing again
            }

            InFlight++;
            PeakInFlight = Math.Max(PeakInFlight, InFlight);
            PeakBatch = Math.Max(PeakBatch, batch.Count);
            _transport.Post(payload, timeout, r => OnReply(batch, r));
        }

        // ------------------------------------------------------------------ reply handling

        private void OnReply(List<Item> batch, UploadReply r)
        {
            foreach (var it in batch)
                it.inFlight = false;
            InFlight = Math.Max(0, InFlight - 1);
            var now = _clock();
            var n = batch.Count;

            if (r.code == 200 || r.code == 201 || r.code == 207)
            {
                var failed = ParseFailedIds(r.text);
                var looksFailed = failed == null && LooksFailed(r.text);
                if (r.code == 207 && failed == null)
                    looksFailed = true; // multi-status without a readable per-result list: trust nothing
                if (!looksFailed)
                {
                    var okCount = 0;
                    foreach (var it in batch)
                    {
                        if (failed != null && it.id != null && failed.Contains(it.id))
                            ResultFailedInBatch(it, n, r, now);
                        else
                        {
                            MarkSent(it, r, now);
                            okCount++;
                        }
                    }

                    _serverErrors = 0;
                    if (okCount > 0)
                        BatchAccepted(n);
                    else
                        ShrinkAfterFailure(n, false);
                    return;
                }

                // a 2xx whose text reports errors: the results are NOT marked as sent
                NonTransientFailure(batch, r, now, n);
                return;
            }

            if (r.code == 401 || r.code == 403)
            {
                _pausedUntil = now + 600;
                foreach (var it in batch)
                    it.due = now + 600;
                LogError("[Upload] the server refused the API key (" + r.code + "): results stay on the device, paused 10 min");
                return;
            }

            if (r.code == 413)
            {
                NonTransientFailure(batch, r, now, n);
                return;
            }

            var transient = r.network || r.code == 408 || r.code == 429 || r.code >= 500 || r.code == 0;
            if (!transient)
            {
                NonTransientFailure(batch, r, now, n);
                return;
            }

            // network error, timeout, 408, 429, 5xx: keep everything, wait with jitter
            var maxAttempts = 0;
            foreach (var it in batch)
            {
                it.attempts++;
                if (r.timeout)
                    it.timeouts++;
                maxAttempts = Math.Max(maxAttempts, it.attempts);
            }

            double delay;
            if (r.retryAfter > 0)
                delay = r.retryAfter;
            else
                delay = 2 + _rng.NextDouble() * Math.Min(900, 5 * Math.Pow(2, Math.Min(maxAttempts, 10))); // full jitter, cap 15 min
            foreach (var it in batch)
                it.due = now + delay;
            if (r.code == 429 || r.code >= 500)
            {
                _serverErrors++;
                if (r.retryAfter > 0)
                    _pausedUntil = Math.Max(_pausedUntil, now + r.retryAfter);
                else if (_serverErrors >= 3)
                    _pausedUntil = Math.Max(_pausedUntil, now + Math.Min(900, 20 * Math.Pow(2, Math.Min(_serverErrors - 3, 6))) * (0.5 + 0.5 * _rng.NextDouble()));
            }

            // a timeout or a gateway error with several results: the body may be too much for this link, send fewer next time
            if (n > 1 && (r.timeout || r.code == 502 || r.code == 503 || r.code == 504))
                ShrinkAfterFailure(n, true);
            LogWarn("[Upload] batch of " + n + " failed (" + (r.network ? (r.timeout ? "timeout" : "network") : r.code.ToString()) + "), next in " + delay.ToString("0") + " s");
        }

        /// <summary>The server will not take this body (400 / 413 / 422 ... or a 2xx that reports errors). Several results: split and try again at once. One result: keep it, try again in 24 h.</summary>
        private void NonTransientFailure(List<Item> batch, UploadReply r, double now, int n)
        {
            if (n > 1)
            {
                ShrinkAfterFailure(n, r.code == 413);
                foreach (var it in batch)
                    it.due = now + 1;
                LogWarn("[Upload] batch of " + n + " refused (" + r.code + "): split and tried again (nothing is dropped)");
                return;
            }

            var one = batch[0];
            if (r.code == 413)
                LogError("[Upload] 413 payload too large for a SINGLE result: the server body limit must be raised (>= 1 MB). Result kept on the device.");
            MarkRejected(one, r, now);
        }

        private void ResultFailedInBatch(Item it, int n, UploadReply r, double now)
        {
            if (n > 1)
            {
                it.solo = true; // the server named this one: it is sent alone next, the others are already done
                it.due = now + 1;
                LogWarn("[Upload] the server did not accept " + Path.GetFileName(it.path) + " inside the batch: it is sent alone");
                return;
            }

            MarkRejected(it, r, now);
        }

        private void MarkRejected(Item it, UploadReply r, double now)
        {
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
        }

        private void MarkSent(Item it, UploadReply r, double now)
        {
            try
            {
                File.WriteAllText(SentOf(it.path), now.ToString("0") + "\n" + r.code + " " + (r.text ?? string.Empty));
                File.Delete(PendingOf(it.path));
                if (File.Exists(RejectedOf(it.path)))
                    File.Delete(RejectedOf(it.path));
                if (File.Exists(PartialOf(it.path)))
                    File.Delete(PartialOf(it.path));
            }
            catch (Exception e)
            {
                LogError("[Upload] sent, but the marker could not be written: " + e.Message);
            }

            _items.Remove(it.path);
            Log("[Upload] OK " + r.code + " " + Path.GetFileName(it.path));
        }

        // ------------------------------------------------------------------ batch size (slow start, shrink, remember)

        private void BatchAccepted(int n)
        {
            if (n >= _ceiling)
            {
                if (++_okAtCeiling >= 5 && _ceiling < MaxBatchItems)
                {
                    _ceiling++; // the link has been fine for a while: allow one more
                    _okAtCeiling = 0;
                }
            }

            if (n >= _limit && _limit < _ceiling)
                _limit++;
            SaveBatchState();
        }

        private void ShrinkAfterFailure(int n, bool lowerCeiling)
        {
            if (n <= 1)
                return;
            _limit = Math.Max(1, n / 2);
            if (lowerCeiling)
            {
                _ceiling = Math.Max(1, n - 1);
                _okAtCeiling = 0;
            }

            _limit = Math.Min(_limit, _ceiling);
            SaveBatchState();
        }

        private void LoadBatchState()
        {
            try
            {
                var f = Path.Combine(_root, BatchStateFile);
                if (!File.Exists(f))
                    return;
                foreach (var line in File.ReadAllLines(f))
                {
                    if (line.StartsWith("limit=", StringComparison.Ordinal) && int.TryParse(line.Substring(6), out var l))
                        _limit = l;
                    else if (line.StartsWith("ceiling=", StringComparison.Ordinal) && int.TryParse(line.Substring(8), out var c))
                        _ceiling = c;
                }

                _ceiling = Math.Max(1, Math.Min(MaxBatchItems, _ceiling));
                _limit = Math.Max(1, Math.Min(_ceiling, _limit));
            }
            catch (Exception)
            {
                _limit = 1;
                _ceiling = MaxBatchItems;
            }
        }

        private void SaveBatchState()
        {
            try
            {
                Directory.CreateDirectory(_root);
                File.WriteAllText(Path.Combine(_root, BatchStateFile), "limit=" + _limit + "\nceiling=" + _ceiling + "\n");
            }
            catch (Exception)
            {
                // a hint only
            }
        }

        // ------------------------------------------------------------------ helpers (public: tests)

        private static byte[] Trim(byte[] b)
        {
            var s = 0;
            var e = b.Length;
            while (s < e && IsWs(b[s]))
                s++;
            while (e > s && IsWs(b[e - 1]))
                e--;
            if (s == 0 && e == b.Length)
                return b;
            var r = new byte[e - s];
            Buffer.BlockCopy(b, s, r, 0, r.Length);
            return r;
        }

        private static bool IsWs(byte c) => c == (byte)' ' || c == (byte)'\n' || c == (byte)'\r' || c == (byte)'\t' || c == 0xEF || c == 0xBB || c == 0xBF;

        private static readonly byte[] BodyHead = Encoding.ASCII.GetBytes("{\"submissions\":[");
        private static readonly byte[] BodyTail = Encoding.ASCII.GetBytes("]}");

        /// <summary>{"submissions":[ body1, body2, ... ]}: the shape the batch endpoint demands (1 to 25 results); the bytes of the .pending files are unchanged.</summary>
        public static byte[] Join(List<byte[]> bodies, long total)
        {
            var r = new byte[BodyHead.Length + total + Math.Max(0, bodies.Count - 1) + BodyTail.Length];
            var p = 0;
            Buffer.BlockCopy(BodyHead, 0, r, p, BodyHead.Length);
            p += BodyHead.Length;
            for (var i = 0; i < bodies.Count; i++)
            {
                if (i > 0)
                    r[p++] = (byte)',';
                Buffer.BlockCopy(bodies[i], 0, r, p, bodies[i].Length);
                p += bodies[i].Length;
            }

            Buffer.BlockCopy(BodyTail, 0, r, p, BodyTail.Length);
            return r;
        }

        private static string ExtractId(byte[] body)
        {
            var s = Encoding.UTF8.GetString(body, 0, Math.Min(body.Length, 600));
            var m = Regex.Match(s, "\"client_submission_id\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static readonly Regex Chunk = new Regex("\\{[^{}]*\\}");
        private static readonly Regex IdIn = new Regex("\"client_submission_id\"\\s*:\\s*\"([^\"]+)\"");
        private static readonly Regex FailFlag = new Regex(
            "\"(success|ok|accepted|created|saved|stored)\"\\s*:\\s*false" +
            "|\"(status|result|state)\"\\s*:\\s*\"(error|failed|failure|rejected|invalid|unprocessable)\"" +
            "|\"(error|errors|message_error)\"\\s*:\\s*(\\[\\s*[^\\]\\s]|\"(?!(none|null|false|ok|no)\")[^\"]|\\{)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Per-result answers of a batch (any flat object that names a client_submission_id). Returns the ids reported as failed
        /// (an empty set when every id is fine) or null when the text has no per-result list at all.
        /// </summary>
        public static HashSet<string> ParseFailedIds(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            HashSet<string> failed = null;
            var seen = false;
            foreach (Match m in Chunk.Matches(text))
            {
                var idm = IdIn.Match(m.Value);
                if (!idm.Success)
                    continue;
                seen = true;
                if (FailFlag.IsMatch(m.Value))
                    (failed ?? (failed = new HashSet<string>())).Add(idm.Groups[1].Value);
            }

            return !seen ? null : failed ?? new HashSet<string>();
        }

        /// <summary>A 2xx answer whose text reports an error somewhere (without naming which result): never treated as success.</summary>
        public static bool LooksFailed(string text) => !string.IsNullOrEmpty(text) && FailFlag.IsMatch(text);

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
                    var csv = ResultPaths.CsvOf(json);
                    if (File.Exists(csv))
                        File.Delete(csv);
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
