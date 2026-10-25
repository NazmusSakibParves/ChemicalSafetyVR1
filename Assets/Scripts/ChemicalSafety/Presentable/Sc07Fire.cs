using System.Collections;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 fire at the foot of the electrical cabinet: flame flip-book, black smoke, sparks and a flickering light
    /// whose strength follows one intensity value (0 = out, 1 = full). Ignite() lets it catch and grow, Extinguish() makes the
    /// flames shrink and die, the smoke thin out and the glow fade. The CO2 extinguisher jet is a particle cone made at runtime
    /// (StartSpray / StopSpray) on the hero's extinguisher nozzle.
    /// </summary>
    public sealed class Sc07Fire : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] flames = new ParticleSystem[0];
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private Light fireLight;
        [SerializeField] private Light[] washLights = new Light[0];
        [SerializeField] private Material sprayMaterial;
        [SerializeField] private Renderer scorch;
        [SerializeField] private float lightBase = 4.2f;

        private float[] _flameRate;
        private float _smokeRate;
        private float _sparkRate;
        private float _emberRate;
        private float[] _flameSize;
        private float _intensity;
        private Coroutine _fade;
        private ParticleSystem _spray;
        private bool _built;

        public float Intensity => _intensity;
        public bool Burning => _intensity > 0.02f;

        private void Awake()
        {
            Build();
            Apply(0f);
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;
            _flameRate = new float[flames.Length];
            _flameSize = new float[flames.Length];
            for (var i = 0; i < flames.Length; i++)
            {
                _flameRate[i] = flames[i] != null ? flames[i].emission.rateOverTimeMultiplier : 0f;
                _flameSize[i] = flames[i] != null ? flames[i].main.startSizeMultiplier : 1f;
            }

            _smokeRate = smoke != null ? smoke.emission.rateOverTimeMultiplier : 0f;
            _sparkRate = sparks != null ? sparks.emission.rateOverTimeMultiplier : 0f;
            _emberRate = embers != null ? embers.emission.rateOverTimeMultiplier : 0f;
        }

        public void SetIntensity(float value)
        {
            Build();
            Apply(Mathf.Clamp01(value));
        }

        public void Ignite(float seconds)
        {
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(Fade(1f, seconds));
        }

        public void Extinguish(float seconds)
        {
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(Fade(0f, seconds));
        }

        private IEnumerator Fade(float to, float seconds)
        {
            var from = _intensity;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, seconds));
                Apply(Mathf.Lerp(from, to, k));
                yield return null;
            }

            Apply(to);
            _fade = null;
        }

        private static void Rate(ParticleSystem ps, float baseRate, float k)
        {
            if (ps == null)
                return;
            var e = ps.emission;
            e.rateOverTimeMultiplier = baseRate * k;
        }

        private void Apply(float i)
        {
            _intensity = i;
            var on = i > 0.02f;
            for (var f = 0; f < flames.Length; f++)
            {
                var ps = flames[f];
                if (ps == null)
                    continue;
                Rate(ps, _flameRate[f], Mathf.Pow(i, 0.8f));
                var m = ps.main;
                m.startSizeMultiplier = _flameSize[f] * Mathf.Lerp(0.35f, 1f, i);
                SetPlaying(ps, on);
            }

            Rate(smoke, _smokeRate, Mathf.Lerp(0.3f, 1f, i) * (i > 0.02f ? 1f : 0f));
            SetPlaying(smoke, on || (smoke != null && smoke.particleCount > 0 && i > 0f));
            Rate(sparks, _sparkRate, i);
            SetPlaying(sparks, on);
            Rate(embers, _emberRate, i);
            SetPlaying(embers, on);
            if (fireLight != null)
                fireLight.enabled = on;
            foreach (var l in washLights)
                if (l != null)
                    l.enabled = on;
            if (scorch != null)
            {
                scorch.enabled = true;
                var c = scorch.sharedMaterial != null ? scorch.sharedMaterial.GetColor("_BaseColor") : Color.white;
                var pb = new MaterialPropertyBlock();
                scorch.GetPropertyBlock(pb);
                pb.SetColor("_BaseColor", new Color(c.r, c.g, c.b, Mathf.Clamp01(0.15f + i * 0.85f) * c.a));
                scorch.SetPropertyBlock(pb);
            }
        }

        private static void SetPlaying(ParticleSystem ps, bool on)
        {
            if (ps == null)
                return;
            if (on && !ps.isPlaying)
                ps.Play();
            else if (!on && ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            if (fireLight == null || _intensity <= 0.02f)
                return;
            var t = Time.time;
            var flick = 0.78f + 0.3f * Mathf.PerlinNoise(t * 7.3f, 1.7f) + 0.12f * Mathf.Sin(t * 23f);
            fireLight.intensity = lightBase * _intensity * flick;
            fireLight.color = Color.Lerp(new Color(1f, 0.42f, 0.12f), new Color(1f, 0.62f, 0.25f), Mathf.PerlinNoise(t * 3f, 4f));
            fireLight.range = 5f + 3f * _intensity;
        }

        // ------------------------------------------------------------------ CO2 jet

        /// <summary>Starts the CO2 cloud at the nozzle, flying along the nozzle's forward direction.</summary>
        public void StartSpray(Transform nozzle)
        {
            if (nozzle == null)
                return;
            if (_spray == null)
            {
                var go = new GameObject("Co2Jet");
                go.transform.SetParent(nozzle, false);
                go.transform.localPosition = Vector3.zero;
                _spray = go.AddComponent<ParticleSystem>();
                _spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _spray.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                main.startColor = new Color(0.95f, 0.97f, 1f, 0.55f);
                main.maxParticles = 420;
                var em = _spray.emission;
                em.rateOverTime = 280f;
                var shape = _spray.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 9f;
                shape.radius = 0.02f;
                var sol = _spray.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 5.2f)));
                var col = _spray.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.9f, 1f), 1f) },
                    new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var noise = _spray.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 1.2f;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = sprayMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            else
            {
                _spray.transform.SetParent(nozzle, false);
            }

            _spray.transform.localPosition = Vector3.zero;
            _spray.Play();
        }

        public void StopSpray()
        {
            if (_spray != null)
                _spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public ParticleSystem Spray => _spray;
    }
}
