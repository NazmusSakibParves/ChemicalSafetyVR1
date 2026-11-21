using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Lets the learner drag a popup (instruction card, MCQ, gate card, result card) with the controller ray: aim at the grip bar on
    /// the top edge, hold the trigger and move. The panel keeps its distance from the ray origin and always faces the learner.
    /// After a drag the chosen pose is re-applied every LateUpdate, so the scenario scripts that re-place the card every frame
    /// do not undo it. The choice is forgotten when the card is hidden or the scenario changes (the next card opens at its default
    /// eye-level pose again). Additive: nothing changes until the learner grabs a bar. Not attached to the anchored popups
    /// (Scenario 2 "Correct Placement", Scenario 12 verdict).
    /// </summary>
    public sealed class DraggablePanel : MonoBehaviour
    {
        public static readonly List<DraggablePanel> All = new List<DraggablePanel>(24);

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _handle;
        private Image _handleImage;
        private bool _moved;
        private Vector3 _pos;
        private Quaternion _rot;
        private int _scenario;
        private Vector3 _grabOffset;
        private float _grabDist;

        public bool Dragging { get; private set; }

        /// <summary>The director keeps this panel below the top bar itself (Scenario 1 question box): the push-out would lift it into the bar again.</summary>
        public bool SkipPushOut;

        public static DraggablePanel Attach(Component host)
        {
            if (host == null)
                return null;
            var d = host.GetComponent<DraggablePanel>();
            return d != null ? d : host.gameObject.AddComponent<DraggablePanel>();
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable()
        {
            All.Remove(this);
            _moved = false;
            _mover = null;
            Dragging = false;
        }

        private Canvas FindCanvas()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
                if (_canvas == null)
                    _canvas = GetComponentInChildren<Canvas>(true);
            }

            return _canvas;
        }

        private bool Shown()
        {
            var c = FindCanvas();
            if (c == null || !c.gameObject.activeInHierarchy)
                return false;
            if (_group == null)
                _group = c.GetComponentInParent<CanvasGroup>();
            return _group == null || _group.alpha > 0.05f;
        }

        private void EnsureHandle()
        {
            return; // client: no grip bar - the whole card is the handle (HitBody)
#pragma warning disable CS0162
            if (_handle != null)
                return;
            var c = FindCanvas();
            if (c == null)
                return;
            var rt = PresentableUi.Rect("DragGrip", c.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(300f, 34f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            _handleImage = PresentableUi.Img(rt, null, new Color(0.35f, 0.85f, 1f, 0.55f));
            // two thin lines = "grip" look
            for (var i = 0; i < 2; i++)
            {
                var line = PresentableUi.Centered("GripLine" + i, rt, new Vector2(0f, i == 0 ? 6f : -6f), new Vector2(120f, 4f));
                PresentableUi.Img(line, null, new Color(0.02f, 0.1f, 0.18f, 0.9f));
            }

            _handle = rt;
#pragma warning restore CS0162
        }

        private void Update()
        {
            if (_moved && (!Shown() || _scenario != PresentableInput.ActiveScenario))
            {
                _moved = false;
                _mover = null;
            }
            if (!PresentableInput.RayActive)
                return;
            if (Shown())
                EnsureHandle();
            if (_handle != null)
                _handle.gameObject.SetActive(Shown());
        }

        /// <summary>
        /// Head-locked panels (the top control bar) place themselves from a head-relative offset every frame. When this is set the drag only
        /// reports the new offset (right, up, forward of the head) and never moves the transform or applies the push-out itself.
        /// </summary>
        public System.Action<Vector3> OnHeadOffset;

        private void LateUpdate()
        {
            if (OnHeadOffset != null)
                return;
            if (_moved)
            {
                (_mover != null ? _mover : transform).SetPositionAndRotation(_pos, _rot);
                return;
            }

            if (ClearOf3D && !SkipPushOut && !Dragging && PresentableInput.ActiveScenario > 0 && Shown())
                PushOutOfProps();
        }

        // ------------------------------------------------------------------ keep popups out of 3D objects (client)

        /// <summary>Master switch of the "popup never inside a 3D object" push-out (false = popups stay exactly where the scenario puts them).</summary>
        public static bool ClearOf3D = true;

        private static readonly Collider[] Hits = new Collider[24];
        private const float MinHeadDistance = 0.8f;
        private const float StepMetres = 0.1f;
        private const int MaxSteps = 14;

        /// <summary>
        /// If the card box overlaps a visible, solid 3D object (cylinder, table, can, machine, wall) it is pushed toward the learner
        /// in 10 cm steps, and lifted when it is already close, until it is clear. Colliders of the rig / hands, triggers, other UI and
        /// huge ground planes are ignored. A card the learner has dragged is never touched.
        /// </summary>
        private void PushOutOfProps()
        {
            var c = FindCanvas();
            var cam = Camera.main;
            if (c == null || cam == null)
                return;
            var rt = (RectTransform)c.transform;
            var m = Mover; // the card itself (for a gate card the host is far away: the push direction must come from the card, not from the host)
            for (var i = 0; i < MaxSteps; i++)
            {
                if (!OverlapsProp(rt, cam.transform))
                    return;
                var head = cam.transform.position;
                var toHead = new Vector3(head.x - m.position.x, 0f, head.z - m.position.z);
                if (toHead.magnitude > MinHeadDistance + StepMetres)
                    m.position += toHead.normalized * StepMetres;
                else
                    m.position += Vector3.up * StepMetres;
            }
        }

        private static bool OverlapsProp(RectTransform rt, Transform cam)
        {
            var s = rt.lossyScale;
            var half = new Vector3(rt.rect.width * s.x * 0.5f, rt.rect.height * s.y * 0.5f, 0.03f);
            if (half.x < 0.02f || half.y < 0.02f)
                return false;
            var centre = rt.TransformPoint(rt.rect.center);
            var n = Physics.OverlapBoxNonAlloc(centre, half, Hits, rt.rotation, ~0, QueryTriggerInteraction.Ignore);
            var root = cam.root;
            for (var i = 0; i < n; i++)
            {
                var col = Hits[i];
                if (col == null || !col.enabled || col.isTrigger)
                    continue;
                var t = col.transform;
                if (t.IsChildOf(root) || t.GetComponentInParent<Canvas>() != null || t.GetComponentInParent<DraggablePanel>() != null)
                    continue; // rig / hands, other UI, the card's own objects
                var b = col.bounds;
                if (b.size.x > 12f && b.size.z > 12f) // ground / meadow plane
                    continue;
                if (col.GetComponentInParent<Renderer>() == null && col.GetComponentInChildren<Renderer>() == null) // invisible helper collider
                    continue;
                return true;
            }

            return false;
        }

        /// <summary>Ray against the grip bar. t = distance along the ray.</summary>
        public bool HitHandle(Vector3 origin, Vector3 dir, float maxT, out float t, out Vector3 point)
        {
            t = 0f;
            point = default;
            if (_handle == null || !_handle.gameObject.activeInHierarchy || !Shown())
                return false;
            var denom = Vector3.Dot(dir, _handle.forward);
            if (denom <= 0.0001f)
                return false;
            t = Vector3.Dot(_handle.position - origin, _handle.forward) / denom;
            if (t <= 0.02f || t >= maxT)
                return false;
            point = origin + dir * t;
            var local = _handle.InverseTransformPoint(point);
            var r = _handle.rect;
            r.xMin -= 30f;
            r.xMax += 30f;
            r.yMin -= 20f;
            r.yMax += 20f;
            return r.Contains(new Vector2(local.x, local.y));
        }

        /// <summary>Ray against the card body (the whole canvas rectangle). t = distance along the ray.</summary>
        public bool HitBody(Vector3 origin, Vector3 dir, float maxT, out float t, out Vector3 point)
        {
            t = 0f;
            point = default;
            if (!Shown())
                return false;
            var c = FindCanvas();
            if (c == null)
                return false;
            var rt = (RectTransform)c.transform;
            var denom = Vector3.Dot(dir, rt.forward);
            if (denom <= 0.0001f)
                return false;
            t = Vector3.Dot(rt.position - origin, rt.forward) / denom;
            if (t <= 0.02f || t >= maxT)
                return false;
            point = origin + dir * t;
            var local = rt.InverseTransformPoint(point);
            return rt.rect.Contains(new Vector2(local.x, local.y));
        }

        public void Highlight(bool on)
        {
            if (_handleImage != null)
                _handleImage.color = on ? new Color(0.6f, 1f, 1f, 0.95f) : new Color(0.35f, 0.85f, 1f, 0.55f);
        }

        public void BeginDrag(Vector3 origin, Vector3 hitPoint)
        {
            Dragging = true;
            var m = Mover;
            _mover = m;
            _grabDist = Vector3.Distance(origin, hitPoint);
            _grabOffset = m.position - hitPoint;
            _pos = m.position;
            _rot = m.rotation;
            _scenario = PresentableInput.ActiveScenario;
            _moved = true;

            // head-locked bar (client 2026-11-20, smooth drag): work in the head's own frame on the plane the bar sits on, so a still hand never
            // moves the bar and a moving hand moves it one to one, whatever the head does meanwhile
            // (the bar's position is the bar's own canvas, not its host object: the host sits at the world origin)
            var cam = OnHeadOffset != null ? Camera.main : null;
            if (cam != null)
            {
                var inv = Quaternion.Inverse(cam.transform.rotation);
                var bar = inv * (m.position - cam.transform.position);
                _headPlaneZ = bar.z;
                var o = inv * (origin - cam.transform.position);
                var d = inv * (hitPoint - origin).normalized;
                _headGrab = d.z > 0.05f ? new Vector2(bar.x, bar.y) - HeadPlaneHit(o, d, _headPlaneZ) : Vector2.zero;
            }
        }

        private float _headPlaneZ = 0.9f;
        private Vector2 _headGrab;

        private static Vector2 HeadPlaneHit(Vector3 o, Vector3 d, float z)
        {
            var t = (z - o.z) / d.z;
            return new Vector2(o.x + d.x * t, o.y + d.y * t);
        }

        public void Drag(Vector3 origin, Vector3 dir, Vector3 head)
        {
            if (!Dragging)
                return;
            var target = origin + dir * _grabDist + _grabOffset;
            if (OnHeadOffset != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var inv = Quaternion.Inverse(cam.transform.rotation);
                    var o = inv * (origin - cam.transform.position);
                    var d = inv * dir;
                    if (d.z > 0.05f) // a ray pointing away from the bar's plane leaves the bar where it is
                    {
                        var p = HeadPlaneHit(o, d, _headPlaneZ) + _headGrab;
                        OnHeadOffset(new Vector3(p.x, p.y, _headPlaneZ));
                    }
                }

                return;
            }

            var toHead = new Vector3(target.x - head.x, 0f, target.z - head.z);
            if (toHead.magnitude < 0.8f) // never into the face
                target = new Vector3(head.x, target.y, head.z) + (toHead.sqrMagnitude < 0.0001f ? Vector3.forward : toHead.normalized) * 0.8f;
            _pos = target;
            var look = new Vector3(target.x - head.x, 0f, target.z - head.z);
            if (look.sqrMagnitude > 0.0001f)
                _rot = Quaternion.LookRotation(look, Vector3.up);
            (_mover != null ? _mover : transform).SetPositionAndRotation(_pos, _rot);
        }

        public void EndDrag() => Dragging = false;

        // What a drag moves. Almost every popup is its own host (the host IS the card), then it is the host as always. A gate card (Sc06Gate) is a child
        // canvas that the scenario opens at a world pose far from its host object: rotating / moving the HOST swung the card out of the view
        // (2026-11-21: the Scenario 9 gate "vanished" when dragged), so for such hosts the card (canvas) itself is moved.
        private Transform _mover;

        private Transform Mover
        {
            get
            {
                var c = FindCanvas();
                if (c == null || c.transform == transform)
                    return transform;
                return (c.transform.position - transform.position).sqrMagnitude > 0.25f ? c.transform : transform;
            }
        }
    }
}
