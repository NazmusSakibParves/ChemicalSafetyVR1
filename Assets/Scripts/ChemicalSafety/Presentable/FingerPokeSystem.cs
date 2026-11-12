using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Finger-only UI: the gloved right index fingertip hovers and presses buttons on registered world-space
    /// canvases (MCQ, label popup, Instructions). Hover = highlight + haptic + a glowing cursor on the panel,
    /// press = touching the panel (button dips, finger curls, tick + haptic, onClick). No XR ray involved.
    /// </summary>
    public sealed class FingerPokeSystem : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private float hoverDepth = 0.09f;
        [SerializeField] private float pressDepth = 0.018f;
        [SerializeField] private float rearmDepth = 0.05f;

        private sealed class Entry
        {
            public Canvas canvas;
            public CanvasGroup group;
            public RectTransform cursor;
            public Image cursorImage;
            public readonly List<Button> buttons = new List<Button>(8);
            public float nextRefresh;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static Sprite _cursorSprite;

        // one set of hover / press state per hand; the left hand only pokes while a screen asks for it (the login keyboard)
        private sealed class HandState
        {
            public Button hovered;
            public Button armed;
            public int band;
        }

        private readonly HandState _right = new HandState();
        private readonly HandState _left = new HandState();

        /// <summary>When true the left index fingertip presses buttons too (two-hand typing on the login keyboard).</summary>
        public static bool LeftPokeEnabled { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
        }

        public static void Register(Canvas canvas)
        {
            Entries.Add(new Entry { canvas = canvas, group = canvas.GetComponent<CanvasGroup>() });
        }

        /// <summary>Forgets every registered canvas (Back to Home reloads the scene; the old canvases are gone).</summary>
        public static void ClearAll() => Entries.Clear();

        /// <summary>Registered interactive canvases (used by the controller ray, ControllerRayPointer).</summary>
        public static void CollectCanvases(List<Canvas> into)
        {
            foreach (var e in Entries)
                if (e.canvas != null)
                    into.Add(e.canvas);
        }

        /// <summary>
        /// Physical stop for the fingertip: the push (world space) that keeps a fingertip sphere of the given
        /// radius in front of every visible registered panel it is over, so the glove rests on the panel instead
        /// of sinking into it. Zero when the tip is not over a panel or is more than 12 cm behind it.
        /// </summary>
        public static Vector3 PanelPush(Vector3 tip, float radius)
        {
            var best = Vector3.zero;
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var e = Entries[i];
                if (e.canvas == null || !e.canvas.gameObject.activeInHierarchy || (e.group != null && e.group.alpha < 0.6f))
                    continue;
                var rt = (RectTransform)e.canvas.transform;
                var local = rt.InverseTransformPoint(tip);
                var scale = rt.lossyScale;
                var depth = -local.z * scale.z; // positive = in front of the panel (user side)
                if (depth >= radius || depth < -0.12f)
                    continue;
                var margin = 0.02f / Mathf.Max(0.0001f, scale.x);
                var r = rt.rect;
                if (local.x < r.xMin - margin || local.x > r.xMax + margin || local.y < r.yMin - margin ||
                    local.y > r.yMax + margin)
                    continue;
                var push = -rt.forward * (radius - depth);
                if (push.sqrMagnitude > best.sqrMagnitude)
                    best = push;
            }

            return best;
        }

        private void Awake()
        {
            if (art != null)
                _cursorSprite = art.ring;
        }

        private void Update()
        {
            // controller-ray scenario (see PresentableInput): the fingertip does not press, the ray does
            if (PresentableInput.RayActive)
            {
                ClearHover(_right);
                ClearHover(_left);
                foreach (var e in Entries)
                    if (e.canvas != null)
                        HideCursor(e);
                return;
            }

            Process(GlovedHandPose.Right, _right, true);
            if (LeftPokeEnabled)
                Process(GlovedHandPose.Left, _left, false);
            else if (_left.hovered != null || _left.armed != null)
                ClearHover(_left);
        }

        private void Process(GlovedHandPose hand, HandState st, bool primary)
        {
            if (hand == null || !hand.HasTip)
            {
                ClearHover(st);
                return;
            }

            var tip = hand.PressPoint;
            if (primary)
                ScrollZone.Handle(tip, pressDepth * 1.5f); // phone-style drag scrolling; inert unless a scroll zone is registered
            Button best = null;
            Entry bestEntry = null;
            var bestDepth = float.MaxValue;
            Vector3 bestLocal = default;

            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var e = Entries[i];
                if (e.canvas == null)
                {
                    Entries.RemoveAt(i);
                    continue;
                }

                if (!e.canvas.gameObject.activeInHierarchy || (e.group != null && e.group.alpha < 0.6f))
                {
                    if (primary)
                        HideCursor(e);
                    continue;
                }

                if (Time.unscaledTime >= e.nextRefresh)
                {
                    e.nextRefresh = Time.unscaledTime + 0.4f;
                    e.canvas.GetComponentsInChildren(false, e.buttons);
                    if (primary)
                        EnsureCursor(e);
                }

                foreach (var b in e.buttons)
                {
                    if (b == null || !b.IsInteractable() || !b.gameObject.activeInHierarchy)
                        continue;
                    var rt = (RectTransform)b.transform;
                    var local = rt.InverseTransformPoint(tip);
                    // canvases face -z in local space: positive depth = in front of the panel (towards the user)
                    var depth = -local.z * rt.lossyScale.z;
                    if (depth < -0.04f || depth > hoverDepth)
                        continue;
                    var r = rt.rect;
                    r.xMin -= 12f;
                    r.xMax += 12f;
                    r.yMin -= 12f;
                    r.yMax += 12f;
                    if (!r.Contains(new Vector2(local.x, local.y)))
                        continue;
                    if (depth < bestDepth)
                    {
                        bestDepth = depth;
                        best = b;
                        bestEntry = e;
                        bestLocal = e.canvas.transform.InverseTransformPoint(tip);
                    }
                }

                if (primary && e != bestEntry)
                    HideCursor(e);
            }

            if (best != st.hovered)
            {
                ClearHover(st);
                st.hovered = best;
                if (st.hovered != null)
                {
                    SetHover(st.hovered, true);
                    PresentableFx.Haptic(primary, 0.15f, 0.03f);
                }
            }

            if (best == null)
            {
                st.armed = null;
                st.band = 0;
                return;
            }

            if (primary)
                ShowCursor(bestEntry, bestLocal, bestDepth);

            // soft haptic ramp while the fingertip approaches the button
            var band = bestDepth < 0.03f ? 2 : bestDepth < 0.06f ? 1 : 0;
            if (band > st.band)
                PresentableFx.Haptic(primary, band == 2 ? 0.22f : 0.1f, 0.025f);
            st.band = band;

            if (bestDepth < pressDepth && st.armed != best)
            {
                st.armed = best;
                hand.PlayPokeCurl();
                PresentableFx.Haptic(primary, 0.5f, 0.07f);
                PresentableFx.Play(PresentableFx.Tick, 0.8f);
                var lift = best.GetComponent<HoverLift>();
                if (lift != null)
                    lift.Press();
                if (bestEntry.cursor != null)
                    StartCoroutine(Ripple(bestEntry, bestLocal));
                best.onClick.Invoke();
            }
            else if (bestDepth > rearmDepth && st.armed == best)
            {
                st.armed = null;
            }
        }

        private static IEnumerator Ripple(Entry e, Vector3 canvasLocal)
        {
            if (e.canvas == null || _cursorSprite == null)
                yield break;
            var rt = PresentableUi.Centered("Ripple", e.canvas.transform, Vector2.zero, new Vector2(40f, 40f));
            rt.localPosition = new Vector3(canvasLocal.x, canvasLocal.y, -32f);
            var img = PresentableUi.Img(rt, _cursorSprite, new Color(0.7f, 1f, 1f, 0.9f));
            yield return PresentableFx.Tween(0.32f, t =>
            {
                if (rt == null)
                    return;
                rt.sizeDelta = Vector2.one * Mathf.Lerp(40f, 150f, t);
                PresentableUi.SetAlpha(img, (1f - t) * 0.9f);
            }, PresentableFx.EaseOutCubic);
            if (rt != null)
                Destroy(rt.gameObject);
        }

        private static void ClearHover(HandState st)
        {
            if (st.hovered != null)
                SetHover(st.hovered, false);
            st.hovered = null;
            st.armed = null;
            st.band = 0;
        }

        private static void SetHover(Button b, bool on)
        {
            var lift = b.GetComponent<HoverLift>();
            if (lift != null)
                lift.SetHover(on);
        }

        private static void EnsureCursor(Entry e)
        {
            if (e.cursor != null || _cursorSprite == null)
                return;
            e.cursor = PresentableUi.Centered("FingerCursor", e.canvas.transform, Vector2.zero, new Vector2(70f, 70f));
            e.cursorImage = PresentableUi.Img(e.cursor, _cursorSprite, new Color(0.4f, 0.95f, 1f, 0f));
            e.cursor.SetAsLastSibling();
            e.cursor.localPosition += new Vector3(0f, 0f, -30f);
            e.cursor.gameObject.SetActive(false);
        }

        private static void ShowCursor(Entry e, Vector3 canvasLocal, float depth)
        {
            if (e.cursor == null)
                return;
            e.cursor.gameObject.SetActive(true);
            var k = Mathf.Clamp01(depth / 0.09f);
            e.cursor.localPosition = new Vector3(canvasLocal.x, canvasLocal.y, -30f);
            e.cursor.sizeDelta = Vector2.one * Mathf.Lerp(34f, 90f, k);
            PresentableUi.SetAlpha(e.cursorImage, Mathf.Lerp(0.95f, 0.45f, k));
        }

        private static void HideCursor(Entry e)
        {
            if (e.cursor != null && e.cursor.gameObject.activeSelf)
                e.cursor.gameObject.SetActive(false);
        }
    }
}
