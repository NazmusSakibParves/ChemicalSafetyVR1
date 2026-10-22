using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC01 popup listing the four label names, opened by poking an unlabeled drum. Flat (not curved) dark-blue
    /// panel with a thin light border, straight rounded rows and orange corner brackets, like the step05
    /// "IMMEDIATE ACTION PROTOCOL" reference. A wrong pick turns this same popup red (it never uses the MCQ popups).
    /// </summary>
    public sealed class LabelPickPopup : MonoBehaviour
    {
        private static readonly Color PanelBorder = new Color(0.45f, 0.78f, 1f, 0.95f);
        private static readonly Color PanelFill = new Color(0.04f, 0.14f, 0.26f, 0.95f);
        private static readonly Color RowBorder = new Color(0.4f, 0.75f, 1f, 0.9f);
        private static readonly Color RowFill = new Color(0.07f, 0.2f, 0.34f, 0.96f);
        private static readonly Color RowHover = new Color(0.16f, 0.4f, 0.6f, 1f);
        private static readonly Color RedBorder = new Color(1f, 0.38f, 0.32f, 0.95f);
        private static readonly Color RedPanel = new Color(0.22f, 0.04f, 0.05f, 0.95f);
        private static readonly Color RedRow = new Color(0.34f, 0.07f, 0.08f, 0.96f);
        private static readonly Color RedHover = new Color(0.55f, 0.14f, 0.13f, 1f);
        private static readonly Color Orange = new Color(1f, 0.52f, 0.2f, 1f);

        public static readonly string[] Names =
            { "Faded Label", "No Label", "Mismatched GHS vs SDS", "Correctly Labelled" };

        [SerializeField] private PresentableArt art;
        [SerializeField] private Vector2 sizePx = new Vector2(1000f, 720f);
        [SerializeField] private float metersPerPixel = 0.00052f;

        private CanvasGroup _group;
        private RectTransform _panel;
        private Image _panelBorder;
        private Image _panelFill;
        private Image _titleLine;
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
            _panelBorder.color = wrong ? RedBorder : PanelBorder;
            _panelFill.color = wrong ? RedPanel : PanelFill;
            _titleLine.color = wrong ? RedBorder : new Color(PanelBorder.r, PanelBorder.g, PanelBorder.b, 0.55f);
            _title.text = wrong ? "WRONG LABEL - TRY AGAIN" : "SELECT THE CORRECT LABEL";
            _title.color = wrong ? PresentableUi.Red : Color.white;
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
            _rowBorders[i].color = _wrong ? RedBorder : RowBorder;
            _rowFills[i].color = _hover[i] ? (_wrong ? RedHover : RowHover) : (_wrong ? RedRow : RowFill);
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

            // flat panel: light border, dark fill (rounded corners)
            _panel = PresentableUi.Rect("Panel", root, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
            _panelBorder = PresentableUi.Img(_panel, art.roundedSolid, PanelBorder, sliced: true);
            _panelFill = PresentableUi.Img(PresentableUi.Rect("Fill", _panel, Vector2.zero, Vector2.one,
                new Vector2(4f, 4f), new Vector2(-4f, -4f)), art.roundedSolid, PanelFill, sliced: true);

            _title = PresentableUi.Text("Title", _panel, new Vector2(0.12f, 0.86f), new Vector2(0.86f, 0.96f),
                string.Empty, 40f, Color.white, bold: true, font: font);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 24f;
            _title.fontSizeMax = 40f;
            _titleLine = PresentableUi.Img("TitleLine", _panel, new Vector2(0.06f, 0.845f), new Vector2(0.94f, 0.85f),
                art.roundedSolid, new Color(PanelBorder.r, PanelBorder.g, PanelBorder.b, 0.55f));

            // straight rounded rows: rounded border + fill, centred white text
            const float rowH = 0.15f;
            const float gap = 0.027f;
            for (var i = 0; i < 4; i++)
            {
                var top = 0.81f - i * (rowH + gap); // last row ends at 0.15
                var slot = PresentableUi.Rect("Option" + (i + 1), _panel, new Vector2(0.07f, top - rowH),
                    new Vector2(0.93f, top));
                slot.localPosition += new Vector3(0f, 0f, -8f);
                _rowBorders[i] = PresentableUi.Img(PresentableUi.Rect("Border", slot, Vector2.zero, Vector2.one),
                    art.roundedSolid, RowBorder, sliced: true);
                _rowFills[i] = PresentableUi.Img(PresentableUi.Rect("Fill", slot, Vector2.zero, Vector2.one,
                    new Vector2(3f, 3f), new Vector2(-3f, -3f)), art.roundedSolid, RowFill, sliced: true, raycast: true);
                _labels[i] = PresentableUi.Text(PresentableUi.Rect("Label", slot, new Vector2(0.04f, 0.1f),
                    new Vector2(0.96f, 0.9f)), Names[i], 36f, Color.white, bold: true, font: font);
                _labels[i].textWrappingMode = TextWrappingModes.NoWrap;
                _labels[i].enableAutoSizing = true;
                _labels[i].fontSizeMin = 22f;
                _labels[i].fontSizeMax = 36f;

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
            var close = PresentableUi.Rect("Close", _panel, new Vector2(0.88f, 0.87f), new Vector2(0.96f, 0.95f));
            var closeBg = PresentableUi.Img(close, art.roundedSolid, new Color(0.1f, 0.3f, 0.45f, 0.95f), sliced: true,
                raycast: true);
            PresentableUi.Img(PresentableUi.Rect("X", close, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f)),
                art.cross, Color.white, preserveAspect: true);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() =>
            {
                if (IsOpen)
                    Closed?.Invoke();
            });

            // orange corner brackets hugging the panel (top right, bottom left), like step05
            Bracket("AccentTopH", new Vector2(1f, 1f), new Vector2(-72f, 3f), new Vector2(10f, 9f));
            Bracket("AccentTopV", new Vector2(1f, 1f), new Vector2(4f, -62f), new Vector2(10f, 9f));
            Bracket("AccentBotH", new Vector2(0f, 0f), new Vector2(-10f, -9f), new Vector2(72f, -3f));
            Bracket("AccentBotV", new Vector2(0f, 0f), new Vector2(-10f, -9f), new Vector2(-4f, 62f));

            // never hidden by rack beams or drums when the user stands close
            PresentableUi.AlwaysOnTop(canvas);
        }

        private void Bracket(string name, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = PresentableUi.Rect(name, _panel, anchor, anchor, offsetMin, offsetMax);
            PresentableUi.Img(rt, art.roundedSolid, Orange);
        }
    }
}
