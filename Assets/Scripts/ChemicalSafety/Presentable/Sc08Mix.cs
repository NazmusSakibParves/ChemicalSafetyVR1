using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 8: the white transparent tray with the Distilled Water beaker (two glass rods) and the fuming Sulfuric Acid beaker.
    /// A poke lifts the tray and carries it in front of the user (it follows the head); in the hood it is set down and the water
    /// beaker is touched: the acid beaker lifts, tilts over the water and pours a thin wobbling stream while a glass rod stirs.
    /// The acid vapor comes from the beaker's "Mouth" (Sc06Lab makes it and draws it into the hood while the hood runs).
    /// </summary>
    public sealed class Sc08Mix : MonoBehaviour
    {
        [SerializeField] private Sc06Lab lab;
        [SerializeField] private Sc06Ambience ambience;
        [SerializeField] private Material streamMaterial;
        [SerializeField] private Material dropMaterial;
        [SerializeField] private Material rippleMaterial;
        [SerializeField] private float pourSeconds = 6.5f;
        [SerializeField] private float tiltDegrees = 62f;

        private const float Scale = 1.8f;       // beaker scale (the mesh is 11.5 cm tall)
        private const float LiquidH = 0.10f;    // liquid mesh height (unscaled)
        private static readonly Vector3 LipLocal = new Vector3(0.0425f, 0.115f, 0f) * Scale;

        private Transform _tray;
        private PokeTarget _trayPoke;
        private Transform _trayGlow;
        private Transform _water;
        private Transform _acid;
        private Transform _acidLiquid;
        private Transform _waterLiquid;
        private Renderer _waterLiquidR;
        private Transform _rod;
        private PokeTarget _waterPoke;
        private Transform _ring;
        private Renderer _ringR;
        private Transform _rest;
        private GameObject _wisp;
        private Vector3 _trayHomePos;
        private Quaternion _trayHomeRot;
        private Vector3 _acidHomeLocal;
        private Quaternion _acidHomeLocalRot;
        private Quaternion _rodHome;
        private Vector3 _waterLiquidScale;
        private Vector3 _acidLiquidScale;
        private LineRenderer _stream;
        private ParticleSystem _splash;
        private Transform _ripple0;
        private Transform _ripple1;
        private Renderer _rr0;
        private Renderer _rr1;
        private MaterialPropertyBlock _mpb;
        private bool _carry;
        private bool _busy;
        private float _flow;
        private float _waterFill;
        private float _acidFill;
        private float _stirWeight;
        private float _stirAngle;
        private bool _ringOn;

        public bool Carrying => _carry;
        public bool Placed { get; private set; }
        public bool Pouring { get; private set; }
        public bool Done { get; private set; }
        public bool TrayPicked { get; private set; }
        public float WaterFill => _waterFill;
        public float AcidFill => _acidFill;
        public float Flow => _flow;
        public bool Stirring => _stirWeight > 0.5f;
        public bool StreamVisible => _stream != null && _stream.enabled;
        public Transform Tray => _tray;
        public Transform AcidBeaker => _acid;
        public Transform WaterBeaker => _water;
        public Transform Rod => _rod;
        public PokeTarget TrayPoke => _trayPoke;
        public PokeTarget WaterPoke => _waterPoke;

        /// <summary>Raised when the tray is in the hands.</summary>
        public event Action Picked;

        /// <summary>Raised when the acid has been added and the water has been stirred.</summary>
        public event Action MixFinished;

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
            _tray = Find(root, "Sc08Tray");
            _water = Find(root, "WaterBeaker");
            _acid = Find(root, "AcidBeaker");
            _acidLiquid = Find(root, "AcidLiquid");
            _waterLiquid = Find(root, "WaterLiquid");
            _waterLiquidR = _waterLiquid != null ? _waterLiquid.GetComponent<Renderer>() : null;
            _rod = Find(root, "RodA");
            var tp = Find(root, "TrayPoke");
            _trayPoke = tp != null ? tp.GetComponent<PokeTarget>() : null;
            _trayGlow = Find(root, "TrayGlow");
            var wp = Find(root, "WaterPoke");
            _waterPoke = wp != null ? wp.GetComponent<PokeTarget>() : null;
            _ring = Find(root, "WaterRing");
            _ringR = _ring != null ? _ring.GetComponent<Renderer>() : null;
            _rest = Find(root, "TrayRest");
            var mw = Find(root, "MixWisp");
            _wisp = mw != null ? mw.gameObject : null;
            _mpb = new MaterialPropertyBlock();
            if (_tray != null)
            {
                _trayHomePos = _tray.position;
                _trayHomeRot = _tray.rotation;
            }

            if (_acid != null)
            {
                _acidHomeLocal = _acid.localPosition;
                _acidHomeLocalRot = _acid.localRotation;
            }

            if (_rod != null)
                _rodHome = _rod.localRotation;
            if (_waterLiquid != null)
                _waterLiquidScale = _waterLiquid.localScale;
            if (_acidLiquid != null)
                _acidLiquidScale = _acidLiquid.localScale;
        }

        private void Start()
        {
            if (_trayPoke != null)
            {
                _trayPoke.Poked += _ => PickTray();
                _trayPoke.SetInteractable(false);
            }

            if (_waterPoke != null)
            {
                _waterPoke.Poked += _ => StartMix();
                _waterPoke.SetInteractable(false);
            }

            if (_trayGlow != null)
                _trayGlow.gameObject.SetActive(false);
            if (_ring != null)
                _ring.gameObject.SetActive(false);
            if (_wisp != null)
                _wisp.SetActive(false);
            BuildStream();
            ApplyFills(0.55f, 0.7f);
        }

        // ------------------------------------------------------------------ set up

        private void BuildStream()
        {
            var go = new GameObject("MixStream");
            go.transform.SetParent(transform, false);
            _stream = go.AddComponent<LineRenderer>();
            _stream.useWorldSpace = true;
            _stream.positionCount = 12;
            _stream.numCapVertices = 3;
            _stream.shadowCastingMode = ShadowCastingMode.Off;
            _stream.receiveShadows = false;
            _stream.sharedMaterial = streamMaterial;
            _stream.alignment = LineAlignment.View;
            _stream.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.8f));
            _stream.enabled = false;

            var sp = new GameObject("MixSplash");
            sp.transform.SetParent(transform, false);
            _splash = sp.AddComponent<ParticleSystem>();
            _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _splash.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.012f);
            main.startColor = new Color(0.95f, 0.96f, 0.85f, 0.8f);
            main.gravityModifier = 0.9f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = _splash.emission;
            em.rateOverTime = 0f;
            var sh = _splash.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 30f;
            sh.radius = 0.008f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var r = sp.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dropMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            _ripple0 = MakeRipple("MixRipple0", out _rr0);
            _ripple1 = MakeRipple("MixRipple1", out _rr1);
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

        // ------------------------------------------------------------------ the tray

        public void SetTrayInteractable(bool on)
        {
            if (_trayPoke != null)
                _trayPoke.SetInteractable(on && !_carry && !Placed);
            if (_trayGlow != null)
                _trayGlow.gameObject.SetActive(on && !_carry && !Placed);
        }

        public void PickTray()
        {
            if (_carry || _busy || Placed || _tray == null)
                return;
            StartCoroutine(PickRoutine());
        }

        private Vector3 CarryPos()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return _trayHomePos + Vector3.up * 0.2f;
            var f = Flat(cam.forward);
            return cam.position + f * 0.5f + Vector3.down * 0.34f;
        }

        // the tray's long side stays across the user's view (home pose = looking along -Z)
        private Quaternion CarryRot()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return _trayHomeRot;
            var f = Flat(cam.forward);
            return Quaternion.Euler(0f, Vector3.SignedAngle(Vector3.back, f, Vector3.up), 0f) * _trayHomeRot;
        }

        private IEnumerator PickRoutine()
        {
            _busy = true;
            TrayPicked = true;
            SetTrayInteractable(false);
            PresentableFx.Play(PresentableFx.Whoosh, 0.35f);
            GlovedHandPose.Left?.SetCradle(true);
            GlovedHandPose.Left?.SetGrasp(0.4f);
            var from = _tray.position;
            var fromRot = _tray.rotation;
            const float dur = 0.6f;
            for (var t = 0f; t < dur; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / dur);
                _tray.position = Vector3.Lerp(from, CarryPos(), k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.08f);
                _tray.rotation = Quaternion.Slerp(fromRot, CarryRot(), k);
                yield return null;
            }

            _carry = true;
            _busy = false;
            Picked?.Invoke();
        }

        private void LateUpdate()
        {
            if (!_carry || _busy || _tray == null)
                return;
            _tray.position = Vector3.Lerp(_tray.position, CarryPos(), 1f - Mathf.Exp(-Time.deltaTime * 7f));
            _tray.rotation = Quaternion.Slerp(_tray.rotation, CarryRot(), 1f - Mathf.Exp(-Time.deltaTime * 6f));
        }

        /// <summary>The tray goes from the hands to its place in the hood.</summary>
        public IEnumerator PlaceInHood()
        {
            if (_tray == null || _rest == null)
                yield break;
            _busy = true;
            _carry = false;
            var from = _tray.position;
            var fromRot = _tray.rotation;
            const float dur = 1.0f;
            for (var t = 0f; t < dur; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / dur);
                _tray.position = Vector3.Lerp(from, _rest.position, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.06f);
                _tray.rotation = Quaternion.Slerp(fromRot, _trayHomeRot, k);
                yield return null;
            }

            _tray.SetPositionAndRotation(_rest.position, _trayHomeRot);
            GlovedHandPose.Left?.SetGrasp(0f);
            GlovedHandPose.Left?.SetCradle(false);
            PresentableFx.Play(PresentableFx.Tick, 0.8f);
            Placed = true;
            _busy = false;
        }

        // ------------------------------------------------------------------ the mix

        public void SetMixInteractable(bool on)
        {
            _ringOn = on && Placed && !Done && !_busy;
            if (_waterPoke != null)
                _waterPoke.SetInteractable(_ringOn);
            if (_ring != null)
                _ring.gameObject.SetActive(_ringOn);
        }

        public void StartMix()
        {
            if (_busy || Done || !Placed || _acid == null || _water == null)
                return;
            StartCoroutine(MixRoutine());
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            _busy = false;
            _carry = false;
            Placed = false;
            Pouring = false;
            Done = false;
            TrayPicked = false;
            _flow = 0f;
            _stirWeight = 0f;
            if (_tray != null)
                _tray.SetPositionAndRotation(_trayHomePos, _trayHomeRot);
            if (_acid != null)
            {
                _acid.localPosition = _acidHomeLocal;
                _acid.localRotation = _acidHomeLocalRot;
            }

            if (_rod != null)
                _rod.localRotation = _rodHome;
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
            if (_trayGlow != null)
                _trayGlow.gameObject.SetActive(false);
            if (_trayPoke != null)
                _trayPoke.SetInteractable(false);
            if (_waterPoke != null)
                _waterPoke.SetInteractable(false);
            if (_wisp != null)
                _wisp.SetActive(false);
            if (ambience != null)
                ambience.SetPour(false);
            GlovedHandPose.Left?.SetGrasp(0f);
            GlovedHandPose.Left?.SetCradle(false);
            ApplyFills(0.55f, 0.7f);
        }

        private void ApplyFills(float water, float acid)
        {
            _waterFill = water;
            _acidFill = acid;
            if (_waterLiquid != null)
                _waterLiquid.localScale = new Vector3(_waterLiquidScale.x, _waterLiquidScale.y * Mathf.Max(0.0001f, water), _waterLiquidScale.z);
            if (_acidLiquid != null)
                _acidLiquid.localScale = new Vector3(_acidLiquidScale.x, _acidLiquidScale.y * Mathf.Max(0.0001f, acid), _acidLiquidScale.z);
            if (_waterLiquidR != null)
            {
                // the water turns very slightly warm and milky as the acid goes in
                var warm = Mathf.InverseLerp(0.55f, 0.85f, water);
                _mpb.SetColor("_BaseColor", Color.Lerp(new Color(0.9f, 0.95f, 0.98f, 0.28f), new Color(0.93f, 0.95f, 0.84f, 0.38f), warm));
                _waterLiquidR.SetPropertyBlock(_mpb);
            }
        }

        private float WaterSurfaceY() => _water.position.y + (0.003f + LiquidH * Mathf.Max(0.0001f, _waterFill)) * Scale;

        private static void PlaceAcid(Transform acid, Vector3 lipAt, float tiltDeg)
        {
            // tilt toward +X (the water beaker is on the viewer's left): negative about Z
            var rot = Quaternion.Euler(0f, 0f, -tiltDeg);
            acid.rotation = rot;
            acid.position = lipAt - rot * LipLocal;
        }

        private IEnumerator MixRoutine()
        {
            _busy = true;
            if (_waterPoke != null)
                _waterPoke.SetInteractable(false);
            if (_ring != null)
                _ring.gameObject.SetActive(false);
            if (lab != null)
                lab.HideArrow();
            if (_acid.parent != _tray)
                _acid.SetParent(_tray, true);
            var startPos = _acid.position;
            var startRot = _acid.rotation;
            var waterTop = _water.position + Vector3.up * (0.115f * Scale);
            var hover = waterTop + new Vector3(-0.02f, 0.11f, 0f);
            var pourAt = waterTop + new Vector3(-0.012f, 0.045f, 0f);
            GlovedHandPose.Left?.SetCradle(true);
            GlovedHandPose.Left?.SetGrasp(0.5f);
            if (_wisp != null)
                _wisp.SetActive(true);

            // A) the acid beaker lifts and goes over the water; the other hand starts to stir
            const float moveT = 0.9f;
            for (var t = 0f; t < moveT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / moveT);
                var lip = Vector3.Lerp(startPos + startRot * LipLocal, hover, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.05f);
                PlaceAcid(_acid, lip, 0f);
                Stir(k);
                yield return null;
            }

            Pouring = true;
            if (ambience != null)
                ambience.SetPour(true);

            // B) tilt slowly: the stream starts only when the lip is down
            const float tiltT = 1.4f;
            for (var t = 0f; t < tiltT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / tiltT);
                PlaceAcid(_acid, Vector3.Lerp(hover, pourAt, k), k * tiltDegrees);
                _flow = Mathf.Clamp01((k - 0.6f) / 0.35f);
                Stir(1f);
                UpdateStream();
                yield return null;
            }

            // C) pour slowly in a thin stream, stir all the time
            for (var t = 0f; t < pourSeconds; t += Time.deltaTime)
            {
                var k = t / pourSeconds;
                var ease = Mathf.SmoothStep(0f, 1f, k);
                PlaceAcid(_acid, pourAt, tiltDegrees + ease * 7f + Mathf.Sin(t * 1.9f) * 1.2f);
                _flow = 1f - Mathf.Clamp01((k - 0.9f) / 0.1f);
                ApplyFills(Mathf.Lerp(0.55f, 0.84f, ease), Mathf.Lerp(0.7f, 0.2f, ease));
                Stir(1f);
                UpdateStream();
                yield return null;
            }

            ApplyFills(0.84f, 0.2f);

            // D) tilt back, the stream runs dry
            for (var t = 0f; t < tiltT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / tiltT);
                PlaceAcid(_acid, Vector3.Lerp(pourAt, hover, k), (1f - k) * (tiltDegrees + 7f));
                _flow = 0f;
                Stir(1f);
                UpdateStream();
                yield return null;
            }

            _stream.enabled = false;
            _ripple0.gameObject.SetActive(false);
            _ripple1.gameObject.SetActive(false);
            _splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (ambience != null)
                ambience.SetPour(false);

            // E) set the acid beaker back on the tray, stir a little longer, then the rod is let go
            var from = _acid.position;
            var fromRot = _acid.rotation;
            var home = _tray.TransformPoint(_acidHomeLocal);
            const float setT = 0.9f;
            for (var t = 0f; t < setT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / setT);
                _acid.position = Vector3.Lerp(from, home, k);
                _acid.rotation = Quaternion.Slerp(fromRot, _tray.rotation * _acidHomeLocalRot, k);
                Stir(1f);
                yield return null;
            }

            _acid.localPosition = _acidHomeLocal;
            _acid.localRotation = _acidHomeLocalRot;
            for (var t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                Stir(1f - Mathf.SmoothStep(0f, 1f, (t - 0.8f) / 0.8f));
                yield return null;
            }

            _stirWeight = 0f;
            if (_rod != null)
                _rod.localRotation = _rodHome;
            if (_wisp != null)
                _wisp.SetActive(false);
            GlovedHandPose.Left?.SetGrasp(0f);
            GlovedHandPose.Left?.SetCradle(false);
            Pouring = false;
            Done = true;
            _busy = false;
            MixFinished?.Invoke();
        }

        // The glass rod circles in the water beaker (weight 0 = resting against the rim).
        private void Stir(float weight)
        {
            _stirWeight = weight;
            if (_rod == null || _water == null)
                return;
            _stirAngle += Time.deltaTime * 6.2f;
            var lean = Mathf.Lerp(0f, 9f, weight);
            // the circle stays on the +X side (away from the pouring beaker)
            var d = new Vector3(0.55f + 0.45f * Mathf.Cos(_stirAngle), 0f, 0.6f * Mathf.Sin(_stirAngle));
            var dir = (d * Mathf.Sin(lean * Mathf.Deg2Rad) + Vector3.up * Mathf.Cos(lean * Mathf.Deg2Rad)).normalized;
            var stirRot = Quaternion.FromToRotation(Vector3.up, dir) * _tray.rotation;
            _rod.rotation = Quaternion.Slerp(_tray.rotation * _rodHome, stirRot, weight);
        }

        private void UpdateStream()
        {
            var lip = _acid.position + _acid.rotation * LipLocal;
            var surfY = WaterSurfaceY();
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

            var drop = Mathf.Max(0.02f, lip.y - surfY);
            var len = Mathf.Lerp(0.2f, 1f, _flow) * drop;
            var n = _stream.positionCount;
            for (var i = 0; i < n; i++)
            {
                var u = i / (float)(n - 1);
                var x = lip.x + 0.006f * (1f - (1f - u) * (1f - u));
                var y = lip.y - len * u;
                var wob = Mathf.Sin(Time.time * 30f + u * 12f) * 0.0012f * u;
                _stream.SetPosition(i, new Vector3(x + wob, y, lip.z + Mathf.Cos(Time.time * 26f + u * 9f) * 0.0008f * u));
            }

            _stream.widthMultiplier = 0.007f * _flow + 0.0015f;
            var surface = new Vector3(lip.x + 0.006f, surfY, lip.z);
            _splash.transform.position = new Vector3(surface.x, lip.y - len, surface.z);
            var em = _splash.emission;
            em.rateOverTime = len >= drop * 0.97f ? 22f * _flow : 0f;
            if (!_splash.isPlaying)
                _splash.Play();
            var hit = len >= drop * 0.97f;
            Ripple(_ripple0, _rr0, hit, 0f, surface);
            Ripple(_ripple1, _rr1, hit, 0.5f, surface);
        }

        private void Ripple(Transform q, Renderer r, bool on, float offset, Vector3 at)
        {
            q.gameObject.SetActive(on);
            if (!on)
                return;
            var ph = (Time.time * 1.2f + offset) % 1f;
            q.position = at + Vector3.up * 0.002f;
            var size = Mathf.Lerp(0.02f, 0.085f, ph);
            q.localScale = new Vector3(size, size, 1f);
            _mpb.SetColor("_BaseColor", new Color(1f, 1f, 0.95f, (1f - ph) * 0.7f * _flow));
            r.SetPropertyBlock(_mpb);
        }

        private void Update()
        {
            if (_ring != null && _ring.gameObject.activeSelf && _ringR != null)
            {
                var p = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.2f);
                _ring.localScale = Vector3.one * (0.22f + 0.04f * p);
                _mpb.SetColor("_BaseColor", new Color(0.25f, 1f, 0.45f, 0.35f + 0.5f * p));
                _ringR.SetPropertyBlock(_mpb);
            }

            if (_trayGlow != null && _trayGlow.gameObject.activeSelf)
            {
                var p = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.6f);
                _trayGlow.localScale = Vector3.one * (0.75f + 0.06f * p);
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.001f ? Vector3.forward : v.normalized;
        }
    }
}
