using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>Turns a ceiling fan of the Scenario 22 hall around the world Y axis.</summary>
    public sealed class Sc22Spin : MonoBehaviour
    {
        [SerializeField] private float degPerSecond = 90f;

        private void Update()
        {
            transform.Rotate(0f, degPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
