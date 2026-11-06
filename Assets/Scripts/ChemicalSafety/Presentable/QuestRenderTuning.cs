using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Headset render tuning: moderate fixed foveation (sharp centre, cheaper periphery) so the full-resolution,
    /// MSAA-enabled Presentable render pipeline asset keeps a steady frame rate on Quest.
    /// </summary>
    public sealed class QuestRenderTuning : MonoBehaviour
    {
        /// <summary>
        /// Headset only. Whatever pipeline asset the build was made with (Mobile_RPAsset has MSAA off and render scale 0.8,
        /// which shows as shimmering rack and drum edges), the headset runs the same sharp settings as Presentable_RPAsset:
        /// MSAA 4x, render scale 1.0, SDR (no HDR buffer, so MSAA stays affordable). Runtime only, the asset files are not touched.
        /// </summary>
        private static void SharpPipeline()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
                return;
            if (urp.msaaSampleCount < 4)
                urp.msaaSampleCount = 4;
            urp.renderScale = 1f;
            if (urp.supportsHDR)
                urp.supportsHDR = false;
            Debug.Log("[Presentable] Headset render: MSAA " + urp.msaaSampleCount + "x, render scale " + urp.renderScale + ", HDR " + urp.supportsHDR);
        }

        [Range(0f, 1f)] [SerializeField] private float foveation = 0.5f;

        private void Start()
        {
            if (!XRSettings.isDeviceActive)
                return;
            SharpPipeline();
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
