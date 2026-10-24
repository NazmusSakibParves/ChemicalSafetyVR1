using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 lab: the three panel switches (Fume Hood, LEV Arm, General Ventilation), the glass sash that slides up from the
    /// bottom, the visible air flow into the hood, the vapor of the open solvent container (pulled into the hood only while it
    /// runs) and the green marker arrow. Objects are found by the names the scene builder gives them.
    /// </summary>
    public sealed class Sc06Lab : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Sc06Ambience ambience;
        [SerializeField] private Material ledOn;
        [SerializeField] private Material ledOff;
        [SerializeField] private Material tubeOn;
        [SerializeField] private Material tubeOff;
        [SerializeField] private Material plateOn;
        [SerializeField] private Material plateOff;
        [SerializeField] private Material streakMaterial;
        [SerializeField] private Material puffMaterial;
        [SerializeField] private Material wispMaterial;
        [Tooltip("World box of the hood interior (sill to back baffle, glass height).")]
        [SerializeField] private Bounds hoodBounds;
        [SerializeField] private float openTravel = 1.05f;
        [SerializeField] private float openSeconds = 1.7f;
        [Tooltip("World position and direction (forward = into the nozzle) of the LEV arm inlet.")]
        [SerializeField] private Transform levNozzle;

        private static readonly bool[] StartOn = { false, false, true };
        private readonly Transform[] _rocker = new Transform[3];
        private readonly Renderer[] _led = new Renderer[3];
        private readonly PokeTarget[] _switchPoke = new PokeTarget[3];
        private readonly bool[] _on = new bool[3];
        private readonly Coroutine[] _rockerRoutine = new Coroutine[3];
        private Transform _sash;
        private PokeTarget _sashPoke;
        private Vector3 _sashClosed;
        private Renderer _tube;
        private Light _hoodLight;
        private Renderer _plate;
        private Transform _strip;
        private Renderer _flash;
        private Transform _mouth;
        private ParticleSystem _vaporPuff;
        private ParticleSystem _vaporWisp;
        private ParticleSystem _bowlWisp;
        private ParticleSystem _airInside;
        private ParticleSystem _airFront;
        private ParticleSystem _levFlow;
        private readonly ParticleSystem.Particle[] _buf = new ParticleSystem.Particle[96];
        private Transform _arrow;
        private Transform _arrowCanvas;
        private Transform _arrowTarget;
        private Vector3 _arrowOffset;
        private bool _sashOpen;
        private bool _sashMoving;
        private bool _flashing;
        private bool _awake;
        private float _stripAngle;

        public bool HoodOn => _on[0];
        public bool LevOn => _on[1];
        public bool VentOn => _on[2];
        public bool SashOpen => _sashOpen;
        public bool SashMoving => _sashMoving;
        public Bounds HoodBounds => hoodBounds;
        public Transform Sash => _sash;
        public Transform Mouth => _mouth;
        public PokeTarget SwitchPoke(int i) => i >= 0 && i < 3 ? _switchPoke[i] : null;
        public PokeTarget SashPoke => _sashPoke;
        public bool ArrowVisible => _arrow != null && _arrow.gameObject.activeSelf;
        public Transform Rocker(int i) => i >= 0 && i < 3 ? _rocker[i] : null;
        public float StripAngle => _stripAngle;
        public int AirParticles => (_airInside != null ? _airInside.particleCount : 0) + (_airFront != null ? _airFront.particleCount : 0);
        public int VaporParticles => (_vaporPuff != null ? _vaporPuff.particleCount : 0) + (_vaporWisp != null ? _vaporWisp.particleCount : 0);

        /// <summary>Raised when a switch flips (index 0 Fume Hood, 1 LEV Arm, 2 General Ventilation, new state).</summary>
        public event Action<int, bool> SwitchChanged;

        /// <summary>Raised when the sash is touched while the hood is off (it does not open).</summary>
        public event Action SashBlocked;

        /// <summary>Raised when the sash has finished opening.</summary>
        public event Action SashOpened;

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void Awake()
        {
            var root = transform.parent != null ? transform.parent : transform;
            for (var i = 0; i < 3; i++)
            {
                _rocker[i] = Find(root, "Rocker" + i);
                var led = Find(root, "Led" + i);
                _led[i] = led != null ? led.GetComponent<Renderer>() : null;
                var poke = Find(root, "SwitchPoke" + i);
                _switchPoke[i] = poke != null ? poke.GetComponent<PokeTarget>() : null;
                _on[i] = StartOn[i];
            }

            _sash = Find(root, "Sash");
            var sp = Find(root, "SashPoke");
            _sashPoke = sp != null ? sp.GetComponent<PokeTarget>() : null;
            var tube = Find(root, "HoodTube");
            _tube = tube != null ? tube.GetComponent<Renderer>() : null;
            var hl = Find(root, "HoodLight");
            _hoodLight = hl != null ? hl.GetComponent<Light>() : null;
            var plate = Find(root, "HoodPlate");
            _plate = plate != null ? plate.GetComponent<Renderer>() : null;
            _strip = Find(root, "Strip");
            var fl = Find(root, "SashFlash");
            _flash = fl != null ? fl.GetComponent<Renderer>() : null;
            _mouth = Find(root, "Mouth");
            var bw = Find(root, "BowlWisp");
            _bowlWisp = bw != null ? bw.GetComponent<ParticleSystem>() : null;
            if (_sash != null)
                _sashClosed = _sash.position;
            _awake = true;
        }

        private void Start()
        {
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                if (_switchPoke[i] != null)
                {
                    _switchPoke[i].Poked += _ => Toggle(index);
                    _switchPoke[i].SetInteractable(false);
                }
            }

            if (_sashPoke != null)
            {
                _sashPoke.Poked += _ => OnSashPoke();
                _sashPoke.SetInteractable(false);
            }

            BuildVapor();
            BuildAir();
            ApplyAll(true);
        }

        /// <summary>Back to the start of the scenario: hood off, LEV off, ventilation on, sash closed, nothing pokeable.</summary>
        public void ResetLab()
        {
            StopAllCoroutines();
            _sashMoving = false;
            _flashing = false;
            if (_flash != null)
                _flash.gameObject.SetActive(false);
            for (var i = 0; i < 3; i++)
            {
                _on[i] = StartOn[i];
                _rockerRoutine[i] = null;
            }

            _sashOpen = false;
            if (_sash != null)
                _sash.position = _sashClosed;
            SetSwitchesInteractable(false);
            SetSashInteractable(false);
            HideArrow();
            if (_awake)
                ApplyAll(true);
            SetVaporOn(true);
            if (ambience != null)
            {
                ambience.SetHood(false);
                ambience.SetVent(true);
            }
        }

        // ------------------------------------------------------------------ switches

        public void SetSwitchesInteractable(bool on)
        {
            for (var i = 0; i < 3; i++)
                if (_switchPoke[i] != null)
                    _switchPoke[i].SetInteractable(on);
        }

        public void SetSashInteractable(bool on)
        {
            if (_sashPoke != null)
                _sashPoke.SetInteractable(on);
        }

        /// <summary>Flips a switch as a finger poke would (director / smoke test).</summary>
        public void Toggle(int index)
        {
            if (index < 0 || index > 2)
                return;
            // the Fume Hood cannot be switched off once the sash is open (the user is working in it)
            if (index == 0 && _on[0] && _sashOpen)
                return;
            _on[index] = !_on[index];
            ApplySwitch(index, true);
            if (ambience != null)
                ambience.PlayClick();
            PresentableFx.Haptic(true, 0.5f, 0.05f);
            SwitchChanged?.Invoke(index, _on[index]);
        }

        private void ApplyAll(bool instant)
        {
            for (var i = 0; i < 3; i++)
                ApplySwitch(i, !instant);
            if (instant)
            {
                for (var i = 0; i < 3; i++)
                {
                    if (_rocker[i] != null)
                        _rocker[i].localRotation = Quaternion.Euler(_on[i] ? -16f : 16f, 0f, 0f);
                }
            }
        }

        private void ApplySwitch(int i, bool animate)
        {
            if (_led[i] != null)
                _led[i].sharedMaterial = _on[i] ? ledOn : ledOff;
            if (_rocker[i] != null)
            {
                var target = Quaternion.Euler(_on[i] ? -16f : 16f, 0f, 0f);
                if (animate && isActiveAndEnabled)
                {
                    if (_rockerRoutine[i] != null)
                        StopCoroutine(_rockerRoutine[i]);
                    _rockerRoutine[i] = StartCoroutine(Flip(_rocker[i], target));
                }
                else
                {
                    _rocker[i].localRotation = target;
                }
            }

            if (i == 0)
            {
                if (_tube != null)
                    _tube.sharedMaterial = _on[0] ? tubeOn : tubeOff;
                if (_hoodLight != null)
                    _hoodLight.enabled = _on[0];
                if (_plate != null)
                    _plate.sharedMaterial = _on[0] ? plateOn : plateOff;
                if (ambience != null)
                    ambience.SetHood(_on[0]);
            }
            else if (i == 2 && ambience != null)
            {
                ambience.SetVent(_on[2]);
            }
        }

        private static IEnumerator Flip(Transform rocker, Quaternion target)
        {
            var from = rocker.localRotation;
            for (var t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                rocker.localRotation = Quaternion.Slerp(from, target, t / 0.14f);
                yield return null;
            }

            rocker.localRotation = target;
        }

        // ------------------------------------------------------------------ sash

        private void OnSashPoke()
        {
            if (_sashMoving || _sashOpen)
                return;
            if (!_on[0])
            {
                SashBlocked?.Invoke();
                StartCoroutine(FlashRed());
                PresentableFx.Play(PresentableFx.Error, 0.6f);
                return;
            }

            StartCoroutine(OpenRoutine());
        }

        /// <summary>Opens the sash (also used by the smoke test). Needs the hood on.</summary>
        public void OpenSash()
        {
            if (!_sashOpen && !_sashMoving && _on[0])
                StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            _sashMoving = true;
            SetSashInteractable(false);
            if (ambience != null)
                ambience.PlaySlide();
            var from = _sashClosed;
            var to = _sashClosed + Vector3.up * openTravel;
            for (var t = 0f; t < openSeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / openSeconds);
                if (_sash != null)
                    _sash.position = Vector3.Lerp(from, to, k);
                yield return null;
            }

            if (_sash != null)
                _sash.position = to;
            _sashOpen = true;
            _sashMoving = false;
            SashOpened?.Invoke();
        }

        /// <summary>Slides the sash shut again (the hood keeps running).</summary>
        public void CloseSash()
        {
            if (_sashOpen && !_sashMoving)
                StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            _sashMoving = true;
            if (ambience != null)
                ambience.PlaySlide();
            var from = _sash != null ? _sash.position : _sashClosed;
            for (var t = 0f; t < openSeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / openSeconds);
                if (_sash != null)
                    _sash.position = Vector3.Lerp(from, _sashClosed, k);
                yield return null;
            }

            if (_sash != null)
                _sash.position = _sashClosed;
            _sashOpen = false;
            _sashMoving = false;
        }

        private IEnumerator FlashRed()
        {
            if (_flash == null || _flashing)
                yield break;
            _flashing = true;
            _flash.gameObject.SetActive(true);
            var mat = _flash.material;
            for (var t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                var a = Mathf.Abs(Mathf.Sin(t / 0.9f * Mathf.PI * 2f)) * (1f - t / 0.9f) * 0.6f;
                var c = new Color(1f, 0.15f, 0.12f, a);
                mat.color = c;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", c);
                yield return null;
            }

            _flash.gameObject.SetActive(false);
            _flashing = false;
        }

        // ------------------------------------------------------------------ particles

        private static ParticleSystem NewPs(Transform parent, string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // Soft vapor spilling over the lip of the open container: a billowing layer and thin wisps, heavier than air.
        private void BuildVapor()
        {
            if (_mouth == null)
                return;
            _vaporPuff = NewPs(_mouth, "VaporPuff", _mouth.position);
            var main = _vaporPuff.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.20f, 0.34f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(0.9f, 0.94f, 0.96f, 0.36f);
            main.maxParticles = 60;
            main.gravityModifier = -0.035f; // the vapor rises from the open mouth
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = _vaporPuff.emission;
            em.rateOverTime = 6f;
            var sh = _vaporPuff.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 18f;
            sh.radius = 0.016f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var noise = _vaporPuff.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.5f;
            var sol = _vaporPuff.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.5f, 1.4f), new Keyframe(1f, 2.6f)));
            var rol = _vaporPuff.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            Fade(_vaporPuff, 0.15f, 0.55f);
            _vaporPuff.GetComponent<ParticleSystemRenderer>().sharedMaterial = puffMaterial;
            _vaporPuff.Play();

            _vaporWisp = NewPs(_mouth, "VaporWisp", _mouth.position + Vector3.up * 0.01f);
            main = _vaporWisp.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            main.startColor = new Color(0.92f, 0.96f, 0.98f, 0.28f);
            main.maxParticles = 30;
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            em = _vaporWisp.emission;
            em.rateOverTime = 2.5f;
            sh = _vaporWisp.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 10f;
            sh.radius = 0.012f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            noise = _vaporWisp.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.8f;
            sol = _vaporWisp.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));
            Fade(_vaporWisp, 0.12f, 0.45f);
            _vaporWisp.GetComponent<ParticleSystemRenderer>().sharedMaterial = wispMaterial;
            _vaporWisp.Play();
        }

        /// <summary>Stops or restarts the vapor of the solvent container (the cap goes on after the pour).</summary>
        public void SetVaporOn(bool on)
        {
            if (_vaporPuff != null)
            {
                var em = _vaporPuff.emission;
                em.rateOverTime = on ? 6f : 0f;
            }

            if (_vaporWisp != null)
            {
                var em = _vaporWisp.emission;
                em.rateOverTime = on ? 2.5f : 0f;
            }
        }

        private static void Fade(ParticleSystem ps, float inAt, float mid)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(mid, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        // Thin white streaks drawn into the hood (inside and, with the sash open, through the opening), and into the LEV arm.
        private void BuildAir()
        {
            var b = hoodBounds;
            if (b.size.sqrMagnitude < 0.01f)
                return;
            var front = b.max.z;
            var back = b.min.z;
            var speed = 0.85f;
            _airInside = Streaks("AirInside", new Vector3(b.center.x, b.min.y + b.size.y * 0.42f, front - 0.1f),
                new Vector3(b.size.x * 0.8f, b.size.y * 0.55f, 0.02f), speed, (front - 0.1f - back) / speed * 0.9f, 0.9f);
            _airFront = Streaks("AirFront", new Vector3(b.center.x, b.min.y + b.size.y * 0.45f, front + 0.32f),
                new Vector3(b.size.x * 0.75f, b.size.y * 0.5f, 0.02f), speed, (front + 0.32f - back) / speed * 0.9f, 0.9f);
            if (levNozzle != null)
            {
                _levFlow = NewPs(transform, "LevFlow", levNozzle.position);
                var main = _levFlow.main;
                main.loop = true;
                main.playOnAwake = false;
                main.duration = 2f;
                main.startLifetime = 0.5f;
                main.startSpeed = -0.55f;
                main.startSize = 0.012f;
                main.startColor = new Color(1f, 1f, 1f, 0.5f);
                main.maxParticles = 30;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = _levFlow.emission;
                em.rateOverTime = 0f;
                var sh = _levFlow.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.3f;
                sh.radiusThickness = 0f;
                var r = _levFlow.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = streakMaterial;
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 5f;
                r.velocityScale = 0.05f;
                Fade(_levFlow, 0.2f, 0.7f);
            }
        }

        private ParticleSystem Streaks(string name, Vector3 at, Vector3 box, float speed, float life, float alpha)
        {
            var ps = NewPs(transform, name, at);
            ps.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); // emit toward the back baffle
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.8f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.85f, speed * 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startColor = new Color(1f, 1f, 1f, alpha * 0.55f);
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = box;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = streakMaterial;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 7f;
            r.velocityScale = 0.04f;
            Fade(ps, 0.15f, 0.8f);
            return ps;
        }

        private static void SetRate(ParticleSystem ps, float rate, bool play)
        {
            if (ps == null)
                return;
            var em = ps.emission;
            em.rateOverTime = rate;
            if (play && !ps.isPlaying)
                ps.Play();
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            var hood = _on[0];
            SetRate(_airInside, hood ? 24f : 0f, hood);
            SetRate(_airFront, hood && (_sashOpen || _sashMoving) ? 46f : 0f, hood);
            SetRate(_levFlow, _on[1] ? 40f : 0f, _on[1]);

            // paper strip on the sash: hangs still when the hood is off, is blown inward and flutters when it runs
            if (_strip != null)
            {
                var target = hood ? 30f + Mathf.Sin(Time.time * 7.3f) * 6f + Mathf.Sin(Time.time * 13.1f) * 3f : 0f;
                _stripAngle = Mathf.Lerp(_stripAngle, target, 1f - Mathf.Exp(-Time.deltaTime * (hood ? 5f : 3f)));
                _strip.localRotation = Quaternion.Euler(_stripAngle, 0f, 0f);
            }

            if (_arrow != null && _arrow.gameObject.activeSelf && _arrowTarget != null)
            {
                var tick = Mathf.Abs(Mathf.Sin(Time.time * 3.4f));
                _arrow.position = _arrowTarget.position + _arrowOffset + Vector3.up * (tick * 0.045f);
                _arrow.localScale = Vector3.one * (1f + 0.12f * tick);
                var cam = Camera.main != null ? Camera.main.transform : null;
                if (cam != null)
                {
                    var f = cam.position - _arrow.position;
                    f.y = 0f;
                    if (f.sqrMagnitude > 0.001f)
                        _arrow.rotation = Quaternion.LookRotation(-f.normalized, Vector3.up);
                }
            }
        }

        private void LateUpdate()
        {
            Suck(_vaporPuff);
            Suck(_vaporWisp);
            Suck(_bowlWisp);
        }

        // Particles inside the hood (or in the air stream in front of the open sash) are drawn to the back baffle and fade out.
        private void Suck(ParticleSystem ps)
        {
            if (ps == null || !_on[0])
                return;
            var n = ps.GetParticles(_buf);
            if (n == 0)
                return;
            var b = hoodBounds;
            var inner = b;
            inner.Expand(new Vector3(0f, 0.05f, 0f));
            var zone = new Bounds(new Vector3(b.center.x, b.center.y, b.max.z + 0.12f), new Vector3(b.size.x, b.size.y + 0.3f, 0.5f));
            var target = new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.min.z + 0.02f);
            var dt = Time.deltaTime;
            var changed = false;
            for (var i = 0; i < n; i++)
            {
                var p = _buf[i];
                var inHood = inner.Contains(p.position);
                var inFront = _sashOpen && zone.Contains(p.position);
                if (!inHood && !inFront)
                    continue;
                var dir = (target - p.position).normalized;
                var gain = inHood ? 3.2f : 1.4f;
                p.velocity = Vector3.Lerp(p.velocity, dir * (inHood ? 0.95f : 0.5f), 1f - Mathf.Exp(-gain * dt));
                if (inHood)
                    p.remainingLifetime = Mathf.Min(p.remainingLifetime, 1.0f);
                _buf[i] = p;
                changed = true;
            }

            if (changed)
                ps.SetParticles(_buf, n);
        }

        // ------------------------------------------------------------------ marker arrow

        /// <summary>Shows the bobbing green arrow above a target (offset in world space).</summary>
        public void PointAt(Transform target, Vector3 offset)
        {
            EnsureArrow();
            if (_arrow == null)
                return;
            _arrowTarget = target;
            _arrowOffset = offset;
            _arrow.gameObject.SetActive(true);
        }

        public void HideArrow()
        {
            if (_arrow != null)
                _arrow.gameObject.SetActive(false);
        }

        private void EnsureArrow()
        {
            if (_arrow != null || art == null)
                return;
            _arrow = new GameObject("Sc06Arrow").transform;
            _arrow.SetParent(transform, false);
            var canvas = PresentableUi.WorldCanvas("Sc06ArrowCanvas", _arrow, new Vector2(200f, 280f), 0.0011f, false, 80);
            _arrowCanvas = canvas.transform;
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", _arrowCanvas, new Vector2(0f, 0.1f), new Vector2(1f, 0.9f), art.radialGlow,
                    new Color(0.2f, 1f, 0.4f, 0.35f));
            if (art.arrowDown != null)
                PresentableUi.Img("Arrow", _arrowCanvas, new Vector2(0.14f, 0.06f), new Vector2(0.86f, 0.94f), art.arrowDown,
                    new Color(0.3f, 1f, 0.45f));
            PresentableUi.AlwaysOnTop(canvas);
            _arrow.gameObject.SetActive(false);
        }
    }
}
