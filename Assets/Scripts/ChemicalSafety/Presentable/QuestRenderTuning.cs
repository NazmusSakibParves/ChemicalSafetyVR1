using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Headset render tuning: moderate fixed foveation (sharp centre, cheaper periphery) so the full-resolution,
    /// MSAA-enabled Presentable render pipeline asset keeps a steady frame rate on Quest.
    /// </summary>
    public sealed class QuestRenderTuning : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float foveation = 0.5f;

        private void Start()
        {
            if (!XRSettings.isDeviceActive)
                return;
            try
            {
                var displays = new List<XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(displays);
                foreach (var d in displays)
                {
                    if (d.running)
                        d.foveatedRenderingLevel = foveation;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Presentable] Foveation not available: " + e.Message);
            }
        }
    }
}
