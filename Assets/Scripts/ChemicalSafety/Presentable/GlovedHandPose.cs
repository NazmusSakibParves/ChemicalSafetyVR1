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
