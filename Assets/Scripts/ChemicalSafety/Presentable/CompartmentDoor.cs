using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    public enum DoorState
    {
        Neutral,
        Correct,
        Wrong
    }

    /// <summary>
    /// One glass compartment door. A finger poke raises <see cref="Poked"/> (and swings the door when
    /// <see cref="toggleOnPoke"/> is on). Placement colours the door red (wrong compartment) or green (right one).
    /// </summary>
    public sealed class CompartmentDoor : MonoBehaviour
    {
        private static readonly Color Glass = new Color(0.72f, 0.88f, 1f, 0.28f);
        private static readonly Color GlassGreen = new Color(0.15f, 1f, 0.45f, 0.42f);
        private static readonly Color GlassRed = new Color(1f, 0.12f, 0.08f, 0.5f);
        private static readonly Color FrameGreen = new Color(0.3f, 1f, 0.5f);
        private static readonly Color FrameRed = new Color(1f, 0.25f, 0.2f);

        [SerializeField] private float openAngle = 105f;
        [SerializeField] private string compartmentName = "";
        public bool toggleOnPoke = true;

        private Quaternion _closed;
        private bool _open;
        private bool _busy;
        private Renderer _glass;
        private Light _stateLight;
        private readonly List<Renderer> _frames = new List<Renderer>();
        private readonly List<Transform> _puffs = new List<Transform>();
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _puffBlock;
        private MaterialPropertyBlock _frameBlock;
        private Coroutine _stateRoutine;

        public event Action<CompartmentDoor> Poked;

        public bool IsOpen => _open;
        public bool IsBusy => _busy;
        public string CompartmentName => compartmentName;
        public DoorState State { get; private set; }

        /// <summary>Centre of the door in world space (used for banners).</summary>
        public Vector3 Center
        {
            get
            {
                var r = _glass != null ? _glass.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
                return r.center;
            }
        }

        private void Awake()
        {
            _closed = transform.localRotation;
            var glass = transform.Find("Glass");
            _glass = glass != null ? glass.GetComponent<Renderer>() : null;
            _block = new MaterialPropertyBlock();
            _puffBlock = new MaterialPropertyBlock();
            _frameBlock = new MaterialPropertyBlock();
            _stateLight = GetComponentInChildren<Light>(true);
            if (_stateLight != null)
                _stateLight.enabled = false;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Puff"))
                    _puffs.Add(t);
                else if (t.name == "Frame")
                {
                    var r = t.GetComponent<Renderer>();
                    if (r != null)
                        _frames.Add(r);
                }
            }

            var poke = GetComponent<PokeTarget>();
            if (poke != null)
                poke.Poked += _ =>
                {
                    Poked?.Invoke(this);
                    if (toggleOnPoke && !_busy)
                        StartCoroutine(Swing(!_open));
                };
        }

        public void SetInteractable(bool value)
        {
            var poke = GetComponent<PokeTarget>();
            if (poke != null)
                poke.SetInteractable(value);
        }

        /// <summary>Swings the door open (soft overshoot, whoosh, haptic).</summary>
        public Coroutine Open() => StartCoroutine(Swing(true));

        public Coroutine Close() => StartCoroutine(Swing(false));

        public IEnumerator OpenRoutine() => Swing(true);

        public IEnumerator CloseRoutine() => Swing(false);

        public void FlashWrong() => SetState(DoorState.Wrong);

        public void FlashCorrect() => SetState(DoorState.Correct);

        /// <summary>Blinks then holds the red / green state, or clears it again.</summary>
        public void SetState(DoorState state)
        {
            State = state;
            if (_stateRoutine != null)
                StopCoroutine(_stateRoutine);
            _stateRoutine = StartCoroutine(StateRoutine(state));
        }

        private IEnumerator Swing(bool open)
        {
            while (_busy)
                yield return null;
            if (_open == open)
                yield break;
            _busy = true;
            var from = transform.localRotation;
            var to = open ? _closed * Quaternion.Euler(0f, openAngle, 0f) : _closed;
            PresentableFx.Play(open ? PresentableFx.Whoosh : PresentableFx.Tick, open ? 0.45f : 0.35f);
            PresentableFx.Haptic(true, 0.35f, 0.05f);
            // opening: ease-out-back (soft overshoot); closing: gentle ease-out
            yield return PresentableFx.Tween(open ? 0.8f : 0.6f, t =>
                transform.localRotation = Quaternion.SlerpUnclamped(from, to, t),
                open ? (Func<float, float>)PresentableFx.EaseOutBack : PresentableFx.EaseOutCubic);
            transform.localRotation = to;
            _open = open;
            _busy = false;
        }

        private IEnumerator StateRoutine(DoorState state)
        {
            var glassColor = state == DoorState.Correct ? GlassGreen : state == DoorState.Wrong ? GlassRed : Glass;
            if (state == DoorState.Wrong)
            {
                PresentableFx.Play(PresentableFx.Error, 0.6f);
                PresentableFx.Haptic(true, 0.8f, 0.2f);
                StartCoroutine(Puff());
            }
            else if (state == DoorState.Correct)
            {
                PresentableFx.Play(PresentableFx.Chime, 0.6f);
                PresentableFx.Haptic(true, 0.4f, 0.08f);
            }

            if (state != DoorState.Neutral)
            {
                for (var i = 0; i < 3; i++)
                {
                    Apply(Glass, DoorState.Neutral);
                    yield return PresentableFx.Wait(0.09f);
                    Apply(glassColor, state);
                    yield return PresentableFx.Wait(0.12f);
                }
            }

            Apply(glassColor, state);
            _stateRoutine = null;
        }

        private void Apply(Color glass, DoorState state)
        {
            if (_glass != null)
            {
                _block.SetColor("_BaseColor", glass);
                _glass.SetPropertyBlock(_block);
            }

            foreach (var f in _frames)
            {
                if (f == null)
                    continue;
                if (state == DoorState.Neutral)
                {
                    f.SetPropertyBlock(null);
                }
                else
                {
                    _frameBlock.SetColor("_BaseColor", state == DoorState.Correct ? FrameGreen : FrameRed);
                    f.SetPropertyBlock(_frameBlock);
                }
            }

            if (_stateLight != null)
            {
                _stateLight.enabled = state != DoorState.Neutral;
                _stateLight.color = state == DoorState.Correct ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.2f, 0.15f);
            }
        }

        private IEnumerator Puff()
        {
            foreach (var p in _puffs)
            {
                if (p == null)
                    continue;
                p.gameObject.SetActive(true);
                p.localScale = Vector3.one * 0.05f;
            }

            yield return PresentableFx.Tween(0.9f, t =>
            {
                foreach (var p in _puffs)
                {
                    if (p == null)
                        continue;
                    p.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.55f, t);
                    var r = p.GetComponent<Renderer>();
                    if (r == null)
                        continue;
                    _puffBlock.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.88f, (1f - t) * 0.45f));
                    r.SetPropertyBlock(_puffBlock);
                }
            });

            foreach (var p in _puffs)
            {
                if (p != null)
                    p.gameObject.SetActive(false);
            }
        }
    }
}
