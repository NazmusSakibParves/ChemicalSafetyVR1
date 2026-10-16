using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SDS clipboard held in the left hand. Left X button (desk: C) shows / hides it.
    /// Falls back to a head-locked position when no XR device is active.
    /// </summary>
    public sealed class SdsClipboardController : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Vector3 handLocalPosition = new Vector3(0.03f, 0.05f, 0.13f);
        [SerializeField] private Vector3 handLocalEuler = new Vector3(52f, 8f, 0f);
        [SerializeField] private bool startVisible = true;

        private InputAction _toggle;
        private Transform _clip;
        private CanvasGroup _clipGroup;
        private Transform _hint;
        private bool _visible;
        private Coroutine _anim;

        public bool IsVisible => _visible;

        private void Awake()
        {
            var parent = ResolveParent(out var deskMode);

            var clipCanvas = PresentableUi.WorldCanvas("SdsClipboard", parent, new Vector2(1024f, 1024f), 0.0003f,
                false, 300);
            _clip = clipCanvas.transform;
            _clipGroup = clipCanvas.gameObject.AddComponent<CanvasGroup>();
            var img = PresentableUi.Rect("Clipboard", _clip, Vector2.zero, Vector2.one);
            PresentableUi.Img(img, art.sdsClipboard, Color.white, preserveAspect: true);

            var hintCanvas = PresentableUi.WorldCanvas("SdsHint", parent, new Vector2(700f, 190f), 0.00022f, false, 301);
            _hint = hintCanvas.transform;
            var bg = PresentableUi.Rect("Bg", _hint, Vector2.zero, Vector2.one);
            PresentableUi.Img(bg, art.roundedPanel, new Color(0.05f, 0.2f, 0.35f, 0.9f));
            PresentableUi.BanglaLine("Bn", _hint, art.bnClipboardHint, new Vector2(0f, 30f), 70f, Color.white);
            PresentableUi.Text("En", _hint, new Vector2(0f, 0.05f), new Vector2(1f, 0.42f), "X: SDS show / hide", 46f,
                PresentableUi.Cyan, font: art.englishFont);

            if (deskMode)
            {
                _clip.localPosition = new Vector3(-0.32f, -0.2f, 0.62f);
                _clip.localRotation = Quaternion.Euler(10f, -12f, 0f);
                _hint.localPosition = new Vector3(-0.32f, -0.42f, 0.62f);
                _hint.localRotation = Quaternion.identity;
            }
            else
            {
                _clip.localPosition = handLocalPosition;
                _clip.localRotation = Quaternion.Euler(handLocalEuler);
                _hint.localPosition = new Vector3(0f, 0.045f, 0.02f);
                _hint.localRotation = Quaternion.Euler(60f, 0f, 0f);
            }

            _visible = startVisible;
            _clip.gameObject.SetActive(_visible);
            _clipGroup.alpha = _visible ? 1f : 0f;
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
            _clip.gameObject.SetActive(true);
            var basePos = _clip.localPosition;
            var from = _clipGroup.alpha;
            var to = show ? 1f : 0f;
            var baseScale = Vector3.one * 0.0003f;
            yield return PresentableFx.Tween(0.3f, t =>
            {
                var a = Mathf.Lerp(from, to, t);
                _clipGroup.alpha = a;
                _clip.localScale = baseScale * Mathf.Lerp(0.6f, 1f, a);
            }, PresentableFx.EaseOutCubic);
            _clip.localPosition = basePos;
            if (!show)
                _clip.gameObject.SetActive(false);
            _anim = null;
        }

        private Transform ResolveParent(out bool deskMode)
        {
            deskMode = !XRSettings.isDeviceActive || leftHand == null;
            if (!deskMode)
                return leftHand;
            return Camera.main != null ? Camera.main.transform : transform;
        }
    }
}
