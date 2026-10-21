using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC03 hold-to-scan. The old drum label itself is highlighted (gold corner brackets that pulse). When the
    /// gloved fingertip comes near, the brackets tighten and a haptic tick plays; touching the label and holding it
    /// for <see cref="holdSeconds"/> fills a ring, runs a scan line down the label and ends with a chime. Lifting
    /// the finger lets the ring drain. At the desk the left mouse button held down does the same.
    /// </summary>
    public sealed class Sc03LabelScan : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [Tooltip("Centre of the drum label; its forward axis points the way the label faces (towards the user).")]
        [SerializeField] private Transform labelAnchor;
        [SerializeField] private float holdSeconds = 0.9f;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _frame;
        private Image _frameImg;
        private RectTransform _line;
        private Image _lineImg;
        private Image _ringFill;
        private Image _ringBack;
        private Image _ringGlow;
        private RectTransform _ringRt;
        private Image _glow;
        private TextMeshProUGUI _tag;
        private bool _armed;
        private bool _near;
        private bool _touching;
        private float _p;
        private float _appear;
        private float _hover;
        private float _nextHaptic;
        private bool _built;
        private bool _forceHand;

        public event Action Completed;
        public bool IsArmed => _armed;
        public float Progress => _p;

        private void Awake() => Build();

        public void Arm()
        {
            Build();
            _armed = true;
            _p = 0f;
            _near = false;
            _touching = false;
            Place();
        }

        public void Disarm() => _armed = false;

        /// <summary>Editor tests: follow the right glove even without a headset.</summary>
        public void ForceHandMode() => _forceHand = true;

        /// <summary>Editor tests: finish the scan without a finger.</summary>
        public void CompleteForTest()
        {
            if (_armed)
                Finish();
        }

        private void Place()
        {
            if (labelAnchor == null)
                return;
            _canvas.transform.SetPositionAndRotation(
                labelAnchor.position + labelAnchor.forward * 0.03f + Vector3.down * 0.03f,
                Quaternion.LookRotation(-labelAnchor.forward, Vector3.up));
        }

        private void Update()
        {
            if (_canvas == null)
                return;
            _appear = Mathf.MoveTowards(_appear, _armed ? 1f : 0f, Time.deltaTime * 3f);
            _group.alpha = Mathf.SmoothStep(0f, 1f, _appear);
            _canvas.gameObject.SetActive(_appear > 0.001f);
            if (!_armed || labelAnchor == null)
                return;

            var near = false;
            var touching = false;
            var hand = GlovedHandPose.Right;
            if ((XRSettings.isDeviceActive || _forceHand) && hand != null && hand.HasTip)
            {
                // anchor local +z points out of the label towards the user
                var local = labelAnchor.InverseTransformPoint(hand.PressPoint);
                var onLabel = Mathf.Abs(local.x) < 0.22f && Mathf.Abs(local.y) < 0.2f;
                near = onLabel && local.z < 0.18f && local.z > -0.12f;
                touching = onLabel && local.z < 0.09f && local.z > -0.12f;
            }
            else
            {
                var mouse = Mouse.current;
                touching = mouse != null && mouse.leftButton.isPressed;
                near = touching;
            }

            if (near && !_near)
                PresentableFx.Haptic(true, 0.22f, 0.04f);
            _near = near;
            _touching = touching;

            _p = Mathf.Clamp01(_p + (touching ? Time.deltaTime / holdSeconds : -Time.deltaTime * 1.4f));
            if (touching && Time.unscaledTime >= _nextHaptic)
            {
                _nextHaptic = Time.unscaledTime + 0.09f;
                PresentableFx.Haptic(true, 0.12f + 0.5f * _p, 0.05f);
            }

            Visuals();
            if (_p >= 1f)
                Finish();
        }

        private void Finish()
        {
            _armed = false;
            _p = 1f;
            PresentableFx.Play(PresentableFx.Success, 0.8f);
            PresentableFx.Haptic(true, 0.8f, 0.18f);
            Completed?.Invoke();
        }

        private void Visuals()
        {
            var t = Time.time;
            _hover = Mathf.MoveTowards(_hover, _near ? 1f : 0f, Time.deltaTime * 5f);
            var pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.2f);
            _frame.localScale = Vector3.one * Mathf.Lerp(1f + 0.03f * pulse, 0.93f, _hover);
            var c = Color.Lerp(Gold, Color.white, _hover * 0.6f);
            c.a = Mathf.Lerp(0.55f + 0.4f * pulse, 1f, _hover);
            _frameImg.color = c;
            _glow.color = new Color(Gold.r, Gold.g, Gold.b, 0.05f + 0.12f * _hover + 0.1f * _p);
            _ringFill.fillAmount = _p;
            _ringFill.color = Color.Lerp(PresentableUi.Cyan, PresentableUi.Green, _p);

            // the ring blinks and glows until the finger is on the label, so it is clear that something is to be done here
            var blink = 0.5f + 0.5f * Mathf.Sin(t * 6.5f);
            var idle = _touching ? 0f : 1f;
            _ringRt.localScale = Vector3.one * (1f + 0.2f * blink * idle);
            _ringBack.color = Color.Lerp(new Color(1f, 1f, 1f, 0.25f), Color.Lerp(Gold, Color.white, blink), idle * (0.4f + 0.6f * blink) + 0.0f);
            var bc = _ringBack.color;
            bc.a = Mathf.Lerp(0.25f, 0.35f + 0.65f * blink, idle);
            _ringBack.color = bc;
            _ringGlow.color = new Color(Gold.r, Gold.g, Gold.b, idle * (0.12f + 0.55f * blink) + _p * 0.3f);

            // scan line runs down the label while the finger is held on it
            var y = Mathf.Lerp(0.92f, 0.3f, Mathf.Repeat(t * 1.1f, 1f));
            _line.anchorMin = new Vector2(0.1f, y);
            _line.anchorMax = new Vector2(0.9f, y + 0.022f);
            _lineImg.color = new Color(PresentableUi.Cyan.r, PresentableUi.Cyan.g, PresentableUi.Cyan.b,
                _touching ? 0.95f : 0f);
            _tag.text = _touching ? "SCANNING..." : _near ? "HOLD YOUR FINGER ON THE LABEL" : "TOUCH & HOLD TO SCAN";
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;
            var font = art.englishFont;
            _canvas = PresentableUi.WorldCanvas("LabelScan", transform, new Vector2(640f, 560f), 0.0007f, false, 80);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var root = _canvas.transform;

            _glow = PresentableUi.Img("Glow", root, new Vector2(-0.05f, 0.2f), new Vector2(1.05f, 1.05f), art.radialGlow,
                new Color(Gold.r, Gold.g, Gold.b, 0.08f));
            _frame = PresentableUi.Rect("Frame", root, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.96f));
            _frameImg = PresentableUi.Img(_frame, art.scanFrame, Gold);
            _line = PresentableUi.Rect("ScanLine", root, new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.922f));
            _lineImg = PresentableUi.Img(_line, art.roundedSolid, new Color(1f, 1f, 1f, 0f));

            _ringGlow = PresentableUi.Img("RingGlow", root, new Vector2(0.33f, 0.05f), new Vector2(0.67f, 0.36f), art.radialGlow,
                new Color(Gold.r, Gold.g, Gold.b, 0.1f));
            _ringRt = PresentableUi.Rect("RingHolder", root, new Vector2(0.43f, 0.12f), new Vector2(0.57f, 0.29f));
            _ringBack = PresentableUi.Img(PresentableUi.Rect("RingBack", _ringRt, Vector2.zero, Vector2.one), art.ring,
                new Color(1f, 1f, 1f, 0.25f), preserveAspect: true);
            _ringFill = PresentableUi.Img(PresentableUi.Rect("RingFill", _ringRt, Vector2.zero, Vector2.one), art.ring,
                PresentableUi.Cyan, preserveAspect: true);
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;
            _ringFill.fillAmount = 0f;

            _tag = PresentableUi.Text("Tag", root, new Vector2(0f, 0f), new Vector2(1f, 0.12f), "TOUCH & HOLD TO SCAN", 34f,
                Color.white, TextAlignmentOptions.Center, true, font);
            _tag.enableAutoSizing = true;
            _tag.fontSizeMin = 20f;
            _tag.fontSizeMax = 34f;
            _tag.textWrappingMode = TextWrappingModes.NoWrap;
            PresentableUi.AlwaysOnTop(_canvas);
            _canvas.gameObject.SetActive(false);
        }
    }
}
