using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// An old fluorescent tube that now and then dims for a moment (two soft dips, never a hard strobe, at most
    /// a third of the brightness) - the room feels old and the user is not bothered.
    /// </summary>
    public sealed class Sc03Flicker : MonoBehaviour
    {
        [SerializeField] private Light[] lights;
        [SerializeField] private Vector2 gapSeconds = new Vector2(7f, 16f);
        [SerializeField] private float depth = 0.33f;

        private float[] _base;
        private float _next;
        private float _t = -1f;

        private void OnEnable()
        {
            _base = new float[lights != null ? lights.Length : 0];
            for (var i = 0; i < _base.Length; i++)
                _base[i] = lights[i] != null ? lights[i].intensity : 0f;
            _next = Time.time + Random.Range(gapSeconds.x * 0.5f, gapSeconds.y);
            _t = -1f;
        }

        private void OnDisable()
        {
            Apply(1f);
        }

        private void Update()
        {
            if (_t < 0f)
            {
                if (Time.time >= _next)
                    _t = 0f;
                return;
            }

            _t += Time.deltaTime;
            const float len = 0.55f;
            if (_t >= len)
            {
                _t = -1f;
                _next = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);
                Apply(1f);
                return;
            }

            // two soft dips inside 0.55 s
            var k = _t / len;
            var dip = Mathf.Max(Mathf.Sin(k * Mathf.PI * 2f) > 0f ? Mathf.Sin(k * Mathf.PI * 4f) * 0.5f + 0.5f : 0f, 0f);
            Apply(1f - depth * dip);
        }

        private void Apply(float k)
        {
            if (lights == null || _base == null)
                return;
            for (var i = 0; i < lights.Length && i < _base.Length; i++)
            {
                if (lights[i] != null)
                    lights[i].intensity = _base[i] * k;
            }
        }
    }
}
