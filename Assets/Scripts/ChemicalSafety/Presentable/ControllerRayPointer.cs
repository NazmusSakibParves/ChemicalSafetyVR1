using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Controller ray for the scenarios listed in <see cref="PresentableInput"/> (experiment, Scenario 1 for now).
    /// The right controller points, the trigger presses. It fires the same Button.onClick, HoverLift hover/press, tick
    /// and haptics as the fingertip (<see cref="FingerPokeSystem"/>), so scenario scripts do not know which input clicked.
    /// Outside the allowed scenarios the ray line is hidden and nothing here runs. Additive: finger touch is untouched.
    /// </summary>
    public sealed class ControllerRayPointer : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private float lineWidth = 0.0045f;

        private readonly List<Canvas> _canvases = new List<Canvas>(8);
        private readonly List<Button> _buttons = new List<Button>(16);
        private Transform _controller;
        private LineRenderer _line;
        private Transform _dot;
        private Button _hovered;
        private bool _wasDown;
        private float _nextScan;
        private PokeTarget[] _pokes = new PokeTarget[0];

        private void Awake()
        {
            var go = new GameObject("RayLine");
            go.transform.SetParent(transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.widthMultiplier = lineWidth;
            _line.numCapVertices = 4;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null)
                _line.material = new Material(sh);
            _line.startColor = new Color(0.45f, 0.95f, 1f, 0.9f);
            _line.endColor = new Color(0.45f, 0.95f, 1f, 0.25f);
            _line.enabled = false;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "RayDot";
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * 0.02f;
            var r = dot.GetComponent<Renderer>();
            if (sh != null)
                r.material = new Material(sh) { color = new Color(0.6f, 1f, 1f, 1f) };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _dot = dot.transform;
            _dot.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            SetHover(null);
            if (_line != null)
                _line.enabled = false;
            if (_dot != null)
                _dot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!PresentableInput.RayActive)
            {
                if (_hovered != null || _line.enabled)
                    OnDisable();
                return;
            }

            if (_controller == null)
                _controller = FindController();
            if (_controller == null || !_controller.gameObject.activeInHierarchy)
                return;

            var origin = _controller.position;
            var dir = _controller.forward;

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.4f;
                _canvases.Clear();
                FingerPokeSystem.CollectCanvases(_canvases);
                _pokes = FindObjectsByType<PokeTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            Button best = null;
            var bestT = maxDistance;
            Vector3 bestPoint = default;
            foreach (var canvas in _canvases)
            {
                if (canvas == null || !canvas.gameObject.activeInHierarchy)
                    continue;
                var group = canvas.GetComponent<CanvasGroup>();
                if (group != null && group.alpha < 0.6f)
                    continue;
                var rt = (RectTransform)canvas.transform;
                var denom = Vector3.Dot(dir, rt.forward);
                if (denom <= 0.0001f) // the panel faces -z: the ray must travel along +z into it
                    continue;
                var t = Vector3.Dot(rt.position - origin, rt.forward) / denom;
                if (t <= 0.02f || t >= bestT)
                    continue;
                var point = origin + dir * t;
                canvas.GetComponentsInChildren(false, _buttons);
                foreach (var b in _buttons)
                {
                    if (b == null || !b.IsInteractable() || !b.gameObject.activeInHierarchy)
                        continue;
                    var brt = (RectTransform)b.transform;
                    var local = brt.InverseTransformPoint(point);
                    var rect = brt.rect;
                    rect.xMin -= 10f;
                    rect.xMax += 10f;
                    rect.yMin -= 10f;
                    rect.yMax += 10f;
                    if (!rect.Contains(new Vector2(local.x, local.y)))
                        continue;
                    best = b;
                    bestT = t;
                    bestPoint = point;
                }
            }

            // 3D props (PokeTarget: SC02 containers) are pressed by the ray too; the nearest hit wins
            PokeTarget bestPoke = null;
            var origin2 = new Ray(origin, dir);
            foreach (var pt in _pokes)
            {
                if (pt == null || !pt.Interactable || !pt.gameObject.activeInHierarchy || pt.Colliders == null)
                    continue;
                foreach (var col in pt.Colliders)
                {
                    if (col == null || !col.enabled || !col.Raycast(origin2, out var hit, bestT))
                        continue;
                    if (hit.distance < bestT)
                    {
                        bestT = hit.distance;
                        bestPoke = pt;
                        bestPoint = hit.point;
                        best = null;
                    }
                }
            }

            if (best != _hovered)
            {
                SetHover(best);
                if (best != null)
                    PresentableFx.Haptic(true, 0.15f, 0.03f);
            }

            var hitSomething = best != null || bestPoke != null;
            var end = hitSomething ? bestPoint : origin + dir * 3f;
            _line.enabled = true;
            _line.SetPosition(0, origin + dir * 0.03f);
            _line.SetPosition(1, end);
            _dot.gameObject.SetActive(hitSomething);
            if (hitSomething)
                _dot.position = bestPoint;

            var down = TriggerDown();
            if (down && !_wasDown && bestPoke != null)
                bestPoke.RayFire();
            else if (down && !_wasDown && best != null)
            {
                PresentableFx.Haptic(true, 0.5f, 0.07f);
                PresentableFx.Play(PresentableFx.Tick, 0.8f);
                var lift = best.GetComponent<HoverLift>();
                if (lift != null)
                    lift.Press();
                best.onClick.Invoke();
            }

            _wasDown = down;
        }

        private void SetHover(Button b)
        {
            if (_hovered != null)
            {
                var old = _hovered.GetComponent<HoverLift>();
                if (old != null)
                    old.SetHover(false);
            }

            _hovered = b;
            if (b != null)
            {
                var lift = b.GetComponent<HoverLift>();
                if (lift != null)
                    lift.SetHover(true);
            }
        }

        private static bool TriggerDown()
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out var pressed))
                return pressed;
            return false;
        }

        private static Transform FindController()
        {
            var cam = Camera.main;
            if (cam == null)
                return null;
            return FindDeep(cam.transform.root, "Right Controller");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f != null)
                    return f;
            }

            return null;
        }
    }
}
