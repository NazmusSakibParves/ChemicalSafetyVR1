using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 2 (controller-ray flow) "Take Decision" card: INSPECTION to read the containers' properties, CONTINUE when the
    /// learner is ready to store them. Same hologram look as the other scenarios' gate card (Sc06Gate), which stays untouched.
    /// </summary>
    public sealed class Sc02DecisionBox : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);

        private Transform _card;
        private CanvasGroup _group;
        private Button _inspection;
        private CanvasGroup _inspectionGroup;
        private Coroutine _fade;
        private float _scale = 1f;

        public bool Visible => _card != null && _card.gameObject.activeSelf;
        public Transform Card => _card;

        public event Action Inspection;
        public event Action Continue;

        public void PressInspection()
        {
            if (Visible && _inspection != null && _inspection.interactable)
                Inspection?.Invoke();
        }

        public void PressContinue()
        {
            if (Visible)
                Continue?.Invoke();
        }

        /// <summary>Opens the card at a pose (the director puts it straight ahead of the learner). Inspection is greyed out once everything has been inspected.</summary>
        public void Show(Vector3 position, Quaternion rotation, float worldScale, bool inspectionEnabled)
        {
            Build();
            if (_card == null)
                return;
            _scale = worldScale;
            _inspection.interactable = inspectionEnabled;
            _inspectionGroup.alpha = inspectionEnabled ? 1f : 0.4f;
            _card.SetPositionAndRotation(position, rotation);
            _card.gameObject.SetActive(true);
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(PopIn());
        }

        public void Hide()
        {
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = null;
            if (_card != null)
                _card.gameObject.SetActive(false);
        }

        private IEnumerator PopIn()
        {
            var baseScale = Vector3.one * (0.00056f * _scale);
            _card.localScale = baseScale * 0.85f;
            _group.alpha = 0f;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                _card.localScale = baseScale * Mathf.Lerp(0.85f, 1f, PresentableFx.EaseOutBack(t / 0.35f));
                _group.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }

            _card.localScale = baseScale;
            _group.alpha = 1f;
            _fade = null;
        }

        private void Build()
        {
            if (_card != null || art == null || (art.gatePanel == null && art.holoPanel == null))
                return;
            var canvas = PresentableUi.WorldCanvas("Sc02Decision", transform, new Vector2(900f, 640f), 0.00056f, true, 31);
            _card = canvas.transform;
            _group = canvas.gameObject.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = canvas.gameObject.AddComponent<CanvasGroup>();
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one,
                art.gatePanel != null ? art.gatePanel : art.holoPanel, Color.white);
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = true;

            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.09f, 0.68f), new Vector2(0.91f, 0.90f),
                Sc02Content.DecisionTitle, 64f, HoloWhite, TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 64f;
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var body = PresentableUi.Text("Body", canvas.transform, new Vector2(0.09f, 0.34f), new Vector2(0.91f, 0.67f),
                Sc02Content.DecisionBody, 38f, HoloWhite, TextAlignmentOptions.Top, false, art.englishFont);
            body.enableAutoSizing = true;
            body.fontSizeMin = 24f;
            body.fontSizeMax = 38f;

            _inspection = HoloButton("InspectionButton", canvas.transform, new Vector2(0.06f, 0.06f), new Vector2(0.48f, 0.26f),
                Sc02Content.InspectionButton, () => Inspection?.Invoke(),
                art.gateButton != null ? art.gateButton : art.holoButton);
            _inspectionGroup = _inspection.gameObject.AddComponent<CanvasGroup>();
            HoloButton("ContinueButton", canvas.transform, new Vector2(0.52f, 0.06f), new Vector2(0.94f, 0.26f),
                Sc02Content.ContinueButton, () => Continue?.Invoke(),
                art.gateButtonPrimary != null ? art.gateButtonPrimary : (art.holoButtonPrimary != null ? art.holoButtonPrimary : art.holoButton));
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private Button HoloButton(string name, Transform parent, Vector2 min, Vector2 max, string label,
            UnityEngine.Events.UnityAction action, Sprite background)
        {
            var rt = PresentableUi.Rect(name, parent, min, max);
            var img = PresentableUi.Img(rt, background, Color.white, sliced: true, raycast: true);
            img.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(action);
            var lift = rt.gameObject.AddComponent<HoverLift>();
            lift.Init(rt);
            btn.onClick.AddListener(lift.Press);
            var text = PresentableUi.Text("Label", rt, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), label, 34f,
                HoloWhite, TextAlignmentOptions.Center, true, art.englishFont);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 34f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return btn;
        }
    }
}
