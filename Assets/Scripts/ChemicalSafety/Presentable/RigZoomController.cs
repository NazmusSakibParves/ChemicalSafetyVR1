using UnityEngine;
using UnityEngine.InputSystem;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// "Zoom" for VR: holding A moves the rig toward the focus point, B moves it back (clamped).
    /// Desk: W/S, arrow keys or mouse wheel.
    /// </summary>
    public sealed class RigZoomController : MonoBehaviour
    {
        [SerializeField] private Transform rig;
        [SerializeField] private Transform focus;
        [SerializeField] private float minDistance = 1.4f;
        [SerializeField] private float maxDistance = 5.5f;
        [SerializeField] private float speed = 1.1f;

        private InputAction _zoomIn;
        private InputAction _zoomOut;
        private float _hapticCooldown;

        public bool ZoomEnabled { get; set; }

        private void OnEnable()
        {
            _zoomIn = new InputAction("ZoomIn", InputActionType.Button);
            _zoomIn.AddBinding("<XRController>{RightHand}/primaryButton");
            _zoomIn.AddBinding("<Keyboard>/w");
            _zoomIn.AddBinding("<Keyboard>/upArrow");
            _zoomIn.Enable();

            _zoomOut = new InputAction("ZoomOut", InputActionType.Button);
            _zoomOut.AddBinding("<XRController>{RightHand}/secondaryButton");
            _zoomOut.AddBinding("<Keyboard>/s");
            _zoomOut.AddBinding("<Keyboard>/downArrow");
            _zoomOut.Enable();
        }

        private void OnDisable()
        {
            _zoomIn?.Dispose();
            _zoomOut?.Dispose();
        }

        public void Configure(Transform rigRoot, Transform focusPoint)
        {
            rig = rigRoot;
            focus = focusPoint;
        }

        private void Update()
        {
            if (!ZoomEnabled || rig == null || focus == null)
                return;

            var dir = 0f;
            if (_zoomIn.IsPressed())
                dir += 1f;
            if (_zoomOut.IsPressed())
                dir -= 1f;

            var mouse = Mouse.current;
            if (mouse != null)
                dir += Mathf.Clamp(mouse.scroll.ReadValue().y, -1f, 1f) * 6f;

            if (Mathf.Approximately(dir, 0f))
                return;

            var toFocus = focus.position - rig.position;
            toFocus.y = 0f;
            var dist = toFocus.magnitude;
            if (dist < 0.001f)
                return;

            var axis = toFocus / dist;
            var newDist = Mathf.Clamp(dist - dir * speed * Time.deltaTime, minDistance, maxDistance);
            if (Mathf.Approximately(newDist, dist))
                return;

            rig.position += axis * (dist - newDist);
            // The XR Origin's CharacterController would otherwise snap back to its cached position.
            Physics.SyncTransforms();

            _hapticCooldown -= Time.deltaTime;
            if (_hapticCooldown <= 0f)
            {
                PresentableFx.Haptic(true, 0.06f, 0.02f);
                _hapticCooldown = 0.12f;
            }
        }
    }
}
