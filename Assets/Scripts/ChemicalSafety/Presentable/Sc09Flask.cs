using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 9: the purple KMnO4 sample flask (B-Sample 2). A poke lifts it and tilts it over B-Sample 1 (a thin purple stream),
    /// then it slips out of the glove, falls onto the steel bench and breaks: the intact flask is swapped for a jagged base and shards
    /// that fly and settle, a purple puddle spreads, drops fly toward the face (the visor takes them), glass-break and splash sounds.
    /// Everything is scripted (no rigidbodies) so it behaves the same on the desk and on the Quest. Objects are found by the names
    /// the scene builder gives them.
    /// </summary>
    public sealed class Sc09Flask : MonoBehaviour
    {
        [SerializeField] private Material streamMaterial;
        [SerializeField] private Material dropMaterial;
        [SerializeField] private AudioClip knockClip;
        [SerializeField] private AudioClip breakClip;
        [SerializeField] private AudioClip splashClip;
        [SerializeField] private Sc09Visor visor;
        [SerializeField] private Color liquidColor = new Color(0.52f, 0.08f, 0.70f, 0.92f);

        private const float FlaskHeight = 0.22f;

        private Transform _flask;
        private Transform _flask1;
        private Transform _liquid2;
        private Renderer _liquid1R;
        private Transform _liquid1;
        private Transform _glow;
        private PokeTarget _poke;
        private Transform _broken;
        private Transform[] _shards;
        private Vector3[] _shardHome;
        private Quaternion[] _shardHomeRot;
        private Transform _puddle;
        private Renderer _puddleR;
        private Transform[] _splats;
        private LineRenderer _stream;
        private ParticleSystem _burst;
        private ParticleSystem _faceSpray;
        private AudioSource _audio;
        private Vector3 _home;
        private Quaternion _homeRot;
        private Vector3 _liquid1Scale;
        private Vector3 _liquid2Scale;
        private bool _busy;
        private MaterialPropertyBlock _mpb;

        public bool Broken { get; private set; }
        public bool Carrying { get; private set; }
        public Transform Flask => _flask;
        public PokeTarget Poke => _poke;

        /// <summary>Raised when the flask has hit the bench and the drops are on the face shield.</summary>
        public event Action Splashed;

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void Awake()
        {
            var root = transform.parent != null ? transform.parent : transform;
            _flask = Find(root, "Sc09Flask2");
            _flask1 = Find(root, "Sc09Flask1");
            _liquid2 = Find(root, "Sc09Liquid2");
            _liquid1 = Find(root, "Sc09Liquid1");
            _liquid1R = _liquid1 != null ? _liquid1.GetComponent<Renderer>() : null;
            _glow = Find(root, "Sc09FlaskGlow");
            var p = Find(root, "Sc09FlaskPoke");
            _poke = p != null ? p.GetComponent<PokeTarget>() : null;
            _broken = Find(root, "Sc09Broken");
            _puddle = Find(root, "Sc09Puddle");
            _puddleR = _puddle != null ? _puddle.GetComponent<Renderer>() : null;
            if (_broken != null)
            {
                var shards = new System.Collections.Generic.List<Transform>();
                var splats = new System.Collections.Generic.List<Transform>();
                foreach (var t in _broken.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Sc09Shard"))
                        shards.Add(t);
                    else if (t.name.StartsWith("Sc09Splat"))
                        splats.Add(t);
                }

                _shards = shards.ToArray();
                _splats = splats.ToArray();
                _shardHome = new Vector3[_shards.Length];
                _shardHomeRot = new Quaternion[_shards.Length];
                for (var i = 0; i < _shards.Length; i++)
                {
                    _shardHome[i] = _shards[i].localPosition;
                    _shardHomeRot[i] = _shards[i].localRotation;
                }
            }

            if (_flask != null)
            {
                _home = _flask.position;
                _homeRot = _flask.rotation;
            }

            if (_liquid1 != null)
                _liquid1Scale = _liquid1.localScale;
            if (_liquid2 != null)
                _liquid2Scale = _liquid2.localScale;
            _mpb = new MaterialPropertyBlock();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void Start()
        {
            if (_poke != null)
            {
                _poke.Poked += _ => StartPour();
                _poke.SetInteractable(false);
            }

            if (_glow != null)
                _glow.gameObject.SetActive(false);
            BuildFx();
            ResetAll();
        }

        private void BuildFx()
        {
            var go = new GameObject("Sc09PourStream");
            go.transform.SetParent(transform, false);
            _stream = go.AddComponent<LineRenderer>();
            _stream.useWorldSpace = true;
            _stream.positionCount = 10;
            _stream.numCapVertices = 3;
            _stream.shadowCastingMode = ShadowCastingMode.Off;
            _stream.receiveShadows = false;
            _stream.sharedMaterial = streamMaterial;
            _stream.alignment = LineAlignment.View;
            _stream.startWidth = 0.011f;
            _stream.endWidth = 0.008f;
            _stream.enabled = false;

            _burst = MakeParticles("Sc09Burst", 70, 0.9f, 0.7f, 2.4f, 0.006f, 0.016f, 1.1f, 80f);
            _faceSpray = MakeParticles("Sc09FaceSpray", 90, 0.5f, 1.6f, 3.2f, 0.007f, 0.018f, 0.5f, 22f);
        }

        private ParticleSystem MakeParticles(string name, int max, float life, float v0, float v1, float s0, float s1, float grav, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(v0, v1);
            main.startSize = new ParticleSystem.MinMaxCurve(s0, s1);
            main.startColor = new Color(0.55f, 0.1f, 0.72f, 0.95f);
            main.gravityModifier = grav;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = angle;
            sh.radius = 0.02f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dropMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // ------------------------------------------------------------------ interaction

        public void SetInteractable(bool on)
        {
            if (_poke != null)
                _poke.SetInteractable(on && !_busy && !Broken);
            if (_glow != null)
                _glow.gameObject.SetActive(on && !_busy && !Broken);
        }

        public void StartPour()
        {
            if (_busy || Broken || _flask == null)
                return;
            StartCoroutine(Accident());
        }

        private void Hit(AudioClip clip, float volume)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip, volume);
        }

        private void SetFill(Transform liquid, Vector3 baseScale, float k)
        {
            if (liquid != null)
                liquid.localScale = new Vector3(baseScale.x, baseScale.y * Mathf.Max(0.02f, k), baseScale.z);
        }

        private IEnumerator Accident()
        {
            _busy = true;
            Carrying = true;
            SetInteractable(false);
            if (_glow != null)
                _glow.gameObject.SetActive(false);
            Hit(knockClip, 0.5f);

            // 1. lift off the bench and carry it over B-Sample 1, tilting like a pour (the viewer's left is +X)
            var up = Vector3.up;
            var target1 = _flask1 != null ? _flask1.position : _home + new Vector3(0f, 0f, 0.27f);
            // horizontal direction from this flask to B-Sample 1 (the tilt leans that way) and toward the user (the flask falls that way)
            var h = target1 - _home;
            h.y = 0f;
            h = h.sqrMagnitude > 1e-6f ? h.normalized : Vector3.forward;
            var cam0 = Camera.main != null ? Camera.main.transform.position : _home - Vector3.right;
            var toCam = cam0 - _home;
            toCam.y = 0f;
            toCam = toCam.sqrMagnitude > 1e-6f ? toCam.normalized : -Vector3.right;
            var lift = _home + up * 0.22f + toCam * 0.05f;
            var tilt = 58f;
            Vector3 Dir(float deg) => h * Mathf.Sin(deg * Mathf.Deg2Rad) + Vector3.up * Mathf.Cos(deg * Mathf.Deg2Rad);
            var dir = Dir(tilt);
            var lipOffset = dir * (FlaskHeight * 0.95f);
            var mouth1 = target1 + Vector3.up * (FlaskHeight * 0.95f);
            var pourPos = mouth1 - lipOffset + Vector3.up * 0.045f - h * 0.012f;
            var pourRot = Quaternion.FromToRotation(Vector3.up, dir);
            yield return Move(_home, _homeRot, lift, _homeRot, 0.55f);
            yield return Move(lift, _homeRot, pourPos, pourRot, 0.95f);

            // 2. pour: the thin stream, B-Sample 1 fills with purple, B-Sample 2 drains a little
            _stream.enabled = true;
            if (_liquid1R != null)
            {
                _mpb.SetColor("_BaseColor", liquidColor);
                _mpb.SetColor("_Color", liquidColor);
                _liquid1R.SetPropertyBlock(_mpb);
            }

            var t = 0f;
            const float pourSeconds = 2.0f;
            while (t < pourSeconds)
            {
                t += Time.deltaTime;
                var k = Mathf.Clamp01(t / pourSeconds);
                var wob = Mathf.Sin(Time.time * 7f) * 0.18f;
                var tilted = Quaternion.FromToRotation(Vector3.up, Dir(tilt + wob * 4f));
                _flask.SetPositionAndRotation(pourPos + new Vector3(0f, Mathf.Sin(Time.time * 3f) * 0.002f, 0f), tilted);
                var lip = _flask.position + _flask.up * (FlaskHeight * 0.95f);
                var end = mouth1 - Vector3.up * (0.03f + 0.06f * k);
                for (var i = 0; i < _stream.positionCount; i++)
                {
                    var f = i / (float)(_stream.positionCount - 1);
                    var p = Vector3.Lerp(lip, end, f);
                    p.x += Mathf.Sin(f * 9f + Time.time * 11f) * 0.0015f * f;
                    p.y -= Mathf.Sin(f * Mathf.PI) * 0.0f;
                    _stream.SetPosition(i, p);
                }

                SetFill(_liquid1, _liquid1Scale, Mathf.Lerp(0.15f, 0.55f, k));
                SetFill(_liquid2, _liquid2Scale, Mathf.Lerp(1f, 0.55f, k));
                yield return null;
            }

            // 3. it slips: the stream stops, the flask tips further and falls
            _stream.enabled = false;
            var fallStart = _flask.position;
            var fallRot = _flask.rotation;
            var impact = _home + toCam * 0.20f;
            var v = new Vector3((impact.x - fallStart.x) / 0.42f, 0.2f, (impact.z - fallStart.z) / 0.42f);
            var spin = new Vector3(40f, 15f, 260f);
            var rot = fallRot;
            var pos = fallStart;
            var baseY = _home.y + 0.055f; // the flask lies on its side: its radius above the bench
            Hit(knockClip, 0.35f);
            while (pos.y > baseY)
            {
                var dt = Time.deltaTime;
                v.y -= 9.81f * dt;
                pos += v * dt;
                rot = Quaternion.Euler(spin * dt) * rot;
                _flask.SetPositionAndRotation(pos, rot);
                yield return null;
            }

            // 4. impact
            Carrying = false;
            var hitPoint = new Vector3(pos.x, _home.y, pos.z);
            yield return Break(hitPoint);
            _busy = false;
            Splashed?.Invoke();
        }

        private IEnumerator Move(Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1, float seconds)
        {
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                _flask.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
        }

        private IEnumerator Break(Vector3 hitPoint)
        {
            Broken = true;
            _flask.gameObject.SetActive(false);
            Hit(breakClip, 1f);
            Hit(splashClip, 0.8f);
            PresentableFx.HapticBoth(0.8f, 0.18f);

            if (_broken != null)
            {
                _broken.position = hitPoint;
                _broken.rotation = Quaternion.Euler(0f, 25f, 0f);
                _broken.gameObject.SetActive(true);
            }

            // the drops: a burst up and out, a spray at the face shield
            _burst.transform.position = hitPoint + Vector3.up * 0.05f;
            _burst.transform.rotation = Quaternion.LookRotation(Vector3.up);
            _burst.Emit(60);
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                _faceSpray.transform.position = hitPoint + Vector3.up * 0.08f;
                var d = cam.position + Vector3.down * 0.05f - _faceSpray.transform.position;
                _faceSpray.transform.rotation = Quaternion.LookRotation(d.normalized);
                _faceSpray.Emit(70);
            }

            if (visor != null)
                StartCoroutine(visor.Splash(0.9f));

            // shards fly, bounce once on the bench and settle; the puddle spreads
            var n = _shards != null ? _shards.Length : 0;
            var vel = new Vector3[n];
            var ang = new Vector3[n];
            var rnd = new System.Random(5);
            for (var i = 0; i < n; i++)
            {
                _shards[i].localPosition = _shardHome[i];
                _shards[i].localRotation = _shardHomeRot[i];
                var a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var sp = 0.5f + (float)rnd.NextDouble() * 0.9f;
                vel[i] = new Vector3(Mathf.Cos(a) * sp, 0.9f + (float)rnd.NextDouble() * 1.1f, Mathf.Sin(a) * sp);
                ang[i] = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 900f;
            }

            if (_puddle != null)
                _puddle.gameObject.SetActive(true);
            var t = 0f;
            var landed = new bool[n];
            while (t < 1.6f)
            {
                var dt = Time.deltaTime;
                t += dt;
                for (var i = 0; i < n; i++)
                {
                    if (landed[i])
                        continue;
                    vel[i].y -= 9.81f * dt;
                    var sh = _shards[i];
                    sh.position += vel[i] * dt;
                    sh.Rotate(ang[i] * dt, Space.World);
                    if (sh.position.y <= hitPoint.y + 0.004f && vel[i].y < 0f)
                    {
                        var p = sh.position;
                        p.y = hitPoint.y + 0.004f;
                        sh.position = p;
                        if (vel[i].y < -1.2f && !landed[i])
                        {
                            vel[i] = new Vector3(vel[i].x * 0.4f, -vel[i].y * 0.28f, vel[i].z * 0.4f);
                            ang[i] *= 0.4f;
                        }
                        else
                        {
                            landed[i] = true;
                            // lie flat
                            var e = sh.eulerAngles;
                            sh.rotation = Quaternion.Euler(Mathf.Round(e.x / 90f) * 90f * 0.3f, e.y, Mathf.Round(e.z / 90f) * 90f * 0.3f);
                        }
                    }
                }

                if (_puddle != null)
                {
                    var k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 1.5f), 3f);
                    _puddle.localScale = new Vector3(Mathf.Lerp(0.05f, 0.46f, k), Mathf.Lerp(0.05f, 0.34f, k), 1f);
                }

                yield return null;
            }

            if (_splats != null)
                foreach (var s in _splats)
                    s.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ reset (BACK, or a new run)

        public void ResetAll()
        {
            StopAllCoroutines();
            _busy = false;
            Broken = false;
            Carrying = false;
            if (_flask != null)
            {
                _flask.gameObject.SetActive(true);
                _flask.SetPositionAndRotation(_home, _homeRot);
            }

            if (_broken != null)
                _broken.gameObject.SetActive(false);
            if (_puddle != null)
            {
                _puddle.gameObject.SetActive(false);
                _puddle.localScale = new Vector3(0.05f, 0.05f, 1f);
            }

            if (_splats != null)
                foreach (var s in _splats)
                    s.gameObject.SetActive(false);
            if (_stream != null)
                _stream.enabled = false;
            if (_burst != null)
                _burst.Clear();
            if (_faceSpray != null)
                _faceSpray.Clear();
            SetFill(_liquid1, _liquid1Scale, 0.15f);
            SetFill(_liquid2, _liquid2Scale, 1f);
            if (_liquid1R != null)
                _liquid1R.SetPropertyBlock(null);
            if (_poke != null)
                _poke.SetInteractable(false);
            if (_glow != null)
                _glow.gameObject.SetActive(false);
            if (visor != null)
                visor.Hide();
        }
    }
}
