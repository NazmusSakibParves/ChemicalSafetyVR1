using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 2: one large callout over the chemical that is being asked about. A glowing partition splits it in two halves of the
    /// same height: on the left the chemical as a hologram (transparent picture on a light stage) with its name and three
    /// properties, on the right the question box (the director lays PresentableMcqPanel exactly over the right half, "embedded").
    /// It faces the learner, arrives from the left, and after the answer the whole callout takes the result colour (emerald / ruby)
    /// with a halo, a travelling light and a glossy sheen.
    /// </summary>
    public sealed class Sc02Callout : MonoBehaviour
    {
        private const float W = 2480f;
        private const float H = 900f;
        public const float Mpp = 0.00055f;
        private const float BodyLift = 100f;   // body centre above the canvas centre (the tail lives below the body)
        private const float TailTip = 405f;    // tail tip below the canvas centre

        private static readonly Color Cyan = new Color(0.30f, 0.85f, 1f, 1f);
        private static readonly Color Orange = new Color(1f, 0.56f, 0.14f, 1f);

        private PresentableArt _art;
        private Transform _cam;
        private Transform _bubble;
        private RectTransform _body;
        private CanvasGroup _group;
        private CanvasGroup _content;
        private Image _border;
        private Image _halo1;
        private Image _halo2;
        private Image _bloom;
        private Image _partCore;
        private Image _partGlow;
        private Image _partDot;
        private Image _stageGlow;
        private Image _beam;
        private Image _icon;
        private RectTransform _iconRt;
        private RectTransform _tail;
        private TextMeshProUGUI _chip;
        private TextMeshProUGUI _title;
        private readonly TextMeshProUGUI[] _lines = new TextMeshProUGUI[3];
        private readonly CanvasGroup[] _rows = new CanvasGroup[3];
        private readonly RectTransform[] _rowRts = new RectTransform[3];
        private readonly Image[] _comet = new Image[9];
        private Image _sheen;
        private RectTransform _sheenHost;
        private Sprite _vGradient;
        private Sprite _hGradient;
        private Vector3 _basePos;
        private float _scale = 1f;
        private float _slide;
        private float _tailPx;
        private bool _built;
        private bool _resultMode;
        private Color _main;
        private Color _light;
        private float _t;

        public bool IsOpen { get; private set; }

        /// <summary>Laid exactly over the right half of the callout, every frame (the question box).</summary>
        public Transform Follower { get; set; }

        public float Scale => _scale;

        private Vector3 BodyCenter => _bubble.position + _bubble.up * (BodyLift * Mpp * _scale);

        public static Sc02Callout Create(PresentableArt art)
        {
            var go = new GameObject("Sc02Callout");
            var c = go.AddComponent<Sc02Callout>();
            c._art = art;
            c.Build();
            return c;
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

        private Image Box(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Color c, Vector2 oMin = default, Vector2 oMax = default,
            bool sliced = true)
        {
            return PresentableUi.Img(PresentableUi.Rect(name, parent, aMin, aMax, oMin, oMax), _art.roundedSolid, c, sliced: sliced);
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;
            _cam = Camera.main != null ? Camera.main.transform : null;
            _vGradient = Gradient(false);
            _hGradient = Gradient(true);
            var font = _art.englishFont;

            var canvas = PresentableUi.WorldCanvas("Callout", transform, new Vector2(W + 360f, H + 360f), Mpp, false, 30);
            _bubble = canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var root = (RectTransform)_bubble;

            // light behind everything: oval bloom + two soft rings that take the result colour later
            _bloom = PresentableUi.Img(PresentableUi.Centered("Bloom", root, new Vector2(0f, BodyLift), new Vector2(W * 1.25f, H * 1.7f)), _art.radialGlow,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f));
            _halo2 = Box("Halo2", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.06f),
                new Vector2(-W * 0.5f - 90f, -H * 0.5f - 80f + BodyLift), new Vector2(W * 0.5f + 90f, H * 0.5f + 80f + BodyLift));
            _halo1 = Box("Halo1", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f),
                new Vector2(-W * 0.5f - 44f, -H * 0.5f - 38f + BodyLift), new Vector2(W * 0.5f + 44f, H * 0.5f + 38f + BodyLift));

            // tail: a diamond with a thin light stem down to the container
            _tail = PresentableUi.Centered("Tail", root, new Vector2(0f, BodyLift - H * 0.5f - 18f), new Vector2(70f, 70f));
            _tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            PresentableUi.Img(_tail, _art.roundedSolid, Cyan, sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Fill", _tail, Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f)),
                _art.roundedSolid, new Color(0.02f, 0.10f, 0.20f, 1f), sliced: true);

            _body = PresentableUi.Centered("Body", root, new Vector2(0f, BodyLift), new Vector2(W, H));
            // frame: cyan edge, deep blue rim, navy glass
            _border = Box("Border", _body, Vector2.zero, Vector2.one, Cyan);
            Box("Rim", _body, Vector2.zero, Vector2.one, new Color(0.05f, 0.24f, 0.46f, 1f), new Vector2(7f, 7f), new Vector2(-7f, -7f));
            Box("Glass", _body, Vector2.zero, Vector2.one, new Color(0.015f, 0.055f, 0.115f, 0.97f), new Vector2(14f, 14f), new Vector2(-14f, -14f));
            PresentableUi.Img(PresentableUi.Rect("TopSheen", _body, new Vector2(0.01f, 0.55f), new Vector2(0.99f, 0.985f), new Vector2(14f, 0f), new Vector2(-14f, -14f)),
                _vGradient, new Color(0.45f, 0.85f, 1f, 0.07f));
            PresentableUi.Img(PresentableUi.Rect("Vignette", _body, new Vector2(0.01f, 0.015f), new Vector2(0.99f, 0.30f), new Vector2(14f, 14f), new Vector2(-14f, 0f)),
                _vGradient, new Color(0f, 0f, 0.05f, 0.35f));

            // orange corner brackets (the same accent as the question card frames)
            for (var c = 0; c < 4; c++)
            {
                var right = (c & 1) == 1;
                var top = c >= 2;
                var ax = right ? 1f : 0f;
                var ay = top ? 1f : 0f;
                var sx = right ? -1f : 1f;
                var sy = top ? -1f : 1f;
                var a = new Vector2(ax, ay);
                var h = PresentableUi.Rect("BracketH" + c, _body, a, a, new Vector2(sx > 0 ? -6f : -150f, sy > 0 ? -6f : -2f), new Vector2(sx > 0 ? 150f : 6f, sy > 0 ? 2f : 6f));
                PresentableUi.Img(h, _art.roundedSolid, Orange, sliced: true);
                var v = PresentableUi.Rect("BracketV" + c, _body, a, a, new Vector2(sx > 0 ? -6f : -2f, sy > 0 ? -6f : -150f), new Vector2(sx > 0 ? 2f : 6f, sy > 0 ? 150f : 6f));
                PresentableUi.Img(v, _art.roundedSolid, Orange, sliced: true);
            }

            // left half: content (fades in after the frame)
            var content = PresentableUi.Rect("Content", _body, Vector2.zero, Vector2.one);
            _content = content.gameObject.AddComponent<CanvasGroup>();

            _chip = PresentableUi.Text("Chip", content, new Vector2(0.035f, 0.905f), new Vector2(0.30f, 0.965f), string.Empty, 28f,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f), TextAlignmentOptions.MidlineLeft, true, font);
            _chip.characterSpacing = 6f;
            _title = PresentableUi.Text("Title", content, new Vector2(0.035f, 0.795f), new Vector2(0.485f, 0.905f), string.Empty, 52f, Color.white,
                TextAlignmentOptions.MidlineLeft, true, font);
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 30f;
            _title.fontSizeMax = 52f;
            PresentableUi.Img(PresentableUi.Rect("Rule", content, new Vector2(0.035f, 0.786f), new Vector2(0.485f, 0.792f)), _hGradient,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f));
            Box("RuleAccent", content, new Vector2(0.035f, 0.783f), new Vector2(0.085f, 0.795f), Orange);

            // the hologram stage: soft light cone, a floor glow with a bright core, the chemical floating above it
            _beam = PresentableUi.Img(PresentableUi.Rect("Beam", content, new Vector2(0.045f, 0.11f), new Vector2(0.195f, 0.76f)), _vGradient,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.20f));
            _stageGlow = PresentableUi.Img(PresentableUi.Rect("StageGlow", content, new Vector2(0.015f, 0.075f), new Vector2(0.225f, 0.215f)), _art.radialGlow,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
            PresentableUi.Img(PresentableUi.Rect("StageCore", content, new Vector2(0.055f, 0.105f), new Vector2(0.185f, 0.175f)), _art.radialGlow,
                new Color(0.85f, 1f, 1f, 0.8f));
            _iconRt = PresentableUi.Rect("Icon", content, new Vector2(0.04f, 0.15f), new Vector2(0.20f, 0.775f));
            _icon = PresentableUi.Img(_iconRt, null, Color.white, preserveAspect: true);

            // three property rows: numbered chip + text on a faint plate
            for (var i = 0; i < 3; i++)
            {
                var top = 0.745f - i * 0.225f;
                var row = PresentableUi.Rect("Row" + i, content, new Vector2(0.235f, top - 0.20f), new Vector2(0.485f, top));
                _rowRts[i] = row;
                _rows[i] = row.gameObject.AddComponent<CanvasGroup>();
                Box("Plate", row, Vector2.zero, Vector2.one, new Color(0.30f, 0.80f, 1f, 0.07f));
                Box("Accent", row, new Vector2(0f, 0.12f), new Vector2(0.012f, 0.88f), Cyan);
                var chipBg = Box("NumBg", row, new Vector2(0.04f, 0.20f), new Vector2(0.15f, 0.80f), new Color(0.05f, 0.30f, 0.52f, 1f));
                chipBg.rectTransform.localRotation = Quaternion.identity;
                var num = PresentableUi.Text("Num", chipBg.rectTransform, Vector2.zero, Vector2.one, "0" + (i + 1), 30f, new Color(0.85f, 0.98f, 1f),
                    TextAlignmentOptions.Center, true, font);
                num.enableAutoSizing = false;
                _lines[i] = PresentableUi.Text("Text", row, new Vector2(0.18f, 0.04f), new Vector2(0.985f, 0.96f), string.Empty, 34f,
                    new Color(0.93f, 0.98f, 1f), TextAlignmentOptions.MidlineLeft, false, font);
                _lines[i].textWrappingMode = TextWrappingModes.Normal;
                _lines[i].enableAutoSizing = true;
                _lines[i].fontSizeMin = 22f;
                _lines[i].fontSizeMax = 34f;
                _lines[i].overflowMode = TextOverflowModes.Truncate;
            }

            // the partition: a light line with a diamond node and a travelling spark
            _partGlow = PresentableUi.Img(PresentableUi.Rect("PartGlow", _body, new Vector2(0.4955f, 0.035f), new Vector2(0.5045f, 0.965f)), _vGradient,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f));
            _partCore = PresentableUi.Img(PresentableUi.Rect("PartCore", _body, new Vector2(0.4991f, 0.04f), new Vector2(0.5009f, 0.96f)), _vGradient,
                new Color(0.85f, 1f, 1f, 0.95f));
            var node = PresentableUi.Centered("PartNode", _body, Vector2.zero, new Vector2(30f, 30f));
            node.localRotation = Quaternion.Euler(0f, 0f, 45f);
            PresentableUi.Img(node, _art.roundedSolid, new Color(0.85f, 1f, 1f, 1f), sliced: true);
            _partDot = PresentableUi.Img(PresentableUi.Centered("PartDot", _body, Vector2.zero, new Vector2(70f, 70f)), _art.radialGlow,
                new Color(0.85f, 1f, 1f, 0.9f));

            // result layers (hidden until the answer): travelling light round the frame, glossy sheen across the whole callout
            for (var i = 0; i < _comet.Length; i++)
            {
                var size = i == 0 ? 110f : Mathf.Lerp(80f, 20f, i / 8f);
                _comet[i] = PresentableUi.Img(PresentableUi.Centered("Comet" + i, _body, Vector2.zero, Vector2.one * size), _art.radialGlow, Color.white);
                _comet[i].gameObject.SetActive(false);
            }

            _sheenHost = PresentableUi.Rect("SheenHost", _body, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            _sheenHost.gameObject.AddComponent<RectMask2D>();
            _sheen = PresentableUi.Img(PresentableUi.Centered("Sheen", _sheenHost, Vector2.zero, new Vector2(520f, H * 1.7f)), _hGradient, Color.white);
            _sheen.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            _sheen.color = new Color(1f, 1f, 1f, 0f);

            PresentableUi.AlwaysOnTop(canvas);
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ open / close

        /// <summary>Opens the callout. <paramref name="anchorTop"/> is the top centre of the container, <paramref name="centerX"/> the
        /// world position the callout is centred on (same height line as the container, clamped sideways by the director).</summary>
        public IEnumerator Open(Vector3 anchorTop, Vector3 center, string chip, string title, string[] props, Sprite icon, float worldScale)
        {
            Build();
            _scale = Mathf.Max(0.2f, worldScale);
            gameObject.SetActive(true);
            _resultMode = false;
            ClearResult();
            _chip.text = chip;
            _title.text = title;
            for (var i = 0; i < _lines.Length; i++)
            {
                var has = props != null && i < props.Length;
                _rows[i].gameObject.SetActive(has);
                if (has)
                    _lines[i].text = props[i];
            }

            _icon.sprite = icon;
            _icon.enabled = icon != null;

            if (_cam == null && Camera.main != null)
                _cam = Camera.main.transform;
            // canvas centre: the tail tip a hand's breadth above the container
            _basePos = new Vector3(center.x, anchorTop.y + (0.08f + TailTip * Mpp) * _scale, center.z);
            _bubble.position = _basePos;
            Face();
            // the tail points down at the container even when the callout is clamped to the middle of the view
            var offset = Vector3.Dot(anchorTop - _basePos, _bubble.right);
            _tailPx = Mathf.Clamp(offset / (Mpp * _scale), -W * 0.5f + 220f, W * 0.5f - 220f);
            _tail.anchoredPosition = new Vector2(_tailPx, BodyLift - H * 0.5f - 18f);

            IsOpen = true;
            _content.alpha = 0f;
            foreach (var r in _rows)
                r.alpha = 0f;
            _partCore.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            _partGlow.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            PresentableFx.Play(PresentableFx.Whoosh, 0.45f);
            StartCoroutine(OpenDetails());
            yield return PresentableFx.Tween(0.6f, t =>
            {
                _group.alpha = Mathf.Clamp01(t * 1.8f);
                _bubble.localScale = Vector3.one * (Mpp * _scale * Mathf.LerpUnclamped(0.7f, 1f, t));
                _body.localScale = new Vector3(Mathf.LerpUnclamped(0.35f, 1f, t), 1f, 1f);
                _slide = Mathf.Lerp(-0.45f * _scale, 0f, t); // arrives from the left
            }, PresentableFx.EaseOutBack);
            _slide = 0f;
            _body.localScale = Vector3.one;
        }

        private IEnumerator OpenDetails()
        {
            yield return PresentableFx.Wait(0.28f);
            StartCoroutine(Grow(_partCore.rectTransform, 0.5f));
            StartCoroutine(Grow(_partGlow.rectTransform, 0.5f));
            yield return PresentableFx.Tween(0.4f, t => _content.alpha = t);
            for (var i = 0; i < _rows.Length; i++)
                StartCoroutine(RowIn(i, 0.18f + i * 0.16f));
        }

        private static IEnumerator Grow(RectTransform rt, float seconds)
        {
            yield return PresentableFx.Tween(seconds, t => rt.localScale = new Vector3(1f, t, 1f), PresentableFx.EaseOutCubic);
        }

        private IEnumerator RowIn(int i, float delay)
        {
            yield return PresentableFx.Wait(delay);
            if (!_rows[i].gameObject.activeSelf)
                yield break;
            var rt = _rowRts[i];
            var basePos = rt.anchoredPosition;
            PresentableFx.Play(PresentableFx.Tick, 0.4f);
            yield return PresentableFx.Tween(0.4f, t =>
            {
                _rows[i].alpha = Mathf.Clamp01(t * 1.5f);
                rt.anchoredPosition = basePos + new Vector2(Mathf.Lerp(-70f, 0f, t), 0f);
            }, PresentableFx.EaseOutCubic);
            rt.anchoredPosition = basePos;
        }

        public IEnumerator Close()
        {
            if (!IsOpen)
                yield break;
            var from = _group.alpha;
            yield return PresentableFx.Tween(0.35f, t =>
            {
                _group.alpha = Mathf.Lerp(from, 0f, t);
                _bubble.localScale = Vector3.one * (Mpp * _scale * Mathf.Lerp(1f, 0.92f, t));
            });
            IsOpen = false;
            _resultMode = false;
            ClearResult();
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ frame

        private void Face()
        {
            if (_cam == null)
                return;
            var look = _bubble.position - _cam.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                _bubble.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private void Update()
        {
            if (!IsOpen || _bubble == null)
                return;
            _t += Time.deltaTime;
            if (_cam == null && Camera.main != null)
                _cam = Camera.main.transform;
            Face();
            _bubble.position = _basePos + _bubble.right * _slide + Vector3.up * (Mathf.Sin(Time.time * 1.4f) * 0.008f);

            // the question box rides exactly on the right half
            if (Follower != null)
                Follower.SetPositionAndRotation(BodyCenter + _bubble.right * (W * 0.25f * Mpp * _scale), _bubble.rotation);

            // the hologram floats; the stage light and the partition spark breathe
            var bob = Mathf.Sin(_t * 1.7f);
            if (_iconRt != null)
                _iconRt.anchoredPosition = new Vector2(0f, bob * 9f);
            var breathe = 0.5f + 0.5f * Mathf.Sin(_t * 2.1f);
            if (!_resultMode)
            {
                _stageGlow.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.40f + 0.25f * breathe);
                _beam.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f + 0.10f * breathe);
                _bloom.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.08f + 0.05f * breathe);
                _halo1.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f + 0.07f * breathe);
            }

            var sp = Mathf.PingPong(_t * 0.28f, 1f);
            _partDot.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-H * 0.42f, H * 0.42f, sp));
        }

        // ------------------------------------------------------------------ result

        private void ClearResult()
        {
            _resultMode = false;
            if (_built)
            {
                _border.color = Cyan;
                _partCore.color = new Color(0.85f, 1f, 1f, 0.95f);
                _partGlow.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f);
                _halo1.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f);
                _halo2.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.06f);
                _sheen.color = new Color(1f, 1f, 1f, 0f);
                foreach (var c in _comet)
                    c.gameObject.SetActive(false);
            }
        }

        /// <summary>The whole callout takes the result colour: halo, travelling light, glossy sheen and a burst of sparks.</summary>
        public void ShowResult(bool correct)
        {
            if (!IsOpen)
                return;
            _resultMode = true;
            _main = correct ? PresentableMcqPanel.EmeraldMain : PresentableMcqPanel.RubyMain;
            _light = correct ? PresentableMcqPanel.EmeraldLight : PresentableMcqPanel.RubyLight;
            foreach (var c in _comet)
                c.gameObject.SetActive(true);
            StartCoroutine(ResultLoop());
            StartCoroutine(ResultSheen());
            StartCoroutine(Sparks(correct));
        }

        private IEnumerator ResultLoop()
        {
            var t0 = Time.time;
            while (_resultMode && IsOpen)
            {
                var t = Time.time - t0;
                var appear = Mathf.Clamp01(t / 0.45f);
                var breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
                _border.color = Color.Lerp(Cyan, Color.Lerp(_light, _main, 0.35f), appear);
                _halo1.color = new Color(_main.r, _main.g, _main.b, appear * (0.60f + 0.22f * breathe));
                _halo2.color = new Color(_main.r, _main.g, _main.b, appear * (0.16f + 0.10f * breathe));
                _bloom.color = new Color(_main.r, _main.g, _main.b, appear * (0.20f + 0.12f * breathe));
                _partCore.color = Color.Lerp(new Color(0.85f, 1f, 1f, 0.95f), _light, appear);
                _partGlow.color = new Color(_main.r, _main.g, _main.b, 0.30f + 0.25f * appear);
                _stageGlow.color = new Color(_main.r, _main.g, _main.b, 0.45f + 0.2f * breathe);
                _beam.color = new Color(_main.r, _main.g, _main.b, 0.14f + 0.10f * breathe);
                UpdateComet(t);
                yield return null;
            }
        }

        private void UpdateComet(float t)
        {
            var hx = W * 0.5f + 6f;
            var hy = H * 0.5f + 6f;
            var lap = 2.6f;
            for (var i = 0; i < _comet.Length; i++)
            {
                var u = Mathf.Repeat(t / lap - i * 0.006f, 1f);
                _comet[i].rectTransform.anchoredPosition = PointOnRect(u, hx, hy);
                var fade = 1f - i / (float)_comet.Length;
                _comet[i].color = i == 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(_light.r, _light.g, _light.b, 0.8f * fade);
            }
        }

        private static Vector2 PointOnRect(float u, float hx, float hy)
        {
            var w = 2f * hx;
            var h = 2f * hy;
            var d = u * (2f * (w + h));
            if (d < w)
                return new Vector2(-hx + d, hy);
            d -= w;
            if (d < h)
                return new Vector2(hx, hy - d);
            d -= h;
            if (d < w)
                return new Vector2(hx - d, -hy);
            d -= w;
            return new Vector2(-hx, -hy + d);
        }

        // a slanted band of light crosses the whole callout: three strong passes, then a soft one every few seconds
        private IEnumerator ResultSheen()
        {
            var pass = 0;
            yield return PresentableFx.Wait(0.3f);
            while (_resultMode && IsOpen)
            {
                var strength = pass < 3 ? 0.55f : 0.30f;
                for (var t = 0f; t < 1f && _resultMode; t += Time.deltaTime / 1.15f)
                {
                    _sheen.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-W * 0.62f, W * 0.62f, t), 0f);
                    var c = Color.Lerp(Color.white, _light, 0.3f);
                    _sheen.color = new Color(c.r, c.g, c.b, Mathf.Sin(t * Mathf.PI) * strength);
                    yield return null;
                }

                _sheen.color = new Color(1f, 1f, 1f, 0f);
                pass++;
                yield return PresentableFx.Wait(pass < 3 ? 0.9f : 4f);
            }
        }

        private IEnumerator Sparks(bool correct)
        {
            yield return PresentableFx.Wait(0.2f);
            var n = correct ? 70 : 36;
            var imgs = new Image[n];
            var vel = new Vector2[n];
            var pos = new Vector2[n];
            var life = new float[n];
            var cols = correct
                ? new[] { PresentableMcqPanel.EmeraldMain, PresentableMcqPanel.EmeraldLight, new Color(1f, 0.90f, 0.55f), Color.white }
                : new[] { PresentableMcqPanel.RubyMain, PresentableMcqPanel.RubyLight, Color.white };
            var host = _body;
            for (var i = 0; i < n; i++)
            {
                var rt = PresentableUi.Centered("Spark" + i, host, new Vector2(W * 0.25f, 0f), Vector2.one * Random.Range(30f, 64f));
                imgs[i] = PresentableUi.Img(rt, _art.radialGlow, cols[i % cols.Length]);
                var a = Random.Range(0f, Mathf.PI * 2f);
                var s = Random.Range(300f, 900f);
                vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s;
                pos[i] = new Vector2(W * 0.25f, 0f);
                life[i] = Random.Range(0.8f, 1.4f);
            }

            for (var t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                for (var i = 0; i < n; i++)
                {
                    vel[i] += new Vector2(0f, -520f) * Time.deltaTime;
                    vel[i] *= 1f - 1.5f * Time.deltaTime;
                    pos[i] += vel[i] * Time.deltaTime;
                    imgs[i].rectTransform.anchoredPosition = pos[i];
                    var c = imgs[i].color;
                    c.a = Mathf.Clamp01(1f - t / life[i]);
                    imgs[i].color = c;
                }

                yield return null;
            }

            for (var i = 0; i < n; i++)
                Destroy(imgs[i].gameObject);
        }
    }
}
