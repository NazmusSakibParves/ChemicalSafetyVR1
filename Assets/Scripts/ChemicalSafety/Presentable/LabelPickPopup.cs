using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC01 popup listing the four label names, opened by poking an unlabeled drum. Uses the same wide
    /// blue frame and option bars as the MCQ. A wrong pick turns the plate and the rows red.
    /// </summary>
    public sealed class LabelPickPopup : MonoBehaviour
    {
        private static readonly Color RowFillRed = new Color(0.3f, 0.05f, 0.05f, 0.78f);
        private static readonly Color RowHover = new Color(0.08f, 0.32f, 0.4f, 0.9f);
        private static readonly Color RedHover = new Color(0.5f, 0.1f, 0.1f, 0.9f);

        public static readonly string[] Names =
            { "Faded Label", "No Label", "Mismatched GHS vs SDS", "Correctly Labelled" };

        [SerializeField] private PresentableArt art;
        [SerializeField] private Vector2 sizePx = new Vector2(1000f, 720f);
        [SerializeField] private float metersPerPixel = 0.00052f;

        private CanvasGroup _group;
        private RectTransform _panel;
        private Image _panelBorder;
        private Image _panelFill;
        private TextMeshProUGUI _title;
        private readonly Image[] _rowBorders = new Image[4];
        private readonly Image[] _rowFills = new Image[4];
        private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[4];
        private readonly bool[] _hover = new bool[4];
        private bool _built;
        private bool _wrong;

        /// <summary>Index into <see cref="Names"/>.</summary>
        public event Action<int> Chosen;
        public event Action Closed;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Build();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        public void Open()
        {
            Build();
            IsOpen = true;
            SetWrong(false);
            StopAllCoroutines();
            StartCoroutine(PopIn());
        }

        public void Close()
        {
            IsOpen = false;
            StopAllCoroutines();
            StartCoroutine(PopOut());
        }

        public void SetWrong(bool wrong)
        {
            _wrong = wrong;
            _panelBorder.color = Color.white;
            _panelFill.color = wrong
                ? new Color(0.22f, 0.04f, 0.05f, 0.88f)
                : new Color(0.02f, 0.05f, 0.09f, 0.82f);
            _title.text = wrong ? "WRONG LABEL - TRY AGAIN" : "SELECT THE CORRECT LABEL";
            _title.color = Color.white;
            for (var i = 0; i < _rowBorders.Length; i++)
            {
                _hover[i] = false;
                ApplyRow(i);
            }

            if (wrong)
                StartCoroutine(Shake());
        }

        private void ApplyRow(int i)
        {
            var bar = art.optionBar != null ? art.optionBar : art.roundedSolid;
            if (_hover[i] && art.optionBarHot != null)
                bar = art.optionBarHot;
            _rowBorders[i].sprite = bar;
            _rowBorders[i].color = _wrong ? new Color(1f, 0.62f, 0.58f, 1f) : Color.white;
            _rowFills[i].color = _hover[i]
                ? (_wrong ? RedHover : RowHover)
                : (_wrong ? RowFillRed : new Color(1f, 1f, 1f, 0.02f));
        }

        private IEnumerator Shake()
        {
            var basePos = _panel.anchoredPosition;
            yield return PresentableFx.Tween(0.35f, t =>
            {
                _panel.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 45f) * (1f - t) * 14f, 0f);
            }, x => x);
            _panel.anchoredPosition = basePos;
        }

        private void OnPicked(int index)
        {
            if (!IsOpen)
                return;
            PresentableFx.Play(PresentableFx.Tick);
            PresentableFx.Haptic(true, 0.35f, 0.05f);
            Chosen?.Invoke(index);
        }

        private IEnumerator PopIn()
        {
            _group.interactable = true;
            _group.blocksRaycasts = true;
            transform.localScale = Vector3.one * 0.8f;
            yield return PresentableFx.Tween(0.35f, t =>
            {
                _group.alpha = Mathf.Clamp01(t * 1.6f);
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, t);
            }, PresentableFx.EaseOutBack);
        }

        private IEnumerator PopOut()
        {
            _group.interactable = false;
            _group.blocksRaycasts = false;
            var from = _group.alpha;
            yield return PresentableFx.Tween(0.25f, t =>
            {
                _group.alpha = Mathf.Lerp(from, 0f, t);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, t);
            });
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            var canvas = PresentableUi.WorldCanvas("LabelPopupCanvas", transform, sizePx, metersPerPixel, true, 220);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            var root = canvas.transform;
            var font = art.englishFont;

            var card = PresentableUi.Rect("Card", root, Vector2.zero, Vector2.one);
            _panel = card;
            var frame = PresentableUi.Centered("Frame", card, Vector2.zero, sizePx * 1.08f);
            _panelBorder = PresentableUi.Img(frame, art.frameWide != null ? art.frameWide : art.roundedSolid, Color.white);
            var plate = PresentableUi.Rect("ReadPlate", card, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.77f));
            _panelFill = PresentableUi.Img(plate, art.roundedSolid, new Color(0.02f, 0.05f, 0.09f, 0.82f), sliced: true);

            _title = PresentableUi.Text("Title", card, new Vector2(0.12f, 0.84f), new Vector2(0.8f, 0.93f),
                string.Empty, 30f, Color.white, bold: true, font: font);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Ellipsis;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 18f;
            _title.fontSizeMax = 32f;

            const float rowH = 0.145f;
            const float gap = 0.012f;
            const float firstTop = 0.75f;
            for (var i = 0; i < 4; i++)
            {
                var top = firstTop - i * (rowH + gap);
                var slot = PresentableUi.Rect("Option" + (i + 1), card, new Vector2(0.04f, top - rowH),
                    new Vector2(0.96f, top));
                slot.localPosition += new Vector3(0f, 0f, -8f);
                var bar = art.optionBar != null ? art.optionBar : art.roundedSolid;
                _rowBorders[i] = PresentableUi.Img(PresentableUi.Rect("Border", slot, Vector2.zero, Vector2.one),
                    bar, Color.white);
                _rowFills[i] = PresentableUi.Img(PresentableUi.Rect("Fill", slot, Vector2.zero, Vector2.one),
                    null, new Color(1f, 1f, 1f, 0.02f), raycast: true);
                _labels[i] = PresentableUi.Text(PresentableUi.Rect("Label", slot, new Vector2(0.06f, 0.12f),
                    new Vector2(0.94f, 0.88f)), Names[i], 28f, Color.white, bold: true, font: font);
                _labels[i].textWrappingMode = TextWrappingModes.Normal;
                _labels[i].overflowMode = TextOverflowModes.Truncate;
                _labels[i].enableAutoSizing = true;
                _labels[i].fontSizeMin = 16f;
                _labels[i].fontSizeMax = 28f;

                var btn = slot.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = _rowFills[i];
                var captured = i;
                btn.onClick.AddListener(() => OnPicked(captured));
                var lift = slot.gameObject.AddComponent<HoverLift>();
                lift.Init(slot);
                lift.HoverChanged += on =>
                {
                    _hover[captured] = on;
                    ApplyRow(captured);
                };
            }

            // close (X), small, inside the panel at the top right
            var close = PresentableUi.Rect("Close", card, new Vector2(0.84f, 0.84f), new Vector2(0.94f, 0.94f));
            var closeBg = PresentableUi.Img(close, art.roundedSolid, new Color(0.28f, 0.03f, 0.04f, 0.96f), sliced: true,
                raycast: true);
            PresentableUi.Img(PresentableUi.Rect("X", close, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f)),
                art.cross, new Color(0.72f, 0.05f, 0.07f), preserveAspect: true);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() =>
            {
                if (IsOpen)
                    Closed?.Invoke();
            });

            // never hidden by rack beams or drums when the user stands close
            PresentableUi.AlwaysOnTop(canvas);
        }
    }
}
