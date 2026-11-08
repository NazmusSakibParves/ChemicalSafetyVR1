using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 8 (controller ray): the automatic acid dilution in the Scenario 6 factory room. It drives the SAME three containers on the
    /// steel table that Scenario 6 uses (empty mixture jar, water jug, acid can) but it is a separate component: <see cref="Sc06AutoPour"/>
    /// is never touched. The containers are carried into the running fume hood one after another; then the water is poured into the empty
    /// jar FIRST, the glass rod comes from the table into the jar and stirs, and the acid is added slowly while the rod keeps stirring.
    /// Everything moves on smooth arcs with fixed slots, so nothing passes through anything. No click is needed.
    /// </summary>
    public sealed class Sc08AutoDilute : MonoBehaviour
    {
        [Header("Containers (pivot at the base centre, upright)")]
        [SerializeField] private Transform jar;
        [SerializeField] private Transform water;
        [SerializeField] private Transform acid;
        [Header("Anchors")]
        [SerializeField] private Transform jarMouth;
        [SerializeField] private Transform waterSpout;
        [SerializeField] private Transform acidSpout;
        [SerializeField] private Transform jarSlot;
        [SerializeField] private Transform waterSlot;
        [SerializeField] private Transform acidSlot;
        [Header("Caps")]
        [SerializeField] private GameObject acidCap;
        [SerializeField] private GameObject waterCap;
        [Header("Glass rod (root = the point where the rod crosses the jar mouth, axis = local Y)")]
        [SerializeField] private Transform rod;
        [SerializeField] private Transform rodShaft;
        [SerializeField] private Transform rodHome;
        [SerializeField] private float rodAboveMouth = 0.05f;
        [SerializeField] private float rodTiltDegrees = 9f;
        [Header("Liquid and stream")]
        [SerializeField] private Transform liquid;
        [SerializeField] private float liquidFullHeight = 0.2f;
        [SerializeField] private Renderer liquidRenderer;
        [SerializeField] private Renderer liquidTop;
        [SerializeField] private Color waterColor = new Color(0.38f, 0.70f, 1.00f, 0.72f);
        [SerializeField] private Color mixColor = new Color(0.80f, 0.92f, 0.74f, 0.68f);
        [SerializeField] private Color waterStreamColor = new Color(0.62f, 0.84f, 1f, 0.78f);
        [SerializeField] private Color acidStreamColor = new Color(0.95f, 0.93f, 0.78f, 0.8f);
        [SerializeField] private Transform stream;
        [SerializeField] private Renderer streamRenderer;
        [SerializeField] private Transform splash;
        [SerializeField] private ParticleSystem fumes;
        [SerializeField] private Sc06Ambience ambience;
        [Header("Timing")]
        [SerializeField] private float carrySeconds = 1.9f;
        [SerializeField] private float waterPourSeconds = 3.0f;
        [SerializeField] private float acidPourSeconds = 5.5f;
        [SerializeField] private float settleSeconds = 0.5f;
        [SerializeField] private float waterLevel = 0.55f;
        [SerializeField] private float finalLevel = 0.85f;

        private sealed class Cap
        {
            public GameObject go;
            public Transform parent;
            public Vector3 localPos;
            public Quaternion localRot;
        }

        private Cap _acidCap;
        private Cap _waterCap;
        private readonly Vector3[] _homePos = new Vector3[3];
        private readonly Quaternion[] _homeRot = new Quaternion[3];
        private Transform[] _items;
        private bool _captured;
        private Vector3 _liquidBaseScale = Vector3.one;
        private float _level;
        private MaterialPropertyBlock _liquidBlock;
        private MaterialPropertyBlock _topBlock;
        private MaterialPropertyBlock _streamBlock;
        private bool _stirring;
        private float _stirPhase;
        private Vector3 _rodHomePos;
        private Quaternion _rodHomeRot;
        private float _jarDepth = 0.12f;
        private bool _pourGo = true;
        // the corners of the pouring container's parts (in its own space) and the radius around the jar axis that must stay clear
        private readonly List<Vector3> _corners = new List<Vector3>();
        private const float JarFootprint = 0.118f;
        // the stream lands a little BEHIND the jar's centre (away from the learner), so the container stands clear behind it and the
        // stream is seen going into the jar; still well inside the opening (radius about 0.10) and clear of the rod at the centre
        [SerializeField] private float pourBackShift = 0.07f;

        public bool Running { get; private set; }
        /// <summary>1 jar, 2 water jug, 3 acid can carried into the hood; 4 water poured into the jar; 5 rod into the jar; 6 acid added while stirring.</summary>
        public int Step { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>True once the three containers stand in the hood; the pour waits there until <see cref="ContinuePour"/>.</summary>
        public bool Carried { get; private set; }
        public bool Stirring => _stirring;
        public Transform Jar => jar;
        public Transform Rod => rod;
        public float Level => _level;
        public float MinSeparation { get; private set; } = float.MaxValue;

        private static Cap MakeCap(GameObject go)
        {
            if (go == null)
                return null;
            return new Cap { go = go, parent = go.transform.parent, localPos = go.transform.localPosition, localRot = go.transform.localRotation };
        }

        private void Capture()
        {
            if (_captured)
                return;
            _items = new[] { jar, water, acid };
            for (var i = 0; i < 3; i++)
            {
                _homePos[i] = _items[i].position;
                _homeRot[i] = _items[i].rotation;
            }

            _acidCap = MakeCap(acidCap);
            _waterCap = MakeCap(waterCap);
            if (liquid != null)
            {
                _liquidBaseScale = liquid.localScale;
                // Scenario 6's component may already have squashed the shared liquid to "empty" (Y near 0) before this one woke up:
                // the real full-size scale of the pivot is 1
                if (_liquidBaseScale.y < 0.5f)
                    _liquidBaseScale = Vector3.one;
            }
            _liquidBlock = new MaterialPropertyBlock();
            _topBlock = new MaterialPropertyBlock();
            _streamBlock = new MaterialPropertyBlock();

            // how deep the jar is (mouth to floor): the rod tip stops just above the floor
            var b = new Bounds(jar.position, Vector3.zero);
            foreach (var r in jar.GetComponentsInChildren<Renderer>(false))
                b.Encapsulate(r.bounds);
            _jarDepth = Mathf.Max(0.06f, jarMouth.position.y - b.min.y);
            if (rod != null && rodHome != null)
            {
                _rodHomePos = rodHome.position;
                _rodHomeRot = rodHome.rotation;
                if (rodShaft != null)
                {
                    var below = _jarDepth * 0.9f;
                    var length = below + rodAboveMouth;
                    rodShaft.localScale = new Vector3(rodShaft.localScale.x, length * 0.5f, rodShaft.localScale.z);
                    rodShaft.localPosition = new Vector3(0f, (rodAboveMouth - below) * 0.5f, 0f);
                }
            }

            _captured = true;
        }

        private void Awake()
        {
            Capture();
            ResetAll();
        }

        /// <summary>Shows or hides the glass rod (it only exists in Scenario 8).</summary>
        public void SetRodVisible(bool on)
        {
            if (rod != null)
                rod.gameObject.SetActive(on);
        }

        /// <summary>Everything back on the table, jar empty, rod on the table, stream off (the PREVIEW button and the scenario start).</summary>
        public void ResetAll()
        {
            Capture();
            StopAllCoroutines();
            Running = false;
            Finished = false;
            Step = 0;
            _stirring = false;
            Carried = false;
            _pourGo = true;
            for (var i = 0; i < 3; i++)
            {
                _items[i].position = _homePos[i];
                _items[i].rotation = _homeRot[i];
            }

            RestoreCap(_acidCap);
            RestoreCap(_waterCap);
            if (rod != null && rodHome != null)
                rod.SetPositionAndRotation(_rodHomePos, _rodHomeRot);
            SetLevel(0f, 0f);
            SetStream(false, Vector3.zero, Vector3.zero, false);
            if (streamRenderer != null)
                streamRenderer.SetPropertyBlock(null); // the stream colour is Scenario 6's again
            if (liquidRenderer != null)
                liquidRenderer.SetPropertyBlock(null);
            if (liquidTop != null)
                liquidTop.SetPropertyBlock(null);
            if (fumes != null)
            {
                var em = fumes.emission;
                em.enabled = false;
                fumes.Clear();
            }

            if (ambience != null)
                ambience.SetPour(false);
            MinSeparation = float.MaxValue;
        }

        /// <param name="waitBeforePour">true = stop after the carrying (Carried = true) and go on only after ContinuePour().</param>
        public void StartPlay(bool waitBeforePour = false)
        {
            Capture();
            Running = true;
            Finished = false;
            Carried = false;
            _pourGo = !waitBeforePour;
            Step = 0;
            StartCoroutine(Play());
        }

        public void ContinuePour()
        {
            _pourGo = true;
        }

        private IEnumerator Play()
        {
            Running = true;
            Finished = false;
            Step = 1;
            yield return Carry(jar, jarSlot, 0.22f);
            yield return PresentableFx.Wait(settleSeconds);
            Step = 2;
            yield return Carry(water, waterSlot, 0.24f);
            yield return PresentableFx.Wait(settleSeconds);
            Step = 3;
            yield return Carry(acid, acidSlot, 0.2f);
            yield return PresentableFx.Wait(settleSeconds);
            Carried = true;
            while (!_pourGo)
                yield return null;
            yield return PresentableFx.Wait(0.3f);
            // the water goes in first
            Step = 4;
            yield return Pour(water, waterSpout, waterSlot, 0f, waterLevel, 0f, 0f, waterPourSeconds, _waterCap, false, waterStreamColor, 0.08f, 108f);
            yield return PresentableFx.Wait(0.4f);
            // the glass rod comes from the table into the jar and keeps stirring
            Step = 5;
            yield return RodIn();
            _stirring = true;
            StartCoroutine(StirLoop());
            yield return PresentableFx.Wait(1.2f);
            // the acid is added slowly while the rod stirs
            Step = 6;
            yield return Pour(acid, acidSpout, acidSlot, waterLevel, finalLevel, 0f, 1f, acidPourSeconds, _acidCap, true, acidStreamColor, 0.09f, 112f);
            yield return PresentableFx.Wait(2.4f);
            _stirring = false;
            yield return PresentableFx.Wait(0.5f);
            Running = false;
            Finished = true;
        }

        private void Measure()
        {
            if (jar == null || water == null || acid == null)
                return;
            var a = Flat(jar.position - water.position).magnitude;
            var b = Flat(jar.position - acid.position).magnitude;
            var c = Flat(water.position - acid.position).magnitude;
            MinSeparation = Mathf.Min(MinSeparation, Mathf.Min(a, Mathf.Min(b, c)));
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private IEnumerator Carry(Transform item, Transform slot, float lift)
        {
            var from = item.position;
            var fromRot = item.rotation;
            var to = slot.position;
            var toRot = Quaternion.identity;
            for (var t = 0f; t < carrySeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / carrySeconds);
                item.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * lift);
                item.rotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }

            item.position = to;
            item.rotation = toRot;
            Measure();
        }

        // The rod pose with its crossing point at the jar mouth: the shaft leans by rodTiltDegrees, swung round the vertical by 'swing'.
        private Pose RodPose(float swing)
        {
            var rot = Quaternion.AngleAxis(swing, Vector3.up) * Quaternion.Euler(rodTiltDegrees, 0f, 0f);
            return new Pose(jarMouth.position, rot);
        }

        private IEnumerator RodIn()
        {
            if (rod == null)
                yield break;
            var from = rod.position;
            var fromRot = rod.rotation;
            var to = RodPose(0f);
            // up and over: it rises first, then settles into the jar (the jar mouth is free: nothing else is above it now)
            for (var t = 0f; t < carrySeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / carrySeconds);
                rod.position = Vector3.Lerp(from, to.position, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.28f);
                rod.rotation = Quaternion.Slerp(fromRot, to.rotation, k);
                yield return null;
            }

            rod.SetPositionAndRotation(to.position, to.rotation);
        }

        // Round and round, a little slower while the acid goes in.
        private IEnumerator StirLoop()
        {
            while (_stirring)
            {
                _stirPhase += Time.deltaTime * 330f;
                var p = RodPose(_stirPhase);
                rod.SetPositionAndRotation(p.position, p.rotation);
                yield return null;
            }
        }

        // The container leans over the jar on the far side of it, spout above the jar mouth; the level rises while the stream runs.
        private IEnumerator Pour(Transform item, Transform spout, Transform slot, float levelFrom, float levelTo, float mixFrom, float mixTo,
            float seconds, Cap cap, bool acidPour, Color streamColor, float hover, float endAngle)
        {
            yield return LiftCapOff(cap, slot);
            var toward = Flat(jarSlot.position - slot.position).normalized;
            var axis = Vector3.Cross(Vector3.up, toward);
            // the spout offset in WORLD units while the container stands upright (the root of the container is scaled: without the scale the
            // neck would end up beside the jar and the stream would fall on a slant)
            var spoutLocal = Vector3.Scale(item.InverseTransformPoint(spout.position), item.lossyScale);
            var lipFlat = new Vector3(spoutLocal.x, 0f, spoutLocal.z);
            var yaw = lipFlat.magnitude > 0.02f ? Vector3.SignedAngle(lipFlat, toward, Vector3.up) : 0f;
            var yawRot = Quaternion.AngleAxis(yaw, Vector3.up);
            // the neck is the lowest point of the tilted container, so the spout hovers only a hand's width above the jar mouth
            var mouth = jarMouth.position + Vector3.up * hover + Vector3.back * pourBackShift;

            CollectCorners(item); // (the cap is already off: it is not part of the container any more)

            Pose PoseAt(float angle)
            {
                var rot = Quaternion.AngleAxis(angle, axis) * yawRot;
                var pos = mouth - rot * spoutLocal;
                // nothing of the container may dip into the jar: lift exactly as far as its lowest part over the jar needs
                pos.y += RequiredLift(rot, pos, item.lossyScale);
                return new Pose(pos, rot);
            }

            var startPos = item.position;
            var startRot = item.rotation;
            var p0 = PoseAt(55f);
            for (var t = 0f; t < carrySeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / carrySeconds);
                item.position = Vector3.Lerp(startPos, p0.position, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.07f);
                item.rotation = Quaternion.Slerp(startRot, p0.rotation, k);
                yield return null;
            }

            item.SetPositionAndRotation(p0.position, p0.rotation);
            if (acidPour && fumes != null)
            {
                var em = fumes.emission;
                em.enabled = true;
            }

            if (ambience != null)
                ambience.SetPour(true);
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = t / seconds;
                var angle = Mathf.Lerp(55f, endAngle, Mathf.SmoothStep(0f, 1f, k));
                var pose = PoseAt(angle);
                item.SetPositionAndRotation(pose.position, pose.rotation);
                var lv = Mathf.Lerp(levelFrom, levelTo, Mathf.SmoothStep(0f, 1f, k));
                SetStream(true, spout.position, LiquidSurface(lv), true, streamColor);
                SetLevel(lv, Mathf.Lerp(mixFrom, mixTo, k));
                yield return null;
            }

            SetLevel(levelTo, mixTo);
            SetStream(false, Vector3.zero, Vector3.zero, false);
            if (ambience != null)
                ambience.SetPour(false);
            var back = item.position;
            var backRot = item.rotation;
            for (var t = 0f; t < carrySeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / carrySeconds);
                item.position = Vector3.Lerp(back, slot.position, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.06f);
                item.rotation = Quaternion.Slerp(backRot, Quaternion.identity, k);
                yield return null;
            }

            item.position = slot.position;
            item.rotation = Quaternion.identity;
            yield return ReplaceCap(cap);
            if (fumes != null)
            {
                var em = fumes.emission;
                em.enabled = false;
            }

            Measure();
            yield return PresentableFx.Wait(settleSeconds);
        }

        private void CollectCorners(Transform item)
        {
            _corners.Clear();
            foreach (var r in item.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                // a dense grid over the part's box (not only its 8 corners: an edge can cross the jar between two corners that are both outside it)
                var b = r.localBounds;
                const int n = 6;
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

        // How much higher the container has to be so that every part of it that is above the jar's opening stays clear of the rim
        // (and, once the rod is in, of the top of the rod around the axis).
        private float RequiredLift(Quaternion rot, Vector3 basePos, Vector3 scale)
        {
            var axis = jarMouth.position;
            var lift = 0f;
            foreach (var c in _corners)
            {
                var w = basePos + rot * Vector3.Scale(c, scale);
                var dx = w.x - axis.x;
                var dz = w.z - axis.z;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > JarFootprint)
                    continue;
                var clear = _stirring && d < 0.05f ? rodAboveMouth + 0.035f : 0.03f;
                lift = Mathf.Max(lift, axis.y + clear - w.y);
            }

            return lift;
        }

        private IEnumerator LiftCapOff(Cap cap, Transform slot)
        {
            if (cap == null || cap.go == null)
                yield break;
            var t = cap.go.transform;
            t.SetParent(transform.parent, true);
            var start = t.position;
            var startRot = t.rotation;
            var lifted = start + Vector3.up * 0.1f;
            for (var k = 0f; k < 0.9f; k += Time.deltaTime)
            {
                var e = Mathf.SmoothStep(0f, 1f, k / 0.9f);
                t.position = Vector3.Lerp(start, lifted, e);
                t.rotation = startRot * Quaternion.Euler(0f, 720f * e, 0f);
                yield return null;
            }

            var dir = Flat(slot.position - jarSlot.position).normalized;
            var setDown = slot.position + dir * 0.30f + Vector3.up * 0.03f;
            var liftedRot = t.rotation;
            for (var k = 0f; k < 0.8f; k += Time.deltaTime)
            {
                var e = Mathf.SmoothStep(0f, 1f, k / 0.8f);
                t.position = Vector3.Lerp(lifted, setDown, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.06f);
                t.rotation = liftedRot;
                yield return null;
            }

            t.position = setDown;
            yield return PresentableFx.Wait(0.2f);
        }

        private IEnumerator ReplaceCap(Cap cap)
        {
            if (cap == null || cap.go == null || cap.parent == null)
                yield break;
            var t = cap.go.transform;
            var home = cap.parent.TransformPoint(cap.localPos);
            var from = t.position;
            var fromRot = t.rotation;
            for (var k = 0f; k < 1.1f; k += Time.deltaTime)
            {
                var e = Mathf.SmoothStep(0f, 1f, k / 1.1f);
                t.position = Vector3.Lerp(from, home + Vector3.up * 0.08f * (1f - e), e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.06f);
                t.rotation = fromRot * Quaternion.Euler(0f, -540f * e, 0f);
                yield return null;
            }

            t.SetParent(cap.parent, false);
            t.localPosition = cap.localPos;
            t.localRotation = cap.localRot;
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
            }

            cap.go.SetActive(true);
        }

        private Vector3 LiquidSurface(float level)
        {
            var basePos = liquid != null ? liquid.position : jarMouth.position;
            return new Vector3(jarMouth.position.x, basePos.y + liquidFullHeight * Mathf.Clamp01(level), jarMouth.position.z - pourBackShift);
        }

        private void SetLevel(float level, float mix)
        {
            _level = level;
            if (liquid == null)
                return;
            liquid.gameObject.SetActive(level > 0.001f);
            liquid.localScale = new Vector3(_liquidBaseScale.x, Mathf.Max(0.0001f, _liquidBaseScale.y * level), _liquidBaseScale.z);
            if (liquidRenderer != null)
            {
                _liquidBlock ??= new MaterialPropertyBlock();
                var c = Color.Lerp(waterColor, mixColor, Mathf.Clamp01(mix));
                _liquidBlock.SetColor("_BaseColor", c);
                liquidRenderer.SetPropertyBlock(_liquidBlock);
                if (liquidTop != null)
                {
                    // the surface: the same hue, lighter and a little more solid, so the rising level reads from above
                    _topBlock ??= new MaterialPropertyBlock();
                    _topBlock.SetColor("_BaseColor", new Color(Mathf.Lerp(c.r, 1f, 0.45f), Mathf.Lerp(c.g, 1f, 0.45f), Mathf.Lerp(c.b, 1f, 0.45f), 0.85f));
                    liquidTop.SetPropertyBlock(_topBlock);
                }
            }

            if (splash != null)
                splash.gameObject.SetActive(false);
        }

        private void SetStream(bool on, Vector3 from, Vector3 to, bool tint, Color color = default)
        {
            if (stream == null)
                return;
            stream.gameObject.SetActive(on);
            if (splash != null)
            {
                splash.gameObject.SetActive(on);
                if (on)
                    splash.position = to + Vector3.up * 0.002f;
            }

            if (!on)
                return;
            var d = to - from;
            var len = Mathf.Max(0.01f, d.magnitude);
            stream.position = from + d * 0.5f;
            stream.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            stream.localScale = new Vector3(0.012f, len * 0.5f, 0.012f);
            if (streamRenderer != null)
            {
                streamRenderer.enabled = true;
                if (tint)
                {
                    _streamBlock ??= new MaterialPropertyBlock();
                    _streamBlock.SetColor("_BaseColor", color);
                    streamRenderer.SetPropertyBlock(_streamBlock);
                }
            }
        }
    }
}
