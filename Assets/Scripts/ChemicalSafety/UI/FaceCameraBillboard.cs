using UnityEngine;

namespace ChemicalSafety.UI
{
    /// <summary>Keeps a world UI panel upright and facing the main/XR camera.</summary>
    public sealed class FaceCameraBillboard : MonoBehaviour
    {
        [SerializeField] private bool flattenPitch = true;

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var dir = transform.position - cam.transform.position;
            if (flattenPitch)
                dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
