using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 3, right answer: the SDS line "Emergency action if degraded: DO NOT OPEN, Evacuate, Notify supervisor, Contact
    /// specialist disposal" played as an emergency response protocol. A glass protocol panel lists the four steps beside the drum and
    /// lights them one after the other while the room reacts: a containment seal closes round the drum, a lit path marches to the
    /// exit, radio rings go out to the supervisor, a progress ring completes for the specialist team. Everything is UI (world
    /// canvases), nothing needs a shader of its own.
    /// </summary>
    public sealed class Sc03Protocol : MonoBehaviour
    {
        private const float PanelW = 1100f;
        private const float PanelH = 860f;
        private const float Mpp = 0.00055f;

        private static readonly Color Cyan = new Color(0.30f, 0.85f, 1f, 1f);
        private static readonly Color Orange = new Color(1f, 0.56f, 0.14f, 1f);

        private PresentableArt _art;
        private Transform _cam;
        private Sprite _vGradient;
        private Sprite _hGradient;

        // panel
        private Transform _panel;
        private CanvasGroup _panelGroup;
        private Image _border;
        private Image _halo;
        private Image _sheen;
        private RectTransform _sheenHost;
        private readonly Image[] _plate = new Image[4];
        private readonly Image[] _outline = new Image[4];
        private readonly Image[] _badge = new Image[4];
        private readonly Image[] _tick = new Image[4];
        private readonly Image[] _iconImg = new Image[4];
        private readonly CanvasGroup[] _rowGroup = new CanvasGroup[4];
        private readonly RectTransform[] _iconRt = new RectTransform[4];
        private readonly Image[] _wave = new Image[3];
        private Image _specRing;
        private RectTransform _doneChip;
        private Image _railFill;

        // room
        private Transform _seal;
        private CanvasGroup _sealGroup;
        private Image _sealRing;
        private Image _sealGlow;
        private Image _sealScan;
        private RectTransform _lock;
        private Transform _floorRing;
        private CanvasGroup _floorRingGroup;
        private Image _floorRingImg;
        private Transform _path;
        private CanvasGroup _pathGroup;
        private readonly Image[] _chevron = new Image[9];
        private Transform _exit;
        private CanvasGroup _exitGroup;
        private const float PathLen = 4f; // the lane canvas is 2000 px x 0.002 = 4 m
        private Vector3 _drum;

        private bool _built;
        private bool _running;
        private int _active = -1;
        private float _t;
        private Color _tone = Cyan;

        public static Sc03Protocol Create(PresentableArt art, Transform parent)
        {
            var go = new GameObject("Sc03Protocol");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Sc03Protocol>();
            p._art = art;
            p.Build();
            return p;
        }

        // ------------------------------------------------------------------ build

        private static Sprite Gradient(bool horizontal)
        {
            var tex = new Texture2D(horizontal ? 64 : 4, horizontal ? 4 : 64, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < tex.height; y++)
            {
                for (var x = 0; x < tex.width; x++)
                {
                    var u = horizontal ? x / (tex.width - 1f) : y / (tex.height - 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.4f)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private Image Box(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Color c, Vector2 oMin = default, Vector2 oMax = default)
        {
            return PresentableUi.Img(PresentableUi.Rect(name, parent, aMin, aMax, oMin, oMax), _art.roundedSolid, c, sliced: true);
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;
            _vGradient = Gradient(false);
            _hGradient = Gradient(true);
            BuildPanel();
            BuildSeal();
            BuildFloorRing();
            BuildPath();
            _panel.gameObject.SetActive(false);
            _seal.gameObject.SetActive(false);
            _floorRing.gameObject.SetActive(false);
            _path.gameObject.SetActive(false);
            _exit.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            var font = _art.englishFont;
            var canvas = PresentableUi.WorldCanvas("ProtocolPanel", transform, new Vector2(PanelW + 260f, PanelH + 260f), Mpp, false, 40);
            _panel = canvas.transform;
            _panelGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)_panel;

            _halo = Box("Halo", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f),
                new Vector2(-PanelW * 0.5f - 40f, -PanelH * 0.5f - 36f), new Vector2(PanelW * 0.5f + 40f, PanelH * 0.5f + 36f));
            var body = PresentableUi.Centered("Body", root, Vector2.zero, new Vector2(PanelW, PanelH));
            _border = Box("Border", body, Vector2.zero, Vector2.one, Cyan);
            Box("Rim", body, Vector2.zero, Vector2.one, new Color(0.05f, 0.24f, 0.46f, 1f), new Vector2(7f, 7f), new Vector2(-7f, -7f));
            Box("Glass", body, Vector2.zero, Vector2.one, new Color(0.015f, 0.055f, 0.115f, 0.97f), new Vector2(14f, 14f), new Vector2(-14f, -14f));
            PresentableUi.Img(PresentableUi.Rect("TopSheen", body, new Vector2(0.01f, 0.55f), new Vector2(0.99f, 0.985f), new Vector2(14f, 0f), new Vector2(-14f, -14f)),
                _vGradient, new Color(0.45f, 0.85f, 1f, 0.07f));
            for (var c = 0; c < 4; c++)
            {
                var right = (c & 1) == 1;
                var top = c >= 2;
                var sx = right ? -1f : 1f;
                var sy = top ? -1f : 1f;
                var a = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
                PresentableUi.Img(PresentableUi.Rect("BracketH" + c, body, a, a, new Vector2(sx > 0 ? -6f : -130f, sy > 0 ? -6f : -2f),
                    new Vector2(sx > 0 ? 130f : 6f, sy > 0 ? 2f : 6f)), _art.roundedSolid, Orange, sliced: true);
                PresentableUi.Img(PresentableUi.Rect("BracketV" + c, body, a, a, new Vector2(sx > 0 ? -6f : -2f, sy > 0 ? -6f : -130f),
                    new Vector2(sx > 0 ? 2f : 6f, sy > 0 ? 130f : 6f)), _art.roundedSolid, Orange, sliced: true);
            }

            var chip = PresentableUi.Text("Source", body, new Vector2(0.05f, 0.925f), new Vector2(0.80f, 0.975f), Sc03Content.ProtocolSource, 26f,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f), TextAlignmentOptions.MidlineLeft, true, font);
            chip.characterSpacing = 4f;
            var title = PresentableUi.Text("Title", body, new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.925f), Sc03Content.ProtocolHeader, 46f, Color.white,
                TextAlignmentOptions.MidlineLeft, true, font);
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 46f;
            PresentableUi.Img(PresentableUi.Rect("Rule", body, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.826f)), _hGradient,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f));
            Box("RuleAccent", body, new Vector2(0.05f, 0.817f), new Vector2(0.10f, 0.83f), Orange);

            // progress rail behind the number badges
            PresentableUi.Img(PresentableUi.Rect("Rail", body, new Vector2(0.0725f, 0.14f), new Vector2(0.0775f, 0.74f)), null, new Color(1f, 1f, 1f, 0.12f));
            _railFill = PresentableUi.Img(PresentableUi.Rect("RailFill", body, new Vector2(0.0725f, 0.74f), new Vector2(0.0775f, 0.74f)), null,
                new Color(0.13f, 0.90f, 0.63f, 0.9f));

            var titles = Sc03Content.ProtocolTitles;
            var lines = Sc03Content.ProtocolLines;
            var icons = new[] { _art.cross, _art.iconNext, _art.iconSpeaker, _art.iconSds };
            for (var i = 0; i < 4; i++)
            {
                var top = 0.79f - i * 0.185f;
                var row = PresentableUi.Rect("Row" + i, body, new Vector2(0.03f, top - 0.165f), new Vector2(0.97f, top));
                _rowGroup[i] = row.gameObject.AddComponent<CanvasGroup>();
                _plate[i] = Box("Plate", row, Vector2.zero, Vector2.one, new Color(0.30f, 0.80f, 1f, 0.06f));
                _outline[i] = Box("Outline", row, Vector2.zero, Vector2.one, new Color(Cyan.r, Cyan.g, Cyan.b, 0f), new Vector2(-3f, -3f), new Vector2(3f, 3f));
                _outline[i].rectTransform.SetSiblingIndex(0);
                // the outline is a ring: a dark inner plate covers its middle
                Box("Inner", row, Vector2.zero, Vector2.one, new Color(0.02f, 0.075f, 0.15f, 1f), new Vector2(2f, 2f), new Vector2(-2f, -2f)).rectTransform.SetSiblingIndex(1);
                _badge[i] = Box("Badge", row, new Vector2(0.01f, 0.18f), new Vector2(0.095f, 0.82f), new Color(0.05f, 0.30f, 0.52f, 1f));
                PresentableUi.Text("Num", _badge[i].rectTransform, Vector2.zero, Vector2.one, "0" + (i + 1), 32f, new Color(0.85f, 0.98f, 1f),
                    TextAlignmentOptions.Center, true, font);
                var iconBox = PresentableUi.Rect("IconBox", row, new Vector2(0.115f, 0.10f), new Vector2(0.235f, 0.90f));
                _iconRt[i] = iconBox;
                PresentableUi.Img(PresentableUi.Rect("Glow", iconBox, new Vector2(-0.2f, -0.2f), new Vector2(1.2f, 1.2f)), _art.radialGlow,
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f));
                _iconImg[i] = PresentableUi.Img(PresentableUi.Rect("Icon", iconBox, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f)), icons[i],
                    new Color(0.85f, 0.98f, 1f), preserveAspect: true);
                if (i == 1)
                {
                    // the "next" icon art reads as a tick; the evacuate step gets a double arrow instead
                    _iconImg[i].enabled = false;
                    var arrows = PresentableUi.Text("Arrows", iconBox, Vector2.zero, Vector2.one, "»", 84f, new Color(0.85f, 0.98f, 1f),
                        TextAlignmentOptions.Center, true, font);
                    arrows.enableAutoSizing = false;
                }

                var t = PresentableUi.Text("Title", row, new Vector2(0.265f, 0.50f), new Vector2(0.86f, 0.96f), titles != null && i < titles.Length ? titles[i] : string.Empty,
                    36f, Color.white, TextAlignmentOptions.MidlineLeft, true, font);
                t.enableAutoSizing = true;
                t.fontSizeMin = 22f;
                t.fontSizeMax = 36f;
                var l = PresentableUi.Text("Line", row, new Vector2(0.265f, 0.04f), new Vector2(0.86f, 0.52f), lines != null && i < lines.Length ? lines[i] : string.Empty,
                    26f, new Color(0.84f, 0.93f, 1f), TextAlignmentOptions.TopLeft, false, font);
                l.enableAutoSizing = true;
                l.fontSizeMin = 17f;
                l.fontSizeMax = 26f;
                l.textWrappingMode = TextWrappingModes.Normal;
                l.overflowMode = TextOverflowModes.Truncate;
                _tick[i] = PresentableUi.Img(PresentableUi.Rect("Tick", row, new Vector2(0.885f, 0.22f), new Vector2(0.985f, 0.78f)), _art.check,
                    new Color(0.13f, 0.90f, 0.63f), preserveAspect: true);
                _tick[i].gameObject.SetActive(false);
            }

            // radio rings (step 3) and the specialist progress ring (step 4)
            for (var i = 0; i < _wave.Length; i++)
            {
                _wave[i] = PresentableUi.Img(PresentableUi.Rect("Wave" + i, _iconRt[2], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f),
                    new Vector2(30f, 30f)), _art.ringThin != null ? _art.ringThin : _art.ring, new Color(Cyan.r, Cyan.g, Cyan.b, 0f), preserveAspect: true);
                _wave[i].raycastTarget = false;
            }

            _specRing = PresentableUi.Img(PresentableUi.Rect("SpecRing", _iconRt[3], new Vector2(-0.1f, -0.1f), new Vector2(1.1f, 1.1f)), _art.ring,
                new Color(0.13f, 0.90f, 0.63f, 0.95f), preserveAspect: true);
            _specRing.type = Image.Type.Filled;
            _specRing.fillMethod = Image.FillMethod.Radial360;
            _specRing.fillOrigin = (int)Image.Origin360.Top;
            _specRing.fillAmount = 0f;

            _doneChip = PresentableUi.Rect("DoneChip", body, new Vector2(0.2f, 0.015f), new Vector2(0.8f, 0.092f));
            Box("Bg", _doneChip, Vector2.zero, Vector2.one, new Color(0.13f, 0.90f, 0.63f, 0.95f));
            PresentableUi.Text("Label", _doneChip, Vector2.zero, Vector2.one, Sc03Content.ProtocolDone, 34f, new Color(0.01f, 0.12f, 0.08f),
                TextAlignmentOptions.Center, true, font);
            _doneChip.gameObject.SetActive(false);

            _sheenHost = PresentableUi.Rect("SheenHost", body, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            _sheenHost.gameObject.AddComponent<RectMask2D>();
            _sheen = PresentableUi.Img(PresentableUi.Centered("Sheen", _sheenHost, Vector2.zero, new Vector2(420f, PanelH * 1.7f)), _hGradient, Color.white);
            _sheen.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            _sheen.color = new Color(1f, 1f, 1f, 0f);
            PresentableUi.AlwaysOnTop(canvas);
        }

        // containment seal: a ring of light with a scanning line round the drum and a padlock above it
        private void BuildSeal()
        {
            var canvas = PresentableUi.WorldCanvas("ProtocolSeal", transform, new Vector2(900f, 1000f), 0.0013f, false, 35);
            _seal = canvas.transform;
            _sealGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)_seal;
            _sealGlow = PresentableUi.Img(PresentableUi.Centered("Glow", root, new Vector2(0f, -40f), new Vector2(860f, 900f)), _art.radialGlow,
                new Color(1f, 0.24f, 0.37f, 0.10f));
            _sealRing = PresentableUi.Img(PresentableUi.Centered("Ring", root, new Vector2(0f, -40f), new Vector2(800f, 800f)), _art.ring,
                new Color(1f, 0.24f, 0.37f, 0.9f), preserveAspect: true);
            PresentableUi.Img(PresentableUi.Centered("RingThin", root, new Vector2(0f, -40f), new Vector2(700f, 700f)), _art.ringThin != null ? _art.ringThin : _art.ring,
                new Color(1f, 0.64f, 0.70f, 0.55f), preserveAspect: true);
            _sealScan = PresentableUi.Img(PresentableUi.Centered("Scan", root, new Vector2(0f, -40f), new Vector2(620f, 44f)), _hGradient,
                new Color(1f, 0.64f, 0.70f, 0.8f));

            // padlock: body + shackle (a ring whose lower half hides behind the body)
            _lock = PresentableUi.Centered("Lock", root, new Vector2(0f, 400f), new Vector2(240f, 240f));
            PresentableUi.Img(PresentableUi.Centered("Disc", _lock, Vector2.zero, new Vector2(230f, 230f)), _art.radialGlow, new Color(1f, 0.24f, 0.37f, 0.55f));
            PresentableUi.Img(PresentableUi.Centered("Shackle", _lock, new Vector2(0f, 56f), new Vector2(150f, 150f)), _art.ring, new Color(1f, 0.64f, 0.70f, 1f),
                preserveAspect: true);
            Box("BodyEdge", _lock, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 0.64f, 0.70f, 1f), new Vector2(-72f, -80f), new Vector2(72f, 26f));
            Box("Body", _lock, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.86f, 0.12f, 0.27f, 1f), new Vector2(-66f, -74f), new Vector2(66f, 20f));
            Box("BodyGloss", _lock, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 1f, 1f, 0.22f), new Vector2(-58f, -26f), new Vector2(58f, 14f));
            PresentableUi.Img(PresentableUi.Centered("Key", _lock, new Vector2(0f, -26f), new Vector2(26f, 26f)), _art.dot != null ? _art.dot : _art.radialGlow,
                new Color(0.2f, 0.02f, 0.05f, 1f), preserveAspect: true);
            PresentableUi.AlwaysOnTop(canvas);
        }

        private void BuildFloorRing()
        {
            var canvas = PresentableUi.WorldCanvas("ProtocolFloorRing", transform, new Vector2(640f, 640f), 0.0016f, false, 5);
            _floorRing = canvas.transform;
            _floorRingGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            PresentableUi.Img(PresentableUi.Rect("Glow", _floorRing, Vector2.zero, Vector2.one), _art.radialGlow, new Color(1f, 0.24f, 0.37f, 0.5f));
            _floorRingImg = PresentableUi.Img(PresentableUi.Rect("Ring", _floorRing, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f)), _art.ring,
                new Color(1f, 0.64f, 0.70f, 0.95f), preserveAspect: true);
        }

        // lit path on the floor with marching chevrons, and an EXIT chip at its end
        private void BuildPath()
        {
            var canvas = PresentableUi.WorldCanvas("ProtocolPath", transform, new Vector2(500f, 2000f), 0.002f, false, 5);
            _path = canvas.transform;
            _pathGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)_path;
            PresentableUi.Img(PresentableUi.Rect("Lane", root, new Vector2(0.18f, 0f), new Vector2(0.82f, 1f)), _vGradient, new Color(0.13f, 0.90f, 0.63f, 0.12f));
            PresentableUi.Img(PresentableUi.Rect("EdgeL", root, new Vector2(0.175f, 0f), new Vector2(0.185f, 1f)), _vGradient, new Color(0.62f, 1f, 0.86f, 0.7f));
            PresentableUi.Img(PresentableUi.Rect("EdgeR", root, new Vector2(0.815f, 0f), new Vector2(0.825f, 1f)), _vGradient, new Color(0.62f, 1f, 0.86f, 0.7f));
            for (var i = 0; i < _chevron.Length; i++)
            {
                _chevron[i] = PresentableUi.Img(PresentableUi.Centered("Chevron" + i, root, Vector2.zero, new Vector2(240f, 240f)), _art.iconNext,
                    new Color(0.62f, 1f, 0.86f, 0f), preserveAspect: true);
                _chevron[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f); // the arrow points along the path (UI up)
            }

            var exit = PresentableUi.WorldCanvas("ProtocolExit", transform, new Vector2(520f, 220f), 0.0016f, false, 30);
            _exit = exit.transform;
            _exitGroup = exit.gameObject.AddComponent<CanvasGroup>();
            var er = (RectTransform)_exit;
            Box("Glow", er, new Vector2(-0.05f, -0.1f), new Vector2(1.05f, 1.1f), new Color(0.13f, 0.90f, 0.63f, 0.20f));
            Box("Bg", er, Vector2.zero, Vector2.one, new Color(0.05f, 0.55f, 0.36f, 1f));
            Box("Rim", er, Vector2.zero, Vector2.one, new Color(0.62f, 1f, 0.86f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            Box("Inner", er, Vector2.zero, Vector2.one, new Color(0.05f, 0.55f, 0.36f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            PresentableUi.Text("Label", er, Vector2.zero, Vector2.one, "EXIT", 110f, Color.white, TextAlignmentOptions.Center, true, _art.englishFont);
            PresentableUi.AlwaysOnTop(exit);
        }

        // ------------------------------------------------------------------ play

        /// <summary>
        /// Runs the whole protocol (about 11 s). <paramref name="drumCentre"/> the middle of the drum, <paramref name="label"/> its label
        /// (forward = towards the aisle), the panel goes to <paramref name="panelPos"/>, <paramref name="exitTarget"/> is where the lit path ends.
        /// </summary>
        public IEnumerator Play(Vector3 drumCentre, Transform label, Vector3 panelPos, Quaternion panelRot, float panelScale, Vector3 exitTarget)
        {
            Build();
            if (_cam == null && Camera.main != null)
                _cam = Camera.main.transform;
            _drum = drumCentre;
            _running = true;
            _active = -1;
            _t = 0f;
            ResetPanel();

            // placements
            _panel.SetPositionAndRotation(panelPos, panelRot);
            _panel.localScale = Vector3.one * (Mpp * panelScale * 0.75f);
            var aisle = new Vector3(label.forward.x, 0f, label.forward.z).normalized;
            var floorY = drumCentre.y - 0.46f;
            _floorRing.SetPositionAndRotation(new Vector3(drumCentre.x, floorY + 0.01f, drumCentre.z), Quaternion.Euler(90f, 0f, 0f));
            _seal.position = drumCentre + aisle * 0.42f + Vector3.up * 0.12f;

            var start = new Vector3(drumCentre.x + aisle.x * 0.8f, 0.02f, drumCentre.z + aisle.z * 0.8f);
            var end = new Vector3(exitTarget.x, 0.02f, exitTarget.z);
            var dir = end - start;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.01f ? aisle : dir.normalized;
            end = start + dir * PathLen;
            _path.SetPositionAndRotation(start + dir * (PathLen * 0.5f), Quaternion.LookRotation(Vector3.down, dir));
            _exit.position = end + Vector3.up * 0.55f;

            _panel.gameObject.SetActive(true);
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);

            // panel pops in, the first step begins
            yield return PresentableFx.Tween(0.6f, t =>
            {
                _panelGroup.alpha = Mathf.Clamp01(t * 1.8f);
                _panel.localScale = Vector3.one * (Mpp * panelScale * Mathf.LerpUnclamped(0.75f, 1f, t));
            }, PresentableFx.EaseOutBack);

            // 1. DO NOT OPEN
            yield return Step(0, Seal(), 2.6f);
            // 2. EVACUATE
            yield return Step(1, EvacuatePath(), 2.8f);
            // 3. NOTIFY SUPERVISOR
            yield return Step(2, RadioRings(), 2.4f);
            // 4. CONTACT SPECIALIST DISPOSAL
            yield return Step(3, SpecialistRing(), 2.4f);

            // all done: the panel and the seal turn emerald, a glossy sweep crosses the panel
            _tone = new Color(0.13f, 0.90f, 0.63f, 1f);
            _doneChip.gameObject.SetActive(true);
            _doneChip.localScale = Vector3.zero;
            PresentableFx.Play(PresentableFx.Success, 0.9f);
            PresentableFx.Haptic(true, 0.8f, 0.2f);
            StartCoroutine(SheenPass());
            yield return PresentableFx.Tween(0.45f, t => _doneChip.localScale = Vector3.one * t, PresentableFx.EaseOutBack);
            yield return PresentableFx.Wait(1.6f);
            _running = false;
            yield return Soften();
        }

        private IEnumerator Step(int i, IEnumerator fx, float seconds)
        {
            _active = i;
            PresentableFx.Play(PresentableFx.Chime, 0.6f);
            PresentableFx.Haptic(true, 0.5f, 0.1f);
            StartCoroutine(fx);
            var rg = _rowGroup[i];
            StartCoroutine(Pop(_iconRt[i]));
            yield return PresentableFx.Wait(seconds - 0.5f);
            // done: the outline and the number turn emerald, the tick pops
            _tick[i].gameObject.SetActive(true);
            _tick[i].rectTransform.localScale = Vector3.zero;
            _badge[i].color = new Color(0.13f, 0.90f, 0.63f, 1f);
            PresentableFx.Play(PresentableFx.Tick, 0.9f);
            StartCoroutine(Pop(_tick[i].rectTransform));
            _railFill.rectTransform.anchorMin = new Vector2(0.0725f, Mathf.Lerp(0.74f, 0.14f, (i + 1) / 4f));
            _active = -1;
            _outline[i].color = new Color(0.13f, 0.90f, 0.63f, 0.9f);
            _plate[i].color = new Color(0.13f, 0.90f, 0.63f, 0.10f);
            yield return PresentableFx.Wait(0.5f);
            rg.alpha = 1f;
        }

        private static IEnumerator Pop(RectTransform rt)
        {
            yield return PresentableFx.Tween(0.4f, t => rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, t), PresentableFx.EaseOutBack);
            rt.localScale = Vector3.one;
        }

        private void ResetPanel()
        {
            _panelGroup.alpha = 0f;
            _tone = Cyan;
            _railFill.rectTransform.anchorMin = new Vector2(0.0725f, 0.74f);
            for (var i = 0; i < 4; i++)
            {
                _tick[i].gameObject.SetActive(false);
                _badge[i].color = new Color(0.05f, 0.30f, 0.52f, 1f);
                _outline[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
                _plate[i].color = new Color(0.30f, 0.80f, 1f, 0.06f);
                _iconRt[i].localScale = Vector3.one;
            }

            _specRing.fillAmount = 0f;
            _doneChip.gameObject.SetActive(false);
            foreach (var w in _wave)
                w.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
            _sheen.color = new Color(1f, 1f, 1f, 0f);
            _floorRing.gameObject.SetActive(false);
            _seal.gameObject.SetActive(false);
            _path.gameObject.SetActive(false);
            _exit.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ beats

        // 1. a ruby ring of light closes round the drum, the padlock drops in and the floor ring pulses
        private IEnumerator Seal()
        {
            _seal.gameObject.SetActive(true);
            _floorRing.gameObject.SetActive(true);
            _sealGroup.alpha = 0f;
            _floorRingGroup.alpha = 0f;
            _lock.anchoredPosition = new Vector2(0f, 700f);
            var sealBase = _seal.localScale;
            yield return PresentableFx.Tween(0.7f, t =>
            {
                _sealGroup.alpha = Mathf.Clamp01(t * 1.6f);
                _floorRingGroup.alpha = t;
                _seal.localScale = sealBase * Mathf.LerpUnclamped(1.5f, 1f, t);
            }, PresentableFx.EaseOutCubic);
            PresentableFx.Haptic(true, 0.9f, 0.15f);
            yield return PresentableFx.Tween(0.45f, t => _lock.anchoredPosition = new Vector2(0f, Mathf.Lerp(700f, 400f, t)), PresentableFx.EaseOutBack);
            PresentableFx.Play(PresentableFx.Error, 0.4f);
        }

        // 2. the path lights up from the drum to the exit and the chevrons march along it
        private IEnumerator EvacuatePath()
        {
            _path.gameObject.SetActive(true);
            _exit.gameObject.SetActive(true);
            _pathGroup.alpha = 0f;
            _exitGroup.alpha = 0f;
            yield return PresentableFx.Tween(0.6f, t =>
            {
                _pathGroup.alpha = t;
                _exitGroup.alpha = t;
            });
        }

        // 3. radio rings go out from the supervisor icon
        private IEnumerator RadioRings()
        {
            for (var k = 0; k < 3; k++)
            {
                StartCoroutine(Ring(_wave[k], k * 0.5f));
                PresentableFx.Haptic(true, 0.35f, 0.06f);
                yield return PresentableFx.Wait(0.5f);
            }
        }

        private static IEnumerator Ring(Image img, float delay)
        {
            var rt = img.rectTransform;
            for (var pass = 0; pass < 2; pass++)
            {
                yield return PresentableFx.Tween(1.1f, t =>
                {
                    rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.6f, t);
                    img.color = new Color(Cyan.r, Cyan.g, Cyan.b, (1f - t) * 0.9f);
                }, PresentableFx.EaseOutCubic);
            }

            img.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
        }

        // 4. the specialist team's progress ring fills up
        private IEnumerator SpecialistRing()
        {
            yield return PresentableFx.Tween(1.7f, t => _specRing.fillAmount = t, x => x);
            _specRing.fillAmount = 1f;
        }

        private IEnumerator SheenPass()
        {
            var w = PanelW;
            for (var pass = 0; pass < 2; pass++)
            {
                yield return PresentableFx.Tween(1.0f, t =>
                {
                    _sheen.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.6f, w * 0.6f, t), 0f);
                    _sheen.color = new Color(0.85f, 1f, 0.95f, Mathf.Sin(t * Mathf.PI) * 0.5f);
                });
                yield return PresentableFx.Wait(0.3f);
            }

            _sheen.color = new Color(1f, 1f, 1f, 0f);
        }

        // fade the panel and the path away; the seal stays as a soft emerald ring round the drum
        private IEnumerator Soften()
        {
            var a0 = _panelGroup.alpha;
            var p0 = _pathGroup.alpha;
            yield return PresentableFx.Tween(0.6f, t =>
            {
                _panelGroup.alpha = Mathf.Lerp(a0, 0f, t);
                _pathGroup.alpha = Mathf.Lerp(p0, 0f, t);
                _exitGroup.alpha = Mathf.Lerp(p0, 0f, t);
            });
            _panel.gameObject.SetActive(false);
            _path.gameObject.SetActive(false);
            _exit.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!_built || (!_running && !_seal.gameObject.activeSelf))
                return;
            _t += Time.deltaTime;
            if (_cam == null && Camera.main != null)
                _cam = Camera.main.transform;

            // the seal, the exit chip and the panel face the learner (yaw only)
            Face(_seal);
            Face(_exit);

            var breathe = 0.5f + 0.5f * Mathf.Sin(_t * 3f);
            if (_seal.gameObject.activeSelf)
            {
                var c = _tone.g > 0.5f ? new Color(0.13f, 0.90f, 0.63f, 1f) : new Color(1f, 0.24f, 0.37f, 1f);
                _sealRing.color = new Color(c.r, c.g, c.b, 0.55f + 0.35f * breathe);
                _sealGlow.color = new Color(c.r, c.g, c.b, 0.08f + 0.08f * breathe);
                _floorRingImg.color = new Color(c.r, c.g, c.b, 0.7f + 0.25f * breathe);
                var sy = Mathf.Lerp(-300f, 300f, Mathf.PingPong(_t * 0.8f, 1f));
                _sealScan.rectTransform.anchoredPosition = new Vector2(0f, sy - 40f);
                var scanC = new Color(c.r, c.g, c.b, 0.8f);
                _sealScan.color = scanC;
                _lock.localScale = Vector3.one * (1f + 0.04f * breathe);
            }

            if (_path.gameObject.activeSelf)
            {
                // chevrons run along the lane (UI y): fade in at the start, out at the end
                var span = 2000f;
                for (var i = 0; i < _chevron.Length; i++)
                {
                    var u = Mathf.Repeat(_t * 0.35f + i / (float)_chevron.Length, 1f);
                    var y = Mathf.Lerp(-span * 0.5f, span * 0.5f, u);
                    _chevron[i].rectTransform.anchoredPosition = new Vector2(0f, y);
                    var a = Mathf.Sin(u * Mathf.PI);
                    _chevron[i].color = new Color(0.62f, 1f, 0.86f, a * 0.95f);
                }
            }

            // the active row glows, the panel frame and halo take the protocol tone
            for (var i = 0; i < 4; i++)
            {
                if (i == _active)
                    _outline[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f + 0.4f * breathe);
            }

            if (_panel.gameObject.activeSelf)
            {
                _border.color = Color.Lerp(_border.color, _tone, 1f - Mathf.Exp(-6f * Time.deltaTime));
                _halo.color = new Color(_tone.r, _tone.g, _tone.b, 0.12f + 0.08f * breathe);
            }
        }

        private void Face(Transform t)
        {
            if (_cam == null || !t.gameObject.activeSelf)
                return;
            var look = t.position - _cam.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                t.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }
}
