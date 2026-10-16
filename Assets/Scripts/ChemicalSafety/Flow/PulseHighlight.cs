using UnityEngine;

namespace ChemicalSafety.Flow
{
    /// <summary>Soft scale pulse used during scenario intro focus.</summary>
    public sealed class PulseHighlight : MonoBehaviour
    {
        [SerializeField] private float amount = 0.06f;
        [SerializeField] private float speed = 2.2f;

        private Vector3 _baseScale;
        private bool _playing;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void Play()
        {
            if (_baseScale == Vector3.zero)
                _baseScale = transform.localScale;
            _playing = true;
            enabled = true;
        }

        public void StopPulse()
        {
            _playing = false;
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            if (!_playing)
                return;

            var s = 1f + Mathf.Sin(Time.time * speed) * amount;
            transform.localScale = _baseScale * s;
        }
    }
}
