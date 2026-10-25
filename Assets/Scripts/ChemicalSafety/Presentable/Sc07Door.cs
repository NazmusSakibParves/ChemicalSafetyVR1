using System.Collections;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 "FIRE EXIT" door. Closed it is a plain steel door under a green sign; Open() swings it about its hinge
    /// (push-bar clack, hinge, a soft bang) and daylight from outside spills in: a bright panel behind the door and a white light.
    /// </summary>
    public sealed class Sc07Door : MonoBehaviour
    {
        [Tooltip("Pivot of the door leaf (at the hinge edge); the leaf mesh hangs under it.")]
        [SerializeField] private Transform leaf;
        [SerializeField] private float openDegrees = 105f;
        [SerializeField] private float seconds = 0.75f;
        [SerializeField] private Light daylight;
        [SerializeField] private Renderer outside;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private float daylightIntensity = 3.2f;

        private AudioSource _audio;
        private Quaternion _closed;
        private bool _captured;
        private Coroutine _swing;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Capture();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.85f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 3f;
            _audio.maxDistance = 30f;
            Apply(0f);
        }

        private void Capture()
        {
            if (_captured || leaf == null)
                return;
            _captured = true;
            _closed = leaf.localRotation;
        }

        /// <summary>Back to closed at once (a new run of the scenario).</summary>
        public void CloseInstant()
        {
            Capture();
            if (_swing != null)
                StopCoroutine(_swing);
            _swing = null;
            IsOpen = false;
            Apply(0f);
        }

        public void Open()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            if (_audio != null && openClip != null)
                _audio.PlayOneShot(openClip, 0.9f);
            if (_swing != null)
                StopCoroutine(_swing);
            _swing = StartCoroutine(SwingOpen());
        }

        private IEnumerator SwingOpen()
        {
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                // a quick push, then it slows as it hits the stop and bounces a little
                var k = Mathf.Clamp01(t / seconds);
                var e = 1f - Mathf.Pow(1f - k, 2.4f);
                Apply(e + 0.03f * Mathf.Sin(k * Mathf.PI * 3f) * (1f - k));
                yield return null;
            }

            Apply(1f);
            _swing = null;
        }

        private void Apply(float open)
        {
            Capture();
            if (leaf != null)
                leaf.localRotation = _closed * Quaternion.AngleAxis(openDegrees * open, Vector3.up);
            if (daylight != null)
            {
                daylight.enabled = open > 0.02f;
                daylight.intensity = daylightIntensity * Mathf.Clamp01(open * 1.2f);
            }
        }
    }
}
