using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SDS clipboard held upright in the gloved left hand (anchored to the palm bone so it follows the hand,
    /// bottom edge in the fingers, all text clear of the glove). Left X button (desk: C) raises / lowers it
    /// with a short animation. Falls back to a head-locked, equally upright pose when no headset is active.
    /// </summary>
    public sealed class SdsClipboardController : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Transform leftHand;
        [SerializeField] private bool startVisible = true;
        [Tooltip("Editor tests: attach to the glove palm even without a headset.")]
        [SerializeField] private bool forceHandMode;

        // Upright board in front of (toward the viewer from) the fist, tilted 10 deg back: the glove stays behind
        // the board so no SDS text is ever covered, with the bottom wood strip resting in the fingers.
        [Header("Held pose (relative to the L_Palm bone)")]
        [SerializeField] private Vector3 palmPosition = new Vector3(0f, 0.14f, -0.058f);
        [SerializeField] private Vector3 palmEuler = new Vector3(10f, 0f, 0f);

        [Header("Desk pose (relative to the camera)")]
        [SerializeField] private Vector3 deskPosition = new Vector3(-0.2f, -0.17f, 0.5f);
        [SerializeField] private Vector3 deskEuler = new Vector3(-12f, 12f, 0f);

        private const float CanvasScale = 0.00032f;

        private InputAction _toggle;
        private Transform _anchor;
        private Transform _clip;
        private CanvasGroup _clipGroup;
        private bool _visible;
        private bool _handMode;
        private bool _attached;
        private bool _animating;
        private Coroutine _anim;
        private Vector3 _heldPos;
        private Quaternion _heldRot;

        public bool IsVisible => _visible;

        /// <summary>Editor tests: switch to the glove-palm anchored pose without a headset.</summary>
        public void ForceHandMode()
        {
            _handMode = true;
            _attached = false;
        }

        private void Awake()
        {
            _anchor = new GameObject("SdsAnchor").transform;
            _anchor.SetParent(transform, false);

            var clipCanvas = PresentableUi.WorldCanvas("SdsClipboard", _anchor, new Vector2(1024f, 1024f), CanvasScale,
                false, 300);
            _clip = clipCanvas.transform;
            _clipGroup = clipCanvas.gameObject.AddComponent<CanvasGroup>();
            var img = PresentableUi.Rect("Clipboard", _clip, Vector2.zero, Vector2.one);
            PresentableUi.Img(img, art.sdsClipboard, Color.white, preserveAspect: true);

            _handMode = (XRSettings.isDeviceActive && leftHand != null) || forceHandMode;
            _visible = startVisible;
            _clip.gameObject.SetActive(_visible);
            _clipGroup.alpha = _visible ? 1f : 0f;
            if (!_handMode)
                AttachToCamera();
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

        private void AttachToCamera()
        {
            var cam = Camera.main;
            _anchor.SetParent(cam != null ? cam.transform : transform, false);
            _heldPos = deskPosition;
            _heldRot = Quaternion.Euler(deskEuler);
            _attached = true;
            ApplyHeld();
        }

        private void TryAttachToHand()
        {
            var hand = GlovedHandPose.Left;
            if (hand == null || hand.Palm == null)
                return;
            _anchor.SetParent(hand.Palm, false);
            _heldPos = palmPosition;
            _heldRot = Quaternion.Euler(palmEuler);
            _attached = true;
            ApplyHeld();
        }

        private void ApplyHeld()
        {
            _anchor.localPosition = _heldPos;
            _anchor.localRotation = _heldRot;
            _clip.localPosition = Vector3.zero;
            _clip.localRotation = Quaternion.identity;
            _clip.localScale = Vector3.one * CanvasScale;
        }

        private void Update()
        {
            if (!_attached)
            {
                if (_handMode)
                    TryAttachToHand();
                return;
            }

            if (_visible && !_animating)
            {
                // barely-there breathing sway so the board feels held, not pinned
                var t = Time.time;
                _clip.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 0.6f, Mathf.Sin(t * 0.9f) * 0.5f, 0f);
                _clip.localPosition = new Vector3(0f, Mathf.Sin(t * 1.7f) * 0.002f, 0f);
            }
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

        // Raises the board from below the hand (flat-ish, small) to the held pose, or lowers it again.
        private IEnumerator Animate(bool show)
        {
            _animating = true;
            _clip.gameObject.SetActive(true);
            var lowPos = new Vector3(0f, -0.16f, -0.04f);
            var lowRot = Quaternion.Euler(55f, 0f, 0f);
            var fromA = _clipGroup.alpha;
            var toA = show ? 1f : 0f;
            yield return PresentableFx.Tween(show ? 0.4f : 0.28f, t =>
            {
                var k = show ? t : 1f - t;
                _clip.localPosition = Vector3.LerpUnclamped(lowPos, Vector3.zero, k);
                _clip.localRotation = Quaternion.SlerpUnclamped(lowRot, Quaternion.identity, k);
                _clip.localScale = Vector3.one * CanvasScale * Mathf.LerpUnclamped(0.7f, 1f, k);
                _clipGroup.alpha = Mathf.Lerp(fromA, toA, Mathf.Clamp01(t * 1.6f));
            }, show ? PresentableFx.EaseOutBack : PresentableFx.EaseOutCubic);
            if (!show)
                _clip.gameObject.SetActive(false);
            _animating = false;
            _anim = null;
        }
    }
}
