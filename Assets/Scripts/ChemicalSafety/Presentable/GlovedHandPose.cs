using System.Collections;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Spawns a gloved hand mesh on a controller and bends its bones into a static pose
    /// (pointing for the answering hand, holding for the clipboard hand).
    /// The index fingertip is aligned to the controller's poke point so poking UI feels exact.
    /// </summary>
    public sealed class GlovedHandPose : MonoBehaviour
    {
        public enum Pose
        {
            Point,
            Hold
        }

        [SerializeField] private GameObject handModel;
        [SerializeField] private Material gloveMaterial;
        [SerializeField] private bool isRight = true;
        [SerializeField] private Pose pose = Pose.Point;
        [SerializeField] private Transform pokePoint;
        [SerializeField] private GameObject controllerVisualToHide;
        [SerializeField] private Vector3 localEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private float scale = 1.06f;

        private Transform _hand;
        private Transform _tipBone;
        private Vector3 _baseLocalPos;
        private Vector3 _push;
        private int _solidMask;
        private readonly Collider[] _hits = new Collider[8];
        private Transform[] _indexBones;
        private Quaternion[] _indexBase;
        private Coroutine _curl;

        /// <summary>The gloved right hand (null until it has spawned).</summary>
        public static GlovedHandPose Right { get; private set; }
        public static GlovedHandPose Left { get; private set; }

        /// <summary>Palm bone of the spawned glove (null until Start has run).</summary>
        public Transform Palm { get; private set; }

        public Transform PokePoint => pokePoint;

        /// <summary>World position of the (collision-clamped) index fingertip - the point everything is poked with.</summary>
        public Vector3 TipPosition => _tipBone != null ? _tipBone.position : pokePoint != null ? pokePoint.position : transform.position;

        /// <summary>
        /// Fingertip for UI presses: the controller's poke point plus the collision push. Unlike
        /// <see cref="TipPosition"/> it ignores the poke-curl animation, so a press cannot re-arm itself.
        /// </summary>
        public Vector3 PressPoint => (pokePoint != null ? pokePoint.position : TipPosition) + _push;

        public bool HasTip => _tipBone != null || pokePoint != null;

        private void Start()
        {
            if (handModel == null)
                return;

            if (controllerVisualToHide != null)
                controllerVisualToHide.SetActive(false);

            _hand = Instantiate(handModel, transform, false).transform;
            _hand.name = isRight ? "GlovedHand_R" : "GlovedHand_L";
            _hand.localRotation = Quaternion.Euler(localEuler);
            _hand.localScale = Vector3.one * scale;

            foreach (var smr in _hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (gloveMaterial != null)
                    smr.sharedMaterial = gloveMaterial;
                smr.updateWhenOffscreen = true;
            }

            ApplyPose();
            AlignToPokePoint();
            _baseLocalPos = _hand.localPosition;
            _tipBone = FindDeep(_hand, (isRight ? "R_" : "L_") + "IndexTip");
            _solidMask = LayerMask.GetMask("SolidProps");
            CaptureIndex();
            Palm = FindDeep(_hand, (isRight ? "R_" : "L_") + "Palm");
            if (isRight)
                Right = this;
            else
                Left = this;
        }

        // Physical-hand trick: tracking is never touched, but the rendered glove is pushed out of solid props
        // (drums, racks, carrier) so the fingertip stops at the surface instead of sliding through.
        private void LateUpdate()
        {
            if (_hand == null)
                return;

            _hand.localPosition = _baseLocalPos;
            var target = _solidMask != 0 ? SolvePush() : Vector3.zero;
            _push = Vector3.Lerp(_push, target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            _hand.position += _push;
        }

        private Vector3 SolvePush()
        {
            var total = Vector3.zero;
            var tip = _tipBone != null ? _tipBone.position : _hand.position;
            total += SphereExit(tip, 0.014f);
            // the fingertip rests on UI panels instead of sinking into them (rest-pose tip, so the curl animation
            // cannot pull the hand out of the panel stop)
            total += FingerPokeSystem.PanelPush(pokePoint != null ? pokePoint.position : tip, 0.012f);
            if (Palm != null)
                total += SphereExit(Palm.position, 0.04f);
            return Vector3.ClampMagnitude(total, 0.3f);
        }

        private Vector3 SphereExit(Vector3 pos, float radius)
        {
            var best = Vector3.zero;
            var n = Physics.OverlapSphereNonAlloc(pos, radius, _hits, _solidMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                var c = _hits[i];
                var closest = c.ClosestPoint(pos);
                Vector3 push;
                var d = (pos - closest);
                if (d.sqrMagnitude > 1e-8f)
                {
                    push = d.normalized * (radius - d.magnitude);
                }
                else
                {
                    // centre is inside the collider: leave sideways through the nearest side
                    var b = c.bounds;
                    var dir = pos - b.center;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 1e-6f)
                        dir = -Vector3.forward;
                    push = dir.normalized * (Mathf.Max(b.extents.x, b.extents.z) - dir.magnitude + radius);
                }

                if (push.sqrMagnitude > best.sqrMagnitude)
                    best = push;
            }

            return best;
        }

        private void OnDestroy()
        {
            if (Right == this)
                Right = null;
            if (Left == this)
                Left = null;
        }

        private void CaptureIndex()
        {
            var p = isRight ? "R_" : "L_";
            _indexBones = new[]
            {
                FindDeep(_hand, p + "IndexProximal"), FindDeep(_hand, p + "IndexIntermediate"),
                FindDeep(_hand, p + "IndexDistal")
            };
            _indexBase = new Quaternion[_indexBones.Length];
            for (var i = 0; i < _indexBones.Length; i++)
                _indexBase[i] = _indexBones[i] != null ? _indexBones[i].localRotation : Quaternion.identity;
        }

        /// <summary>Briefly curls the index finger (poke feedback), then straightens it again.</summary>
        public void PlayPokeCurl()
        {
            if (_indexBones == null)
                return;
            if (_curl != null)
                StopCoroutine(_curl);
            _curl = StartCoroutine(CurlRoutine());
        }

        private IEnumerator CurlRoutine()
        {
            var extra = new[] { 55f, 65f, 45f };
            yield return PresentableFx.Tween(0.09f, t => SetIndexCurl(t, extra));
            yield return PresentableFx.Wait(0.05f);
            yield return PresentableFx.Tween(0.18f, t => SetIndexCurl(1f - t, extra));
            _curl = null;
        }

        private void SetIndexCurl(float k, float[] extra)
        {
            for (var i = 0; i < _indexBones.Length; i++)
            {
                if (_indexBones[i] != null)
                    _indexBones[i].localRotation = _indexBase[i] * Quaternion.Euler(extra[i] * k, 0f, 0f);
            }
        }

        private void ApplyPose()
        {
            var p = isRight ? "R_" : "L_";
            if (pose == Pose.Point)
            {
                Curl(p + "Index", 4f, 6f, 4f);
                Curl(p + "Middle", 72f, 88f, 55f);
                Curl(p + "Ring", 78f, 90f, 55f);
                Curl(p + "Little", 82f, 90f, 55f);
                Thumb(p, 18f, 30f);
            }
            else
            {
                Curl(p + "Index", 38f, 45f, 25f);
                Curl(p + "Middle", 45f, 50f, 30f);
                Curl(p + "Ring", 50f, 52f, 30f);
                Curl(p + "Little", 55f, 55f, 30f);
                Thumb(p, 10f, 15f);
            }
        }

        private void Curl(string finger, float proximal, float intermediate, float distal)
        {
            Bend(finger + "Proximal", proximal);
            Bend(finger + "Intermediate", intermediate);
            Bend(finger + "Distal", distal);
        }

        private void Thumb(string prefix, float proximal, float distal)
        {
            Bend(prefix + "ThumbProximal", proximal);
            Bend(prefix + "ThumbDistal", distal);
        }

        private void Bend(string bone, float degrees)
        {
            var t = FindDeep(_hand, bone);
            if (t != null)
                t.localRotation *= Quaternion.Euler(degrees, 0f, 0f);
        }

        private void AlignToPokePoint()
        {
            if (pose != Pose.Point || pokePoint == null)
                return;

            var tip = FindDeep(_hand, (isRight ? "R_" : "L_") + "IndexTip");
            if (tip == null)
                return;

            _hand.position += pokePoint.position - tip.position;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }
    }
}
