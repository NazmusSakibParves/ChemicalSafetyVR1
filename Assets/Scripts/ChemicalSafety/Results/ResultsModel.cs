using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChemicalSafety.Results
{
    // Field names follow Docs/Login/giz_vr_mcq_submission_sample.json exactly (JsonUtility uses the field names).

    [Serializable]
    public sealed class OptionPair
    {
        public string key; // A..D, the slot the user SAW
        public string text;
        public string optionId; // stable id from the content JSON
    }

    [Serializable]
    public sealed class AnswerRecord
    {
        public string question_id;
        public string question_text;
        public List<OptionPair> option_list = new List<OptionPair>();
        public string selected_option; // letter of the last (deciding) selection
        public string correct_option;
        public bool is_correct;
        public long time_ms;
        public bool hint_used;
        public int wrong_attempts;
        public List<string> tries = new List<string>(); // every selection in order, "B", "D"
    }

    [Serializable]
    public sealed class AttemptRecord
    {
        public string client_attempt_id = Guid.NewGuid().ToString();
        public string lesson_id; // SC01..SC22
        public string language; // en | bn
        public string started_at;
        public string ended_at;
        public int score;
        public int max_score;
        public bool passed;
        public bool interrupted; // time-out in the middle of this scenario: not counted as completed
        public List<AnswerRecord> answers = new List<AnswerRecord>();
    }

    [Serializable]
    public sealed class Submission
    {
        public string client_submission_id = Guid.NewGuid().ToString();
        public string participant_name = string.Empty;
        public string participant_email = string.Empty;
        public string participant_phone = string.Empty;
        public string participant_organisation = string.Empty;
        public string participant_batch = string.Empty;
        public string participant_designation = string.Empty;
        public string participant_notes = string.Empty;
        public string device_id = string.Empty;
        public string completed_at = string.Empty;
        // Totals for the result card (additions to the sample schema; 1 mark per MCQ, first try right = 1, else 0).
        public int total_score;
        public int total_max;
        public int total_correct;
        public int total_incorrect;
        public long elapsed_ms;
        public int completed_scenarios;
        public bool timed_out;
        public List<AttemptRecord> attempts = new List<AttemptRecord>();

        /// <summary>Recomputes the totals from the attempts. Scenarios never reached count in neither score nor answered.</summary>
        public void RecomputeTotals()
        {
            total_score = 0;
            total_max = 0;
            total_correct = 0;
            total_incorrect = 0;
            completed_scenarios = 0;
            foreach (var a in attempts)
            {
                total_score += a.score;
                total_max += a.max_score;
                if (!string.IsNullOrEmpty(a.ended_at) && !a.interrupted)
                    completed_scenarios++;
                foreach (var an in a.answers)
                {
                    if (an.is_correct && an.wrong_attempts == 0)
                        total_correct++;
                    else if (!string.IsNullOrEmpty(an.selected_option))
                        total_incorrect++;
                }
            }
        }
    }

    /// <summary>Where a finished (or in-progress) submission goes. File today; an HTTP sink later (retry queue).</summary>
    public interface ISubmissionSink
    {
        void Save(Submission submission);
    }

    public sealed class FileSubmissionSink : ISubmissionSink
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "results");

        public void Save(Submission submission)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, submission.client_submission_id + ".json");
                // the file in the sample schema of the server API (what gets uploaded / reported)
                File.WriteAllText(path + ".tmp", SubmissionExport.ToJson(submission), new System.Text.UTF8Encoding(false));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(path + ".tmp", path);
            }
            catch (Exception e)
            {
                Debug.LogError("[Results] save failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Current session: who plays, which language, every answer. Saved after every answer so a quit loses nothing.
    /// The runtime hooks (RunQuestion, scenario start / end) call BeginScenario, RecordTry and EndScenario only.
    /// </summary>
    public static class SessionResults
    {
        private static ISubmissionSink _sink = new FileSubmissionSink();
        private static AttemptRecord _attempt;
        private static AnswerRecord _answer;
        private static float _questionOpenedAt;
        private static float _pausedAtOpen;

        public static Submission Current { get; private set; } = new Submission();

        /// <summary>Seed for the option shuffle; a new login gets a new one.</summary>
        public static string ShuffleSeed => Current.client_submission_id;

        public static void SetSink(ISubmissionSink sink) => _sink = sink ?? new FileSubmissionSink();

        public static void StartSession(string name, string email, string phone, string language)
        {
            Current = new Submission
            {
                participant_name = name ?? string.Empty,
                participant_email = email ?? string.Empty,
                participant_phone = phone ?? string.Empty,
                device_id = SystemInfo.deviceUniqueIdentifier
            };
            _attempt = null;
            _answer = null;
        }

        private static string Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        public static void BeginScenario(string lessonId, string language)
        {
            _attempt = new AttemptRecord { lesson_id = lessonId, language = language, started_at = Now() };
            Current.attempts.Add(_attempt);
        }

        public static void OpenQuestion(string questionId, string text, List<OptionPair> shown, string correctLetter)
        {
            if (_attempt == null)
                return;
            _answer = new AnswerRecord
            {
                question_id = questionId,
                question_text = text,
                option_list = shown,
                correct_option = correctLetter
            };
            _attempt.answers.Add(_answer);
            _attempt.max_score += 1;
            _questionOpenedAt = Time.realtimeSinceStartup;
            _pausedAtOpen = ChemicalSafety.Presentable.SessionClock.PausedTotal;
        }

        public static void RecordTry(string letter, bool correct, bool hintUsed = false)
        {
            if (_answer == null)
                return;
            _answer.tries.Add(letter);
            _answer.selected_option = letter;
            _answer.is_correct = correct;
            _answer.hint_used |= hintUsed;
            var paused = ChemicalSafety.Presentable.SessionClock.PausedTotal - _pausedAtOpen; // the pause button stops the question clock too
            _answer.time_ms = (long)(Mathf.Max(0f, Time.realtimeSinceStartup - _questionOpenedAt - paused) * 1000f);
            if (!correct)
                _answer.wrong_attempts++;
            else if (_attempt != null && _answer.wrong_attempts == 0)
                _attempt.score = Math.Min(_attempt.max_score, _attempt.score + 1); // full mark on the first try (to be confirmed)
            _sink.Save(Current);
        }

        public static void EndScenario()
        {
            if (_attempt == null)
                return;
            _attempt.ended_at = Now();
            _attempt.passed = _attempt.score >= _attempt.max_score && _attempt.max_score > 0;
            _answer = null;
            _sink.Save(Current);
        }

        public static void CompleteAll(long elapsedMs = 0, bool timedOut = false)
        {
            Current.completed_at = Now();
            Current.elapsed_ms = elapsedMs;
            Current.timed_out = timedOut;
            if (_attempt != null && string.IsNullOrEmpty(_attempt.ended_at))
            {
                _attempt.interrupted = true; // time-out in the middle of a scenario: keep what was answered, do not count it as completed
                EndScenario();
            }
            Current.RecomputeTotals();
            _sink.Save(Current);
        }
    }
}
