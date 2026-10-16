using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// step04-style numbered bubble + caption above a drum, plus a floor glow used for
    /// intro pulses and correct / wrong feedback.
    /// </summary>
    public sealed class DrumCallout : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private int number = 1;
        [SerializeField] private string caption = "Faded Label";
        [SerializeField] private float bubbleHeight = 1.15f;
        [Tooltip("World offset so the bubble floats in front of rack beams.")]
        [SerializeField] private Vector3 bubbleOffset = Vector3.zero;

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

            var bubble = PresentableUi.WorldCanvas("Callout", transform, new Vector2(620f, 300f), 0.00085f, false, 20);
            _bubble = bubble.transform;
            _bubble.position = transform.position + Vector3.up * bubbleHeight + bubbleOffset;
            _bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();

            var circleRt = PresentableUi.Centered("Number", _bubble, new Vector2(0f, 70f), new Vector2(140f, 140f));
            PresentableUi.Img(circleRt, art.dot, new Color(0.1f, 0.45f, 0.95f, 1f), preserveAspect: true);
            PresentableUi.Img(PresentableUi.Rect("Ring", circleRt, Vector2.zero, Vector2.one), art.circle,
                Color.white, preserveAspect: true);
            PresentableUi.Text(PresentableUi.Rect("Digit", circleRt, Vector2.zero, Vector2.one), number.ToString(), 84f,
                Color.white, bold: true, font: font);

            var capRt = PresentableUi.Centered("Caption", _bubble, new Vector2(0f, -70f), new Vector2(620f, 110f));
            PresentableUi.Img(capRt, art.answerBox, PresentableUi.Cyan);
            PresentableUi.Img(PresentableUi.Rect("Fill", capRt, Vector2.zero, Vector2.one, new Vector2(10f, 10f),
                new Vector2(-10f, -10f)), art.roundedSolid, new Color(0.03f, 0.16f, 0.32f, 0.92f));
            var capText = PresentableUi.Text(PresentableUi.Rect("Text", capRt, new Vector2(0.04f, 0f),
                new Vector2(0.96f, 1f)), caption, 46f, Color.white, bold: true, font: font);
            capText.textWrappingMode = TextWrappingModes.NoWrap;
            capText.enableAutoSizing = true;
            capText.fontSizeMin = 28f;
            capText.fontSizeMax = 46f;

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
                                   Vector3.up * (bubbleHeight + Mathf.Sin(Time.time * 1.6f + number) * 0.015f);
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

        public void Highlight(Color color, bool pulse)
        {
            _glowColor = color;
            _glowStrength = 0.95f;
            _pulse = pulse;
        }

        public void ClearHighlight()
        {
            _glowStrength = 0f;
            _pulse = false;
        }

        private IEnumerator FadeBubble(float target, float duration)
        {
            var from = _bubbleGroup.alpha;
            yield return PresentableFx.Tween(duration, t => _bubbleGroup.alpha = Mathf.Lerp(from, target, t));
        }
    }
}
