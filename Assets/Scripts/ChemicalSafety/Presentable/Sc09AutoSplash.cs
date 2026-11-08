using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 9 (controller ray): the automatic accident in the Scenario 6/8 factory room. It drives the SAME table containers
    /// (the clear mixture jar and the acid can) but it is a separate component: Scenario 6's and 8's components are never touched.
    /// The acid can is lifted over the empty jar and starts to pour; it slips out of the hand, hits the jar (the jar breaks, shards fly),
    /// acid drops fly at the learner's face (<see cref="Sc09Visor"/> takes them) and the can ends lying on its side on the table.
    /// A yellow acid puddle spreads over the table top, drips over the edge and spreads on the floor under the table.
    /// <see cref="ResetAll"/> puts everything back exactly as Scenario 6 and 8 expect it.
    /// </summary>
    public sealed class Sc09AutoSplash : MonoBehaviour
    {
        [Header("Table containers (pivot at the base centre, upright)")]
        [SerializeField] private Transform jar;
        [SerializeField] private Transform acid;
        [SerializeField] private Transform jarMouth;
        [SerializeField] private Transform acidSpout;
        [SerializeField] private GameObject acidCap;
        [Header("Jar liquid (shared with Scenario 6/8, restored by ResetAll)")]
        [SerializeField] private Transform liquid;
        [SerializeField] private Renderer liquidRenderer;
        [SerializeField] private Renderer liquidTop;
        [SerializeField] private float liquidFullHeight = 0.2f;
        [Header("Accident props (built hidden)")]
        [SerializeField] private Transform broken;
        [SerializeField] private Transform puddleTable;
        [SerializeField] private Transform puddleFloor;
        [SerializeField] private Transform spills;
        [SerializeField] private ParticleSystem drip;
        [SerializeField] private Vector2 puddleTableSize = new Vector2(0.62f, 0.56f);
        [SerializeField] private Vector2 puddleFloorSize = new Vector2(0.90f, 0.66f);
        [Header("Table")]
        [SerializeField] private float tableTopY = 0.92f;
        [SerializeField] private float tableFrontZ = 0.93f;
        [Header("Look and sound")]
        [SerializeField] private Material streamMaterial;
        [SerializeField] private Material dropMaterial;
        [SerializeField] private AudioClip knockClip;
        [SerializeField] private AudioClip breakClip;
        [SerializeField] private AudioClip splashClip;
        [SerializeField] private Sc09Visor visor;
        [SerializeField] private Color acidColor = new Color(0.62f, 0.56f, 0.10f, 0.92f);
        [Header("Timing")]
        [SerializeField] private float startDelay = 0.8f;
        [SerializeField] private float carrySeconds = 1.6f;
        [SerializeField] private float pourSeconds = 2.4f;

        private sealed class Cap
        {
            public GameObject go;
            public Transform parent;
            public Vector3 localPos;
            public Quaternion localRot;
            public Vector3 localScale = Vector3.one;
        }

        private Cap _cap;
        private Vector3 _jarHome;
        private Quaternion _jarHomeRot;
        private Vector3 _acidHome;
        private Quaternion _acidHomeRot;
        private Vector3 _liquidBaseScale = Vector3.one;
        private MaterialPropertyBlock _liquidBlock;
        private LineRenderer _stream;
        private ParticleSystem _burst;
        private ParticleSystem _faceSpray;
        private AudioSource _audio;
        private Transform[] _shards;
        private Vector3[] _shardHome;
        private Quaternion[] _shardHomeRot;
        private readonly List<Vector3> _corners = new List<Vector3>();
        private bool _captured;

        public bool Running { get; private set; }

        /// <summary>True from the moment the acid has hit the face shield.</summary>
        public bool Splashed { get; private set; }

        /// <summary>True when the can lies on the table, the puddles have spread and everything is at rest.</summary>
        public bool Finished { get; private set; }

        public bool JarBroken { get; private set; }
        public Transform Acid => acid;
        public Transform Jar => jar;
        public Transform Puddle => puddleTable;
        public Transform FloorPuddle => puddleFloor;

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (_captured || jar == null || acid == null)
                return;
            _jarHome = jar.position;
            _jarHomeRot = jar.rotation;
            _acidHome = acid.position;
            _acidHomeRot = acid.rotation;
            if (acidCap != null)
                _cap = new Cap
                {
                    go = acidCap,
                    parent = acidCap.transform.parent,
                    localPos = acidCap.transform.localPosition,
                    localRot = acidCap.transform.localRotation,
                    localScale = acidCap.transform.localScale
                };
            if (liquid != null)
            {
                _liquidBaseScale = liquid.localScale;
                if (_liquidBaseScale.y < 0.5f)
                    _liquidBaseScale = Vector3.one; // Scenario 6 may have squashed the shared liquid to "empty" before this woke up
            }

            if (broken != null)
            {
                var shards = new List<Transform>();
                foreach (var t in broken.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Sc09FacShard"))
                        shards.Add(t);
                _shards = shards.ToArray();
                _shardHome = new Vector3[_shards.Length];
                _shardHomeRot = new Quaternion[_shards.Length];
                for (var i = 0; i < _shards.Length; i++)
                {
                    _shardHome[i] = _shards[i].localPosition;
                    _shardHomeRot[i] = _shards[i].localRotation;
                }
            }

            _liquidBlock = new MaterialPropertyBlock();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            BuildFx();
            _captured = true;
        }

        private void BuildFx()
        {
            var go = new GameObject("Sc09AutoStream");
            go.transform.SetParent(transform, false);
            _stream = go.AddComponent<LineRenderer>();
            _stream.useWorldSpace = true;
            _stream.positionCount = 10;
            _stream.numCapVertices = 3;
            _stream.shadowCastingMode = ShadowCastingMode.Off;
            _stream.receiveShadows = false;
            _stream.sharedMaterial = streamMaterial;
            _stream.alignment = LineAlignment.View;
            _stream.startWidth = 0.014f;
            _stream.endWidth = 0.010f;
            _stream.enabled = false;
            _burst = MakeParticles("Sc09AutoBurst", 90, 0.9f, 0.8f, 2.6f, 0.006f, 0.018f, 1.1f, 80f);
            _faceSpray = MakeParticles("Sc09AutoFaceSpray", 110, 0.55f, 1.6f, 3.3f, 0.007f, 0.020f, 0.5f, 24f);
        }

        private ParticleSystem MakeParticles(string name, int max, float life, float v0, float v1, float s0, float s1, float grav, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(v0, v1);
            main.startSize = new ParticleSystem.MinMaxCurve(s0, s1);
            main.startColor = acidColor;
            main.gravityModifier = grav;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = angle;
            sh.radius = 0.02f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dropMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        private void Hit(AudioClip clip, float volume)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ play

        public void StartPlay()
        {
            Capture();
            if (Running || jar == null || acid == null)
                return;
            ResetAll();
            Running = true;
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            Running = true;
            Splashed = false;
            Finished = false;
            JarBroken = false;
            yield return PresentableFx.Wait(startDelay);
            Hit(knockClip, 0.45f);

            // 1. the can is carried over the empty jar and tilted: the cap comes off first
            var toward = Flat(_jarHome - _acidHome).normalized;
            if (toward.sqrMagnitude < 1e-4f)
                toward = Vector3.right;
            var axis = Vector3.Cross(Vector3.up, toward);
            var spoutLocal = Vector3.Scale(acid.InverseTransformPoint(acidSpout.position), acid.lossyScale);
            var lipFlat = new Vector3(spoutLocal.x, 0f, spoutLocal.z);
            var yaw = lipFlat.magnitude > 0.02f ? Vector3.SignedAngle(lipFlat, toward, Vector3.up) : 0f;
            var yawRot = Quaternion.AngleAxis(yaw, Vector3.up);
            var mouth = jarMouth.position + Vector3.up * 0.08f + Vector3.back * 0.05f;
            CollectCorners(acid);

            Pose PoseAt(float angle)
            {
                var rot = Quaternion.AngleAxis(angle, axis) * yawRot;
                var pos = mouth - rot * spoutLocal;
                pos.y += RequiredLift(rot, pos, acid.lossyScale);
                return new Pose(pos, rot);
            }

            yield return LiftCapOff();
            var startPos = acid.position;
            var startRot = acid.rotation;
            var p0 = PoseAt(55f);
            for (var t = 0f; t < carrySeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / carrySeconds);
                acid.position = Vector3.Lerp(startPos, p0.position, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.07f);
                acid.rotation = Quaternion.Slerp(startRot, p0.rotation, k);
                yield return null;
            }

            acid.SetPositionAndRotation(p0.position, p0.rotation);

            // 2. a short pour: the acid runs into the jar (a thin yellow stream, the level rises a little)
            _stream.enabled = true;
            SetLiquid(0f);
            for (var t = 0f; t < pourSeconds; t += Time.deltaTime)
            {
                var k = t / pourSeconds;
                var pose = PoseAt(Mathf.Lerp(55f, 82f, Mathf.SmoothStep(0f, 1f, k)));
                acid.SetPositionAndRotation(pose.position, pose.rotation);
                var level = Mathf.Lerp(0f, 0.30f, k);
                SetLiquid(level);
                var from = acidSpout.position;
                var to = new Vector3(jarMouth.position.x, liquid != null ? liquid.position.y + liquidFullHeight * level : jarMouth.position.y - 0.1f,
                    jarMouth.position.z - 0.05f);
                for (var i = 0; i < _stream.positionCount; i++)
                {
                    var f = i / (float)(_stream.positionCount - 1);
                    var p = Vector3.Lerp(from, to, f);
                    p.x += Mathf.Sin(f * 9f + Time.time * 11f) * 0.0015f * f;
                    _stream.SetPosition(i, p);
                }

                yield return null;
            }

            // 3. it slips out of the hand toward the learner and hits the jar; the can ends lying on the table in front of the jar
            _stream.enabled = false;
            var fallStart = acid.position;
            var fallRot = acid.rotation;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var toCam = cam != null ? Flat(cam.position - _jarHome).normalized : Vector3.forward;
            if (toCam.sqrMagnitude < 1e-4f)
                toCam = Vector3.forward;
            var lieYaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg + 70f;
            var lieRot = Quaternion.Euler(0f, lieYaw, 90f);
            var restXZ = new Vector3(_jarHome.x - 0.24f, 0f, Mathf.Min(tableFrontZ - 0.40f, _jarHome.z + 0.04f));
            acid.rotation = lieRot;
            var lowest = float.MaxValue;
            foreach (var r in acid.GetComponentsInChildren<Renderer>(false))
                if (!(r is ParticleSystemRenderer) && !(r is LineRenderer))
                    lowest = Mathf.Min(lowest, r.bounds.min.y);
            var belowPivot = lowest < float.MaxValue ? acid.position.y - lowest : 0.0f;
            var restY = tableTopY + belowPivot + 0.003f;
            acid.rotation = fallRot;
            var drop = Mathf.Max(0.05f, fallStart.y - restY);
            var duration = Mathf.Clamp(Mathf.Sqrt(2f * drop / 9.81f) + 0.12f, 0.3f, 0.9f);
            Hit(knockClip, 0.4f);
            var broke = false;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                var xz = Vector3.Lerp(Flat(fallStart), restXZ, Mathf.SmoothStep(0f, 1f, k));
                var y = Mathf.Lerp(fallStart.y, restY, k * k);
                acid.SetPositionAndRotation(new Vector3(xz.x, y, xz.z), Quaternion.Slerp(fallRot, lieRot, Mathf.SmoothStep(0f, 1f, k)));
                if (!broke && k > 0.3f)
                {
                    broke = true;
                    StartCoroutine(BreakJar(toCam));
                }

                yield return null;
            }

            if (!broke)
                StartCoroutine(BreakJar(toCam));
            // a small hop and rest
            var landed = new Vector3(restXZ.x, restY, restXZ.z);
            for (var t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                var hop = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI) * 0.03f;
                acid.position = landed + Vector3.up * hop;
                yield return null;
            }

            acid.SetPositionAndRotation(landed, lieRot);
            while (!Finished)
                yield return null;
            Running = false;
        }

        private IEnumerator BreakJar(Vector3 toCam)
        {
            JarBroken = true;
            var hitPoint = new Vector3(_jarHome.x, tableTopY, _jarHome.z);
            jar.gameObject.SetActive(false);
            Hit(breakClip, 1f);
            Hit(splashClip, 0.85f);
            PresentableFx.HapticBoth(0.8f, 0.18f);

            if (broken != null)
            {
                broken.position = hitPoint;
                broken.rotation = Quaternion.Euler(0f, 25f, 0f);
                broken.gameObject.SetActive(true);
            }

            // acid drops: a burst up and out and a spray at the face shield
            _burst.transform.position = hitPoint + Vector3.up * 0.10f;
            _burst.transform.rotation = Quaternion.LookRotation(Vector3.up);
            _burst.Emit(80);
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                _faceSpray.transform.position = hitPoint + Vector3.up * 0.14f;
                var d = cam.position + Vector3.down * 0.05f - _faceSpray.transform.position;
                _faceSpray.transform.rotation = Quaternion.LookRotation(d.normalized);
                _faceSpray.Emit(100);
            }

            if (visor != null)
                StartCoroutine(visor.Splash(0.9f));
            Splashed = true;

            // shards fly, bounce once on the table and settle
            var n = _shards != null ? _shards.Length : 0;
            var vel = new Vector3[n];
            var ang = new Vector3[n];
            var rnd = new System.Random(9);
            for (var i = 0; i < n; i++)
            {
                _shards[i].localPosition = _shardHome[i];
                _shards[i].localRotation = _shardHomeRot[i];
                var a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var sp = 0.5f + (float)rnd.NextDouble() * 0.9f;
                vel[i] = new Vector3(Mathf.Cos(a) * sp, 0.9f + (float)rnd.NextDouble() * 1.1f, Mathf.Sin(a) * sp);
                // keep the shards on the table: the ones thrown toward the front edge are slowed
                ang[i] = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 900f;
            }

            if (puddleTable != null)
                puddleTable.gameObject.SetActive(true);
            if (drip != null)
                drip.Play();
            var landedFlag = new bool[n];
            var puddleStart = puddleTable != null ? puddleTable.localScale : Vector3.one;
            for (var t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                var dt = Time.deltaTime;
                for (var i = 0; i < n; i++)
                {
                    if (landedFlag[i])
                        continue;
                    vel[i].y -= 9.81f * dt;
                    var sh = _shards[i];
                    sh.position += vel[i] * dt;
                    sh.Rotate(ang[i] * dt, Space.World);
                    if (sh.position.y <= tableTopY + 0.004f && vel[i].y < 0f)
                    {
                        var p = sh.position;
                        p.y = tableTopY + 0.004f;
                        sh.position = p;
                        if (vel[i].y < -1.2f)
                        {
                            vel[i] = new Vector3(vel[i].x * 0.4f, -vel[i].y * 0.28f, vel[i].z * 0.4f);
                            ang[i] *= 0.4f;
                        }
                        else
                        {
                            landedFlag[i] = true;
                            var e = sh.eulerAngles;
                            sh.rotation = Quaternion.Euler(Mathf.Round(e.x / 90f) * 90f * 0.3f, e.y, Mathf.Round(e.z / 90f) * 90f * 0.3f);
                        }
                    }
                }

                var k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 2.0f), 3f);
                if (puddleTable != null)
                    puddleTable.localScale = new Vector3(Mathf.Lerp(0.05f, puddleTableSize.x, k), Mathf.Lerp(0.05f, puddleTableSize.y, k), 1f);
                yield return null;
            }

            if (spills != null)
                spills.gameObject.SetActive(true);
            // the floor puddle spreads slowly under the table edge while the user walks away
            if (puddleFloor != null)
            {
                puddleFloor.gameObject.SetActive(true);
                for (var t = 0f; t < 5f; t += Time.deltaTime)
                {
                    var k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 5f), 2f);
                    puddleFloor.localScale = new Vector3(Mathf.Lerp(0.05f, puddleFloorSize.x, k), Mathf.Lerp(0.05f, puddleFloorSize.y, k), 1f);
                    yield return null;
                }

                puddleFloor.localScale = new Vector3(puddleFloorSize.x, puddleFloorSize.y, 1f);
            }

            Finished = true;
        }

        // ------------------------------------------------------------------ liquid in the jar

        private void SetLiquid(float level)
        {
            if (liquid == null)
                return;
            liquid.gameObject.SetActive(level > 0.001f);
            liquid.localScale = new Vector3(_liquidBaseScale.x, Mathf.Max(0.0001f, _liquidBaseScale.y * level), _liquidBaseScale.z);
            if (liquidRenderer != null)
            {
                _liquidBlock ??= new MaterialPropertyBlock();
                _liquidBlock.SetColor("_BaseColor", new Color(acidColor.r, acidColor.g, acidColor.b, 0.7f));
                liquidRenderer.SetPropertyBlock(_liquidBlock);
            }

            if (liquidTop != null)
            {
                var b = new MaterialPropertyBlock();
                b.SetColor("_BaseColor", new Color(Mathf.Lerp(acidColor.r, 1f, 0.35f), Mathf.Lerp(acidColor.g, 1f, 0.35f), Mathf.Lerp(acidColor.b, 1f, 0.35f), 0.85f));
                liquidTop.SetPropertyBlock(b);
            }
        }

        // ------------------------------------------------------------------ cap

        private IEnumerator LiftCapOff()
        {
            if (_cap == null || _cap.go == null)
                yield break;
            var t = _cap.go.transform;
            t.SetParent(transform.parent, true);
            var start = t.position;
            var startRot = t.rotation;
            var lifted = start + Vector3.up * 0.1f;
            for (var k = 0f; k < 0.8f; k += Time.deltaTime)
            {
                var e = Mathf.SmoothStep(0f, 1f, k / 0.8f);
                t.position = Vector3.Lerp(start, lifted, e);
                t.rotation = startRot * Quaternion.Euler(0f, 720f * e, 0f);
                yield return null;
            }

            // set down on the table on the side away from the jar
            var away = Flat(_acidHome - _jarHome).normalized;
            var setDown = new Vector3(_acidHome.x, tableTopY + 0.03f, _acidHome.z) + away * 0.26f;
            var liftedRot = t.rotation;
            for (var k = 0f; k < 0.7f; k += Time.deltaTime)
            {
                var e = Mathf.SmoothStep(0f, 1f, k / 0.7f);
                t.position = Vector3.Lerp(lifted, setDown, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.05f);
                t.rotation = liftedRot;
                yield return null;
            }

            t.position = setDown;
        }

        private static void RestoreCap(Cap cap)
        {
            if (cap == null || cap.go == null)
                return;
            if (cap.parent != null)
            {
                cap.go.transform.SetParent(cap.parent, false);
                cap.go.transform.localPosition = cap.localPos;
                cap.go.transform.localRotation = cap.localRot;
                cap.go.transform.localScale = cap.localScale;
            }

            cap.go.SetActive(true);
        }

        // ------------------------------------------------------------------ clearance over the jar (as Scenario 8's pour)

        private const float JarFootprint = 0.118f;

        private void CollectCorners(Transform item)
        {
            _corners.Clear();
            foreach (var r in item.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                var b = r.localBounds;
                const int n = 5;
                for (var ix = 0; ix < n; ix++)
                    for (var iy = 0; iy < n; iy++)
                        for (var iz = 0; iz < n; iz++)
                        {
                            var f = new Vector3(ix / (n - 1f) * 2f - 1f, iy / (n - 1f) * 2f - 1f, iz / (n - 1f) * 2f - 1f);
                            var c = b.center + Vector3.Scale(b.extents, f);
                            _corners.Add(item.InverseTransformPoint(r.transform.TransformPoint(c)));
                        }
            }
        }

        private float RequiredLift(Quaternion rot, Vector3 basePos, Vector3 scale)
        {
            var axis = jarMouth.position;
            var lift = 0f;
            foreach (var c in _corners)
            {
                var w = basePos + rot * Vector3.Scale(c, scale);
                var dx = w.x - axis.x;
                var dz = w.z - axis.z;
                if (Mathf.Sqrt(dx * dx + dz * dz) > JarFootprint)
                    continue;
                lift = Mathf.Max(lift, axis.y + 0.03f - w.y);
            }

            return lift;
        }

        // ------------------------------------------------------------------ reset

        /// <summary>Jar, can and cap back where Scenario 6 and 8 expect them; every accident prop hidden.</summary>
        public void ResetAll()
        {
            Capture();
            StopAllCoroutines();
            Running = false;
            Splashed = false;
            Finished = false;
            JarBroken = false;
            if (jar == null || acid == null)
                return;
            jar.gameObject.SetActive(true);
            jar.SetPositionAndRotation(_jarHome, _jarHomeRot);
            acid.SetPositionAndRotation(_acidHome, _acidHomeRot);
            RestoreCap(_cap);
            if (liquid != null)
            {
                liquid.localScale = new Vector3(_liquidBaseScale.x, 0.0001f, _liquidBaseScale.z);
                liquid.gameObject.SetActive(false);
            }

            if (liquidRenderer != null)
                liquidRenderer.SetPropertyBlock(null);
            if (liquidTop != null)
                liquidTop.SetPropertyBlock(null);
            if (broken != null)
                broken.gameObject.SetActive(false);
            if (_shards != null)
                for (var i = 0; i < _shards.Length; i++)
                {
                    _shards[i].localPosition = _shardHome[i];
                    _shards[i].localRotation = _shardHomeRot[i];
                }

            if (puddleTable != null)
            {
                puddleTable.gameObject.SetActive(false);
                puddleTable.localScale = new Vector3(0.05f, 0.05f, 1f);
            }

            if (puddleFloor != null)
            {
                puddleFloor.gameObject.SetActive(false);
                puddleFloor.localScale = new Vector3(0.05f, 0.05f, 1f);
            }

            if (spills != null)
                spills.gameObject.SetActive(false);
            if (drip != null)
                drip.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_stream != null)
                _stream.enabled = false;
            if (_burst != null)
                _burst.Clear();
            if (_faceSpray != null)
                _faceSpray.Clear();
            if (visor != null)
                visor.Hide();
        }
    }
}
