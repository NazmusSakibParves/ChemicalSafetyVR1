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
    /// World-space MCQ matching step05 (question), step06 (correct) and step07 (wrong): cyan sci-fi frame,
    /// title, four rows (radio + label + drum thumbnail), green CORRECT popup + NEXT, red WRONG popup with X.
    /// Answers are poked with the gloved finger.
    /// </summary>
    public sealed class PresentableMcqPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Option
        {
            public string label;
            public string wrongLabel;
            public Sprite[] icons = Array.Empty<Sprite>();
            [Tooltip("Optional sticker drawn on top of the matching icon (same index).")]
            public Sprite[] overlays = Array.Empty<Sprite>();
            [TextArea(2, 4)] public string feedback;
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
        }

        private sealed class Row
        {
            public RectTransform root;
            public Button button;
            public Image fill;
            public Image frame;
            public Image radio;
            public Image dot;
            public Image check;
            public TextMeshProUGUI label;
            public Image[] icons;
            public Image[] overlays;
        }

        private const float FrameScale = 1.14f; // frame drawn a bit larger than the content so nothing touches its border

        private static readonly Color RowFill = new Color(0.02f, 0.16f, 0.22f, 0.72f);
        private static readonly Color RowFillGreen = new Color(0.05f, 0.5f, 0.28f, 0.88f);
        private static readonly Color RowFillRed = new Color(0.45f, 0.05f, 0.05f, 0.88f);
        private static readonly Color RowFillRedDim = new Color(0.14f, 0.04f, 0.04f, 0.6f);
        private static readonly Color RadioCyan = new Color(0.45f, 0.95f, 1f, 1f);

        [SerializeField] private PresentableArt art;
        [SerializeField] private Vector2 sizePx = new Vector2(1300f, 790f);
        [SerializeField] private float metersPerPixel = 0.00055f;

        private readonly List<Row> _rows = new List<Row>();
        private Question _question;
        private CanvasGroup _group;
        private Image _frame;
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
        private bool _locked;
        private bool _built;

        /// <summary>(index, isCorrect)</summary>
        public event Action<int, bool> Answered;
        public event Action NextPressed;

        public bool IsOpen { get; private set; }

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
            _locked = false;
            IsOpen = true;
            _header.text = question.header;
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
                _rows[i].label.rectTransform.anchorMax = new Vector2(n > 0 ? 0.97f - n * 0.125f - 0.01f : 0.86f, 1f);
                for (var k = 0; k < _rows[i].icons.Length; k++)
                {
                    var has = k < n && opt.icons[k] != null;
                    var icon = _rows[i].icons[k];
                    icon.gameObject.SetActive(has);
                    if (!has)
                        continue;
                    icon.sprite = opt.icons[k];
                    var x1 = 0.97f - (n - 1 - k) * 0.125f;
                    icon.rectTransform.anchorMin = new Vector2(x1 - 0.115f, 0.07f);
                    icon.rectTransform.anchorMax = new Vector2(x1, 0.93f);
                    var sticker = opt.overlays != null && k < opt.overlays.Length ? opt.overlays[k] : null;
                    _rows[i].overlays[k].gameObject.SetActive(sticker != null);
                    _rows[i].overlays[k].sprite = sticker;
                }
            }

            ApplyNormalState();
            _correctPopup.gameObject.SetActive(false);
            _wrongPopup.gameObject.SetActive(false);
            _nextButton.gameObject.SetActive(false);
            StopAllCoroutines();
            StartCoroutine(PopIn());
        }

        public void Hide()
        {
            IsOpen = false;
            StopAllCoroutines();
            StartCoroutine(PopOut());
        }

        private void OnOptionClicked(int index)
        {
            if (_locked || _question == null)
                return;

            _locked = true;
            var correct = index == _question.correctIndex;
            var opt = _question.options[index];

            if (correct)
                StartCoroutine(ShowCorrect(index, opt));
            else
                StartCoroutine(ShowWrong(index, opt));

            Answered?.Invoke(index, correct);
        }

        private IEnumerator ShowCorrect(int index, Option opt)
        {
            PresentableFx.Play(PresentableFx.Success);
            PresentableFx.Haptic(true, 0.6f, 0.15f);

            var row = _rows[index];
            row.dot.gameObject.SetActive(true);
            row.check.gameObject.SetActive(true);
            row.fill.color = RowFillGreen;
            row.frame.color = new Color(0.55f, 1f, 0.65f, 1f);
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i != index)
                    _rows[i].root.GetComponent<CanvasGroup>().alpha = 0.55f;
            }

            _correctTitle.text = _question.correctTitle;
            _correctBody.text = opt.feedback;
            yield return PopupIn(_correctPopup);

            _nextLabel.text = _question.nextLabel;
            yield return PopupIn(_nextButton);
        }

        private IEnumerator ShowWrong(int index, Option opt)
        {
            PresentableFx.Play(PresentableFx.Error);
            PresentableFx.Haptic(true, 0.9f, 0.25f);

            _frame.sprite = art.popupFrameRed;
            _header.color = PresentableUi.Red;
            for (var i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                var selected = i == index;
                r.frame.sprite = art.answerBoxRed;
                r.frame.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                r.fill.color = selected ? RowFillRed : RowFillRedDim;
                r.radio.color = selected ? PresentableUi.Red : PresentableUi.RedDim;
                r.dot.color = PresentableUi.Red;
                r.dot.gameObject.SetActive(selected);
                r.root.GetComponent<CanvasGroup>().alpha = selected ? 1f : 0.6f;
            }

            if (!string.IsNullOrEmpty(opt.wrongLabel))
                _rows[index].label.text = opt.wrongLabel;

            _wrongTitle.text = _question.wrongTitle;
            _wrongBody.text = opt.feedback;
            StartCoroutine(Shake(_rows[index].root));
            yield return PopupIn(_wrongPopup);
        }

        private void OnWrongClosed()
        {
            PresentableFx.Play(PresentableFx.Tick);
            StartCoroutine(RetryRoutine());
        }

        private IEnumerator RetryRoutine()
        {
            yield return PopupOut(_wrongPopup);
            if (_question != null)
            {
                for (var i = 0; i < _rows.Count && i < _question.options.Length; i++)
                    _rows[i].label.text = _question.options[i].label;
            }

            ApplyNormalState();
            _locked = false;
        }

        private void OnNextClicked()
        {
            PresentableFx.Play(PresentableFx.Tick);
            NextPressed?.Invoke();
        }

        private void ApplyNormalState()
        {
            _frame.sprite = art.popupFrame;
            _header.color = PresentableUi.Cyan;
            foreach (var r in _rows)
            {
                r.frame.sprite = art.answerBox;
                r.frame.color = Color.white;
                r.fill.color = RowFill;
                r.radio.color = RadioCyan;
                r.dot.color = PresentableUi.Green;
                r.dot.gameObject.SetActive(false);
                r.check.gameObject.SetActive(false);
                r.root.GetComponent<CanvasGroup>().alpha = 1f;
            }
        }

        private IEnumerator PopIn()
        {
            _group.interactable = true;
            _group.blocksRaycasts = true;
            transform.localScale = Vector3.one * 0.8f;
            yield return PresentableFx.Tween(0.45f, t =>
            {
                _group.alpha = Mathf.Clamp01(t * 1.5f);
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, t);
            }, PresentableFx.EaseOutBack);

            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].root.gameObject.activeSelf)
                    StartCoroutine(RowIn(_rows[i], i * 0.07f));
            }
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
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, t);
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
            var font = art.englishFont;

            // The designer frame has a soft transparent margin, so it is drawn larger than the canvas.
            var frameRt = PresentableUi.Centered("Frame", root, Vector2.zero,
                new Vector2(sizePx.x / 0.788f * FrameScale, sizePx.x / 0.788f / 1.777f * FrameScale));
            _frame = PresentableUi.Img(frameRt, art.popupFrame, Color.white);

            _header = PresentableUi.Text("Header", root, new Vector2(0.06f, 0.875f), new Vector2(0.94f, 0.95f),
                string.Empty, 40f, PresentableUi.Cyan, bold: true, font: font);
            _header.textWrappingMode = TextWrappingModes.NoWrap;
            _header.enableAutoSizing = true;
            _header.fontSizeMin = 24f;
            _header.fontSizeMax = 40f;
            _prompt = PresentableUi.Text("Prompt", root, new Vector2(0.06f, 0.8f), new Vector2(0.94f, 0.875f),
                string.Empty, 28f, Color.white, font: font);
            _prompt.enableAutoSizing = true;
            _prompt.fontSizeMin = 20f;
            _prompt.fontSizeMax = 28f;

            for (var i = 0; i < 4; i++)
                _rows.Add(BuildRow(root, i, font));

            BuildCorrectPopup(root, font);
            BuildWrongPopup(root, font);
            BuildNextButton(root, font);
        }

        private Row BuildRow(Transform root, int index, TMP_FontAsset font)
        {
            const float rowH = 0.165f;
            const float gap = 0.02f;
            var top = 0.775f - index * (rowH + gap);
            var row = new Row
            {
                root = PresentableUi.Rect("Row" + (index + 1), root, new Vector2(0.17f, top - rowH),
                    new Vector2(0.92f, top))
            };
            row.root.gameObject.AddComponent<CanvasGroup>();
            row.root.localPosition += new Vector3(0f, 0f, -12f);

            var fillRt = PresentableUi.Rect("Fill", row.root, Vector2.zero, Vector2.one, new Vector2(6f, 4f),
                new Vector2(-6f, -4f));
            row.fill = PresentableUi.Img(fillRt, art.roundedSolid, RowFill, raycast: true);
            var frameRt = PresentableUi.Rect("Frame", row.root, Vector2.zero, Vector2.one);
            row.frame = PresentableUi.Img(frameRt, art.answerBox, Color.white);

            row.button = row.root.gameObject.AddComponent<Button>();
            row.button.transition = Selectable.Transition.None;
            row.button.targetGraphic = row.fill;
            var captured = index;
            row.button.onClick.AddListener(() => OnOptionClicked(captured));
            row.root.gameObject.AddComponent<HoverLift>().Init(row.root);

            var radioRt = PresentableUi.Rect("Radio", row.root, new Vector2(-0.125f, 0.12f), new Vector2(-0.02f, 0.88f));
            row.radio = PresentableUi.Img(radioRt, art.circle, RadioCyan, preserveAspect: true);
            var dotRt = PresentableUi.Rect("Dot", radioRt, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f));
            row.dot = PresentableUi.Img(dotRt, art.dot, PresentableUi.Green, preserveAspect: true);

            var labelRt = PresentableUi.Rect("Label", row.root, new Vector2(0.04f, 0f), new Vector2(0.62f, 1f));
            row.label = PresentableUi.Text(labelRt, string.Empty, 44f, Color.white, TextAlignmentOptions.Left,
                true, font);
            row.label.enableAutoSizing = true;
            row.label.fontSizeMin = 22f;
            row.label.fontSizeMax = 44f;

            row.icons = new Image[3];
            row.overlays = new Image[3];
            for (var k = 0; k < 3; k++)
            {
                var iconRt = PresentableUi.Rect("Icon" + k, row.root, new Vector2(0.8f, 0.07f), new Vector2(0.9f, 0.93f));
                row.icons[k] = PresentableUi.Img(iconRt, null, Color.white, preserveAspect: true);
                // Sticker sits on the drum body: 62 % of the drum width, top at 30 % of its height.
                var stickerRt = PresentableUi.Rect("Sticker", iconRt, new Vector2(0.19f, 0.21f), new Vector2(0.81f, 0.70f));
                row.overlays[k] = PresentableUi.Img(stickerRt, null, Color.white, preserveAspect: true);
            }

            var checkRt = PresentableUi.Rect("Check", row.root, new Vector2(0.9f, 0.12f), new Vector2(1.0f, 0.88f));
            row.check = PresentableUi.Img(checkRt, art.check, PresentableUi.Green, preserveAspect: true);
            return row;
        }

        private void BuildCorrectPopup(Transform root, TMP_FontAsset font)
        {
            _correctPopup = PresentableUi.Centered("CorrectPopup", root, new Vector2(-20f, 20f),
                new Vector2(730f, 440f));
            _correctPopup.localPosition += new Vector3(0f, 0f, -45f);
            _correctPopup.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(PresentableUi.Rect("Glow", _correctPopup, Vector2.zero, Vector2.one,
                new Vector2(-50f, -50f), new Vector2(50f, 50f)), art.softGlow, new Color(0.2f, 1f, 0.4f, 0.45f));
            PresentableUi.Img(PresentableUi.Rect("Fill", _correctPopup, Vector2.zero, Vector2.one), art.roundedSolid,
                new Color(0.03f, 0.35f, 0.15f, 0.96f));
            PresentableUi.Img(PresentableUi.Rect("Frame", _correctPopup, Vector2.zero, Vector2.one), art.pop02,
                PresentableUi.Green);
            _correctTitle = PresentableUi.Text("Title", _correctPopup, new Vector2(0.08f, 0.71f),
                new Vector2(0.92f, 0.87f), string.Empty, 50f, new Color(0.6f, 1f, 0.6f), bold: true, font: font);
            _correctTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _correctTitle.enableAutoSizing = true;
            _correctTitle.fontSizeMin = 30f;
            _correctTitle.fontSizeMax = 50f;
            _correctBody = PresentableUi.Text("Body", _correctPopup, new Vector2(0.09f, 0.15f),
                new Vector2(0.91f, 0.69f), string.Empty, 29f, Color.white, font: font);
            _correctBody.enableAutoSizing = true;
            _correctBody.fontSizeMin = 20f;
            _correctBody.fontSizeMax = 29f;
            var check = PresentableUi.Rect("Check", _correctPopup, new Vector2(0.84f, -0.06f), new Vector2(0.98f, 0.16f));
            PresentableUi.Img(check, art.check, PresentableUi.Green, preserveAspect: true);
            _correctPopup.gameObject.SetActive(false);
        }

        private void BuildWrongPopup(Transform root, TMP_FontAsset font)
        {
            _wrongPopup = PresentableUi.Centered("WrongPopup", root, new Vector2(-20f, 20f), new Vector2(740f, 430f));
            _wrongPopup.localPosition += new Vector3(0f, 0f, -45f);
            _wrongPopup.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(PresentableUi.Rect("Glow", _wrongPopup, Vector2.zero, Vector2.one,
                new Vector2(-50f, -50f), new Vector2(50f, 50f)), art.softGlow, new Color(1f, 0.2f, 0.15f, 0.45f));
            PresentableUi.Img(PresentableUi.Rect("Fill", _wrongPopup, Vector2.zero, Vector2.one), art.roundedSolid,
                new Color(0.4f, 0.04f, 0.04f, 0.96f));
            PresentableUi.Img(PresentableUi.Rect("Frame", _wrongPopup, Vector2.zero, Vector2.one), art.pop02,
                PresentableUi.Red);
            _wrongTitle = PresentableUi.Text("Title", _wrongPopup, new Vector2(0.08f, 0.7f), new Vector2(0.85f, 0.87f),
                string.Empty, 50f, new Color(1f, 0.55f, 0.5f), bold: true, font: font);
            _wrongTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _wrongTitle.enableAutoSizing = true;
            _wrongTitle.fontSizeMin = 30f;
            _wrongTitle.fontSizeMax = 50f;
            _wrongBody = PresentableUi.Text("Body", _wrongPopup, new Vector2(0.09f, 0.12f), new Vector2(0.91f, 0.67f),
                string.Empty, 30f, Color.white, TextAlignmentOptions.Left, font: font);
            _wrongBody.enableAutoSizing = true;
            _wrongBody.fontSizeMin = 20f;
            _wrongBody.fontSizeMax = 30f;

            var close = PresentableUi.Rect("Close", _wrongPopup, new Vector2(0.88f, 0.76f), new Vector2(0.98f, 0.96f));
            var closeBg = PresentableUi.Img(close, art.roundedPanel, new Color(0.8f, 0.15f, 0.12f, 1f), raycast: true);
            var closeIcon = PresentableUi.Rect("X", close, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            PresentableUi.Img(closeIcon, art.cross, Color.white, preserveAspect: true);
            var btn = close.gameObject.AddComponent<Button>();
            btn.targetGraphic = closeBg;
            btn.onClick.AddListener(OnWrongClosed);
            close.gameObject.AddComponent<HoverLift>().Init(close);
            _wrongPopup.gameObject.SetActive(false);
        }

        private void BuildNextButton(Transform root, TMP_FontAsset font)
        {
            _nextButton = PresentableUi.Rect("NextButton", root, new Vector2(0.6f, -0.15f), new Vector2(0.92f, -0.03f));
            _nextButton.localPosition += new Vector3(0f, 0f, -30f);
            _nextButton.gameObject.AddComponent<CanvasGroup>();
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", _nextButton, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Pill", _nextButton, Vector2.zero, Vector2.one,
                new Vector2(-80f, -52f), new Vector2(80f, 52f)), art.button, Color.white);
            _nextLabel = PresentableUi.Text("Label", _nextButton, Vector2.zero, Vector2.one, "NEXT", 46f, Color.white,
                bold: true, font: font);
            var btn = _nextButton.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(OnNextClicked);
            _nextButton.gameObject.AddComponent<HoverLift>().Init(_nextButton);
            _nextButton.gameObject.SetActive(false);
        }
    }

    /// <summary>Hover feedback: lifts the element toward the user + tiny haptic tick.</summary>
    public sealed class HoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _rt;
        private Vector3 _baseScale = Vector3.one;
        private float _target = 1f;
        private float _current = 1f;

        public void Init(RectTransform rt)
        {
            _rt = rt;
            _baseScale = rt.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _target = 1.04f;
            PresentableFx.Haptic(true, 0.12f, 0.03f);
            PresentableFx.Play(PresentableFx.Tick, 0.35f);
        }

        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        private void Update()
        {
            if (_rt == null)
                return;
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            _rt.localScale = _baseScale * _current;
        }
    }
}
