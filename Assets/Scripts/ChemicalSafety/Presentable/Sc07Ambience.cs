using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 sound: hall hum, the fire (crackle follows the fire's intensity) and the fire alarm bell. Every loop
    /// ducks under the voice-over and returns after it; the alarm is capped so it never drowns the voice.
    /// </summary>
    public sealed class Sc07Ambience : MonoBehaviour
    {
        [SerializeField] private AudioClip hall;
        [SerializeField] private AudioClip fire;
        [SerializeField] private AudioClip alarm;
        [SerializeField] private AudioSource voice;
        [SerializeField, Range(0f, 1f)] private float hallLevel = 0.3f;
        [SerializeField, Range(0f, 1f)] private float fireLevel = 0.55f;
        [SerializeField, Range(0f, 1f)] private float alarmLevel = 0.38f;
        [SerializeField, Range(0f, 1f)] private float duck = 0.28f;
        [SerializeField] private Transform fireAt;
        [SerializeField] private Transform alarmAt;

        private AudioSource _hall, _fire, _alarm;
        private float _hallV, _fireV, _alarmV, _duckV = 1f;
        private bool _on;
        private bool _alarmOn;
        private float _fireIntensity;

        public bool AlarmRinging => _alarmOn && _alarmV > 0.02f;
        public bool IsOn => _on;

        private AudioSource Make(AudioClip clip, Transform at, bool spatial)
        {
            var go = new GameObject("Loop_" + (clip != null ? clip.name : "none"));
            go.transform.SetParent(transform, false);
            if (at != null)
                go.transform.position = at.position;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            s.spatialBlend = spatial && at != null ? 0.6f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = 40f;
            return s;
        }

        private void Awake()
        {
            _hall = Make(hall, null, false);
            _fire = Make(fire, fireAt, true);
            _alarm = Make(alarm, alarmAt, true);
        }

        public void Begin()
        {
            _on = true;
            if (hall != null && !_hall.isPlaying)
                _hall.Play();
        }

        public void End()
        {
            _on = false;
            _alarmOn = false;
            _fireIntensity = 0f;
        }

        public void SetAlarm(bool on)
        {
            _alarmOn = on;
            if (on && alarm != null && !_alarm.isPlaying)
                _alarm.Play();
        }

        /// <summary>0..1: loudness of the fire loop.</summary>
        public void SetFire(float intensity)
        {
            _fireIntensity = Mathf.Clamp01(intensity);
            if (_fireIntensity > 0.01f && fire != null && !_fire.isPlaying)
                _fire.Play();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var talking = voice != null && voice.isPlaying;
            _duckV = Mathf.MoveTowards(_duckV, talking ? duck : 1f, dt * (talking ? 3f : 0.9f));
            _hallV = Mathf.MoveTowards(_hallV, _on ? hallLevel : 0f, dt * 0.6f);
            _fireV = Mathf.MoveTowards(_fireV, _on ? fireLevel * _fireIntensity : 0f, dt * 0.8f);
            _alarmV = Mathf.MoveTowards(_alarmV, _on && _alarmOn ? alarmLevel : 0f, dt * (_alarmOn ? 1.5f : 0.9f));
            _hall.volume = _hallV * _duckV;
            _fire.volume = _fireV * _duckV;
            _alarm.volume = _alarmV * _duckV;
            if (_hallV <= 0.001f && !_on && _hall.isPlaying)
                _hall.Stop();
            if (_fireV <= 0.001f && _fireIntensity <= 0.01f && _fire.isPlaying)
                _fire.Stop();
            if (_alarmV <= 0.001f && !_alarmOn && _alarm.isPlaying)
                _alarm.Stop();
        }
    }
}
