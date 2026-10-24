using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 only ("fancy" question). Extra life for the shared MCQ panel without touching how it works:
    /// the options arrive one by one, every row breathes with a soft glow and shows a nudging chevron so it reads as touchable,
    /// a comet of light runs round the frame, a faint scan line sweeps the question and a sheen crosses the title.
    /// A right answer ends in a burst of green sparks, a wrong one in a red flicker. All pieces are plain UI images (cheap on Quest)
    /// and none of them takes input. To use it in another scenario set Question.fancy = true; nothing else changes.
    /// </summary>
    public sealed class Sc06McqFx : MonoBehaviour
    {
        [Tooltip("Seconds between two options landing.")]
        [SerializeField] private float rowStagger = 0.2f;
        [SerializeField] private float lapSeconds = 4.2f;
        [SerializeField] private float scanSeconds = 5.5f;
        [SerializeField] private float sheenSeconds = 3.4f;
        [Tooltip("Distance of the light comet from the edge of the frame (canvas pixels).")]
        [SerializeField] private Vector2 cometInset = new Vector2(16f, 16f);

        private PresentableMcqPanel _panel;
        private PresentableArt _art;
        private RectTransform _canvas;
        private RectTransform _fx;
        private RectTransform _burst;
        private readonly List<Image> _glows = new List<Image>();
        private readonly List<TextMeshProUGUI> _chevrons = new List<TextMeshProUGUI>();
        private readonly List<bool> _hover = new List<bool>();
        private readonly List<Image> _comet = new List<Image>();
        private Image _scan;
        private Image _sheen;
        private Image _flash;
        private Sprite _hGradient;
        private Sprite _vGradient;
        private bool _built;
        private bool _active;
        private bool _landed;
        private int _rows;
        private float _t;

        public bool RowsLanded => _landed;
        public bool Active => _active;
        public int CometCount => _comet.Count;
        public int ChevronCount => _chevrons.Count;
        public bool FxVisible => _fx != null && _fx.gameObject.activeSelf;

        // The panel's canvas draws with the depth test off; the pieces added here must too, or they hide behind the rows.
        private static void Top(Graphic g)
        {
            if (g == null)
                return;
            if (g is TMP_Text tmp)
            {
                var m = new Material(tmp.fontSharedMaterial);
                m.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                tmp.fontSharedMaterial = m;
            }
            else
            {
                var m = new Material(g.defaultMaterial);
                m.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                g.material = m;
            }
        }

        public void Attach(PresentableMcqPanel panel)
        {
            _panel = panel;
            _art = panel.Art;
            _canvas = panel.CanvasRect;
            _rows = panel.RowCount;
            if (!_built)
                Build();
            _active = true;
            _landed = false;
            _t = 0f;
            _fx.gameObject.SetActive(true);
            _burst.gameObject.SetActive(false);
            _flash.gameObject.SetActive(false);
            for (var i = 0; i < _chevrons.Count; i++)
            {
                _chevrons[i].gameObject.SetActive(i < _rows);
                _glows[i].gameObject.SetActive(i < _rows);
                _glows[i].color = new Color(0.35f, 0.9f, 1f, 0f);
                _hover[i] = false;
            }

            // rows stay hidden until RowsIn lets them land one by one
            for (var i = 0; i < _rows; i++)
            {
                var g = panel.RowRoot(i).GetComponent<CanvasGroup>();
                if (g != null)
                    g.alpha = 0f;
            }
        }

        public void Detach()
        {
            _active = false;
            if (_fx != null)
                _fx.gameObject.SetActive(false);
            if (_burst != null)
                _burst.gameObject.SetActive(false);
        }

        /// <summary>The panel closes: everything fades with it.</summary>
        public void Stop() => Detach();

        // ------------------------------------------------------------------ rows arrive one by one

        public IEnumerator RowsIn()
        {
            _landed = false;
            for (var i = 0; i < _rows; i++)
                StartCoroutine(RowLand(i, i * rowStagger));
            yield return PresentableFx.Wait((_rows - 1) * rowStagger + 0.5f);
            _landed = true;
        }

        private IEnumerator RowLand(int i, float delay)
        {
            var rt = _panel.RowRoot(i);
            var g = rt.GetComponent<CanvasGroup>();
            yield return PresentableFx.Wait(delay);
            PresentableFx.Play(PresentableFx.Tick, 0.55f);
            var basePos = rt.anchoredPosition;
            // a flash sweeps the row as it lands
            StartCoroutine(Flash(i));
            yield return PresentableFx.Tween(0.42f, t =>
            {
                g.alpha = Mathf.Clamp01(t * 1.6f);
                rt.anchoredPosition = basePos + new Vector2(Mathf.Lerp(-110f, 0f, t), 0f);
            }, PresentableFx.EaseOutBack);
            g.alpha = 1f;
            rt.anchoredPosition = basePos;
        }

        private IEnumerator Flash(int i)
        {
            if (i >= _glows.Count)
                yield break;
            var glow = _glows[i];
            for (var t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                var k = t / 0.55f;
                glow.color = new Color(0.75f, 1f, 1f, (1f - k) * 0.5f);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ results

        public void OnResult(bool correct, int index)
        {
            _fx.gameObject.SetActive(false); // the question frame is hidden while the result card is up
            if (correct)
                StartCoroutine(SparkBurst());
            else
                StartCoroutine(RedFlicker());
        }

        public void OnRetry()
        {
            if (_flash != null)
                _flash.gameObject.SetActive(false);
            _fx.gameObject.SetActive(true);
            for (var i = 0; i < _hover.Count; i++)
                _hover[i] = false;
        }

        private IEnumerator RedFlicker()
        {
            _flash.gameObject.SetActive(true);
            for (var t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                var k = t / 0.9f;
                var a = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k) * 0.55f;
                _flash.color = new Color(1f, 0.18f, 0.14f, a);
                yield return null;
            }

            _flash.gameObject.SetActive(false);
        }

        private IEnumerator SparkBurst()
        {
            yield return PresentableFx.Wait(0.25f);
            _burst.gameObject.SetActive(true);
            const int n = 46;
            var imgs = new Image[n];
            var vel = new Vector2[n];
            var pos = new Vector2[n];
            var life = new float[n];
            var cols = new[] { new Color(0.4f, 1f, 0.55f), new Color(0.75f, 1f, 0.8f), new Color(1f, 0.92f, 0.5f), Color.white };
            for (var i = 0; i < n; i++)
            {
                var rt = PresentableUi.Centered("Spark" + i, _burst, new Vector2(0f, 40f), Vector2.one * Random.Range(28f, 58f));
                imgs[i] = PresentableUi.Img(rt, _art.radialGlow, cols[i % cols.Length]);
                Top(imgs[i]);
                var a = Random.Range(0f, Mathf.PI * 2f);
                var s = Random.Range(220f, 640f);
                vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s;
                pos[i] = new Vector2(0f, 40f);
                life[i] = Random.Range(0.8f, 1.35f);
            }

            for (var t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                for (var i = 0; i < n; i++)
                {
                    vel[i] += new Vector2(0f, -420f) * Time.deltaTime;
                    vel[i] *= 1f - 1.6f * Time.deltaTime;
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
            _burst.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ idle life

        private void Update()
        {
            if (!_active || _fx == null || !_fx.gameObject.activeSelf)
                return;
            _t += Time.deltaTime;

            // rows breathe once they have landed; the hovered one glows brighter
            if (_landed)
            {
                for (var i = 0; i < _rows && i < _glows.Count; i++)
                {
                    var pulse = 0.5f + 0.5f * Mathf.Sin(_t * 2.1f + i * 0.9f);
                    var a = _hover[i] ? 0.36f : 0.07f + 0.15f * pulse;
                    var c = _glows[i].color;
                    c = Color.Lerp(c, new Color(0.35f, 0.9f, 1f, a), 1f - Mathf.Exp(-10f * Time.deltaTime));
                    _glows[i].color = c;
                    var nudge = Mathf.Abs(Mathf.Sin(_t * 3.2f + i * 0.5f)) * (_hover[i] ? 12f : 7f);
                    _chevrons[i].rectTransform.anchoredPosition = new Vector2(-14f - nudge, 0f);
                    _chevrons[i].color = new Color(0.55f, 0.95f, 1f, _hover[i] ? 1f : 0.55f + 0.35f * pulse);
                }
            }
            else
            {
                for (var i = 0; i < _chevrons.Count; i++)
                    _chevrons[i].color = new Color(0.55f, 0.95f, 1f, 0f);
            }

            UpdateComet();

            // scan line over the question, a sheen across the title
            var size = _canvas.rect.size;
            var sp = Mathf.Repeat(_t / scanSeconds, 1f);
            var scanOn = sp < 0.45f;
            _scan.gameObject.SetActive(scanOn);
            if (scanOn)
            {
                var k = sp / 0.45f;
                _scan.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(size.y * 0.30f, -size.y * 0.40f, k));
                _scan.color = new Color(0.4f, 0.95f, 1f, Mathf.Sin(k * Mathf.PI) * 0.22f);
            }

            var hp = Mathf.Repeat(_t / sheenSeconds, 1f);
            var sheenOn = hp < 0.5f;
            _sheen.gameObject.SetActive(sheenOn);
            if (sheenOn)
            {
                var k = hp / 0.5f;
                _sheen.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-size.x * 0.34f, size.x * 0.34f, k), size.y * 0.405f);
                _sheen.color = new Color(0.85f, 1f, 1f, Mathf.Sin(k * Mathf.PI) * 0.55f);
            }
        }

        private void UpdateComet()
        {
            var size = _canvas.rect.size * 1.0f;
            var hx = size.x * 0.5f * 1.04f - cometInset.x;
            var hy = size.y * 0.5f * 1.04f - cometInset.y;
            var perimeter = 4f * (hx + hy);
            for (var i = 0; i < _comet.Count; i++)
            {
                var u = Mathf.Repeat(_t / lapSeconds - i * 0.0085f, 1f);
                _comet[i].rectTransform.anchoredPosition = PointOnRect(u, hx, hy);
                var fade = 1f - i / (float)_comet.Count;
                var c = i == 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.45f, 0.95f, 1f, 0.7f * fade);
                _comet[i].color = c;
            }
        }

        // u runs 0..1 clockwise from the top-left corner along the rectangle (+-hx, +-hy)
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
                    var a = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private void Build()
        {
            _built = true;
            _hGradient = Gradient(true);
            _vGradient = Gradient(false);
            _fx = PresentableUi.Rect("Sc06Fx", _canvas, Vector2.zero, Vector2.one);
            // above the question frame, plate and text, below the option rows
            _fx.SetSiblingIndex(_panel.RowRoot(0).GetSiblingIndex());
            var glowSprite = _art.radialGlow;
            for (var i = 0; i < 9; i++)
            {
                var size = i == 0 ? 120f : Mathf.Lerp(86f, 22f, i / 8f);
                var rt = PresentableUi.Centered("Comet" + i, _fx, Vector2.zero, Vector2.one * size);
                _comet.Add(PresentableUi.Img(rt, glowSprite, Color.white));
            }

            var scanRt = PresentableUi.Centered("Scan", _fx, Vector2.zero, new Vector2(_canvas.rect.width * 0.84f, 70f));
            _scan = PresentableUi.Img(scanRt, _vGradient, new Color(0.4f, 0.95f, 1f, 0f));
            var sheenRt = PresentableUi.Centered("Sheen", _fx, Vector2.zero, new Vector2(260f, 56f));
            _sheen = PresentableUi.Img(sheenRt, _hGradient, new Color(0.85f, 1f, 1f, 0f));

            // per-row glow (behind the label) and chevron (right edge)
            for (var i = 0; i < 4; i++)
            {
                var row = _panel.RowRoot(i);
                var gRt = PresentableUi.Rect("FxGlow", row, new Vector2(0.01f, 0.06f), new Vector2(0.99f, 0.94f));
                var g = PresentableUi.Img(gRt, _art.roundedSolid, new Color(0.35f, 0.9f, 1f, 0f), sliced: true);
                gRt.SetSiblingIndex(1);
                _glows.Add(g);
                var cRt = PresentableUi.Rect("FxChevron", row, new Vector2(0.925f, 0.12f), new Vector2(0.99f, 0.88f));
                var chev = PresentableUi.Text(cRt, "»", 48f, new Color(0.55f, 0.95f, 1f, 0f), TextAlignmentOptions.Center, true, _art.englishFont);
                chev.raycastTarget = false;
                chev.enableAutoSizing = false;
                chev.fontSize = 52f;
                _chevrons.Add(chev);
                _hover.Add(false);
                var lift = row.GetComponent<HoverLift>();
                var captured = i;
                if (lift != null)
                    lift.HoverChanged += on => _hover[captured] = on;
            }

            // red flicker and the spark container sit on top of everything else of the canvas
            var flashRt = PresentableUi.Centered("RedFlicker", _canvas, Vector2.zero, _canvas.rect.size * 1.12f);
            _flash = PresentableUi.Img(flashRt, glowSprite, new Color(1f, 0.18f, 0.14f, 0f));
            _flash.gameObject.SetActive(false);
            _burst = PresentableUi.Rect("SparkBurst", _canvas, Vector2.zero, Vector2.one);
            _burst.gameObject.SetActive(false);
            foreach (var g in _fx.GetComponentsInChildren<Graphic>(true))
                Top(g);
            foreach (var g in _glows)
                Top(g);
            foreach (var c in _chevrons)
                Top(c);
            Top(_flash);
        }
    }
}
