using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Clear popup listing the four label names. Opened by poking an unlabeled drum; a wrong pick
    /// turns this same popup red (it never uses the MCQ popups).
    /// </summary>
    public sealed class LabelPickPopup : MonoBehaviour
    {
        private const float FrameScale = 1.15f;
        private static readonly Color RowFill = new Color(0.02f, 0.16f, 0.22f, 0.72f);
        private static readonly Color RowFillRed = new Color(0.3f, 0.05f, 0.05f, 0.78f);

        public static readonly string[] Names =
            { "Faded Label", "No Label", "Mismatched GHS vs SDS", "Correctly Labelled" };

        [SerializeField] private PresentableArt art;
        [SerializeField] private Vector2 sizePx = new Vector2(1000f, 720f);
        [SerializeField] private float metersPerPixel = 0.00052f;

        private CanvasGroup _group;
        private Image _frame;
        private TextMeshProUGUI _title;
        private readonly Image[] _frames = new Image[4];
        private readonly Image[] _fills = new Image[4];
        private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[4];
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
            _frame.sprite = wrong ? art.popupFrameRed : art.popupFrame;
            _title.text = wrong ? "WRONG LABEL - TRY AGAIN" : "SELECT THE CORRECT LABEL";
            _title.color = wrong ? PresentableUi.Red : PresentableUi.Cyan;
            for (var i = 0; i < _frames.Length; i++)
            {
                _frames[i].sprite = wrong ? art.answerBoxRed : art.answerBox;
                _fills[i].color = wrong ? RowFillRed : RowFill;
            }
            if (wrong)
                StartCoroutine(Shake());
        }

        private IEnumerator Shake()
        {
            var rt = (RectTransform)_frame.transform.parent;
            var basePos = rt.anchoredPosition;
            yield return PresentableFx.Tween(0.35f, t =>
            {
                rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 45f) * (1f - t) * 14f, 0f);
            }, x => x);
            rt.anchoredPosition = basePos;
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

            // The designer frame has a soft transparent margin, so it is drawn larger than the canvas.
            var frameHolder = PresentableUi.Rect("FrameHolder", root, Vector2.zero, Vector2.one);
            var frameRt = PresentableUi.Centered("Frame", frameHolder, Vector2.zero,
                new Vector2(sizePx.x / 0.788f * FrameScale, sizePx.x / 0.788f / 1.777f * FrameScale));
            _frame = PresentableUi.Img(frameRt, art.popupFrame, Color.white);

            // safe areas measured on the frame art: top arc ~0.91, title underline ~0.78, bottom arc ~0.12
            _title = PresentableUi.Text("Title", root, new Vector2(0.14f, 0.8f), new Vector2(0.82f, 0.885f),
                string.Empty, 38f, PresentableUi.Cyan, bold: true, font: font);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 24f;
            _title.fontSizeMax = 38f;

            // step05 style rows: dark blue fill + glowing box, centred white text
            const float rowH = 0.125f;
            const float gap = 0.03f;
            for (var i = 0; i < 4; i++)
            {
                var top = 0.74f - i * (rowH + gap); // last row ends at 0.15
                var slot = PresentableUi.Rect("Option" + (i + 1), root, new Vector2(0.1f, top - rowH),
                    new Vector2(0.9f, top));
                slot.localPosition += new Vector3(0f, 0f, -8f);
                _fills[i] = PresentableUi.Img(PresentableUi.Rect("Fill", slot, Vector2.zero, Vector2.one,
                    new Vector2(6f, 4f), new Vector2(-6f, -4f)), art.roundedSolid, RowFill, raycast: true);
                _frames[i] = PresentableUi.Img(PresentableUi.Rect("Frame", slot, Vector2.zero, Vector2.one), art.answerBox,
                    Color.white);
                _labels[i] = PresentableUi.Text(PresentableUi.Rect("Label", slot, new Vector2(0.05f, 0.12f),
                    new Vector2(0.95f, 0.88f)), Names[i], 34f, Color.white, bold: true, font: font);
                _labels[i].textWrappingMode = TextWrappingModes.NoWrap;
                _labels[i].enableAutoSizing = true;
                _labels[i].fontSizeMin = 22f;
                _labels[i].fontSizeMax = 34f;

                var btn = slot.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = _fills[i];
                var captured = i;
                btn.onClick.AddListener(() => OnPicked(captured));
                slot.gameObject.AddComponent<HoverLift>().Init(slot);
            }

            var close = PresentableUi.Rect("Close", root, new Vector2(0.87f, 0.8f), new Vector2(0.95f, 0.89f));
            var closeBg = PresentableUi.Img(close, art.roundedSolid, new Color(0.05f, 0.2f, 0.3f, 0.9f),
                raycast: true);
            PresentableUi.Img(PresentableUi.Rect("X", close, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f)),
                art.cross, Color.white, preserveAspect: true);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() =>
            {
                if (IsOpen)
                    Closed?.Invoke();
            });
            PresentableUi.CornerAccents(root, art.roundedSolid);

            // never hidden by rack beams or drums when the user stands close
            PresentableUi.AlwaysOnTop(canvas);
        }
    }
}
