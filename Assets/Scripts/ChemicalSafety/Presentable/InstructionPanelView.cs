using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Floating "INSTRUCTIONS" card matching step01 / Scene01 (speaker icon, EN + BN text).
    /// </summary>
    public sealed class InstructionPanelView : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;

        private CanvasGroup _group;
        private RectTransform _speaker;
        private Image _salam;
        private bool _speaking;

        private void Awake()
        {
            var canvas = PresentableUi.WorldCanvas("InstructionCanvas", transform, new Vector2(1400f, 900f), 0.0011f,
                false, 100);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var root = canvas.transform;

            var glow = PresentableUi.Rect("Glow", root, Vector2.zero, Vector2.one, new Vector2(-60f, -60f),
                new Vector2(60f, 60f));
            PresentableUi.Img(glow, art.softGlow, new Color(0.2f, 0.6f, 1f, 0.35f));
            var fill = PresentableUi.Rect("Fill", root, Vector2.zero, Vector2.one, new Vector2(30f, 30f),
                new Vector2(-30f, -30f));
            PresentableUi.Img(fill, art.roundedPanel, new Color(0.05f, 0.25f, 0.55f, 0.75f));
            var frame = PresentableUi.Rect("Frame", root, Vector2.zero, Vector2.one);
            PresentableUi.Img(frame, art.popupFrame, PresentableUi.Cyan);

            _salam = PresentableUi.BanglaLine("Salam", root, art.bnSalam, new Vector2(0f, 330f), 96f,
                new Color(0.85f, 0.95f, 1f));
            PresentableUi.Text("Title", root, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.8f), "INSTRUCTIONS", 84f,
                Color.white, bold: true, font: art.englishFont);

            _speaker = PresentableUi.Centered("Speaker", root, new Vector2(0f, 120f), new Vector2(170f, 170f));
            PresentableUi.Img(_speaker, art.speaker, Color.white, preserveAspect: true);

            PresentableUi.Text("English", root, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.44f),
                "VOICE-OVER: Select the correctly labeled drum based on the provided Safety Data Sheet (SDS).\n" +
                "<size=70%>English subtitles available.</size>",
                44f, Color.white, font: art.englishFont);

            PresentableUi.BanglaLine("Bangla", root, art.bnInstructionPanel, new Vector2(0f, -330f), 150f,
                new Color(0.9f, 0.96f, 1f));
        }

        private void Update()
        {
            if (_speaker == null)
                return;
            var s = _speaking ? 1f + Mathf.Sin(Time.time * 9f) * 0.08f : 1f;
            _speaker.localScale = Vector3.one * s;
        }

        public void SetSpeaking(bool speaking) => _speaking = speaking;

        public void SetSalamVisible(bool visible)
        {
            if (_salam != null)
                _salam.enabled = visible;
        }

        public IEnumerator Show()
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.one * 0.85f;
            yield return PresentableFx.Tween(0.5f, t =>
            {
                _group.alpha = t;
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, t);
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
