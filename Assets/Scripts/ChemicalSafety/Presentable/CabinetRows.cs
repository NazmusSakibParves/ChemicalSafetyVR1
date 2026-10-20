using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The eye-level board (with the empty places for the containers) and the small top row move together to the
    /// user's eye height, so the places are comfortable to reach seated or standing.
    /// </summary>
    public sealed class CabinetRows : MonoBehaviour
    {
        [Tooltip("Height of the eye-level board as built.")]
        [SerializeField] private float builtEyeY = 1.3f;
        [SerializeField] private float minEyeY = 1.0f;
        [SerializeField] private float maxEyeY = 1.3f;
        [Tooltip("The board sits this far below the eyes (a comfortable reach and look-down).")]
        [SerializeField] private float belowHead = 0.32f;

        private float _currentY;
        private bool _init;

        public float CurrentEyeBoardY => _init ? _currentY : builtEyeY;

        /// <summary>Moves the rows to suit a head at <paramref name="headHeight"/> metres above the floor.</summary>
        public void AdjustToHead(float headHeight)
        {
            if (!_init)
            {
                _currentY = builtEyeY;
                _init = true;
            }

            var target = Mathf.Clamp(headHeight - belowHead, minEyeY, maxEyeY);
            transform.position += Vector3.up * (target - _currentY);
            _currentY = target;
        }
    }
}
