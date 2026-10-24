using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6: the Volatile Solvent container. A finger poke lifts it into the left hand (the glove turns palm up under it, the
    /// fingers curl round it; at the desk it floats in front of the camera). Its label faces the user. The pour (step 4) moves it
    /// from there.
    /// </summary>
    public sealed class Sc06Jug : MonoBehaviour
    {
        private Transform _jug;
        private PokeTarget _poke;
        private Transform _glow;
        private Transform _cap;
        private PokeTarget _capPoke;
        private Transform _capGlow;
        private bool _capBusy;
        private Vector3 _capPos;
        private Quaternion _capRot;
        private Transform _capParent;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private Vector3 _holdPos;
        private Vector3 _holdVel;
        private bool _held;
        private bool _busy;
        private bool _follow;

        public const float Height = 0.30f;

        /// <summary>Smoke test / editor: use the hand even without a headset.</summary>
        public bool forceHand;

        public bool Held => _held;
        public bool Busy => _busy;
        public Transform JugTransform => _jug;
        public PokeTarget Poke => _poke;
        public Vector3 HomePosition => _homePos;

        /// <summary>Raised when the container is in the hand.</summary>
        public event Action Picked;

        private void Awake()
        {
            var root = transform.parent != null ? transform.parent : transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "SolventJug")
                    _jug = t;
                else if (t.name == "JugPoke")
                    _poke = t.GetComponent<PokeTarget>();
                else if (t.name == "JugGlow")
                    _glow = t;
                else if (t.name == "JugCap")
                    _cap = t;
                else if (t.name == "CapPoke")
                    _capPoke = t.GetComponent<PokeTarget>();
                else if (t.name == "CapGlow")
                    _capGlow = t;
            }

            if (_jug != null)
            {
                _homePos = _jug.position;
                _homeRot = _jug.rotation;
            }

            if (_cap != null)
            {
                _capPos = _cap.position;
                _capRot = _cap.rotation;
                _capParent = _cap.parent;
            }
        }

        private void Start()
        {
            if (_poke != null)
            {
                _poke.Poked += _ => Pick();
                _poke.SetInteractable(false);
            }

            if (_capPoke != null)
            {
                _capPoke.Poked += _ => CloseFromTable();
                _capPoke.SetInteractable(false);
            }

            if (_capGlow != null)
                _capGlow.gameObject.SetActive(false);

            if (_glow != null)
                _glow.gameObject.SetActive(false);
        }

        public void SetInteractable(bool on)
        {
            if (_poke != null)
                _poke.SetInteractable(on && !_held);
            if (_glow != null)
                _glow.gameObject.SetActive(on && !_held);
        }

        public void Pick()
        {
            if (_held || _busy || _jug == null)
                return;
            StartCoroutine(PickRoutine());
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.001f ? Vector3.forward : v.normalized;
        }

        // Where the container's centre sits in the hand.
        public Vector3 HeldCenter()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return _homePos + Vector3.up * (Height * 0.5f);
            var fwd = Flat(cam.forward);
            var right = Vector3.Cross(Vector3.up, fwd);
            var hand = GlovedHandPose.Left;
            if ((XRSettings.isDeviceActive || forceHand) && hand != null && hand.gameObject.activeInHierarchy)
            {
                if (hand.TryGetPalmFrame(out var pc, out _, out var pf))
                {
                    pf.y = 0f;
                    pf = pf.sqrMagnitude > 0.001f ? pf.normalized : fwd;
                    return pc + Vector3.up * (Height * 0.5f + 0.03f) + pf * 0.02f;
                }

                var palm = hand.Palm != null ? hand.Palm.position : hand.transform.position;
                return palm + Vector3.up * (Height * 0.32f) + fwd * 0.025f;
            }

            return cam.position + fwd * 0.62f + right * -0.2f + Vector3.up * -0.1f;
        }

        private Quaternion HeldRotation(Vector3 centre)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return _homeRot;
            var to = cam.position - centre;
            to.y = 0f;
            return to.sqrMagnitude < 0.001f ? _homeRot : Quaternion.LookRotation(to.normalized, Vector3.up); // label (+Z) toward the user
        }

        private IEnumerator PickRoutine()
        {
            _busy = true;
            if (_poke != null)
                _poke.SetInteractable(false);
            if (_glow != null)
                _glow.gameObject.SetActive(false);
            PresentableFx.Play(PresentableFx.Whoosh, 0.35f);
            GlovedHandPose.Left?.SetCradle(true);
            GlovedHandPose.Left?.SetGrasp(0.5f);
            var from = _jug.position;
            var fromRot = _jug.rotation;
            const float dur = 0.55f;
            for (var t = 0f; t < dur; t += Time.deltaTime)
            {
                var c = HeldCenter();
                var k = Mathf.SmoothStep(0f, 1f, t / dur);
                var arc = Mathf.Sin(k * Mathf.PI) * 0.08f;
                var dest = c - Vector3.up * (Height * 0.5f);
                _jug.position = Vector3.Lerp(from, dest, k) + Vector3.up * arc;
                _jug.rotation = Quaternion.Slerp(fromRot, HeldRotation(c), k);
                yield return null;
            }

            _holdPos = HeldCenter();
            _holdVel = Vector3.zero;
            _held = true;
            _follow = true;
            _busy = false;
            Picked?.Invoke();
        }

        /// <summary>Stops following the hand (the pour moves the container itself).</summary>
        public void SetFollow(bool on)
        {
            _follow = on;
            if (on)
            {
                _holdPos = HeldCenter();
                _holdVel = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (!_held || _busy || !_follow || _jug == null)
                return;
            _holdPos = Vector3.SmoothDamp(_holdPos, HeldCenter(), ref _holdVel, 0.07f);
            _jug.position = _holdPos - Vector3.up * (Height * 0.5f);
            _jug.rotation = Quaternion.Slerp(_jug.rotation, HeldRotation(_holdPos), 1f - Mathf.Exp(-Time.deltaTime * 8f));
        }

        public bool CapIsOn { get; private set; }
        public PokeTarget CapPoke => _capPoke;
        public Transform CapTransform => _cap;

        /// <summary>Raised when the cap has been screwed on at the table (the container is closed).</summary>
        public event Action CapClosed;

        /// <summary>The loose cap can be touched (arrow and glow on it) until the container is closed.</summary>
        public void SetCapInteractable(bool on)
        {
            if (_capPoke != null)
                _capPoke.SetInteractable(on && !CapIsOn && !_capBusy);
            if (_capGlow != null)
                _capGlow.gameObject.SetActive(on && !CapIsOn && !_capBusy);
        }

        private void CloseFromTable()
        {
            if (CapIsOn || _capBusy || _cap == null)
                return;
            SetCapInteractable(false);
            StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            yield return CapOn();
            CapClosed?.Invoke();
        }

        private Vector3 NeckWorld() => _jug.TransformPoint(new Vector3(-0.0704f, 0.2641f, 0f));

        /// <summary>The cap lifts over the neck and screws on (about one and a half turns), then the container is closed.</summary>
        public IEnumerator CapOn()
        {
            if (_cap == null || _jug == null)
                yield break;
            _capBusy = true;
            if (_capGlow != null)
                _capGlow.gameObject.SetActive(false);
            _cap.SetParent(_capParent, true);
            var from = _cap.position;
            var fromRot = _cap.rotation;
            // 1) up and over the neck
            const float flyT = 0.55f;
            for (var t = 0f; t < flyT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / flyT);
                var dest = NeckWorld() + Vector3.up * 0.07f;
                _cap.position = Vector3.Lerp(from, dest, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.08f);
                _cap.rotation = Quaternion.Slerp(fromRot, _jug.rotation, k);
                yield return null;
            }

            // 2) screw down while spinning
            const float screwT = 0.75f;
            PresentableFx.Play(PresentableFx.Tick, 0.5f);
            for (var t = 0f; t < screwT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / screwT);
                _cap.position = NeckWorld() + Vector3.up * (0.07f * (1f - k));
                _cap.rotation = _jug.rotation * Quaternion.Euler(0f, (1f - k) * 540f, 0f);
                yield return null;
            }

            _cap.SetParent(_jug, true);
            _cap.position = NeckWorld();
            _cap.rotation = _jug.rotation;
            CapIsOn = true;
            _capBusy = false;
            PresentableFx.Play(PresentableFx.Tick, 0.9f);
            PresentableFx.Haptic(false, 0.35f, 0.06f);
        }

        /// <summary>The cap unscrews and goes to 'restWorld' (lying on its side) so the container can be poured.</summary>
        public IEnumerator CapOff(Vector3 restWorld)
        {
            if (_cap == null || _jug == null || !CapIsOn)
                yield break;
            _capBusy = true;
            _cap.SetParent(_capParent, true);
            var start = _cap.position;
            // 1) unscrew upwards
            const float upT = 0.7f;
            for (var t = 0f; t < upT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / upT);
                _cap.position = NeckWorld() + Vector3.up * (0.07f * k);
                _cap.rotation = _jug.rotation * Quaternion.Euler(0f, -k * 540f, 0f);
                yield return null;
            }

            // 2) lay it down on the hood surface
            var from = _cap.position;
            var fromRot = _cap.rotation;
            var restRot = Quaternion.Euler(0f, 20f, 90f);
            const float layT = 0.6f;
            for (var t = 0f; t < layT; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / layT);
                _cap.position = Vector3.Lerp(from, restWorld, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.05f);
                _cap.rotation = Quaternion.Slerp(fromRot, restRot, k);
                yield return null;
            }

            _cap.position = restWorld;
            _cap.rotation = restRot;
            CapIsOn = false;
            _capBusy = false;
            PresentableFx.Play(PresentableFx.Tick, 0.7f);
        }

        /// <summary>Puts the container back on the table (reset).</summary>
        public void ReturnHome()
        {
            StopAllCoroutines();
            _busy = false;
            _held = false;
            _follow = false;
            GlovedHandPose.Left?.SetGrasp(0f);
            GlovedHandPose.Left?.SetCradle(false);
            if (_jug != null)
            {
                _jug.position = _homePos;
                _jug.rotation = _homeRot;
            }

            if (_cap != null)
            {
                _cap.SetParent(_capParent, true);
                _cap.SetPositionAndRotation(_capPos, _capRot);
            }

            CapIsOn = false;
            _capBusy = false;
            if (_capPoke != null)
                _capPoke.SetInteractable(false);
            if (_capGlow != null)
                _capGlow.gameObject.SetActive(false);
            if (_poke != null)
                _poke.SetInteractable(false);
            if (_glow != null)
                _glow.gameObject.SetActive(false);
        }
    }
}
