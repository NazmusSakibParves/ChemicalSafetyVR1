using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 pour: touching the glass jar inside the hood lifts the solvent container over it, tilts it, and pours a wobbling
    /// liquid stream into the jar (splash, ripples, rising level, gurgling sound). The vapor of the pour is drawn to the hood back.
    /// Afterwards the container stands upright on the hood surface with its cap on.
    /// </summary>
    public sealed class Sc06Pour : MonoBehaviour
    {
        [SerializeField] private Sc06Lab lab;
        [SerializeField] private Sc06Jug jug;
        [SerializeField] private Sc06Ambience ambience;
        [SerializeField] private Material streamMaterial;
        [SerializeField] private Material dropMaterial;
        [SerializeField] private Material rippleMaterial;
        [SerializeField] private float pourSeconds = 4.2f;
        [SerializeField, Range(0.2f, 1f)] private float fillTarget = 0.66f;
        [SerializeField] private float tiltDegrees = 104f;

        private const float LiquidHeight = 0.17f;
        private Transform _jar;
        private Transform _liquid;
        private PokeTarget _poke;
        private Transform _ring;
        private Renderer _ringRenderer;
        private LineRenderer _stream;
        private ParticleSystem _splash;
        private Transform _ripple0;
        private Transform _ripple1;
        private Renderer _rr0;
        private Renderer _rr1;
        private MaterialPropertyBlock _mpb;
        private Vector3 _liquidScale;
        private float _liquidBaseY;
        private bool _ringOn;
        private bool _busy;
        private float _fill;
        private float _flow;
        private Vector3 _surface;

        public bool Pouring { get; private set; }
        public bool Done { get; private set; }
        public float Fill => _fill;
        public float Flow => _flow;
        public Transform Jar => _jar;
        public PokeTarget Poke => _poke;
        public int SplashParticles => _splash != null ? _splash.particleCount : 0;
        public bool StreamVisible => _stream != null && _stream.enabled;
        public Vector3 JarOpening => _jar != null ? _jar.position + Vector3.up * 0.27f : transform.position;

        /// <summary>Raised when the pour starts (container is above the jar).</summary>
        public event Action PourStarted;

        /// <summary>Raised when the container stands upright again and the pour is over.</summary>
        public event Action PourFinished;

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
            _jar = Find(root, "PourJar");
            _liquid = Find(root, "JarLiquid");
            var jp = Find(root, "JarPoke");
            _poke = jp != null ? jp.GetComponent<PokeTarget>() : null;
            _ring = Find(root, "JarRing");
            _ringRenderer = _ring != null ? _ring.GetComponent<Renderer>() : null;
            _mpb = new MaterialPropertyBlock();
            if (_liquid != null)
            {
                _liquidScale = new Vector3(_liquid.localScale.x, LiquidHeight * 0.5f, _liquid.localScale.z);
                _liquidBaseY = _jar != null ? _jar.position.y + 0.006f : _liquid.position.y;
            }
        }

        private void Start()
        {
            if (_poke != null)
            {
                _poke.Poked += _ => StartPour();
                _poke.SetInteractable(false);
            }

            if (_ring != null)
                _ring.gameObject.SetActive(false);
            BuildStream();
            ApplyFill(0f);
        }

        private void BuildStream()
        {
            var go = new GameObject("PourStream");
            go.transform.SetParent(transform, false);
            _stream = go.AddComponent<LineRenderer>();
            _stream.useWorldSpace = true;
            _stream.positionCount = 14;
            _stream.numCapVertices = 3;
            _stream.numCornerVertices = 2;
            _stream.shadowCastingMode = ShadowCastingMode.Off;
            _stream.receiveShadows = false;
            _stream.sharedMaterial = streamMaterial;
            _stream.alignment = LineAlignment.View;
            _stream.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.75f));
            _stream.enabled = false;

            var sp = new GameObject("PourSplash");
            sp.transform.SetParent(transform, false);
            _splash = sp.AddComponent<ParticleSystem>();
            _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _splash.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.010f, 0.018f);
            main.startColor = new Color(0.85f, 0.94f, 1f, 0.85f);
            main.gravityModifier = 0.9f;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = _splash.emission;
            em.rateOverTime = 0f;
            var sh = _splash.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 32f;
            sh.radius = 0.012f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var r = sp.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dropMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;

            _ripple0 = MakeRipple("PourRipple0", out _rr0);
            _ripple1 = MakeRipple("PourRipple1", out _rr1);
        }

        private Transform MakeRipple(string name, out Renderer rend)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            rend = q.GetComponent<Renderer>();
            rend.sharedMaterial = rippleMaterial;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            q.SetActive(false);
            return q.transform;
        }

        // ------------------------------------------------------------------ public control

        public void SetInteractable(bool on)
        {
            _ringOn = on && !Done && !_busy;
            if (_poke != null)
                _poke.SetInteractable(_ringOn);
            if (_ring != null)
                _ring.gameObject.SetActive(_ringOn);
        }

        public void StartPour()
        {
            if (_busy || Done || jug == null || jug.JugTransform == null || _jar == null)
                return;
            StartCoroutine(PourRoutine());
        }

        public void ResetPour()
        {
            StopAllCoroutines();
            _busy = false;
            Pouring = false;
            Done = false;
            _flow = 0f;
            _fill = 0f;
            _ringOn = false;
            if (_stream != null)
                _stream.enabled = false;
            if (_ripple0 != null)
                _ripple0.gameObject.SetActive(false);
            if (_ripple1 != null)
                _ripple1.gameObject.SetActive(false);
            if (_splash != null)
                _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_ring != null)
                _ring.gameObject.SetActive(false);
            if (_poke != null)
                _poke.SetInteractable(false);
            if (ambience != null)
                ambience.SetPour(false);
            ApplyFill(0f);
        }

        // ------------------------------------------------------------------ the pour

        private void ApplyFill(float f)
        {
            _fill = f;
            if (_liquid == null)
                return;
            var h = Mathf.Max(0.0001f, f);
            _liquid.localScale = new Vector3(_liquidScale.x, _liquidScale.y * h, _liquidScale.z);
            var p = _liquid.position;
            p.y = _liquidBaseY + LiquidHeight * 0.5f * h;
            _liquid.position = p;
        }

        private Vector3 MouthLocal(Transform j, Transform mouth) => j.InverseTransformPoint(mouth.position);

        private IEnumerator PourRoutine()
        {
            _busy = true;
            if (_poke != null)
                _poke.SetInteractable(false);
            if (_ring != null)
                _ring.gameObject.SetActive(false);
            lab.HideArrow();
            var j = jug.JugTransform;
            jug.SetFollow(false);
            var mouth = lab.Mouth;
            // mouth position in the jug's own frame (the jug stands upright with its label toward +Z at this moment)
            var upright = Quaternion.identity;
            var startRot = j.rotation;
            j.rotation = upright;
            var mouthLocal = MouthLocal(j, mouth);
            j.rotation = startRot;

            var open = JarOpening;
            var mouthPour = open + new Vector3(-0.012f, 0.07f, 0f);
            var mouthHover = mouthPour + new Vector3(0.02f, 0.13f, 0f);

            // A) over the jar, upright
            var from = j.position;
            const float moveT = 0.8f;
            for (var t = 0f; t < moveT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / moveT);
                var rot = Quaternion.Slerp(startRot, upright, k);
                var mTarget = mouthHover;
                var rootTarget = mTarget - upright * mouthLocal;
                j.position = Vector3.Lerp(from, rootTarget, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.05f);
                j.rotation = rot;
                yield return null;
            }

            Pouring = true;
            PourStarted?.Invoke();
            if (ambience != null)
                ambience.SetPour(true);

            // B) tilt toward the jar (the mouth side, viewer's right, goes down) while the mouth settles over the opening
            const float tiltT = 1.1f;
            for (var t = 0f; t < tiltT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / tiltT);
                Place(j, mouthLocal, Vector3.Lerp(mouthHover, mouthPour, k), k * tiltDegrees);
                _flow = Mathf.Clamp01((k - 0.55f) / 0.35f);
                UpdateStream(mouth.position);
                yield return null;
            }

            // C) pour: the level rises, the stream wobbles and thins toward the end
            for (var t = 0f; t < pourSeconds; t += Time.deltaTime)
            {
                var k = t / pourSeconds;
                Place(j, mouthLocal, mouthPour, tiltDegrees + Mathf.Sin(t * 2.1f) * 1.5f);
                _flow = 1f - Mathf.Clamp01((k - 0.88f) / 0.12f);
                ApplyFill(fillTarget * Mathf.SmoothStep(0f, 1f, k));
                UpdateStream(mouth.position);
                yield return null;
            }

            ApplyFill(fillTarget);

            // D) tilt back, the stream runs dry
            for (var t = 0f; t < tiltT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / tiltT);
                Place(j, mouthLocal, Vector3.Lerp(mouthPour, mouthHover, k), (1f - k) * tiltDegrees);
                _flow = 0f;
                UpdateStream(mouth.position);
                yield return null;
            }

            _stream.enabled = false;
            _ripple0.gameObject.SetActive(false);
            _ripple1.gameObject.SetActive(false);
            _splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (ambience != null)
                ambience.SetPour(false);

            // E) set the container down on the hood surface and put the cap on (close the container immediately after use)
            var rest = _jar.position + new Vector3(0.30f, 0.0f, 0.03f);
            from = j.position;
            const float setT = 0.8f;
            for (var t = 0f; t < setT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / setT);
                j.position = Vector3.Lerp(from, rest, k);
                j.rotation = upright;
                yield return null;
            }

            j.position = rest;
            GlovedHandPose.Left?.SetGrasp(0f);
            GlovedHandPose.Left?.SetCradle(false);
            yield return jug.CapOn();
            lab.SetVaporOn(false);
            Pouring = false;
            Done = true;
            _busy = false;
            PourFinished?.Invoke();
        }

        // Tilt about Z (positive = mouth side down), the mouth point held at 'mouthAt'.
        private static void Place(Transform j, Vector3 mouthLocal, Vector3 mouthAt, float degrees)
        {
            var rot = Quaternion.Euler(0f, 0f, degrees);
            j.rotation = rot;
            j.position = mouthAt - rot * mouthLocal;
        }

        private void UpdateStream(Vector3 mouthPos)
        {
            var liquidY = _liquidBaseY + LiquidHeight * Mathf.Max(0.0001f, _fill);
            _surface = new Vector3(mouthPos.x - 0.012f, liquidY, mouthPos.z);
            var on = _flow > 0.02f;
            _stream.enabled = on;
            if (!on)
            {
                var emOff = _splash.emission;
                emOff.rateOverTime = 0f;
                _ripple0.gameObject.SetActive(false);
                _ripple1.gameObject.SetActive(false);
                return;
            }

            var n = _stream.positionCount;
            var drop = Mathf.Max(0.02f, mouthPos.y - liquidY);
            var len = Mathf.Lerp(0.15f, 1f, _flow) * drop; // the stream grows out of the mouth
            for (var i = 0; i < n; i++)
            {
                var u = i / (float)(n - 1);
                // starts leaving the lip slightly outward and falls under gravity
                var x = mouthPos.x - 0.012f * (1f - (1f - u) * (1f - u));
                var y = mouthPos.y - len * u;
                var wob = Mathf.Sin(Time.time * 34f + u * 14f) * 0.0018f * u;
                _stream.SetPosition(i, new Vector3(x + wob, y, mouthPos.z + Mathf.Cos(Time.time * 29f + u * 11f) * 0.0012f * u));
            }

            _stream.widthMultiplier = 0.016f * _flow + 0.003f;
            _splash.transform.position = new Vector3(_surface.x, mouthPos.y - len, _surface.z);
            var em = _splash.emission;
            em.rateOverTime = len >= drop * 0.97f ? 38f * _flow : 0f;
            if (!_splash.isPlaying)
                _splash.Play();
            // ripples spread over the liquid surface while the stream hits it
            var hit = len >= drop * 0.97f;
            RippleAt(_ripple0, _rr0, hit, 0f);
            RippleAt(_ripple1, _rr1, hit, 0.5f);
        }

        private void RippleAt(Transform q, Renderer r, bool on, float offset)
        {
            q.gameObject.SetActive(on);
            if (!on)
                return;
            var ph = (Time.time * 1.4f + offset) % 1f;
            q.position = _surface + Vector3.up * 0.002f;
            var size = Mathf.Lerp(0.035f, 0.14f, ph);
            q.localScale = new Vector3(size, size, 1f);
            _mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, (1f - ph) * 0.8f * _flow));
            r.SetPropertyBlock(_mpb);
        }

        private void Update()
        {
            if (_ring != null && _ring.gameObject.activeSelf && _ringRenderer != null)
            {
                var p = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.2f);
                _ring.localScale = Vector3.one * (0.32f + 0.05f * p);
                _mpb.SetColor("_BaseColor", new Color(0.25f, 1f, 0.45f, 0.35f + 0.5f * p));
                _ringRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
