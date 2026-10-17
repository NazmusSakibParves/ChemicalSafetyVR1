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
        private const float FrameScale = 1.1f;

        public static readonly string[] Names =
            { "Faded Label", "No Label", "Mismatched GHS vs SDS", "Correctly Labelled" };

        [SerializeField] private PresentableArt art;
        [SerializeField] private Vector2 sizePx = new Vector2(940f, 600f);
        [SerializeField] private float metersPerPixel = 0.00052f;

        private CanvasGroup _group;
        private Image _frame;
        private TextMeshProUGUI _title;
        private readonly Image[] _pills = new Image[4];
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
            for (var i = 0; i < _pills.Length; i++)
                _pills[i].sprite = wrong ? art.buttonRed : art.button;
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

            _title = PresentableUi.Text("Title", root, new Vector2(0.14f, 0.82f), new Vector2(0.86f, 0.95f),
                string.Empty, 38f, PresentableUi.Cyan, bold: true, font: font);
            _title.textWrappingMode = TextWrappingModes.NoWrap;

            const float rowH = 0.165f;
            const float gap = 0.0165f;
            for (var i = 0; i < 4; i++)
            {
                var top = 0.80f - i * (rowH + gap);
                var slot = PresentableUi.Rect("Option" + (i + 1), root, new Vector2(0.15f, top - rowH),
                    new Vector2(0.85f, top));
                slot.localPosition += new Vector3(0f, 0f, -8f);
                var hit = PresentableUi.Img(PresentableUi.Rect("Hit", slot, Vector2.zero, Vector2.one), null,
                    new Color(1f, 1f, 1f, 0.02f), raycast: true);
                _pills[i] = PresentableUi.Img(PresentableUi.Rect("Pill", slot, Vector2.zero, Vector2.one,
                    new Vector2(-88f, -54f), new Vector2(88f, 54f)), art.button, Color.white);
                _labels[i] = PresentableUi.Text(PresentableUi.Rect("Label", slot, new Vector2(0.05f, 0f),
                    new Vector2(0.95f, 1f)), Names[i], 40f, Color.white, bold: true, font: font);
                _labels[i].textWrappingMode = TextWrappingModes.NoWrap;
                _labels[i].enableAutoSizing = true;
                _labels[i].fontSizeMin = 24f;
                _labels[i].fontSizeMax = 40f;

                var btn = slot.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = hit;
                var captured = i;
                btn.onClick.AddListener(() => OnPicked(captured));
                slot.gameObject.AddComponent<HoverLift>().Init(slot);
            }

            var close = PresentableUi.Rect("Close", root, new Vector2(0.9f, 0.84f), new Vector2(0.97f, 0.97f));
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
        }
    }
}
