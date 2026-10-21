using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC03 wrong answer (step08): the organic peroxide drum detonates. In order: white-orange flash, shock ring,
    /// the intact drum is swapped for the burst one (top peeled open, charred, glowing), the lid and blue plastic
    /// shards fly off, a flip-book fireball, flames, smoke, sparks, embers and crystal dust, scorch marks, red siren
    /// lamps, a red screen-edge vignette and sound (blast, fire crackle, alarm). <see cref="Clear"/> restores the
    /// room before the retry. Materials / meshes / textures are scene assets (a runtime Shader.Find would be
    /// stripped from the APK); everything else is built here.
    /// </summary>
    public sealed class Sc03Detonation : MonoBehaviour
    {
        [Header("Drum")]
        [SerializeField] private Transform origin;
        [SerializeField] private Transform drum;
        [SerializeField] private GameObject burstDrum;
        [SerializeField] private Transform lid;
        [SerializeField] private Renderer[] scorch;

        [Header("Particle materials")]
        [SerializeField] private Material fireSheet;
        [SerializeField] private Material smokeSheet;
        [SerializeField] private Material glowAdd;
        [SerializeField] private Material dustMaterial;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material shardMaterial;
        [SerializeField] private Mesh shardMesh;

        [Header("Sirens")]
        [SerializeField] private Light[] sirenLights;
        [SerializeField] private Renderer[] sirenLamps;
        [SerializeField] private Material lampOn;
        [SerializeField] private Material lampOff;
        [SerializeField] private Sprite vignette;

        private const int SampleRate = 44100;

        private ParticleSystem _fireBurst;
        private ParticleSystem _flames;
        private ParticleSystem _body;
        private ParticleSystem _smoke;
        private ParticleSystem _sparks;
        private ParticleSystem _embers;
        private ParticleSystem _shards;
        private ParticleSystem _dust;
        private ParticleSystem _ring;
        private ParticleSystem _ring2;
        private ParticleSystem _ringFlat;
        private bool _pending;
        private bool _fogSaved;
        private Color _fogColor0;
        private float _fogEnd0;
        private Light _fireLight;
        private Light _flashLight;
        private AudioSource _oneShot;
        private AudioSource _fireLoop;
        private AudioSource _alarmLoop;
        private AudioClip _boom;
        private Image _flash;
        private Image _vignetteImg;
        private MaterialPropertyBlock _block;
        private bool _active;
        private float _t;
        private Vector3 _burstScale = Vector3.one;
        private Coroutine _routine;

        public bool IsActive => _active || _pending;

        private void Awake()
        {
            if (origin == null)
                origin = transform;

            _fireBurst = Make("FireBurst", fireSheet, origin.position, Vector3.zero, 1f);
            _flames = Make("Flames", fireSheet, origin.position, new Vector3(-90f, 0f, 0f), 1.1f);
            var bodyPos = drum != null ? drum.position + Vector3.up * 0.45f : origin.position;
            _body = Make("FireBody", fireSheet, bodyPos, Vector3.zero, 1.2f);
            _smoke = Make("Smoke", smokeSheet, origin.position, new Vector3(-90f, 0f, 0f), 3f);
            _sparks = Make("Sparks", glowAdd, origin.position, new Vector3(-90f, 0f, 0f), 0.8f);
            _embers = Make("Embers", glowAdd, bodyPos, Vector3.zero, 0.8f);
            _shards = Make("Shards", shardMaterial, origin.position, new Vector3(-90f, 0f, 0f), 2f);
            _dust = Make("CrystalDust", dustMaterial, bodyPos, Vector3.zero, 2f);
            _ring = Make("ShockRing", ringMaterial, bodyPos, Vector3.zero, 0f);
            _ring2 = Make("ShockRing2", ringMaterial, bodyPos, Vector3.zero, 0f);
            _ringFlat = Make("ShockRingFlat", ringMaterial, drum != null ? drum.position + Vector3.up * 0.03f : origin.position,
                new Vector3(90f, 0f, 0f), 0f);
            _ringFlat.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;

            ConfigureFireBurst(_fireBurst);
            ConfigureFlames(_flames);
            ConfigureBody(_body);
            ConfigureSmoke(_smoke);
            ConfigureSparks(_sparks);
            ConfigureEmbers(_embers);
            ConfigureShards(_shards, shardMesh);
            ConfigureDust(_dust);
            ConfigureRing(_ring, 4.6f, 0.55f);
            ConfigureRing(_ring2, 3.0f, 0.8f);
            ConfigureRing(_ringFlat, 5.2f, 0.75f);

            _fireLight = MakeLight("FireLight", origin.position + Vector3.up * 0.25f, new Color(1f, 0.55f, 0.2f), 6f);
            _flashLight = MakeLight("FlashLight", origin.position + Vector3.up * 0.4f, new Color(1f, 0.8f, 0.55f), 9f);

            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _oneShot.spatialBlend = 0f;
            _fireLoop = gameObject.AddComponent<AudioSource>();
            _fireLoop.playOnAwake = false;
            _fireLoop.loop = true;
            _fireLoop.spatialBlend = 0f;
            _fireLoop.clip = BuildCrackle();
            _alarmLoop = gameObject.AddComponent<AudioSource>();
            _alarmLoop.playOnAwake = false;
            _alarmLoop.loop = true;
            _alarmLoop.spatialBlend = 0f;
            _alarmLoop.clip = BuildAlarm();
            _boom = BuildBoom();

            BuildScreenFx();
            _block = new MaterialPropertyBlock();
            if (burstDrum != null)
            {
                _burstScale = burstDrum.transform.localScale;
                burstDrum.SetActive(false);
            }

            if (lid != null)
                lid.gameObject.SetActive(false);
            SetScorch(0f);
            SetSirens(false, 0f);
        }

        // ---------------------------------------------------------------- build

        private ParticleSystem Make(string name, Material mat, Vector3 worldPos, Vector3 euler, float sortFudge)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(euler);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = sortFudge;
            return ps;
        }

        private Light MakeLight(string name, Vector3 pos, Color color, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            return l;
        }

        private void BuildScreenFx()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return;
            var canvas = PresentableUi.WorldCanvas("BlastScreenFx", cam, new Vector2(400f, 400f), 0.011f, false, 31900);
            canvas.transform.localPosition = new Vector3(0f, 0f, 0.13f);
            canvas.transform.localRotation = Quaternion.identity;
            var vr = PresentableUi.Rect("Vignette", canvas.transform, Vector2.zero, Vector2.one);
            _vignetteImg = PresentableUi.Img(vr, vignette, new Color(1f, 1f, 1f, 0f));
            var fr = PresentableUi.Rect("Flash", canvas.transform, Vector2.zero, Vector2.one);
            _flash = PresentableUi.Img(fr, null, new Color(1f, 0.82f, 0.55f, 0f));
        }

        private static void Sheet(ParticleSystem ps, int tiles)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = tiles;
            tsa.numTilesY = tiles;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.cycleCount = 1;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        }

        private static void Fade(ParticleSystem ps, float inTime, float outStart)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inTime), new GradientAlphaKey(1f, outStart),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = g;
        }

        private static void ConfigureFireBurst(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = -0.08f;
            main.maxParticles = 60;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.16f;
            Sheet(ps, 6);
        }

        private static void ConfigureFlames(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.25f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            main.gravityModifier = -0.12f;
            main.maxParticles = 50;
            var emission = ps.emission;
            emission.rateOverTime = 15f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.24f;
            Sheet(ps, 6);
        }

        // flames licking the whole drum body so the eruption reads at eye level, not only above the lid
        private static void ConfigureBody(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            main.gravityModifier = -0.18f;
            main.maxParticles = 60;
            var emission = ps.emission;
            emission.rateOverTime = 26f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.27f;
            Sheet(ps, 6);
        }

        private static void ConfigureSmoke(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = -0.03f;
            main.maxParticles = 60;
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            var emission = ps.emission;
            emission.rateOverTime = 6f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 10) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.3f;
            Sheet(ps, 6);
        }

        private static void ConfigureSparks(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 100;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.5f, 0.12f));
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = 0.15f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3f;
            r.velocityScale = 0.12f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));
        }

        private static void ConfigureEmbers(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            main.gravityModifier = -0.04f;
            main.maxParticles = 70;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.25f), new Color(1f, 0.38f, 0.08f));
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.9f;
            Fade(ps, 0.1f, 0.6f);
        }

        private static void ConfigureShards(ParticleSystem ps, Mesh mesh)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            main.gravityModifier = 1.3f;
            main.maxParticles = 60;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 0.2f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
            rot.y = new ParticleSystem.MinMaxCurve(-9f, 9f);
            rot.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
        }

        private static void ConfigureDust(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.gravityModifier = 0.05f;
            main.maxParticles = 50;
            main.startColor = new Color(1f, 0.97f, 0.9f, 0.7f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.5f)));
            Fade(ps, 0.05f, 0.3f);
        }

        private static void ConfigureRing(ParticleSystem ps, float endSize, float life)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = 0.4f;
            main.startColor = new Color(1f, 0.82f, 0.55f, 0.9f);
            main.maxParticles = 2;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(endSize / 0.4f,
                new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.35f, 0.65f), new Keyframe(1f, 1f)));
            Fade(ps, 0.02f, 0.2f);
        }

        // ---------------------------------------------------------------- play

        // The drum trembles and rumbles for a quarter of a second, then it goes.
        public void Explode()
        {
            if (_pending || _active)
                return;
            _pending = true;
            StartCoroutine(Tremor());
        }

        private IEnumerator Tremor()
        {
            PresentableFx.Play(PresentableFx.Whoosh, 0.55f);
            PresentableFx.HapticBoth(0.35f, 0.25f);
            if (drum != null)
            {
                var basePos = drum.localPosition;
                var baseRot = drum.localRotation;
                yield return PresentableFx.Tween(0.26f, k =>
                {
                    var a = k * 0.014f;
                    drum.localPosition = basePos + new Vector3(Mathf.Sin(k * 90f) * a, 0f, Mathf.Cos(k * 77f) * a);
                    drum.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(k * 63f) * 2.4f * k, 0f, Mathf.Cos(k * 55f) * 2.4f * k);
                }, x => x);
                drum.localPosition = basePos;
                drum.localRotation = baseRot;
            }

            _pending = false;
            ExplodeNow();
        }

        private void ExplodeNow()
        {
            _active = true;
            _t = 0f;
            if (!_fogSaved)
            {
                _fogSaved = true;
                _fogColor0 = RenderSettings.fogColor;
                _fogEnd0 = RenderSettings.fogEndDistance;
            }
            foreach (var ps in AllSystems())
            {
                ps.Clear();
                ps.Play();
            }

            if (drum != null)
                drum.gameObject.SetActive(false);
            if (burstDrum != null)
            {
                burstDrum.SetActive(true);
                burstDrum.transform.localScale = _burstScale * 0.9f;
            }

            if (lid != null && origin != null)
            {
                lid.gameObject.SetActive(true);
                lid.position = origin.position;
                lid.rotation = Quaternion.identity;
            }

            _oneShot.PlayOneShot(_boom, 1f);
            _fireLoop.volume = 0.45f;
            _fireLoop.Play();
            _alarmLoop.volume = 0.16f;
            _alarmLoop.Play();
            PresentableFx.HapticBoth(1f, 0.6f);
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(Sequence());
        }

        public void Clear()
        {
            if (_fireBurst == null)
                return; // never built: the room has not been shown yet
            _active = false;
            _pending = false;
            if (_fogSaved)
            {
                RenderSettings.fogColor = _fogColor0;
                RenderSettings.fogEndDistance = _fogEnd0;
                _fogSaved = false;
            }
            foreach (var ps in AllSystems())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _fireLight.intensity = 0f;
            _flashLight.intensity = 0f;
            SetSirens(false, 0f);
            SetScorch(0f);
            StopAllCoroutines();
            _routine = null;
            _oneShot.Stop();
            _fireLoop.Stop();
            _alarmLoop.Stop();
            if (_flash != null)
                PresentableUi.SetAlpha(_flash, 0f);
            if (_vignetteImg != null)
                PresentableUi.SetAlpha(_vignetteImg, 0f);
            if (lid != null)
                lid.gameObject.SetActive(false);
            if (burstDrum != null)
                burstDrum.SetActive(false);
            if (drum != null)
                drum.gameObject.SetActive(true);
        }

        private ParticleSystem[] AllSystems() => new[]
        {
            _fireBurst, _flames, _body, _smoke, _sparks, _embers, _shards, _dust, _ring, _ring2, _ringFlat
        };

        private IEnumerator Sequence()
        {
            // flash, lid, scorch marks, then two smaller secondary pops
            StartCoroutine(PresentableFx.Tween(0.6f, k =>
            {
                if (_flash != null)
                    PresentableUi.SetAlpha(_flash, 0.7f * (1f - k) * (1f - k));
                _flashLight.intensity = 26f * (1f - k) * (1f - k);
            }, x => x));
            StartCoroutine(Lid());
            StartCoroutine(PresentableFx.Tween(1.4f, k => SetScorch(k), PresentableFx.EaseOutCubic));
            yield return PresentableFx.Wait(0.32f);
            _fireBurst.Emit(7);
            _sparks.Emit(30);
            yield return PresentableFx.Wait(0.5f);
            _fireBurst.Emit(5);
            PresentableFx.HapticBoth(0.5f, 0.25f);
            _routine = null;
        }

        // the lid is blown off the drum: ballistic arc with a tumble
        private IEnumerator Lid()
        {
            if (lid == null || origin == null)
                yield break;
            var v = new Vector3(Random.Range(0.6f, 1.6f), Random.Range(6.2f, 7.5f), Random.Range(-1.4f, 1.0f));
            var w = new Vector3(Random.Range(5f, 9f), Random.Range(3f, 7f), Random.Range(4f, 8f));
            var p = origin.position;
            var q = Quaternion.identity;
            var t = 0f;
            while (t < 2.6f && _active)
            {
                t += Time.deltaTime;
                v += Physics.gravity * Time.deltaTime;
                p += v * Time.deltaTime;
                q = Quaternion.Euler(w * (Time.deltaTime * Mathf.Rad2Deg)) * q;
                lid.SetPositionAndRotation(p, q);
                // the ceiling stops it
                if (p.y > 3.0f)
                    v.y = -Mathf.Abs(v.y) * 0.3f;
                yield return null;
            }

            lid.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_active)
                return;
            _t += Time.deltaTime;
            var t = Time.time;
            var burst = Mathf.Exp(-_t * 1.6f);
            _fireLight.intensity = 3.4f + burst * 5f + Mathf.PerlinNoise(t * 9f, 0.3f) * 3.2f;
            var beat = 0.5f + 0.5f * Mathf.Sin(t * 5.2f); // smooth, never a hard strobe
            // the room fills with warm smoke: the fog turns brownish and closes in
            var smoke = Mathf.Clamp01(_t * 0.7f);
            RenderSettings.fogColor = Color.Lerp(_fogColor0, new Color(0.2f, 0.09f, 0.045f), smoke);
            RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd0, 9f, smoke);
            SetSirens(true, beat);
            if (_vignetteImg != null)
            {
                var vig = Mathf.Clamp01(_t * 2.2f) * (0.45f + 0.25f * beat);
                PresentableUi.SetAlpha(_vignetteImg, vig);
            }

            if (burstDrum != null)
            {
                // the burst drum settles from a small shock scale, then breathes like hot metal
                var s = Mathf.Lerp(0.9f, 1f, 1f - Mathf.Exp(-_t * 9f)) + Mathf.Sin(t * 3f) * 0.004f;
                burstDrum.transform.localScale = _burstScale * s;
            }
        }

        private void SetSirens(bool active, float level)
        {
            if (sirenLights != null)
            {
                foreach (var l in sirenLights)
                {
                    if (l != null)
                        l.intensity = active ? 1f + 4.5f * level : 0f;
                }
            }

            if (sirenLamps != null)
            {
                foreach (var r in sirenLamps)
                {
                    if (r != null)
                        r.sharedMaterial = active && level > 0.45f ? lampOn : lampOff;
                }
            }
        }

        private void SetScorch(float k)
        {
            if (scorch == null)
                return;
            if (_block == null)
                _block = new MaterialPropertyBlock();
            foreach (var r in scorch)
            {
                if (r == null)
                    continue;
                r.enabled = k > 0.001f;
                r.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", new Color(1f, 1f, 1f, k));
                r.SetPropertyBlock(_block);
            }
        }

        // ---------------------------------------------------------------- sound (procedural, no assets)

        private static AudioClip BuildBoom()
        {
            var n = Mathf.CeilToInt(3.2f * SampleRate);
            var d = new float[n];
            var rng = new System.Random(5);
            var low = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)SampleRate;
                var white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += Mathf.Lerp(0.2f, 0.015f, Mathf.Clamp01(t / 1.4f)) * (white - low);
                var rumble = low * Mathf.Exp(-1.7f * t) * (1f - Mathf.Exp(-t * 90f)) * 2.4f;
                var f = 48f + 70f * Mathf.Exp(-7f * t);
                var thump = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-2.6f * t) * 0.95f;
                var crack = white * Mathf.Exp(-45f * t) * 0.9f;
                d[i] = (float)System.Math.Tanh(rumble + thump + crack);
            }

            return Make("SFX_Boom", d);
        }

        private static AudioClip BuildCrackle()
        {
            var n = 2 * SampleRate;
            var d = new float[n];
            var rng = new System.Random(11);
            var low = 0f;
            var pop = 0f;
            for (var i = 0; i < n; i++)
            {
                var white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.03f * (white - low);
                if (rng.NextDouble() < 0.0016)
                    pop = (float)(0.35 + rng.NextDouble() * 0.6);
                pop *= 0.992f;
                d[i] = low * 0.9f + white * pop * 0.55f;
            }

            return Make("SFX_FireCrackle", d);
        }

        private static AudioClip BuildAlarm()
        {
            var n = Mathf.CeilToInt(1.6f * SampleRate);
            var d = new float[n];
            var phase = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)SampleRate;
                var k = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 1.6f * 2f); // two sweeps per clip
                var f = Mathf.Lerp(640f, 920f, k);
                phase += 2f * Mathf.PI * f / SampleRate;
                d[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase)) * 0.55f;
            }

            return Make("SFX_Alarm", d);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
