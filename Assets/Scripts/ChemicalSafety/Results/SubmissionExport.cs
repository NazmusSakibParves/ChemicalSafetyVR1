using System.Text;

namespace ChemicalSafety.Results
{
    /// <summary>
    /// Writes a Submission in the exact shape of Docs/Login/giz_vr_mcq_submission_sample.json (options as an object A..D,
    /// timestamps as strings). Extra fields per answer (question_id, wrong_attempts, tries, option_ids_order) are additions;
    /// the API owner must accept them or they can be dropped here in one place.
    /// </summary>
    public static class SubmissionExport
    {
        public static string ToJson(Submission s)
        {
            var sb = new StringBuilder(4096);
            sb.Append("{\n");
            Field(sb, 1, "client_submission_id", s.client_submission_id, true);
            Raw(sb, 1, "training_id", s.training_id.ToString(), true);
            Field(sb, 1, "participant_name", s.participant_name, true);
            Field(sb, 1, "participant_email", s.participant_email, true);
            Field(sb, 1, "participant_phone", s.participant_phone, true);
            Field(sb, 1, "participant_organisation", s.participant_organisation, true);
            Field(sb, 1, "participant_batch", s.participant_batch, true);
            Field(sb, 1, "participant_designation", s.participant_designation, true);
            Field(sb, 1, "participant_notes", s.participant_notes, true);
            Field(sb, 1, "device_id", s.device_id, true);
            Field(sb, 1, "completed_at", s.completed_at, true);
            Raw(sb, 1, "total_score", s.total_score.ToString(), true);
            Raw(sb, 1, "total_max_score", s.total_max.ToString(), true);
            Raw(sb, 1, "total_correct", s.total_correct.ToString(), true);
            Raw(sb, 1, "total_incorrect", s.total_incorrect.ToString(), true);
            Raw(sb, 1, "completed_scenarios", s.completed_scenarios.ToString(), true);
            Raw(sb, 1, "elapsed_ms", s.elapsed_ms.ToString(), true);
            Raw(sb, 1, "timed_out", s.timed_out ? "true" : "false", true);
            sb.Append("  \"attempts\": [");
            for (var i = 0; i < s.attempts.Count; i++)
            {
                var a = s.attempts[i];
                sb.Append(i == 0 ? "\n    {\n" : ",\n    {\n");
                Field(sb, 3, "client_attempt_id", a.client_attempt_id, true);
                Field(sb, 3, "lesson_id", a.lesson_id, true);
                Field(sb, 3, "language", a.language, true);
                Field(sb, 3, "started_at", a.started_at, true);
                Field(sb, 3, "ended_at", a.ended_at, true);
                Raw(sb, 3, "score", a.score.ToString(), true);
                Raw(sb, 3, "max_score", a.max_score.ToString(), true);
                Raw(sb, 3, "passed", a.passed ? "true" : "false", true);
                sb.Append("      \"answers\": [");
                for (var j = 0; j < a.answers.Count; j++)
                {
                    var an = a.answers[j];
                    sb.Append(j == 0 ? "\n        {\n" : ",\n        {\n");
                    Field(sb, 5, "question_id", an.question_id, true);
                    Field(sb, 5, "question_text", an.question_text, true);
                    sb.Append("          \"options\": {");
                    for (var k = 0; k < an.option_list.Count; k++)
                    {
                        sb.Append(k == 0 ? "\n" : ",\n");
                        sb.Append("            \"").Append(Esc(an.option_list[k].key)).Append("\": \"").Append(Esc(an.option_list[k].text)).Append('"');
                    }

                    sb.Append("\n          },\n");
                    Field(sb, 5, "selected_option", an.selected_option, true);
                    Field(sb, 5, "correct_option", an.correct_option, true);
                    Raw(sb, 5, "is_correct", an.is_correct ? "true" : "false", true);
                    Raw(sb, 5, "time_ms", an.time_ms.ToString(), true);
                    Raw(sb, 5, "hint_used", an.hint_used ? "true" : "false", true);
                    Raw(sb, 5, "wrong_attempts", an.wrong_attempts.ToString(), true);
                    sb.Append("          \"tries\": [");
                    for (var k = 0; k < an.tries.Count; k++)
                        sb.Append(k == 0 ? "" : ", ").Append('"').Append(Esc(an.tries[k])).Append('"');
                    sb.Append("],\n          \"option_ids_order\": [");
                    for (var k = 0; k < an.option_list.Count; k++)
                        sb.Append(k == 0 ? "" : ", ").Append('"').Append(Esc(an.option_list[k].optionId)).Append('"');
                    sb.Append("]\n        }");
                }

                sb.Append("\n      ]\n    }");
            }

            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Body for POST /api/v1/results: the fields of bsrm_training1_full_submission.json and nothing else (the API rejects / ignores PII and
        /// unknown fields). <paramref name="withExtras"/> (ResultUploader.SendExtras) adds email, phone, totals and the per-answer extras for
        /// the day the backend accepts them. Attempts without a picked answer are left out (the API needs >= 1 answer per lesson).
        /// Multi-select answers ("A,C") are sent as ONE letter: the first correct tick when right, else the first wrong tick.
        /// </summary>
        public static string ToApiJson(Submission s, bool withExtras = false)
        {
            var sb = new StringBuilder(8192);
            sb.Append("{\n");
            Field(sb, 1, "client_submission_id", s.client_submission_id, true);
            Raw(sb, 1, "training_id", s.training_id.ToString(), true);
            Field(sb, 1, "participant_name", s.participant_name, true);
            if (withExtras)
            {
                Field(sb, 1, "participant_email", s.participant_email, true);
                Field(sb, 1, "participant_phone", s.participant_phone, true);
            }

            Field(sb, 1, "participant_organisation", s.participant_organisation, true);
            Field(sb, 1, "participant_batch", s.participant_batch, true);
            Field(sb, 1, "participant_designation", s.participant_designation, true);
            Field(sb, 1, "participant_notes", s.participant_notes, true);
            Field(sb, 1, "device_id", s.device_id, true);
            Field(sb, 1, "completed_at", string.IsNullOrEmpty(s.completed_at) ? System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() : s.completed_at, true);
            if (withExtras)
            {
                Raw(sb, 1, "total_score", s.total_score.ToString(), true);
                Raw(sb, 1, "total_max_score", s.total_max.ToString(), true);
                Raw(sb, 1, "total_correct", s.total_correct.ToString(), true);
                Raw(sb, 1, "total_incorrect", s.total_incorrect.ToString(), true);
                Raw(sb, 1, "completed_scenarios", s.completed_scenarios.ToString(), true);
                Raw(sb, 1, "elapsed_ms", s.elapsed_ms.ToString(), true);
                Raw(sb, 1, "timed_out", s.timed_out ? "true" : "false", true);
            }

            sb.Append("  \"attempts\": [");
            var firstAttempt = true;
            foreach (var a in s.attempts)
            {
                var answers = new StringBuilder();
                var n = 0;
                foreach (var an in a.answers)
                {
                    if (string.IsNullOrEmpty(an.selected_option))
                        continue;
                    var sel = FirstLetter(an.selected_option, an.correct_option, an.is_correct, false);
                    var cor = FirstLetter(an.correct_option, an.correct_option, true, true);
                    if (sel == null || cor == null)
                        continue;
                    answers.Append(n++ == 0 ? "\n        {\n" : ",\n        {\n");
                    Field(answers, 5, "question_text", an.question_text, true);
                    answers.Append("          \"options\": {");
                    for (var k = 0; k < 4; k++)
                    {
                        // SC19 has 2 and SC20 has 3 options, but the API needs A-D non-empty (422 otherwise): the missing ones are "N/A"
                        var text = k < an.option_list.Count ? an.option_list[k].text : string.Empty;
                        if (string.IsNullOrWhiteSpace(text))
                            text = "N/A";
                        answers.Append(k == 0 ? "\n" : ",\n");
                        answers.Append("            \"").Append((char)('A' + k)).Append("\": \"").Append(Esc(text)).Append('"');
                    }

                    answers.Append("\n          },\n");
                    Field(answers, 5, "selected_option", sel, true);
                    Field(answers, 5, "correct_option", cor, true);
                    Raw(answers, 5, "is_correct", an.is_correct ? "true" : "false", true);
                    Raw(answers, 5, "time_ms", an.time_ms.ToString(), true);
                    if (withExtras)
                    {
                        Raw(answers, 5, "hint_used", an.hint_used ? "true" : "false", true);
                        Field(answers, 5, "question_id", an.question_id, true);
                        Raw(answers, 5, "wrong_attempts", an.wrong_attempts.ToString(), false);
                    }
                    else
                        Raw(answers, 5, "hint_used", an.hint_used ? "true" : "false", false);
                    answers.Append("        }");
                }

                if (n == 0)
                    continue;
                sb.Append(firstAttempt ? "\n    {\n" : ",\n    {\n");
                firstAttempt = false;
                Field(sb, 3, "lesson_id", a.lesson_id, true);
                Field(sb, 3, "language", a.language, true);
                Field(sb, 3, "started_at", a.started_at, true);
                Field(sb, 3, "ended_at", string.IsNullOrEmpty(a.ended_at) ? System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() : a.ended_at, true);
                Raw(sb, 3, "score", a.score.ToString(), true);
                Raw(sb, 3, "max_score", a.max_score.ToString(), true);
                Raw(sb, 3, "passed", a.passed ? "true" : "false", true);
                sb.Append("      \"answers\": [").Append(answers).Append("\n      ]\n    }");
            }

            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        // "B" -> B; "A,C" -> first letter (for the selection: the first WRONG tick when the answer was wrong, else the first correct one)
        private static string FirstLetter(string letters, string correct, bool isCorrect, bool isCorrectField)
        {
            if (string.IsNullOrEmpty(letters))
                return null;
            var parts = letters.Split(',');
            string pick = null;
            if (!isCorrectField && parts.Length > 1)
            {
                var corr = (correct ?? string.Empty).Split(',');
                foreach (var p in parts)
                {
                    var inCorrect = System.Array.IndexOf(corr, p) >= 0;
                    if (isCorrect ? inCorrect : !inCorrect)
                    {
                        pick = p;
                        break;
                    }
                }
            }

            pick = pick ?? parts[0];
            return pick.Length == 1 && pick[0] >= 'A' && pick[0] <= 'D' ? pick : null;
        }

        private static void Field(StringBuilder sb, int indentHalf, string key, string value, bool comma) =>
            Raw(sb, indentHalf, key, "\"" + Esc(value) + "\"", comma);

        private static void Raw(StringBuilder sb, int indentHalf, string key, string raw, bool comma)
        {
            sb.Append(' ', indentHalf * 2).Append('"').Append(key).Append("\": ").Append(raw).Append(comma ? ",\n" : "\n");
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
