using System;
using System.Collections;
using ChemicalSafety.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Client design "BACK | NEXT | PAUSE | timer": one glass control bar fixed at the TOP-CENTRE of the view (it follows the head, so it stays
    /// in the same place when the learner turns, sits or stands; a little above the eye line so no head tilt is needed). The learner can
    /// drag it with the controller ray (hold the trigger on the bar, not on a button); the new place is kept for the session.
    /// Back / Next go to <see cref="ScenarioNav"/>, Pause / Resume to <see cref="SessionClock"/>. Also hosts the milestone flash and the
    /// "time is up" toast (screen centre, with voice-over). The clock itself is <see cref="SessionClock"/>; this only draws and ticks it.
    /// </summary>
    public sealed class SessionHud : MonoBehaviour
    {
        // metres in the view: right, up, forward of the head. Default = top centre, ~24 degrees above (it was 29 and fell outside a normal game view) the eye line (2026-11-17: it used to sit at ~10 degrees
        // and ran through every instruction card / MCQ box / popup, whose top edges reach 17-31 degrees). Quest vertical field of view is about +-48 degrees.
        private static readonly Vector3 DefaultBarOffset = new Vector3(0f, 0.46f, 0.9f); // 2026-11-18: a little higher (was 0.40) so the cards / boxes under it have room
        private static Vector3 _barOffset = DefaultBarOffset; // survives a re-created bar (jump), reset by ResetBarPosition
        private static readonly Vector3 ToastOffset = new Vector3(0f, 0.02f, 1.5f);

        public static SessionHud Instance { get; private set; }

        public event Action TimeUp;

        private const float BarW = 960f;
        private const float BarH = 120f;
        private const float BarScale = 0.00085f; // 0.82 m wide at 0.9 m, ~50 degrees

        private Transform _cam;
        private Transform _timerRoot;
        private Transform _toastRoot;
        private TextMeshProUGUI _time;
        private TextMeshProUGUI _pauseText;
        private Image _pauseIcon;
        private Image _pauseFrame;
        private Image _timerFrame;
        private Sprite _pauseSprite;
        private Sprite _playSprite;
        private TextMeshProUGUI _flash;
        private CanvasGroup _flashGroup;
        private RectTransform _backRt;
        private CanvasGroup _backGroup;
        private CanvasGroup _nextGroup;
        private float _born;
        private int _lastWholeSecond = -1;
        private bool _five, _one;
        private CanvasGroup _timerGroup;
        private CanvasGroup _toastGroup;
        private AudioSource _audio;
        private bool _fired;
        private bool _snapped;
        private bool _visible = true;
        private bool _busy;
        private bool _lastPaused;

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

        /// <summary>Puts the bar back to the default top-centre place.</summary>
        public static void ResetBarPosition() => _barOffset = DefaultBarOffset;

        /// <summary>Height of the bar's lower edge above the eye line per metre of distance (a box at distance d must keep its top below d * this).</summary>
        public static float BarBottomRatio => (_barOffset.y - BarH * BarScale * 0.5f) / Mathf.Max(0.3f, _barOffset.z);

        /// <summary>Same, but the bar is head-locked: it sinks when the learner looks down. 8 degrees of looking down are allowed for, so a box placed below this stays under the bar for a normal glance at the drums / table.</summary>
        public static float SafeBarRatio => Mathf.Tan(Mathf.Atan(BarBottomRatio) - 8f * Mathf.Deg2Rad);

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_timerGroup != null)
            {
                _timerGroup.alpha = visible ? 1f : 0f;
                _timerGroup.blocksRaycasts = visible;
            }
        }

        /// <summary>True while a scenario jump is running: Back / Next ignore presses (no double jump).</summary>
        public void SetBusy(bool busy) => _busy = busy;

        internal static Sprite PlaySprite()
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

        // one button of the bar: the client's frame, the icon on one side and the label in the rest, all inset from the frame edge
        private RectTransform MakeButton(RectTransform host, string name, float x, float w, Sprite icon, bool iconLeft, string label,
            UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI text, out Image iconImg, out Image frame)
        {
            const float h = 74f;
            var rt = PresentableUi.Centered(name, host, new Vector2(x, 0f), new Vector2(w, h));
            frame = PresentableUi.Img(rt, HudArt.Get(name == "Pause" ? "Ui/Hud/04_pause_button_frame" : name == "Back" ? "Ui/Hud/02_back_button_frame"
                : "Ui/Hud/03_next_button_frame"), new Color(1f, 1f, 1f, 0.92f), raycast: true);
            const float pad = 26f; // inside the frame's own border
            const float iconSize = 34f;
            var iconX = iconLeft ? -w * 0.5f + pad + iconSize * 0.5f : w * 0.5f - pad - iconSize * 0.5f;
            var iconRt = PresentableUi.Centered("Icon", rt, new Vector2(iconX, 0f), new Vector2(iconSize, iconSize));
            iconImg = PresentableUi.Img(iconRt, icon, new Color(0.9f, 0.98f, 1f, 1f), preserveAspect: true);
            // label fills the space beside the icon with a margin on both sides
            var labelW = w - pad * 2f - iconSize - 14f;
            var labelX = iconLeft ? (iconSize + 14f) * 0.5f : -(iconSize + 14f) * 0.5f;
            var labelRt = PresentableUi.Centered("Label", rt, new Vector2(labelX, 1f), new Vector2(labelW, 46f));
            text = PresentableUi.Text(labelRt, label, 34f, Color.white, TextAlignmentOptions.Center, true);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax = 34f;
            text.fontSizeMin = 18f;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            var lift = rt.gameObject.AddComponent<HoverLift>();
            lift.Init(rt);
            button.onClick.AddListener(lift.Press);
            button.onClick.AddListener(onClick);
            return rt;
        }

        private void Build()
        {
            var tr = new GameObject("Bar");
            tr.transform.SetParent(transform, false);
            _timerRoot = tr.transform;
            var canvas = PresentableUi.WorldCanvas("BarCanvas", _timerRoot, new Vector2(BarW, BarH), BarScale, true, 30);
            _timerGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            FingerPokeSystem.RefreshGroup(canvas);
            var host = (RectTransform)canvas.transform;

            // glass base: the client's panel, see-through so the room stays visible behind it
            PresentableUi.Img(PresentableUi.Rect("Glass", host, Vector2.zero, Vector2.one), HudArt.Get("Ui/Hud/01_blank_popup_panel"),
                new Color(1f, 1f, 1f, 0.55f), raycast: true);

            // buttons, left to right as in the design (centres measured from the bar centre)
            _backRt = MakeButton(host, "Back", -362f, 196f, HudArt.Get("Ui/Hud/06_back_chevrons_icon"), true, Loc.T("GENERAL", "BarBack"),
                () => { if (!_busy && _visible) ScenarioNav.RequestPrevious(); }, out _, out _, out _);
            _backGroup = _backRt.gameObject.AddComponent<CanvasGroup>();
            var nextRt = MakeButton(host, "Next", -150f, 200f, HudArt.Get("Ui/Hud/07_next_chevrons_icon"), false, Loc.T("GENERAL", "BarNext"),
                () => { if (!_busy && _visible) ScenarioNav.RequestNext(); }, out _, out _, out _);
            _nextGroup = nextRt.gameObject.AddComponent<CanvasGroup>();
            _pauseSprite = HudArt.Get("Ui/Hud/08_pause_icon");
            _playSprite = PlaySprite();
            MakeButton(host, "Pause", 107f, 232f, _pauseSprite, false, Loc.T("GENERAL", "BarPause"), TogglePause, out _pauseText, out _pauseIcon, out _pauseFrame);

            // timer
            var timerRt = PresentableUi.Centered("Timer", host, new Vector2(369f, 0f), new Vector2(172f, 74f));
            _timerFrame = PresentableUi.Img(timerRt, HudArt.Get("Ui/Hud/05_timer_display_frame"), new Color(1f, 1f, 1f, 0.95f));
            var clockRt = PresentableUi.Centered("Time", timerRt, new Vector2(0f, 1f), new Vector2(124f, 50f));
            _time = PresentableUi.Text(clockRt, SessionClock.Format(SessionClock.LimitSeconds), 52f, Color.white, TextAlignmentOptions.Center, true);
            _time.textWrappingMode = TextWrappingModes.NoWrap;
            _time.enableAutoSizing = true;
            _time.fontSizeMax = 52f;
            _time.fontSizeMin = 28f;

            // milestone flash below the bar ("5 MINUTES LEFT")
            var fl = PresentableUi.Centered("Flash", host, new Vector2(0f, -96f), new Vector2(700f, 60f));
            _flashGroup = fl.gameObject.AddComponent<CanvasGroup>();
            _flashGroup.alpha = 0f;
            _flash = PresentableUi.Text(fl, " ", 40f, new Color(1f, 0.85f, 0.3f, 1f), TextAlignmentOptions.Center, true);
            _flash.textWrappingMode = TextWrappingModes.NoWrap;
            // client 2026-11-19: the bar text was hidden behind the fume hood's top edge when the learner stood at the hood (TextMeshPro text is depth tested):
            // the same Times New Roman with the overlay shader is drawn on top of everything; only the bar uses it
            var hudFont = Resources.Load<TMP_FontAsset>("Fonts/TimesNewRoman HudOverlay SDF");
            if (hudFont != null)
                foreach (var tmp in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                    tmp.font = hudFont;
            PresentableUi.AlwaysOnTop(canvas);

            // drag the whole bar with the ray (a press on a button still clicks it)
            var drag = DraggablePanel.Attach(this);
            drag.OnHeadOffset = o =>
            {
                _barOffset = new Vector3(Mathf.Clamp(o.x, -0.7f, 0.7f), Mathf.Clamp(o.y, -0.45f, 0.9f), Mathf.Clamp(o.z, 0.6f, 1.4f));
            };
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
                _time.text = SessionClock.Format(remaining);

                var calm = Color.white;
                var warn = new Color(1f, 0.82f, 0.4f, 1f);
                var danger = new Color(1f, 0.45f, 0.4f, 1f);
                var col = remaining <= 60f ? danger : remaining <= 300f ? warn : calm;
                var pulse = 0f;
                var whole = Mathf.CeilToInt(remaining);
                if (!paused && remaining <= 60f)
                    pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (remaining <= 10f ? 9f : 4f));
                if (!paused && remaining <= 10f && whole != _lastWholeSecond && whole > 0)
                    PresentableFx.Play(PresentableFx.Tick, 0.6f);
                _lastWholeSecond = whole;

                if (paused)
                    col = warn;
                var blink = !paused || Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f;
                _time.color = new Color(col.r, col.g, col.b, blink ? 1f : 0.35f);
                // timer frame tint: calm = the blue of the design, then amber / red when time runs short
                _timerFrame.color = Color.Lerp(new Color(1f, 1f, 1f, 0.95f), new Color(col.r, col.g * 0.8f, col.b * 0.7f, 1f),
                    (remaining <= 300f || paused ? 0.55f : 0f) + 0.3f * pulse);

                if (paused != _lastPaused)
                {
                    _lastPaused = paused;
                    _pauseText.text = Loc.T("GENERAL", paused ? "BarResume" : "BarPause");
                    _pauseIcon.sprite = paused ? _playSprite : _pauseSprite;
                }

                // Back: not there at the first scenario (client); Back / Next dim while a jump runs or at the limits
                var showPrev = ScenarioNav.CanPrevious;
                if (_backRt.gameObject.activeSelf != showPrev)
                    _backRt.gameObject.SetActive(showPrev);
                _backGroup.alpha = ScenarioNav.CanPrevious && !_busy ? 1f : 0.4f;
                _nextGroup.alpha = ScenarioNav.CanNext && !_busy ? 1f : 0.4f;

                // entrance: grow in with a small overshoot, then a gentle pulse only when the time is short
                var age = Time.unscaledTime - _born;
                var sc = age < 0.7f ? PresentableFx.EaseOutBack(Mathf.Clamp01(age / 0.7f)) : 1f;
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

        private void LateUpdate()
        {
            if (_cam == null)
                return;
            // fixed in the view: the whole offset rotates with the head, with a short smoothing so it does not jitter
            var k = _snapped ? 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime) : 1f;
            Place(_timerRoot, _barOffset, k);
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
