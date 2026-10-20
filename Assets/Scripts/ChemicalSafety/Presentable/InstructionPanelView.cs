using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// "INSTRUCTIONS" card in the cyan pop-up frame: clickable speaker (replays the voice-over),
    /// EN + BN text filling the box and a Continue button that appears once the voice-over has finished.
    /// Pokeable like the MCQ panel.
    /// </summary>
    public sealed class InstructionPanelView : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Vector2 sizePx = new Vector2(1300f, 790f);
        [SerializeField] private float metersPerPixel = 0.0006f;

        private CanvasGroup _group;
        private RectTransform _speaker;
        private RectTransform _continue;
        private TextMeshProUGUI _english;
        private Image _bangla;
        private bool _speaking;
        private bool _built;

        public event Action SpeakerClicked;
        public event Action Continue;

        private void Awake() => Build();

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            var canvas = PresentableUi.WorldCanvas("InstructionCanvas", transform, sizePx, metersPerPixel, true, 100);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var root = canvas.transform;
            var font = art.englishFont;

            // The designer frame has a soft transparent margin, so it is drawn larger than the canvas.
            var frame = PresentableUi.Centered("Frame", root, Vector2.zero,
                new Vector2(sizePx.x / 0.788f, sizePx.x / 0.788f / 1.777f));
            PresentableUi.Img(frame, art.popupFrame, Color.white);

            var title = PresentableUi.Text("Title", root, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.94f),
                "INSTRUCTIONS", 66f, Color.white, bold: true, font: font);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            // Speaker: the designer art has a wide transparent margin, so the image is larger than its hit area.
            _speaker = PresentableUi.Rect("Speaker", root, new Vector2(0.435f, 0.545f), new Vector2(0.565f, 0.795f));
            _speaker.localPosition += new Vector3(0f, 0f, -10f);
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", _speaker, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Icon", _speaker, Vector2.zero, Vector2.one,
                new Vector2(-95f, -75f), new Vector2(95f, 75f)), art.speaker, Color.white, preserveAspect: true);
            var speakerBtn = _speaker.gameObject.AddComponent<Button>();
            speakerBtn.transition = Selectable.Transition.None;
            speakerBtn.targetGraphic = hit;
            speakerBtn.onClick.AddListener(() =>
            {
                PresentableFx.Haptic(true, 0.4f, 0.06f);
                SpeakerClicked?.Invoke();
            });
            _speaker.gameObject.AddComponent<HoverLift>().Init(_speaker);

            _english = PresentableUi.Text("English", root, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.55f),
                "<color=#5FD8FF><b>VOICE-OVER:</b></color> Select the correctly labeled drum based on the " +
                "provided Safety Data Sheet (SDS).", 46f, Color.white, font: font);
            _english.enableAutoSizing = true;
            _english.fontSizeMin = 28f;
            _english.fontSizeMax = 50f;

            var bn = art.bnInstructionPanel;
            var aspect = bn != null ? bn.rect.width / Mathf.Max(1f, bn.rect.height) : 6f;
            var bnH = Mathf.Min(190f, 1060f / aspect);
            var bangla = PresentableUi.Centered("Bangla", root, new Vector2(0f, -sizePx.y * 0.275f),
                new Vector2(bnH * aspect, bnH));
            _bangla = PresentableUi.Img(bangla, bn, new Color(0.92f, 0.97f, 1f), preserveAspect: true);

            // Continue (button.png): appears below the frame, bottom right, once the voice-over is done.
            _continue = PresentableUi.Rect("ContinueButton", root, new Vector2(0.6f, -0.15f), new Vector2(0.92f, -0.03f));
            _continue.localPosition += new Vector3(0f, 0f, -20f);
            _continue.gameObject.AddComponent<CanvasGroup>();
            var contHit = PresentableUi.Img(PresentableUi.Rect("Hit", _continue, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Pill", _continue, Vector2.zero, Vector2.one,
                new Vector2(-80f, -52f), new Vector2(80f, 52f)), art.button, Color.white);
            PresentableUi.Text("Label", _continue, Vector2.zero, Vector2.one, "Continue", 46f, Color.white,
                bold: true, font: font);
            var btn = _continue.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = contHit;
            btn.onClick.AddListener(() =>
            {
                PresentableFx.Play(PresentableFx.Tick);
                PresentableFx.Haptic(true, 0.4f, 0.06f);
                Continue?.Invoke();
            });
            _continue.gameObject.AddComponent<HoverLift>().Init(_continue);
            _continue.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_speaker == null)
                return;
            var s = _speaking ? 1f + Mathf.Sin(Time.time * 9f) * 0.07f : 1f;
            _speaker.localScale = Vector3.one * s;
        }

        public void SetSpeaking(bool speaking) => _speaking = speaking;

        /// <summary>SC03: the Continue button sits under the middle of the card instead of the bottom right.</summary>
        public void SetContinueCentered(bool centered)
        {
            Build();
            _continue.anchorMin = new Vector2(centered ? 0.34f : 0.6f, -0.15f);
            _continue.anchorMax = new Vector2(centered ? 0.66f : 0.92f, -0.03f);
        }

        /// <summary>Swaps the card text. Scene 1 keeps the defaults built above.</summary>
        public void SetContent(string english, Sprite bangla)
        {
            Build();
            if (!string.IsNullOrEmpty(english))
                _english.text = english;
            if (bangla == null || _bangla == null)
                return;
            _bangla.sprite = bangla;
            var aspect = bangla.rect.width / Mathf.Max(1f, bangla.rect.height);
            var h = Mathf.Min(190f, 1060f / aspect);
            _bangla.rectTransform.sizeDelta = new Vector2(h * aspect, h);
        }

        public IEnumerator Show()
        {
            Build();
            gameObject.SetActive(true);
            _continue.gameObject.SetActive(false);
            transform.localScale = Vector3.one * 0.85f;
            yield return PresentableFx.Tween(0.5f, t =>
            {
                _group.alpha = t;
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, t);
            }, PresentableFx.EaseOutBack);
        }

        public IEnumerator ShowContinue()
        {
            var g = _continue.GetComponent<CanvasGroup>();
            g.alpha = 0f;
            _continue.gameObject.SetActive(true);
            yield return PresentableFx.Tween(0.4f, t =>
            {
                g.alpha = Mathf.Clamp01(t * 1.5f);
                _continue.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, t);
            }, PresentableFx.EaseOutBack);
        }

        public IEnumerator Hide()
        {
            yield return PresentableFx.Tween(0.35f, t =>
            {
                _group.alpha = 1f - t;
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, t);
            });
            gameObject.SetActive(false);
        }
    }
}
