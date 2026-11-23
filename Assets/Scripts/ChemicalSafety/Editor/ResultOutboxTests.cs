using System;
using System.Collections.Generic;
using System.IO;
using ChemicalSafety.Results;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.EditorTools
{
    /// <summary>Menu "Chemical Safety/Presentable/Run Result Sync Test": the offline-first queue against a fake server and a fake clock (no Play mode, no network).</summary>
    public static class ResultOutboxTests
    {
        private sealed class FakeServer : IResultTransport
        {
            public readonly HashSet<string> Db = new HashSet<string>(); // stored client_submission_ids (unique, like the backend index)
            public int Posts;
            public int Inserts;
            public int MaxPersonsSeen;
            public long MaxBytesSeen;
            public readonly List<(byte[] body, Action<UploadReply> done, double at)> Open = new List<(byte[], Action<UploadReply>, double)>();
            public Func<int, string[], UploadReply?> Fault = (n, ids) => null; // (post number, ids in the batch) -> forced reply, null = behave
            public bool InsertThenLoseResponse; // saves, but the answer never arrives (timeout)
            public int BodyLimitBytes = int.MaxValue; // like nginx client_max_body_size: 413 above it
            public HashSet<string> BadIds = new HashSet<string>(); // results the server refuses
            public bool PerItemAnswers = true;           // false: one answer for the whole batch (422 when it holds a bad id, nothing stored)
            public Func<double> Clock;
            public double Latency = 1;

            public void Post(byte[] body, int timeoutSeconds, Action<UploadReply> done)
            {
                Posts++;
                Open.Add((body, done, Clock() + Latency));
            }

            public void Complete(double now)
            {
                for (var i = Open.Count - 1; i >= 0; i--)
                {
                    if (Open[i].at > now)
                        continue;
                    var (body, done, _) = Open[i];
                    Open.RemoveAt(i);
                    var ids = Ids(body);
                    MaxPersonsSeen = Math.Max(MaxPersonsSeen, ids.Length);
                    MaxBytesSeen = Math.Max(MaxBytesSeen, body.Length);
                    if (body.Length > BodyLimitBytes)
                    {
                        done(new UploadReply { code = 413, text = "Payload Too Large" });
                        continue;
                    }

                    var forced = Fault(Posts, ids);
                    if (forced.HasValue && !InsertThenLoseResponse)
                    {
                        done(forced.Value);
                        continue;
                    }

                    var anyBad = false;
                    foreach (var id in ids)
                        anyBad |= BadIds.Contains(id);
                    var sb = new System.Text.StringBuilder("{\"results\":[");
                    for (var k = 0; k < ids.Length; k++)
                    {
                        var bad = BadIds.Contains(ids[k]);
                        if (!bad && (PerItemAnswers || !anyBad) && Db.Add(ids[k]))
                            Inserts++;
                        sb.Append(k == 0 ? "" : ",").Append("{\"client_submission_id\":\"").Append(ids[k]).Append("\",\"status\":\"").Append(bad ? "error" : "created").Append("\"}");
                    }

                    sb.Append("]}");
                    if (InsertThenLoseResponse && forced.HasValue)
                    {
                        done(forced.Value);
                        continue;
                    }

                    if (!PerItemAnswers && anyBad)
                    {
                        done(new UploadReply { code = 422, text = "one result is invalid" });
                        continue;
                    }

                    done(new UploadReply { code = 201, text = sb.ToString() });
                }
            }

            private static string[] Ids(byte[] body)
            {
                var s = System.Text.Encoding.UTF8.GetString(body);
                var list = new List<string>();
                var from = 0;
                while (true)
                {
                    var k = s.IndexOf("client_submission_id", from, StringComparison.Ordinal);
                    if (k < 0)
                        break;
                    var a = s.IndexOf('"', s.IndexOf(':', k) + 1) + 1;
                    list.Add(s.Substring(a, s.IndexOf('"', a) - a));
                    from = a;
                }

                return list.ToArray();
            }
        }

        /// <summary>Lets a test look at every body that goes to the (fake) server.</summary>
        private sealed class BodyTap : IResultTransport
        {
            private readonly IResultTransport _inner;
            private readonly Action<byte[]> _tap;

            public BodyTap(IResultTransport inner, Action<byte[]> tap)
            {
                _inner = inner;
                _tap = tap;
            }

            public void Post(byte[] body, int timeoutSeconds, Action<UploadReply> done)
            {
                _tap(body);
                _inner.Post(body, timeoutSeconds, done);
            }
        }

        /// <summary>Small CSV reader (quotes, doubled quotes, commas and new lines inside cells) for the export test.</summary>
        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new System.Text.StringBuilder();
            var q = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (q)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                            q = false;
                    }
                    else
                        cell.Append(c);
                }
                else if (c == '"')
                    q = true;
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else if (c != '\r')
                    cell.Append(c);
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        private static int _fail;

        private static void Check(bool ok, string what)
        {
            if (!ok)
            {
                _fail++;
                Debug.LogError("[SYNC TEST] FAIL " + what);
            }
            else
                Debug.Log("[SYNC TEST] ok " + what);
        }

        private static string MakeResult(string root, int training, string name, DateTime when, out string id, int padBytes = 0)
        {
            ResultPaths.ForgetAll();
            var s = new Submission { training_id = training, participant_name = name, startedLocal = when };
            id = s.client_submission_id;
            var json = ResultPaths.For(root, s);
            ResultPaths.WriteAtomic(json, "{\"client_submission_id\": \"" + id + "\"}");
            var pad = padBytes > 0 ? ", \"pad\": \"" + new string('x', padBytes) + "\"" : string.Empty;
            ResultPaths.WriteAtomic(ResultOutbox.PendingOf(json), "{\"client_submission_id\": \"" + id + "\", \"training_id\": " + training + pad + "}\n");
            return json;
        }

        private static ResultOutbox NewBox(string root, FakeServer srv, Func<double> clock, int seed) =>
            new ResultOutbox(root, srv, clock, new System.Random(seed)) { Log = x => { }, LogWarn = x => { }, LogError = x => { } };

        private static bool IsSent(string json) => File.Exists(ResultOutbox.SentOf(json)) && !File.Exists(ResultOutbox.PendingOf(json));

        private static string TempDir(string tag)
        {
            var d = Path.Combine(Path.GetTempPath(), "cs_sync_" + tag + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(d);
            return d;
        }

        private static void Run(ResultOutbox box, FakeServer srv, ref double now, double seconds, double step = 1)
        {
            var end = now + seconds;
            while (now < end)
            {
                now += step;
                srv.Complete(now);
                box.Tick();
            }
        }

        [MenuItem("Chemical Safety/Presentable/Run Result Sync Test")]
        public static void Main()
        {
            _fail = 0;
            var root = Path.Combine(Path.GetTempPath(), "cs_sync_test_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(root);
            try
            {
                // ---- layout
                ResultPaths.ForgetAll();
                var when = new DateTime(2026, 11, 16, 14, 5, 9);
                var s1 = new Submission { training_id = 7, participant_name = "Rahim: Uddin/<x>", startedLocal = when };
                var p1 = ResultPaths.For(root, s1);
                var rel = p1.Substring(root.Length + 1).Replace('\\', '/');
                Check(rel == "7/2026-11-16/Rahim_Uddinx_20261116-140509.json", "layout training/date/name_time (" + rel + ")");
                var s2 = new Submission { training_id = 7, participant_name = "Rahim: Uddin/<x>", startedLocal = when };
                Directory.CreateDirectory(Path.GetDirectoryName(p1));
                File.WriteAllText(p1, "x");
                var p2 = ResultPaths.For(root, s2);
                Check(p2 != p1 && p2.EndsWith(".json"), "same name + second keeps both files");
                var bn = ResultPaths.Safe("রহিম উদ্দিন");
                Check(bn == "রহিম_উদ্দিন", "Bangla name kept in the file name (" + bn + ")");
                Check(ResultPaths.Safe("   ") == "player" && ResultPaths.Safe(new string('a', 80)).Length == 40, "empty -> player, long name cut to 40");
                File.Delete(p1);
                Directory.Delete(Path.Combine(root, "7"), true);

                // ---- 500 results arrive together (internet came back): all sent once, never more than 2 in the air
                var now = 1_800_000_000.0;
                var srv = new FakeServer { Clock = () => now };
                var box = new ResultOutbox(root, srv, () => now, new System.Random(1)) { Log = s => { }, LogWarn = s => { }, LogError = s => { } };
                var files = new List<string>();
                for (var i = 0; i < 500; i++)
                    files.Add(MakeResult(root, 1 + i % 5, "P" + i, when.AddDays(i % 3), out _));
                Check(box.Scan() == 500, "scan finds 500 pending files in all training/date folders");
                box.NetworkRegained(20);
                Run(box, srv, ref now, 1500);
                var sent = 0;
                foreach (var f in files)
                    if (File.Exists(ResultOutbox.SentOf(f)) && !File.Exists(ResultOutbox.PendingOf(f)))
                        sent++;
                Check(sent == 500 && srv.Db.Count == 500 && srv.Inserts == 500, "500 sent, 500 stored, no duplicate (sent=" + sent + " db=" + srv.Db.Count + ")");
                Check(box.PeakInFlight <= ResultOutbox.MaxInFlight, "never more than 1 request in flight (peak " + box.PeakInFlight + ")");

                // ---- network errors then success; and "saved but the answer was lost" must not insert twice
                srv = new FakeServer { Clock = () => now };
                box = new ResultOutbox(root, srv, () => now, new System.Random(2)) { Log = s => { }, LogWarn = s => { }, LogError = s => { } };
                var f1 = MakeResult(root, 1, "Fail", when, out var id1);
                box.Enqueue(f1);
                var tries = 0;
                srv.InsertThenLoseResponse = true;
                srv.Fault = (n, id) => ++tries <= 2 ? new UploadReply { network = true, timeout = true } : (UploadReply?)null;
                Run(box, srv, ref now, 600);
                Check(File.Exists(ResultOutbox.SentOf(f1)) && srv.Db.Count == 1 && srv.Inserts == 1, "lost answers x2 then ok: stored exactly once (posts " + srv.Posts + ", inserts " + srv.Inserts + ")");

                // ---- 429 with Retry-After: nothing is posted before it, queue pauses
                srv = new FakeServer { Clock = () => now };
                box = new ResultOutbox(root, srv, () => now, new System.Random(3)) { Log = s => { }, LogWarn = s => { }, LogError = s => { } };
                var f2 = MakeResult(root, 1, "Busy", when, out _);
                var f3 = MakeResult(root, 1, "Busy2", when, out _);
                box.Enqueue(f2);
                box.Enqueue(f3);
                var first = true;
                srv.Fault = (n, id) => { if (first) { first = false; return new UploadReply { code = 429, retryAfter = 60 }; } return null; };
                Run(box, srv, ref now, 3);
                var postsAfter3 = srv.Posts;
                Run(box, srv, ref now, 50);
                Check(srv.Posts == postsAfter3, "Retry-After 60: no new request for 50 s (posts " + srv.Posts + ")");
                Run(box, srv, ref now, 80);
                Check(File.Exists(ResultOutbox.SentOf(f2)) && File.Exists(ResultOutbox.SentOf(f3)), "both sent after the pause");

                // ---- 422 (Training ID not live): kept, marked rejected, not retried before 24 h, retried after
                srv = new FakeServer { Clock = () => now };
                box = new ResultOutbox(root, srv, () => now, new System.Random(4)) { Log = s => { }, LogWarn = s => { }, LogError = s => { } };
                var f4 = MakeResult(root, 99, "Rej", when, out _);
                box.Enqueue(f4);
                var reject = true;
                srv.Fault = (n, id) => reject ? new UploadReply { code = 422, text = "training not live" } : (UploadReply?)null;
                Run(box, srv, ref now, 10);
                Check(File.Exists(ResultOutbox.RejectedOf(f4)) && File.Exists(ResultOutbox.PendingOf(f4)) && box.Status(f4) == UploadStatus.Rejected, "422: data kept, marked rejected");
                var p0 = srv.Posts;
                Run(box, srv, ref now, 3600 * 5, 10);
                Check(srv.Posts == p0, "422: not retried within 5 h");
                reject = false;
                Run(box, srv, ref now, 3600 * 20, 30);
                Check(File.Exists(ResultOutbox.SentOf(f4)) && !File.Exists(ResultOutbox.RejectedOf(f4)), "422: retried after 24 h, now accepted");

                // ---- 401: paused, data kept
                srv = new FakeServer { Clock = () => now };
                box = new ResultOutbox(root, srv, () => now, new System.Random(5)) { Log = s => { }, LogWarn = s => { }, LogError = s => { } };
                var f5 = MakeResult(root, 1, "Key", when, out _);
                box.Enqueue(f5);
                srv.Fault = (n, id) => new UploadReply { code = 401 };
                Run(box, srv, ref now, 30);
                Check(File.Exists(ResultOutbox.PendingOf(f5)) && box.PausedUntil > now, "401: data kept, queue paused");

                // ---- crash window: a .pending file with no queue entry (app restarted) is found by Scan and sent
                srv = new FakeServer { Clock = () => now };
                box = new ResultOutbox(root, srv, () => now, new System.Random(6)) { Log = s => { }, LogWarn = s => { }, LogError = s => { } };
                var f6 = MakeResult(root, 3, "Crash", when, out _);
                Check(box.Scan() >= 1, "restart: pending file found again");
                Run(box, srv, ref now, 30);
                Check(File.Exists(ResultOutbox.SentOf(f6)), "restart: sent");

                // ================= BATCH TESTS =================
                var fresh = TempDir("batch");
                try
                {
                    // ---- 100 results through slow start: far fewer calls, never more than 5 persons, state remembered
                    now = 1_800_100_000.0;
                    srv = new FakeServer { Clock = () => now };
                    box = NewBox(fresh, srv, () => now, 11);
                    var many = new List<string>();
                    for (var i = 0; i < 100; i++)
                        many.Add(MakeResult(fresh, 1 + i % 4, "B" + i, when.AddDays(i % 3), out _));
                    box.Scan();
                    box.NetworkRegained(5);
                    Run(box, srv, ref now, 3000);
                    var all = true;
                    foreach (var f in many)
                        all &= IsSent(f);
                    Check(all && srv.Db.Count == 100 && srv.Inserts == 100, "100 results: all sent exactly once through batches (posts " + srv.Posts + ")");
                    Check(srv.Posts <= 40 && srv.Posts < 100, "far fewer calls than results: " + srv.Posts + " calls for 100 results");
                    Check(srv.MaxPersonsSeen <= ResultOutbox.MaxBatchItems && box.PeakBatch == srv.MaxPersonsSeen, "never more than 5 persons in a batch (max " + srv.MaxPersonsSeen + ")");
                    Check(File.Exists(Path.Combine(fresh, "_batch.txt")), "the learned batch size is saved");
                    var box2 = NewBox(fresh, srv, () => now, 12);
                    Check(box2.BatchLimit == box.BatchLimit && box2.BatchCeiling == box.BatchCeiling, "restart: the batch size is remembered (limit " + box2.BatchLimit + ")");

                    // ---- first batch is ONE person (the server limit is unknown), then it grows
                    now = 1_800_200_000.0;
                    var d2 = TempDir("slow");
                    srv = new FakeServer { Clock = () => now };
                    var sizes = new List<int>();
                    srv.Fault = (n, ids) => { sizes.Add(ids.Length); return null; };
                    box = NewBox(d2, srv, () => now, 13);
                    for (var i = 0; i < 12; i++)
                        MakeResult(d2, 2, "S" + i, when, out _);
                    box.Scan();
                    box.NetworkRegained(1);
                    Run(box, srv, ref now, 300);
                    Check(sizes.Count > 3 && sizes[0] == 1 && sizes[1] == 2 && sizes[2] == 3, "slow start 1, 2, 3 ... (" + string.Join(",", sizes) + ")");
                    try { Directory.Delete(d2, true); } catch (Exception) { }

                    // ---- body = [ a, b, c ] built from the .pending text unchanged
                    now = 1_800_300_000.0;
                    var d3 = TempDir("body");
                    srv = new FakeServer { Clock = () => now };
                    byte[] seen = null;
                    string[] pendingTexts = null;
                    var tap = new BodyTap(srv, b =>
                    {
                        if (seen == null && System.Text.Encoding.UTF8.GetString(b).Split(new[] { "client_submission_id" }, StringSplitOptions.None).Length - 1 >= 3)
                            seen = b;
                    });
                    box = new ResultOutbox(d3, tap, () => now, new System.Random(14)) { Log = x => { }, LogWarn = x => { }, LogError = x => { } };
                    var three = new List<string>();
                    for (var i = 0; i < 20; i++)
                        three.Add(MakeResult(d3, 3, "J" + i, when, out _));
                    var texts = new List<string>();
                    foreach (var f in three)
                        texts.Add(File.ReadAllText(ResultOutbox.PendingOf(f)).Trim());
                    pendingTexts = texts.ToArray();
                    box.Scan();
                    box.NetworkRegained(1);
                    Run(box, srv, ref now, 300);
                    var okBody = seen != null;
                    if (okBody)
                    {
                        var text = System.Text.Encoding.UTF8.GetString(seen);
                        okBody = text.StartsWith("{\"submissions\":[", StringComparison.Ordinal) && text.EndsWith("]}", StringComparison.Ordinal);
                        var found = 0;
                        foreach (var t in pendingTexts)
                            if (text.Contains(t))
                                found++;
                        okBody &= found >= 3;
                    }

                    Check(okBody, "a batch body is {\"submissions\":[...]} and holds the .pending texts unchanged");
                    try { Directory.Delete(d3, true); } catch (Exception) { }

                    // ---- body limit (413): shrink, nothing is lost, nothing is marked rejected, no batch above 200 KB
                    now = 1_800_400_000.0;
                    var d4 = TempDir("limit");
                    srv = new FakeServer { Clock = () => now, BodyLimitBytes = 130 * 1024 };
                    box = NewBox(d4, srv, () => now, 15);
                    var big = new List<string>();
                    for (var i = 0; i < 12; i++)
                        big.Add(MakeResult(d4, 4, "G" + i, when, out _, 60 * 1024));
                    box.Scan();
                    box.NetworkRegained(1);
                    Run(box, srv, ref now, 1500);
                    all = true;
                    var rej = 0;
                    foreach (var f in big)
                    {
                        all &= IsSent(f);
                        if (File.Exists(ResultOutbox.RejectedOf(f)))
                            rej++;
                    }

                    Check(all && rej == 0 && srv.Db.Count == 12, "413 above the server body limit: every result still arrives, none rejected (posts " + srv.Posts + ")");
                    Check(srv.MaxBytesSeen <= 200 * 1024 + 64, "a batch never exceeds 200 KB (" + srv.MaxBytesSeen + " bytes)");
                    try { Directory.Delete(d4, true); } catch (Exception) { }

                    // ---- one bad result: with per-result answers AND with a single answer per batch, the others arrive and only the bad one waits
                    foreach (var perItem in new[] { true, false })
                    {
                        now = 1_800_500_000.0;
                        var d5 = TempDir("bad");
                        srv = new FakeServer { Clock = () => now, PerItemAnswers = perItem };
                        box = NewBox(d5, srv, () => now, 16);
                        var nine = new List<string>();
                        string badId = null;
                        for (var i = 0; i < 9; i++)
                        {
                            nine.Add(MakeResult(d5, 5, "Q" + i, when, out var idq));
                            if (i == 4)
                                badId = idq;
                        }

                        srv.BadIds.Add(badId);
                        box.Scan();
                        box.NetworkRegained(1);
                        Run(box, srv, ref now, 600);
                        var good = 0;
                        var waiting = 0;
                        foreach (var f in nine)
                        {
                            if (IsSent(f))
                                good++;
                            else if (File.Exists(ResultOutbox.PendingOf(f)) && File.Exists(ResultOutbox.RejectedOf(f)))
                                waiting++;
                        }

                        Check(good == 8 && waiting == 1 && srv.Db.Count == 8, "one bad result (" + (perItem ? "per-result answers" : "one answer per batch") + "): 8 sent, the bad one kept and marked (good " + good + ", kept " + waiting + ")");
                        try { Directory.Delete(d5, true); } catch (Exception) { }
                    }

                    // ---- a 200 whose text says "error" is never taken as success
                    now = 1_800_600_000.0;
                    var d6 = TempDir("err200");
                    srv = new FakeServer { Clock = () => now };
                    srv.Fault = (n, ids) => new UploadReply { code = 200, text = "{\"success\":false,\"errors\":[\"invalid body\"]}" };
                    box = NewBox(d6, srv, () => now, 17);
                    var f200 = MakeResult(d6, 6, "E", when, out _);
                    box.Enqueue(f200);
                    Run(box, srv, ref now, 30);
                    Check(!File.Exists(ResultOutbox.SentOf(f200)) && File.Exists(ResultOutbox.PendingOf(f200)), "200 with an error text: not marked sent, data kept");
                    Check(ResultOutbox.ParseFailedIds("{\"errors\":[]}") == null && !ResultOutbox.LooksFailed("{\"errors\":[],\"error\":null,\"success\":true}"), "empty error lists and null are not failures");
                    try { Directory.Delete(d6, true); } catch (Exception) { }

                    // ---- slow link: batches of 3+ always time out, everything still arrives in smaller batches
                    now = 1_800_700_000.0;
                    var d7 = TempDir("slowlink");
                    srv = new FakeServer { Clock = () => now };
                    srv.Fault = (n, ids) => ids.Length >= 3 ? new UploadReply { network = true, timeout = true } : (UploadReply?)null;
                    box = NewBox(d7, srv, () => now, 18);
                    var slow = new List<string>();
                    for (var i = 0; i < 15; i++)
                        slow.Add(MakeResult(d7, 7, "T" + i, when, out _));
                    box.Scan();
                    box.NetworkRegained(1);
                    Run(box, srv, ref now, 8000, 2);
                    all = true;
                    foreach (var f in slow)
                        all &= IsSent(f);
                    Check(all && srv.Db.Count == 15, "slow link (3+ persons time out): all 15 arrive in smaller batches (ceiling " + box.BatchCeiling + ")");
                    try { Directory.Delete(d7, true); } catch (Exception) { }

                    // ---- cut-off session: .partial becomes .pending at the next start and is sent; a finished result wins over a stale partial
                    now = 1_800_800_000.0;
                    var d8 = TempDir("cut");
                    srv = new FakeServer { Clock = () => now };
                    box = NewBox(d8, srv, () => now, 19);
                    var cut = MakeResult(d8, 8, "Cut", when, out _);
                    File.Move(ResultOutbox.PendingOf(cut), ResultOutbox.PartialOf(cut));
                    var done8 = MakeResult(d8, 8, "Done", when.AddMinutes(1), out _);
                    File.WriteAllText(ResultOutbox.PartialOf(done8), "{}");
                    Check(ResultUploader.PromoteCutOffSessions(d8) == 1, "one cut-off session promoted");
                    Check(File.Exists(ResultOutbox.PendingOf(cut)) && !File.Exists(ResultOutbox.PartialOf(cut)), "cut-off: .partial is now .pending");
                    Check(!File.Exists(ResultOutbox.PartialOf(done8)), "stale .partial of a finished result removed");
                    box.Scan();
                    box.NetworkRegained(1);
                    Run(box, srv, ref now, 60);
                    Check(IsSent(cut), "the cut-off session is sent");
                    try { Directory.Delete(d8, true); } catch (Exception) { }

                    // ---- atomic write + leftovers of a crash
                    var d9 = TempDir("atomic");
                    var fa = Path.Combine(d9, "a.json");
                    ResultPaths.WriteAtomic(fa, "one");
                    ResultPaths.WriteAtomic(fa, "two");
                    Check(File.ReadAllText(fa) == "two" && !File.Exists(fa + ".tmp"), "WriteAtomic replaces and leaves no .tmp");
                    File.WriteAllText(Path.Combine(d9, "b.json.tmp"), "complete copy");
                    File.WriteAllText(Path.Combine(d9, "c.json"), "new");
                    File.WriteAllText(Path.Combine(d9, "c.json.tmp"), "stale");
                    Check(ResultPaths.RecoverTemps(d9) == 1 && File.ReadAllText(Path.Combine(d9, "b.json")) == "complete copy" && File.ReadAllText(Path.Combine(d9, "c.json")) == "new",
                        "crash leftovers: .tmp recovered when the target is missing, dropped when stale");
                    try { Directory.Delete(d9, true); } catch (Exception) { }

                    // ---- CSV: every field, one row per answer, same columns on every row, Bangla / commas / quotes intact, BOM on disk
                    var sub = new Submission { training_id = 12, participant_name = "রহিম উদ্দিন, \"Rony\"", participant_phone = "01712345678", device_id = "1e5", completed_at = "1790000000000" };
                    var att = new AttemptRecord { lesson_id = "SC01", language = "bn", started_at = "1790000000000", ended_at = "1790000100000", score = 1, max_score = 1, passed = true };
                    var ans = new AnswerRecord { question_id = "Q1", question_text = "কোন ড্রামটি নিরাপদ?\n(দ্বিতীয় লাইন, কমা)", selected_option = "B", correct_option = "B", is_correct = true, time_ms = 4321 };
                    ans.option_list.Add(new OptionPair { key = "A", text = "ক", optionId = "o1" });
                    ans.option_list.Add(new OptionPair { key = "B", text = "খ, গ", optionId = "o2" });
                    ans.tries.Add("B");
                    att.answers.Add(ans);
                    sub.attempts.Add(att);
                    sub.attempts.Add(new AttemptRecord { lesson_id = "SC02", language = "bn" });
                    sub.RecomputeTotals();
                    var csv = CsvExport.ToCsv(sub);
                    var rows = ParseCsv(csv);
                    var cols = CsvExport.Header.Length;
                    Check(rows.Count == 3 && rows[0].Count == cols && rows[1].Count == cols && rows[2].Count == cols,
                        "csv: header + 2 rows, every row has " + cols + " columns (rows " + rows.Count + ", cols " + (rows.Count > 1 ? rows[1].Count : 0) + "/" + (rows.Count > 2 ? rows[2].Count : 0) + ")");
                    Check(rows.Count > 1 && rows[1][1] == "রহিম উদ্দিন, \"Rony\"" && rows[1][Array.IndexOf(CsvExport.Header, "question_text")] == "কোন ড্রামটি নিরাপদ?\n(দ্বিতীয় লাইন, কমা)", "csv: Bangla, comma, quote and new line survive");
                    Check(rows.Count > 1 && rows[1][Array.IndexOf(CsvExport.Header, "option_B")] == "খ, গ" && rows[1][3] == "=\"01712345678\"", "csv: options in the A-D columns, the phone keeps its leading zero for Excel");
                    var fcsv = Path.Combine(fresh, "t.csv");
                    ResultPaths.WriteAtomic(fcsv, csv, true);
                    var raw = File.ReadAllBytes(fcsv);
                    Check(raw.Length > 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF, "csv file starts with the UTF-8 BOM (Excel shows Bangla)");
                    Check(ResultPaths.CsvOf("x/y/Name_2026.json") == "x/y/Name_2026.csv", "csv sits next to the json with the same name");

                    // ---- server profile
                    const string cfg = "{\"active\":\"stage\",\"profiles\":[{\"name\":\"stage\",\"baseUrl\":\"http://s/api/v1/\",\"batchPath\":\"results/batch\",\"apiKey\":\"k\"},{\"name\":\"production\",\"baseUrl\":\"https://gizvr.sensor.buzz/api/v1/\",\"batchPath\":\"results/batch\",\"apiKey\":\"k\"}]}";
                    var ps = ResultApiConfig.Pick(cfg, null);
                    var pp = ResultApiConfig.Pick(cfg, "Production");
                    Check(ps != null && ps.name == "stage" && pp != null && ResultApiConfig.Join(pp.baseUrl, pp.batchPath) == "https://gizvr.sensor.buzz/api/v1/results/batch", "profiles: stage default, production by name, url joined once");
                    Check(ResultApiConfig.Pick(cfg, "nope") == null && ResultApiConfig.Pick("", null) == null && ResultApiConfig.Pick("{bad", null) == null, "unknown / empty / broken config: no profile (nothing is sent)");
                }
                finally
                {
                    try { Directory.Delete(fresh, true); } catch (Exception) { }
                }
            }
            finally
            {
                try { Directory.Delete(root, true); } catch (Exception) { }
                ResultPaths.ForgetAll();
            }

            Debug.Log("[SYNC TEST] DONE failures = " + _fail);
        }
    }
}
