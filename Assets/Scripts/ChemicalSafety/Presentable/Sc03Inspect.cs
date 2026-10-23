using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC03 drum inspection (step04). The user touches the floating arrow in front of the old drum; a gold ring
    /// closes around it, the label is enlarged inside a "Selected Label" scan frame and the DRUM INSPECTION REPORT
    /// card types in beside it with a Continue button. All texts come from <see cref="Sc03Content"/>.
    /// </summary>
    public sealed class Sc03Inspect : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private FloatingTouchButton touchButton;
        [Tooltip("Hold-to-scan on the drum label (replaces the floating arrow).")]
        [SerializeField] private Sc03LabelScan labelScan;
        [Tooltip("Centre of the drum label; its forward axis points the way the label faces (towards the user).")]
        [SerializeField] private Transform labelAnchor;
        [SerializeField] private float ringDistance = 0.12f;
        [SerializeField] private float scanDistance = 0.34f;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        private static readonly Color Red = new Color(1f, 0.3f, 0.28f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.86f, 0.25f, 1f);
        private static readonly Color[] DotColors = { Red, Yellow, Red, Yellow };

        private Transform _cam;
        private Transform _ring;
        private CanvasGroup _ringGroup;
        private Transform _scan;
        private CanvasGroup _scanGroup;
        private Transform _card;
        private CanvasGroup _cardGroup;
        private Vector3 _cardScale;
        private TextMeshProUGUI[] _rows;
        private RectTransform _continue;
        private bool _touched;
        private bool _continued;
        private bool _built;
        private float _pulse;
        private bool _pulseOn;

        public bool Touched => _touched;
        public bool ContinuePressed => _continued;
        public bool ReportVisible => _card != null && _card.gameObject.activeSelf && _cardGroup.alpha > 0.5f;
        public FloatingTouchButton TouchButton => touchButton;
        public Sc03LabelScan LabelScan => labelScan;
        public bool ScanArmed => labelScan != null && labelScan.IsArmed;
        public Transform ReportCard => _card;

        /// <summary>Raised when the finger presses the arrow or the Continue button (smoke test hooks this too).</summary>
        public event Action ArrowPressed;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (touchButton != null)
                touchButton.Pressed -= OnArrow;
            if (labelScan != null)
                labelScan.Completed -= OnScanned;
        }

        private void OnScanned()
        {
            _touched = true;
            ArrowPressed?.Invoke();
        }

        private void OnArrow(FloatingTouchButton _)
        {
            _touched = true;
            ArrowPressed?.Invoke();
        }

        private void OnContinue()
        {
            PresentableFx.Play(PresentableFx.Tick);
            PresentableFx.Haptic(true, 0.4f, 0.06f);
            _continued = true;
        }

        /// <summary>Arrow fades in and can be touched.</summary>
        public void ArmTouch()
        {
            Build();
            _touched = false;
            if (labelScan != null)
            {
                labelScan.Completed -= OnScanned;
                labelScan.Completed += OnScanned;
                labelScan.Arm();
                return;
            }

            if (touchButton == null)
                return;
            touchButton.Pressed -= OnArrow;
            touchButton.Pressed += OnArrow;
            touchButton.Arm(true);
        }

        public void DisarmTouch()
        {
            if (labelScan != null)
                labelScan.Disarm();
            if (touchButton != null)
                touchButton.Disarm();
        }

        /// <summary>Ring, enlarged label scan and the typed report card; returns once the card is complete.</summary>
        public IEnumerator ShowReport()
        {
            Build();
            _continued = false;
            DisarmTouch();
            PresentableFx.Play(PresentableFx.Success, 0.7f);
            PresentableFx.Haptic(true, 0.5f, 0.12f);

            var facing = Quaternion.LookRotation(-labelAnchor.forward, Vector3.up);
            _ring.SetPositionAndRotation(labelAnchor.position + labelAnchor.forward * ringDistance, facing);
            _scan.SetPositionAndRotation(labelAnchor.position + labelAnchor.forward * scanDistance, facing);
            PlaceCard();
            for (var i = 0; i < _rows.Length; i++)
                _rows[i].maxVisibleCharacters = 0;
            _continue.gameObject.SetActive(false);

            _ring.gameObject.SetActive(true);
            _scan.gameObject.SetActive(true);
            var ringBase = _ring.localScale;
            var scanBase = _scan.localScale;
            yield return PresentableFx.Tween(0.55f, t =>
            {
                _ringGroup.alpha = Mathf.Clamp01(t * 2f);
                _ring.localScale = ringBase * Mathf.LerpUnclamped(1.35f, 1f, t);
            }, PresentableFx.EaseOutBack);
            _pulseOn = true;
            yield return PresentableFx.Tween(0.45f, t =>
            {
                _scanGroup.alpha = Mathf.Clamp01(t * 2f);
                _scan.localScale = scanBase * Mathf.LerpUnclamped(1.25f, 1f, t);
            }, PresentableFx.EaseOutCubic);

            _card.gameObject.SetActive(true);
            _cardGroup.alpha = 0f;
            var totals = new int[_rows.Length];
            for (var i = 0; i < _rows.Length; i++)
            {
                _rows[i].ForceMeshUpdate();
                totals[i] = _rows[i].textInfo.characterCount;
            }

            PresentableFx.Play(PresentableFx.Chime, 0.3f);
            yield return PresentableFx.Tween(0.3f, t => _cardGroup.alpha = t);
            yield return PresentableFx.Tween(0.9f, t =>
            {
                for (var i = 0; i < _rows.Length; i++)
                    _rows[i].maxVisibleCharacters = Mathf.CeilToInt(t * totals[i]);
            }, x => x);
            foreach (var r in _rows)
                r.maxVisibleCharacters = 99999;

            var cg = _continue.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            _continue.gameObject.SetActive(true);
            yield return PresentableFx.Tween(0.35f, t =>
            {
                cg.alpha = Mathf.Clamp01(t * 1.5f);
                _continue.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, t);
            }, PresentableFx.EaseOutBack);
        }

        public IEnumerator HideReport()
        {
            var ringA = _ringGroup.alpha;
            var scanA = _scanGroup.alpha;
            var cardA = _cardGroup.alpha;
            yield return PresentableFx.Tween(0.35f, t =>
            {
                _ringGroup.alpha = Mathf.Lerp(ringA, 0f, t);
                _scanGroup.alpha = Mathf.Lerp(scanA, 0f, t);
                _cardGroup.alpha = Mathf.Lerp(cardA, 0f, t);
            });
            _pulseOn = false;
            _ring.localScale = Vector3.one * RingScale;
            _ring.gameObject.SetActive(false);
            _scan.gameObject.SetActive(false);
            _card.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_pulseOn || _ring == null || !_ring.gameObject.activeSelf)
                return;
            _pulse += Time.deltaTime;
            var s = 1f + Mathf.Sin(_pulse * 2.4f) * 0.025f;
            _ring.localScale = Vector3.one * (RingScale * s);
        }

        private const float RingScale = 0.0021f;

        // The card hangs in front of the head a little to the right (like the report in step04) so Continue is within reach.
        private void PlaceCard()
        {
            if (_cam == null)
                _cam = Camera.main != null ? Camera.main.transform : null;
            if (_cam == null)
                return;
            var fwd = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
            var right = Vector3.Cross(Vector3.up, fwd);
            var pos = _cam.position + fwd * 0.66f + right * 0.26f + Vector3.down * 0.01f;
            _card.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;
            _cam = Camera.main != null ? Camera.main.transform : null;
            var font = art.englishFont;

            // gold ring around the drum
            var ringCanvas = PresentableUi.WorldCanvas("InspectRing", transform, new Vector2(512f, 512f), RingScale, false, 60);
            _ring = ringCanvas.transform;
            _ringGroup = ringCanvas.gameObject.AddComponent<CanvasGroup>();
            var ringSprite = art.ringThin != null ? art.ringThin : art.ring;
            PresentableUi.Img("Glow", _ring, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), art.radialGlow,
                new Color(Gold.r, Gold.g, Gold.b, 0.07f));
            PresentableUi.Img("Ring", _ring, Vector2.zero, Vector2.one, ringSprite, Color.white, true);
            PresentableUi.AlwaysOnTop(ringCanvas);
            _ring.gameObject.SetActive(false);

            // enlarged label inside a "Selected Label" scan frame
            var scanCanvas = PresentableUi.WorldCanvas("InspectScan", transform, new Vector2(560f, 600f), 0.00068f, false, 70);
            _scan = scanCanvas.transform;
            _scanGroup = scanCanvas.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img("Label", _scan, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.82f), art.sc03Label,
                Color.white, true);
            PresentableUi.Img("Frame", _scan, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.56f), art.scanFrame,
                Color.white);
            var cap = PresentableUi.Text("Caption", _scan, new Vector2(0.1f, 0.575f), new Vector2(0.9f, 0.69f),
                Sc03Content.ScanCaption, 44f, Color.white, TextAlignmentOptions.Center, true, font);
            cap.enableAutoSizing = true;
            cap.fontSizeMin = 24f;
            cap.fontSizeMax = 44f;
            PresentableUi.AlwaysOnTop(scanCanvas);
            _scan.gameObject.SetActive(false);

            BuildCard(font);
        }

        private void BuildCard(TMP_FontAsset font)
        {
            var canvas = PresentableUi.WorldCanvas("InspectReport", transform, new Vector2(1000f, 700f), 0.00041f, true, 130);
            canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            _card = canvas.transform;
            _cardScale = _card.localScale;
            _cardGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var panel = PresentableUi.Rect("Panel", _card, new Vector2(0.0f, 0.0f), new Vector2(1f, 1f));
            if (art.frameWide != null)
                PresentableUi.Img(panel, art.frameWide, Color.white);
            else
                PresentableUi.FlatBox(panel, art.roundedSolid, PresentableUi.FlatBorder, PresentableUi.FlatFill);
            var title = PresentableUi.Text("Title", panel, new Vector2(0.12f, 0.78f), new Vector2(0.88f, 0.9f),
                Sc03Content.ReportTitle, 40f, Color.white, TextAlignmentOptions.Left, true, font);
            title.enableAutoSizing = true;
            title.fontSizeMin = 22f;
            title.fontSizeMax = 40f;
            title.textWrappingMode = TextWrappingModes.Normal;
            title.overflowMode = TextOverflowModes.Truncate;
            PresentableUi.Img("Divider", panel, new Vector2(0.14f, 0.76f), new Vector2(0.86f, 0.768f), art.roundedSolid,
                new Color(PresentableUi.FlatBorder.r, PresentableUi.FlatBorder.g, PresentableUi.FlatBorder.b, 0.55f));

            var count = Mathf.Min(Sc03Content.ReportLabels.Length, Sc03Content.ReportValues.Length);
            _rows = new TextMeshProUGUI[count];
            const float top = 0.74f;
            const float rowH = 0.145f;
            for (var i = 0; i < count; i++)
            {
                var y1 = top - i * rowH;
                var dot = PresentableUi.Img("Dot" + i, panel, new Vector2(0.08f, y1 - 0.07f), new Vector2(0.13f, y1 - 0.02f),
                    art.dot, DotColors[i % DotColors.Length], true);
                dot.raycastTarget = false;
                var valueColor = i == 2 ? "#FF6A5E" : "#FFFFFF";
                var text = "<b>" + Sc03Content.ReportLabels[i] + "</b>\n<color=" + valueColor + ">" +
                           Sc03Content.ReportValues[i] + "</color>";
                var tmp = PresentableUi.Text("Row" + i, panel, new Vector2(0.16f, y1 - rowH + 0.012f),
                    new Vector2(0.94f, y1 - 0.006f), text, 32f, Color.white, TextAlignmentOptions.TopLeft, false, font);
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 16f;
                tmp.fontSizeMax = 32f;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode = TextOverflowModes.Truncate;
                tmp.margin = new Vector4(4f, 2f, 4f, 2f);
                _rows[i] = tmp;
            }

            _continue = PresentableUi.Rect("ContinueButton", _card, new Vector2(0.28f, -0.2f), new Vector2(0.72f, -0.04f));
            _continue.gameObject.AddComponent<CanvasGroup>();
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", _continue, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Pill", _continue, Vector2.zero, Vector2.one,
                new Vector2(-60f, -42f), new Vector2(60f, 42f)), art.button, Color.white);
            PresentableUi.Text("Label", _continue, Vector2.zero, Vector2.one, Sc03Content.ContinueLabel, 44f, Color.white,
                TextAlignmentOptions.Center, true, font);
            var btn = _continue.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(OnContinue);
            _continue.gameObject.AddComponent<HoverLift>().Init(_continue);
            _continue.gameObject.SetActive(false);

            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        /// <summary>Editor / smoke test: press Continue without a finger.</summary>
        public void PressContinueForTest() => OnContinue();
    }
}
