using System;
using System.Globalization;
using System.Text;

namespace ChemicalSafety.Results
{
    /// <summary>
    /// The Excel copy of a result (X.csv next to X.json): everything the local JSON holds, one row per answer, the person's data repeated on
    /// every row so Excel can filter and pivot. Written as UTF-8 with BOM so Bangla opens correctly. It never goes to the server.
    /// </summary>
    public static class CsvExport
    {
        public static readonly string[] Header =
        {
            "training_id", "participant_name", "participant_email", "participant_phone", "participant_organisation", "participant_batch",
            "participant_designation", "participant_notes", "device_id", "client_submission_id",
            "session_completed_at_ms", "session_completed_at_local", "total_score", "total_max_score", "total_correct", "total_incorrect",
            "completed_scenarios", "elapsed_ms", "timed_out",
            "lesson_id", "language", "attempt_started_at_ms", "attempt_started_at_local", "attempt_ended_at_ms", "attempt_ended_at_local",
            "attempt_score", "attempt_max_score", "attempt_passed", "attempt_interrupted", "client_attempt_id",
            "question_id", "question_text", "option_A", "option_B", "option_C", "option_D",
            "selected_option", "correct_option", "is_correct", "time_ms", "hint_used", "wrong_attempts", "tries", "option_ids_order"
        };

        public static string ToCsv(Submission s)
        {
            var sb = new StringBuilder(8192);
            for (var i = 0; i < Header.Length; i++)
                sb.Append(i == 0 ? "" : ",").Append(Header[i]);
            sb.Append("\r\n");

            var any = false;
            foreach (var a in s.attempts)
            {
                if (a.answers.Count == 0)
                {
                    Row(sb, s, a, null);
                    any = true;
                    continue;
                }

                foreach (var an in a.answers)
                {
                    Row(sb, s, a, an);
                    any = true;
                }
            }

            if (!any)
                Row(sb, s, null, null); // a person with no answers yet is still listed
            return sb.ToString();
        }

        private static void Row(StringBuilder sb, Submission s, AttemptRecord a, AnswerRecord an)
        {
            Num(sb, s.training_id.ToString(), true);
            Txt(sb, s.participant_name);
            Txt(sb, s.participant_email);
            Keep(sb, s.participant_phone); // Excel would drop the leading 0 of 017...
            Txt(sb, s.participant_organisation);
            Txt(sb, s.participant_batch);
            Txt(sb, s.participant_designation);
            Txt(sb, s.participant_notes);
            Keep(sb, s.device_id);
            Txt(sb, s.client_submission_id);
            Txt(sb, s.completed_at);
            Txt(sb, Local(s.completed_at));
            Num(sb, s.total_score.ToString());
            Num(sb, s.total_max.ToString());
            Num(sb, s.total_correct.ToString());
            Num(sb, s.total_incorrect.ToString());
            Num(sb, s.completed_scenarios.ToString());
            Num(sb, s.elapsed_ms.ToString());
            Txt(sb, s.timed_out ? "TRUE" : "FALSE");
            if (a == null)
            {
                for (var i = 0; i < 25; i++)
                    sb.Append(',');
                sb.Append("\r\n");
                return;
            }

            Txt(sb, a.lesson_id);
            Txt(sb, a.language);
            Txt(sb, a.started_at);
            Txt(sb, Local(a.started_at));
            Txt(sb, a.ended_at);
            Txt(sb, Local(a.ended_at));
            Num(sb, a.score.ToString());
            Num(sb, a.max_score.ToString());
            Txt(sb, a.passed ? "TRUE" : "FALSE");
            Txt(sb, a.interrupted ? "TRUE" : "FALSE");
            Txt(sb, a.client_attempt_id);
            if (an == null)
            {
                for (var i = 0; i < 14; i++)
                    sb.Append(',');
                sb.Append("\r\n");
                return;
            }

            Txt(sb, an.question_id);
            Txt(sb, an.question_text);
            for (var k = 0; k < 4; k++)
                Txt(sb, OptionText(an, (char)('A' + k)));
            Txt(sb, an.selected_option);
            Txt(sb, an.correct_option);
            Txt(sb, an.is_correct ? "TRUE" : "FALSE");
            Num(sb, an.time_ms.ToString());
            Txt(sb, an.hint_used ? "TRUE" : "FALSE");
            Num(sb, an.wrong_attempts.ToString());
            Txt(sb, string.Join(" > ", an.tries));
            var ids = new StringBuilder();
            foreach (var o in an.option_list)
                ids.Append(ids.Length == 0 ? "" : " | ").Append(o.optionId);
            Txt(sb, ids.ToString());
            sb.Append("\r\n");
        }

        private static string OptionText(AnswerRecord an, char key)
        {
            foreach (var o in an.option_list)
                if (o != null && o.key == key.ToString())
                    return o.text;
            return string.Empty;
        }

        /// <summary>Unix milliseconds (a string) as the local date and time, empty when it is not a number.</summary>
        public static string Local(string unixMs)
        {
            if (!long.TryParse(unixMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) || ms <= 0)
                return string.Empty;
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // ---- cells (every cell is followed by a comma, except the very last of a row)

        private static void Num(StringBuilder sb, string v, bool first = false)
        {
            sb.Append(first ? "" : ",").Append(v);
        }

        private static void Txt(StringBuilder sb, string v)
        {
            sb.Append(',');
            sb.Append(Quote(v));
        }

        /// <summary>Text that must stay text in Excel (phone numbers, long ids): ="017..."</summary>
        private static void Keep(StringBuilder sb, string v)
        {
            sb.Append(',');
            if (string.IsNullOrEmpty(v))
                return;
            // standard CSV quoting of the cell  ="017..."  : Excel shows the text with its leading zero
            sb.Append("\"=\"\"").Append(v.Replace("\"", "\"\"\"\"")).Append("\"\"\"");
        }

        public static string Quote(string v)
        {
            if (string.IsNullOrEmpty(v))
                return string.Empty;
            var needs = v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || v[0] == ' ' || v[v.Length - 1] == ' ';
            // a cell that starts with = + - @ would be run as a formula by Excel: keep it as text
            if (v[0] == '=' || v[0] == '+' || v[0] == '-' || v[0] == '@')
            {
                v = "'" + v;
                needs = true;
            }

            return needs ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
    }
}
