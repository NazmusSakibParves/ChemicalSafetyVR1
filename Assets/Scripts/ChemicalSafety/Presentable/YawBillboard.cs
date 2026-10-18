using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>Turns a world-space card to face the user around the vertical axis only.</summary>
    public sealed class YawBillboard : MonoBehaviour
    {
        private Transform _cam;

        private void LateUpdate()
        {
            if (_cam == null)
            {
                _cam = Camera.main != null ? Camera.main.transform : null;
                if (_cam == null)
                    return;
            }

            var d = transform.position - _cam.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
