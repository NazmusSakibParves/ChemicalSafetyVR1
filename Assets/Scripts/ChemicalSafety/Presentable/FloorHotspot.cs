using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Animated floor marker (pulsing rings, light beam, bobbing arrow, BN/EN label).
    /// Activates when the user stands on it, ray-selects it, or presses E on desk.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class FloorHotspot : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Color color = new Color(0.3f, 1f, 0.55f, 1f);
        [SerializeField] private float standRadius = 0.55f;
        [SerializeField] private string englishLabel = "Stand Here";
        [Tooltip("Off for pokeable markers (the trolley): only Activate() or a ray select triggers them.")]
        [SerializeField] private bool activateOnStand = true;
        [SerializeField] private bool showBanglaLabel = true;

        private Transform _cam;
        private XRSimpleInteractable _interactable;
        private RectTransform _ringA;
        private RectTransform _ringB;
        private RectTransform _dashed;
        private RectTransform _arrow;
        private CanvasGroup _floorGroup;
        private CanvasGroup _standGroup;
        private Transform _standCanvas;
        private bool _armed;
        private bool _activated;

        public event Action Activated;

        public bool IsArmed => _armed;

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            _interactable = GetComponent<XRSimpleInteractable>();
            _interactable.selectEntered.AddListener(_ => Activate());
            _interactable.hoverEntered.AddListener(_ => PresentableFx.Haptic(true, 0.15f, 0.04f));

            var floor = PresentableUi.WorldCanvas("FloorRings", transform, new Vector2(600f, 600f), 0.0022f, false, 10);
            floor.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _floorGroup = floor.gameObject.AddComponent<CanvasGroup>();

            var glow = PresentableUi.Rect("Glow", floor.transform, Vector2.zero, Vector2.one, new Vector2(-80f, -80f),
                new Vector2(80f, 80f));
            PresentableUi.Img(glow, art.radialGlow, WithAlpha(color, 0.45f));
            _ringA = PresentableUi.Rect("RingA", floor.transform, Vector2.zero, Vector2.one);
            PresentableUi.Img(_ringA, art.ring, color);
            _ringB = PresentableUi.Rect("RingB", floor.transform, Vector2.zero, Vector2.one);
            PresentableUi.Img(_ringB, art.ring, WithAlpha(color, 0.6f));
            _dashed = PresentableUi.Rect("Dashed", floor.transform, Vector2.zero, Vector2.one, new Vector2(70f, 70f),
                new Vector2(-70f, -70f));
            PresentableUi.Img(_dashed, art.ringDashed, WithAlpha(Color.white, 0.85f));
            var core = PresentableUi.Centered("Core", floor.transform, Vector2.zero, new Vector2(150f, 150f));
            PresentableUi.Img(core, art.dot, WithAlpha(color, 0.5f));

            var stand = PresentableUi.WorldCanvas("StandMarker", transform, new Vector2(500f, 1300f), 0.0016f, false, 11);
            _standCanvas = stand.transform;
            _standCanvas.localPosition = new Vector3(0f, 1.05f, 0f);
            _standGroup = stand.gameObject.AddComponent<CanvasGroup>();

            var beam = PresentableUi.Rect("Beam", stand.transform, new Vector2(0.3f, 0f), new Vector2(0.7f, 0.62f));
            var beamImg = PresentableUi.Img(beam, art.beam, WithAlpha(color, 0.55f));
            beamImg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _arrow = PresentableUi.Centered("Arrow", stand.transform, new Vector2(0f, 200f), new Vector2(170f, 170f));
            PresentableUi.Img(_arrow, art.arrowDown, color, preserveAspect: true);

            if (art.standBadge != null)
            {
                PresentableUi.Img(PresentableUi.Centered("Badge", stand.transform, new Vector2(0f, 470f), new Vector2(500f, 167f)),
                    art.standBadge, Color.white, preserveAspect: true);
            }
            else
            {
                if (showBanglaLabel)
                    PresentableUi.BanglaLine("LabelBn", stand.transform, art.bnStandHere, new Vector2(0f, 420f), 110f,
                        Color.white);
                PresentableUi.Text("LabelEn", stand.transform, new Vector2(0f, 0.86f), new Vector2(1f, 0.93f),
                    englishLabel, 64f, color, bold: true, font: art.englishFont);
            }

            SetVisible(false, true);
        }

        private void Update()
        {
            if (!_armed || _activated)
                return;

            var t = Time.time;
            var p1 = Mathf.Repeat(t * 0.6f, 1f);
            var p2 = Mathf.Repeat(t * 0.6f + 0.5f, 1f);
            Pulse(_ringA, p1);
            Pulse(_ringB, p2);
            _dashed.localRotation = Quaternion.Euler(0f, 0f, t * 25f);
            _arrow.anchoredPosition = new Vector2(0f, 200f + Mathf.Sin(t * 3f) * 35f);

            if (_cam != null)
            {
                var look = _cam.position - _standCanvas.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    _standCanvas.rotation = Quaternion.LookRotation(-look.normalized, Vector3.up);

                var d = _cam.position - transform.position;
                d.y = 0f;
                if (activateOnStand && d.magnitude < standRadius)
                    Activate();
            }

            var kb = Keyboard.current;
            if (activateOnStand && kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                Activate();
        }

        public void Arm()
        {
            _activated = false;
            _armed = true;
            gameObject.SetActive(true);
            StartCoroutine(FadeIn());
        }

        public void Activate()
        {
            if (!_armed || _activated)
                return;

            _activated = true;
            PresentableFx.Play(PresentableFx.Chime, 0.9f);
            PresentableFx.HapticBoth(0.5f, 0.12f);
            StartCoroutine(Collapse());
            Activated?.Invoke();
        }

        private IEnumerator FadeIn()
        {
            SetVisible(true, false);
            yield return PresentableFx.Tween(0.6f, t =>
            {
                _floorGroup.alpha = t;
                _standGroup.alpha = t;
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, t);
            }, PresentableFx.EaseOutBack);
        }

        private IEnumerator Collapse()
        {
            yield return PresentableFx.Tween(0.45f, t =>
            {
                _floorGroup.alpha = 1f - t;
                _standGroup.alpha = 1f - t;
                _floorGroup.transform.localScale = Vector3.one * 0.0022f * Mathf.Lerp(1f, 1.6f, t);
            });
            _armed = false;
            SetVisible(false, true);
            _floorGroup.transform.localScale = Vector3.one * 0.0022f;
        }

        private void SetVisible(bool visible, bool immediate)
        {
            _interactable.enabled = visible;
            if (immediate)
            {
                _floorGroup.alpha = visible ? 1f : 0f;
                _standGroup.alpha = visible ? 1f : 0f;
            }
        }

        private void Pulse(RectTransform ring, float phase)
        {
            var s = Mathf.Lerp(0.35f, 1.15f, phase);
            ring.localScale = Vector3.one * s;
            var img = ring.GetComponent<Image>();
            PresentableUi.SetAlpha(img, (1f - phase) * 0.95f);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
