using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>Scenario 9: the bobbing green arrow over the thing to touch (same look as the Sc06Lab marker arrow).</summary>
    public sealed class Sc09Guide : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;

        private Transform _arrow;
        private Transform _target;
        private Vector3 _offset;

        public bool Visible => _arrow != null && _arrow.gameObject.activeSelf;

        public void PointAt(Transform target, Vector3 worldOffset)
        {
            Ensure();
            if (_arrow == null)
                return;
            _target = target;
            _offset = worldOffset;
            _arrow.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_arrow != null)
                _arrow.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_arrow == null || !_arrow.gameObject.activeSelf || _target == null)
                return;
            var tick = Mathf.Sin(Time.time * 4.2f);
            _arrow.position = _target.position + _offset + Vector3.up * (tick * 0.045f);
            _arrow.localScale = Vector3.one * (1f + 0.12f * tick);
            var cam = Camera.main;
            if (cam != null)
            {
                var f = cam.transform.position - _arrow.position;
                f.y = 0f;
                if (f.sqrMagnitude > 1e-4f)
                    _arrow.rotation = Quaternion.LookRotation(-f.normalized, Vector3.up);
            }
        }

        private void Ensure()
        {
            if (_arrow != null || art == null)
                return;
            _arrow = new GameObject("Sc09Arrow").transform;
            _arrow.SetParent(transform, false);
            var canvas = PresentableUi.WorldCanvas("Sc09ArrowCanvas", _arrow, new Vector2(200f, 280f), 0.0011f, false, 80);
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", canvas.transform, new Vector2(0f, 0.1f), new Vector2(1f, 0.9f), art.radialGlow,
                    new Color(0.2f, 1f, 0.4f, 0.35f));
            if (art.arrowDown != null)
                PresentableUi.Img("Arrow", canvas.transform, new Vector2(0.14f, 0.06f), new Vector2(0.86f, 0.94f), art.arrowDown,
                    new Color(0.3f, 1f, 0.45f));
            PresentableUi.AlwaysOnTop(canvas);
            _arrow.gameObject.SetActive(false);
        }
    }
}
