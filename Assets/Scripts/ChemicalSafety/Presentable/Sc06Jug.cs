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

        /// <summary>The loose red cap flies onto the neck (close the container immediately after use).</summary>
        public IEnumerator CapOn()
        {
            if (_cap == null || _jug == null)
                yield break;
            var from = _cap.position;
            var fromRot = _cap.rotation;
            const float dur = 0.7f;
            for (var t = 0f; t < dur; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / dur);
                var dest = _jug.TransformPoint(new Vector3(-0.0704f, 0.2641f, 0f));
                _cap.position = Vector3.Lerp(from, dest, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.1f);
                _cap.rotation = Quaternion.Slerp(fromRot, _jug.rotation, k);
                yield return null;
            }

            _cap.SetParent(_jug, true);
            _cap.position = _jug.TransformPoint(new Vector3(-0.0704f, 0.2641f, 0f));
            _cap.rotation = _jug.rotation;
            CapIsOn = true;
            PresentableFx.Play(PresentableFx.Tick, 0.8f);
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
            if (_poke != null)
                _poke.SetInteractable(false);
            if (_glow != null)
                _glow.gameObject.SetActive(false);
        }
    }
}
