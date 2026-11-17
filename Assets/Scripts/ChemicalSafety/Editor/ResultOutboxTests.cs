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
            public readonly List<(byte[] body, Action<UploadReply> done, double at)> Open = new List<(byte[], Action<UploadReply>, double)>();
            public Func<int, string, UploadReply?> Fault = (n, id) => null; // (post number, id) -> forced reply, null = behave
            public bool InsertThenLoseResponse; // saves, but the answer never arrives (timeout)
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
                    var id = Id(body);
                    var forced = Fault(Posts, id);
                    if (forced.HasValue && !InsertThenLoseResponse)
                    {
                        done(forced.Value);
                        continue;
                    }

                    var isNew = Db.Add(id);
                    if (isNew)
                        Inserts++;
                    if (InsertThenLoseResponse && forced.HasValue)
                    {
                        done(forced.Value);
                        continue;
                    }

                    done(new UploadReply { code = isNew ? 201 : 200, text = "ok" });
                }
            }

            private static string Id(byte[] body)
            {
                var s = System.Text.Encoding.UTF8.GetString(body);
                var k = s.IndexOf("client_submission_id", StringComparison.Ordinal);
                var a = s.IndexOf('"', s.IndexOf(':', k) + 1) + 1;
                return s.Substring(a, s.IndexOf('"', a) - a);
            }
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

        private static string MakeResult(string root, int training, string name, DateTime when, out string id)
        {
            ResultPaths.ForgetAll();
            var s = new Submission { training_id = training, participant_name = name, startedLocal = when };
            id = s.client_submission_id;
            var json = ResultPaths.For(root, s);
            ResultPaths.WriteAtomic(json, "{\"client_submission_id\": \"" + id + "\"}");
            ResultPaths.WriteAtomic(ResultOutbox.PendingOf(json), "{\"client_submission_id\": \"" + id + "\", \"training_id\": " + training + "}");
            return json;
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
                Check(box.PeakInFlight <= ResultOutbox.MaxInFlight, "never more than 2 in flight (peak " + box.PeakInFlight + ")");

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
