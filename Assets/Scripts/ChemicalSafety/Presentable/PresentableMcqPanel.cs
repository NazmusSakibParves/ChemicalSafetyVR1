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
            [Tooltip("Scenario 6 look: options arrive one by one, glow and chevrons, a light comet round the frame (Sc06McqFx).")]
            public bool fancy;
        }

        private sealed class Row
        {
            public RectTransform root;
            public Button button;
            public Image fill;
            public Image frame;
            public Image check;
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
        private bool _locked;
        private bool _built;
        private Sc06McqFx _fx;

        public PresentableArt Art => art;
        public RectTransform CanvasRect { get; private set; }
        public int RowCount => _rows.Count;
        public RectTransform RowRoot(int index) => _rows[index].root;
        public bool Fancy => _fx != null;
        public Sc06McqFx Fx => _fx;

        /// <summary>(index, isCorrect)</summary>
        public event Action<int, bool> Answered;
        public event Action NextPressed;

        /// <summary>Raised when the red card is closed with its X and the question is open for a retry.</summary>
        public event Action WrongClosed;

        /// <summary>Raised once the correct / wrong popup is fully on screen (isCorrect).</summary>
        public event Action<bool> PopupShown;

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
            SetQuestionChrome(true);
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
                const float iconW = 0.082f;
                const float iconGap = 0.014f;
                const float iconRight = 0.95f;
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

            ApplyNormalState();
            _correctPopup.gameObject.SetActive(false);
            _wrongPopup.gameObject.SetActive(false);
            _nextButton.gameObject.SetActive(false);
            StopAllCoroutines();
            _fx = null;
            var oldFx = GetComponent<Sc06McqFx>();
            if (question.fancy)
            {
                _fx = oldFx != null ? oldFx : gameObject.AddComponent<Sc06McqFx>();
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

            SetQuestionChrome(false);
            var row = _rows[index];
            row.check.gameObject.SetActive(true);
            PaintRow(row, true, Color.white);
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i != index)
                    _rows[i].root.GetComponent<CanvasGroup>().alpha = 0.55f;
            }

            if (_fx != null)
                _fx.OnResult(true, index);
            _correctTitle.text = _question.correctTitle;
            _correctBody.text = opt.feedback;
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
                _frame.gameObject.SetActive(visible);
            if (_readPlate != null)
                _readPlate.gameObject.SetActive(visible);
            if (_header != null)
                _header.gameObject.SetActive(visible);
            if (_prompt != null)
                _prompt.gameObject.SetActive(visible);
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
            _header.color = Color.white;
            foreach (var r in _rows)
            {
                PaintRow(r, false, Color.white);
                r.check.gameObject.SetActive(false);
                r.root.GetComponent<CanvasGroup>().alpha = 1f;
            }
        }

        private void PaintRow(Row row, bool hot, Color tint)
        {
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
            transform.localScale = Vector3.one * 0.8f;
            yield return PresentableFx.Tween(0.45f, t =>
            {
                _group.alpha = Mathf.Clamp01(t * 1.5f);
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, t);
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
            CanvasRect = (RectTransform)root;
            var font = art.englishFont;

            var panel = root;
            if (art.frameWide != null)
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
            PresentableUi.Img(_readPlate, art.roundedSolid, new Color(0.02f, 0.05f, 0.09f, 0.82f), sliced: true);
            _header = HeaderTitle(PresentableUi.Text("Header", panel, new Vector2(0.08f, 0.845f), new Vector2(0.92f, 0.93f),
                string.Empty, 24f, Color.white, bold: true, font: font));
            _header.fontSizeMin = 18f;
            _header.fontSizeMax = 28f;
            _prompt = FitInside(PresentableUi.Text("Prompt", panel, new Vector2(0.08f, 0.675f), new Vector2(0.92f, 0.825f),
                string.Empty, 28f, new Color(0.94f, 0.97f, 1f), font: font), 20f, 32f);

            for (var i = 0; i < 4; i++)
                _rows.Add(BuildRow(panel, i, font));

            BuildCorrectPopup(root, font);
            BuildWrongPopup(root, font);
            BuildNextButton(root, font);
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

            var bar = art.optionBar != null ? art.optionBar : art.roundedSolid;
            row.frame = PresentableUi.Img(PresentableUi.Rect("Frame", row.root, Vector2.zero, Vector2.one),
                bar, Color.white);
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
                if (_locked || art.optionBar == null)
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

            var checkRt = PresentableUi.Rect("Check", row.root, new Vector2(0.015f, 0.28f), new Vector2(0.07f, 0.72f));
            row.check = PresentableUi.Img(checkRt, art.check, PresentableUi.Green, preserveAspect: true);
            row.check.gameObject.SetActive(false);
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
