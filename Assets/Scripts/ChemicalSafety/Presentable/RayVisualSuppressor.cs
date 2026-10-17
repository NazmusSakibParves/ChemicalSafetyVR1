using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Hides the Near-Far interactor ray lines (and their reticles) so everything in the demo is poked
    /// with the gloved finger instead of pointed at. Teleport interactor visuals are left alone.
    /// </summary>
    public sealed class RayVisualSuppressor : MonoBehaviour
    {
        private float _next;

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.4f;
            foreach (var v in FindObjectsByType<XRInteractorLineVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsNearFar(v.transform))
                    continue;
                if (v.enabled)
                    v.enabled = false;
                var lr = v.GetComponent<LineRenderer>();
                if (lr != null && lr.enabled)
                    lr.enabled = false;
            }
        }

        private static bool IsNearFar(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.Contains("NearFar") || t.name.Contains("Near-Far"))
                    return true;
            }

            return false;
        }
    }
}
