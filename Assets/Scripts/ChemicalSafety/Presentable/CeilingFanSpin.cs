using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>Slow constant spin for the warehouse ceiling fans.</summary>
    public sealed class CeilingFanSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 80f;

        private void Start()
        {
            transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        private void Update()
        {
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
