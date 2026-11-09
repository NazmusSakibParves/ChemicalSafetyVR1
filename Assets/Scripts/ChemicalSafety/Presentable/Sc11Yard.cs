using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 11 (gas cylinder leak in the yard). Hosts everything that moves: the leaking valve (hiss, frost, white then purple
    /// gas), the neon icons (siren / call supervisor, touched with a finger like the SC10 icons), the alarm (beacons, siren, the
    /// workers evacuating through the exit gate) and the wireless call outside the fence (walkie-talkie in hand, blinking
    /// CALLING screen, key click, static, call tone, roger beep, a supervisor voice that is band-limited like a real radio).
    /// Scripted only, no rigidbodies. The icon canvas must keep its world scale 0.00085.
    /// </summary>
    public sealed class Sc11Yard : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;

        [Header("Leak")]
        [SerializeField] private ParticleSystem leakGas;
        [SerializeField] private ParticleSystem leakGasPurple;
        [SerializeField] private Renderer[] frost = new Renderer[0];

        [Header("Alarm and evacuation")]
        [SerializeField] private Sc07Beacon[] beacons = new Sc07Beacon[0];
        [SerializeField] private GameObject exitSign;
        [SerializeField] private Sc07Human[] workers = new Sc07Human[0];
        [SerializeField] private Transform[] workerStarts = new Transform[0];
        [SerializeField] private Transform[] exitPath = new Transform[0];

        [Header("Wireless")]
        [SerializeField] private Transform radio;          // the walkie-talkie model, scaled and oriented by the builder
        [SerializeField] private Transform radioScreen;    // where the LCD text sits (child of the radio)
        [SerializeField] private GameObject outsideRoot;

        [Header("Sound")]
        [SerializeField] private AudioClip windLoop;
        [SerializeField] private AudioClip hissLoop;
        [SerializeField] private AudioClip sirenLoop;
        [SerializeField] private AudioClip staticLoop;
        [SerializeField] private AudioClip squelch;
        [SerializeField] private AudioClip callTone;
        [SerializeField] private AudioClip roger;
        [SerializeField] private AudioClip click;
        [SerializeField] private AudioClip murmurLoop;
        [SerializeField] private AudioClip[] crowdBn = new AudioClip[0];
        [SerializeField] private AudioClip[] crowdEn = new AudioClip[0];
        [SerializeField] private Transform hissAt;
        [SerializeField] private Transform sirenAt;

        private AudioSource _wind;
        private AudioSource _hiss;
        private AudioSource _siren;
        private AudioSource _static;
        private AudioSource _murmur;
        private AudioSource _sfx;
        private AudioSource _radioVoice;

        private Transform _icon;
        private PokeTarget _iconPoke;
        private Image _iconImg;
        private bool _iconPoked;

        private Transform _map;
        private Transform _mapPulse;
        private Image _mapDot;
        private bool _mapOn;
        private float _mapK;
        private TextMeshProUGUI _lcd;
        private Image _txDot;
        private bool _calling;
        private bool _talking;
        private bool _radioHeld;
        private float _radioRise;
        private bool _on;
        private float _gasLevel = 1f;
        private float _purple;
        private float _hissTarget = 0.5f;
        private bool _alarm;

        public bool AlarmOn => _alarm;
        public bool IconVisible => _icon != null && _icon.gameObject.activeSelf;
        public PokeTarget IconPoke => _iconPoke;
        public bool RadioVisible => radio != null && radio.gameObject.activeSelf;
        public bool Calling => _calling;
        public bool SirenPlaying => _siren != null && _siren.isPlaying && _siren.volume > 0.05f;
        public int WorkersOut { get; private set; }
        public int WorkerCount => workers.Length;
        public bool ReplyPlaying => _radioVoice != null && _radioVoice.isPlaying;
        public bool ExitShown => exitSign != null && exitSign.activeSelf;
        public float PurpleAmount => _purple;

        private void Awake()
        {
            _wind = MakeSource("Wind", windLoop, true, 0f, 0f);
            _hiss = MakeSource("Hiss", hissLoop, true, 1f, 0f, hissAt);
            _siren = MakeSource("Siren", sirenLoop, true, 0.8f, 0f, sirenAt);
            _static = MakeSource("RadioStatic", staticLoop, true, 0f, 0f);
            _murmur = MakeSource("Murmur", murmurLoop, true, 0.6f, 0f, workerStarts.Length > 0 ? workerStarts[0] : null);
            _sfx = MakeSource("Sfx", null, false, 0f, 1f);
            _radioVoice = MakeSource("RadioVoice", null, false, 0f, 1f);
            // a real handset: no bass, no air, a little grit
            var hp = _radioVoice.gameObject.AddComponent<AudioHighPassFilter>();
            hp.cutoffFrequency = 520f;
            var lp = _radioVoice.gameObject.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 3000f;
            var ds = _radioVoice.gameObject.AddComponent<AudioDistortionFilter>();
            ds.distortionLevel = 0.32f;
        }

        private AudioSource MakeSource(string n, AudioClip clip, bool loop, float spatial, float vol, Transform at = null)
        {
            var go = new GameObject("Sc11_" + n);
            go.transform.SetParent(at != null ? at : transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.minDistance = 1.5f;
            s.maxDistance = 30f;
            s.volume = vol;
            return s;
        }

        // ------------------------------------------------------------------ lifetime

        public void Begin()
        {
            _on = true;
            ResetAll();
            if (windLoop != null) _wind.Play();
            if (hissLoop != null) _hiss.Play();
        }

        public void End()
        {
            _on = false;
            foreach (var s in new[] { _wind, _hiss, _siren, _static, _radioVoice, _murmur })
                if (s != null) s.Stop();
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            _alarm = false;
            _calling = false;
            _talking = false;
            _radioHeld = false;
            _radioRise = 0f;
            _purple = 0f;
            _gasLevel = 1f;
            _hissTarget = 0.5f;
            WorkersOut = 0;
            foreach (var b in beacons)
                if (b != null) b.SetOn(false);
            if (exitSign != null) exitSign.SetActive(false);
            if (_siren != null) { _siren.Stop(); _siren.volume = 0f; }
            if (_static != null) { _static.Stop(); _static.volume = 0f; }
            if (outsideRoot != null) outsideRoot.SetActive(false);
            if (radio != null) radio.gameObject.SetActive(false);
            if (_icon != null) _icon.gameObject.SetActive(false);
            _mapOn = false;
            if (_map != null) _map.gameObject.SetActive(false);
            for (var i = 0; i < workers.Length; i++)
            {
                if (workers[i] == null) continue;
                workers[i].gameObject.SetActive(true);
                if (i < workerStarts.Length && workerStarts[i] != null)
                    workers[i].transform.SetPositionAndRotation(workerStarts[i].position, workerStarts[i].rotation);
                workers[i].SnapPose(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }
        }

        public void ShowExit(bool on)
        {
            if (exitSign != null)
                exitSign.SetActive(on);
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!_on)
                return;
            var dt = Time.deltaTime;
            if (_wind != null) _wind.volume = Mathf.MoveTowards(_wind.volume, _radioHeld ? 0.12f : 0.32f, dt * 0.5f);
            if (_hiss != null) _hiss.volume = Mathf.MoveTowards(_hiss.volume, _radioHeld ? 0f : _hissTarget * _gasLevel, dt * 0.6f);

            if (leakGas != null)
            {
                var em = leakGas.emission;
                em.rateOverTimeMultiplier = 26f * _gasLevel * (1f - _purple * 0.5f);
            }

            if (leakGasPurple != null)
            {
                var em = leakGasPurple.emission;
                em.rateOverTimeMultiplier = 16f * _purple * _gasLevel;
            }

            UpdateRadio(dt);
            UpdateMap(dt);
        }

        /// <summary>The location-map card at the lower left of the view while the radio call runs ("location shared").</summary>
        public void ShowMap(bool on)
        {
            _mapOn = on;
            if (on)
                EnsureMap();
        }

        public bool MapVisible => _map != null && _map.gameObject.activeSelf;

        private void EnsureMap()
        {
            if (_map != null || art == null || art.minimapSc11 == null)
                return;
            var canvas = PresentableUi.WorldCanvas("Sc11Map", transform, new Vector2(300f, 372f), 0.0007f, false, 70);
            _map = canvas.transform;
            PresentableUi.Img("Back", canvas.transform, Vector2.zero, Vector2.one, null, new Color(0.03f, 0.08f, 0.12f, 0.82f), true);
            PresentableUi.Img("Map", canvas.transform, new Vector2(0.03f, 0.17f), new Vector2(0.97f, 0.98f), art.minimapSc11, Color.white, true);
            var pin = new Vector2(0.62f, 0.67f);
            var pulse = PresentableUi.Img("Pulse", canvas.transform, pin - new Vector2(0.14f, 0.11f), pin + new Vector2(0.14f, 0.11f), art.radialGlow,
                new Color(0.2f, 0.7f, 1f, 0.7f));
            _mapPulse = pulse.transform;
            PresentableUi.Img("Pin", canvas.transform, pin - new Vector2(0.05f, 0.04f), pin + new Vector2(0.05f, 0.04f), art.radialGlow, new Color(0.15f, 0.55f, 1f, 1f));
            var you = PresentableUi.Text("You", canvas.transform, pin + new Vector2(0.06f, -0.03f), pin + new Vector2(0.34f, 0.05f), Sc11Content.MapYou, 30f,
                Color.white, TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            you.name = "You";
            _mapDot = PresentableUi.Img("Dot", canvas.transform, new Vector2(0.06f, 0.05f), new Vector2(0.14f, 0.12f), art.radialGlow, new Color(0.2f, 1f, 0.4f, 1f));
            var lbl = PresentableUi.Text("Shared", canvas.transform, new Vector2(0.16f, 0.0f), new Vector2(0.98f, 0.17f), Sc11Content.MapShared, 40f,
                new Color(0.45f, 1f, 0.6f), TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            lbl.enableAutoSizing = true;
            lbl.fontSizeMin = 20f;
            lbl.fontSizeMax = 40f;
            lbl.name = "Shared";
            PresentableUi.AlwaysOnTop(canvas);
            _map.gameObject.SetActive(false);
        }

        private void UpdateMap(float dt)
        {
            if (_map == null)
                return;
            _mapK = Mathf.MoveTowards(_mapK, _mapOn ? 1f : 0f, dt * 2.2f);
            var show = _mapK > 0.01f;
            if (_map.gameObject.activeSelf != show)
                _map.gameObject.SetActive(show);
            if (!show)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return;
            var flat = new Vector3(cam.forward.x, 0f, cam.forward.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, flat);
            var target = cam.position + flat * 0.62f - right * 0.21f + Vector3.down * 0.19f;
            _map.position = Vector3.Lerp(_map.position, target, 1f - Mathf.Exp(-9f * dt));
            _map.rotation = Quaternion.LookRotation(_map.position - cam.position, Vector3.up);
            _map.localScale = Vector3.one * (0.0007f * Mathf.SmoothStep(0f, 1f, _mapK));
            if (_mapPulse != null)
            {
                var p = Mathf.Repeat(Time.time * 0.9f, 1f);
                _mapPulse.localScale = Vector3.one * (0.5f + p * 1.5f);
                var img = _mapPulse.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(0.2f, 0.7f, 1f, 0.7f * (1f - p));
            }

            if (_mapDot != null)
                _mapDot.color = new Color(0.2f, 1f, 0.4f, Mathf.Sin(Time.time * 6f) > 0f ? 1f : 0.3f);
        }

        private void UpdateRadio(float dt)
        {
            if (radio == null || !radio.gameObject.activeSelf)
                return;
            _radioRise = Mathf.MoveTowards(_radioRise, _radioHeld ? 1f : 0f, dt * 1.3f);
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return;
            var fwd = cam.forward;
            var flat = new Vector3(fwd.x, 0f, fwd.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, flat);
            var k = Mathf.SmoothStep(0f, 1f, _radioRise);
            // held in the right-hand side of the view like the workflow picture: antenna up, screen toward the face
            var sway = new Vector3(Mathf.Sin(Time.time * 1.7f) * 0.004f, Mathf.Sin(Time.time * 2.3f) * 0.003f, 0f);
            var held = cam.position + flat * 0.42f + right * 0.15f + Vector3.down * 0.20f + sway;
            var low = cam.position + flat * 0.38f + right * 0.22f + Vector3.down * 0.55f;
            radio.position = Vector3.Lerp(low, held, k);
            radio.rotation = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(-14f, 8f, 0f) * Quaternion.Euler(0f, -90f, 0f);

            if (_lcd != null)
            {
                var blink = _calling && !_talking ? (Mathf.Sin(Time.time * 7f) > -0.2f ? 1f : 0.25f) : 1f;
                _lcd.alpha = blink;
            }

            if (_txDot != null)
                _txDot.color = _talking ? new Color(1f, 0.15f, 0.1f, 0.6f + 0.4f * Mathf.Sin(Time.time * 12f)) : new Color(0.1f, 0.9f, 0.3f, 0.85f);
        }

        // ------------------------------------------------------------------ neon icons

        private void EnsureIcon()
        {
            if (_icon != null || art == null)
                return;
            var canvas = PresentableUi.WorldCanvas("Sc11Icon", transform, new Vector2(520f, 640f), 0.00085f, false, 40);
            _icon = canvas.transform;
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", canvas.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), art.radialGlow, new Color(0.2f, 1f, 0.4f, 0.22f));
            _iconImg = PresentableUi.Img("Icon", canvas.transform, new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.92f), null, Color.white, true);
            var label = PresentableUi.Text("Label", canvas.transform, new Vector2(0.0f, 0.0f), new Vector2(1f, 0.26f), "", 74f,
                new Color(0.35f, 1f, 0.5f), TextAlignmentOptions.Center, true, art.englishFont);
            label.enableAutoSizing = true;
            label.fontSizeMin = 34f;
            label.fontSizeMax = 72f;
            label.name = "Label";
            PresentableUi.AlwaysOnTop(canvas);
            var col = _icon.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(520f, 640f, 200f);
            _iconPoke = _icon.gameObject.AddComponent<PokeTarget>();
            _iconPoke.SetInteractable(false);
            _iconPoke.Poked += _ => _iconPoked = true;
            _icon.gameObject.SetActive(false);
        }

        /// <summary>Shows a neon icon 0.62 m in front of the user and waits until a finger touches it.</summary>
        public IEnumerator WaitIcon(Sprite icon, string label, float autoClickAfter = 0f)
        {
            EnsureIcon();
            if (_icon == null)
                yield break;
            _iconImg.sprite = icon;
            _iconImg.enabled = icon != null;
            var tmp = _icon.Find("Label") != null ? _icon.Find("Label").GetComponent<TextMeshProUGUI>() : null;
            if (tmp != null)
                tmp.text = label;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? new Vector3(cam.forward.x, 0f, cam.forward.z).normalized : Vector3.forward;
            var head = cam != null ? cam.position : _icon.position;
            _icon.position = head + fwd * 0.62f + Vector3.down * 0.10f;
            _icon.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _iconPoked = false;
            const float baseScale = 0.00085f;
            _icon.localScale = Vector3.zero;
            _icon.gameObject.SetActive(true);
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                _icon.localScale = Vector3.one * (baseScale * Mathf.SmoothStep(0f, 1f, t / 0.35f));
                yield return null;
            }

            // a finger already resting where the icon appears must not trigger it
            for (var wait = 0f; wait < 6f; wait += Time.deltaTime)
            {
                var hand = GlovedHandPose.Right;
                if (hand == null || !hand.HasTip || (hand.TipPosition - _icon.position).magnitude > 0.3f)
                    break;
                yield return null;
            }

            yield return PresentableFx.Wait(0.3f);
            _iconPoke.SetInteractable(true);
            var shown = 0f;
            // autoClickAfter > 0 (controller-ray yard): the icon presses itself after that many seconds, nobody has to click
            while (!_iconPoked && !(autoClickAfter > 0f && shown >= autoClickAfter))
            {
                shown += Time.deltaTime;
                _icon.localScale = Vector3.one * (baseScale * (1f + 0.05f * Mathf.Sin(Time.time * 4f)));
                yield return null;
            }

            _iconPoke.SetInteractable(false);
            PresentableFx.Haptic(true, 0.5f, 0.1f);
            PresentableFx.Play(PresentableFx.Success, 0.6f);
            for (var t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                _icon.localScale = Vector3.one * (baseScale * (1f + 0.25f * Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f)) * (1f - t / 0.25f));
                yield return null;
            }

            _icon.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ alarm and evacuation

        /// <summary>Beacons flash, the siren wails, the gas thickens and turns purple.</summary>
        public void RaiseAlarm()
        {
            _alarm = true;
            foreach (var b in beacons)
                if (b != null) b.SetOn(true);
            if (sirenLoop != null && _siren != null)
            {
                _siren.volume = 0f;
                _siren.Play();
                StartCoroutine(PresentableFx.Tween(1.6f, v => _siren.volume = 0.55f * v));
            }

            StartCoroutine(PresentableFx.Tween(5f, v => _purple = v));
            _gasLevel = 1.25f;
            _hissTarget = 0.8f;
            foreach (var f in frost)
                if (f != null) f.enabled = true;
        }

        /// <summary>The five workers walk out along the exit path one after another; returns when the last one is through the gate.</summary>
        public IEnumerator Evacuate()
        {
            WorkersOut = 0;
            StartCoroutine(CrowdNoise());
            var pts = new Vector3[exitPath.Length];
            for (var i = 0; i < pts.Length; i++)
                pts[i] = exitPath[i].position;
            for (var i = 0; i < workers.Length; i++)
            {
                if (workers[i] != null)
                    StartCoroutine(WorkerOut(workers[i], pts, i));
                yield return PresentableFx.Wait(0.55f);
            }

            var guard = 0f;
            while (WorkersOut < workers.Length && guard < 40f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>The crowd noise while everybody leaves: a murmur bed plus panicked shouts in the language of the session.</summary>
        private IEnumerator CrowdNoise()
        {
            if (murmurLoop != null && _murmur != null)
            {
                _murmur.volume = 0f;
                _murmur.Play();
            }

            var t = 0f;
            var next = 0.4f;
            while (WorkersOut < workers.Length && t < 40f)
            {
                t += Time.deltaTime;
                if (_murmur != null)
                    _murmur.volume = Mathf.MoveTowards(_murmur.volume, 0.42f, Time.deltaTime * 0.5f);
                next -= Time.deltaTime;
                if (next <= 0f)
                {
                    next = Random.Range(0.9f, 1.8f);
                    var set = ChemicalSafety.Localization.Loc.IsBangla ? crowdBn : crowdEn;
                    if (set.Length > 0)
                    {
                        var who = workers[Random.Range(0, workers.Length)];
                        var clip = set[Random.Range(0, set.Length)];
                        if (who != null && clip != null && who.gameObject.activeSelf)
                            AudioSource.PlayClipAtPoint(clip, who.transform.position + Vector3.up * 1.5f, 0.55f);
                    }
                }

                yield return null;
            }

            while (_murmur != null && _murmur.volume > 0.01f)
            {
                _murmur.volume = Mathf.MoveTowards(_murmur.volume, 0f, Time.deltaTime * 0.6f);
                yield return null;
            }

            if (_murmur != null)
                _murmur.Stop();
        }

        private IEnumerator WorkerOut(Sc07Human w, Vector3[] pts, int index)
        {
            var offset = new Vector3(((index % 3) - 1) * 0.35f, 0f, 0f);
            var shifted = new Vector3[pts.Length];
            for (var i = 0; i < pts.Length; i++)
                shifted[i] = pts[i] + (i < pts.Length - 1 ? offset : Vector3.zero);
            yield return w.WalkPath(shifted, 1.45f, 0.1f, 260f, false);
            w.gameObject.SetActive(false);
            WorkersOut++;
        }

        // ------------------------------------------------------------------ the wireless call

        /// <summary>Moves the user outside the fence: meadow on, the leak is only a faint plume far away.</summary>
        public void ShowOutside(bool on)
        {
            if (outsideRoot != null)
                outsideRoot.SetActive(on);
        }

        public IEnumerator RadioRaise()
        {
            if (radio == null)
                yield break;
            _radioRise = 0f;
            _radioHeld = false;
            radio.gameObject.SetActive(true);
            EnsureLcd();
            SetLcd(true);
            Shot(click, 0.9f);
            _radioHeld = true;
            yield return PresentableFx.Wait(1.0f);
        }

        /// <summary>The ring-out: key-up click, squelch, call chirp, the screen blinks CALLING while static hisses.</summary>
        public IEnumerator RadioDial()
        {
            _calling = true;
            if (staticLoop != null)
            {
                _static.volume = 0f;
                _static.Play();
                StartCoroutine(PresentableFx.Tween(0.4f, v => _static.volume = 0.30f * v));
            }

            Shot(squelch, 0.8f);
            yield return PresentableFx.Wait(0.5f);
            for (var i = 0; i < 2; i++)
            {
                Shot(callTone, 0.9f);
                PresentableFx.Haptic(false, 0.25f, 0.2f);
                yield return PresentableFx.Wait(1.25f);
            }
        }

        public void RadioTalk(bool on)
        {
            _talking = on;
            _calling = !on && _calling;
            if (on) Shot(click, 0.8f);
        }

        /// <summary>The supervisor answers: squelch, the voice through the radio filter, roger beep, squelch.</summary>
        public IEnumerator RadioReply(AudioClip voiceClip)
        {
            _calling = false;
            Shot(squelch, 0.7f);
            yield return PresentableFx.Wait(0.35f);
            if (voiceClip != null && _radioVoice != null)
            {
                if (voiceClip.loadState != AudioDataLoadState.Loaded)
                    voiceClip.LoadAudioData();
                _radioVoice.clip = voiceClip;
                _radioVoice.volume = 1f;
                _radioVoice.Play();
                var w = 0f;
                while (!_radioVoice.isPlaying && w < 3f)
                {
                    w += Time.unscaledDeltaTime;
                    yield return null;
                }

                w = 0f;
                while (_radioVoice.isPlaying && w < 30f)
                {
                    w += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return PresentableFx.Wait(2.5f);
            }

            Shot(roger, 0.8f);
            yield return PresentableFx.Wait(0.5f);
            Shot(squelch, 0.6f);
            yield return PresentableFx.Wait(0.35f);
            StartCoroutine(PresentableFx.Tween(0.6f, v => _static.volume = 0.30f * (1f - v)));
        }

        public void RadioHide()
        {
            _radioHeld = false;
            _calling = false;
            _talking = false;
            if (radio != null)
                radio.gameObject.SetActive(false);
            if (_static != null)
                _static.Stop();
            _mapOn = false;
        }

        private void EnsureLcd()
        {
            if (_lcd != null || radioScreen == null || art == null)
                return;
            var canvas = PresentableUi.WorldCanvas("Sc11Lcd", radioScreen, new Vector2(400f, 270f), 0.000125f, false, 60);
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // the radio front looks along -X
            PresentableUi.Img("Bg", canvas.transform, Vector2.zero, Vector2.one, null, new Color(0.80f, 0.86f, 0.80f, 1f), true);
            _lcd = PresentableUi.Text("Text", canvas.transform, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.95f), "CALLING:\nSUPERVISOR", 62f,
                new Color(0.05f, 0.08f, 0.05f), TextAlignmentOptions.Center, true, art.englishFont);
            _lcd.enableAutoSizing = true;
            _lcd.fontSizeMin = 30f;
            _lcd.fontSizeMax = 70f;
            _lcd.fontStyle = FontStyles.Bold;
            _txDot = PresentableUi.Img("Tx", canvas.transform, new Vector2(0.86f, 0.80f), new Vector2(0.96f, 0.96f), art.radialGlow, new Color(0.1f, 0.9f, 0.3f, 0.85f));
        }

        private void SetLcd(bool on)
        {
            if (_lcd != null)
                _lcd.transform.parent.gameObject.SetActive(on);
        }

        private void Shot(AudioClip c, float v)
        {
            if (c != null && _sfx != null)
                _sfx.PlayOneShot(c, v);
        }
    }
}
