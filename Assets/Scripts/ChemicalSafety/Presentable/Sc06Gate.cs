using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 "Inspection completed" card (workflow step08): hologram panel, the question, the hint, BACK (circular arrow, left)
    /// and NEXT (check after the text, right). Used only in Scenario 6; BACK restarts the inspection, NEXT opens the MCQ.
    /// </summary>
    public sealed class Sc06Gate : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [Tooltip("Texts of another scenario's card (Scenario 4 reuses this card); empty = the Scenario 6 texts.")]
        [SerializeField] private string titleText;
        [SerializeField] private string questionText;
        [SerializeField] private string bodyText;

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color HoloCyan = new Color(0.62f, 0.95f, 1f);
        private Transform _card;
        private CanvasGroup _group;
        private Coroutine _fade;

        public bool Visible => _card != null && _card.gameObject.activeSelf;
        public Transform Card => _card;

        /// <summary>Raised when BACK is pressed (review again from the start).</summary>
        public event Action Back;

        /// <summary>Raised when NEXT is pressed (go to the multiple choice questions).</summary>
        public event Action Next;

        private void Start()
        {
            Build();
        }

        public void PressBack()
        {
            if (Visible)
                Back?.Invoke();
        }

        public void PressNext()
        {
            if (Visible)
                Next?.Invoke();
        }

        /// <summary>Opens the card about half a metre in front of the head, upright, facing the user.</summary>
        public void Show()
        {
            if (_card == null)
                Build();
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? cam.forward : Vector3.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.001f ? Vector3.forward : fwd.normalized;
            var pos = (cam != null ? cam.position : Vector3.zero) + fwd * 0.55f + Vector3.down * 0.02f;
            _card.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
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
            var baseScale = _card.localScale;
            _card.localScale = baseScale * 0.85f;
            if (_group != null)
                _group.alpha = 0f;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                var k = PresentableFx.EaseOutBack(t / 0.35f);
                _card.localScale = baseScale * Mathf.Lerp(0.85f, 1f, k);
                if (_group != null)
                    _group.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }

            _card.localScale = baseScale;
            if (_group != null)
                _group.alpha = 1f;
            _fade = null;
        }

        private void Build()
        {
            if (_card != null || art == null || (art.gatePanel == null && art.holoPanel == null))
                return;
            var canvas = PresentableUi.WorldCanvas("Sc06Gate", transform, new Vector2(900f, 640f), 0.00056f, true, 31);
            _card = canvas.transform;
            _group = canvas.gameObject.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = canvas.gameObject.AddComponent<CanvasGroup>();
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one,
                art.gatePanel != null ? art.gatePanel : art.holoPanel, Color.white);
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = true;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.09f, 0.70f), new Vector2(0.90f, 0.90f),
                string.IsNullOrEmpty(titleText) ? Sc06Content.GateTitle : titleText, 64f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 64f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var q = PresentableUi.Text("Question", canvas.transform, new Vector2(0.09f, 0.47f), new Vector2(0.91f, 0.69f),
                string.IsNullOrEmpty(questionText) ? Sc06Content.GateQuestion : questionText, 38f, HoloWhite, TextAlignmentOptions.TopLeft, false, art.englishFont);
            q.enableAutoSizing = true;
            q.fontSizeMin = 24f;
            q.fontSizeMax = 38f;
            var hint = PresentableUi.Text("Hint", canvas.transform, new Vector2(0.09f, 0.25f), new Vector2(0.91f, 0.46f),
                string.IsNullOrEmpty(bodyText) ? Sc06Content.GateBody : bodyText, 38f, HoloWhite, TextAlignmentOptions.TopLeft, false, art.englishFont);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 24f;
            hint.fontSizeMax = 38f;
            HoloButton("BackButton", canvas.transform, new Vector2(0.07f, 0.045f), new Vector2(0.47f, 0.235f), "BACK", art.iconBack, true,
                () => Back?.Invoke(), art.gateButton != null ? art.gateButton : art.holoButton);
            HoloButton("NextButton", canvas.transform, new Vector2(0.53f, 0.045f), new Vector2(0.93f, 0.235f), "NEXT", art.iconNext, false,
                () => Next?.Invoke(),
                art.gateButtonPrimary != null ? art.gateButtonPrimary : (art.holoButtonPrimary != null ? art.holoButtonPrimary : art.holoButton));
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private void HoloButton(string name, Transform parent, Vector2 min, Vector2 max, string label, Sprite icon, bool iconFirst,
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
            var iconRt = iconFirst
                ? PresentableUi.Rect("Icon", rt, new Vector2(0.07f, 0.18f), new Vector2(0.27f, 0.82f))
                : PresentableUi.Rect("Icon", rt, new Vector2(0.72f, 0.18f), new Vector2(0.92f, 0.82f));
            if (icon != null)
                PresentableUi.Img(iconRt, icon, Color.white, preserveAspect: true);
            var textMin = iconFirst ? new Vector2(0.30f, 0.08f) : new Vector2(0.08f, 0.08f);
            var textMax = iconFirst ? new Vector2(0.94f, 0.92f) : new Vector2(0.70f, 0.92f);
            var text = PresentableUi.Text("Label", rt, textMin, textMax, label, 34f, HoloWhite, TextAlignmentOptions.Center, true, art.englishFont);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 34f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }
}
