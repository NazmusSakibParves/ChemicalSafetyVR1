using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Glowing "Touch" disc floating in front of a drum (or the drum carrier) at chest height.
    /// The gloved fingertip hovers (disc grows, light haptic, optional glow callback) and presses it
    /// by touching it - no need to reach into the prop. Desk: left mouse click.
    /// </summary>
    public sealed class FloatingTouchButton : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Color color = new Color(0.3f, 0.9f, 1f, 1f);
        [SerializeField] private float hoverRadius = 0.17f;
        [SerializeField] private float pressRadius = 0.1f;
        [SerializeField] private string label = string.Empty;
        [Tooltip("World offset of the fingertip touch point from the arrow (toward the user).")]
        [SerializeField] private Vector3 touchOffset = Vector3.zero;

        private Transform _cam;
        private Transform _canvasT;
        private CanvasGroup _group;
        private RectTransform _ring;
        private RectTransform _pulse;
        private Image _glow;
        private Image _icon;
        private SphereCollider _collider;
        private bool _armed;
        private bool _hover;
        private bool _pressed;
        private float _scale = 1f;
        private float _appear;
        private Vector3 _basePos;
        private float _phase;
        private bool _pulseHint;
        private bool _flashing;
        private RectTransform _iconRt;

        public event Action<FloatingTouchButton> Pressed;
        public event Action<bool> HoverChanged;

        public bool IsArmed => _armed;

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            _basePos = transform.position;
            _phase = UnityEngine.Random.value * 6f;

            var canvas = PresentableUi.WorldCanvas("TouchCanvas", transform, new Vector2(320f, 380f), 0.0007f, false, 40);
            _canvasT = canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // floating arrow marker: soft glow behind, an expanding glow pulse, the arrow itself on top
            _glow = PresentableUi.Img(PresentableUi.Rect("Glow", _canvasT, new Vector2(0.0f, 0.2f), new Vector2(1f, 1f)),
                art.radialGlow, WithAlpha(color, 0.4f));
            _pulse = PresentableUi.Rect("Pulse", _canvasT, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.95f));
            PresentableUi.Img(_pulse, art.radialGlow, WithAlpha(color, 0.6f));
            _ring = PresentableUi.Rect("Arrow", _canvasT, new Vector2(0.2f, 0.26f), new Vector2(0.8f, 0.96f));
            _iconRt = PresentableUi.Rect("Icon", _ring, Vector2.zero, Vector2.one);
            _icon = PresentableUi.Img(_iconRt, art.iconTouch, Color.white, preserveAspect: true);

            if (!string.IsNullOrEmpty(label))
            {
                PresentableUi.Text("Label", _canvasT, new Vector2(0f, 0.0f), new Vector2(1f, 0.16f), label, 44f, Color.white,
                    bold: true, font: art.englishFont);
            }

            PresentableUi.AlwaysOnTop(canvas);

            _collider = gameObject.AddComponent<SphereCollider>();
            _collider.isTrigger = true;
            _collider.radius = 0.12f;
            _collider.enabled = false;
            gameObject.SetActive(true);
        }

        /// <summary>Fade the button in and make it touchable.</summary>
        public void Arm(bool pulseHint = false)
        {
            _armed = true;
            _pressed = false;
            _pulseHint = pulseHint;
            _collider.enabled = true;
        }

        public void Disarm()
        {
            _armed = false;
            _collider.enabled = false;
            SetHover(false);
        }

        private void Update()
        {
            // fade in / out
            var target = _armed ? 1f : 0f;
            _appear = Mathf.MoveTowards(_appear, target, Time.deltaTime * 3.2f);
            _group.alpha = Mathf.SmoothStep(0f, 1f, _appear);
            _canvasT.gameObject.SetActive(_appear > 0.001f);
            if (_appear <= 0.001f)
                return;

            // billboard + gentle bob
            var t = Time.time + _phase;
            transform.position = _basePos + Vector3.up * (Mathf.Sin(t * 1.8f) * 0.012f);
            if (_cam != null)
            {
                var look = transform.position - _cam.position;
                look.y *= 0.3f;
                if (look.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            // pulse ring
            var p = Mathf.Repeat(t * 0.9f, 1f);
            _pulse.localScale = Vector3.one * Mathf.Lerp(0.8f, _pulseHint ? 1.25f : 1.15f, p);
            PresentableUi.SetAlpha(_pulse.GetComponent<Image>(), (1f - p) * (_pulseHint ? 0.7f : 0.45f));

            if (!_flashing)
            {
                // blinking "touch here" arrow with a tiny downward bob
                var blink = 0.5f + 0.5f * Mathf.Sin(t * 8.2f);
                _icon.color = WithAlpha(Color.white, Mathf.Lerp(0.62f, 1f, blink));
                _iconRt.anchoredPosition = new Vector2(0f, 14f * (1f - blink));
            }

            var targetScale = _pressed ? 0.88f : _hover ? 1.18f : 1f;
            _scale = Mathf.Lerp(_scale, targetScale, 1f - Mathf.Exp(-16f * Time.deltaTime));
            _ring.localScale = Vector3.one * _scale;
            _glow.color = WithAlpha(color, _hover ? 0.85f : 0.4f);

            if (!_armed)
                return;

            var hand = GlovedHandPose.Right;
            if (hand != null && hand.HasTip)
            {
                var d = Vector3.Distance(hand.TipPosition, transform.position + touchOffset);
                SetHover(d < hoverRadius);
                if (d < pressRadius && !_pressed)
                    Press(true);
                else if (d > hoverRadius * 1.25f)
                    _pressed = false;
            }

            var mouse = Mouse.current;
            if (!XRSettings.isDeviceActive && mouse != null && mouse.leftButton.wasPressedThisFrame && _cam != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                    if (_collider.Raycast(ray, out _, 30f))
                        Press(false);
                }
            }
        }

        private void SetHover(bool hover)
        {
            if (hover == _hover)
                return;
            _hover = hover;
            if (hover)
            {
                PresentableFx.Haptic(true, 0.2f, 0.04f);
                PresentableFx.Play(PresentableFx.Tick, 0.35f);
            }

            HoverChanged?.Invoke(hover);
        }

        private void Press(bool vr)
        {
            _pressed = true;
            if (vr)
            {
                var hand = GlovedHandPose.Right;
                if (hand != null)
                    hand.PlayPokeCurl();
                PresentableFx.Haptic(true, 0.55f, 0.08f);
            }

            PresentableFx.Play(PresentableFx.Chime, 0.6f);
            StartCoroutine(Flash());
            Pressed?.Invoke(this);
        }

        private IEnumerator Flash()
        {
            _flashing = true;
            yield return PresentableFx.Tween(0.25f, t =>
            {
                _icon.color = Color.white;
                _glow.color = WithAlpha(Color.white, Mathf.Lerp(1f, 0.4f, t));
            });
            _flashing = false;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
