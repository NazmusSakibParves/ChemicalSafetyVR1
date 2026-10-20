using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Darkens the shared scene lighting while the SC03 room is active (the warehouse and SC02 room are bright)
    /// and restores it when the room is switched off.
    /// </summary>
    public sealed class Sc03Mood : MonoBehaviour
    {
        [SerializeField] private Color ambientSky = new Color(0.2f, 0.2f, 0.21f);
        [SerializeField] private Color ambientEquator = new Color(0.16f, 0.15f, 0.15f);
        [SerializeField] private Color ambientGround = new Color(0.07f, 0.07f, 0.07f);
        [SerializeField] private Color fogColor = new Color(0.05f, 0.05f, 0.055f);
        [SerializeField] private float fogStart = 5f;
        [SerializeField] private float fogEnd = 16f;
        [SerializeField] private float sunIntensity = 0.1f;

        private AmbientMode _mode;
        private Color _sky;
        private Color _equator;
        private Color _ground;
        private Color _fog;
        private float _start;
        private float _end;
        private Light _sun;
        private float _sunIntensity;
        private bool _applied;

        private void OnEnable()
        {
            _mode = RenderSettings.ambientMode;
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fogColor;
            _start = RenderSettings.fogStartDistance;
            _end = RenderSettings.fogEndDistance;
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    _sun = l;
                    _sunIntensity = l.intensity;
                    l.intensity = sunIntensity;
                    break;
                }
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            _applied = true;
        }

        private void OnDisable()
        {
            if (!_applied)
                return;
            _applied = false;
            RenderSettings.ambientMode = _mode;
            RenderSettings.ambientSkyColor = _sky;
            RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground;
            RenderSettings.fogColor = _fog;
            RenderSettings.fogStartDistance = _start;
            RenderSettings.fogEndDistance = _end;
            if (_sun != null)
                _sun.intensity = _sunIntensity;
        }
    }
}
