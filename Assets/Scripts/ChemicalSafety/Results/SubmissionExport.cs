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
            Field(sb, 1, "participant_name", s.participant_name, true);
            Field(sb, 1, "participant_email", s.participant_email, true);
            Field(sb, 1, "participant_phone", s.participant_phone, true);
            Field(sb, 1, "participant_organisation", s.participant_organisation, true);
            Field(sb, 1, "participant_batch", s.participant_batch, true);
            Field(sb, 1, "participant_designation", s.participant_designation, true);
            Field(sb, 1, "participant_notes", s.participant_notes, true);
            Field(sb, 1, "device_id", s.device_id, true);
            Field(sb, 1, "completed_at", s.completed_at, true);
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
