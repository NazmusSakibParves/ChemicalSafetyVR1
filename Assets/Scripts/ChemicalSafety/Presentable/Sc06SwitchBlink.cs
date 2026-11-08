using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6: a switch that is ON keeps a soft green pulse (the lamp breathes and its glow swells) so that it is clear at a glance
    /// that the fume hood / exhaust fan is running. Index 0 follows the fume hood switch, index 1 the exhaust fan switch.
    /// </summary>
    public sealed class Sc06SwitchBlink : MonoBehaviour
    {
        [SerializeField] private Sc06Lab lab;
        [SerializeField] private Renderer[] leds = new Renderer[2];
        [SerializeField] private Transform[] glows = new Transform[2];
        [SerializeField] private float pulsesPerSecond = 1.1f;

        private readonly Vector3[] _glowScale = new Vector3[2];
        private MaterialPropertyBlock _block;
        private bool _captured;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Capture()
        {
            if (_captured)
                return;
            _block = new MaterialPropertyBlock();
            for (var i = 0; i < 2; i++)
                if (glows[i] != null)
                    _glowScale[i] = glows[i].localScale;
            _captured = true;
        }

        private void Update()
        {
            Capture();
            for (var i = 0; i < 2; i++)
            {
                var on = lab != null && (i == 0 ? lab.HoodOn : lab.LevOn);
                var pulse = on ? 0.5f + 0.5f * Mathf.Sin(Time.time * pulsesPerSecond * 6.2832f) : 0f;
                if (glows[i] != null)
                    glows[i].localScale = _glowScale[i] * (on ? Mathf.Lerp(0.85f, 1.35f, pulse) : 1f);
                if (leds[i] != null)
                {
                    if (on)
                    {
                        _block.SetColor(EmissionColor, new Color(0.3f, 5f, 0.7f) * Mathf.Lerp(0.55f, 1.5f, pulse));
                        leds[i].SetPropertyBlock(_block);
                    }
                    else
                    {
                        leds[i].SetPropertyBlock(null);
                    }
                }
            }
        }
    }
}
