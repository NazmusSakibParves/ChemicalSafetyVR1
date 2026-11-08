using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6: the big wall exhaust fan. The rotor turns while the EXHAUST FAN switch (the lab's second switch) is on: it eases up to
    /// speed and eases down again, with a slight wobble, and a soft light behind it breathes with the speed.
    /// </summary>
    public sealed class Sc06ExhaustFan : MonoBehaviour
    {
        [SerializeField] private Sc06Lab lab;
        [SerializeField] private Transform rotor;
        [SerializeField] private Vector3 axis = Vector3.forward;
        [SerializeField] private float maxDegreesPerSecond = 640f;
        [SerializeField] private float spinUpSeconds = 2.2f;
        [SerializeField] private Light glow;
        [SerializeField] private float glowOff = 0.05f;
        [SerializeField] private float glowOn = 0.35f;

        private float _speed01;

        public float Speed01 => _speed01;

        /// <summary>Back to standing still (the scenario restarts).</summary>
        public void ResetFan()
        {
            _speed01 = 0f;
        }

        private void Update()
        {
            var on = lab != null && lab.LevOn;
            _speed01 = Mathf.MoveTowards(_speed01, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.1f, spinUpSeconds));
            if (rotor != null && _speed01 > 0.001f)
                rotor.Rotate(axis, maxDegreesPerSecond * _speed01 * _speed01 * Time.deltaTime, Space.Self);
            if (glow != null)
                glow.intensity = Mathf.Lerp(glowOff, glowOn, _speed01);
        }
    }
}
