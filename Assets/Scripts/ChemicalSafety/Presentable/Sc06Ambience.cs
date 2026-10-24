using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 lab sound: a quiet air-conditioning hum, the fume hood motor and air rush while the hood runs, switch clicks,
    /// the glass sash sliding and the pour. Every loop fades under the voice-over and comes back after it. Never loud.
    /// </summary>
    public sealed class Sc06Ambience : MonoBehaviour
    {
        [SerializeField] private AudioClip roomLoop;
        [SerializeField] private AudioClip hoodLoop;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip slideClip;
        [SerializeField] private AudioClip pourLoop;
        [SerializeField] private AudioSource voice;
        [SerializeField, Range(0f, 1f)] private float roomLevel = 0.22f;
        [SerializeField, Range(0f, 1f)] private float hoodLevel = 0.34f;
        [SerializeField, Range(0f, 1f)] private float pourLevel = 0.5f;
        [SerializeField, Range(0f, 1f)] private float duck = 0.3f;

        private AudioSource _room;
        private AudioSource _hood;
        private AudioSource _pour;
        private AudioSource _shot;
        private bool _on;
        private bool _hoodOn;
        private bool _pourOn;
        private bool _vent = true;
        private float _roomV;
        private float _hoodV;
        private float _pourV;

        public bool IsOn => _on;
        public bool HoodSound => _hoodOn;
        public float HoodVolume => _hood != null ? _hood.volume : 0f;
        public float RoomVolume => _room != null ? _room.volume : 0f;

        private AudioSource Make(AudioClip clip, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        private void Awake()
        {
            _room = Make(roomLoop, true);
            _hood = Make(hoodLoop, true);
            _pour = Make(pourLoop, true);
            _shot = Make(null, false);
            _shot.volume = 1f;
        }

        public void Begin()
        {
            _on = true;
            if (_room != null && roomLoop != null && !_room.isPlaying)
                _room.Play();
        }

        public void End()
        {
            _on = false;
            _hoodOn = false;
            _pourOn = false;
        }

        public void SetHood(bool on)
        {
            _hoodOn = on;
            if (on && _hood != null && hoodLoop != null && !_hood.isPlaying)
                _hood.Play();
        }

        /// <summary>General ventilation switch: a little more room air when it runs.</summary>
        public void SetVent(bool on) => _vent = on;

        public void SetPour(bool on)
        {
            _pourOn = on;
            if (on && _pour != null && pourLoop != null && !_pour.isPlaying)
                _pour.Play();
        }

        public void PlayClick() => Shot(clickClip, 0.8f);

        public void PlaySlide() => Shot(slideClip, 0.7f);

        private void Shot(AudioClip clip, float volume)
        {
            if (clip != null && _shot != null)
                _shot.PlayOneShot(clip, volume);
        }

        private void Update()
        {
            var talking = voice != null && voice.isPlaying;
            var d = talking ? duck : 1f;
            var dt = Time.unscaledDeltaTime;
            var rate = talking ? 3f : 0.9f;
            _roomV = Mathf.MoveTowards(_roomV, _on ? roomLevel * d * (_vent ? 1f : 0.6f) : 0f, rate * 0.3f * dt);
            _hoodV = Mathf.MoveTowards(_hoodV, _on && _hoodOn ? hoodLevel * d : 0f, rate * 0.4f * dt);
            _pourV = Mathf.MoveTowards(_pourV, _on && _pourOn ? pourLevel * d : 0f, 2.2f * dt);
            Apply(_room, _roomV);
            Apply(_hood, _hoodV);
            Apply(_pour, _pourV);
        }

        private static void Apply(AudioSource s, float v)
        {
            if (s == null)
                return;
            s.volume = v;
            if (v <= 0.001f && s.isPlaying)
                s.Stop();
        }
    }
}
