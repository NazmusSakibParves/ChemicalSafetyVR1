using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ChemicalSafety.Interaction
{
    /// <summary>
    /// Desk testing without headset: E = SC01 hotspot, Alpha2 = SC02 hotspot.
    /// </summary>
    public sealed class EditorHotspotDualKeyActivator : MonoBehaviour
    {
        [SerializeField] private HotspotInteractable hotspotSc01;
        [SerializeField] private HotspotInteractable hotspotSc02;

        public void Configure(HotspotInteractable sc01, HotspotInteractable sc02)
        {
            hotspotSc01 = sc01;
            hotspotSc02 = sc02;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.eKey.wasPressedThisFrame && hotspotSc01 != null)
                hotspotSc01.TryActivate();

            if (Keyboard.current.digit2Key.wasPressedThisFrame && hotspotSc02 != null)
                hotspotSc02.TryActivate();
#else
            if (Input.GetKeyDown(KeyCode.E) && hotspotSc01 != null)
                hotspotSc01.TryActivate();

            if (Input.GetKeyDown(KeyCode.Alpha2) && hotspotSc02 != null)
                hotspotSc02.TryActivate();
#endif
        }
    }
}
