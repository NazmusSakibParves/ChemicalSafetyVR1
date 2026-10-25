using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 fire alarm beacon: while on, the lamp pulses red (smoothly, never a hard strobe), a red wash light throws a
    /// soft glow on the wall and a halo billboard breathes around it. Off = dark glass.
    /// </summary>
    public sealed class Sc07Beacon : MonoBehaviour
    {
        [SerializeField] private Renderer lamp;
        [SerializeField] private Light washLight;
        [SerializeField] private Renderer halo;
        [SerializeField] private float lightMax = 2.2f;
        [SerializeField] private float rate = 5.2f;

        private bool _on;
        private float _level;
        private MaterialPropertyBlock _pb;
        private MaterialPropertyBlock _pbHalo;

        public bool IsOn => _on;
        public float Level => _level;

        public void SetOn(bool on) => _on = on;

        private void Awake()
        {
            _pb = new MaterialPropertyBlock();
            _pbHalo = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void Update()
        {
            _level = Mathf.MoveTowards(_level, _on ? 1f : 0f, Time.deltaTime * 2.5f);
            var beat = 0.5f + 0.5f * Mathf.Sin(Time.time * rate);
            Apply(_level * (0.35f + 0.65f * beat));
        }

        private void Apply(float k)
        {
            if (washLight != null)
            {
                washLight.enabled = k > 0.01f;
                washLight.intensity = lightMax * k;
            }

            if (lamp != null)
            {
                lamp.GetPropertyBlock(_pb);
                _pb.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.05f) * (0.3f + 3.2f * k));
                lamp.SetPropertyBlock(_pb);
            }

            if (halo != null)
            {
                halo.enabled = k > 0.01f;
                halo.GetPropertyBlock(_pbHalo);
                _pbHalo.SetColor("_BaseColor", new Color(1f, 0.12f, 0.08f, 0.75f * k));
                halo.SetPropertyBlock(_pbHalo);
            }
        }
    }
}
