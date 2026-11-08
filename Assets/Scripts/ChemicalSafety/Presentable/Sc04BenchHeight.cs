using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The glove bench (table, cloth, gloves, SDS, tubes) sits a comfortable look-down below the
    /// eyes, so the same table works seated or standing. Small head bobs stay inside the dead zone.
    /// With <c>fixedHeight</c> (the Quest-comfort setting) the bench never moves: a table that rises and sinks with the head makes
    /// the whole world drift and makes people dizzy, so it keeps its built height (the learner places the head, not the table).
    /// </summary>
    public sealed class Sc04BenchHeight : MonoBehaviour
    {
        [SerializeField] private float builtTopY = 0.9f;
        [SerializeField] private float belowHead = 0.32f;
        [SerializeField] private float minTop = 0.55f;
        [SerializeField] private float maxTop = 1.45f;
        [SerializeField] private float deadZone = 0.08f;
        [Tooltip("true: SnapTo and Follow do nothing, the table keeps its built height (no drifting world in the headset).")]
        [SerializeField] private bool fixedHeight;

        private float _top;
        private bool _init;

        /// <summary>Current height of the table top (metres above the floor).</summary>
        public float TopY
        {
            get
            {
                Ensure();
                return _top;
            }
        }

        /// <summary>Places the tabletop immediately for a head at <paramref name="headY"/>.</summary>
        public void SnapTo(float headY)
        {
            Ensure();
            if (fixedHeight)
                return;
            Apply(Mathf.Clamp(headY - belowHead, minTop, maxTop));
        }

        /// <summary>Eases toward a sit or stand change. Ignores shifts smaller than the dead zone.</summary>
        public void Follow(float headY)
        {
            if (fixedHeight)
            {
                Ensure();
                return;
            }

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
