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
        [Tooltip("Ribbed glove cuff at the wrist (blue).")]
        [SerializeField] private Material cuffMaterial;
        [Tooltip("Lab-coat sleeve that hides the end of the hand mesh.")]
        [SerializeField] private Material sleeveMaterial;
        [Tooltip("Optional glove surface skinned to the same skeleton; replaces the stock hand mesh.")]
        [SerializeField] private Mesh gloveMesh;

        private Transform _hand;
        private Transform _tipBone;
        private Vector3 _baseLocalPos;
        private Vector3 _push;
        private int _solidMask;
        private readonly Collider[] _hits = new Collider[8];
        private Transform[] _indexBones;
        private Quaternion[] _indexBase;
        private Coroutine _curl;
        private Transform[] _graspBones;
        private Quaternion[] _graspBase;
        private float[] _graspExtra;
        private float _grasp;
        private Transform _midProx;
        private Transform _midDist;
        private Transform _idxProx;
        private Transform _litProx;
        private float _palmSign = 1f;
        private Quaternion _baseLocalRot = Quaternion.identity;
        private float _cradle;
        private float _cradleTarget;
        private Coroutine _graspRoutine;

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
            _baseLocalRot = _hand.localRotation;
            _hand.localScale = Vector3.one * scale;

            foreach (var smr in _hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (gloveMesh != null)
                    smr.sharedMesh = gloveMesh;
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
            CaptureGrasp();
            CapturePalmFrame();
            BuildSleeve();
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
            _hand.localRotation = _baseLocalRot;
            _cradle = Mathf.MoveTowards(_cradle, _cradleTarget, Time.unscaledDeltaTime / 0.38f);
            if (_cradle > 0.001f && Palm != null && _midProx != null && _idxProx != null && _litProx != null)
                ApplyCradle(Mathf.SmoothStep(0f, 1f, _cradle));
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

        // Cuff and sleeve: the stock hand mesh stops at the wrist (hand local +Z points to the fingers), so a short
        // ribbed cuff and a lab-coat sleeve continue it backwards instead of leaving the hand floating.
        private void BuildSleeve()
        {
            if (_hand == null || (cuffMaterial == null && sleeveMaterial == null))
                return;
            void Tube(string name, float zCentre, float length, float radius, Material mat)
            {
                if (mat == null)
                    return;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(_hand, false);
                go.transform.localPosition = new Vector3(0f, -0.004f, zCentre);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            Tube("GloveCuff", -0.108f, 0.05f, 0.036f, cuffMaterial);
            Tube("GloveCuffRib", -0.135f, 0.012f, 0.039f, cuffMaterial);
            // with the glove mesh the cuff is part of the surface (to z -0.118), the sleeve starts right behind it
            Tube("LabSleeve", cuffMaterial != null ? -0.25f : -0.22f, 0.22f, 0.041f, sleeveMaterial);
        }

        // Extra finger bend (on top of the pose) that closes the fingers around a held container.
        private void CaptureGrasp()
        {
            var p = isRight ? "R_" : "L_";
            var names = new[]
            {
                "IndexProximal", "IndexIntermediate", "IndexDistal", "MiddleProximal", "MiddleIntermediate", "MiddleDistal",
                "RingProximal", "RingIntermediate", "RingDistal", "LittleProximal", "LittleIntermediate", "LittleDistal",
                "ThumbProximal", "ThumbDistal"
            };
            var extra = new[] { 30f, 36f, 20f, 36f, 40f, 24f, 38f, 42f, 24f, 40f, 42f, 24f, 14f, 22f };
            _graspBones = new Transform[names.Length];
            _graspBase = new Quaternion[names.Length];
            _graspExtra = extra;
            for (var i = 0; i < names.Length; i++)
            {
                _graspBones[i] = FindDeep(_hand, p + names[i]);
                _graspBase[i] = _graspBones[i] != null ? _graspBones[i].localRotation : Quaternion.identity;
            }
        }

        // The palm frame: which way the palm faces and where the fingers point, measured from the skeleton. The sign
        // of the normal is found once by closing the fingers (they curl towards the palm side).
        private void CapturePalmFrame()
        {
            var p = isRight ? "R_" : "L_";
            _midProx = FindDeep(_hand, p + "MiddleProximal");
            _midDist = FindDeep(_hand, p + "MiddleDistal");
            _idxProx = FindDeep(_hand, p + "IndexProximal");
            _litProx = FindDeep(_hand, p + "LittleProximal");
            _palmSign = 1f;
            if (Palm == null || _midProx == null || _midDist == null || _idxProx == null || _litProx == null ||
                _graspBones == null)
                return;
            var n = RawPalmNormal();
            var open = _midDist.position;
            for (var i = 0; i < _graspBones.Length; i++)
            {
                if (_graspBones[i] != null)
                    _graspBones[i].localRotation = _graspBase[i] * Quaternion.Euler(_graspExtra[i], 0f, 0f);
            }

            var closed = _midDist.position;
            for (var i = 0; i < _graspBones.Length; i++)
            {
                if (_graspBones[i] != null)
                    _graspBones[i].localRotation = _graspBase[i];
            }

            if (Vector3.Dot(closed - open, n) < 0f)
                _palmSign = -1f;
        }

        /// <summary>
        /// Holding pose for the left hand: the glove (visual only, tracking is untouched) turns palm up under the
        /// container with the fingers pointing the way the user looks, and the fingers curl round it from below.
        /// </summary>
        public void SetCradle(bool on) => _cradleTarget = on ? 1f : 0f;

        public float CradleAmount => _cradle;

        private void ApplyCradle(float k)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up) : Vector3.forward;
            fwd = fwd.sqrMagnitude < 0.001f ? Vector3.forward : fwd.normalized;
            var palmPos = Palm.position;
            var f = (_midProx.position - palmPos).normalized;
            var n = RawPalmNormal() * _palmSign;
            var cur = Quaternion.LookRotation(f, n);
            var tgt = Quaternion.LookRotation(fwd, Vector3.up);
            var delta = tgt * Quaternion.Inverse(cur);
            _hand.rotation = Quaternion.Slerp(Quaternion.identity, delta, k) * _hand.rotation;
            _hand.position += palmPos - Palm.position;
        }

        private Vector3 RawPalmNormal()
        {
            var f = (_midProx.position - Palm.position).normalized;
            var a = (_idxProx.position - _litProx.position).normalized;
            return Vector3.Cross(f, a).normalized;
        }

        /// <summary>Palm centre, the direction the palm faces (where a held object sits) and the finger direction.</summary>
        public bool TryGetPalmFrame(out Vector3 center, out Vector3 normal, out Vector3 fingers)
        {
            center = Vector3.zero;
            normal = Vector3.up;
            fingers = Vector3.forward;
            if (Palm == null || _midProx == null || _idxProx == null || _litProx == null)
                return false;
            fingers = (_midProx.position - Palm.position).normalized;
            normal = RawPalmNormal() * _palmSign;
            center = Palm.position + fingers * 0.015f;
            return true;
        }

        /// <summary>0 = relaxed pose, 1 = fingers closed around a held container.</summary>
        public void SetGrasp(float target)
        {
            if (_graspBones == null)
                return;
            if (_graspRoutine != null)
                StopCoroutine(_graspRoutine);
            _graspRoutine = StartCoroutine(GraspRoutine(target));
        }

        private IEnumerator GraspRoutine(float target)
        {
            var from = _grasp;
            yield return PresentableFx.Tween(0.3f, t =>
            {
                _grasp = Mathf.Lerp(from, target, t);
                for (var i = 0; i < _graspBones.Length; i++)
                {
                    if (_graspBones[i] != null)
                        _graspBones[i].localRotation = _graspBase[i] * Quaternion.Euler(_graspExtra[i] * _grasp, 0f, 0f);
                }
            }, PresentableFx.EaseOutCubic);
            _graspRoutine = null;
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
