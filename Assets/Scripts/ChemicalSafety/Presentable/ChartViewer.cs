using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Wall compatibility chart. A poke brings a large copy up in front of the user; the close button puts it back.
    /// </summary>
    public sealed class ChartViewer : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Sprite chart;

        private PokeTarget _poke;
        private Transform _panel;
        private Vector3 _panelScale;
        private CanvasGroup _group;
        private bool _open;
        private bool _built;

        public bool IsOpen => _open;

        /// <summary>Raised when the large chart opens / closes (the details card hides while it is open).</summary>
        public event Action Opened;

        public event Action Closed;

        private float _distance = 0.58f;

        /// <summary>How far in front of the head the large chart opens (closer at the glass cabinet).</summary>
        public void SetDistance(float meters) => _distance = Mathf.Clamp(meters, 0.45f, 1.5f);

        private void Start()
        {
            _poke = GetComponent<PokeTarget>();
            if (_poke != null)
                _poke.Poked += _ =>
                {
                    if (_open)
                        Close();
                    else
                        Open();
                };
            BuildPanel();
        }

        public void SetInteractable(bool value)
        {
            if (_poke != null)
                _poke.SetInteractable(value);
        }

        public void Open()
        {
            if (_open)
                return;
            BuildPanel();
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                var fwd = cam.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f)
                    fwd = Vector3.forward;
                fwd.Normalize();
                _panel.position = cam.position + fwd * _distance; // eye level
                _panel.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }

            _open = true;
            _panel.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Fade(1f));
            PresentableFx.Play(PresentableFx.Whoosh, 0.35f);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            StopAllCoroutines();
            StartCoroutine(Fade(0f));
            Closed?.Invoke();
        }

        private IEnumerator Fade(float target)
        {
            var from = _group.alpha;
            yield return PresentableFx.Tween(0.28f, t =>
            {
                _group.alpha = Mathf.Lerp(from, target, t);
                var s = Mathf.Lerp(target > 0.5f ? 0.92f : 1f, target > 0.5f ? 1f : 0.94f, t);
                _panel.localScale = _panelScale * s;
            });
            if (target <= 0f)
                _panel.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            if (_built)
                return;
            _built = true;

            var canvas = PresentableUi.WorldCanvas("ChartPanel", transform, new Vector2(1120f, 1060f), 0.00056f, true, 220);
            _panel = canvas.transform;
            _panelScale = _panel.localScale;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            PresentableUi.Img(PresentableUi.Rect("Plate", _panel, Vector2.zero, Vector2.one), art.roundedSolid,
                new Color(0.02f, 0.08f, 0.14f, 0.94f));
            PresentableUi.Img(PresentableUi.Rect("Chart", _panel, new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.97f)),
                chart, Color.white, preserveAspect: true);
            PresentableUi.AlwaysOnTop(canvas);

            // small X in the top right corner as well (both within arm's reach)
            var x = PresentableUi.Rect("CloseChartX", _panel, new Vector2(0.9f, 0.9f), new Vector2(0.99f, 0.99f));
            var xHit = PresentableUi.Img(PresentableUi.Rect("Hit", x, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Disc", x, Vector2.zero, Vector2.one), art.circle,
                new Color(0.85f, 0.2f, 0.18f, 1f), preserveAspect: true);
            PresentableUi.Img(PresentableUi.Rect("X", x, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f)), art.cross,
                Color.white, preserveAspect: true);
            var xBtn = x.gameObject.AddComponent<Button>();
            xBtn.transition = Selectable.Transition.None;
            xBtn.targetGraphic = xHit;
            xBtn.onClick.AddListener(() =>
            {
                PresentableFx.Play(PresentableFx.Tick);
                Close();
            });

            var close = PresentableUi.Rect("CloseChart", _panel, new Vector2(0.56f, 0.0f), new Vector2(0.96f, 0.115f));
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", close, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Pill", close, Vector2.zero, Vector2.one), art.button, Color.white);
            PresentableUi.Text("Label", close, Vector2.zero, Vector2.one, "Close", 42f, Color.white, bold: true,
                font: art.englishFont);
            var btn = close.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(() =>
            {
                PresentableFx.Play(PresentableFx.Tick);
                Close();
            });

            _panel.gameObject.SetActive(false);
        }
    }
}
