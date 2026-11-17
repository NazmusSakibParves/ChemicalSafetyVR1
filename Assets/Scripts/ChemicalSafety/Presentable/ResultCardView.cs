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
        public int trainingId;
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
                trainingId = sub.training_id,
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
    /// The final scorecard, client design "Trainee Performance Scorecard" (Scorecard UI folder). The frame, icons and empty bars are the
    /// client's pictures (Resources/Ui/Score); every text sits inside the frame with a margin and shrinks to fit, also in Bangla. The
    /// bars and the gauge are drawn by code and fill up (numbers count up with them). Behind the card a light 3D backdrop of tilted
    /// rotating rings and drifting light dots at different depths gives it depth. World-space, at eye level, draggable with the ray.
    /// Bangla and English follow <see cref="Loc"/>.
    /// </summary>
    public sealed class ResultCardView : MonoBehaviour
    {
        private const float W = 1400f;       // canvas
        private const float H = 960f;
        private const float FrameH = 765f;   // the client's frame picture sits at the top, the Back to Home button below it
        private const float Scale = 0.0015f; // 2.1 m wide

        private static readonly Color Label = new Color(0.62f, 0.9f, 0.96f, 1f);
        private static readonly Color Value = new Color(0.96f, 1f, 1f, 1f);
        private static readonly Color Cyan = new Color(0.45f, 0.92f, 1f, 1f);

        private Action _onHome;
        private ResultSummary _s;
        private CanvasGroup _group;
        private float _born;

        // animated chart pieces
        private RectTransform _barA, _barM;
        private Image _gauge;
        private TextMeshProUGUI _completedTxt, _completedPct, _correctTxt, _timeTxt, _marksTxt, _marksPct;
        private RectTransform _sweep;
        private readonly List<Transform> _rings = new List<Transform>();
        private readonly List<float> _ringSpeed = new List<float>();
        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private readonly List<Image> _dotImg = new List<Image>();
        private readonly List<Vector3> _dotData = new List<Vector3>(); // speed, phase, layer
        private Image _glowImg;
        private TextMeshProUGUI _upLine;
        private UploadStatus _upShown = UploadStatus.None;
        private float _upCheck;

        public static ResultCardView Show(Transform cam, ResultSummary s, Action onHome)
        {
            var root = new GameObject("ResultCard");
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.001f)
                fwd = Vector3.forward;
            fwd.Normalize();
            root.transform.SetPositionAndRotation(cam.position + fwd * 1.9f, Quaternion.LookRotation(fwd, Vector3.up));
            var v = root.AddComponent<ResultCardView>();
            v._onHome = onHome;
            v._s = s;
            v.Build(s);
            DraggablePanel.Attach(v);
            return v;
        }

        public void Close() => Destroy(gameObject);

        private static string G(string key) => Loc.T("GENERAL", key);

        // ------------------------------------------------------------------ build

        private static Vector2 P(float cx, float cy) => new Vector2(cx - W * 0.5f, H * 0.5f - cy);

        private static RectTransform At(Transform parent, string name, float cx, float cy, float w, float h) =>
            PresentableUi.Centered(name, parent, P(cx, cy), new Vector2(w, h));

        // text centred at (cx, cy) in canvas px (origin top-left), shrinks to fit the box, never wraps
        private static TextMeshProUGUI T(Transform parent, string text, float cx, float cy, float w, float h, float size, Color color, bool bold,
            TextAlignmentOptions align)
        {
            if (Loc.IsBangla && HasBangla(text)) // Bangla pictures are fitted to the box height: give them more room so they read as big as the English
            {
                size *= 1.45f;
                h *= 1.4f;
            }

            var t = PresentableUi.Text(At(parent, "T", cx, cy, w, h), text, size, color, align, bold);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.5f;
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }

        private static bool HasBangla(string t)
        {
            if (string.IsNullOrEmpty(t))
                return false;
            foreach (var c in t)
                if (c >= 'ঀ' && c <= '৿')
                    return true;
            return false;
        }

        // text whose LEFT edge is at leftX
        private static TextMeshProUGUI TL(Transform parent, string text, float leftX, float cy, float w, float h, float size, Color color, bool bold) =>
            T(parent, text, leftX + w * 0.5f, cy, w, h, size, color, bold, TextAlignmentOptions.Left);

        private static Image Icon(Transform parent, string file, float cx, float cy, float h)
        {
            var sp = HudArt.Get("Ui/Score/" + file);
            var aspect = sp != null ? sp.rect.width / sp.rect.height : 1f;
            var rt = At(parent, file, cx, cy, h * aspect, h);
            return PresentableUi.Img(rt, sp, new Color(0.8f, 0.97f, 1f, 1f), preserveAspect: true);
        }

        private void Build(ResultSummary s)
        {
            var canvas = PresentableUi.WorldCanvas("ResultCanvas", transform, new Vector2(W, H), Scale, true, 20);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            FingerPokeSystem.RefreshGroup(canvas);
            var host = (RectTransform)canvas.transform;

            // frame (client picture), see-through like the design
            var frame = PresentableUi.Rect("Frame", host, new Vector2(0f, 1f), new Vector2(1f, 1f));
            frame.pivot = new Vector2(0.5f, 1f);
            frame.sizeDelta = new Vector2(0f, FrameH);
            frame.anchoredPosition = Vector2.zero;
            PresentableUi.Img(frame, HudArt.Get("Ui/Score/01_blank_popup_screen"), new Color(1f, 1f, 1f, 0.97f), raycast: true);

            // light sweep over the frame (clipped to the frame)
            var clip = PresentableUi.Rect("Clip", frame, new Vector2(0.04f, 0.07f), new Vector2(0.96f, 0.93f));
            clip.gameObject.AddComponent<RectMask2D>();
            _sweep = At(clip, "Sweep", 0f, 0f, 160f, 1100f);
            _sweep.SetParent(clip, false);
            _sweep.localRotation = Quaternion.Euler(0f, 0f, 18f);
            PresentableUi.Img(_sweep, HudArt.Glow(), new Color(0.6f, 0.95f, 1f, 0.12f));

            // ---- header: shield + "CHEMICAL SAFETY / TRAINING SIMULATION" | "TRAINEE PERFORMANCE SCORECARD"
            Icon(host, "shield", 143f, 118f, 76f);
            TL(host, G("RsTitle1"), 199f, 108f, 330f, 40f, 36f, Value, true);
            TL(host, G("RsTitle2"), 199f, 146f, 330f, 32f, 25f, Label, false);
            var slash = At(host, "Slash", 560f, 118f, 3f, 78f);
            slash.localRotation = Quaternion.Euler(0f, 0f, 16f);
            PresentableUi.Img(slash, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f));
            T(host, G("ScTitle"), 945f, 120f, 680f, 48f, 40f, Cyan, true, TextAlignmentOptions.Center);

            // ---- left panel: training id, trainee, contact
            TL(host, G("ScTrainingId"), 126f, 242f, 235f, 38f, 30f, Label, false);
            TL(host, s.trainingId > 0 ? s.trainingId.ToString() : "-", 368f, 242f, 210f, 44f, 36f, Value, true);

            Icon(host, "person", 146f, 305f, 40f);
            TL(host, G("ScName"), 190f, 305f, 385f, 36f, 28f, Label, false);
            TL(host, string.IsNullOrEmpty(s.name) ? "-" : s.name, 126f, 366f, 450f, 60f, 44f, Value, true);

            var contactIsPhone = !string.IsNullOrEmpty(SessionResults.Current.participant_phone);
            Icon(host, "mobile", 142f, 462f, 40f);
            TL(host, G(contactIsPhone ? "ScMobile" : "ScEmail"), 190f, 462f, 385f, 36f, 28f, Label, false);
            TL(host, string.IsNullOrEmpty(s.contact) ? "-" : s.contact, 126f, 520f, 450f, 52f, 38f, Value, true);

            // ---- right panel
            // completed scenarios + bar
            Icon(host, "clipboard", 704f, 214f, 46f);
            TL(host, G("ScCompleted"), 760f, 214f, 330f, 36f, 28f, Label, false);
            _completedTxt = T(host, "0/" + s.totalScenarios, 1138f, 214f, 110f, 38f, 28f, Value, true, TextAlignmentOptions.Center);
            _completedPct = T(host, "0%", 1236f, 214f, 80f, 38f, 28f, Value, true, TextAlignmentOptions.Center);
            _barA = Bar(host, "scenarios_empty_bar", 1011f, 268f, 560f, 0.03f, 0.96f, 0.24f, 0.58f);

            // total correct + gauge
            Icon(host, "check_circle", 704f, 356f, 50f);
            TL(host, G("ScCorrect"), 760f, 356f, 290f, 36f, 28f, Label, false);
            var gaugeRt = At(host, "GaugeBase", 1165f, 360f, 220f, 109f);
            PresentableUi.Img(gaugeRt, HudArt.Get("Ui/Score/total_correct_empty_semicircle"), new Color(0.25f, 0.45f, 0.6f, 0.85f));
            var gaugeFill = At(host, "GaugeFill", 1165f, 360f, 220f, 109f);
            _gauge = PresentableUi.Img(gaugeFill, HudArt.Get("Ui/Score/total_correct_empty_semicircle"), new Color(0.55f, 1f, 1f, 1f));
            _gauge.type = Image.Type.Filled;
            _gauge.fillMethod = Image.FillMethod.Radial180;
            _gauge.fillOrigin = (int)Image.Origin180.Bottom;
            _gauge.fillClockwise = true;
            _gauge.fillAmount = 0f;
            _correctTxt = T(host, "0/" + s.maxMarks, 1165f, 384f, 120f, 40f, 34f, Value, true, TextAlignmentOptions.Center);

            // time duration
            Icon(host, "clock", 704f, 448f, 50f);
            TL(host, G("ScTime"), 760f, 448f, 290f, 36f, 28f, Label, false);
            _timeTxt = T(host, "00:00", 1098f, 446f, 130f, 48f, 40f, Value, true, TextAlignmentOptions.Right);
            TL(host, G("ScMinutes"), 1176f, 452f, 100f, 34f, 24f, Label, false);

            // obtained marks + bar
            Icon(host, "medal", 704f, 528f, 52f);
            TL(host, G("ScMarks"), 760f, 520f, 245f, 36f, 28f, Label, false);
            _marksTxt = T(host, "0/" + s.maxMarks, 1088f, 516f, 160f, 40f, 32f, Value, true, TextAlignmentOptions.Center);
            _marksPct = T(host, "0.0%", 1232f, 522f, 90f, 34f, 25f, Label, false, TextAlignmentOptions.Center);
            _barM = Bar(host, "marks_empty_bar", 1146f, 556f, 270f, 0.03f, 0.965f, 0.26f, 0.62f);

            // BACK TO HOME: glass button below the frame
            var btnRt = At(host, "HomeButton", W * 0.5f, FrameH + 62f, 400f, 82f);
            // same colours as the scorecard: soft cyan glow, thin cyan neon edge, dark teal glass inside
            PresentableUi.Img(PresentableUi.Rect("Glow", btnRt, Vector2.zero, Vector2.one, new Vector2(-10f, -10f), new Vector2(10f, 10f)),
                HudArt.Pill(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.16f), sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Edge", btnRt, Vector2.zero, Vector2.one), HudArt.Pill(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f), sliced: true);
            var btnImg = PresentableUi.Img(PresentableUi.Rect("Plate", btnRt, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)),
                HudArt.Pill(), new Color(0.02f, 0.14f, 0.22f, 0.92f), sliced: true, raycast: true);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.None;
            var lift = btnRt.gameObject.AddComponent<HoverLift>();
            lift.Init(btnRt);
            btn.onClick.AddListener(lift.Press);
            btn.onClick.AddListener(() =>
            {
                btn.interactable = false;
                _onHome?.Invoke();
            });
            var bt = PresentableUi.Text(PresentableUi.Centered("T", btnRt, Vector2.zero, new Vector2(320f, 52f)), G("RsHome"), 34f, Value,
                TextAlignmentOptions.Center, true);
            bt.textWrappingMode = TextWrappingModes.NoWrap;
            bt.enableAutoSizing = true;
            bt.fontSizeMax = 34f;
            bt.fontSizeMin = 20f;

            // upload state line under the button: saved / waiting for internet / sent (the result is always on the device first)
            _upLine = T(host, G("UpSaved"), W * 0.5f, FrameH + 142f, 1000f, 40f, 26f, Label, false, TextAlignmentOptions.Center);
            _upLine.text = string.Empty;

            PresentableUi.AlwaysOnTop(canvas);
            BuildBackdrop();
            _group.alpha = 0f;
            _born = Time.unscaledTime;
        }

        // empty bar picture + a pill fill inside its hollow (insets in 0..1 of the picture, y from the bottom)
        private static RectTransform Bar(RectTransform host, string art, float cx, float cy, float w, float x0, float x1, float y0, float y1)
        {
            var sp = HudArt.Get("Ui/Score/" + art);
            var h = w * sp.rect.height / sp.rect.width;
            var rt = At(host, art, cx, cy, w, h);
            PresentableUi.Img(rt, sp, Color.white);
            var fill = PresentableUi.Rect("Fill", rt, new Vector2(x0, y0), new Vector2(x0, y1));
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            PresentableUi.Img(fill, HudArt.Pill(), new Color(0.45f, 0.9f, 0.98f, 1f), sliced: true);
            var gloss = PresentableUi.Rect("Gloss", fill, new Vector2(0.04f, 0.5f), new Vector2(0.96f, 0.85f));
            PresentableUi.Img(gloss, HudArt.Pill(), new Color(1f, 1f, 1f, 0.35f), sliced: true);
            fill.gameObject.name = "Fill|" + x0 + "|" + x1;
            return fill;
        }

        private static void SetBar(RectTransform fill, float amount)
        {
            var parts = fill.gameObject.name.Split('|');
            var x0 = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            var x1 = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            var a = Mathf.Clamp01(amount);
            fill.gameObject.SetActive(a > 0.012f);
            fill.anchorMax = new Vector2(Mathf.Lerp(x0, x1, a), fill.anchorMax.y);
        }

        // ------------------------------------------------------------------ 3D backdrop (behind the card, follows it)

        private void BuildBackdrop()
        {
            // z: behind the card (the panel faces -z, the learner is on the -z side)
            var back = new GameObject("Backdrop").transform;
            back.SetParent(transform, false);
            back.localPosition = new Vector3(0f, 0f, 0f);

            // big soft glow behind everything
            var glowCanvas = BackCanvas(back, "Glow", 3400f, 2200f, 0.9f, 10);
            _glowImg = PresentableUi.Img(PresentableUi.Rect("G", glowCanvas.transform, new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.88f)),
                HudArt.Glow(), new Color(0.1f, 0.55f, 1f, 0.5f));

            // tilted rotating rings at three depths: they read as a holographic gyroscope behind the card
            var cfg = new[]
            {
                new Vector4(0.7f, 2200f, 12f, -9f),   // z, size px, tilt x, tilt y
                new Vector4(0.45f, 1750f, -10f, 8f),
                new Vector4(0.25f, 1380f, 7f, 5f)
            };
            var segs = new[] { 3, 24, 2 };
            var fills = new[] { 0.55f, 0.35f, 0.7f };
            var thick = new[] { 0.02f, 0.012f, 0.03f };
            var speeds = new[] { 6f, -11f, 17f };
            for (var i = 0; i < cfg.Length; i++)
            {
                var tilt = new GameObject("RingTilt" + i).transform;
                tilt.SetParent(back, false);
                tilt.localPosition = new Vector3(0f, 0f, cfg[i].x);
                tilt.localRotation = Quaternion.Euler(cfg[i].z, cfg[i].w, 0f);
                var c = BackCanvas(tilt, "Ring" + i, cfg[i].y, cfg[i].y, 0f, 11 + i);
                PresentableUi.Img(PresentableUi.Rect("R", c.transform, Vector2.zero, Vector2.one), HudArt.Ring(segs[i], fills[i], thick[i]),
                    new Color(0.4f, 0.9f, 1f, 0.9f - i * 0.12f));
                _rings.Add(c.transform);
                _ringSpeed.Add(speeds[i]);
            }

            // drifting light dots on three depth layers (parallax when the learner moves the head)
            var zs = new[] { 0.3f, 0.6f, 0.95f };
            var rnd = new System.Random(7);
            for (var layer = 0; layer < zs.Length; layer++)
            {
                var c = BackCanvas(back, "Dots" + layer, 3200f, 2000f, zs[layer], 15 + layer);
                for (var i = 0; i < 14; i++)
                {
                    var size = 14f + (float)rnd.NextDouble() * 30f * (1f + layer * 0.5f);
                    var rt = At(c.transform, "d", 0f, 0f, size, size);
                    rt.anchoredPosition = new Vector2(((float)rnd.NextDouble() - 0.5f) * 3000f, ((float)rnd.NextDouble() - 0.5f) * 1900f);
                    var img = PresentableUi.Img(rt, HudArt.Glow(), new Color(0.5f, 0.92f, 1f, 0.7f));
                    _dots.Add(rt);
                    _dotImg.Add(img);
                    _dotData.Add(new Vector3(18f + (float)rnd.NextDouble() * 34f + layer * 12f, (float)rnd.NextDouble() * 6.28f, layer));
                }
            }
        }

        private static Canvas BackCanvas(Transform parent, string name, float w, float h, float z, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            var c = PresentableUi.WorldCanvas(name + "Canvas", go.transform, new Vector2(w, h), 0.001f, false, order);
            PresentableUi.AlwaysOnTop(c); // filled afterwards by the caller; z test is switched again below for the added images
            c.gameObject.AddComponent<BackdropZ>();
            return c;
        }

        /// <summary>Applies "always on top" to the graphics added to a backdrop canvas after it was created.</summary>
        private sealed class BackdropZ : MonoBehaviour
        {
            private void Start() => PresentableUi.AlwaysOnTop(GetComponent<Canvas>());
        }

        // ------------------------------------------------------------------ animation

        private static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        private void Update()
        {
            var age = Time.unscaledTime - _born;
            _group.alpha = Mathf.Clamp01(age / 0.5f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, PresentableFx.EaseOutBack(Mathf.Clamp01(age / 0.7f)));

            // charts: bars and gauge fill one after the other, the numbers count with them
            var s = _s;
            var pA = s.totalScenarios > 0 ? Mathf.Clamp01((float)s.completedScenarios / s.totalScenarios) : 0f;
            var pC = s.maxMarks > 0 ? Mathf.Clamp01((float)s.correct / s.maxMarks) : 0f;
            var pM = s.maxMarks > 0 ? Mathf.Clamp01((float)s.marks / s.maxMarks) : 0f;
            var eA = Ease((age - 0.5f) / 1.4f);
            var eC = Ease((age - 0.8f) / 1.4f);
            var eT = Ease((age - 1.1f) / 1.2f);
            var eM = Ease((age - 1.4f) / 1.4f);
            SetBar(_barA, pA * eA);
            _completedTxt.text = Mathf.RoundToInt(s.completedScenarios * eA) + "/" + s.totalScenarios;
            _completedPct.text = Mathf.RoundToInt(pA * 100f * eA) + "%";
            _gauge.fillAmount = pC * eC;
            _correctTxt.text = Mathf.RoundToInt(s.correct * eC) + "/" + s.maxMarks;
            _timeTxt.text = SessionClock.Format(s.seconds * eT);
            SetBar(_barM, pM * eM);
            _marksTxt.text = Mathf.RoundToInt(s.marks * eM) + "/" + s.maxMarks;
            _marksPct.text = (pM * 100f * eM).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";

            // light sweep across the frame
            var ph = Mathf.Repeat(Time.unscaledTime / 5.5f, 1f);
            _sweep.anchoredPosition = new Vector2(Mathf.Lerp(-900f, 900f, ph), 0f);

            // backdrop: rings turn, dots rise and twinkle, glow breathes
            for (var i = 0; i < _rings.Count; i++)
                _rings[i].Rotate(0f, 0f, _ringSpeed[i] * Time.unscaledDeltaTime, Space.Self);
            for (var i = 0; i < _dots.Count; i++)
            {
                var d = _dotData[i];
                var p = _dots[i].anchoredPosition;
                p.y += d.x * Time.unscaledDeltaTime;
                p.x += Mathf.Sin(Time.unscaledTime * 0.5f + d.y) * 6f * Time.unscaledDeltaTime;
                if (p.y > 1000f)
                    p.y -= 2000f;
                _dots[i].anchoredPosition = p;
                var c = _dotImg[i].color;
                c.a = 0.25f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.3f + d.y * 3f));
                _dotImg[i].color = c;
            }

            if (Time.unscaledTime >= _upCheck)
            {
                _upCheck = Time.unscaledTime + 1f;
                var st = ResultUploader.StatusOf(SessionResults.Current);
                if (st != _upShown)
                {
                    _upShown = st;
                    var key = st == UploadStatus.Sent ? "UpSent" : st == UploadStatus.Waiting ? "UpWaiting" : st == UploadStatus.Rejected ? "UpRejected"
                        : st == UploadStatus.Saved ? "UpSaved" : null;
                    _upLine.text = key == null ? string.Empty : G(key);
                    _upLine.color = st == UploadStatus.Sent ? new Color(0.5f, 1f, 0.7f, 1f) : st == UploadStatus.Waiting || st == UploadStatus.Rejected ? new Color(1f, 0.85f, 0.45f, 1f) : Label;
                }
            }

            if (_glowImg != null)
                _glowImg.color = new Color(0.1f, 0.55f, 1f, 0.42f + 0.1f * Mathf.Sin(Time.unscaledTime * 0.8f));
        }
    }
}
