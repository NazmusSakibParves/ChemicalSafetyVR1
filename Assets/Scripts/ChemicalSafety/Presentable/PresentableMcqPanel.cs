using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// World-space MCQ. Flat dark-blue panel, rounded option rows and orange corner brackets, shared with the
    /// property cards. Option text wraps inside its row. Green CORRECT popup + NEXT, red WRONG popup with X.
    /// Answers are poked with the gloved finger. Every scenario uses this one panel.
    /// </summary>
    public sealed class PresentableMcqPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Option
        {
            public string label;
            public string wrongLabel;
            [Tooltip("Optional red card title for this option (falls back to Question.wrongTitle).")]
            public string wrongTitle;
            public Sprite[] icons = Array.Empty<Sprite>();
            [Tooltip("Optional sticker drawn on top of the matching icon (same index).")]
            public Sprite[] overlays = Array.Empty<Sprite>();
            [TextArea(2, 4)] public string feedback;
            [Tooltip("One line shown under this option once the question is answered (inlineReveal).")]
            public string reason;
        }

        [Serializable]
        public sealed class Question
        {
            public string header = "PROFESSIONAL CHEMICAL SAFETY TRAINING - MCS Module";
            [TextArea(1, 3)] public string prompt;
            public Option[] options = Array.Empty<Option>();
            public int correctIndex;
            public string correctTitle = "CORRECT SELECTION!";
            public string wrongTitle = "WRONG SELECTION!";
            public string nextLabel = "NEXT";
            [Tooltip("Shared fancy look (SC01–SC10): options arrive one by one, glow and chevrons, a light comet round the frame (Sc06McqFx).")]
            public bool fancy;
            [Tooltip("Scenario 1: no result popup. The answer is marked on the rows (green tick + border on the right one, red cross on a wrong pick) with a one-line reason under every option, then NEXT. A wrong answer also goes on to NEXT (no retry).")]
            public bool inlineReveal;
            [Tooltip("A long question (3 lines): the title may shrink further to stay inside the header bar. Default false = every other scenario unchanged.")]
            public bool longPrompt;
            [Tooltip("inlineReveal: no NEXT button after the reveal; the director carries on by itself (Revealed event). Scenario 2, questions 1-5.")]
            public bool hideNext;
            [Tooltip("Scenario 1 (2026-11-18, long client texts): the box may grow taller than its normal size so four answers with their reasons fit without squeezing or touching the edges. Default false = every other scenario unchanged.")]
            public bool growBox;
            [Tooltip("Scenarios 1 and 2 (client 2026-11-18): the question is one size larger and bold, the answers are larger and never shrink below a readable size. Default false = every other scenario unchanged.")]
            public bool largeText;
            [Tooltip("Scenario 5 (client 2026-11-19): the frame's bezel and header strip are this much larger (1 = unchanged) so a two-line question stays clear of the header border.")]
            public float headerScale = 1f;
            [Tooltip("Scenario 5 (client 2026-11-19): the question stays pure white (not the light-blue title colour) at every stage.")]
            public bool whiteHeader;
            public string tagCorrect = "CORRECT";
            public string tagWrong = "WRONG";
            [NonSerialized] public Func<bool> holdNext; // inlineReveal: true while a cutscene must finish before NEXT shows
            [NonSerialized] public int[] sourceOrder; // for each shown slot, the index the option had before the per-session shuffle
            [NonSerialized] public Func<bool> holdCorrect; // true while a cutscene must finish before the right-answer card opens
        }

        private sealed class Row
        {
            public RectTransform root;
            public Button button;
            public Image fill;
            public Image frame;
            public Image check;
            public Image cross;
            public Image verdict; // coloured outline behind the row frame (inlineReveal)
            public Image bloom;   // soft outer glow behind the outline (inlineReveal)
            public Image wash;    // light colour wash over the bar (inlineReveal)
            public Image gloss;   // white highlight on the top half of the bar (inlineReveal)
            public Image badge;   // round glow behind the tick / cross (inlineReveal)
            public TextMeshProUGUI reason; // line under the row (inlineReveal)
            public TextMeshProUGUI reasonTag; // client design: the word "Reason:" / "কারণঃ" in front of the reason line
            public TextMeshProUGUI label;
            public Image[] icons;
            public Image[] overlays;
        }

        private static readonly Color RowFillGreen = new Color(0.05f, 0.42f, 0.24f, 0.96f);
        private static readonly Color RowBorderGreen = new Color(0.55f, 1f, 0.65f, 1f);
        private static readonly Color RowFillRed = new Color(0.42f, 0.06f, 0.06f, 0.96f);
        private static readonly Color RowFillRedDim = new Color(0.22f, 0.05f, 0.05f, 0.7f);
        private static readonly Color RowBorderRed = new Color(1f, 0.4f, 0.34f, 1f);
        private static readonly Color PanelFillRed = new Color(0.22f, 0.04f, 0.05f, 0.96f);

        [SerializeField] private PresentableArt art;
        [SerializeField] private Vector2 sizePx = new Vector2(1240f, 900f);
        [SerializeField] private float metersPerPixel = 0.00055f;

        private float _worldScale = 1f;
        public float WorldScale => _worldScale;

        // ---- client MCQ design (Scenarios 1-12): transparent glass frame, light-blue answer boxes that turn green / red, tick / cross.
        // Active only when the builder gave the panel the new sprites (PresentableSceneBuilder.McqNewDesign); otherwise nothing changes.
        private bool Skin => art != null && art.mcqFrame != null && art.mcqBoxBlue != null && art.mcqBoxGreen != null && art.mcqBoxRed != null;

        /// <summary>True when this panel wears the client MCQ design (the box effects of Sc06McqFx then stay off).</summary>
        public bool NewSkin => Skin;

        private static readonly Color SkinTitle = new Color(0.62f, 0.90f, 1f, 1f);
        private static readonly Color SkinIdle = new Color(0.88f, 0.95f, 1f, 1f);
        private const float SkinTop0 = 0.765f;
        private const float SkinSlot = 0.158f;
        private const float SkinRowH = 0.104f;
        private const float SkinRowHBn = 0.086f; // Bangla: shorter answer boxes leave room for a two-line, larger reason under each

        private static float SkinRowHNow => ChemicalSafety.Localization.Loc.IsBangla ? SkinRowHBn : SkinRowH;

        // largeText in the fixed (embedded callout) layout: slightly shorter answer boxes leave room for a two-line reason under each answer (English;
        // Bangla already has the short rows)
        private float RowHNow => _question != null && _question.largeText && !_dyn && !ChemicalSafety.Localization.Loc.IsBangla ? 0.082f : SkinRowHNow;
        private readonly List<GameObject> _skinDecor = new List<GameObject>();

        private static float SkinTop(int i) => SkinTop0 - i * SkinSlot;

        /// <summary>Controller-ray scenario: same card, same text sizes in pixels, scaled uniformly so it reads from further away.</summary>
        public void SetWorldScale(float scale)
        {
            _worldScale = Mathf.Max(0.1f, scale);
            transform.localScale = Vector3.one * _worldScale;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Question _question;
        private CanvasGroup _group;
        private Image _frame;
        private RectTransform _readPlate;
        private Image _panelFill;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI _prompt;
        private RectTransform _correctPopup;
        private TextMeshProUGUI _correctTitle;
        private TextMeshProUGUI _correctBody;
        private RectTransform _wrongPopup;
        private TextMeshProUGUI _wrongTitle;
        private TextMeshProUGUI _wrongBody;
        private RectTransform _nextButton;
        private TextMeshProUGUI _nextLabel;
        private RectTransform _inlineNext;

        // ---- dynamic box (new skin, fewer than 4 options): the box is as tall as the question and the options need, not four slots high.
        // Everything is laid out in the old 4-slot units (0..1 = the old canvas height) and DY() converts to the cropped canvas.
        /// <summary>The dark square plate that used to sit inside the glass (client: not in the design). 0 = off; 0.30 brings it back.</summary>
        private const float SkinReadPlateAlpha = 0f;
        private bool _dyn;
        private float _dynF = 1f, _dynB;
        private readonly float[] _dTop = new float[4], _dRowH = new float[4], _dReasonH = new float[4];
        private float _dNextTop, _dNextBottom;
        private RectTransform _frameRt, _qBadgeRt, _qMarkRt;
        private Sprite _slicedFrame;

        private float DY(float y) => _dyn ? (y - _dynB) / _dynF : y;

        /// <summary>Bangla text is an image picked by size (BnTmp never enlarges it, and takes the one-line image when everything fits): in the dynamic box the
        /// size caps are raised so the multi-line images are chosen and the Bangla reads as large as the English.</summary>
        // ---- ONE type system for the question boxes (client 2026-11-18). Fixed pixel sizes, never auto-sized, Bangla images never shrunk: the same size for
        // every question, answer and reason in Scenarios 1, 3, 4 (and, a little smaller because of its tighter fixed canvas, the Scenario 2 callout).
        // The box takes its height from the measured text (BuildDyn), the text never takes its size from the box.
        // client 2026-11-18: question, then answers ONE step (4 px) smaller, then reasons ONE step smaller again
        // (a Bangla image of the same nominal size is drawn about a third larger than Times New Roman, so Bangla uses a smaller triple, again 4 px per step)
        private static bool Bn => ChemicalSafety.Localization.Loc.IsBangla;
        private static float TypeQuestion => Bn ? 32f : 44f;
        private static float TypeAnswer => Bn ? 28f : 40f;
        private static float TypeReason => Bn ? 24f : 36f;
        private static float TypeTag => Bn ? 20f : 30f;
        private static float CalloutQuestion => Bn ? 28f : 36f;
        private static float CalloutAnswer => Bn ? 24f : 32f;
        private static float CalloutReason => Bn ? 20f : 28f;
        private static float CalloutTag => Bn ? 16f : 24f;

        public static float QuestionSize(bool embedded) => embedded ? CalloutQuestion : TypeQuestion;
        public static float AnswerSize(bool embedded) => embedded ? CalloutAnswer : TypeAnswer;
        public static float ReasonSize(bool embedded) => embedded ? CalloutReason : TypeReason;

        // A soft dark shadow behind the white text (English: TextMeshPro underlay; Bangla images: UI Shadow) so it reads on the blue glass.
        private static void SetTextShadow(TextMeshProUGUI t, bool on)
        {
            if (t == null)
                return;
            if (t is ChemicalSafety.Localization.BnTmp bt)
                bt.Shadowed = on;
            var m = t.fontMaterial;
            if (m == null)
                return;
            if (on)
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0f, 0.03f, 0.1f, 0.85f));
                m.SetFloat("_UnderlayOffsetX", 0.6f);
                m.SetFloat("_UnderlayOffsetY", -0.6f);
                m.SetFloat("_UnderlaySoftness", 0.3f);
                m.SetFloat("_UnderlayDilate", 0.15f);
            }
            else
            {
                m.DisableKeyword("UNDERLAY_ON");
            }
        }

        private void ApplyLargeText(bool on)
        {
            var emb = _embedded;
            if (_header != null)
            {
                _header.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                if (Skin)
                    _header.color = on ? Color.white : SkinTitle; // the question is pure white
                SetTextShadow(_header, on);
                _header.enableAutoSizing = !on;
                if (on)
                {
                    _header.fontSize = emb ? CalloutQuestion : TypeQuestion;
                    _header.textWrappingMode = TextWrappingModes.Normal;
                    _header.margin = new Vector4(6f, 0f, 6f, 0f); // the header pane is one fixed strip: two lines of the question fit it only without a vertical margin
                }
                else
                {
                    _header.fontSizeMax = Skin ? 30f : 28f;
                    if (Skin)
                        _header.margin = new Vector4(4f, 0f, 4f, 0f);
                }

                if (_header is ChemicalSafety.Localization.BnTmp hb)
                    hb.NoShrink = on;
            }

            foreach (var r in _rows)
            {
                if (r.label != null)
                {
                    r.label.enableAutoSizing = !on;
                    if (on)
                    {
                        r.label.fontSize = emb ? CalloutAnswer : TypeAnswer;
                        r.label.textWrappingMode = TextWrappingModes.Normal;
                        r.label.overflowMode = TextOverflowModes.Overflow;
                    }
                    else
                    {
                        r.label.fontSizeMin = 14f;
                        r.label.fontSizeMax = 28f;
                    }

                    if (r.label is ChemicalSafety.Localization.BnTmp lb)
                        lb.NoShrink = on;
                    if (on)
                        r.label.color = Color.white;
                    SetTextShadow(r.label, on);
                }

                if (r.reason != null)
                {
                    r.reason.enableAutoSizing = !on;
                    if (on)
                    {
                        r.reason.fontSize = emb ? CalloutReason : TypeReason;
                        r.reason.textWrappingMode = TextWrappingModes.Normal;
                        r.reason.overflowMode = TextOverflowModes.Overflow;
                    }

                    if (r.reason is ChemicalSafety.Localization.BnTmp rb)
                        rb.NoShrink = on;
                    SetTextShadow(r.reason, on);
                }

                if (r.reasonTag != null)
                {
                    r.reasonTag.enableAutoSizing = !on;
                    if (on)
                        r.reasonTag.fontSize = emb ? CalloutTag : TypeTag;
                    else
                    {
                        r.reasonTag.fontSizeMin = 14f;
                        r.reasonTag.fontSizeMax = 22f;
                    }

                    if (r.reasonTag is ChemicalSafety.Localization.BnTmp tb)
                        tb.NoShrink = on;
                    SetTextShadow(r.reasonTag, on);
                }
            }
        }

        private void SetDynFonts(bool on)
        {
            if (_header is ChemicalSafety.Localization.BnTmp hb)
                hb.AvoidCapped = on;
            foreach (var r in _rows)
            {
                if (r.label is ChemicalSafety.Localization.BnTmp lb)
                    lb.AvoidCapped = on;
                if (r.reason is ChemicalSafety.Localization.BnTmp rb)
                    rb.AvoidCapped = on;
            }
        }

        /// <summary>Dynamic box: the reasons wrap over their taller space and are as large as it allows (the 4-slot box keeps one shrunken line).</summary>
        private void SetReasonWrap(bool wrap)
        {
            foreach (var r in _rows)
            {
                if (r.reason == null)
                    continue;
                if (_question != null && _question.largeText)
                    continue; // fixed size, wrapped by the measured layout (ApplyLargeText)
                r.reason.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                r.reason.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
                var big = _question != null && _question.largeText;
                // client 2026-11-18: the reasons were too small to read, larger now (the boxes make room: see BuildDyn / RowHNow)
                r.reason.fontSizeMax = wrap ? (big ? (_embedded ? 29f : 27f) : 24f) : 22f;
                r.reason.fontSizeMin = wrap ? (big ? (_embedded ? 20f : 19f) : 12f) : 14f;
            }
        }

        private void SetA(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, DY(y0));
            r.anchorMax = new Vector2(x1, DY(y1));
        }

        private static int TextLines(string text, int perLine)
        {
            if (string.IsNullOrEmpty(text))
                return 1;
            var lines = 0;
            foreach (var part in text.Split('\n'))
                lines += Mathf.Max(1, Mathf.CeilToInt(part.Length / (float)perLine));
            return lines;
        }

        /// <summary>Header, Q badge, ? mark and the (optional) plate on the current canvas (old units, converted by DY).</summary>
        private void LayoutChrome()
        {
            if (!Skin)
                return;
            if (_dyn && !_embedded)
            {
                // the frame is cut into a 9-slice and its header pane is at a fixed pixel position: the title, the Q and the ? sit on the pane's middle
                var yc = 1f - PaneCenterPx(_dynF) / (_dynF * sizePx.y) - 0.012f; // a hair below the middle: clear of the bezel arcs
                void Band(RectTransform r, float x0, float x1, float halfOld)
                {
                    r.anchorMin = new Vector2(x0, yc - halfOld / _dynF);
                    r.anchorMax = new Vector2(x1, yc + halfOld / _dynF);
                }

                Band(_header.rectTransform, 0.17f, 0.89f, 0.062f * HeaderScale); // as tall as the pane's inside: a long (Bangla) title shrinks to fit it, it never spills out
                if (_qBadgeRt != null)
                    Band(_qBadgeRt, 0.075f, 0.16f, 0.0575f);
                if (_qMarkRt != null)
                    Band(_qMarkRt, 0.9f, 0.95f, 0.035f);
                SetA(_readPlate, 0.045f, 0.075f, 0.955f, 0.925f);
                return;
            }

            // callout (embedded, largeText): the question band sits a little lower so a 2-3 line question stays inside the header pane
            var calloutHead = _embedded && _question != null && _question.largeText;
            // the callout's header pane (the frame art, stretched over 900 px) spans about 0.77 - 0.98 of the height; the question is centred in it
            SetA(_header.rectTransform, calloutHead ? 0.15f : 0.17f, calloutHead ? 0.777f : 0.785f, calloutHead ? 0.9f : 0.89f, calloutHead ? 0.975f : 0.955f);
            if (_qBadgeRt != null)
                SetA(_qBadgeRt, 0.075f, 0.84f, 0.16f, 0.955f);
            if (_qMarkRt != null)
                SetA(_qMarkRt, 0.9f, 0.87f, 0.95f, 0.94f);
            SetA(_readPlate, 0.045f, 0.075f, 0.955f, 0.925f);
        }

        private void SetFrameSliced(bool on)
        {
            if (_frame == null || art.mcqFrame == null)
                return;
            if (!on)
            {
                _frame.sprite = art.mcqFrame;
                _frame.type = Image.Type.Simple;
                _frame.pixelsPerUnitMultiplier = 1f;
                return;
            }

            if (_slicedFrame == null)
            {
                var src = art.mcqFrame;
                var r = src.rect;
                // the glass between the top pane and the bottom ornament can stretch; the bezel, the header pane and the bottom tab keep their size
                _slicedFrame = Sprite.Create(src.texture, r, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect,
                    new Vector4(r.width * 0.075f, r.height * 0.10f, r.width * 0.075f, r.height * 0.22f));
                _slicedFrame.name = "McqFrameSliced";
            }

            _frame.sprite = _slicedFrame;
            _frame.type = Image.Type.Sliced;
            var cv = _frame.canvas;
            var refPpu = cv != null ? cv.referencePixelsPerUnit : 100f;
            _frame.pixelsPerUnitMultiplier = refPpu / _slicedFrame.pixelsPerUnit * (_slicedFrame.rect.width / (sizePx.x * 1.1f)) / HeaderScale;
        }

        // a larger header strip (Scenario 5 only; every other box keeps 1): the sliced frame's borders, including the header pane, grow by this factor
        private float HeaderScale => _question != null && _question.headerScale > 1f ? _question.headerScale : 1f;

        private Color HeaderColour() => _question != null && _question.whiteHeader ? Color.white : (Skin ? SkinTitle : Color.white);

        // The two stages of the dynamic box: the QUESTION (the options close together, the box as small as they need) and the REVEAL (room for
        // a reason under every option and the NEXT button). The box grows from one to the other in a quarter of a second when an answer is picked.
        private sealed class DynLayout
        {
            public float f, b, nextTop, nextBottom;
            public readonly float[] top = new float[4], rowH = new float[4], reasonH = new float[4];
        }

        // growBox questions (Scenarios 1, 3, 4; client 2026-11-18): the box is wider as well as taller (frame proportions kept: the bevel stays the same size in
        // pixels), so the reasons fit on fewer lines and the box does not have to grow so tall
        private const float WideK = 1.4f;
        private float _wk = 1f;

        private DynLayout _dynQ, _dynR;
        private int _dynN;

        /// <summary>Pixels (old canvas units) from the TOP of the frame art to the bottom of its header pane, and to the middle of the pane.</summary>
        private float PaneBottomPx(float f)
        {
            var src = art.mcqFrame;
            var s = sizePx.x * 1.1f / src.rect.width * HeaderScale; // the sliced frame scales its borders uniformly with the width
            return 0.205f * src.rect.height * s - 0.05f * f * sizePx.y;
        }

        private float PaneCenterPx(float f)
        {
            var src = art.mcqFrame;
            var s = sizePx.x * 1.1f / src.rect.width * HeaderScale;
            return 0.125f * src.rect.height * s - 0.05f * f * sizePx.y;
        }

        private DynLayout BuildDyn(Question q, bool reveal)
        {
            var n = q.options.Length;
            var bn = ChemicalSafety.Localization.Loc.IsBangla;
            var rowH = new float[4];
            var reasonH = new float[4];
            float gap;
            if (q.largeText)
            {
                // ---- measured layout (client 2026-11-18): every text is measured at its FIXED size in its real slot width; nothing is guessed from a character
                // count and nothing is squeezed, so the box simply takes the height the text needs
                var H = sizePx.y;
                var W = sizePx.x * _wk;
                var reasonW = W * (0.95f - 0.205f) - 24f;
                for (var i = 0; i < n; i++)
                {
                    var o = q.options[i];
                    var nIcons = o.icons == null ? 0 : Mathf.Min(o.icons.Length, 3);
                    var labelRight = nIcons > 0 ? (q.inlineReveal ? 0.885f : 0.95f) - nIcons * (0.082f + 0.014f) - 0.02f : 0.92f;
                    var labelW = 0.9f * W * (labelRight - 0.125f) - 24f;
                    rowH[i] = Mathf.Max(74f, MeasureTextPx(_rows[i].label, o.label, labelW) + (Bn ? 48f : 36f)) / H;
                    reasonH[i] = reveal ? (MeasureTextPx(_rows[i].reason, o.reason, reasonW) + (Bn ? 36f : 20f)) / H : 0f;
                }

                gap = (reveal ? 14f : 26f) / H;
                // the box is never flatter than 1.46 : 1 (the designed proportion is 1.38 : 1): short content gets more air instead
                var minF = _embedded ? 1f : 1240f * _wk / (900f * 1.46f);
                return SolveDyn(rowH, reasonH, gap, n, reveal, q.growBox ? 2.6f : 1.0f, minF);
            }

            var wideK = q.growBox ? WideK : 1f;
            var capLabel = Mathf.RoundToInt((bn ? 30 : 56) * wideK);   // characters per line of an answer at a readable size
            var capReason = Mathf.RoundToInt((bn ? 34 : 84) * wideK);  // the reason is smaller (Bangla needs more, shorter lines)
            var rowLine = bn ? 0.044f : 0.036f;
            var reasonLine = bn ? 0.042f : 0.032f;
            var maxLabelLines = bn ? 5 : 4;
            var maxReasonLines = bn ? 6 : 4;
            gap = reveal ? 0.018f : 0.030f;
            for (var i = 0; i < n; i++)
            {
                rowH[i] = 0.066f + rowLine * Mathf.Min(maxLabelLines, TextLines(q.options[i].label, capLabel));
                reasonH[i] = reveal ? 0.020f + reasonLine * Mathf.Min(maxReasonLines, TextLines(q.options[i].reason, capReason)) : 0f;
            }

            return SolveDyn(rowH, reasonH, gap, n, reveal, q.growBox ? 1.5f : 1f, 0f);
        }

        /// <summary>
        /// Typography check for the smoke test (client 2026-11-18: every question / answer / reason of a box must be drawn at ONE size, nothing shrunk, nothing
        /// outside its slot). Lists the size every visible text is drawn at; ok = false when a group is not uniform, a Bangla image was scaled down, or a text
        /// does not fit its slot.
        /// </summary>
        public string TypeReport(out bool ok)
        {
            ok = true;
            var sb = new System.Text.StringBuilder();

            float Size(TextMeshProUGUI t, out bool fits, out bool shrunk)
            {
                fits = true;
                shrunk = false;
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text))
                    return -1f;
                var rect = t.rectTransform.rect;
                if (t is ChemicalSafety.Localization.BnTmp bt && ChemicalSafety.Localization.Loc.IsBangla && bt.ShowingImages)
                {
                    fits = bt.LastImageHeight <= rect.height + 6f;
                    shrunk = bt.LastShrink < 0.98f;
                    return t.fontSize * bt.LastShrink;
                }

                fits = t.preferredHeight <= rect.height + 6f;
                return t.fontSize;
            }

            void Group(string name, System.Collections.Generic.List<float> sizes, ref bool good)
            {
                if (sizes.Count == 0)
                    return;
                var lo = Mathf.Min(sizes.ToArray());
                var hi = Mathf.Max(sizes.ToArray());
                sb.Append(" " + name + "=" + lo.ToString("0.#") + (hi - lo > 0.5f ? ".." + hi.ToString("0.#") + " NOT-UNIFORM" : ""));
                if (hi - lo > 0.5f)
                    good = false;
            }

            var labels = new System.Collections.Generic.List<float>();
            var reasons = new System.Collections.Generic.List<float>();
            var good2 = true;
            for (var i = 0; i < _rows.Count && _question != null && i < _question.options.Length; i++)
            {
                var r = _rows[i];
                var s1 = Size(r.label, out var f1, out var sh1);
                if (s1 > 0f)
                {
                    labels.Add(s1);
                    if (!f1)
                    {
                        sb.Append(" label" + (i + 1) + "-OVERFLOW");
                        good2 = false;
                    }

                    if (sh1)
                    {
                        sb.Append(" label" + (i + 1) + "-SHRUNK");
                        good2 = false;
                    }
                }

                var s2 = Size(r.reason, out var f2, out var sh2);
                if (s2 > 0f)
                {
                    reasons.Add(s2);
                    if (!f2)
                    {
                        sb.Append(" reason" + (i + 1) + "-OVERFLOW");
                        good2 = false;
                    }

                    if (sh2)
                    {
                        sb.Append(" reason" + (i + 1) + "-SHRUNK");
                        good2 = false;
                    }
                }
            }

            var q = Size(_header, out var fq, out var shq);
            if (q > 0f)
            {
                sb.Append(" question=" + q.ToString("0.#"));
                if (!fq)
                {
                    sb.Append(" question-OVERFLOW");
                    good2 = false;
                }

                if (shq)
                {
                    sb.Append(" question-SHRUNK");
                    good2 = false;
                }
            }

            Group("answers", labels, ref good2);
            Group("reasons", reasons, ref good2);
            sb.Append(" box=" + (CanvasRect != null ? CanvasRect.sizeDelta.x.ToString("0") + "x" + CanvasRect.sizeDelta.y.ToString("0") : "?"));
            ok = good2;
            return sb.ToString();
        }

        // Height of a text in pixels at its current (fixed) font in a slot widthPx wide: the Bangla image that would be drawn, or the TextMeshPro layout.
        private static float MeasureTextPx(TextMeshProUGUI t, string text, float widthPx)
        {
            if (t == null || string.IsNullOrEmpty(text))
                return 0f;
            if (t is ChemicalSafety.Localization.BnTmp bt)
            {
                var h = bt.MeasureHeight(text, widthPx);
                if (h >= 0f)
                    return h;
            }

            return t.GetPreferredValues(text, widthPx, 0f).y;
        }

        // Places the rows, reasons and NEXT from their heights (fractions of the normal box height) and finds the box height f. In the Scenario 2 callout
        // (embedded) the box height is fixed: the rows start under the header band and spare room becomes air between the rows.
        private DynLayout SolveDyn(float[] rowH, float[] reasonH, float gap, int n, bool reveal, float maxF, float minF)
        {
            const float nextH = 0.094f, nextGap = 0.010f, bottomPad = 0.075f;
            var L = new DynLayout();
            var total = 0f;
            for (var i = 0; i < n; i++)
                total += rowH[i] + reasonH[i] + gap;

            if (_embedded)
            {
                const float start = 0.752f;
                var tailE = reveal ? nextH + nextGap + 0.03f : 0.03f;
                var avail = start - tailE;
                var gapE = gap;
                var kE = 1f;
                if (total > avail)
                    kE = avail / total;
                else
                    gapE = Mathf.Min(gap + (avail - total) / Mathf.Max(1, n), 0.05f);
                var cur = start;
                for (var i = 0; i < n; i++)
                {
                    L.rowH[i] = rowH[i] * kE;
                    L.reasonH[i] = reasonH[i] * kE;
                    L.top[i] = cur;
                    cur -= L.rowH[i] + L.reasonH[i] + gapE * kE;
                }

                L.nextTop = cur - nextGap;
                L.nextBottom = L.nextTop - nextH;
                L.f = 1f;
                L.b = 0f;
                return L;
            }

            var f = 0.85f;
            for (var pass = 0; pass < 2; pass++)
            {
                // the first answer keeps the same distance below the header pane as in the 4-slot box (53 px)
                var start = 1f - (PaneBottomPx(f) + 53f) / sizePx.y;
                var tail = reveal ? nextH + nextGap + bottomPad : bottomPad;
                var budget = start - tail + (maxF - 1f);
                var k = total > budget ? budget / total : 1f;
                var cursor = start;
                for (var i = 0; i < n; i++)
                {
                    L.rowH[i] = rowH[i] * k;
                    L.reasonH[i] = reasonH[i] * k;
                    L.top[i] = cursor;
                    cursor -= L.rowH[i] + L.reasonH[i] + gap * k;
                }

                if (reveal)
                {
                    L.nextTop = cursor - nextGap;
                    L.nextBottom = L.nextTop - nextH;
                    L.b = L.nextBottom - bottomPad;
                }
                else
                {
                    L.nextTop = L.nextBottom = cursor;
                    L.b = cursor - bottomPad + gap * k;
                }

                f = Mathf.Clamp(1f - L.b, 0.45f, maxF);
            }

            if (f < minF)
            {
                // spread the spare height as air between the rows so the box keeps its designed proportion
                var extra = (minF - f) / Mathf.Max(1, n);
                var cur2 = 1f - (PaneBottomPx(minF) + 53f) / sizePx.y;
                for (var i = 0; i < n; i++)
                {
                    L.top[i] = cur2;
                    cur2 -= L.rowH[i] + L.reasonH[i] + gap + extra;
                }

                if (reveal)
                {
                    L.nextTop = cur2 - nextGap;
                    L.nextBottom = L.nextTop - nextH;
                }
                else
                {
                    L.nextTop = L.nextBottom = cur2;
                }

                f = minF;
            }

            L.f = f;
            L.b = 1f - f;
            return L;
        }

        private void SetDyn(float t)
        {
            var qL = _dynQ;
            var rL = _dynR;
            _dynF = Mathf.Lerp(qL.f, rL.f, t);
            _dynB = 1f - _dynF;
            for (var i = 0; i < _dynN; i++)
            {
                _dTop[i] = Mathf.Lerp(qL.top[i], rL.top[i], t);
                _dRowH[i] = Mathf.Lerp(qL.rowH[i], rL.rowH[i], t);
                _dReasonH[i] = Mathf.Lerp(qL.reasonH[i], rL.reasonH[i], t);
            }

            _dNextTop = Mathf.Lerp(qL.nextTop, rL.nextTop, t);
            _dNextBottom = Mathf.Lerp(qL.nextBottom, rL.nextBottom, t);
            PlaceDyn();
        }

        /// <summary>Canvas, frame, header and every row / reason / NEXT of the dynamic box at the current blend.</summary>
        private void PlaceDyn()
        {
            if (!_embedded)
            {
            CanvasRect.sizeDelta = new Vector2(sizePx.x * _wk, sizePx.y * _dynF);
            _frameRt.sizeDelta = new Vector2(sizePx.x * _wk * 1.1f, sizePx.y * _dynF * 1.1f);
            // a box taller than its normal size (growBox): it grows mostly downwards so its top edge stays clear of the top bar / the view's upper edge
            var lp = _canvasBasePos;
            if (_dynF > 1f)
                lp.y -= (_dynF - 1f) * sizePx.y * 0.5f * CanvasRect.localScale.y; // top edge stays where the normal box's top is
            CanvasRect.localPosition = lp;
            }

            LayoutChrome();
            for (var i = 0; i < _dynN && i < _rows.Count; i++)
            {
                var r = _rows[i];
                r.root.anchorMin = new Vector2(0.05f, DY(_dTop[i] - _dRowH[i]));
                r.root.anchorMax = new Vector2(0.95f, DY(_dTop[i]));
                var top = _dTop[i] - _dRowH[i];
                var space = Mathf.Max(0.01f, _dReasonH[i] - 0.006f);
                r.reason.rectTransform.anchorMin = new Vector2(0.205f, DY(top - space - 0.002f));
                r.reason.rectTransform.anchorMax = new Vector2(0.95f, DY(top - 0.004f)); // up to the right edge of the answer box
                r.reasonTag.rectTransform.anchorMin = new Vector2(0.105f, DY(top - space - 0.002f));
                r.reasonTag.rectTransform.anchorMax = new Vector2(0.2f, DY(top - 0.004f));
            }

            SetA(_inlineNext, 0.5f - 0.18f / _wk, _dNextBottom, 0.5f + 0.18f / _wk, _dNextTop); // the NEXT pill keeps its size in a wider box
        }

        private IEnumerator DynBlend(float from, float to, float seconds)
        {
            var t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetDyn(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds))));
                yield return null;
            }

            SetDyn(to);
        }

        /// <summary>Called by Show: fits the box to the question (new skin with 1-3 options) or restores the 4-slot box.</summary>
        private void ApplyDynamicLayout(Question q)
        {
            var n = q.options != null ? q.options.Length : 0;
            _wk = 1f;
            // the measured layout also drives the Scenario 2 callout (embedded: fixed canvas, rows placed from the measured heights)
            var want = Skin && n >= 1 && (_embedded ? q.largeText : (n < 4 || q.growBox));
            if (!want)
            {
                if (_dyn)
                {
                    _dyn = false;
                    _dynF = 1f;
                    _dynB = 0f;
                    if (!_embedded)
                    {
                        CanvasRect.sizeDelta = sizePx;
                        CanvasRect.localPosition = _canvasBasePos;
                        _frameRt.sizeDelta = sizePx * 1.1f;
                        SetFrameSliced(false);
                    }

                    SetReasonWrap(false);
                    SetDynFonts(false);
                    LayoutChrome();
                }

                SetReasonWrap(q.largeText);
                LayoutChrome();
                return;
            }

            _dynN = n;
            if (q.largeText && q.growBox && !_embedded)
            {
                // the box keeps the designed proportion (1240 x 900 = 1.38 : 1): the narrowest width K (1.0 .. 1.6) at which the box is at least 1.3 : 1 wide
                // (more text -> taller AND wider together, never a flat strip)
                for (var k = 1f; k <= 1.601f; k += 0.05f)
                {
                    _wk = k;
                    _dynR = BuildDyn(q, true);
                    // the question must fit the header strip in at most two lines at this width
                    var headFits = _header == null || MeasureTextPx(_header, _header.text, 0.72f * 1240f * k - 12f) <= 0.124f * sizePx.y * HeaderScale - (Bn ? 22f : 2f);
                    if (headFits && 1240f * k / (900f * _dynR.f) >= 1.3f)
                        break;
                }

                _dynQ = _dynR; // sized ONCE for the final layout: nothing grows or moves when the answer is shown (as the original box)
            }
            else
            {
                _dynR = BuildDyn(q, true);
                _dynQ = q.largeText ? _dynR : BuildDyn(q, false);
            }

            _dyn = true;
            if (!_embedded)
                SetFrameSliced(true);
            SetReasonWrap(true);
            SetDynFonts(true);
            SetDyn(0f); // the question stage; the reveal stage is blended in by LayoutReveal
        }

        private TextMeshProUGUI _inlineNextLabel;
        private Image _inlineNextGlow;
        private bool _locked;
        private bool _built;
        private Sc06McqFx _fx;

        public PresentableArt Art => art;
        public RectTransform CanvasRect { get; private set; }

        /// <summary>World height of the bottom edge of the frame (Scenario 4 keeps it above the bench).</summary>
        public float FrameBottomWorldY
        {
            get
            {
                var rt = _frameRt != null ? _frameRt : CanvasRect;
                if (rt == null)
                    return float.MinValue;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Mathf.Min(Mathf.Min(c[0].y, c[1].y), Mathf.Min(c[2].y, c[3].y));
            }
        }

        /// <summary>World height of the top edge of the frame (for keeping the box below the top bar).</summary>
        public float FrameTopWorldY
        {
            get
            {
                var rt = _frameRt != null ? _frameRt : CanvasRect;
                if (rt == null)
                    return float.MaxValue;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Mathf.Max(Mathf.Max(c[0].y, c[1].y), Mathf.Max(c[2].y, c[3].y));
            }
        }
        /// <summary>Height of the box relative to its normal size when it is taller (growBox), else 1 (used by the smoke test's pixel-based margins).</summary>
        public float TallScale => _dyn && _dynF > 1f ? _dynF : 1f;
        private Vector3 _canvasBasePos;
        public int RowCount => _rows.Count;
        public RectTransform RowRoot(int index) => _rows[index].root;
        public bool Fancy => _fx != null;
        public Sc06McqFx Fx => _fx;

        /// <summary>(index, isCorrect)</summary>
        public event Action<int, bool> Answered;
        public event Action NextPressed;

        /// <summary>inlineReveal with hideNext: the answer is marked on the rows and the reasons are up (isCorrect).</summary>
        public event Action<bool> Revealed;

        /// <summary>Embedded in a callout (Scenario 2): the answer was given; the callout, not this panel, takes the result colour.</summary>
        public event Action<bool> EmbeddedResult;

        private bool _embedded;

        /// <summary>
        /// Embedded: the panel draws no frame and no dark plate of its own (the callout around it is the frame) and its own box
        /// effects are off, so the question sits on the right half of the callout.
        /// </summary>
        public void SetEmbedded(bool on)
        {
            _embedded = on;
            if (_fx != null)
                _fx.Embedded = on;
        }

        /// <summary>Raised when the red card is closed with its X and the question is open for a retry.</summary>
        public event Action WrongClosed;

        /// <summary>Raised once the correct / wrong popup is fully on screen (isCorrect).</summary>
        public event Action<bool> PopupShown;

        public bool IsOpen { get; private set; }

        public Question Current => _question;

        private void Awake()
        {
            Build();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        public void Show(Question question)
        {
            Build();
            _question = question;
            SetQuestionChrome(true);
            _locked = false;
            IsOpen = true;
            _header.text = Skin && !string.IsNullOrEmpty(question.prompt) ? question.prompt : question.header;
            _header.fontSizeMin = question.longPrompt ? 12f : 18f;
            ApplyLargeText(question.largeText);
            _prompt.text = question.prompt;

            for (var i = 0; i < _rows.Count; i++)
            {
                var active = i < question.options.Length;
                _rows[i].root.gameObject.SetActive(active);
                if (!active)
                    continue;

                var opt = question.options[i];
                _rows[i].label.text = opt.label;
                var n = opt.icons == null ? 0 : Mathf.Min(opt.icons.Length, _rows[i].icons.Length);
                const float iconW = 0.082f;
                const float iconGap = 0.014f;
                // inlineReveal (Scenario 1): the icons keep clear of the » chevron at the right edge of the row
                var iconRight = question.inlineReveal ? 0.885f : 0.95f;
                var labelRight = n > 0 ? iconRight - n * (iconW + iconGap) - 0.02f : 0.92f;
                _rows[i].label.rectTransform.anchorMin = new Vector2(0.06f, 0.08f);
                _rows[i].label.rectTransform.anchorMax = new Vector2(labelRight, 0.92f);
                _rows[i].label.alignment = TextAlignmentOptions.MidlineLeft;
                for (var k = 0; k < _rows[i].icons.Length; k++)
                {
                    var has = k < n && opt.icons[k] != null;
                    var icon = _rows[i].icons[k];
                    icon.gameObject.SetActive(has);
                    if (!has)
                        continue;
                    icon.sprite = opt.icons[k];
                    var x1 = iconRight - (n - 1 - k) * (iconW + iconGap);
                    icon.rectTransform.anchorMin = new Vector2(x1 - iconW, 0.2f);
                    icon.rectTransform.anchorMax = new Vector2(x1, 0.8f);
                    var sticker = opt.overlays != null && k < opt.overlays.Length ? opt.overlays[k] : null;
                    _rows[i].overlays[k].gameObject.SetActive(sticker != null);
                    _rows[i].overlays[k].sprite = sticker;
                }
            }

            ApplyDynamicLayout(question);
            ApplyNormalState();
            ResetInline();
            _correctPopup.gameObject.SetActive(false);
            _wrongPopup.gameObject.SetActive(false);
            _nextButton.gameObject.SetActive(false);
            StopAllCoroutines();
            _fx = null;
            var oldFx = GetComponent<Sc06McqFx>();
            if (question.fancy)
            {
                _fx = oldFx != null ? oldFx : gameObject.AddComponent<Sc06McqFx>();
                _fx.Embedded = _embedded;
                _fx.Attach(this);
            }
            else if (oldFx != null)
            {
                oldFx.Detach();
            }

            StartCoroutine(PopIn());
        }

        public void Hide()
        {
            IsOpen = false;
            if (_fx != null)
                _fx.Stop();
            StopAllCoroutines();
            StartCoroutine(PopOut());
        }

        private void OnOptionClicked(int index)
        {
            if (_locked || _question == null)
                return;
            if (index < 0 || index >= _question.options.Length)
            {
                Debug.LogWarning("[Mcq] row " + index + " clicked but the question has " + _question.options.Length + " options (" + name + ")");
                return;
            }

            _locked = true;
            var correct = index == _question.correctIndex;
            var opt = _question.options[index];

            if (_question.inlineReveal)
                StartCoroutine(ShowInline(index, correct));
            else if (correct)
                StartCoroutine(ShowCorrect(index, opt));
            else
                StartCoroutine(ShowWrong(index, opt));

            Answered?.Invoke(index, correct);
        }

        private IEnumerator ShowCorrect(int index, Option opt)
        {
            SetQuestionChrome(false);
            var row = _rows[index];
            row.check.gameObject.SetActive(true);
            PaintRow(row, true, Color.white);
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i != index)
                    _rows[i].root.GetComponent<CanvasGroup>().alpha = 0.55f;
            }

            _correctTitle.text = _question.correctTitle;
            _correctBody.text = opt.feedback;
            while (_question.holdCorrect != null && _question.holdCorrect())
                yield return null;
            PresentableFx.Play(PresentableFx.Success);
            PresentableFx.Haptic(true, 0.6f, 0.15f);
            if (_fx != null)
                _fx.OnResult(true, index);
            yield return PopupIn(_correctPopup);
            PopupShown?.Invoke(true);

            _nextLabel.text = _question.nextLabel;
            yield return PopupIn(_nextButton);
        }

        private IEnumerator ShowWrong(int index, Option opt)
        {
            PresentableFx.Play(PresentableFx.Error);
            PresentableFx.Haptic(true, 0.9f, 0.25f);

            SetQuestionChrome(false);
            _header.color = Color.white;
            for (var i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                var selected = i == index;
                PaintRow(r, selected, selected ? new Color(1f, 0.72f, 0.68f) : new Color(1f, 1f, 1f, 0.45f));
                r.root.GetComponent<CanvasGroup>().alpha = selected ? 1f : 0.6f;
            }

            if (!string.IsNullOrEmpty(opt.wrongLabel))
                _rows[index].label.text = opt.wrongLabel;

            if (_fx != null)
                _fx.OnResult(false, index);
            _wrongTitle.text = string.IsNullOrEmpty(opt.wrongTitle) ? _question.wrongTitle : opt.wrongTitle;
            _wrongBody.text = opt.feedback;
            StartCoroutine(Shake(_rows[index].root));
            yield return PopupIn(_wrongPopup);
            PopupShown?.Invoke(false);
        }

        // ------------------------------------------------------------------ inlineReveal (Scenario 1)

        private const float RowH = 0.128f;
        private const float RowGap = 0.012f;
        private const float RevealRowH = 0.105f;
        private const float RevealSlot = 0.1575f;

        // Scenario 1 result colours: emerald and ruby (jewel tones with a light edge), not the plain RGB green / red
        public static readonly Color EmeraldMain = new Color(0.13f, 0.90f, 0.63f, 1f);
        public static readonly Color EmeraldLight = new Color(0.62f, 1f, 0.86f, 1f);
        public static readonly Color RubyMain = new Color(1f, 0.24f, 0.37f, 1f);
        public static readonly Color RubyLight = new Color(1f, 0.64f, 0.70f, 1f);

        /// <summary>Normal question layout again (prompt visible, rows in their slots) and the reveal pieces hidden.</summary>
        private void ResetInline()
        {
            if (Skin)
            {
                SetA(_readPlate, 0.045f, 0.075f, 0.955f, 0.925f);
            }
            else
            {
                _readPlate.anchorMin = new Vector2(0.06f, 0.10f);
                _readPlate.anchorMax = new Vector2(0.94f, 0.83f);
            }

            _prompt.gameObject.SetActive(!Skin);
            _inlineNext.gameObject.SetActive(false);
            if (Skin)
            {
                var nr = _inlineNext;
                if (_dyn)
                {
                    SetA(nr, 0.5f - 0.18f / _wk, _dNextBottom, 0.5f + 0.18f / _wk, _dNextTop); // inside the box, clear of the bottom ornament
                }
                else
                {
                    nr.anchorMin = new Vector2(0.32f, 0.012f);
                    nr.anchorMax = new Vector2(0.68f, 0.108f);
                }
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var top = Skin ? (_dyn ? _dTop[i] : SkinTop(i)) : 0.655f - i * (RowH + RowGap);
                var rowH = Skin ? (_dyn ? _dRowH[i] : RowHNow) : RowH;
                var r = _rows[i];
                r.root.anchorMin = new Vector2(0.05f, DY(top - rowH));
                r.root.anchorMax = new Vector2(0.95f, DY(top));
                r.verdict.gameObject.SetActive(false);
                r.bloom.gameObject.SetActive(false);
                r.wash.gameObject.SetActive(false);
                r.gloss.gameObject.SetActive(false);
                r.badge.gameObject.SetActive(false);
                r.cross.gameObject.SetActive(false);
                r.reason.gameObject.SetActive(false);
                if (r.reasonTag != null)
                    r.reasonTag.gameObject.SetActive(false);
                r.label.rectTransform.anchorMin = new Vector2(Skin ? 0.125f : 0.06f, r.label.rectTransform.anchorMin.y);
            }
        }

        // Rows move up (the question line is gone) so every option has a one-line reason under it, and NEXT fits below.
        private void LayoutReveal()
        {
            if (Skin && _dyn)
            {
                StartCoroutine(DynBlend(0f, 1f, 0.28f)); // the box grows: room for the reasons and NEXT
                return;
            }

            if (Skin)
            {
                // rows stay where they are (the question stays on top); only the reason lines appear under them
                for (var i = 0; i < _rows.Count && i < _question.options.Length; i++)
                {
                    var rr = _rows[i].reason.rectTransform;
                    var top = _dyn ? _dTop[i] - _dRowH[i] : SkinTop(i) - RowHNow;
                    var space = _dyn ? _dReasonH[i] - 0.006f : SkinSlot - RowHNow - 0.008f; // the whole gap down to the next answer box
                    rr.anchorMin = new Vector2(0.205f, DY(top - space - 0.002f));
                    rr.anchorMax = new Vector2(0.95f, DY(top - 0.004f));
                    var tg = _rows[i].reasonTag.rectTransform;
                    tg.anchorMin = new Vector2(0.105f, DY(top - space - 0.002f));
                    tg.anchorMax = new Vector2(0.2f, DY(top - 0.004f));
                }

                return;
            }

            _prompt.gameObject.SetActive(false);
            _readPlate.anchorMin = new Vector2(0.06f, 0.17f);
            _readPlate.anchorMax = new Vector2(0.94f, 0.83f);
            for (var i = 0; i < _rows.Count && i < _question.options.Length; i++)
            {
                var top = 0.825f - i * RevealSlot;
                var r = _rows[i];
                r.root.anchorMin = new Vector2(0.05f, top - RevealRowH);
                r.root.anchorMax = new Vector2(0.95f, top);
                r.label.rectTransform.anchorMin = new Vector2(0.105f, r.label.rectTransform.anchorMin.y);
                var rr = r.reason.rectTransform;
                rr.anchorMin = new Vector2(0.09f, top - RevealRowH - 0.0485f);
                rr.anchorMax = new Vector2(0.94f, top - RevealRowH - 0.0035f);
            }
        }

        private static IEnumerator SkinMarkPop(RectTransform rt)
        {
            rt.localScale = Vector3.zero;
            yield return PresentableFx.Tween(0.4f, t => rt.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, t), PresentableFx.EaseOutBack);
            rt.localScale = Vector3.one;
        }

        // The marked row's bloom swells in and then breathes softly; the badge pops.
        private IEnumerator RowGlowIn(Row r, Color main, float delay)
        {
            var badgeRt = r.badge.rectTransform;
            r.bloom.color = new Color(main.r, main.g, main.b, 0f);
            badgeRt.localScale = Vector3.zero;
            if (delay > 0f)
                yield return PresentableFx.Wait(delay);
            yield return PresentableFx.Tween(0.45f, t =>
            {
                r.bloom.color = new Color(main.r, main.g, main.b, 0.30f * t);
                badgeRt.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, t);
            }, PresentableFx.EaseOutBack);
            badgeRt.localScale = Vector3.one;
            var t0 = Time.time;
            while (r.bloom.gameObject.activeInHierarchy)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin((Time.time - t0) * 2.4f);
                r.bloom.color = new Color(main.r, main.g, main.b, 0.18f + 0.16f * pulse);
                yield return null;
            }
        }

        /// <summary>Tint of the panel frame (the result colour fades in on the whole box).</summary>
        public void TintFrame(Color c)
        {
            if (_frame != null)
                _frame.color = c;
        }

        /// <summary>
        /// A question that was already answered in an earlier visit of the scenario (Previous / Next buttons): shown again with the first pick
        /// selected, the right and wrong rows marked and every reason visible. It is locked: no click changes it, nothing is recorded, no sound.
        /// </summary>
        public void ShowAnswered(Question question, int index)
        {
            question.inlineReveal = true;
            Show(question);
            _locked = true;
            if (index >= 0 && index < question.options.Length)
                StartCoroutine(ShowInline(index, index == question.correctIndex, true));
        }

        private IEnumerator ShowInline(int index, bool correct, bool silent = false)
        {
            if (!silent)
            {
                PresentableFx.Play(correct ? PresentableFx.Success : PresentableFx.Error);
                PresentableFx.Haptic(true, correct ? 0.6f : 0.9f, correct ? 0.15f : 0.25f);
            }
            _header.color = HeaderColour();
            if (_embedded)
            {
                if (_fx != null && !silent)
                    _fx.OnResult(correct, index, true, true);
                EmbeddedResult?.Invoke(correct);
            }
            else if (_fx != null && !silent)
            {
                _fx.OnResult(correct, index, true);
            }
            LayoutReveal();

            var rightIdx = Mathf.Clamp(_question.correctIndex, 0, _rows.Count - 1);
            for (var i = 0; i < _rows.Count && i < _question.options.Length; i++)
            {
                var r = _rows[i];
                var isRight = i == rightIdx;
                var isPick = i == index;
                if (Skin)
                {
                    // client rules: right answer = the picked row green + tick, the others red; wrong answer = the pick red + cross,
                    // the other wrong ones red, the right one green WITHOUT a tick
                    r.frame.sprite = isRight ? art.mcqBoxGreen : art.mcqBoxRed;
                    r.frame.color = Color.white;
                    r.check.gameObject.SetActive(isRight && isPick);
                    r.cross.gameObject.SetActive(isPick && !isRight);
                    if (isPick)
                        StartCoroutine(SkinMarkPop(isRight ? r.check.rectTransform : r.cross.rectTransform));
                    // client: every line starts with "Reason:" (Bangla "কারণঃ"), no RIGHT / WRONG tag
                    r.reasonTag.text = ChemicalSafety.Localization.Loc.T("GENERAL", "McqReason");
                    r.reasonTag.color = isRight ? new Color(0.55f, 1f, 0.68f) : new Color(1f, 0.62f, 0.58f);
                    r.reasonTag.gameObject.SetActive(true);
                    r.reason.text = _question.options[i].reason;
                    r.reason.color = _question.largeText ? Color.white : new Color(0.95f, 0.98f, 1f);
                    r.reason.gameObject.SetActive(true);
                    r.root.GetComponent<CanvasGroup>().alpha = 1f;
                    continue;
                }

                var good = isRight;
                var marked = isRight || isPick;
                var main = good ? EmeraldMain : RubyMain;
                var light = good ? EmeraldLight : RubyLight;
                r.verdict.gameObject.SetActive(marked);
                r.verdict.color = main;
                r.bloom.gameObject.SetActive(marked);
                r.wash.gameObject.SetActive(marked);
                r.wash.color = new Color(main.r, main.g, main.b, 0.18f);
                r.gloss.gameObject.SetActive(marked);
                r.check.gameObject.SetActive(isRight);
                r.check.color = Color.white;
                r.cross.gameObject.SetActive(isPick && !isRight);
                r.cross.color = Color.white;
                r.badge.gameObject.SetActive(marked);
                r.badge.color = new Color(main.r, main.g, main.b, 0.85f);
                if (marked)
                    StartCoroutine(RowGlowIn(r, main, isPick ? 0f : 0.12f));
                var tagColor = good ? "#6BE38A" : "#FF8A80";
                var tag = good ? _question.tagCorrect : _question.tagWrong;
                r.reason.text = "<b><color=" + tagColor + ">" + tag + "</color></b>  " + _question.options[i].reason;
                r.reason.color = _question.largeText ? Color.white : (good ? new Color(0.80f, 1f, 0.86f) : new Color(1f, 0.86f, 0.84f));
                r.reason.gameObject.SetActive(true);
                r.root.GetComponent<CanvasGroup>().alpha = isRight || isPick ? 1f : 0.85f;
            }

            if (!correct && !silent)
                StartCoroutine(Shake(_rows[index].root));
            yield return PresentableFx.Wait(silent ? 0.25f : 0.6f);

            // right answer: the drum goes onto the trolley first (the director holds NEXT until that has finished)
            while (!silent && _question.holdNext != null && _question.holdNext())
                yield return null;
            if (_question.hideNext)
            {
                if (!silent)
                    PopupShown?.Invoke(correct); // (it starts the answer voice: a repeated answer is silent)
                Revealed?.Invoke(correct);
                yield break;
            }

            _inlineNextLabel.text = _question.nextLabel;
            if (!silent)
                PopupShown?.Invoke(correct);
            yield return PopupIn(_inlineNext);
            // the button breathes so it reads as the thing to press
            while (_inlineNext.gameObject.activeInHierarchy)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
                _inlineNextGlow.color = new Color(0.30f, 0.80f, 1f, 0.22f + 0.30f * pulse);
                yield return null;
            }
        }

        private void OnWrongClosed()
        {
            PresentableFx.Play(PresentableFx.Tick);
            StartCoroutine(RetryRoutine());
        }

        private IEnumerator RetryRoutine()
        {
            yield return PopupOut(_wrongPopup);
            SetQuestionChrome(true);
            if (_question != null)
            {
                for (var i = 0; i < _rows.Count && i < _question.options.Length; i++)
                    _rows[i].label.text = _question.options[i].label;
            }

            ApplyNormalState();
            if (_fx != null)
            {
                _fx.OnRetry();
                for (var i = 0; i < _rows.Count && i < _question.options.Length; i++)
                    _rows[i].root.GetComponent<CanvasGroup>().alpha = 1f;
            }

            _locked = false;
            WrongClosed?.Invoke();
        }

        private void OnNextClicked()
        {
            PresentableFx.Play(PresentableFx.Tick);
            NextPressed?.Invoke();
        }

        // The question frame, title and rows hide while a result card is open, and come back only when the wrong card closes.
        private void SetQuestionChrome(bool visible)
        {
            if (_frame != null)
                _frame.gameObject.SetActive(visible && !_embedded);
            if (_readPlate != null)
                _readPlate.gameObject.SetActive(visible && !_embedded);
            if (_header != null)
                _header.gameObject.SetActive(visible);
            if (_prompt != null)
                _prompt.gameObject.SetActive(visible && !Skin);
            foreach (var d in _skinDecor)
                d.SetActive(visible);
            for (var i = 0; i < _rows.Count; i++)
            {
                var show = visible && _question != null && i < _question.options.Length;
                _rows[i].root.gameObject.SetActive(show);
            }
        }

        private void ApplyNormalState()
        {
            if (art.frameWide == null)
                SetPanelColors(PresentableUi.FlatBorder, PresentableUi.FlatFill);
            else if (_frame != null)
                _frame.color = Color.white;
            _header.color = HeaderColour();
            foreach (var r in _rows)
            {
                PaintRow(r, false, Color.white);
                r.check.gameObject.SetActive(false);
                r.root.GetComponent<CanvasGroup>().alpha = 1f;
            }
        }

        private void PaintRow(Row row, bool hot, Color tint)
        {
            if (Skin)
            {
                row.frame.sprite = art.mcqBoxBlue;
                row.frame.color = hot ? Color.white : SkinIdle;
                return;
            }

            if (art.optionBar != null)
                row.frame.sprite = hot && art.optionBarHot != null ? art.optionBarHot : art.optionBar;
            row.frame.color = tint;
        }

        private void SetPanelColors(Color border, Color fill)
        {
            if (_frame != null)
                _frame.color = border;
            if (_panelFill != null)
                _panelFill.color = fill;
        }

        private IEnumerator PopIn()
        {
            // A fingertip left over from the previous card (Continue) must not answer by accident:
            // the panel takes input only after the pop-in and once no fingertip is in front of it.
            _group.interactable = false;
            _group.blocksRaycasts = true;
            transform.localScale = Vector3.one * (0.8f * _worldScale);
            yield return PresentableFx.Tween(0.45f, t =>
            {
                _group.alpha = Mathf.Clamp01(t * 1.5f);
                transform.localScale = Vector3.one * (_worldScale * Mathf.LerpUnclamped(0.8f, 1f, t));
            }, PresentableFx.EaseOutBack);
            var guard = 0f;
            while (FingerInFront() && guard < 2.5f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            yield return PresentableFx.Wait(0.25f);
            if (_fx != null)
            {
                // Scenario 6: the options land one by one, input opens when the last one has landed
                yield return _fx.RowsIn();
                if (IsOpen && !_locked)
                    _group.interactable = true;
                yield break;
            }

            if (IsOpen && !_locked)
                _group.interactable = true;

            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].root.gameObject.activeSelf)
                    StartCoroutine(RowIn(_rows[i], i * 0.07f));
            }
        }

        private bool FingerInFront()
        {
            return TipInFront(GlovedHandPose.Right) || TipInFront(GlovedHandPose.Left);
        }

        private bool TipInFront(GlovedHandPose hand)
        {
            if (hand == null || !hand.HasTip)
                return false;
            var rt = GetComponentInChildren<RectTransform>();
            if (rt == null)
                return false;
            var local = rt.InverseTransformPoint(hand.PressPoint);
            var depth = -local.z * rt.lossyScale.z;
            if (depth < -0.05f || depth > 0.12f)
                return false;
            var r = rt.rect;
            return local.x >= r.xMin - 60f && local.x <= r.xMax + 60f && local.y >= r.yMin - 60f && local.y <= r.yMax + 60f;
        }

        private IEnumerator RowIn(Row row, float delay)
        {
            var g = row.root.GetComponent<CanvasGroup>();
            g.alpha = 0f;
            yield return PresentableFx.Wait(delay);
            var basePos = row.root.anchoredPosition;
            yield return PresentableFx.Tween(0.3f, t =>
            {
                g.alpha = t;
                row.root.anchoredPosition = basePos + new Vector2(Mathf.Lerp(-60f, 0f, t), 0f);
            }, PresentableFx.EaseOutCubic);
        }

        private IEnumerator PopOut()
        {
            _group.interactable = false;
            _group.blocksRaycasts = false;
            var from = _group.alpha;
            yield return PresentableFx.Tween(0.3f, t =>
            {
                _group.alpha = Mathf.Lerp(from, 0f, t);
                transform.localScale = Vector3.one * (_worldScale * Mathf.Lerp(1f, 0.9f, t));
            });
        }

        private static IEnumerator PopupIn(RectTransform popup)
        {
            popup.gameObject.SetActive(true);
            var g = popup.GetComponent<CanvasGroup>();
            yield return PresentableFx.Tween(0.35f, t =>
            {
                g.alpha = Mathf.Clamp01(t * 2f);
                popup.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, t);
            }, PresentableFx.EaseOutBack);
        }

        private static IEnumerator PopupOut(RectTransform popup)
        {
            var g = popup.GetComponent<CanvasGroup>();
            yield return PresentableFx.Tween(0.2f, t =>
            {
                g.alpha = 1f - t;
                popup.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, t);
            });
            popup.gameObject.SetActive(false);
        }

        private static IEnumerator Shake(RectTransform rt)
        {
            var basePos = rt.anchoredPosition;
            yield return PresentableFx.Tween(0.4f, t =>
            {
                var amp = (1f - t) * 18f;
                rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 50f) * amp, 0f);
            }, x => x);
            rt.anchoredPosition = basePos;
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            var canvas = PresentableUi.WorldCanvas("McqCanvas", transform, sizePx, metersPerPixel, true, 200);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            var root = canvas.transform;
            CanvasRect = (RectTransform)root;
            _canvasBasePos = CanvasRect.localPosition;
            var font = art.englishFont;

            var panel = root;
            if (Skin)
            {
                var skinRt = PresentableUi.Centered("Frame", root, Vector2.zero, sizePx * 1.1f);
                _frameRt = skinRt;
                _frame = PresentableUi.Img(skinRt, art.mcqFrame, Color.white);
            }
            else if (art.frameWide != null)
            {
                var frameRt = PresentableUi.Centered("Frame", root, Vector2.zero, sizePx * 1.08f);
                _frame = PresentableUi.Img(frameRt, art.frameWide, Color.white);
            }
            else
            {
                var host = PresentableUi.Rect("Panel", root, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
                var box = PresentableUi.FlatBox(host, art.roundedSolid, PresentableUi.FlatBorder, PresentableUi.FlatFill);
                _frame = box.border;
                _panelFill = box.fill;
                panel = host;
            }

            // Dark plate behind the words so they stay readable on the translucent blue glass.
            _readPlate = PresentableUi.Rect("ReadPlate", panel, new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.83f));
            PresentableUi.Img(_readPlate, art.roundedSolid, Skin ? new Color(0.03f, 0.10f, 0.22f, SkinReadPlateAlpha) : new Color(0.02f, 0.05f, 0.09f, 0.82f), sliced: true);
            _header = HeaderTitle(PresentableUi.Text("Header", panel, new Vector2(0.08f, 0.845f), new Vector2(0.92f, 0.93f),
                string.Empty, 24f, Color.white, bold: true, font: font));
            _header.fontSizeMin = 18f;
            _header.fontSizeMax = 28f;
            if (Skin)
            {
                // the question is the title line (sample: Q badge, question, ? mark)
                _header.rectTransform.anchorMin = new Vector2(0.17f, 0.785f);
                _header.rectTransform.anchorMax = new Vector2(0.89f, 0.955f);
                _header.textWrappingMode = TextWrappingModes.Normal;
                _header.overflowMode = TextOverflowModes.Truncate;
                _header.fontSizeMin = 14f;
                _header.fontSizeMax = 30f;
                _header.margin = new Vector4(4f, 0f, 4f, 0f);
                _header.color = SkinTitle;
                var q = PresentableUi.Text("QBadge", panel, new Vector2(0.075f, 0.84f), new Vector2(0.16f, 0.955f), "Q", 72f, SkinTitle,
                    TextAlignmentOptions.Center, true, font);
                q.enableAutoSizing = false;
                var qm = PresentableUi.Text("QMark", panel, new Vector2(0.9f, 0.87f), new Vector2(0.95f, 0.94f), "?", 40f,
                    new Color(SkinTitle.r, SkinTitle.g, SkinTitle.b, 0.5f), TextAlignmentOptions.Center, true, font);
                qm.enableAutoSizing = false;
                _skinDecor.Add(q.gameObject);
                _skinDecor.Add(qm.gameObject);
                _qBadgeRt = q.rectTransform;
                _qMarkRt = qm.rectTransform;
            }
            _prompt = FitInside(PresentableUi.Text("Prompt", panel, new Vector2(0.08f, 0.675f), new Vector2(0.92f, 0.825f),
                string.Empty, 28f, new Color(0.94f, 0.97f, 1f), font: font), 20f, 32f);

            for (var i = 0; i < 4; i++)
                _rows.Add(BuildRow(panel, i, font));

            BuildCorrectPopup(root, font);
            BuildWrongPopup(root, font);
            BuildNextButton(root, font);
            BuildInlineNext(panel, font);
            PresentableUi.AlwaysOnTop(canvas);
        }

        private Row BuildRow(Transform root, int index, TMP_FontAsset font)
        {
            const float rowH = 0.128f;
            const float gap = 0.012f;
            var top = 0.655f - index * (rowH + gap);
            var row = new Row
            {
                root = PresentableUi.Rect("Row" + (index + 1), root, new Vector2(0.05f, top - rowH),
                    new Vector2(0.95f, top))
            };
            row.root.gameObject.AddComponent<CanvasGroup>();
            row.root.localPosition += new Vector3(0f, 0f, -12f);

            var bar = Skin ? art.mcqBoxBlue : art.optionBar != null ? art.optionBar : art.roundedSolid;
            row.frame = PresentableUi.Img(PresentableUi.Rect("Frame", row.root, Vector2.zero, Vector2.one),
                bar, Skin ? SkinIdle : Color.white, sliced: Skin);
            row.fill = PresentableUi.Img(PresentableUi.Rect("Fill", row.root, Vector2.zero, Vector2.one),
                null, new Color(1f, 1f, 1f, 0.02f), raycast: true);

            row.button = row.root.gameObject.AddComponent<Button>();
            row.button.transition = Selectable.Transition.None;
            row.button.targetGraphic = row.fill;
            var captured = index;
            row.button.onClick.AddListener(() => OnOptionClicked(captured));
            var lift = row.root.gameObject.AddComponent<HoverLift>();
            lift.Init(row.root);
            lift.HoverChanged += on =>
            {
                if (_locked)
                    return;
                if (Skin)
                {
                    row.frame.color = on ? Color.white : SkinIdle;
                    return;
                }

                if (art.optionBar == null)
                    return;
                row.frame.sprite = on && art.optionBarHot != null ? art.optionBarHot : art.optionBar;
                row.frame.color = Color.white;
            };

            var labelRt = PresentableUi.Rect("Label", row.root, new Vector2(0.06f, 0.08f), new Vector2(0.7f, 0.92f));
            row.label = PresentableUi.Text(labelRt, string.Empty, 28f, Color.white, TextAlignmentOptions.MidlineLeft,
                true, font);
            FitInside(row.label, 18f, 28f);

            row.icons = new Image[3];
            row.overlays = new Image[3];
            for (var k = 0; k < 3; k++)
            {
                var iconRt = PresentableUi.Rect("Icon" + k, row.root, new Vector2(0.78f, 0.2f), new Vector2(0.95f, 0.8f));
                row.icons[k] = PresentableUi.Img(iconRt, null, Color.white, preserveAspect: true);
                var stickerRt = PresentableUi.Rect("Sticker", iconRt, new Vector2(0.19f, 0.21f), new Vector2(0.81f, 0.70f));
                row.overlays[k] = PresentableUi.Img(stickerRt, null, Color.white, preserveAspect: true);
            }

            // round glow behind the tick / cross, then the mark itself (inlineReveal): bigger than before
            var badgeRt = PresentableUi.Rect("Badge", row.root, new Vector2(-0.004f, 0.02f), new Vector2(0.098f, 0.98f));
            row.badge = PresentableUi.Img(badgeRt, art.radialGlow, EmeraldMain, preserveAspect: true);
            row.badge.gameObject.SetActive(false);
            var checkRt = Skin ? PresentableUi.Rect("Check", row.root, new Vector2(0.022f, 0.09f), new Vector2(0.108f, 0.91f))
                : PresentableUi.Rect("Check", row.root, new Vector2(0.012f, 0.20f), new Vector2(0.082f, 0.80f));
            row.check = PresentableUi.Img(checkRt, Skin && art.mcqTick != null ? art.mcqTick : art.check, Skin && art.mcqTick != null ? Color.white : PresentableUi.Green, preserveAspect: true);
            row.check.gameObject.SetActive(false);

            var crossRt = Skin ? PresentableUi.Rect("Cross", row.root, new Vector2(0.022f, 0.09f), new Vector2(0.108f, 0.91f))
                : PresentableUi.Rect("Cross", row.root, new Vector2(0.012f, 0.20f), new Vector2(0.082f, 0.80f));
            row.cross = PresentableUi.Img(crossRt, Skin && art.mcqCross != null ? art.mcqCross : art.cross, Skin && art.mcqCross != null ? Color.white : RubyLight, preserveAspect: true);
            row.cross.gameObject.SetActive(false);

            // colour wash and top gloss over the bar (below the words), inlineReveal only
            var washRt = PresentableUi.Rect("Wash", row.root, new Vector2(0.004f, 0.06f), new Vector2(0.996f, 0.94f));
            row.wash = PresentableUi.Img(washRt, art.roundedSolid, new Color(EmeraldMain.r, EmeraldMain.g, EmeraldMain.b, 0.16f), sliced: true);
            washRt.SetSiblingIndex(row.fill.transform.GetSiblingIndex() + 1);
            row.wash.gameObject.SetActive(false);
            var glossRt = PresentableUi.Rect("Gloss", row.root, new Vector2(0.02f, 0.52f), new Vector2(0.98f, 0.93f));
            row.gloss = PresentableUi.Img(glossRt, art.roundedSolid, new Color(1f, 1f, 1f, 0.13f), sliced: true);
            glossRt.SetSiblingIndex(row.wash.transform.GetSiblingIndex() + 1);
            row.gloss.gameObject.SetActive(false);

            // coloured outline and soft bloom behind the frame (inlineReveal): emerald on the right option, ruby on a wrong pick
            var bloomRt = PresentableUi.Rect("Bloom", row.root, Vector2.zero, Vector2.one, new Vector2(-20f, -20f), new Vector2(20f, 20f));
            row.bloom = PresentableUi.Img(bloomRt, art.roundedSolid, new Color(EmeraldMain.r, EmeraldMain.g, EmeraldMain.b, 0.16f), sliced: true);
            bloomRt.SetSiblingIndex(0);
            row.bloom.gameObject.SetActive(false);
            var verdictRt = PresentableUi.Rect("Verdict", row.root, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));
            row.verdict = PresentableUi.Img(verdictRt, art.roundedSolid, EmeraldMain, sliced: true);
            verdictRt.SetSiblingIndex(1);
            row.verdict.gameObject.SetActive(false);

            // one-line reason under the row; sits in the panel, not in the row, so the row's hover lift does not move it
            var reasonRt = PresentableUi.Rect("Reason" + (index + 1), root, new Vector2(0.09f, 0.1f), new Vector2(0.94f, 0.15f));
            reasonRt.localPosition += new Vector3(0f, 0f, -12f);
            row.reason = PresentableUi.Text(reasonRt, string.Empty, 22f, Color.white, TextAlignmentOptions.MidlineLeft, false, font);
            row.reason.richText = true;
            row.reason.fontStyle = FontStyles.Italic;
            row.reason.textWrappingMode = TextWrappingModes.NoWrap;
            row.reason.overflowMode = TextOverflowModes.Ellipsis;
            row.reason.enableAutoSizing = true;
            row.reason.fontSizeMin = 14f;
            row.reason.fontSizeMax = 22f;
            row.reason.raycastTarget = false;
            row.reason.gameObject.SetActive(false);
            if (Skin)
            {
                var tagRt = PresentableUi.Rect("ReasonTag" + (index + 1), root, new Vector2(0.09f, 0.1f), new Vector2(0.17f, 0.15f));
                tagRt.localPosition += new Vector3(0f, 0f, -12f);
                row.reasonTag = PresentableUi.Text(tagRt, string.Empty, 22f, SkinTitle, TextAlignmentOptions.MidlineLeft, true, font);
                row.reasonTag.fontStyle = FontStyles.Bold | FontStyles.Italic;
                row.reasonTag.textWrappingMode = TextWrappingModes.NoWrap;
                row.reasonTag.enableAutoSizing = true;
                row.reasonTag.fontSizeMin = 14f;
                row.reasonTag.fontSizeMax = 22f;
                row.reasonTag.raycastTarget = false;
                row.reasonTag.gameObject.SetActive(false);
            }

            return row;
        }

        private static TextMeshProUGUI HeaderTitle(TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = 28f;
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        private static TextMeshProUGUI FitInside(TextMeshProUGUI t, float min, float max)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            t.margin = new Vector4(6f, 2f, 6f, 2f);
            return t;
        }

        private void BuildCorrectPopup(Transform root, TMP_FontAsset font)
        {
            // The supplied green frame already has the blue metal border. Words stay in the inner glass.
            _correctPopup = PresentableUi.Centered("CorrectPopup", root, new Vector2(0f, 40f), new Vector2(900f, 482f));
            _correctPopup.localPosition += new Vector3(0f, 0f, -45f);
            _correctPopup.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(_correctPopup, art.frameCorrect != null ? art.frameCorrect : art.frameWide, Color.white);
            _correctTitle = HeaderTitle(PresentableUi.Text("Title", _correctPopup, new Vector2(0.18f, 0.855f),
                new Vector2(0.82f, 0.93f), string.Empty, 26f, Color.white, bold: true, font: font));
            PresentableUi.Img(PresentableUi.Rect("Tick", _correctPopup, new Vector2(0.42f, 0.62f), new Vector2(0.58f, 0.76f)),
                art.check, new Color(0.55f, 1f, 0.62f), preserveAspect: true);
            _correctBody = FitInside(PresentableUi.Text("Body", _correctPopup, new Vector2(0.16f, 0.22f),
                new Vector2(0.84f, 0.58f), string.Empty, 26f, new Color(0.86f, 1f, 0.9f), font: font), 16f, 28f);
            _correctPopup.gameObject.SetActive(false);
        }

        private void BuildWrongPopup(Transform root, TMP_FontAsset font)
        {
            _wrongPopup = PresentableUi.Centered("WrongPopup", root, new Vector2(0f, 40f), new Vector2(900f, 482f));
            _wrongPopup.localPosition += new Vector3(0f, 0f, -45f);
            _wrongPopup.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(_wrongPopup, art.frameWrong != null ? art.frameWrong : art.frameWide, Color.white);
            _wrongTitle = HeaderTitle(PresentableUi.Text("Title", _wrongPopup, new Vector2(0.18f, 0.855f),
                new Vector2(0.82f, 0.93f), string.Empty, 24f, Color.white, bold: true, font: font));
            _wrongBody = FitInside(PresentableUi.Text("Body", _wrongPopup, new Vector2(0.16f, 0.36f),
                new Vector2(0.84f, 0.74f), string.Empty, 22f, new Color(1f, 0.94f, 0.94f), font: font), 15f, 24f);

            var close = PresentableUi.Rect("Close", _wrongPopup, new Vector2(0.74f, 0.20f), new Vector2(0.86f, 0.32f));
            var closeBg = PresentableUi.Img(close, art.roundedSolid, new Color(0.75f, 0.14f, 0.12f, 1f), sliced: true,
                raycast: true);
            PresentableUi.Img(PresentableUi.Rect("X", close, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f)),
                art.cross, Color.white, preserveAspect: true);
            var btn = close.gameObject.AddComponent<Button>();
            btn.targetGraphic = closeBg;
            btn.onClick.AddListener(OnWrongClosed);
            close.gameObject.AddComponent<HoverLift>().Init(close);
            _wrongPopup.gameObject.SetActive(false);
        }

        private void BuildNextButton(Transform root, TMP_FontAsset font)
        {
            _nextButton = PresentableUi.Rect("NextButton", _correctPopup, new Vector2(0.18f, -0.34f), new Vector2(0.82f, -0.08f));
            _nextButton.localPosition += new Vector3(0f, 0f, -30f);
            _nextButton.gameObject.AddComponent<CanvasGroup>();
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", _nextButton, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Pill", _nextButton, Vector2.zero, Vector2.one,
                new Vector2(-80f, -52f), new Vector2(80f, 52f)), art.button, Color.white);
            _nextLabel = PresentableUi.Text("Label", _nextButton, Vector2.zero, Vector2.one, "NEXT", 42f, Color.white,
                bold: true, font: font);
            var btn = _nextButton.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(OnNextClicked);
            _nextButton.gameObject.AddComponent<HoverLift>().Init(_nextButton);
            _nextButton.gameObject.SetActive(false);
        }

        // inlineReveal: the same NEXT pill, on the panel below the last option (there is no result popup to hang it on)
        private void BuildInlineNext(Transform panel, TMP_FontAsset font)
        {
            _inlineNext = PresentableUi.Rect("NextButton", panel, new Vector2(0.30f, 0.03f), new Vector2(0.70f, 0.165f));
            _inlineNext.localPosition += new Vector3(0f, 0f, -30f);
            _inlineNext.gameObject.AddComponent<CanvasGroup>();
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", _inlineNext, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);

            // A real button, not a text pill: soft shadow, pulsing glow, light border, blue body with a top gloss and an arrow.
            var half = new Vector2(190f, 50f);
            var mid = new Vector2(0.5f, 0.5f);
            _inlineNextGlow = PresentableUi.Img(PresentableUi.Rect("Glow", _inlineNext, mid, mid, -half * 1.35f, half * 1.35f),
                art.radialGlow, new Color(0.30f, 0.80f, 1f, 0.35f));
            PresentableUi.Img(PresentableUi.Rect("Shadow", _inlineNext, mid, mid, new Vector2(-half.x - 6f, -half.y - 14f),
                new Vector2(half.x + 6f, half.y - 6f)), art.roundedSolid, new Color(0f, 0.02f, 0.06f, 0.45f), sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Border", _inlineNext, mid, mid, -half, half), art.roundedSolid,
                new Color(0.62f, 0.92f, 1f, 1f), sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Body", _inlineNext, mid, mid, -half + new Vector2(4f, 4f), half - new Vector2(4f, 4f)),
                art.roundedSolid, new Color(0.06f, 0.44f, 0.90f, 1f), sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Shade", _inlineNext, mid, mid, new Vector2(-half.x + 4f, -half.y + 4f),
                new Vector2(half.x - 4f, -4f)), art.roundedSolid, new Color(0.02f, 0.16f, 0.50f, 0.55f), sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Gloss", _inlineNext, mid, mid, new Vector2(-half.x + 10f, 4f),
                new Vector2(half.x - 10f, half.y - 7f)), art.roundedSolid, new Color(1f, 1f, 1f, 0.20f), sliced: true);
            _inlineNextLabel = PresentableUi.Text("Label", _inlineNext, mid, mid, "NEXT", 42f, Color.white, bold: true, font: font);
            _inlineNextLabel.rectTransform.offsetMin = new Vector2(-half.x + 30f, -half.y);
            _inlineNextLabel.rectTransform.offsetMax = new Vector2(half.x - 90f, half.y);
            _inlineNextLabel.characterSpacing = 6f;
            PresentableUi.Img(PresentableUi.Rect("Arrow", _inlineNext, mid, mid, new Vector2(half.x - 84f, -26f),
                new Vector2(half.x - 32f, 26f)), art.iconNext != null ? art.iconNext : art.cross, Color.white, preserveAspect: true);
            var btn = _inlineNext.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(OnNextClicked);
            _inlineNext.gameObject.AddComponent<HoverLift>().Init(_inlineNext);
            _inlineNext.gameObject.SetActive(false);
        }
    }

    /// <summary>Hover feedback: lifts the element toward the user + tiny haptic tick.</summary>
    public sealed class HoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _rt;
        private Vector3 _baseScale = Vector3.one;
        private float _target = 1f;
        private float _current = 1f;
        private float _press;

        /// <summary>Raised when the hover starts / ends (pointer or finger), for rows that also change colour.</summary>
        public event Action<bool> HoverChanged;

        public void Init(RectTransform rt)
        {
            _rt = rt;
            _baseScale = rt.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _target = 1.04f;
            HoverChanged?.Invoke(true);
            PresentableFx.Haptic(true, 0.12f, 0.03f);
            PresentableFx.Play(PresentableFx.Tick, 0.35f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _target = 1f;
            HoverChanged?.Invoke(false);
        }

        /// <summary>Hover from the finger poke system (no pointer events involved).</summary>
        public void SetHover(bool on)
        {
            _target = on ? 1.04f : 1f;
            HoverChanged?.Invoke(on);
        }

        /// <summary>Short dip when the finger presses the button.</summary>
        public void Press() => _press = 1f;

        private void Update()
        {
            if (_rt == null)
                return;
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            _press = Mathf.MoveTowards(_press, 0f, Time.deltaTime * 8f);
            _rt.localScale = _baseScale * (_current * (1f - 0.06f * _press));
        }
    }
}
