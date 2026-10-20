using System.Collections;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC03 wrong answer (step08): the drum detonates. Fire, smoke and sparks burst from the drum lid, red siren
    /// lamps flash above the shelf and the drum jolts. <see cref="Clear"/> puts the room back before the retry.
    /// Materials are scene assets (a runtime Shader.Find would be stripped from the APK).
    /// </summary>
    public sealed class Sc03Detonation : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private Transform drum;
        [SerializeField] private Material fireMaterial;
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Light[] sirenLights;
        [SerializeField] private Renderer[] sirenLamps;
        [SerializeField] private Material lampOn;
        [SerializeField] private Material lampOff;

        private ParticleSystem _fire;
        private ParticleSystem _body;
        private ParticleSystem _smoke;
        private ParticleSystem _sparks;
        private Light _fireLight;
        private bool _active;
        private Vector3 _drumPos;
        private Quaternion _drumRot;
        private Coroutine _jolt;

        public bool IsActive => _active;

        private void Awake()
        {
            if (origin == null)
                origin = transform;
            _fire = Make("Fire", fireMaterial, new Vector3(-90f, 0f, 0f));
            _smoke = Make("Smoke", smokeMaterial, new Vector3(-90f, 0f, 0f));
            _sparks = Make("Sparks", sparkMaterial, new Vector3(-90f, 0f, 0f));
            ConfigureFire(_fire);
            _body = Make("FireBody", fireMaterial, Vector3.zero);
            if (drum != null)
                _body.transform.position = drum.position + Vector3.up * 0.45f;
            ConfigureBody(_body);
            ConfigureSmoke(_smoke);
            ConfigureSparks(_sparks);

            var l = new GameObject("FireLight");
            l.transform.SetParent(origin, false);
            l.transform.localPosition = Vector3.up * 0.3f;
            _fireLight = l.AddComponent<Light>();
            _fireLight.type = LightType.Point;
            _fireLight.color = new Color(1f, 0.55f, 0.2f);
            _fireLight.range = 6f;
            _fireLight.intensity = 0f;
            _fireLight.shadows = LightShadows.None;

            if (drum != null)
            {
                _drumPos = drum.localPosition;
                _drumRot = drum.localRotation;
            }

            SetSirens(false, 0f);
        }

        private ParticleSystem Make(string name, Material mat, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(origin, false);
            go.transform.localRotation = Quaternion.Euler(euler);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void ConfigureFire(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            main.gravityModifier = -0.12f;
            main.maxParticles = 220;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f),
                new Color(1f, 0.85f, 0.35f, 0.9f));
            var emission = ps.emission;
            emission.rateOverTime = 34f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.45f),
                    new GradientColorKey(new Color(0.5f, 0.1f, 0.02f), 1f)
                },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.35f, 1.2f), new Keyframe(1f, 0.3f)));
        }

        // flames licking the whole drum body so the eruption is seen at eye level, not only above the lid
        private static void ConfigureBody(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            main.gravityModifier = -0.2f;
            main.maxParticles = 160;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.1f, 0.85f),
                new Color(1f, 0.8f, 0.3f, 0.85f));
            var emission = ps.emission;
            emission.rateOverTime = 55f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.08f), 0.5f),
                    new GradientColorKey(new Color(0.45f, 0.08f, 0.02f), 1f)
                },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1.15f), new Keyframe(1f, 0.25f)));
        }

        private static void ConfigureSmoke(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 120;
            main.startColor = new Color(0.12f, 0.1f, 0.09f, 0.55f);
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f)));
        }

        private static void ConfigureSparks(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.gravityModifier = 1.1f;
            main.maxParticles = 120;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.5f, 0.1f));
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 0.15f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.5f;
            r.velocityScale = 0.1f;
        }

        public void Explode()
        {
            _active = true;
            _fire.Clear();
            _body.Clear();
            _smoke.Clear();
            _sparks.Clear();
            _fire.Play();
            _body.Play();
            _smoke.Play();
            _sparks.Play();
            PresentableFx.Play(PresentableFx.Whoosh, 1f);
            PresentableFx.HapticBoth(1f, 0.5f);
            if (_jolt != null)
                StopCoroutine(_jolt);
            _jolt = StartCoroutine(Jolt());
        }

        public void Clear()
        {
            if (_fire == null)
                return; // never built: the room has not been shown yet
            _active = false;
            _fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _body.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _fireLight.intensity = 0f;
            SetSirens(false, 0f);
            if (_jolt != null)
                StopCoroutine(_jolt);
            _jolt = null;
            if (drum != null)
            {
                drum.localPosition = _drumPos;
                drum.localRotation = _drumRot;
            }
        }

        private IEnumerator Jolt()
        {
            if (drum == null)
                yield break;
            yield return PresentableFx.Tween(0.45f, t =>
            {
                var k = Mathf.Sin(t * Mathf.PI);
                drum.localPosition = _drumPos + Vector3.up * (0.06f * k);
                drum.localRotation = _drumRot * Quaternion.Euler(10f * k, 0f, 6f * k);
            });
            _jolt = null;
        }

        private void Update()
        {
            if (!_active)
                return;
            var t = Time.time;
            _fireLight.intensity = 3.2f + Mathf.PerlinNoise(t * 9f, 0.3f) * 3.4f;
            var on = Mathf.Repeat(t * 2.4f, 1f) < 0.55f;
            SetSirens(true, on ? 1f : 0.15f);
        }

        private void SetSirens(bool active, float level)
        {
            if (sirenLights != null)
            {
                foreach (var l in sirenLights)
                {
                    if (l != null)
                        l.intensity = active ? 4f * level : 0f;
                }
            }

            if (sirenLamps != null)
            {
                foreach (var r in sirenLamps)
                {
                    if (r != null)
                        r.sharedMaterial = active && level > 0.5f ? lampOn : lampOff;
                }
            }
        }
    }
}
