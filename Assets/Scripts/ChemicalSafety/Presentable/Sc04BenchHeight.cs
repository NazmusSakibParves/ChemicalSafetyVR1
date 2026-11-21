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

        private const float MaxSeatedDrop = 0.10f;

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

        /// <summary>
        /// Scenario 4, once at the start while the screen is black: a SEATED head gets a slightly lower table (head height minus <c>belowHead</c>, never
        /// below <c>minTop</c>); a standing head (or the editor camera) keeps the built height. It never raises the table and never follows the head
        /// afterwards, so nothing drifts in the headset. Works with <c>fixedHeight</c> on.
        /// </summary>
        public void FitToSeat(float headY)
        {
            Ensure();
            // at most 10 cm lower than built: the lower shelf and rails of the table stay clear of the floor and the table keeps its proportions
            var lowest = Mathf.Max(minTop, builtTopY - MaxSeatedDrop);
            var target = Mathf.Min(builtTopY, Mathf.Max(lowest, headY - belowHead));
            if (target >= _top - 0.01f)
                return;
            Apply(target);
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
