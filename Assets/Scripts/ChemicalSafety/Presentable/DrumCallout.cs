using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// step04-style dark name pill ("1. Faded Label") that appears above a drum once its label has been
    /// chosen correctly, plus a floor glow for correct / wrong feedback. Hidden until revealed.
    /// </summary>
    public sealed class DrumCallout : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private int number = 1;
        [SerializeField] private string caption = "Faded Label";
        [SerializeField] private float bubbleHeight = 1.15f;
        [Tooltip("World offset so the pill floats in front of rack beams.")]
        [SerializeField] private Vector3 bubbleOffset = Vector3.zero;
        [SerializeField] private Color numberColor = Color.white;

        private static readonly Color PillFill = new Color(0.02f, 0.09f, 0.17f, 0.96f);
        private static readonly Color PillBorder = new Color(0.3f, 0.78f, 1f, 1f);

        private Transform _cam;
        private Transform _bubble;
        private CanvasGroup _bubbleGroup;
        private Image _glow;
        private Color _glowColor;
        private float _glowStrength;
        private bool _pulse;

        public void Configure(PresentableArt artRef, int index, string text)
        {
            art = artRef;
            number = index;
            caption = text;
        }

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            var font = art.englishFont;

            var width = Mathf.Max(300f, caption.Length * 25f + 120f);
            var bubble = PresentableUi.WorldCanvas("Callout", transform, new Vector2(width + 20f, 150f), 0.0009f,
                false, 20);
            _bubble = bubble.transform;
            _bubble.position = transform.position + Vector3.up * bubbleHeight + bubbleOffset;
            _bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            _bubbleGroup.alpha = 0f;

            // Tail notch (a rotated square, half hidden behind the pill body).
            var tail = PresentableUi.Centered("Tail", _bubble, new Vector2(0f, -50f), new Vector2(34f, 34f));
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            PresentableUi.Img(tail, art.roundedSolid, PillBorder);
            var tailIn = PresentableUi.Rect("Fill", tail, Vector2.zero, Vector2.one, new Vector2(4f, 4f),
                new Vector2(-4f, -4f));
            PresentableUi.Img(tailIn, art.roundedSolid, PillFill);

            var body = PresentableUi.Centered("Pill", _bubble, new Vector2(0f, 8f), new Vector2(width, 88f));
            PresentableUi.Img(body, art.roundedSolid, PillBorder);
            PresentableUi.Img(PresentableUi.Rect("Fill", body, Vector2.zero, Vector2.one, new Vector2(5f, 5f),
                new Vector2(-5f, -5f)), art.roundedSolid, PillFill);

            var hex = ColorUtility.ToHtmlStringRGB(numberColor);
            var text = PresentableUi.Text(PresentableUi.Rect("Text", body, new Vector2(0.03f, 0f),
                new Vector2(0.97f, 1f)), "<color=#" + hex + ">" + number + ".</color> " + caption, 44f, Color.white,
                bold: true, font: font);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 44f;

            var floor = PresentableUi.WorldCanvas("FloorGlow", transform, new Vector2(400f, 400f), 0.0024f, false, 5);
            floor.transform.position = new Vector3(transform.position.x, transform.position.y + 0.01f,
                transform.position.z);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _glow = PresentableUi.Img(PresentableUi.Rect("Glow", floor.transform, Vector2.zero, Vector2.one),
                art.radialGlow, new Color(0f, 0f, 0f, 0f));
        }

        private void Update()
        {
            if (_cam != null && _bubble != null)
            {
                var look = _bubble.position - _cam.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    _bubble.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                _bubble.position = transform.position + bubbleOffset +
                                   Vector3.up * (bubbleHeight + Mathf.Sin(Time.time * 1.6f + number) * 0.012f);
            }

            if (_glow != null)
            {
                var k = _pulse ? 0.55f + Mathf.Sin(Time.time * 5f) * 0.35f : 1f;
                var c = _glowColor;
                c.a = _glowStrength * k;
                _glow.color = c;
            }
        }

        public void SetCalloutVisible(bool visible, float duration = 0.4f)
        {
            StartCoroutine(FadeBubble(visible ? 1f : 0f, duration));
        }

        /// <summary>Pops the name pill in above the drum.</summary>
        public void RevealPlate() => StartCoroutine(PopPlate());

        public void Highlight(Color color, bool pulse)
        {
            _glowColor = color;
            _glowStrength = 0.95f;
            _pulse = pulse;
        }

        public void SetHover(bool on)
        {
            if (on)
                Highlight(PresentableUi.Cyan, true);
            else
                ClearHighlight();
        }

        public void ClearHighlight()
        {
            _glowStrength = 0f;
            _pulse = false;
        }

        private IEnumerator PopPlate()
        {
            yield return PresentableFx.Tween(0.4f, t =>
            {
                _bubbleGroup.alpha = Mathf.Clamp01(t * 2f);
                _bubble.localScale = Vector3.one * 0.0009f * Mathf.LerpUnclamped(0.6f, 1f, t);
            }, PresentableFx.EaseOutBack);
        }

        private IEnumerator FadeBubble(float target, float duration)
        {
            var from = _bubbleGroup.alpha;
            yield return PresentableFx.Tween(duration, t => _bubbleGroup.alpha = Mathf.Lerp(from, target, t));
        }
    }
}
