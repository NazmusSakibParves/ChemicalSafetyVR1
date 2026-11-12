using System;
using System.Collections.Generic;
using ChemicalSafety.Localization;
using ChemicalSafety.Results;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>Numbers of the result card. Marks: 1 per MCQ, first try right = 1 (see Content/MARKING.json and Docs/MARKING_RULES.md).</summary>
    public struct ResultSummary
    {
        public string name;
        public string contact;
        public int completedScenarios;
        public int totalScenarios;
        public int marks;
        public int maxMarks;
        public int correct;
        public int incorrect;
        public float seconds;

        [Serializable]
        private sealed class McqEntry
        {
            public string id;
            public int count;
        }

        [Serializable]
        private sealed class MarkingFile
        {
            public int totalScenarios = 22;
            public List<McqEntry> mcqs = new List<McqEntry>();
        }

        private static MarkingFile LoadMarking()
        {
            var ta = Resources.Load<TextAsset>("Content/MARKING");
            if (ta != null)
            {
                try
                {
                    return JsonUtility.FromJson<MarkingFile>(ta.text);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Results] MARKING.json: " + e.Message);
                }
            }

            return new MarkingFile();
        }

        public static ResultSummary Compute()
        {
            var sub = SessionResults.Current;
            sub.RecomputeTotals();
            var marking = LoadMarking();
            var expected = 0;
            foreach (var m in marking.mcqs)
                expected += m.count;
            // MCQs the learner never reached are wrong (0 marks); a scenario added later raises the total through MARKING.json
            var max = Mathf.Max(expected, sub.total_max);
            return new ResultSummary
            {
                name = sub.participant_name,
                contact = !string.IsNullOrEmpty(sub.participant_phone) ? sub.participant_phone : sub.participant_email,
                completedScenarios = sub.completed_scenarios,
                totalScenarios = marking.totalScenarios,
                marks = sub.total_score,
                maxMarks = max,
                correct = sub.total_correct,
                incorrect = Mathf.Max(0, max - sub.total_correct),
                seconds = SessionClock.Elapsed
            };
        }
    }


    /// <summary>
    /// The final result card, drawn like the client design "result card 2.jpeg": the artwork is <c>Resources/Ui/result_card_bg.png</c>
    /// (Tools/generate_result_card.py, no text) and every text sits at the pixel position of the design (reference picture
    /// coordinates, card box 420..1180 x 100..840). World-space, at eye level in front of the learner, draggable.
    /// Bangla and English follow <see cref="Loc"/>.
    /// </summary>
    public sealed class ResultCardView : MonoBehaviour
    {
        private const float W = 760f;
        private const float H = 740f;
        private const float CenterX = 800f; // card centre in the reference picture
        private const float CenterY = 470f;

        private static Sprite _bg;
        private Action _onHome;

        private static Sprite Background()
        {
            if (_bg != null)
                return _bg;
            var tex = Resources.Load<Texture2D>("Ui/result_card_bg");
            if (tex == null)
            {
                Debug.LogError("[ResultCard] Resources/Ui/result_card_bg.png missing (run Tools/generate_result_card.py)");
                return null;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            _bg = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _bg;
        }

        public static ResultCardView Show(Transform cam, ResultSummary s, Action onHome)
        {
            var root = new GameObject("ResultCard");
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.001f)
                fwd = Vector3.forward;
            fwd.Normalize();
            root.transform.SetPositionAndRotation(cam.position + fwd * 1.7f, Quaternion.LookRotation(fwd, Vector3.up));
            var v = root.AddComponent<ResultCardView>();
            v._onHome = onHome;
            v.Build(s);
            DraggablePanel.Attach(v);
            return v;
        }

        public void Close() => Destroy(gameObject);

        private static string G(string key) => Loc.T("GENERAL", key);

        private void Build(ResultSummary s)
        {
            var canvas = PresentableUi.WorldCanvas("ResultCanvas", transform, new Vector2(W, H), 0.0013f, true, 20);
            var host = (RectTransform)canvas.transform;
            PresentableUi.Img(PresentableUi.Rect("Art", host, Vector2.zero, Vector2.one), Background(), Color.white);

            var white = Color.white;
            var yellow = new Color(1f, 0.9f, 0.1f, 1f);
            var green = new Color(0.2f, 1f, 0.45f, 1f);
            var red = new Color(1f, 0.2f, 0.2f, 1f);
            var soft = new Color(0.9f, 0.96f, 1f, 1f);

            // header and pill
            T(host, G("RsTitle1"), 800, 146, 700, 54, 52, white, true);
            T(host, G("RsTitle2"), 800, 190, 700, 54, 52, yellow, true);
            T(host, G("RsFinal"), 800, 260, 360, 50, 40, white, true);

            // trainee rows: label left at x 553, value left at x 780 (scenarios value at 878), as in the design
            var contactIsPhone = !string.IsNullOrEmpty(SessionResults.Current.participant_phone);
            TL(host, G("RsName"), 553, 325, 220, 44, 30, white, true);
            TL(host, s.name, 780, 325, 380, 44, 30, white, true);
            TL(host, contactIsPhone ? G("RsMobile") : G("RsEmail"), 553, 368, 220, 44, 30, white, true);
            TL(host, s.contact, 780, 368, 380, 44, 30, white, true);
            TL(host, G("RsCompleted"), 553, 412, 320, 44, 30, white, true);
            TL(host, s.completedScenarios + " / " + s.totalScenarios, 878, 412, 200, 44, 30, white, true);

            // obtained marks
            TL(host, G("RsMarks"), 593, 481, 300, 50, 32, white, true);
            T(host, s.marks + " / " + s.maxMarks, 960, 481, 250, 60, 48, green, true);

            // time
            TL(host, G("RsTime"), 591, 550, 270, 50, 32, white, true);
            T(host, SessionClock.Format(s.seconds), 895, 550, 120, 50, 34, white, true);
            T(host, G("RsMinutes"), 1008, 550, 110, 50, 32, white, true);

            // message (italic)
            var msg = T(host, G(s.maxMarks > 0 && (float)s.marks / s.maxMarks >= 0.8f ? "RsExcellent"
                : s.maxMarks > 0 && (float)s.marks / s.maxMarks >= 0.5f ? "RsGood" : "RsKeep"), 800, 611, 640, 40, 23, soft, false);
            msg.fontStyle = FontStyles.Italic;

            // correct / incorrect
            T(host, G("RsCorrect"), 678, 670, 230, 40, 27, white, true);
            T(host, s.correct + "/" + s.maxMarks, 676, 708, 230, 56, 42, green, true);
            T(host, G("RsIncorrect"), 999, 670, 230, 40, 27, white, true);
            T(host, s.incorrect + "/" + s.maxMarks, 987, 708, 230, 56, 42, red, true);

            // BACK TO HOME: the pill is in the artwork; a see-through button on top of it takes the click
            var btnRt = Pos(host, "HomeButton", 806, 803, 356, 72);
            var btnImg = PresentableUi.Img(btnRt, null, new Color(1f, 1f, 1f, 0.004f), raycast: true);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.None; // hover cue = the ray's haptic tick
            btn.onClick.AddListener(() =>
            {
                btn.interactable = false;
                _onHome?.Invoke();
            });
            T(btnRt, G("RsHome"), 806, 803, 340, 56, 38, white, true, true);

            PresentableUi.AlwaysOnTop(canvas);
        }

        // rect centred at reference coordinates (rx, ry)
        private static RectTransform Pos(RectTransform host, string name, float rx, float ry, float w, float h) =>
            PresentableUi.Centered(name, host, new Vector2(rx - CenterX, CenterY - ry), new Vector2(w, h));

        // centred text
        private static TextMeshProUGUI T(RectTransform host, string text, float rx, float ry, float w, float h, float size, Color color,
            bool bold, bool sameParent = false)
        {
            return Make(host, text, rx, ry, w, h, size, color, bold, TextAlignmentOptions.Center, sameParent);
        }

        // text whose LEFT edge is at reference x (rx)
        private static TextMeshProUGUI TL(RectTransform host, string text, float leftX, float ry, float w, float h, float size, Color color,
            bool bold)
        {
            return Make(host, text, leftX + w * 0.5f, ry, w, h, size, color, bold, TextAlignmentOptions.Left, false);
        }

        private static TextMeshProUGUI Make(RectTransform host, string text, float rx, float ry, float w, float h, float size, Color color,
            bool bold, TextAlignmentOptions align, bool sameParent)
        {
            RectTransform rt;
            if (sameParent) // child of the button: centred on it
                rt = PresentableUi.Centered("T", host, Vector2.zero, new Vector2(w, h));
            else
                rt = Pos(host, "T", rx, ry, w, h);
            var t = PresentableUi.Text(rt, text, size, color, align, bold);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.5f;
            return t;
        }
    }
}
