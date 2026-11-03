using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    public enum SupPose
    {
        /// <summary>Standing, arms relaxed at the sides.</summary>
        Relaxed,
        /// <summary>Same as Relaxed (kept for the callers of the first version).</summary>
        Clipboard,
        /// <summary>Talking: the right forearm lifts now and then in a small, calm gesture.</summary>
        Talk,
        /// <summary>Same as Relaxed.</summary>
        Point,
        /// <summary>Same as Relaxed.</summary>
        Wait
    }

    /// <summary>
    /// Lab supervisor animated by code on the supplied Lab Technician mesh (skeleton made by the builder: bones unrotated in the bind
    /// pose, +Z forward, R = +X). Pure forward kinematics with small, eased angles and its own random timing, so nothing loops visibly:
    /// breathing, weight shift, a body that turns to the player, a head that follows the player (or glances at the poster), calm
    /// talking gestures, nods and head shakes, and a real walking gait (legs, counter-swinging arms, hip and shoulder twist, bob).
    /// </summary>
    public sealed class LabSupervisor : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> _b = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Vector3> _restLocal = new Dictionary<string, Vector3>();
        private bool _ready;
        private float _seed;

        private bool _talking;
        private float _talk;
        private Transform _lookTarget;
        private Vector3? _lookPoint;
        private Transform _glanceTarget;
        private float _glanceUntil;
        private float _yaw, _pitch, _glanceT, _glanceNext;
        private float _nod, _shake, _gesture;
        private float _walkBlend, _walkPhase, _walkSpeedNow;
        private Vector3 _walkDest;
        private bool _walking;
        private float _walkSpeed = 1.15f;
        private float _breathe;
        private bool _turning;
        private float _turnVel;
        private float _gestT, _gestNext = 2f, _gestHold;

        private static readonly Dictionary<string, float> Tune = new Dictionary<string, float>();

        // editor only: "key=value" lines in Temp/sup_params.txt override the pose numbers while tuning (read when the scene starts)
        private static float P(string key, float def) => Tune.TryGetValue(key, out var v) ? v : def;

        private static void LoadTune()
        {
            Tune.Clear();
#if UNITY_EDITOR
            try
            {
                var path = System.IO.Path.Combine(Application.dataPath, "../Temp/sup_params.txt");
                if (!System.IO.File.Exists(path))
                    return;
                foreach (var line in System.IO.File.ReadAllLines(path))
                {
                    var kv = line.Split('=');
                    if (kv.Length == 2 && float.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                        Tune[kv[0].Trim()] = f;
                }
            }
            catch (System.Exception)
            {
            }
#endif
        }

        public bool Walking => _walking;
        public Transform RightSocket => Bone("HandR_Socket");
        public Transform LeftSocket => Bone("HandL_Socket");
        public Transform Bone(string n) => _b.TryGetValue(n, out var t) ? t : null;

        /// <summary>Talk makes the hands gesture; every other pose is the relaxed stance.</summary>
        public void SetPose(SupPose p) => _talking = p == SupPose.Talk;

        public void LookAt(Transform t) { _lookTarget = t; _lookPoint = null; }
        public void LookAtPoint(Vector3 p) { _lookTarget = null; _lookPoint = p; }
        public void PointAt(Transform t) { _glanceTarget = t; _glanceUntil = Time.time + 2.6f; }
        public void Nod() { _nod = 1f; }
        public void ShakeHead() { _shake = 1f; }
        public void Gesture() { _gesture = 1f; }

        public void WalkTo(Vector3 worldPos, float speed = 1.15f)
        {
            _walkDest = worldPos;
            _walkSpeed = speed;
            _walking = true;
        }

        /// <summary>Teleports to a place and faces the given point (used before a scenario starts).</summary>
        public void PlaceAt(Vector3 worldPos, Vector3 faceToward)
        {
            transform.position = worldPos;
            var to = faceToward - worldPos;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            _walking = false;
        }

        /// <summary>Editor previews: poses the bones without Play mode.</summary>
        public void SnapPose(SupPose pose, float time)
        {
            Init();
            SetPose(pose);
            _talk = _talking ? 1f : 0f;
            for (var k = 0; k < 30; k++)
                Tick(time + k * 0.05f, 0.05f);
        }

        /// <summary>Body-space positions of the main joints, for the pose photos of the smoke test.</summary>
        public string DebugDump()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in new[] { "Hips", "Chest", "Head", "UpperArmL", "ForeArmL", "HandL", "UpperArmR", "ForeArmR", "HandR" })
                if (_b.TryGetValue(n, out var t))
                    sb.Append(n).Append('=').Append(transform.InverseTransformPoint(t.position).ToString("0.00")).Append("  ");
            return sb.ToString();
        }

        private void Awake() => Init();

        private void Init()
        {
            if (_ready && _b.Count > 0)
                return;
            _b.Clear();
            _restLocal.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                _b[t.name] = t;
            // the skeleton is unrotated in the bind pose: whatever pose an edit-mode test left in the scene is thrown away here
            foreach (var kv in _b)
            {
                if (kv.Value == transform || kv.Value.GetComponent<Renderer>() != null || kv.Key.EndsWith("_Socket"))
                    continue;
                kv.Value.localRotation = Quaternion.identity;
            }

            foreach (var kv in _b)
                _restLocal[kv.Key] = kv.Value.localPosition;
            LoadTune();
            _seed = Random.value * 20f;
            _glanceNext = Random.Range(3f, 7f);
            _ready = _b.ContainsKey("Hips") && _b.ContainsKey("UpperArmR");
        }

        private static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;

        private void LateUpdate()
        {
            if (!_ready)
            {
                Init();
                if (!_ready)
                    return;
            }

            Tick(Time.time, Mathf.Min(Time.deltaTime, 0.05f));
        }

        private void Tick(float t, float dt)
        {
            UpdateWalk(dt);
            FaceTarget(dt);
            _talk = Mathf.MoveTowards(_talk, _talking && !_walking ? 1f : 0f, dt * 1.2f);
            _nod = Mathf.MoveTowards(_nod, 0f, dt * 1.1f);
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 0.9f);
            _gesture = Mathf.MoveTowards(_gesture, 0f, dt * 0.7f);
            _breathe = Mathf.Sin(t * 1.35f + _seed) * 0.5f + 0.5f;

            Torso(t);
            Legs(t);
            Arms(t, dt);
            Head(t, dt);
        }

        // ------------------------------------------------------------------ walking and facing

        private void UpdateWalk(float dt)
        {
            var target = 0f;
            _walkSpeedNow = 0f;
            if (_walking)
            {
                var delta = _walkDest - transform.position;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist < 0.05f)
                {
                    _walking = false;
                }
                else
                {
                    target = 1f;
                    var want = Quaternion.LookRotation(delta.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-4.5f * dt));
                    // eased start and stop: speed follows the gait blend and slows down on arrival
                    var speed = _walkSpeed * Mathf.Clamp01(_walkBlend * 1.6f) * Mathf.Clamp01(dist / 0.7f + 0.2f);
                    transform.position += transform.forward * Mathf.Min(dist, speed * dt);
                    _walkSpeedNow = speed;
                    _walkPhase += dt * speed * 3.1f; // one full step cycle per ~2 m of walking
                }
            }

            _walkBlend = Mathf.MoveTowards(_walkBlend, target, dt * 2.0f);
        }

        // the body turns to the player (eased, only when the angle gets large), the head does the small movements
        private void FaceTarget(float dt)
        {
            if (_walking || (_lookTarget == null && !_lookPoint.HasValue))
                return;
            var to = (_lookTarget != null ? _lookTarget.position : _lookPoint.Value) - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
                return;
            var ang = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            if (Mathf.Abs(ang) > 24f)
                _turning = true;
            if (!_turning)
                return;
            var cur = transform.eulerAngles.y;
            var next = Mathf.SmoothDampAngle(cur, cur + ang, ref _turnVel, 0.6f, 110f, dt);
            transform.rotation = Quaternion.Euler(0f, next, 0f);
            if (Mathf.Abs(ang) < 4f)
                _turning = false;
        }

        // ------------------------------------------------------------------ pose helpers

        private void Rot(string n, float x, float y, float z)
        {
            if (_b.TryGetValue(n, out var tr))
                tr.localRotation = Quaternion.Euler(x, y, z);
        }

        private void Torso(float t)
        {
            var sway = Noise(t * 0.12f, _seed) * 1.4f;
            var shift = Mathf.Sin(t * 0.21f + _seed * 1.3f);
            var s = Mathf.Sin(_walkPhase);
            var bob = Mathf.Abs(Mathf.Cos(_walkPhase)) * _walkBlend * 0.03f;
            if (_b.TryGetValue("Hips", out var hips))
            {
                hips.localPosition = _restLocal["Hips"] + new Vector3(shift * 0.03f * (1f - _walkBlend), bob - _walkBlend * 0.012f, 0f);
                hips.localRotation = Quaternion.Euler(0f, sway * 0.5f - s * 5f * _walkBlend, shift * 1.2f * (1f - _walkBlend) + s * 2f * _walkBlend);
            }

            Rot("Spine", 1.0f + _breathe * 0.5f + _walkBlend * 3f, s * 4f * _walkBlend - sway * 0.3f, -shift * 0.5f * (1f - _walkBlend));
            Rot("Chest", 0.6f + _breathe * 1.2f, s * 3f * _walkBlend + sway * 0.4f, 0f);
        }

        private void Legs(float t)
        {
            var s = Mathf.Sin(_walkPhase);
            var c = Mathf.Cos(_walkPhase);
            var shift = Mathf.Sin(t * 0.21f + _seed * 1.3f);
            var idleR = Mathf.Lerp(5f, 0.5f, shift > 0f ? 1f : 0f) * (1f - _walkBlend);
            var idleL = Mathf.Lerp(0.5f, 5f, shift > 0f ? 1f : 0f) * (1f - _walkBlend);
            // thigh forward = negative X; knee bends backwards = positive X
            Rot("ThighR", -s * 27f * _walkBlend - idleR * 0.4f, 0f, 0f);
            Rot("ShinR", Mathf.Max(0f, c) * 38f * _walkBlend + idleR, 0f, 0f);
            Rot("FootR", Mathf.Max(0f, -s) * 10f * _walkBlend - Mathf.Max(0f, s) * 6f * _walkBlend, 0f, 0f);
            Rot("ThighL", s * 27f * _walkBlend - idleL * 0.4f, 0f, 0f);
            Rot("ShinL", Mathf.Max(0f, -c) * 38f * _walkBlend + idleL, 0f, 0f);
            Rot("FootL", Mathf.Max(0f, s) * 10f * _walkBlend - Mathf.Max(0f, -s) * 6f * _walkBlend, 0f, 0f);
        }

        // ------------------------------------------------------------------ arms: relaxed at the sides, a calm talking gesture, a gait swing

        private void Arms(float t, float dt)
        {
            var s = Mathf.Sin(_walkPhase);
            var rest = 1f - _talk;
            var armIn = P("armZ", 27f); // the A-pose arms are about 36 degrees out: bring them close to the body
            var elbow = P("elbow", 16f);

            // random gesture timing: every few seconds the right forearm lifts for a moment while talking
            _gestT += dt;
            if (_gestT > _gestNext)
            {
                _gestT = 0f;
                _gestNext = Random.Range(2.2f, 4.5f);
                _gestHold = Random.Range(1.2f, 2.2f);
            }

            var lift = _talk * Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(_gestT / Mathf.Max(0.5f, _gestHold)) * Mathf.PI)) + _gesture * 0.6f * Mathf.Sin(_gesture * Mathf.PI);
            lift = Mathf.Clamp01(lift) * (1f - _walkBlend);
            var wob = Noise(t * 0.8f, _seed + 4f);

            var swingR = s * 20f * _walkBlend; // right arm swings opposite to the right leg
            var swingL = -s * 20f * _walkBlend;

            // right arm: x forward is negative
            Rot("UpperArmR", -6f + swingR - lift * 20f, lift * 6f, -armIn + lift * 6f);
            Rot("ForeArmR", -elbow - Mathf.Max(0f, -swingR) * 0.6f - lift * (55f + wob * 8f), lift * 10f, -3f - lift * 8f);
            Rot("HandR", -6f - lift * 14f, 0f, lift * 12f * wob);
            // left arm: hangs quietly, a little forward bend
            Rot("UpperArmL", -5f + swingL, 0f, armIn);
            Rot("ForeArmL", -elbow - Mathf.Max(0f, -swingL) * 0.6f, 0f, 3f);
            Rot("HandL", -6f, 0f, 0f);

            // fingers: a soft curl, a bit more when the hand lifts
            var curl = 14f + lift * 14f + _breathe * 2f;
            Rot("FingersR", 0f, 0f, -curl);
            Rot("FingersL", 0f, 0f, curl);
        }

        // ------------------------------------------------------------------ head

        private Vector3 PlayerHead()
        {
            if (_glanceTarget != null && Time.time < _glanceUntil)
                return _glanceTarget.position;
            if (_lookTarget != null)
                return _lookTarget.position;
            if (_lookPoint.HasValue)
                return _lookPoint.Value;
            return transform.position + transform.forward * 2f + Vector3.up * 1.6f;
        }

        private void Head(float t, float dt)
        {
            var head = _b["Head"];
            var look = PlayerHead();
            var to = transform.InverseTransformPoint(look) - transform.InverseTransformPoint(head.position);
            var wantYaw = Mathf.Clamp(Mathf.Atan2(to.x, Mathf.Max(0.01f, to.z)) * Mathf.Rad2Deg, -65f, 65f);
            var wantPitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -22f, 20f);

            // small living glances and a slow drift, so the gaze is never frozen
            _glanceT += dt;
            if (_glanceT > _glanceNext)
            {
                _glanceT = 0f;
                _glanceNext = Random.Range(3f, 7f);
            }

            var drift = Mathf.Sin(t * 0.7f + _seed) * 2.5f + Noise(t * 0.5f, _seed + 2f) * 3f;
            _yaw = Mathf.Lerp(_yaw, wantYaw + drift, 1f - Mathf.Exp(-3.2f * dt));
            _pitch = Mathf.Lerp(_pitch, wantPitch + Noise(t * 0.4f, _seed + 9f) * 1.5f, 1f - Mathf.Exp(-3.6f * dt));

            var nodAng = Mathf.Sin((1f - _nod) * Mathf.PI * 3f) * _nod * 14f;
            var shakeAng = Mathf.Sin((1f - _shake) * Mathf.PI * 5f) * _shake * 20f;
            var micro = Noise(t * 0.4f, _seed + 7f) * 1.5f;
            var walkNod = Mathf.Abs(Mathf.Cos(_walkPhase)) * _walkBlend * 1.5f;
            Rot("Neck", _pitch * 0.4f + nodAng * 0.4f + walkNod, _yaw * 0.4f + shakeAng * 0.35f, micro * 0.4f);
            Rot("Head", _pitch * 0.6f + nodAng * 0.6f, _yaw * 0.6f + shakeAng * 0.65f, micro * 0.6f);
        }
    }
}
