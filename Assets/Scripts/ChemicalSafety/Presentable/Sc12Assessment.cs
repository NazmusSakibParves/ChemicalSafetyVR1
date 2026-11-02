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
        }

        [SerializeField] private PresentableArt art;

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color RowIdle = new Color(0.8f, 0.95f, 1f, 0.9f);
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

        /// <summary>Raised on SUBMIT: (selection by shown row, all right).</summary>
        public event Action<bool[], bool> Submitted;

        public event Action NextPressed;

        public bool Visible => _panel != null && _panel.gameObject.activeSelf;
        public bool Won => _won;
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
            for (var i = 0; i < RowCount; i++)
            {
                _labels[i].text = q.labels[i];
                _checks[i].gameObject.SetActive(false);
                _rows[i].color = RowIdle;
            }

            _submitText.text = q.submitLabel;
            _nextLabel.text = q.nextLabel;
            _popupRoot.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
            StartCoroutine(PopIn());
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
            _rows[row].color = _on[row] ? RowOn : RowIdle;
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
            if (allRight)
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
            if (_won)
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
            _panel.localScale = Vector3.one * 0.00064f;
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
            var baseScale = Vector3.one * 0.00064f;
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
            var plate = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("Sc12AssessPanel", transform, new Vector2(820f, 780f), 0.00064f, true, 30);
            _panel = canvas.transform;
            canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, plate, Color.white);
            if (art.holoPanel != null)
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

            _labels = new TextMeshProUGUI[rows];
            _rows = new Image[rows];
            _checks = new Image[rows];
            for (var i = 0; i < rows; i++)
            {
                var top = 0.73f - i * 0.135f;
                var row = PresentableUi.Rect("Row" + i, canvas.transform, new Vector2(0.07f, top - 0.118f), new Vector2(0.93f, top));
                _rows[i] = PresentableUi.Img(row, art.holoButton != null ? art.holoButton : art.roundedSolid, RowIdle, true, true);
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
            }

            var holo = art.holoButtonPrimary != null ? art.holoButtonPrimary : art.holoButton;
            _submit = HoloButton("SubmitButton", canvas.transform, new Vector2(0.30f, 0.055f), new Vector2(0.70f, 0.18f), holo, out _submitText);
            _submit.onClick.AddListener(PressSubmit);
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
