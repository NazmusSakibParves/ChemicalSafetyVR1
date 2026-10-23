using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The glove bench (table, cloth, gloves, SDS, tubes) sits a comfortable look-down below the
    /// eyes, so the same table works seated or standing. Small head bobs stay inside the dead zone.
    /// </summary>
    public sealed class Sc04BenchHeight : MonoBehaviour
    {
        [SerializeField] private float builtTopY = 0.9f;
        [SerializeField] private float belowHead = 0.32f;
        [SerializeField] private float minTop = 0.55f;
        [SerializeField] private float maxTop = 1.45f;
        [SerializeField] private float deadZone = 0.08f;

        private float _top;
        private bool _init;

        /// <summary>Places the tabletop immediately for a head at <paramref name="headY"/>.</summary>
        public void SnapTo(float headY)
        {
            Ensure();
            Apply(Mathf.Clamp(headY - belowHead, minTop, maxTop));
        }

        /// <summary>Eases toward a sit or stand change. Ignores shifts smaller than the dead zone.</summary>
        public void Follow(float headY)
        {
            if (!_init)
            {
                SnapTo(headY);
                return;
            }

            var target = Mathf.Clamp(headY - belowHead, minTop, maxTop);
            if (Mathf.Abs(target - _top) < deadZone)
                return;
            Apply(Mathf.MoveTowards(_top, target, Time.deltaTime * 0.45f));
        }

        private void Ensure()
        {
            if (_init)
                return;
            _top = builtTopY;
            _init = true;
        }

        private void Apply(float top)
        {
            transform.position += Vector3.up * (top - _top);
            _top = top;
        }
    }
}
