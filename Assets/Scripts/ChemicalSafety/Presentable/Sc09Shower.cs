using System;
using System.Collections;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 9: the safety shower and eye wash. The user comes to the glowing "STAND HERE" mark, touches the push plate, the valve
    /// clicks and the water starts (shower head spray, two eye wash jets, splashes in the bowl). While it runs the face shield is
    /// washed clean (<see cref="Sc09Visor.Wash"/>), the hologram text "HOLD Eyelids OPEN ... 15 MINUTES" hangs in front of the user.
    /// Objects are found by the names the scene builder gives them.
    /// </summary>
    public sealed class Sc09Shower : MonoBehaviour
    {
        [SerializeField] private Sc09Visor visor;
        [SerializeField] private Sprite holoSprite;
        [SerializeField] private AudioClip valveClip;
        [SerializeField] private AudioClip waterLoop;
        [SerializeField, Range(0f, 1f)] private float waterLevel = 0.55f;
        [SerializeField] private float washSeconds = 12f;

        private PokeTarget _poke;
        private Transform _pushGlow;
        private Transform _standHere;
        private Transform _holo;
        private ParticleSystem[] _water;
        private Transform _plate;
        private AudioSource _loop;
        private AudioSource _shot;
        private float _loopV;
        private bool _loopOn;
        private Vector3 _plateHome;

        public bool Running { get; private set; }
        public bool WashDone { get; private set; }
        public bool HoloVisible => _holo != null && _holo.gameObject.activeSelf;
        public Transform PushPlate => _plate;
        public PokeTarget PushPoke => _poke;
        public int ActiveJets
        {
            get
            {
                var n = 0;
                if (_water != null)
                    foreach (var p in _water)
                        if (p != null && p.isPlaying)
                            n++;
                return n;
            }
        }

        /// <summary>Raised when the push plate is touched.</summary>
        public event Action Pushed;

        /// <summary>Raised when the shield is clean.</summary>
        public event Action Washed;

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void Awake()
        {
            var root = transform.parent != null ? transform.parent : transform;
            var p = Find(root, "Sc09PushPoke");
            _poke = p != null ? p.GetComponent<PokeTarget>() : null;
            _pushGlow = Find(root, "Sc09PushGlow");
            _standHere = Find(root, "Sc09StandHere");
            if (holoSprite != null)
            {
                _holo = new GameObject("Sc09Holo").transform;
                _holo.SetParent(root, false);
                var canvas = PresentableUi.WorldCanvas("Sc09HoloCanvas", _holo, new Vector2(1100f, 420f), 0.0009f, false, 70);
                PresentableUi.Img("HoloText", canvas.transform, Vector2.zero, Vector2.one, holoSprite, Color.white);
                PresentableUi.AlwaysOnTop(canvas);
                _holo.gameObject.SetActive(false);
            }
            _plate = Find(root, "Sc09PushPlate");
            var fx = Find(root, "Sc09WaterFx");
            _water = fx != null ? fx.GetComponentsInChildren<ParticleSystem>(true) : new ParticleSystem[0];
            _loop = gameObject.AddComponent<AudioSource>();
            _loop.clip = waterLoop;
            _loop.loop = true;
            _loop.playOnAwake = false;
            _loop.spatialBlend = 0f;
            _loop.volume = 0f;
            _shot = gameObject.AddComponent<AudioSource>();
            _shot.playOnAwake = false;
            _shot.spatialBlend = 0f;
            if (_plate != null)
                _plateHome = _plate.localPosition;
        }

        private void Start()
        {
            if (_poke != null)
            {
                _poke.Poked += _ => Push();
                _poke.SetInteractable(false);
            }

            ResetAll();
        }

        private void Update()
        {
            var target = _loopOn ? waterLevel : 0f;
            if (!Mathf.Approximately(_loopV, target))
            {
                _loopV = Mathf.MoveTowards(_loopV, target, Time.deltaTime * 0.8f);
                if (_loop != null)
                {
                    _loop.volume = _loopV;
                    if (_loopV > 0.001f && !_loop.isPlaying && waterLoop != null)
                        _loop.Play();
                    else if (_loopV <= 0.001f && _loop.isPlaying)
                        _loop.Stop();
                }
            }

            if (_holo != null && _holo.gameObject.activeSelf)
            {
                var cam = Camera.main != null ? Camera.main.transform : null;
                if (cam != null)
                {
                    var f = cam.forward;
                    f.y = 0f;
                    if (f.sqrMagnitude > 1e-4f)
                    {
                        f.Normalize();
                        var pos = cam.position + f * 1.0f + Vector3.up * 0.02f;
                        _holo.position = Vector3.Lerp(_holo.position, pos, 1f - Mathf.Exp(-6f * Time.deltaTime));
                        _holo.rotation = Quaternion.LookRotation(f, Vector3.up);
                    }
                }
            }
        }

        public void ShowStandMark(bool on)
        {
            if (_standHere != null)
                _standHere.gameObject.SetActive(on);
        }

        public void SetInteractable(bool on)
        {
            if (_poke != null)
                _poke.SetInteractable(on && !Running);
            if (_pushGlow != null)
                _pushGlow.gameObject.SetActive(on && !Running);
        }

        public void Push()
        {
            if (Running)
                return;
            Running = true;
            SetInteractable(false);
            if (_shot != null && valveClip != null)
                _shot.PlayOneShot(valveClip, 0.9f);
            PresentableFx.Haptic(true, 0.7f, 0.12f);
            StartCoroutine(PlateRoutine());
            Pushed?.Invoke();
        }

        private IEnumerator PlateRoutine()
        {
            if (_plate != null)
            {
                var t = 0f;
                while (t < 0.18f)
                {
                    t += Time.deltaTime;
                    _plate.localPosition = _plateHome + Vector3.back * 0.0f + new Vector3(0f, 0f, 0.012f) * Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI);
                    yield return null;
                }

                _plate.localPosition = _plateHome;
            }
        }

        /// <summary>Water on: all the jets, the loop; then the shield is washed over <c>washSeconds</c>; the text shows halfway.</summary>
        public IEnumerator RunWash()
        {
            yield return PresentableFx.Wait(0.25f);
            if (_water != null)
                foreach (var p in _water)
                    if (p != null)
                        p.Play();
            _loopOn = true;
            if (_holo != null)
            {
                _holo.gameObject.SetActive(true);
                var cam = Camera.main != null ? Camera.main.transform : null;
                if (cam != null)
                {
                    var f = cam.forward;
                    f.y = 0f;
                    f.Normalize();
                    _holo.position = cam.position + f * 1.0f + Vector3.up * 0.02f;
                    _holo.rotation = Quaternion.LookRotation(f, Vector3.up);
                }
            }

            if (visor != null)
                yield return visor.Wash(washSeconds);
            else
                yield return PresentableFx.Wait(washSeconds);
            WashDone = true;
            Washed?.Invoke();
        }

        /// <summary>Water off (the text stays until <see cref="ResetAll"/> or <see cref="HideHolo"/>).</summary>
        public void StopWater()
        {
            if (_water != null)
                foreach (var p in _water)
                    if (p != null)
                        p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _loopOn = false;
        }

        public void HideHolo()
        {
            if (_holo != null)
                _holo.gameObject.SetActive(false);
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            Running = false;
            WashDone = false;
            if (_water != null)
                foreach (var p in _water)
                    if (p != null)
                        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _loopOn = false;
            _loopV = 0f;
            if (_loop != null)
            {
                _loop.volume = 0f;
                _loop.Stop();
            }

            if (_holo != null)
                _holo.gameObject.SetActive(false);
            if (_pushGlow != null)
                _pushGlow.gameObject.SetActive(false);
            if (_standHere != null)
                _standHere.gameObject.SetActive(false);
            if (_poke != null)
                _poke.SetInteractable(false);
            if (_plate != null)
                _plate.localPosition = _plateHome;
        }
    }
}
