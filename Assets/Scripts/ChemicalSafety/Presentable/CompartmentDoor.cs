using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// One glass compartment door. A finger poke swings it open or shut. FlashWrong / FlashCorrect
    /// are ready for the container-placement step (red blink + smoke, or a green blink).
    /// </summary>
    public sealed class CompartmentDoor : MonoBehaviour
    {
        private static readonly Color Glass = new Color(0.72f, 0.88f, 1f, 0.28f);

        [SerializeField] private float openAngle = 105f;

        private Quaternion _closed;
        private bool _open;
        private bool _busy;
        private Renderer _glass;
        private readonly List<Transform> _puffs = new List<Transform>();
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _puffBlock;

        public bool IsOpen => _open;

        private void Awake()
        {
            _closed = transform.localRotation;
            var glass = transform.Find("Glass");
            _glass = glass != null ? glass.GetComponent<Renderer>() : null;
            _block = new MaterialPropertyBlock();
            _puffBlock = new MaterialPropertyBlock();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Puff"))
                    _puffs.Add(t);
            }

            var poke = GetComponent<PokeTarget>();
            if (poke != null)
                poke.Poked += _ =>
                {
                    if (!_busy)
                        StartCoroutine(Swing(!_open));
                };
        }

        public void SetInteractable(bool value)
        {
            var poke = GetComponent<PokeTarget>();
            if (poke != null)
                poke.SetInteractable(value);
        }

        public void FlashWrong() => StartCoroutine(Blink(new Color(1f, 0.12f, 0.08f, 0.62f), true));

        public void FlashCorrect() => StartCoroutine(Blink(new Color(0.25f, 1f, 0.45f, 0.5f), false));

        private System.Collections.IEnumerator Swing(bool open)
        {
            _busy = true;
            var from = transform.localRotation;
            var to = open ? _closed * Quaternion.Euler(0f, openAngle, 0f) : _closed;
            PresentableFx.Play(PresentableFx.Whoosh, 0.4f);
            PresentableFx.Haptic(true, 0.35f, 0.05f);
            yield return PresentableFx.Tween(0.55f, t =>
                transform.localRotation = Quaternion.Slerp(from, to, t), PresentableFx.EaseOutCubic);
            _open = open;
            _busy = false;
        }

        private System.Collections.IEnumerator Blink(Color tint, bool smoke)
        {
            if (smoke)
            {
                PresentableFx.Play(PresentableFx.Error, 0.6f);
                StartCoroutine(Puff());
            }
            else
            {
                PresentableFx.Play(PresentableFx.Chime, 0.5f);
            }

            for (var i = 0; i < 3; i++)
            {
                SetGlass(Color.Lerp(Glass, tint, 1f));
                yield return PresentableFx.Wait(0.12f);
                SetGlass(Glass);
                yield return PresentableFx.Wait(0.1f);
            }
        }

        private void SetGlass(Color color)
        {
            if (_glass == null)
                return;
            _block.SetColor("_BaseColor", color);
            _glass.SetPropertyBlock(_block);
        }

        private System.Collections.IEnumerator Puff()
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
