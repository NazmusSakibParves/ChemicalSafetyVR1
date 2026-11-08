using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>Scenario 6: a small repeating up-and-down motion (the sewing machine's needle bar).</summary>
    public sealed class Sc06Bobber : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.01f;
        [SerializeField] private float hertz = 3f;
        [SerializeField] private Vector3 axis = Vector3.up;

        private Vector3 _home;

        private void Awake()
        {
            _home = transform.localPosition;
        }

        private void Update()
        {
            transform.localPosition = _home + axis * (Mathf.Sin(Time.time * hertz * 6.2832f) * amplitude);
        }
    }
}
