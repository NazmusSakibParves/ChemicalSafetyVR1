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

        private Button _hovered;
        private Button _armedBlock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
        }

        public static void Register(Canvas canvas)
        {
            Entries.Add(new Entry { canvas = canvas, group = canvas.GetComponent<CanvasGroup>() });
        }

        private void Awake()
        {
            if (art != null)
                _cursorSprite = art.ring;
        }

        private void Update()
        {
            var hand = GlovedHandPose.Right;
            if (hand == null || !hand.HasTip)
            {
                ClearHover();
                return;
            }

            var tip = hand.TipPosition;
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
                    HideCursor(e);
                    continue;
                }

                if (Time.unscaledTime >= e.nextRefresh)
                {
                    e.nextRefresh = Time.unscaledTime + 0.4f;
                    e.canvas.GetComponentsInChildren(false, e.buttons);
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

                if (e != bestEntry)
                    HideCursor(e);
            }

            if (best != _hovered)
            {
                ClearHover();
                _hovered = best;
                if (_hovered != null)
                {
                    SetHover(_hovered, true);
                    PresentableFx.Haptic(true, 0.15f, 0.03f);
                }
            }

            if (best == null)
            {
                _armedBlock = null;
                return;
            }

            ShowCursor(bestEntry, bestLocal, bestDepth);

            if (bestDepth < pressDepth && _armedBlock != best)
            {
                _armedBlock = best;
                hand.PlayPokeCurl();
                PresentableFx.Haptic(true, 0.5f, 0.07f);
                PresentableFx.Play(PresentableFx.Tick, 0.8f);
                best.onClick.Invoke();
            }
            else if (bestDepth > rearmDepth && _armedBlock == best)
            {
                _armedBlock = null;
            }
        }

        private void ClearHover()
        {
            if (_hovered != null)
                SetHover(_hovered, false);
            _hovered = null;
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
