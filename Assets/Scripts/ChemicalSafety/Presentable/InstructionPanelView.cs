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
        private RectTransform _canvasRt;
        private RectTransform _frameRt;
        private RectTransform _titleRt;
        private RectTransform _speaker;
        private Image _speakerIcon;
        private RectTransform _continue;
        private TextMeshProUGUI _english;
        private Image _bangla;
        private bool _speaking;
        private bool _built;
        private bool _compact;
        private float _baseScale = 1f;

        public event Action SpeakerClicked;
        public event Action Continue;

        private void Awake() => Build();

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            var canvas = PresentableUi.WorldCanvas("InstructionCanvas", transform, sizePx, metersPerPixel, true, 100);
            _canvasRt = (RectTransform)canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var root = canvas.transform;
            var font = art.englishFont;

            // The pop-up frame. Reflow sizes it around the words so nothing is clipped.
            _frameRt = PresentableUi.Centered("Frame", root, Vector2.zero, sizePx);
            PresentableUi.Img(_frameRt, art.popupFrame, Color.white);

            var title = PresentableUi.Text("Title", root, new Vector2(0.2f, 0.6f), new Vector2(0.8f, 0.7f),
                "INSTRUCTIONS", 40f, Color.white, bold: true, font: font);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            _titleRt = title.rectTransform;

            _speaker = PresentableUi.Rect("Speaker", root, new Vector2(0.43f, 0.46f), new Vector2(0.57f, 0.58f));
            _speaker.localPosition += new Vector3(0f, 0f, -10f);
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", _speaker, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            _speakerIcon = PresentableUi.Img(PresentableUi.Rect("Icon", _speaker, new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f)), art.speaker, Color.white, preserveAspect: true);
            var speakerBtn = _speaker.gameObject.AddComponent<Button>();
            speakerBtn.transition = Selectable.Transition.None;
            speakerBtn.targetGraphic = hit;
            speakerBtn.onClick.AddListener(() =>
            {
                PresentableFx.Haptic(true, 0.4f, 0.06f);
                SpeakerClicked?.Invoke();
            });
            _speaker.gameObject.AddComponent<HoverLift>().Init(_speaker);

            _english = PresentableUi.Text("English", root, new Vector2(0.14f, 0.32f), new Vector2(0.86f, 0.44f),
                "<color=#5FD8FF><b>VOICE-OVER:</b></color> Select the correctly labeled drum based on the " +
                "provided Safety Data Sheet (SDS).", 32f, Color.white, font: font);
            _english.textWrappingMode = TextWrappingModes.Normal;
            _english.overflowMode = TextOverflowModes.Overflow;

            var bangla = PresentableUi.Rect("Bangla", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _bangla = PresentableUi.Img(bangla, art.bnInstructionPanel, new Color(0.92f, 0.97f, 1f), preserveAspect: true);

            _continue = PresentableUi.Rect("ContinueButton", root, new Vector2(0.34f, 0.04f), new Vector2(0.66f, 0.14f));
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
            Reflow();
        }

        private void Update()
        {
            if (_speakerIcon == null)
                return;
            // Blink the icon only. Scaling the button made it look bigger in some scenes than others.
            var c = Color.white;
            c.a = _speaking ? 0.4f + Mathf.PingPong(Time.time * 1.6f, 0.6f) : 1f;
            _speakerIcon.color = c;
        }

        /// <summary>World distance from the card centre down to the bottom of the Continue button (for docking a subtitle under it).</summary>
        public float BottomExtentMeters
        {
            get
            {
                Build();
                return (_canvasRt.sizeDelta.y * 0.5f + 160f) * metersPerPixel * _baseScale;
            }
        }

        public void SetSpeaking(bool speaking) => _speaking = speaking;

        /// <summary>Continue stays centred under the words on every scenario.</summary>
        public void SetContinueCentered(bool centered)
        {
            Build();
            if (centered)
                Reflow();
        }

        /// <summary>
        /// Controller-ray scenario (Scenario 1): the same card shape and layout, scaled uniformly on every side (so the
        /// frame keeps its proportions) and shown further away. Other scenarios keep scale 1.
        /// </summary>
        public void SetCompact(bool on, float worldScale = 1.4f)
        {
            Build();
            _compact = on;
            _baseScale = on ? worldScale : 1f;
            transform.localScale = Vector3.one * _baseScale;
            Reflow();
        }

        /// <summary>Swaps the card text and grows the frame so the words stay inside.</summary>
        public void SetContent(string english, Sprite bangla)
        {
            Build();
            if (art != null && art.englishFont != null)
                _english.font = art.englishFont;
            if (!string.IsNullOrEmpty(english))
                _english.text = english;
            if (_bangla != null)
                _bangla.sprite = bangla; // null: the English text is swapped for its Bangla image by BnTmp
            Reflow();
        }

        // Controller-ray scenario: the card shrinks to its words on every side (same 32 pt text and same Bangla image size as the
        // other scenarios, even margins), keeping the frame's own proportions so the shape stays premium.
        private void ReflowCompact()
        {
            const float textW = 760f;
            _english.enableAutoSizing = false;
            _english.fontSize = 32f;
            _english.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
            var useBn = ChemicalSafety.Localization.Loc.IsBangla && _bangla.sprite != null;
            _english.gameObject.SetActive(!useBn);
            _bangla.gameObject.SetActive(useBn);
            _english.ForceMeshUpdate();
            var englishH = useBn ? 0f : Mathf.Clamp(_english.preferredHeight + 8f, 64f, 420f);

            var aspect = 6f;
            if (_bangla.sprite != null && _bangla.sprite.rect.height > 1f)
                aspect = _bangla.sprite.rect.width / _bangla.sprite.rect.height;
            var bnH = useBn ? Mathf.Min(190f, 920f / Mathf.Max(0.1f, aspect)) : 0f;
            var bnW = bnH * aspect;

            const float padTop = 64f;
            const float titleH = 56f;
            const float speakerH = 172f;
            const float speakerDrop = 28f;
            const float gap = 18f;
            const float padBot = 120f; // room under the text so it never touches the frame's lower edge
            const float sidePad = 110f;
            var contentW = useBn ? Mathf.Max(bnW, 520f) : textW;
            var cardW = Mathf.Max(720f, contentW + 2f * sidePad);
            var h = padTop + titleH + gap + speakerDrop + speakerH + gap + (useBn ? bnH : englishH) + padBot;

            _canvasRt.sizeDelta = new Vector2(cardW, h);
            _frameRt.sizeDelta = new Vector2(cardW * 1.269f, h * 1.175f); // the frame art's own ratios (as at 1300 x 790)

            var y = padTop;
            // the title keeps the frame-relative height it has in the normal card (about 17 % of the frame height from its top edge)
            Band(_titleRt, 0.116f * h, titleH, 680f);
            y += titleH + gap + speakerDrop;
            Band(_speaker, y, speakerH, speakerH);
            y += speakerH + gap;
            Band(_english.rectTransform, y, englishH, textW);
            Band(_bangla.rectTransform, y, bnH, bnW);

            _continue.anchorMin = _continue.anchorMax = new Vector2(0.5f, 0f);
            _continue.pivot = new Vector2(0.5f, 1f);
            _continue.sizeDelta = new Vector2(420f, 78f);
            var z = _continue.localPosition.z;
            _continue.anchoredPosition = new Vector2(0f, -78f);
            var p = _continue.localPosition;
            p.z = z;
            _continue.localPosition = p;
        }

        // The frame hugs the words. Continue sits under the frame, not in the glass.
        private void Reflow()
        {
            if (_compact)
            {
                ReflowCompact();
                return;
            }

            const float cardW = 1300f;
            const float textW = 920f;
            _english.enableAutoSizing = false;
            _english.fontSize = 32f;
            _english.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
            // English mode: the English words only; Bangla mode: the Bangla image only (when the scenario has one)
            var useBn = ChemicalSafety.Localization.Loc.IsBangla && _bangla.sprite != null;
            _english.gameObject.SetActive(!useBn);
            _bangla.gameObject.SetActive(useBn);
            _english.ForceMeshUpdate();
            var englishH = useBn ? 0f : Mathf.Clamp(_english.preferredHeight + 8f, 64f, 340f);

            var aspect = 6f;
            if (_bangla.sprite != null && _bangla.sprite.rect.height > 1f)
                aspect = _bangla.sprite.rect.width / _bangla.sprite.rect.height;
            var bnH = useBn ? Mathf.Min(190f, textW / Mathf.Max(0.1f, aspect)) : 0f;
            var bnW = bnH * aspect;

            const float padTop = 72f;
            const float titleH = 56f;
            const float speakerH = 172f;
            const float speakerDrop = 28f;
            const float gap = 18f;
            const float padBot = 48f;
            var h = Mathf.Max(790f, padTop + titleH + gap + speakerDrop + speakerH + gap + englishH + gap + bnH + padBot);
            if (h > 900f)
            {
                var extra = h - 900f;
                var share = extra / Mathf.Max(1f, englishH + bnH);
                englishH = useBn ? 0f : Mathf.Max(72f, englishH * (1f - share));
                bnH = useBn ? Mathf.Max(56f, bnH * (1f - share)) : 0f;
                bnW = bnH * aspect;
                h = 900f;
            }

            _canvasRt.sizeDelta = new Vector2(cardW, h);
            var frameW = cardW / 0.788f;
            _frameRt.sizeDelta = new Vector2(frameW, frameW / 1.777f * (h / 790f));

            var y = padTop;
            Band(_titleRt, y + 20f, titleH, 680f);
            y += titleH + gap + speakerDrop;
            Band(_speaker, y, speakerH, speakerH);
            y += speakerH + gap;
            Band(_english.rectTransform, y, englishH, textW);
            y += englishH + gap;
            Band(_bangla.rectTransform, y, bnH, bnW);

            _continue.anchorMin = _continue.anchorMax = new Vector2(0.5f, 0f);
            _continue.pivot = new Vector2(0.5f, 1f);
            _continue.sizeDelta = new Vector2(420f, 78f);
            var z = _continue.localPosition.z;
            _continue.anchoredPosition = new Vector2(0f, -78f);
            var p = _continue.localPosition;
            p.z = z;
            _continue.localPosition = p;
        }

        private static void Band(RectTransform rt, float fromTop, float height, float width)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            var z = rt.localPosition.z;
            rt.anchoredPosition = new Vector2(0f, -fromTop);
            var p = rt.localPosition;
            p.z = z;
            rt.localPosition = p;
        }

        public IEnumerator Show()
        {
            Build();
            Reflow(); // the card has its final size and layout from the first frame (no small text that jumps later)
            gameObject.SetActive(true);
            _continue.gameObject.SetActive(false);
            transform.localScale = Vector3.one * 0.85f * _baseScale;
            yield return PresentableFx.Tween(0.5f, t =>
            {
                _group.alpha = t;
                transform.localScale = Vector3.one * (_baseScale * Mathf.LerpUnclamped(0.85f, 1f, t));
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
                transform.localScale = Vector3.one * (_baseScale * Mathf.Lerp(1f, 0.9f, t));
            });
            gameObject.SetActive(false);
        }
    }
}
