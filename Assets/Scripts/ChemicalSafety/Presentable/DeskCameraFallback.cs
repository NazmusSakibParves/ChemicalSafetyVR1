using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Editor / desk testing without a headset: eye height + right-mouse look.
    /// Disabled automatically when an XR device is active.
    /// </summary>
    public sealed class DeskCameraFallback : MonoBehaviour
    {
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private float lookSpeed = 0.15f;

        private float _yaw;
        private float _pitch;
        private bool _active;

        private void Start()
        {
            _active = !XRSettings.isDeviceActive;
            if (!_active)
            {
                enabled = false;
                return;
            }

            var p = transform.localPosition;
            transform.localPosition = new Vector3(p.x, eyeHeight, p.z);
            var e = transform.localEulerAngles;
            _yaw = e.y;
            _pitch = 8f;
            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void LateUpdate()
        {
            var parent = transform.parent;
            if (parent != null)
            {
                var lp = transform.localPosition;
                lp.y = eyeHeight - parent.localPosition.y;
                transform.localPosition = lp;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed)
                return;

            var d = mouse.delta.ReadValue() * lookSpeed;
            _yaw += d.x;
            _pitch = Mathf.Clamp(_pitch - d.y, -60f, 70f);
            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
