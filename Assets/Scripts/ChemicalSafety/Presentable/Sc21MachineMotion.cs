using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Makes a machine of the Scenario 21 factory hall (seen through the doorway) look like it is running: a roller or drum spins, a body trembles a little,
    /// a lamp blinks, a box travels along the conveyor and starts again. Plain transform animation (no clips), so it also runs on the Quest.
    /// </summary>
    public sealed class Sc21MachineMotion : MonoBehaviour
    {
        public enum Kind { Spin, Vibrate, Blink, Travel }

        [SerializeField] private Kind kind;
        [SerializeField] private Vector3 axis = Vector3.up;      // Spin: local axis; Travel: world direction of the belt
        [SerializeField] private float speed = 120f;             // Spin: degrees per second; Travel: metres per second; Blink: Hz
        [SerializeField] private float amplitude = 0.004f;       // Vibrate
        [SerializeField] private float length = 8f;              // Travel: belt length
        [SerializeField] private float phase;

        private Vector3 _start;
        private Quaternion _rot;
        private Renderer _renderer;
        private float _t;

        public void Setup(Kind k, Vector3 ax, float spd, float amp, float len, float ph)
        {
            kind = k;
            axis = ax;
            speed = spd;
            amplitude = amp;
            length = len;
            phase = ph;
        }

        private void Awake()
        {
            _start = transform.position;
            _rot = transform.rotation;
            _renderer = GetComponent<Renderer>();
            _t = phase;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            switch (kind)
            {
                case Kind.Spin:
                    transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
                    break;
                case Kind.Vibrate:
                    transform.position = _start + new Vector3(Mathf.Sin(_t * 47f), Mathf.Sin(_t * 61f + 1f) * 0.6f, Mathf.Sin(_t * 53f + 2f)) * amplitude;
                    break;
                case Kind.Blink:
                    if (_renderer != null)
                        _renderer.enabled = Mathf.Sin(_t * speed * Mathf.PI * 2f) > -0.2f;
                    break;
                case Kind.Travel:
                    var d = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
                    transform.position = _start + d * Mathf.Repeat(_t * speed, length);
                    break;
            }
        }
    }
}
