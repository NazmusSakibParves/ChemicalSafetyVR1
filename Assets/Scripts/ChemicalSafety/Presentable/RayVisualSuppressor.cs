using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Finger-only mode: hides the Near-Far ray lines and switches off UI interaction on the Near-Far and
    /// Poke interactors, so every button is pressed by the gloved fingertip (FingerPokeSystem) and never by a ray.
    /// Teleport interactor visuals are left alone. Scans the rig a few times (no per-frame allocation).
    /// </summary>
    public sealed class RayVisualSuppressor : MonoBehaviour
    {
        private readonly List<XRInteractorLineVisual> _visuals = new List<XRInteractorLineVisual>(4);
        private readonly List<CurveVisualController> _curves = new List<CurveVisualController>(4);
        private readonly List<NearFarInteractor> _nearFar = new List<NearFarInteractor>(4);
        private readonly List<XRPokeInteractor> _pokes = new List<XRPokeInteractor>(4);
        private float _next;
        private int _scans;

        private void Update()
        {
            // The controllers are (re)activated when tracking starts, so re-scan on a slow timer.
            if (Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + (_scans++ < 6 ? 0.5f : 3f);

            var root = Camera.main != null ? Camera.main.transform.root : null;
            if (root == null)
                return;

            root.GetComponentsInChildren(true, _visuals);
            foreach (var v in _visuals)
            {
                if (!IsNearFar(v.transform))
                    continue;
                if (v.enabled)
                    v.enabled = false;
                var lr = v.GetComponent<LineRenderer>();
                if (lr != null && lr.enabled)
                    lr.enabled = false;
            }

            // XRI 3.x Near-Far draws its ray with CurveVisualController, which re-enables the LineRenderer every frame.
            root.GetComponentsInChildren(true, _curves);
            foreach (var c in _curves)
            {
                if (!IsNearFar(c.transform))
                    continue;
                if (c.enabled)
                    c.enabled = false;
                var lr = c.GetComponent<LineRenderer>();
                if (lr != null && lr.enabled)
                    lr.enabled = false;
            }

            root.GetComponentsInChildren(true, _nearFar);
            foreach (var n in _nearFar)
            {
                if (n.enableUIInteraction)
                    n.enableUIInteraction = false;
            }

            root.GetComponentsInChildren(true, _pokes);
            foreach (var p in _pokes)
            {
                if (p.enableUIInteraction)
                    p.enableUIInteraction = false;
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
