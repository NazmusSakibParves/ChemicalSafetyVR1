using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 10 (controller ray, Scenario 6/8/9 factory room): the room is shown AFTER the accident. The acid can lies on its side on the
    /// table with its mouth towards the front edge, the acid runs out of it over the table, falls over the edge as a thin glossy runnel and
    /// drips onto the floor puddle; the glass jar is gone from the table (its pieces lie on the floor, built by the Sc10 bay). Uses the SAME
    /// jar, can and cap objects as Scenarios 6, 8 and 9 and puts every one of them back in <see cref="ResetAll"/>, so the shared room is
    /// left exactly as the other scenarios expect it. Nothing here is shown outside Scenario 10.
    /// </summary>
    public sealed class Sc10Accident : MonoBehaviour
    {
        [Header("Shared table containers (same objects as Scenario 6/8/9)")]
        [SerializeField] private Transform jar;
        [SerializeField] private Transform acid;
        [SerializeField] private Transform acidSpout;
        [SerializeField] private GameObject acidCap;
        [Header("Accident props (built hidden)")]
        [SerializeField] private Transform tablePuddle;
        [SerializeField] private Transform tableSplats;
        [SerializeField] private ParticleSystem edgeDrip;
        [SerializeField] private Material runnelMaterial;
        [SerializeField] private Vector2 puddleTableSize = new Vector2(1.35f, 0.80f);
        [Header("Table")]
        [SerializeField] private float tableTopY = 0.92f;
        [SerializeField] private float tableFrontZ = 0.93f;
        [Tooltip("Where the runnel lands on the floor (x, z).")]
        [SerializeField] private Vector2 floorLanding = new Vector2(200f, 1.1f);
        [Tooltip("World x the can's mouth is moved to: the acid runs off the table straight above the floor puddle.")]
        [SerializeField] private float mouthTargetX = float.NaN;
        [SerializeField] private float canFrontInset = 0.30f;
        [SerializeField] private float canOffsetX = -0.10f;

        private bool _captured;
        private Vector3 _jarHome;
        private Quaternion _jarHomeRot;
        private Vector3 _acidHome;
        private Quaternion _acidHomeRot;
        private Transform _capParent;
        private Vector3 _capPos;
        private Quaternion _capRot;
        private Vector3 _capScale;
        private LineRenderer _runnel;
        private Coroutine _fx;

        public bool Shown { get; private set; }
        public bool CanUpright { get; private set; }
        public Vector3 SpoutWorld => acidSpout != null ? acidSpout.position : Vector3.zero;

        private void Capture()
        {
            if (_captured || jar == null || acid == null)
                return;
            _jarHome = jar.position;
            _jarHomeRot = jar.rotation;
            _acidHome = acid.position;
            _acidHomeRot = acid.rotation;
            if (acidCap != null)
            {
                _capParent = acidCap.transform.parent;
                _capPos = acidCap.transform.localPosition;
                _capRot = acidCap.transform.localRotation;
                _capScale = acidCap.transform.localScale;
            }

            var go = new GameObject("Sc10Runnel");
            go.transform.SetParent(transform, false);
            _runnel = go.AddComponent<LineRenderer>();
            _runnel.useWorldSpace = true;
            _runnel.positionCount = 14;
            _runnel.numCapVertices = 4;
            _runnel.shadowCastingMode = ShadowCastingMode.Off;
            _runnel.receiveShadows = false;
            _runnel.sharedMaterial = runnelMaterial;
            _runnel.alignment = LineAlignment.View;
            _runnel.startWidth = 0.022f;
            _runnel.endWidth = 0.012f;
            _runnel.enabled = false;
            _captured = true;
        }

        private static float LowestY(Transform root)
        {
            var lowest = float.MaxValue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                if (!(r is ParticleSystemRenderer) && !(r is LineRenderer))
                    lowest = Mathf.Min(lowest, r.bounds.min.y);
            return lowest;
        }

        /// <summary>The room right after the accident (all at rest; the acid keeps flowing slowly).</summary>
        public void Show()
        {
            Capture();
            if (!_captured)
                return;
            StopAllFx();
            Shown = true;
            CanUpright = false;
            jar.gameObject.SetActive(false);

            // the can lies on its side, mouth towards the front edge of the table (the yaw with the mouth furthest to the front wins)
            var rot = Quaternion.Euler(0f, 0f, 90f);
            var bestYaw = 0f;
            var bestZ = float.MinValue;
            var restXZ = new Vector3(_jarHome.x + canOffsetX, 0f, tableFrontZ - canFrontInset);
            acid.position = new Vector3(restXZ.x, tableTopY + 0.2f, restXZ.z);
            for (var yaw = 0f; yaw < 360f; yaw += 10f)
            {
                acid.rotation = Quaternion.Euler(0f, yaw, 90f);
                var dz = acidSpout != null ? acidSpout.position.z - acid.position.z : 0f;
                if (dz > bestZ)
                {
                    bestZ = dz;
                    bestYaw = yaw;
                }
            }

            rot = Quaternion.Euler(0f, bestYaw, 90f);
            acid.rotation = rot;
            var lowest = LowestY(acid);
            var p = acid.position;
            p.y += tableTopY + 0.003f - lowest;
            acid.position = p;
            // the mouth hangs a little over the front edge: shift the can so the mouth is 6 cm behind the edge, and sideways so the
            // mouth is exactly above the floor puddle (the acid must run down into the puddle, not beside it)
            if (acidSpout != null)
            {
                var over = acidSpout.position.z - (tableFrontZ - 0.06f);
                var side = float.IsNaN(mouthTargetX) ? 0f : mouthTargetX - acidSpout.position.x;
                acid.position += new Vector3(side, 0f, -over);
            }

            // the cap lies on the table beside the can
            if (acidCap != null)
            {
                var ct = acidCap.transform;
                ct.rotation = Quaternion.Euler(0f, bestYaw + 40f, 90f);
                var cp = acid.position + new Vector3(0.34f, 0f, -0.16f);
                ct.position = cp;
                var low = LowestY(ct);
                if (low < float.MaxValue)
                    ct.position += Vector3.up * (tableTopY + 0.002f - low);
            }

            if (tablePuddle != null)
            {
                tablePuddle.gameObject.SetActive(true);
                var mouth = acidSpout != null ? acidSpout.position : acid.position;
                // the acid has run over the table to the front edge and smeared out there (the pool ends 2 cm short of the edge)
                tablePuddle.rotation = Quaternion.Euler(90f, 0f, 0f);
                tablePuddle.position = new Vector3(mouth.x, tableTopY + 0.0035f, tableFrontZ - 0.02f - puddleTableSize.y * 0.5f);
                tablePuddle.localScale = new Vector3(0.06f, 0.06f, 1f);
            }

            if (tableSplats != null)
                tableSplats.gameObject.SetActive(true);
            _fx = StartCoroutine(Flow());
        }

        private IEnumerator Flow()
        {
            _runnel.enabled = false;
            if (edgeDrip != null)
                edgeDrip.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // the leak has been going on for a while when the learner arrives: the pool on the table is already big, the acid is already
            // running over the edge and dripping onto the floor (nothing starts or grows after the instruction card)
            var mouth = acidSpout != null ? acidSpout.position : acid.position;
            var edgeX = mouth.x;
            if (tablePuddle != null)
                tablePuddle.localScale = new Vector3(puddleTableSize.x, puddleTableSize.y, 1f);
            if (edgeDrip != null)
            {
                edgeDrip.transform.position = new Vector3(edgeX, tableTopY - 0.012f, tableFrontZ + 0.012f);
                edgeDrip.Play();
            }

            _runnel.enabled = true;
            while (true)
            {
                UpdateRunnel(edgeX, 1f);
                // the pool on the table creeps very slowly
                if (tablePuddle != null)
                {
                    var breathe = 1f + 0.012f * Mathf.Sin(Time.time * 0.8f);
                    tablePuddle.localScale = new Vector3(puddleTableSize.x * breathe, puddleTableSize.y * breathe, 1f);
                }

                yield return null;
            }
        }

        // a thin glossy runnel from the table edge to the floor puddle: it hugs the table front, then falls, wobbling a little
        private void UpdateRunnel(float edgeX, float grow)
        {
            if (_runnel == null)
                return;
            var yTop = tableTopY + 0.002f;
            var yBot = 0.03f;
            var n = _runnel.positionCount;
            var width = Mathf.Lerp(0.0f, 0.03f, grow) * (1f + 0.12f * Mathf.Sin(Time.time * 2.3f));
            _runnel.startWidth = width;
            _runnel.endWidth = width * 0.55f;
            for (var i = 0; i < n; i++)
            {
                var f = i / (float)(n - 1);
                var y = Mathf.Lerp(yTop, yBot, f);
                // over the rounded edge first, then straight down
                var zz = tableFrontZ + Mathf.Lerp(0.004f, 0.045f, Mathf.Sqrt(f));
                var xx = edgeX; // straight down
                xx += Mathf.Sin(f * 11f + Time.time * 5f) * 0.0025f * f;
                _runnel.SetPosition(i, new Vector3(xx, y, zz));
            }
        }

        private void StopAllFx()
        {
            if (_fx != null)
                StopCoroutine(_fx);
            _fx = null;
            if (_runnel != null)
                _runnel.enabled = false;
            if (edgeDrip != null)
                edgeDrip.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// The first thing to do at a spill: stop the source. The can is lifted, turned upright and put down on the table with its cap on, the
        /// acid stops running and dripping, and the table is clean again (the cleaning itself is not shown).
        /// </summary>
        public IEnumerator StandCanUp()
        {
            if (!Shown || !_captured)
                yield break;
            StopAllFx(); // the runnel and the drops stop now
            var fromPos = acid.position;
            var fromRot = acid.rotation;
            var toPos = _acidHome;
            var toRot = _acidHomeRot;
            const float dur = 2.2f;
            var capBack = false;
            for (var t = 0f; t < dur; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / dur);
                acid.position = Vector3.Lerp(fromPos, toPos, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.18f);
                acid.rotation = Quaternion.Slerp(fromRot, toRot, k);
                if (!capBack && k > 0.55f && acidCap != null)
                {
                    capBack = true;
                    var ct = acidCap.transform;
                    ct.SetParent(_capParent, false);
                    ct.localPosition = _capPos;
                    ct.localRotation = _capRot;
                    ct.localScale = _capScale;
                }

                yield return null;
            }

            acid.SetPositionAndRotation(toPos, toRot);
            if (acidCap != null && !capBack)
            {
                var ct = acidCap.transform;
                ct.SetParent(_capParent, false);
                ct.localPosition = _capPos;
                ct.localRotation = _capRot;
                ct.localScale = _capScale;
            }

            // the table is clean again
            if (tablePuddle != null)
                tablePuddle.gameObject.SetActive(false);
            if (tableSplats != null)
                tableSplats.gameObject.SetActive(false);
            CanUpright = true;
        }

        /// <summary>Jar, can and cap back where Scenarios 6, 8 and 9 expect them; every accident prop hidden.</summary>
        public void ResetAll()
        {
            StopAllFx();
            Shown = false;
            CanUpright = false;
            if (!_captured)
                return;
            jar.gameObject.SetActive(true);
            jar.SetPositionAndRotation(_jarHome, _jarHomeRot);
            acid.SetPositionAndRotation(_acidHome, _acidHomeRot);
            if (acidCap != null)
            {
                var ct = acidCap.transform;
                ct.SetParent(_capParent, false);
                ct.localPosition = _capPos;
                ct.localRotation = _capRot;
                ct.localScale = _capScale;
            }

            if (tablePuddle != null)
                tablePuddle.gameObject.SetActive(false);
            if (tableSplats != null)
                tableSplats.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // the mount is switched off when Scenario 10 ends: leave nothing of the accident behind in the shared room
            if (Shown)
                ResetAll();
        }
    }
}
