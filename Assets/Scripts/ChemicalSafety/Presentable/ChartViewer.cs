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

        private float _distance = 1.05f;

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
                _panel.position = cam.position + fwd * _distance + Vector3.down * 0.02f;
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

            var canvas = PresentableUi.WorldCanvas("ChartPanel", transform, new Vector2(1120f, 1060f), 0.0007f, true, 220);
            _panel = canvas.transform;
            _panelScale = _panel.localScale;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            PresentableUi.Img(PresentableUi.Rect("Plate", _panel, Vector2.zero, Vector2.one), art.roundedSolid,
                new Color(0.02f, 0.08f, 0.14f, 0.94f));
            PresentableUi.Img(PresentableUi.Rect("Chart", _panel, new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.97f)),
                chart, Color.white, preserveAspect: true);
            PresentableUi.AlwaysOnTop(canvas);

            var close = PresentableUi.Rect("CloseChart", _panel, new Vector2(0.78f, 0.01f), new Vector2(0.98f, 0.1f));
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
