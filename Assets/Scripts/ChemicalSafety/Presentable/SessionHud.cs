using System;
using System.Collections;
using ChemicalSafety.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Client request: after the login a glass timer capsule sits fixed in the lower LEFT of the view (it follows the head, so it
    /// stays in the same place when the learner turns), with a pause / resume button next to it. Also hosts the "time is up" toast
    /// (screen centre, with voice-over). The clock itself is <see cref="SessionClock"/>; this only draws and ticks it.
    /// </summary>
    public sealed class SessionHud : MonoBehaviour
    {
        // metres in the view: right, down, forward of the head
        private static readonly Vector3 TimerOffset = new Vector3(-0.30f, -0.27f, 0.9f); // lower LEFT of the view, turns with the head
        private static readonly Vector3 ToastOffset = new Vector3(0f, 0.02f, 1.5f);

        public static SessionHud Instance { get; private set; }

        public event Action TimeUp;

        private Transform _cam;
        private Transform _timerRoot;
        private Transform _toastRoot;
        private TextMeshProUGUI _time;
        private Image _pauseA;
        private Image _pauseB;
        private Image _play;
        private Image _plate;
        private Image _ring;
        private Image _glow;
        private Image _edge;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _flash;
        private CanvasGroup _flashGroup;
        private float _born;
        private int _lastWholeSecond = -1;
        private bool _five, _one;
        private CanvasGroup _timerGroup;
        private CanvasGroup _toastGroup;
        private AudioSource _audio;
        private bool _fired;
        private bool _snapped;
        private bool _visible = true;

        public static SessionHud Create(Transform cam)
        {
            if (Instance != null)
                Destroy(Instance.gameObject);
            var go = new GameObject("SessionHud");
            var hud = go.AddComponent<SessionHud>();
            hud._cam = cam;
            Instance = hud;
            hud.Build();
            return hud;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_timerGroup != null)
                _timerGroup.alpha = visible ? 1f : 0f;
            if (_timerGroup != null)
                _timerGroup.blocksRaycasts = visible;
        }

        private static Sprite PlaySprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var fx = (x + 0.5f) / n;
                var fy = (y + 0.5f) / n;
                var inside = fx <= 1f - Mathf.Abs(fy - 0.5f) * 2f;
                tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private void Build()
        {
            // ---- timer: glass capsule, progress ring with the pause button inside, label + big digits. 460 x 150 px, 0.001 m / px
            var tr = new GameObject("Timer");
            tr.transform.SetParent(transform, false);
            _timerRoot = tr.transform;
            var canvas = PresentableUi.WorldCanvas("TimerCanvas", _timerRoot, new Vector2(460f, 150f), 0.001f, true, 30);
            _timerGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var host = (RectTransform)canvas.transform;
            var cap = CapsuleSprite();
            var neon = new Color(0.35f, 0.85f, 1f, 1f);

            _glow = PresentableUi.Img(PresentableUi.Rect("Glow", host, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(8f, 8f)), EdgeSprite(),
                new Color(neon.r, neon.g, neon.b, 0.28f), sliced: true);
            _edge = PresentableUi.Img(PresentableUi.Rect("Edge", host, Vector2.zero, Vector2.one), EdgeSprite(), neon, sliced: true);
            _plate = PresentableUi.Img(PresentableUi.Rect("Plate", host, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)), cap,
                new Color(0.02f, 0.08f, 0.17f, 0.36f), sliced: true); // see-through: the scene stays visible behind the timer

            // ring (left): dark track + radial fill that drains with the remaining time
            var ringHost = PresentableUi.Centered("Ring", host, new Vector2(-165f, 0f), new Vector2(118f, 118f));
            PresentableUi.Img(PresentableUi.Rect("Track", ringHost, Vector2.zero, Vector2.one), RingSprite(), new Color(0.2f, 0.4f, 0.6f, 0.35f));
            _ring = PresentableUi.Img(PresentableUi.Rect("Fill", ringHost, Vector2.zero, Vector2.one), RingSprite(), new Color(0.4f, 1f, 0.7f, 1f));
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = false;
            _ring.fillAmount = 1f;

            // pause / resume button inside the ring
            var btnRt = PresentableUi.Centered("PauseButton", ringHost, Vector2.zero, new Vector2(78f, 78f));
            var btnImg = PresentableUi.Img(btnRt, DiscSprite(), new Color(0.1f, 0.3f, 0.5f, 0.5f), raycast: true);
            var a1 = PresentableUi.Centered("BarA", btnRt, new Vector2(-9f, 0f), new Vector2(11f, 32f));
            _pauseA = PresentableUi.Img(a1, null, Color.white);
            var b1 = PresentableUi.Centered("BarB", btnRt, new Vector2(9f, 0f), new Vector2(11f, 32f));
            _pauseB = PresentableUi.Img(b1, null, Color.white);
            var p1 = PresentableUi.Centered("Play", btnRt, new Vector2(3f, 0f), new Vector2(30f, 36f));
            _play = PresentableUi.Img(p1, PlaySprite(), Color.white);
            _play.gameObject.SetActive(false);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(TogglePause);

            // label + digits (right of the ring)
            // label: soft translucent gold, readable over grey floors and white walls, no backing strip
            var lab = PresentableUi.Centered("Label", host, new Vector2(20f, 38f), new Vector2(190f, 34f));
            _label = PresentableUi.Text(lab, Loc.T("GENERAL", "TimerLabel"), 26f, new Color(1f, 0.82f, 0.36f, 0.92f), TextAlignmentOptions.Center, true);
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            var clockText = PresentableUi.Centered("Time", host, new Vector2(55f, -14f), new Vector2(270f, 84f));
            _time = PresentableUi.Text(clockText, SessionClock.Format(SessionClock.LimitSeconds), 74f, new Color(0.75f, 1f, 0.9f, 1f),
                TextAlignmentOptions.Left, true);
            _time.textWrappingMode = TextWrappingModes.NoWrap;

            // milestone flash above the capsule ("5 MINUTES LEFT")
            var fl = PresentableUi.Centered("Flash", host, new Vector2(0f, 112f), new Vector2(460f, 60f));
            _flashGroup = fl.gameObject.AddComponent<CanvasGroup>();
            _flashGroup.alpha = 0f;
            _flash = PresentableUi.Text(fl, " ", 40f, new Color(1f, 0.85f, 0.3f, 1f), TextAlignmentOptions.Center, true);
            _flash.textWrappingMode = TextWrappingModes.NoWrap;
            PresentableUi.AlwaysOnTop(canvas);
            // client: "TIME LEFT" bolder at the same font size (thicker strokes on the SDF face)
            if (_label != null && _label.fontSharedMaterial != null)
            {
                _label.fontSharedMaterial.SetFloat("_FaceDilate", 0.32f);
                _label.fontSharedMaterial.SetFloat("_OutlineWidth", 0.08f);
                _label.fontSharedMaterial.SetColor("_OutlineColor", new Color(0.12f, 0.08f, 0.02f, 0.9f));
            }

            _born = Time.unscaledTime;

            // ---- toast: 1000 x 280 px
            var to = new GameObject("Toast");
            to.transform.SetParent(transform, false);
            _toastRoot = to.transform;
            var tc = PresentableUi.WorldCanvas("ToastCanvas", _toastRoot, new Vector2(1000f, 280f), 0.0015f, false, 40);
            _toastGroup = tc.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            var th = (RectTransform)tc.transform;
            PresentableUi.Img(PresentableUi.Rect("Plate", th, Vector2.zero, Vector2.one), null, new Color(0.03f, 0.05f, 0.1f, 0.88f));
            PresentableUi.Img(PresentableUi.Rect("Line", th, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 8f)), null,
                new Color(1f, 0.55f, 0.15f, 1f));
            PresentableUi.Text("Title", th, new Vector2(0.03f, 0.45f), new Vector2(0.97f, 0.97f), Loc.T("GENERAL", "TimeUpTitle"), 110f,
                new Color(1f, 0.6f, 0.15f, 1f), TextAlignmentOptions.Center, true);
            PresentableUi.Text("Sub", th, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.45f), Loc.T("GENERAL", "TimeUpSub"), 46f,
                Color.white, TextAlignmentOptions.Center, false);
            PresentableUi.AlwaysOnTop(tc);
            _toastRoot.gameObject.SetActive(false);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
        }

        private void TogglePause()
        {
            SessionClock.SetPaused(!SessionClock.Paused);
            PresentableFx.Play(PresentableFx.Tick, 0.8f);
        }

        private void Update()
        {
            SessionClock.Tick(Time.unscaledDeltaTime);
            if (_time != null)
            {
                var remaining = SessionClock.Remaining;
                var paused = SessionClock.Paused;
                var frac = Mathf.Clamp01(remaining / Mathf.Max(1f, SessionClock.LimitSeconds));
                _time.text = SessionClock.Format(remaining);
                _ring.fillAmount = frac;

                var calm = new Color(0.4f, 1f, 0.7f, 1f);
                var warn = new Color(1f, 0.8f, 0.25f, 1f);
                var danger = new Color(1f, 0.3f, 0.28f, 1f);
                var col = remaining <= 60f ? danger : remaining <= 300f ? warn : calm;
                var pulse = 0f;
                var whole = Mathf.CeilToInt(remaining);
                if (!paused && remaining <= 60f)
                    pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (remaining <= 10f ? 9f : 4f));
                if (!paused && remaining <= 10f && whole != _lastWholeSecond && whole > 0)
                    PresentableFx.Play(PresentableFx.Tick, 0.6f);
                _lastWholeSecond = whole;

                if (paused)
                    col = new Color(1f, 0.8f, 0.3f, 1f);
                var blink = !paused || Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f;
                _ring.color = col;
                _time.color = new Color(Mathf.Lerp(col.r, 1f, 0.55f), Mathf.Lerp(col.g, 1f, 0.55f), Mathf.Lerp(col.b, 1f, 0.55f), blink ? 1f : 0.35f);
                var edge = Color.Lerp(new Color(0.35f, 0.85f, 1f, 1f), col, remaining <= 300f || paused ? 0.85f : 0f);
                _edge.color = edge;
                _glow.color = new Color(edge.r, edge.g, edge.b, 0.22f + 0.35f * pulse);
                var labelKey = paused ? "TimerPaused" : "TimerLabel";
                if (_labelKey != labelKey)
                {
                    _labelKey = labelKey;
                    _label.text = Loc.T("GENERAL", labelKey);
                }

                _pauseA.gameObject.SetActive(!paused);
                _pauseB.gameObject.SetActive(!paused);
                _play.gameObject.SetActive(paused);

                // entrance: grow in with a small overshoot, then a gentle breathing while running
                var age = Time.unscaledTime - _born;
                var sc = age < 0.7f ? PresentableFx.EaseOutBack(Mathf.Clamp01(age / 0.7f)) : 1f + 0.012f * Mathf.Sin(Time.unscaledTime * 1.6f) + 0.03f * pulse;
                _timerRoot.localScale = Vector3.one * sc;

                if (!_five && remaining <= 300f && remaining > 290f)
                {
                    _five = true;
                    StartCoroutine(FlashRoutine(Loc.T("GENERAL", "TimerFive")));
                }

                if (!_one && remaining <= 60f && remaining > 50f)
                {
                    _one = true;
                    StartCoroutine(FlashRoutine(Loc.T("GENERAL", "TimerOne")));
                }
            }

            if (SessionClock.Expired && !_fired)
            {
                _fired = true;
                TimeUp?.Invoke();
            }
        }

        private string _labelKey = "TimerLabel";

        private IEnumerator FlashRoutine(string text)
        {
            _flash.text = text;
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            var t = 0f;
            while (t < 3.2f)
            {
                t += Time.unscaledDeltaTime;
                _flashGroup.alpha = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((3.2f - t) / 0.5f);
                yield return null;
            }

            _flashGroup.alpha = 0f;
        }

        // ---- procedural sprites (no assets): rounded capsule (9-sliced), ring, disc
        private static Sprite _capsule, _ringSprite, _disc;

        private static Sprite CapsuleSprite()
        {
            if (_capsule != null)
                return _capsule;
            const int n = 96;
            const float r = 44f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                var dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f)));
            }

            tex.Apply();
            _capsule = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _capsule;
        }

        private static Sprite _edgeSprite;

        // hollow rounded outline (4 px) so only the neon border is solid and the inside stays see-through
        private static Sprite EdgeSprite()
        {
            if (_edgeSprite != null)
                return _edgeSprite;
            const int n = 96;
            const float r = 44f;
            const float w = 4f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                var dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                var d = Mathf.Sqrt(dx * dx + dy * dy); // distance past the straight edge, in the corner area
                var outer = Mathf.Clamp01(r - d + 0.5f);
                var edgeDist = Mathf.Min(Mathf.Min(x + 0.5f, n - x - 0.5f), Mathf.Min(y + 0.5f, n - y - 0.5f));
                var inner = d > 0f ? Mathf.Clamp01(d - (r - w) + 0.5f) : Mathf.Clamp01(w - edgeDist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, outer * inner));
            }

            tex.Apply();
            _edgeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _edgeSprite;
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null)
                return _ringSprite;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                var a = Mathf.Clamp01(n * 0.5f - d) * Mathf.Clamp01(d - (n * 0.5f - 22f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }

        private static Sprite DiscSprite()
        {
            if (_disc != null)
                return _disc;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(n * 0.5f - d)));
            }

            tex.Apply();
            _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _disc;
        }

        private void LateUpdate()
        {
            if (_cam == null)
                return;
            // fixed in the view: the whole offset rotates with the head, with a short smoothing so it does not jitter
            var k = _snapped ? 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime) : 1f;
            Place(_timerRoot, TimerOffset, k);
            if (_toastRoot.gameObject.activeSelf)
                Place(_toastRoot, ToastOffset, k);
            _snapped = true;
        }

        private void Place(Transform t, Vector3 offset, float k)
        {
            var pos = _cam.position + _cam.rotation * offset;
            var rot = Quaternion.LookRotation(pos - _cam.position, _cam.up);
            t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, k), Quaternion.Slerp(t.rotation, rot, k));
        }

        /// <summary>"Time is up" toast at the centre of the view with the voice-over; returns when it has faded out.</summary>
        public IEnumerator Toast(float holdSeconds)
        {
            _toastRoot.gameObject.SetActive(true);
            Place(_toastRoot, ToastOffset, 1f);
            var clip = Resources.Load<AudioClip>("VoSession/timeup_" + Loc.Suffix());
            var len = 0f;
            if (clip != null)
            {
                _audio.PlayOneShot(clip);
                len = clip.length;
            }
            else
                Debug.LogWarning("[SessionHud] no time-up voice-over clip");

            PresentableFx.Play(PresentableFx.Whoosh, 0.7f);
            var t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                var e = Mathf.Clamp01(t / 0.4f);
                _toastGroup.alpha = e;
                _toastRoot.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, e);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(holdSeconds, len + 0.4f));
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                _toastGroup.alpha = 1f - Mathf.Clamp01(t / 0.5f);
                yield return null;
            }

            _toastRoot.gameObject.SetActive(false);
        }
    }
}
