using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 5 factory sound: a quiet machine loop that fades under the voice-over (the voice stays the focus)
    /// and comes back when the voice ends. Never loud: the level is capped so it cannot bother the user.
    /// </summary>
    public sealed class Sc05Ambience : MonoBehaviour
    {
        [SerializeField] private AudioClip loop;
        [SerializeField] private AudioSource voice;
        [SerializeField, Range(0f, 1f)] private float level = 0.32f;
        [SerializeField, Range(0f, 1f)] private float underVoice = 0.09f;
        [Tooltip("Seconds to fade down when a voice starts and up when it ends.")]
        [SerializeField] private float fadeDown = 0.35f;
        [SerializeField] private float fadeUp = 1.2f;

        private AudioSource _src;
        private float _current;
        private bool _on;

        public bool IsOn => _on;
        public float CurrentVolume => _src != null ? _src.volume : 0f;
        public float TargetVolume => !_on ? 0f : (voice != null && voice.isPlaying ? underVoice : level);

        private void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = loop;
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.volume = 0f;
        }

        /// <summary>Starts the loop (fades in). Safe to call twice.</summary>
        public void Begin()
        {
            _on = true;
            if (_src != null && loop != null && !_src.isPlaying)
                _src.Play();
        }

        /// <summary>Fades the loop out and stops it.</summary>
        public void End() => _on = false;

        private void Update()
        {
            if (_src == null)
                return;
            var target = TargetVolume;
            var rate = target < _current ? 1f / Mathf.Max(0.05f, fadeDown) : 1f / Mathf.Max(0.05f, fadeUp);
            _current = Mathf.MoveTowards(_current, target, rate * level * Time.unscaledDeltaTime);
            _src.volume = _current;
            if (!_on && _current <= 0.001f && _src.isPlaying)
                _src.Stop();
        }
    }
}
