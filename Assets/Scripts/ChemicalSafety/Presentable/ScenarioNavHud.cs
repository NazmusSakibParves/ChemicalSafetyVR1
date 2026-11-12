using ChemicalSafety.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// "Previous" / "Next" glass pills fixed in the lower RIGHT of the view (the mirror of the timer capsule of <see cref="SessionHud"/>, same
    /// see-through look, follows the head). Finger touch and controller ray press them like every other button (a world canvas Button).
    /// Previous is dimmed at the first scenario, Next at the last one. The jump itself is done by the director (<see cref="ScenarioNav"/>).
    /// </summary>
    public sealed class ScenarioNavHud : MonoBehaviour
    {
        // metres in the view: right, down, forward of the head (the timer sits at -0.30 on the left)
        private static readonly Vector3 NavOffset = new Vector3(0.30f, -0.27f, 0.9f);

        public static ScenarioNavHud Instance { get; private set; }

        private sealed class Pill
        {
            public Image edge;
            public Image glow;
            public Image plate;
            public Button button;
            public CanvasGroup group;
            public HoverLift lift;
        }

        private Transform _cam;
        private Transform _root;
        private CanvasGroup _canvasGroup;
        private Pill _prev;
        private Pill _next;
        private float _born;
        private bool _snapped;
        private bool _visible = true;
        private bool _busy;
        private Sprite _prevIcon;
        private Sprite _nextIcon;

        public static ScenarioNavHud Create(Transform cam, Sprite previousIcon, Sprite nextIcon)
        {
            if (Instance != null)
                Destroy(Instance.gameObject);
            var go = new GameObject("ScenarioNavHud");
            var hud = go.AddComponent<ScenarioNavHud>();
            hud._cam = cam;
            hud._prevIcon = previousIcon;
            hud._nextIcon = nextIcon;
            Instance = hud;
            hud.Build();
            return hud;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
            }
        }

        /// <summary>True while a jump is running: the buttons ignore presses (no double jump).</summary>
        public void SetBusy(bool busy) => _busy = busy;

        private void Build()
        {
            var tr = new GameObject("Nav");
            tr.transform.SetParent(transform, false);
            _root = tr.transform;
            var canvas = PresentableUi.WorldCanvas("NavCanvas", _root, new Vector2(500f, 120f), 0.001f, true, 30);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var host = (RectTransform)canvas.transform;
            _prev = BuildPill(host, new Vector2(-126f, 0f), "NavPrevious", true, _prevIcon, ScenarioNav.RequestPrevious);
            _next = BuildPill(host, new Vector2(126f, 0f), "NavNext", false, _nextIcon, ScenarioNav.RequestNext);
            PresentableUi.AlwaysOnTop(canvas);
            _born = Time.unscaledTime;
        }

        private Pill BuildPill(RectTransform host, Vector2 pos, string labelKey, bool iconLeft, Sprite icon, UnityEngine.Events.UnityAction onClick)
        {
            var p = new Pill();
            var neon = new Color(0.35f, 0.85f, 1f, 1f);
            var rt = PresentableUi.Centered(labelKey, host, pos, new Vector2(240f, 96f));
            p.group = rt.gameObject.AddComponent<CanvasGroup>();
            p.glow = PresentableUi.Img(PresentableUi.Rect("Glow", rt, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f)), SessionHud.EdgeSprite(),
                new Color(neon.r, neon.g, neon.b, 0.26f), sliced: true);
            p.edge = PresentableUi.Img(PresentableUi.Rect("Edge", rt, Vector2.zero, Vector2.one), SessionHud.EdgeSprite(), neon, sliced: true);
            // see-through plate (same alpha as the timer): the scene stays visible behind the button
            p.plate = PresentableUi.Img(PresentableUi.Rect("Plate", rt, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)),
                SessionHud.CapsuleSprite(), new Color(0.02f, 0.08f, 0.17f, 0.36f), sliced: true, raycast: true);

            // arrow icon at the outer end, label in the remaining space
            var iconRt = PresentableUi.Centered("Icon", rt, new Vector2(iconLeft ? -82f : 82f, 0f), new Vector2(46f, 46f));
            Image iconImg;
            if (icon != null)
            {
                iconImg = PresentableUi.Img(iconRt, icon, Color.white);
                iconImg.preserveAspect = true;
            }
            else
            {
                iconImg = PresentableUi.Img(iconRt, SessionHud.PlaySprite(), Color.white); // a triangle: points right; flipped for Previous
                if (iconLeft)
                    iconRt.localScale = new Vector3(-1f, 1f, 1f);
            }

            iconImg.color = new Color(0.78f, 0.97f, 1f, 1f);
            iconImg.raycastTarget = false;
            var label = PresentableUi.Centered("Label", rt, new Vector2(iconLeft ? 22f : -22f, 0f), new Vector2(150f, 56f));
            var text = PresentableUi.Text(label, Loc.T("GENERAL", labelKey), 34f, new Color(0.85f, 0.98f, 1f, 1f), TextAlignmentOptions.Center, true);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            p.button = rt.gameObject.AddComponent<Button>();
            p.button.targetGraphic = p.plate;
            p.button.transition = Selectable.Transition.None;
            p.lift = rt.gameObject.AddComponent<HoverLift>();
            p.lift.Init(rt);
            p.button.onClick.AddListener(p.lift.Press);
            p.button.onClick.AddListener(() =>
            {
                if (_busy || !_visible)
                    return;
                onClick();
            });
            return p;
        }

        private void Update()
        {
            if (_prev == null)
                return;
            var canPrev = ScenarioNav.CanPrevious && !_busy;
            var canNext = ScenarioNav.CanNext && !_busy;
            // client: Scenario 1 has no Previous button at all (not dimmed, not there)
            var showPrev = ScenarioNav.CanPrevious;
            if (_prev.group.gameObject.activeSelf != showPrev)
                _prev.group.gameObject.SetActive(showPrev);
            _prev.group.alpha = canPrev ? 1f : 0.38f;
            _next.group.alpha = canNext ? 1f : 0.38f;
            // entrance: grow in with a small overshoot, like the timer
            var age = Time.unscaledTime - _born;
            var sc = age < 0.7f ? PresentableFx.EaseOutBack(Mathf.Clamp01(age / 0.7f)) : 1f;
            _root.localScale = Vector3.one * sc;
        }

        private void LateUpdate()
        {
            if (_cam == null || _root == null)
                return;
            // fixed in the view: the whole offset rotates with the head, with a short smoothing so it does not jitter
            var k = _snapped ? 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime) : 1f;
            var pos = _cam.position + _cam.rotation * NavOffset;
            var rot = Quaternion.LookRotation(pos - _cam.position, _cam.up);
            _root.SetPositionAndRotation(Vector3.Lerp(_root.position, pos, k), Quaternion.Slerp(_root.rotation, rot, k));
            _snapped = true;
        }
    }
}
