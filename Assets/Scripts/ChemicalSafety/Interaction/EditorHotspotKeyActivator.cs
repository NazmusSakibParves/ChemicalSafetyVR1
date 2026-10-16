using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ChemicalSafety.Interaction
{
    /// <summary>
    /// Press E in Play Mode to activate a hotspot without XR hardware (demo / desk testing).
    /// </summary>
    public sealed class EditorHotspotKeyActivator : MonoBehaviour
    {
        [SerializeField] private HotspotInteractable hotspot;

        public void Configure(HotspotInteractable target) => hotspot = target;

        private void Update()
        {
            if (hotspot == null)
                return;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                hotspot.TryActivate();
#else
            if (Input.GetKeyDown(KeyCode.E))
                hotspot.TryActivate();
#endif
        }
    }
}
