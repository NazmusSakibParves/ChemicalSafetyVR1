using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 12 "PPE SELECTION ASSESSMENT" (workflow step08/09): a multiple-answer question. Every row has a check box that a finger
    /// poke toggles, SUBMIT checks the whole selection. The result comes as the same popup the other scenarios use after an MCQ answer:
    /// the question hides, a centred green RIGHT CHOICE popup (tick, text, NEXT below it, pop-in, chime, glow) or a red WRONG CHOICE
    /// popup with a close button (the selection stays and can be edited and submitted again).
    /// </summary>
    public sealed class Sc12Assessment : MonoBehaviour
    {
        [Serializable]
        public sealed class Question
        {
            public string header;
            public string prompt;
            public string[] labels;
            public bool[] correct;
            public string submitLabel;
            public string nextLabel;
            public string rightTitle;
            public string rightBody;
            public string wrongTitle;
            public string wrongItemBody;
            public string missedBody;
            /// <summary>Ray version: a reason under every row, shown after SUBMIT (REQUIRED / NOT REQUIRED), no popup, NEXT always.</summary>
            public bool inlineReveal;
            public string[] reasons;
            public string tagRequired;
            public string tagNotRequired;
        }

        [SerializeField] private PresentableArt art;

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color RowIdle = new Color(0.8f, 0.95f, 1f, 0.9f);

        // client MCQ design (Scenarios 1-12): transparent glass frame + light-blue boxes that turn green / red. Same logic as before.
        private bool Skin => art != null && art.mcqFrame != null && art.mcqBoxBlue != null && art.mcqBoxGreen != null && art.mcqBoxRed != null;
        private static readonly Color SkinIdle = new Color(0.88f, 0.95f, 1f, 1f);
        private Color IdleColor => Skin ? SkinIdle : RowIdle;
        private static readonly Color RowOn = new Color(0.55f, 0.92f, 1f, 1f);
        private const float PopupScale = 0.0006f;

        private Transform _panel;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI _prompt;
        private TextMeshProUGUI[] _labels;
        private Image[] _rows;
        private Image[] _checks;
        private Button _submit;
        private TextMeshProUGUI _submitText;

        // the result popup (same layout as the MCQ right / wrong popups)
        private Transform _popupRoot;
        private RectTransform _correctPopup;
        private RectTransform _wrongPopup;
        private RectTransform _nextButton;
        private RectTransform _tick;
        private Image _glow;
        private TextMeshProUGUI _correctTitle;
        private TextMeshProUGUI _correctBody;
        private TextMeshProUGUI _wrongTitle;
        private TextMeshProUGUI _wrongBody;
        private TextMeshProUGUI _nextLabel;

        private Question _q;
        private bool[] _on;
        private bool _locked;
        private bool _won;

        // inline (ray) look: a taller panel, a reason line per row, an own NEXT pill under the rows
        private RectTransform _panelRt;
        private RectTransform _headerRt, _promptRt, _submitRt;
        private RectTransform[] _rowRt, _labelRt, _reasonRt;
        private TextMeshProUGUI[] _reasons;
        private bool _revealed;
        private RectTransform _inlineNextRt;
        private TextMeshProUGUI _inlineNextText;
        private bool _inlineMode;
        private float _scaleMul = 1f;

        /// <summary>World size of the panel as a multiple of the default (the ray version shows it bigger).</summary>
        public void SetWorldScale(float mul)
        {
            _scaleMul = Mathf.Max(0.1f, mul);
            if (_panel != null && _panel.gameObject.activeSelf)
                _panel.localScale = Vector3.one * 0.00064f * _scaleMul;
        }

        /// <summary>Raised on SUBMIT: (selection by shown row, all right).</summary>
        public event Action<bool[], bool> Submitted;

        public event Action NextPressed;

        public bool Visible => _panel != null && _panel.gameObject.activeSelf;
        public bool Won => _won;
        public bool Revealed => _revealed;
        public bool PopupVisible => _popupRoot != null && _popupRoot.gameObject.activeSelf;
        public int RowCount => _labels != null ? _labels.Length : 0;

        private void Start()
        {
            Build();
        }

        // ------------------------------------------------------------------ driver API (also used by the smoke test)

        public void Show(Question q)
        {
            if (_panel == null)
                Build();
            StopAllCoroutines();
            _q = q;
            _on = new bool[RowCount];
            _won = false;
            _locked = false;
            _header.text = q.header;
            _prompt.text = q.prompt;
            _prompt.color = HoloWhite;
            _revealed = false;
            _inlineMode = q.inlineReveal;
            ApplyLayout(_inlineMode);
            for (var i = 0; i < RowCount; i++)
            {
                _labels[i].text = q.labels[i];
                _checks[i].gameObject.SetActive(false);
                _checks[i].sprite = art.check;
                _checks[i].color = Color.white;
                if (Skin)
                    _rows[i].sprite = art.mcqBoxBlue;
                _rows[i].color = IdleColor;
                _reasons[i].gameObject.SetActive(false);
            }

            _submitText.text = q.submitLabel;
            _inlineNextText.text = q.nextLabel;
            _nextLabel.text = q.nextLabel;
            _popupRoot.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
            StartCoroutine(PopIn());
        }

        // The standard panel is 820 x 780 with four plain rows. The inline (ray) look is taller: every row has its label and a reason
        // line under it, and the NEXT pill sits under the rows after SUBMIT.
        private void ApplyLayout(bool inline)
        {
            _panelRt.sizeDelta = inline ? new Vector2(900f, 1080f) : new Vector2(820f, 780f);
            void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
            {
                rt.anchorMin = new Vector2(x0, y0);
                rt.anchorMax = new Vector2(x1, y1);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (inline)
            {
                Anchor(_headerRt, 0.06f, 0.875f, 0.94f, 0.945f);
                Anchor(_promptRt, 0.07f, 0.775f, 0.93f, 0.87f);
                for (var i = 0; i < RowCount; i++)
                {
                    var top = 0.765f - i * 0.155f;
                    Anchor(_rowRt[i], 0.06f, top - 0.143f, 0.94f, top);
                    Anchor(_labelRt[i], 0.15f, 0.52f, 0.97f, 0.97f);
                    Anchor(_reasonRt[i], 0.15f, 0.04f, 0.97f, 0.52f);
                }

                Anchor(_submitRt, 0.30f, 0.07f, 0.70f, 0.150f);
            }
            else
            {
                Anchor(_headerRt, 0.06f, 0.85f, 0.94f, 0.935f);
                Anchor(_promptRt, 0.07f, 0.745f, 0.93f, 0.85f);
                for (var i = 0; i < RowCount; i++)
                {
                    var top = 0.73f - i * 0.135f;
                    Anchor(_rowRt[i], 0.07f, top - 0.118f, 0.93f, top);
                    Anchor(_labelRt[i], 0.15f, 0.04f, 0.97f, 0.96f);
                }

                Anchor(_submitRt, 0.30f, 0.055f, 0.70f, 0.18f);
            }

            _submit.gameObject.SetActive(true);
            _inlineNextRt.gameObject.SetActive(false);
        }

        /// <summary>
        /// Answered in an earlier visit (Previous / Next buttons): the ticks of the first submit are shown again, every row with its verdict and
        /// reason. Locked, no sound, nothing is recorded. Only the ray (inline) look is used.
        /// </summary>
        public void ShowAnswered(Question q, bool[] ticked)
        {
            q.inlineReveal = true;
            Show(q);
            var allRight = true;
            var wrongTicked = false;
            for (var i = 0; i < RowCount; i++)
            {
                _on[i] = ticked != null && i < ticked.Length && ticked[i];
                if (_on[i] != q.correct[i])
                    allRight = false;
                if (_on[i] && !q.correct[i])
                    wrongTicked = true;
            }

            _locked = true;
            _won = allRight;
            _silentReveal = true;
            Reveal(allRight, wrongTicked);
            _silentReveal = false;
        }

        private bool _silentReveal;

        // ray version: no popup. Every row shows its verdict tag and reason, the ticked rows get a green tick / red cross, then NEXT.
        private void Reveal(bool allRight, bool wrongTicked)
        {
            _revealed = true;
            for (var i = 0; i < RowCount; i++)
            {
                var good = _q.correct[i];
                if (Skin)
                {
                    _rows[i].sprite = good ? art.mcqBoxGreen : art.mcqBoxRed;
                    _rows[i].color = Color.white;
                }
                else
                    _rows[i].color = good ? new Color(0.62f, 1f, 0.78f, 1f) : new Color(1f, 0.72f, 0.70f, 1f);
                var tag = good ? _q.tagRequired : _q.tagNotRequired;
                var reason = _q.reasons != null && i < _q.reasons.Length ? _q.reasons[i] : string.Empty;
                var hex = good ? "#7CFFA8" : "#FF8A80";
                _reasons[i].text = "<b><color=" + hex + ">" + tag + "</color></b>  " + reason;
                _reasons[i].gameObject.SetActive(true);
                _checks[i].gameObject.SetActive(_on[i] || good);
                if (Skin && art.mcqTick != null && art.mcqCross != null)
                {
                    _checks[i].sprite = good ? art.mcqTick : art.mcqCross;
                    _checks[i].color = new Color(1f, 1f, 1f, good && !_on[i] ? 0.5f : 1f);
                }
                else
                {
                    _checks[i].sprite = good ? art.check : art.cross;
                    _checks[i].color = good ? new Color(0.30f, 1f, 0.52f, _on[i] ? 1f : 0.45f) : new Color(1f, 0.28f, 0.26f, 1f);
                }
            }

            _prompt.text = allRight ? _q.rightTitle : _q.wrongTitle;
            _prompt.color = allRight ? new Color(0.45f, 1f, 0.62f) : new Color(1f, 0.55f, 0.50f);
            _submit.gameObject.SetActive(false);
            _inlineNextRt.gameObject.SetActive(true);
            if (_silentReveal)
                return;
            PresentableFx.Play(allRight ? PresentableFx.Success : PresentableFx.Error, 0.6f);
            PresentableFx.Haptic(true, allRight ? 0.5f : 0.8f, 0.15f);
        }

        public void Hide()
        {
            StopAllCoroutines();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (_popupRoot != null)
                _popupRoot.gameObject.SetActive(false);
        }

        public void ToggleRow(int row)
        {
            if (_locked || row < 0 || row >= RowCount || !Visible)
                return;
            _on[row] = !_on[row];
            _checks[row].gameObject.SetActive(_on[row]);
            _rows[row].color = _on[row] ? (Skin ? Color.white : RowOn) : IdleColor;
            PresentableFx.Play(PresentableFx.Tick, 0.7f);
        }

        public void PressSubmit()
        {
            if (_locked || !Visible)
                return;
            var any = false;
            var wrongTicked = false;
            var allRight = true;
            for (var i = 0; i < RowCount; i++)
            {
                any |= _on[i];
                if (_on[i] != _q.correct[i])
                    allRight = false;
                if (_on[i] && !_q.correct[i])
                    wrongTicked = true;
            }

            if (!any)
                return;
            _locked = true;
            _won = allRight;
            if (_inlineMode)
            {
                Reveal(allRight, wrongTicked);
            }
            else if (allRight)
            {
                _correctTitle.text = _q.rightTitle;
                _correctBody.text = _q.rightBody;
                StartCoroutine(ShowCorrect());
            }
            else
            {
                _wrongTitle.text = _q.wrongTitle;
                _wrongBody.text = wrongTicked ? _q.wrongItemBody : _q.missedBody;
                StartCoroutine(ShowWrong());
            }

            Submitted?.Invoke((bool[])_on.Clone(), allRight);
        }

        public void PressNext()
        {
            if (_won || (_inlineMode && _revealed))
            {
                PresentableFx.Play(PresentableFx.Tick);
                NextPressed?.Invoke();
            }
        }

        private void OnWrongClosed()
        {
            PresentableFx.Play(PresentableFx.Tick);
            StartCoroutine(RetryRoutine());
        }

        // ------------------------------------------------------------------ result popups

        private IEnumerator ShowCorrect()
        {
            _panel.gameObject.SetActive(false);
            _popupRoot.gameObject.SetActive(true);
            _wrongPopup.gameObject.SetActive(false);
            _nextButton.gameObject.SetActive(false);
            PresentableFx.Play(PresentableFx.Success);
            PresentableFx.Haptic(true, 0.6f, 0.15f);
            StartCoroutine(GlowBurst());
            yield return PopupIn(_correctPopup);
            StartCoroutine(TickPulse());
            yield return PopupIn(_nextButton);
        }

        private IEnumerator ShowWrong()
        {
            PresentableFx.Play(PresentableFx.Error);
            PresentableFx.Haptic(true, 0.9f, 0.25f);
            _panel.gameObject.SetActive(false);
            _popupRoot.gameObject.SetActive(true);
            _correctPopup.gameObject.SetActive(false);
            _nextButton.gameObject.SetActive(false);
            yield return PopupIn(_wrongPopup);
        }

        private IEnumerator RetryRoutine()
        {
            yield return PopupOut(_wrongPopup);
            _popupRoot.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
            _panel.localScale = Vector3.one * 0.00064f * _scaleMul;
            _locked = false;
        }

        private IEnumerator TickPulse()
        {
            yield return PresentableFx.Tween(0.45f, t => _tick.localScale = Vector3.one * Mathf.LerpUnclamped(1.5f, 1f, t),
                PresentableFx.EaseOutBack);
            _tick.localScale = Vector3.one;
        }

        // a soft green ring of light that opens behind the popup
        private IEnumerator GlowBurst()
        {
            _glow.gameObject.SetActive(true);
            yield return PresentableFx.Tween(0.9f, t =>
            {
                _glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.5f, t);
                PresentableUi.SetAlpha(_glow, 0.55f * (1f - t));
            }, PresentableFx.EaseOutCubic);
            _glow.gameObject.SetActive(false);
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
            g.alpha = 1f;
            popup.localScale = Vector3.one;
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

        // ------------------------------------------------------------------ build

        private IEnumerator PopIn()
        {
            var baseScale = Vector3.one * 0.00064f * _scaleMul;
            var canvas = _panel.GetComponent<CanvasGroup>();
            _panel.localScale = baseScale * 0.85f;
            if (canvas != null)
                canvas.alpha = 0f;
            for (var t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                var k = PresentableFx.EaseOutBack(t / 0.3f);
                _panel.localScale = baseScale * Mathf.Lerp(0.85f, 1f, k);
                if (canvas != null)
                    canvas.alpha = Mathf.Clamp01(t / 0.2f);
                yield return null;
            }

            _panel.localScale = baseScale;
            if (canvas != null)
                canvas.alpha = 1f;
        }

        private Button HoloButton(string name, Transform parent, Vector2 min, Vector2 max, Sprite bg, out TextMeshProUGUI text)
        {
            var rt = PresentableUi.Rect(name, parent, min, max);
            var img = PresentableUi.Img(rt, bg, Color.white, true, true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            var lift = rt.gameObject.AddComponent<HoverLift>();
            lift.Init(rt);
            btn.onClick.AddListener(lift.Press);
            text = PresentableUi.Text("Label", rt, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), "", 36f, HoloWhite,
                TextAlignmentOptions.Center, true, art.englishFont);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 36f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return btn;
        }

        private static TextMeshProUGUI HeaderTitle(TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = 30f;
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

        private void Build()
        {
            if (_panel != null || art == null)
                return;
            BuildQuestion();
            BuildPopups();
        }

        private void BuildQuestion()
        {
            const int rows = 4;
            var plate = Skin ? art.mcqFrame : art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("Sc12AssessPanel", transform, new Vector2(820f, 780f), 0.00064f, true, 30);
            _panel = canvas.transform;
            canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, plate, Color.white);
            if (art.holoPanel != null && !Skin)
                bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            _header = PresentableUi.Text("Header", canvas.transform, new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.935f), "", 40f,
                new Color(0.7f, 0.93f, 1f), TextAlignmentOptions.Center, true, art.englishFont);
            _header.enableAutoSizing = true;
            _header.fontSizeMin = 20f;
            _header.fontSizeMax = 40f;
            _header.textWrappingMode = TextWrappingModes.NoWrap;
            _prompt = PresentableUi.Text("Prompt", canvas.transform, new Vector2(0.07f, 0.745f), new Vector2(0.93f, 0.85f), "", 34f,
                HoloWhite, TextAlignmentOptions.Center, false, art.englishFont);
            _prompt.enableAutoSizing = true;
            _prompt.fontSizeMin = 18f;
            _prompt.fontSizeMax = 34f;

            _panelRt = canvas.GetComponent<RectTransform>();
            _headerRt = _header.rectTransform;
            _promptRt = _prompt.rectTransform;
            _labels = new TextMeshProUGUI[rows];
            _rows = new Image[rows];
            _checks = new Image[rows];
            _rowRt = new RectTransform[rows];
            _labelRt = new RectTransform[rows];
            _reasonRt = new RectTransform[rows];
            _reasons = new TextMeshProUGUI[rows];
            for (var i = 0; i < rows; i++)
            {
                var top = 0.73f - i * 0.135f;
                var row = PresentableUi.Rect("Row" + i, canvas.transform, new Vector2(0.07f, top - 0.118f), new Vector2(0.93f, top));
                _rowRt[i] = row;
                _rows[i] = PresentableUi.Img(row, Skin ? art.mcqBoxBlue : art.holoButton != null ? art.holoButton : art.roundedSolid, IdleColor, true, true);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = _rows[i];
                btn.transition = Selectable.Transition.None;
                var index = i;
                btn.onClick.AddListener(() => ToggleRow(index));
                var lift = row.gameObject.AddComponent<HoverLift>();
                lift.Init(row);
                btn.onClick.AddListener(lift.Press);
                var box = PresentableUi.Rect("Box", row, new Vector2(0.03f, 0.18f), new Vector2(0.11f, 0.82f));
                PresentableUi.Img(box, art.roundedSolid, new Color(0.7f, 0.95f, 1f, 0.28f));
                var ck = PresentableUi.Rect("Check", row, new Vector2(0.02f, 0.06f), new Vector2(0.125f, 0.94f));
                _checks[i] = PresentableUi.Img(ck, art.check, Color.white, false, false, true);
                _checks[i].gameObject.SetActive(false);
                _labels[i] = PresentableUi.Text("Label", row, new Vector2(0.15f, 0.04f), new Vector2(0.97f, 0.96f), "", 34f, HoloWhite,
                    TextAlignmentOptions.MidlineLeft, false, art.englishFont);
                _labels[i].enableAutoSizing = true;
                _labels[i].fontSizeMin = 16f;
                _labels[i].fontSizeMax = 34f;
                _labels[i].textWrappingMode = TextWrappingModes.Normal;
                _labelRt[i] = _labels[i].rectTransform;
                // the reason line of the ray version (hidden until SUBMIT)
                _reasons[i] = PresentableUi.Text("Reason", row, new Vector2(0.15f, 0.03f), new Vector2(0.97f, 0.50f), "", 24f,
                    new Color(0.88f, 0.97f, 1f), TextAlignmentOptions.TopLeft, false, art.englishFont);
                _reasons[i].enableAutoSizing = true;
                _reasons[i].fontSizeMin = 14f;
                _reasons[i].fontSizeMax = 24f;
                _reasons[i].textWrappingMode = TextWrappingModes.Normal;
                _reasons[i].richText = true;
                _reasons[i].raycastTarget = false;
                _reasonRt[i] = _reasons[i].rectTransform;
                _reasons[i].gameObject.SetActive(false);
            }

            var holo = art.holoButtonPrimary != null ? art.holoButtonPrimary : art.holoButton;
            _submit = HoloButton("SubmitButton", canvas.transform, new Vector2(0.30f, 0.055f), new Vector2(0.70f, 0.18f), holo, out _submitText);
            _submit.onClick.AddListener(PressSubmit);
            _submitRt = _submit.GetComponent<RectTransform>();
            var nextBtn = HoloButton("InlineNextButton", canvas.transform, new Vector2(0.30f, 0.07f), new Vector2(0.70f, 0.150f), holo,
                out _inlineNextText);
            nextBtn.onClick.AddListener(PressNext);
            _inlineNextRt = nextBtn.GetComponent<RectTransform>();
            _inlineNextRt.gameObject.SetActive(false);
            PresentableUi.AlwaysOnTop(canvas);
            _panel.gameObject.SetActive(false);
        }

        // Mirrors PresentableMcqPanel.BuildCorrectPopup / BuildWrongPopup / BuildNextButton (same frame, anchors, close and NEXT).
        private void BuildPopups()
        {
            var canvas = PresentableUi.WorldCanvas("Sc12AssessPopup", transform, new Vector2(900f, 760f), PopupScale, true, 34);
            _popupRoot = canvas.transform;

            var glowRt = PresentableUi.Centered("Glow", canvas.transform, new Vector2(0f, 110f), new Vector2(980f, 560f));
            _glow = PresentableUi.Img(glowRt, art.radialGlow, new Color(0.3f, 1f, 0.5f, 0.5f));
            _glow.raycastTarget = false;
            _glow.gameObject.SetActive(false);

            // correct
            _correctPopup = PresentableUi.Centered("CorrectPopup", canvas.transform, new Vector2(0f, 110f), new Vector2(900f, 482f));
            _correctPopup.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(_correctPopup, art.frameCorrect != null ? art.frameCorrect : art.frameWide, Color.white);
            _correctTitle = HeaderTitle(PresentableUi.Text("Title", _correctPopup, new Vector2(0.18f, 0.855f), new Vector2(0.82f, 0.93f),
                string.Empty, 26f, Color.white, bold: true, font: art.englishFont));
            _tick = PresentableUi.Rect("Tick", _correctPopup, new Vector2(0.42f, 0.62f), new Vector2(0.58f, 0.76f));
            PresentableUi.Img(_tick, art.check, new Color(0.55f, 1f, 0.62f), preserveAspect: true);
            _correctBody = FitInside(PresentableUi.Text("Body", _correctPopup, new Vector2(0.16f, 0.22f), new Vector2(0.84f, 0.58f),
                string.Empty, 26f, new Color(0.86f, 1f, 0.9f), font: art.englishFont), 16f, 28f);
            _correctPopup.gameObject.SetActive(false);

            // wrong, with the close (X) button
            _wrongPopup = PresentableUi.Centered("WrongPopup", canvas.transform, new Vector2(0f, 110f), new Vector2(900f, 482f));
            _wrongPopup.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(_wrongPopup, art.frameWrong != null ? art.frameWrong : art.frameWide, Color.white);
            _wrongTitle = HeaderTitle(PresentableUi.Text("Title", _wrongPopup, new Vector2(0.18f, 0.855f), new Vector2(0.82f, 0.93f),
                string.Empty, 24f, Color.white, bold: true, font: art.englishFont));
            _wrongBody = FitInside(PresentableUi.Text("Body", _wrongPopup, new Vector2(0.16f, 0.36f), new Vector2(0.84f, 0.74f),
                string.Empty, 22f, new Color(1f, 0.94f, 0.94f), font: art.englishFont), 15f, 24f);
            var close = PresentableUi.Rect("Close", _wrongPopup, new Vector2(0.74f, 0.20f), new Vector2(0.86f, 0.32f));
            var closeBg = PresentableUi.Img(close, art.roundedSolid, new Color(0.75f, 0.14f, 0.12f, 1f), sliced: true, raycast: true);
            PresentableUi.Img(PresentableUi.Rect("X", close, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f)), art.cross, Color.white,
                preserveAspect: true);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(OnWrongClosed);
            close.gameObject.AddComponent<HoverLift>().Init(close);
            _wrongPopup.gameObject.SetActive(false);

            // NEXT pill under the correct popup
            _nextButton = PresentableUi.Rect("NextButton", _correctPopup, new Vector2(0.18f, -0.34f), new Vector2(0.82f, -0.08f));
            _nextButton.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(PresentableUi.Rect("Hit", _nextButton, Vector2.zero, Vector2.one), null, new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Pill", _nextButton, Vector2.zero, Vector2.one, new Vector2(-80f, -52f), new Vector2(80f, 52f)),
                art.button, Color.white);
            _nextLabel = PresentableUi.Text("Label", _nextButton, Vector2.zero, Vector2.one, "NEXT", 42f, Color.white, bold: true, font: art.englishFont);
            var nextBtn = _nextButton.gameObject.AddComponent<Button>();
            nextBtn.targetGraphic = _nextButton.GetComponentInChildren<Image>();
            nextBtn.onClick.AddListener(PressNext);
            _nextButton.gameObject.AddComponent<HoverLift>().Init(_nextButton);
            _nextButton.gameObject.SetActive(false);

            PresentableUi.AlwaysOnTop(canvas);
            _popupRoot.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            // the popup stands where the question panel stood (the director moves this host once, before Show)
            if (_popupRoot != null && _panel != null && _popupRoot.gameObject.activeSelf)
            {
                _popupRoot.position = _panel.position;
                _popupRoot.rotation = _panel.rotation;
            }
        }
    }
}
