using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Pokeable prop. Fires <see cref="Poked"/> when the gloved right fingertip enters its collider
    /// (curls the finger + short haptic), or on a left mouse click at the desk. No ray involved.
    /// </summary>
    public sealed class PokeTarget : MonoBehaviour
    {
        [SerializeField] private float reach = 0.04f;

        private Collider[] _cols;
        private bool _wasInside;

        public event Action<PokeTarget> Poked;

        public bool Interactable { get; set; }

        private void Awake()
        {
            _cols = GetComponentsInChildren<Collider>(true);
        }

        private void Update()
        {
            if (!Interactable || _cols == null)
                return;

            var hand = GlovedHandPose.Right;
            var tip = hand != null ? hand.PokePoint : null;
            if (tip != null)
            {
                var inside = false;
                foreach (var c in _cols)
                {
                    if (c == null || !c.enabled)
                        continue;
                    var p = c.ClosestPoint(tip.position);
                    if ((p - tip.position).sqrMagnitude <= reach * reach)
                    {
                        inside = true;
                        break;
                    }
                }

                if (inside && !_wasInside)
                    Fire(true);
                _wasInside = inside;
            }

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (!XRSettings.isDeviceActive && mouse != null && cam != null && mouse.leftButton.wasPressedThisFrame)
            {
                var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                var hits = Physics.RaycastAll(ray, 30f, ~0, QueryTriggerInteraction.Collide);
                foreach (var h in hits)
                {
                    if (Array.IndexOf(_cols, h.collider) >= 0)
                    {
                        Fire(false);
                        break;
                    }
                }
            }
        }

        private void Fire(bool vr)
        {
            if (vr)
            {
                var hand = GlovedHandPose.Right;
                if (hand != null)
                    hand.PlayPokeCurl();
                PresentableFx.Haptic(true, 0.55f, 0.07f);
            }

            PresentableFx.Play(PresentableFx.Tick, 0.7f);
            Poked?.Invoke(this);
        }

        public void SetInteractable(bool value)
        {
            Interactable = value;
            _wasInside = false;
        }
    }
}
