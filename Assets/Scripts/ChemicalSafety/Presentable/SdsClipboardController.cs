using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SDS clipboard held in front of the gloved left hand like a real clipboard: upright, tilted back, facing the
    /// user's head, always readable no matter how the wrist is turned (position follows the palm, orientation is
    /// derived from the head). The glove stays behind / below the board so no text is covered.
    /// Left X button (desk: C) raises / lowers it with a short animation.
    /// </summary>
    public sealed class SdsClipboardController : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Transform leftHand;
        [SerializeField] private bool startVisible = true;
        [Tooltip("Editor tests: follow the glove palm even without a headset.")]
        [SerializeField] private bool forceHandMode;

        [Header("Held pose (hand mode)")]
        [Tooltip("Board centre above the palm, towards the head, in metres (head space: x right, y up, z towards the user).")]
        [SerializeField] private Vector3 handOffset = new Vector3(0.0f, 0.15f, 0.07f);
        [SerializeField] private float tiltBack = 12f;
        [SerializeField] private float followSharpness = 14f;

        [Header("Desk pose (relative to the camera)")]
        [SerializeField] private Vector3 deskPosition = new Vector3(-0.2f, -0.17f, 0.5f);
        [SerializeField] private Vector3 deskEuler = new Vector3(-12f, 12f, 0f);

        private const float CanvasScale = 0.00032f;

        private InputAction _toggle;
        private Transform _anchor;
        private Transform _clip;
        private Image _sheet;
        private CanvasGroup _clipGroup;
        private Transform _cam;
        private bool _visible;
        private bool _handMode;
        private bool _deskAttached;
        private bool _animating;
        private float _raise = 1f; // 0 = lowered, 1 = held
        private Coroutine _anim;
        private Vector3 _pos;
        private Quaternion _rot = Quaternion.identity;
        private bool _hasPose;

        public bool IsVisible => _visible;

        /// <summary>Editor tests: switch to the glove-palm following pose without a headset.</summary>
        public void ForceHandMode()
        {
            _handMode = true;
            _hasPose = false;
            _anchor.SetParent(null, false);
        }

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            _anchor = new GameObject("SdsAnchor").transform;
            _anchor.SetParent(transform, false);

            var clipCanvas = PresentableUi.WorldCanvas("SdsClipboard", _anchor, new Vector2(1024f, 1024f), CanvasScale,
                false, 300);
            _clip = clipCanvas.transform;
            _clipGroup = clipCanvas.gameObject.AddComponent<CanvasGroup>();
            var img = PresentableUi.Rect("Clipboard", _clip, Vector2.zero, Vector2.one);
            _sheet = PresentableUi.Img(img, art.sdsClipboard, Color.white, preserveAspect: true);

            _handMode = (XRSettings.isDeviceActive && leftHand != null) || forceHandMode;
            _visible = startVisible;
            _raise = _visible ? 1f : 0f;
            _clip.gameObject.SetActive(_visible);
            _clipGroup.alpha = _visible ? 1f : 0f;
            if (_handMode)
                _anchor.SetParent(null, false);
        }

        private void OnEnable()
        {
            _toggle = new InputAction("ToggleSds", InputActionType.Button);
            _toggle.AddBinding("<XRController>{LeftHand}/primaryButton");
            _toggle.AddBinding("<Keyboard>/c");
            _toggle.performed += _ => Toggle();
            _toggle.Enable();
        }

        private void OnDisable()
        {
            _toggle?.Dispose();
        }

        private void LateUpdate()
        {
            if (_cam == null)
                return;

            if (!_handMode)
            {
                if (!_deskAttached)
                {
                    _anchor.SetParent(_cam, false);
                    _deskAttached = true;
                }

                // desk: head-locked, same upright pose
                _anchor.localPosition = deskPosition;
                _anchor.localRotation = Quaternion.Euler(deskEuler);
                ApplyLocal();
                return;
            }

            var hand = GlovedHandPose.Left;
            if (hand == null || hand.Palm == null)
                return;

            // head-space basis: board faces the head, upright
            var toHand = hand.Palm.position - _cam.position;
            var flat = new Vector3(toHand.x, 0f, toHand.z);
            if (flat.sqrMagnitude < 0.0004f)
                flat = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
            flat.Normalize();
            var yaw = Quaternion.LookRotation(flat, Vector3.up);
            var right = yaw * Vector3.right;
            var towardUser = -flat;

            var target = hand.Palm.position + Vector3.up * handOffset.y + right * handOffset.x + towardUser * handOffset.z;
            var rot = yaw * Quaternion.Euler(tiltBack, 0f, 0f);
            if (!_hasPose)
            {
                _pos = target;
                _rot = rot;
                _hasPose = true;
            }

            var k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            _pos = Vector3.Lerp(_pos, target, k);
            _rot = Quaternion.Slerp(_rot, rot, k);
            _anchor.SetPositionAndRotation(_pos, _rot);
            ApplyLocal();
        }

        // raise factor (0..1): board rises from below the hand and straightens up; plus a tiny breathing sway
        private void ApplyLocal()
        {
            var lowPos = new Vector3(0f, -0.16f, -0.04f);
            var lowRot = Quaternion.Euler(55f, 0f, 0f);
            var sway = Quaternion.identity;
            var swayPos = Vector3.zero;
            if (_visible && !_animating)
            {
                var t = Time.time;
                sway = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 0.6f, Mathf.Sin(t * 0.9f) * 0.5f, 0f);
                swayPos = new Vector3(0f, Mathf.Sin(t * 1.7f) * 0.002f, 0f);
            }

            _clip.localPosition = Vector3.LerpUnclamped(lowPos, Vector3.zero, _raise) + swayPos;
            _clip.localRotation = Quaternion.SlerpUnclamped(lowRot, Quaternion.identity, _raise) * sway;
            _clip.localScale = Vector3.one * CanvasScale * Mathf.LerpUnclamped(0.7f, 1f, _raise);
        }

        /// <summary>Scene 2 uses a different sheet. The X button still shows and hides it.</summary>
        public void SetSheet(Sprite sprite)
        {
            if (_sheet != null && sprite != null)
                _sheet.sprite = sprite;
        }

        public void Toggle() => SetVisible(!_visible);

        public void SetVisible(bool visible)
        {
            if (visible == _visible)
                return;

            _visible = visible;
            PresentableFx.Play(PresentableFx.Whoosh, 0.5f);
            PresentableFx.Haptic(false, 0.3f, 0.06f);
            if (_anim != null)
                StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(visible));
        }

        private IEnumerator Animate(bool show)
        {
            _animating = true;
            _clip.gameObject.SetActive(true);
            var from = _raise;
            var fromA = _clipGroup.alpha;
            var toA = show ? 1f : 0f;
            yield return PresentableFx.Tween(show ? 0.45f : 0.28f, t =>
            {
                _raise = Mathf.LerpUnclamped(from, show ? 1f : 0f, t);
                _clipGroup.alpha = Mathf.Lerp(fromA, toA, Mathf.Clamp01(t * 1.6f));
            }, show ? PresentableFx.EaseOutBack : PresentableFx.EaseOutCubic);
            _raise = show ? 1f : 0f;
            if (!show)
                _clip.gameObject.SetActive(false);
            _animating = false;
            _anim = null;
        }
    }
}
