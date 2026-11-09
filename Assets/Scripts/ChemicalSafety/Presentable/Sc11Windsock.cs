using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>Gentle sway of the yard wind sock (yaw and droop), purely visual.</summary>
    public sealed class Sc11Windsock : MonoBehaviour
    {
        private Quaternion _rest;

        private void Awake() => _rest = transform.localRotation;

        private void Update()
        {
            var t = Time.time;
            var yaw = Mathf.Sin(t * 0.7f) * 7f + Mathf.Sin(t * 1.9f) * 2.5f;
            var droop = -12f + Mathf.Sin(t * 1.1f + 1f) * 5f;
            transform.localRotation = _rest * Quaternion.Euler(0f, yaw, droop);
        }
    }
}
