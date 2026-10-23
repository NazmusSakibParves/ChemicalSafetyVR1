using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 5 life: fast ceiling fans, shaking machine panels, swaying cloth, a red detector that pulses and a
    /// couple of tube lights that flicker. Everything is a plain transform / light animation (no per-frame allocations).
    /// Only runs while the room is active.
    /// </summary>
    public sealed class Sc05FactoryFx : MonoBehaviour
    {
        [System.Serializable]
        public struct Spinner
        {
            public Transform target;
            public Vector3 axis;
            public float degreesPerSecond;
        }

        [System.Serializable]
        public struct Shaker
        {
            public Transform target;
            public float amplitude;
            public float frequency;
        }

        [System.Serializable]
        public struct Swayer
        {
            public Transform target;
            public float degrees;
            public float speed;
        }

        [System.Serializable]
        public struct Flicker
        {
            public Light light;
            public Renderer glow;
            public float baseIntensity;
            public float seed;
        }

        [SerializeField] private Spinner[] spinners = new Spinner[0];
        [SerializeField] private Shaker[] shakers = new Shaker[0];
        [SerializeField] private Swayer[] swayers = new Swayer[0];
        [SerializeField] private Flicker[] flickers = new Flicker[0];
        [SerializeField] private Light detectorLight;
        [SerializeField] private Renderer detectorGlow;

        private Vector3[] _shakeHome;
        private Quaternion[] _swayHome;
        private MaterialPropertyBlock _block;

        public int SpinnerCount => spinners.Length;
        public float FastestSpin
        {
            get
            {
                var best = 0f;
                for (var i = 0; i < spinners.Length; i++)
                    best = Mathf.Max(best, Mathf.Abs(spinners[i].degreesPerSecond));
                return best;
            }
        }

        private void OnEnable()
        {
            _block = new MaterialPropertyBlock();
            _shakeHome = new Vector3[shakers.Length];
            for (var i = 0; i < shakers.Length; i++)
                if (shakers[i].target != null)
                    _shakeHome[i] = shakers[i].target.localPosition;
            _swayHome = new Quaternion[swayers.Length];
            for (var i = 0; i < swayers.Length; i++)
                if (swayers[i].target != null)
                    _swayHome[i] = swayers[i].target.localRotation;
        }

        private void OnDisable()
        {
            if (_shakeHome == null)
                return;
            for (var i = 0; i < shakers.Length; i++)
                if (shakers[i].target != null)
                    shakers[i].target.localPosition = _shakeHome[i];
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var t = Time.time;
            for (var i = 0; i < spinners.Length; i++)
            {
                var s = spinners[i];
                if (s.target != null)
                    s.target.Rotate(s.axis, s.degreesPerSecond * dt, Space.Self);
            }

            for (var i = 0; i < shakers.Length; i++)
            {
                var s = shakers[i];
                if (s.target == null)
                    continue;
                var k = t * s.frequency * 6.2832f;
                s.target.localPosition = _shakeHome[i] +
                                         new Vector3(Mathf.Sin(k) * s.amplitude, Mathf.Sin(k * 1.37f + 1f) * s.amplitude * 0.6f, 0f);
            }

            for (var i = 0; i < swayers.Length; i++)
            {
                var s = swayers[i];
                if (s.target == null)
                    continue;
                var a = Mathf.Sin(t * s.speed + i * 1.7f) * s.degrees;
                s.target.localRotation = _swayHome[i] * Quaternion.Euler(0f, 0f, a);
            }

            for (var i = 0; i < flickers.Length; i++)
            {
                var f = flickers[i];
                // mostly steady, sometimes a quick dip, like a tired tube
                var n = Mathf.PerlinNoise(t * 3.1f, f.seed);
                var dip = n > 0.82f ? Mathf.InverseLerp(1f, 0.82f, n) * 0.45f + 0.55f : 1f;
                var v = f.baseIntensity * dip;
                if (f.light != null)
                    f.light.intensity = v;
                if (f.glow != null)
                {
                    f.glow.GetPropertyBlock(_block);
                    _block.SetColor("_EmissionColor", new Color(1f, 1f, 0.95f) * (1.15f * dip));
                    f.glow.SetPropertyBlock(_block);
                }
            }

            // the gas detector breathes red about once a second: the vapor level is high today
            var beat = 0.5f + 0.5f * Mathf.Sin(t * 6.3f);
            if (detectorLight != null)
                detectorLight.intensity = 0.04f + beat * 0.14f;
            if (detectorGlow != null)
            {
                detectorGlow.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f, 0.12f + beat * 0.3f));
                detectorGlow.SetPropertyBlock(_block);
            }
        }
    }
}
