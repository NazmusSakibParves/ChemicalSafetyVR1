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
        private TextMeshProUGUI _title;
        private Image _frameImg;
        private Image _glassImg;
        private Image _glassBack;
        private Image _pillBack;
        private bool _clear;
        private const float ClearBackAlpha = 0.90f;

        [Tooltip("Glass card: opacity of the dark backing behind the translucent glass (0 = the old see-through card, 1 = solid). Higher = the words and the box stay clear over any scene, also for weak eyes.")]
        [SerializeField, Range(0f, 1f)] private float glassBackAlpha = 0.62f;
        private Image _pill;
        private RectTransform _pillRt;
        private TextMeshProUGUI _contLabel;
        private bool _glass;
        private RectTransform _divHeader;
        private RectTransform _divFooter;
        private RectTransform _cap;
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
            _frameImg = PresentableUi.Img(_frameRt, art.popupFrame, Color.white);

            // Scenario 1 glass card (client reference): translucent teal glass with a glowing aqua border, off until SetGlass(true)
            // a dark backing under the glass keeps the words and the card clear over a bright or busy scene (client: too transparent)
            var backRt = PresentableUi.Rect("GlassBack", root, Vector2.zero, Vector2.one);
            _glassBack = PresentableUi.Img(backRt, art.glassPanel, new Color(0.02f, 0.08f, 0.11f, glassBackAlpha), sliced: true);
            _glassBack.gameObject.SetActive(false);
            var glassRt = PresentableUi.Rect("Glass", root, Vector2.zero, Vector2.one);
            _glassImg = PresentableUi.Img(glassRt, art.glassPanel, Color.white, sliced: true);
            _glassImg.gameObject.SetActive(false);
            _divHeader = GlassLine("HeaderLine", root);   // under the title (the reference's header part)
            _divFooter = GlassLine("FooterLine", root);   // under the words
            _cap = GlassLine("TopCap", root);             // bright accent on the top edge, over the title

            var title = PresentableUi.Text("Title", root, new Vector2(0.2f, 0.6f), new Vector2(0.8f, 0.7f),
                "INSTRUCTIONS", 40f, Color.white, bold: true, font: font);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            _titleRt = title.rectTransform;
            _title = title;

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
            var pillBackRt = PresentableUi.Rect("PillBack", _continue, Vector2.zero, Vector2.one);
            _pillBack = PresentableUi.Img(pillBackRt, art.glassButton, new Color(0.02f, 0.08f, 0.11f, glassBackAlpha), sliced: true);
            _pillBack.gameObject.SetActive(false);
            _pillRt = PresentableUi.Rect("Pill", _continue, Vector2.zero, Vector2.one, new Vector2(-80f, -52f), new Vector2(80f, 52f));
            _pill = PresentableUi.Img(_pillRt, art.button, Color.white);
            _contLabel = PresentableUi.Text("Label", _continue, Vector2.zero, Vector2.one, "Continue", 46f, Color.white,
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

        private RectTransform GlassLine(string name, Transform root)
        {
            var rt = PresentableUi.Rect(name, root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            PresentableUi.Img(rt, art.glassDivider, Color.white);
            rt.gameObject.SetActive(false);
            return rt;
        }

        /// <summary>
        /// Scenario 1 look (client reference picture): translucent teal glass card, title centred, speaker in the top right corner,
        /// words centred, and the Continue button in the same glass style under the card. Every other scenario keeps the old frame.
        /// </summary>
        public void SetGlass(bool on)
        {
            Build();
            on &= art != null && art.glassPanel != null && art.glassButton != null;
            if (!on && _clear)
            {
                _clear = false;
                ApplyBacking();
            }

            if (_glass == on)
                return;
            _glass = on;
            _frameImg.gameObject.SetActive(!on);
            _glassImg.gameObject.SetActive(on);
            _glassBack.gameObject.SetActive(on);
            _pillBack.gameObject.SetActive(on);
            _title.color = on ? new Color(0.72f, 0.96f, 1f) : Color.white;
            _title.fontSize = on ? 52f : 40f;
            _title.characterSpacing = on ? 6f : 0f;
            _divHeader.gameObject.SetActive(on);
            _divFooter.gameObject.SetActive(on);
            _cap.gameObject.SetActive(on);
            _english.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            _contLabel.text = on ? "CONTINUE" : "Continue";
            _pill.sprite = on ? art.glassButton : art.button;
            _pill.type = on ? Image.Type.Sliced : Image.Type.Simple;
            _pillRt.offsetMin = on ? Vector2.zero : new Vector2(-80f, -52f);
            _pillRt.offsetMax = on ? Vector2.zero : new Vector2(80f, 52f);
            _contLabel.fontSize = on ? 44f : 46f;
            Reflow();
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

        /// <summary>
        /// Clear look of the glass card (Scenarios 6, 8, 9: it sits over a bright poster): the dark backing is almost solid (0.90) and the words one
        /// size larger (48). Reset by SetGlass(false).
        /// </summary>
        public void SetClearLook(bool on)
        {
            Build();
            if (_clear == on)
                return;
            _clear = on;
            ApplyBacking();
            if (_glass)
                Reflow();
        }

        private void ApplyBacking()
        {
            var a = _clear ? ClearBackAlpha : glassBackAlpha;
            if (_glassBack != null)
                _glassBack.color = new Color(0.02f, 0.08f, 0.11f, a);
            if (_pillBack != null)
                _pillBack.color = new Color(0.02f, 0.08f, 0.11f, a);
        }

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
            if (_glass)
            {
                ReflowGlass();
                return;
            }

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

        // Glass card: title centred at the top, speaker in the top right corner, the words centred below, Continue under the card.
        private void ReflowGlass()
        {
            // (client: the box a little bigger and the words one size larger: card 1000 -> 1100, words 840 -> 940 wide, 40 -> 44 pt)
            const float cardW = 1100f;
            const float textW = 940f;
            _english.enableAutoSizing = false;
            _english.fontSize = _clear ? 48f : 44f;
            _english.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
            var useBn = ChemicalSafety.Localization.Loc.IsBangla && _bangla.sprite != null;
            _english.gameObject.SetActive(!useBn);
            _bangla.gameObject.SetActive(useBn);
            _english.ForceMeshUpdate();
            var englishH = useBn ? 0f : Mathf.Clamp(_english.preferredHeight + 8f, 64f, 420f);

            var aspect = 6f;
            if (_bangla.sprite != null && _bangla.sprite.rect.height > 1f)
                aspect = _bangla.sprite.rect.width / _bangla.sprite.rect.height;
            var bnH = useBn ? Mathf.Min(200f, textW / Mathf.Max(0.1f, aspect)) : 0f;
            var bnW = bnH * aspect;

            const float padTop = 40f;
            const float titleH = 60f;
            const float lineH = 22f;
            const float headerLineY = padTop + titleH + 8f;      // underline of the heading
            const float bodyTop = headerLineY + lineH + 6f;
            const float footBand = 74f;                          // footer line + the margin under it
            const float bodyPad = 30f;
            var body = useBn ? bnH : englishH;
            var h = Mathf.Max(430f, bodyTop + bodyPad + body + bodyPad + footBand);
            _canvasRt.sizeDelta = new Vector2(cardW, h);

            Band(_titleRt, padTop, titleH, 720f);
            Band(_cap, 3f, 26f, 420f);
            Band(_divHeader, headerLineY, lineH, 980f);
            Band(_divFooter, h - footBand + 6f, lineH, 980f);
            var speakerZ = _speaker.localPosition.z;
            _speaker.anchorMin = _speaker.anchorMax = new Vector2(1f, 1f);
            _speaker.pivot = new Vector2(1f, 1f);
            _speaker.sizeDelta = new Vector2(92f, 92f);
            _speaker.anchoredPosition = new Vector2(-30f, -(padTop + (titleH - 92f) * 0.5f));
            var sp = _speaker.localPosition;
            sp.z = speakerZ;
            _speaker.localPosition = sp;

            // the words sit exactly in the middle between the two lines
            var areaTop = bodyTop;
            var areaBottom = h - footBand + 6f;
            var y = areaTop + (areaBottom - areaTop - body) * 0.5f;
            Band(_english.rectTransform, y, englishH, textW);
            Band(_bangla.rectTransform, y, bnH, bnW);

            _continue.anchorMin = _continue.anchorMax = new Vector2(0.5f, 0f);
            _continue.pivot = new Vector2(0.5f, 1f);
            _continue.sizeDelta = new Vector2(330f, 90f);
            var z = _continue.localPosition.z;
            _continue.anchoredPosition = new Vector2(0f, -28f);
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
            // glass card (Scenario 1): fades in almost in place, no bounce, so it never seems to come closer and then go away
            var from = _glass ? 0.97f : 0.85f;
            transform.localScale = Vector3.one * from * _baseScale;
            yield return PresentableFx.Tween(_glass ? 0.45f : 0.5f, t =>
            {
                _group.alpha = t;
                transform.localScale = Vector3.one * (_baseScale * Mathf.LerpUnclamped(from, 1f, t));
            }, _glass ? PresentableFx.EaseOutCubic : PresentableFx.EaseOutBack);
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
                transform.localScale = Vector3.one * (_baseScale * Mathf.Lerp(1f, _glass ? 0.98f : 0.9f, t));
            });
            gameObject.SetActive(false);
        }
    }
}
