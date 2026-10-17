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
        private Transform[] _indexBones;
        private Quaternion[] _indexBase;
        private Coroutine _curl;

        /// <summary>The gloved right hand (null until it has spawned).</summary>
        public static GlovedHandPose Right { get; private set; }
        public static GlovedHandPose Left { get; private set; }

        /// <summary>Palm bone of the spawned glove (null until Start has run).</summary>
        public Transform Palm { get; private set; }

        public Transform PokePoint => pokePoint;

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
            CaptureIndex();
            Palm = FindDeep(_hand, (isRight ? "R_" : "L_") + "Palm");
            if (isRight)
                Right = this;
            else
                Left = this;
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
