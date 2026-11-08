using System.Collections;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 (controller ray): the automatic demonstration. The three containers on the steel table (the empty mixture jar, the water
    /// jug and the acid can) are carried one after another into the running fume hood on smooth arcs, each to its own slot on the hood
    /// floor, and the jar is filled: water first, then the acid slowly. One step starts only when the previous one is finished, the
    /// containers keep fixed slots at least their own footprint apart, and the pouring container leans away from the jar, so nothing
    /// ever overlaps or passes through another container. All motion is on this component; nothing here needs a click.
    /// </summary>
    public sealed class Sc06AutoPour : MonoBehaviour
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
        [Header("Liquid and stream")]
        [SerializeField] private GameObject acidCap;
        [SerializeField] private GameObject waterCap;
        [SerializeField] private Transform liquid;
        [SerializeField] private float liquidFullHeight = 0.2f;
        [SerializeField] private Renderer liquidRenderer;
        [SerializeField] private Color waterColor = new Color(0.93f, 0.92f, 0.76f, 0.55f); // sulfuric acid: pale and clear
        [SerializeField] private Color mixColor = new Color(0.93f, 0.92f, 0.76f, 0.6f);
        [SerializeField] private Transform stream;
        [SerializeField] private Renderer streamRenderer;
        [SerializeField] private Transform splash;
        [SerializeField] private ParticleSystem fumes;
        [SerializeField] private Sc06Ambience ambience;
        [Header("Timing")]
        [SerializeField] private float carrySeconds = 1.9f;
        [SerializeField] private float pourSeconds = 3.6f;
        [SerializeField] private float settleSeconds = 0.5f;

        private Vector3 _capHomeLocal;
        private Quaternion _capHomeRot;
        private Transform _capParent;
        private Vector3 _capSetDown;
        private readonly Vector3[] _homePos = new Vector3[3];
        private readonly Quaternion[] _homeRot = new Quaternion[3];
        private Transform[] _items;
        private bool _captured;
        private Vector3 _liquidBaseScale = Vector3.one;
        private float _level;
        private MaterialPropertyBlock _liquidBlock;

        public bool Running { get; private set; }
        /// <summary>1 carry the empty jar, 2 carry the water jug (not opened), 3 carry the acid can, 4 open the acid can and pour it into the jar.</summary>
        public int Step { get; private set; }
        public bool Finished { get; private set; }
        public Transform Jar => jar;
        public Transform Water => water;
        public Transform Acid => acid;
        public float Level => _level;

        /// <summary>Smallest distance between two containers' centres while they stand in the hood (the smoke test checks it).</summary>
        public float MinSeparation { get; private set; } = float.MaxValue;

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

            if (acidCap != null)
            {
                _capParent = acidCap.transform.parent;
                _capHomeLocal = acidCap.transform.localPosition;
                _capHomeRot = acidCap.transform.localRotation;
            }

            if (liquid != null)
                _liquidBaseScale = liquid.localScale;
            _liquidBlock = new MaterialPropertyBlock();
            _captured = true;
        }

        private void Awake()
        {
            Capture();
            ResetAll();
        }

        /// <summary>Everything back on the table, jar empty, stream off (the PREVIEW button and the scenario start).</summary>
        public void ResetAll()
        {
            Capture();
            StopAllCoroutines();
            Running = false;
            Finished = false;
            Step = 0;
            for (var i = 0; i < 3; i++)
            {
                _items[i].position = _homePos[i];
                _items[i].rotation = _homeRot[i];
            }

            RestoreCap();
            SetLevel(0f, 0f);
            SetStream(false, Vector3.zero, Vector3.zero);
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

        /// <summary>Starts the demonstration on this component (ResetAll stops it); poll Finished.</summary>
        public void StartPlay()
        {
            Capture();
            Running = true;
            Finished = false;
            Step = 0;
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            Capture();
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
            Step = 4;
            yield return Pour(acid, acidSpout, acidSlot, jarSlot, 0f, 0.8f, true);
            yield return PresentableFx.Wait(0.8f);
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

        // A smooth arc from where the container is to its slot on the hood floor (upright, facing the viewer).
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

        // The container leans over the jar on the far side of it, spout above the jar mouth; the jar level rises while the stream runs.
        private IEnumerator Pour(Transform item, Transform spout, Transform slot, Transform target, float levelFrom, float levelTo, bool acidPour)
        {
            if (acidPour)
                yield return LiftCapOff(); // unscrewed and set down beside the can (animated, no voice), while the can still stands upright
            var toward = Flat(target.position - slot.position).normalized;
            var axis = Vector3.Cross(Vector3.up, toward);
            var spoutLocal = item.InverseTransformPoint(spout.position); // at rest, upright
            // the lip (the cap end of a jerry can) is not at the centre: turn the container so that its lip faces the jar, then lean it
            var lipFlat = new Vector3(spoutLocal.x, 0f, spoutLocal.z);
            var yaw = lipFlat.magnitude > 0.02f ? Vector3.SignedAngle(lipFlat, toward, Vector3.up) : 0f;
            var yawRot = Quaternion.AngleAxis(yaw, Vector3.up);
            // hover height: the lower rim of the leaning container must clear the jar's rim even at the steepest lean
            var radius = 0.12f;
            var bounds = new Bounds(item.position, Vector3.zero);
            foreach (var r in item.GetComponentsInChildren<Renderer>(false))
                bounds.Encapsulate(r.bounds);
            radius = Mathf.Max(0.08f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            var mouth = jarMouth.position + Vector3.up * (0.05f + radius * 0.97f);

            Pose PoseAt(float angle)
            {
                var rot = Quaternion.AngleAxis(angle, axis) * yawRot;
                return new Pose(mouth - rot * spoutLocal, rot);
            }

            var startPos = item.position;
            var startRot = item.rotation;
            var p0 = PoseAt(52f);
            // to the pour pose: lift first, then lean (no path through the jar: the container comes from its own side)
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
            for (var t = 0f; t < pourSeconds; t += Time.deltaTime)
            {
                var k = t / pourSeconds;
                var angle = Mathf.Lerp(52f, 76f, Mathf.SmoothStep(0f, 1f, k));
                var pose = PoseAt(angle);
                item.SetPositionAndRotation(pose.position, pose.rotation);
                var surface = LiquidSurface(Mathf.Lerp(levelFrom, levelTo, k));
                SetStream(true, spout.position, surface);
                SetLevel(Mathf.Lerp(levelFrom, levelTo, Mathf.SmoothStep(0f, 1f, k)), acidPour ? k : 0f);
                yield return null;
            }

            SetLevel(levelTo, acidPour ? 1f : 0f);
            SetStream(false, Vector3.zero, Vector3.zero);
            if (ambience != null)
                ambience.SetPour(false);
            // upright again on its own slot (it leaves along the way it came, never over the jar)
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
            if (acidPour)
                yield return ReplaceCap(); // screwed back on
            if (fumes != null)
            {
                var em = fumes.emission;
                em.enabled = false;
            }

            Measure();
            yield return PresentableFx.Wait(settleSeconds);
        }

        // The cap unscrews (turns while rising), travels in a small arc to the hood floor beside the can and is set down there, visible
        // for the whole pour; afterwards it goes back on (ReplaceCap). It is free of the can in between, so it stays when the can leans.
        private IEnumerator LiftCapOff()
        {
            if (acidCap == null)
                yield break;
            var t = acidCap.transform;
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

            var dir = Flat(acidSlot.position - jarSlot.position).normalized;
            _capSetDown = acidSlot.position + dir * 0.30f + Vector3.up * 0.03f;
            var liftedRot = t.rotation;
            for (var k = 0f; k < 0.8f; k += Time.deltaTime)
            {
                var e = Mathf.SmoothStep(0f, 1f, k / 0.8f);
                t.position = Vector3.Lerp(lifted, _capSetDown, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.06f);
                t.rotation = liftedRot;
                yield return null;
            }

            t.position = _capSetDown;
            yield return PresentableFx.Wait(0.2f);
        }

        // The cap flies back to the can's mouth and screws on again.
        private IEnumerator ReplaceCap()
        {
            if (acidCap == null)
                yield break;
            var t = acidCap.transform;
            if (_capParent == null)
                yield break;
            var home = _capParent.TransformPoint(_capHomeLocal);
            var homeRot = _capParent.rotation * _capHomeRot;
            var from = t.position;
            var fromRot = t.rotation;
            for (var k = 0f; k < 1.1f; k += Time.deltaTime)
            {
                var e = Mathf.SmoothStep(0f, 1f, k / 1.1f);
                t.position = Vector3.Lerp(from, home + Vector3.up * 0.08f * (1f - e), e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.06f);
                t.rotation = fromRot * Quaternion.Euler(0f, -540f * e, 0f);
                yield return null;
            }

            t.SetParent(_capParent, false);
            t.localPosition = _capHomeLocal;
            t.localRotation = _capHomeRot;
            _ = homeRot;
        }

        private void RestoreCap()
        {
            if (acidCap == null)
                return;
            if (_capParent != null)
            {
                acidCap.transform.SetParent(_capParent, false);
                acidCap.transform.localPosition = _capHomeLocal;
                acidCap.transform.localRotation = _capHomeRot;
            }

            acidCap.SetActive(true);
            if (waterCap != null)
                waterCap.SetActive(true);
        }

        private Vector3 LiquidSurface(float level)
        {
            var basePos = liquid != null ? liquid.position : jarMouth.position;
            return new Vector3(jarMouth.position.x, basePos.y + liquidFullHeight * Mathf.Clamp01(level), jarMouth.position.z);
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
                _liquidBlock.SetColor("_BaseColor", Color.Lerp(waterColor, mixColor, Mathf.Clamp01(mix)));
                liquidRenderer.SetPropertyBlock(_liquidBlock);
            }

            if (splash != null)
            {
                splash.gameObject.SetActive(false);
            }
        }

        private void SetStream(bool on, Vector3 from, Vector3 to)
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
            stream.localScale = new Vector3(0.012f, len * 0.5f, 0.012f); // unit cylinder: height 2 along Y
            if (streamRenderer != null)
                streamRenderer.enabled = true;
        }
    }
}
