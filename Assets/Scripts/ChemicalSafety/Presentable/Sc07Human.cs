using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 worker, animated by code (no clips): idle breathing, a walk and a run cycle driven by the real ground speed
    /// (hips, knees, arms, torso lean, hip bob), a startled pose, carrying / aiming / reaching poses for the hero, and boot
    /// steps on concrete played on the foot plants. The skeleton is made by the builder (Sc07 human rig; bones are unrotated
    /// in the bind pose, +Z forward, R = +X), so a bone's pose is simply `root rotation * delta`.
    /// </summary>
    public sealed class Sc07Human : MonoBehaviour
    {
        [SerializeField] private AudioClip[] stepWalk = new AudioClip[0];
        [SerializeField] private AudioClip[] stepRun = new AudioClip[0];
        [SerializeField] private float stepVolume = 1f;

        private readonly Dictionary<string, Transform> _b = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Vector3> _restPos = new Dictionary<string, Vector3>();
        private Quaternion _armDownR, _armDownL;
        private float _phase;
        private float _speed;
        private float _walk;
        private float _run;
        private float _startle, _startleT;
        private float _carry, _carryT;
        private float _aim, _aimT;
        private float _reach, _reachT;
        private float _headYaw, _headYawT;
        private Vector3 _lastPos;
        private int _lastStep;
        private AudioSource _audio;
        private float _seed;
        private bool _ready;

        public float Speed => _speed;
        public Transform Bone(string name) => _b.TryGetValue(name, out var t) ? t : null;

        /// <summary>Socket on the right hand where the hero holds the extinguisher.</summary>
        public Transform RightSocket => Bone("HandR_Socket");

        public void SetStartled(bool on) => _startleT = on ? 1f : 0f;
        public void SetCarry(bool on) => _carryT = on ? 1f : 0f;
        public void SetAim(bool on) => _aimT = on ? 1f : 0f;
        public void SetReach(bool on) => _reachT = on ? 1f : 0f;

        /// <summary>Head turn (degrees, + = to the character's right) on top of the body.</summary>
        public void SetHeadYaw(float degrees) => _headYawT = Mathf.Clamp(degrees, -70f, 70f);

        /// <summary>Sets every pose weight directly and poses the bones once (editor previews and tests).</summary>
        public void SnapPose(float walk, float run, float startle, float carry, float aim, float reach, float phase)
        {
            Init();
            _walk = walk;
            _run = run;
            _startle = _startleT = startle;
            _carry = _carryT = carry;
            _aim = _aimT = aim;
            _reach = _reachT = reach;
            _speed = walk > 0f ? Mathf.Max(1.2f, run * 4f) : 0f;
            Evaluate(phase, 0f);
        }

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (_ready && _b.Count > 0)
                return;
            _b.Clear();
            _restPos.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                _b[t.name] = t;
            foreach (var kv in _b)
                _restPos[kv.Key] = kv.Value.localPosition;
            _armDownR = ArmDown("UpperArmR", "ForeArmR", 1f);
            _armDownL = ArmDown("UpperArmL", "ForeArmL", -1f);
            _lastPos = transform.position;
            _seed = Random.value * 10f;
            _audio = gameObject.GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 1.5f;
            _audio.maxDistance = 22f;
            _ready = true;
        }

        private Quaternion ArmDown(string upper, string lower, float side)
        {
            if (!_b.ContainsKey(upper) || !_b.ContainsKey(lower))
                return Quaternion.identity;
            // local offset of the child bone = the arm direction in the T-pose (bind pose bones are unrotated), whatever the current pose
            var rest = _b[lower].localPosition.normalized;
            var down = new Vector3(side * 0.10f, -1f, 0.03f).normalized;
            return Quaternion.FromToRotation(rest, down);
        }

        private static Quaternion Pitch(float forwardDeg) => Quaternion.AngleAxis(-forwardDeg, Vector3.right);

        private void LateUpdate()
        {
            if (!_ready || _b.Count == 0)
                Init();
            var dt = Mathf.Max(0.0001f, Time.deltaTime);
            var moved = transform.position - _lastPos;
            moved.y = 0f;
            _lastPos = transform.position;
            var v = moved.magnitude / dt;
            _speed = Mathf.Lerp(_speed, Mathf.Min(v, 7f), 1f - Mathf.Exp(-9f * dt));
            _walk = Mathf.Clamp01(_speed / 1.3f);
            _run = Mathf.Clamp01((_speed - 2.2f) / 1.8f);
            _startle = Mathf.MoveTowards(_startle, _startleT, dt * 4f);
            _carry = Mathf.MoveTowards(_carry, _carryT, dt * 3f);
            _aim = Mathf.MoveTowards(_aim, _aimT, dt * 3f);
            _reach = Mathf.MoveTowards(_reach, _reachT, dt * 3.5f);
            _headYaw = Mathf.MoveTowards(_headYaw, _headYawT, dt * 160f);
            var cycleLen = Mathf.Lerp(1.45f, 2.7f, _run);
            if (_speed > 0.05f)
                _phase = Mathf.Repeat(_phase + v * dt / cycleLen, 1f);
            Evaluate(_phase, Time.time);
            // foot plants: the leading foot hits the ground at phase 0.25 (right) and 0.75 (left)
            var half = Mathf.FloorToInt((_phase - 0.25f) * 2f + 4f);
            if (half != _lastStep)
            {
                _lastStep = half;
                if (_speed > 0.45f)
                    PlayStep();
            }
        }

        private void PlayStep()
        {
            var run = _run > 0.4f;
            var set = run ? stepRun : stepWalk;
            if (set == null || set.Length == 0 || _audio == null)
                return;
            _audio.pitch = Random.Range(0.9f, 1.1f);
            _audio.PlayOneShot(set[Random.Range(0, set.Length)], (run ? 0.9f : 0.55f) * stepVolume);
        }

        /// <summary>Poses every bone for a gait phase and the current overlay weights (also callable from editor previews).</summary>
        public void Evaluate(float phase, float time)
        {
            if (!_ready || _b.Count == 0)
                Init();
            var root = transform.rotation;
            var s0 = Mathf.Sin(phase * Mathf.PI * 2f);
            var c0 = Mathf.Cos(phase * Mathf.PI * 2f);
            var w = _walk;
            var r = _run;
            var thighSwing = Mathf.Lerp(24f * w, 56f, r);
            var kneeMax = Mathf.Lerp(34f * w, 105f, r);
            var armSwing = Mathf.Lerp(17f * w, 50f, r);
            var elbowBase = Mathf.Lerp(7f + 6f * w, 88f, r);
            var lean = r * 15f + w * 2f;
            var breathe = Mathf.Sin(time * 1.9f + _seed) * (1f - w) ;

            // legs (forward angle positive)
            var thighR = thighSwing * s0;
            var thighL = -thighSwing * s0;
            var kneeR = kneeMax * Mathf.Max(0f, c0);
            var kneeL = kneeMax * Mathf.Max(0f, -c0);
            // a stiff startle step back
            thighR -= 10f * _startle;
            thighL += 6f * _startle;
            SetBone("ThighR", root * Pitch(thighR));
            SetBone("ThighL", root * Pitch(thighL));
            SetBone("ShinR", root * Pitch(thighR - kneeR));
            SetBone("ShinL", root * Pitch(thighL - kneeL));
            SetBone("FootR", root * Pitch(-kneeR * 0.25f));
            SetBone("FootL", root * Pitch(-kneeL * 0.25f));

            // arms: gait swing, then overlay poses
            var armPitchR = -armSwing * s0;
            var armPitchL = armSwing * s0;
            var elbowR = elbowBase * (0.65f + 0.35f * Mathf.Max(0f, armPitchR / Mathf.Max(1f, armSwing)));
            var elbowL = elbowBase * (0.65f + 0.35f * Mathf.Max(0f, armPitchL / Mathf.Max(1f, armSwing)));
            // startled: both hands up in front of the chest
            armPitchR = Mathf.Lerp(armPitchR, 62f, _startle);
            armPitchL = Mathf.Lerp(armPitchL, 66f, _startle);
            elbowR = Mathf.Lerp(elbowR, 82f, _startle);
            elbowL = Mathf.Lerp(elbowL, 86f, _startle);
            // carrying the extinguisher at the hip / chest, left hand under the horn
            armPitchR = Mathf.Lerp(armPitchR, 38f, _carry);
            armPitchL = Mathf.Lerp(armPitchL, 48f, _carry);
            elbowR = Mathf.Lerp(elbowR, 78f, _carry);
            elbowL = Mathf.Lerp(elbowL, 70f, _carry);
            // aiming: arms forward
            armPitchR = Mathf.Lerp(armPitchR, 66f, _aim);
            armPitchL = Mathf.Lerp(armPitchL, 74f, _aim);
            elbowR = Mathf.Lerp(elbowR, 52f, _aim);
            elbowL = Mathf.Lerp(elbowL, 30f, _aim);
            // reaching for the extinguisher on the wall with the right hand
            armPitchR = Mathf.Lerp(armPitchR, 84f, _reach);
            elbowR = Mathf.Lerp(elbowR, 18f, _reach);
            // arms turned in toward the body's centre line (hands meet on the extinguisher)
            var inward = 6f * _startle + 25f * _carry + 16f * _aim;
            var inR = Quaternion.AngleAxis(-inward, Vector3.forward);
            var inL = Quaternion.AngleAxis(inward, Vector3.forward);
            SetBone("UpperArmR", root * Pitch(armPitchR) * inR * _armDownR);
            SetBone("UpperArmL", root * Pitch(armPitchL) * inL * _armDownL);
            SetBone("ForeArmR", root * Pitch(armPitchR + elbowR) * inR * _armDownR);
            SetBone("ForeArmL", root * Pitch(armPitchL + elbowL) * inL * _armDownL);
            SetBone("HandR", root * Pitch(armPitchR + elbowR) * inR * _armDownR);
            SetBone("HandL", root * Pitch(armPitchL + elbowL) * inL * _armDownL);

            // torso: lean into the run, back when startled, twist against the legs, breathing; head turn on top
            var twist = (thighSwing * 0.18f) * s0;
            var leanTot = lean - 9f * _startle + 5f * _aim + breathe * 0.8f;
            SetBone("Spine", root * Quaternion.AngleAxis(twist * 0.5f, Vector3.up) * Pitch(-leanTot * 0.5f));
            SetBone("Chest", root * Quaternion.AngleAxis(twist, Vector3.up) * Pitch(-leanTot));
            // standing people are never statues: they glance about and shift their weight
            var still = 1f - Mathf.Clamp01(_walk * 2f);
            var glance = (Mathf.Sin(time * 0.45f + _seed * 3f) + 0.5f * Mathf.Sin(time * 1.1f + _seed)) * 9f * still * (1f - _startle * 0.7f);
            var headYaw = _headYaw + glance;
            SetBone("Neck", root * Quaternion.AngleAxis(twist * 0.6f + headYaw * 0.4f, Vector3.up) * Pitch(leanTot * 0.6f));
            SetBone("Head", root * Quaternion.AngleAxis(twist * 0.3f + headYaw * 0.6f, Vector3.up) * Pitch(leanTot * 0.5f + 6f * _startle + 2.5f * Mathf.Sin(time * 0.8f + _seed) * still));

            // hips bob and a little tilt (in model units: the root scale makes it metres)
            var hips = Bone("Hips");
            if (hips != null && _restPos.TryGetValue("Hips", out var rp))
            {
                var bob = (Mathf.Lerp(0.02f, 0.07f, r) * Mathf.Abs(c0) * Mathf.Clamp01(_speed)) / Mathf.Max(0.01f, transform.lossyScale.y);
                hips.localPosition = rp + new Vector3(0f, -bob - 0.02f * r, 0f);
                var shift = Mathf.Sin(time * 0.6f + _seed) * 1.8f * still;
                hips.rotation = root * Quaternion.AngleAxis(-thighSwing * 0.08f * s0, Vector3.up) * Quaternion.AngleAxis(shift, Vector3.forward) * Pitch(-lean * 0.3f);
            }
        }

        private void SetBone(string name, Quaternion world)
        {
            if (_b.TryGetValue(name, out var t))
                t.rotation = world;
        }

        // ------------------------------------------------------------------ movement helpers (the crowd and the director drive these)

        /// <summary>Walks or runs (metres per second) to a floor point, turning toward it; ends within 'stop' metres.</summary>
        public IEnumerator WalkTo(Vector3 target, float speed, float stop = 0.05f, float turnRate = 280f)
        {
            while (true)
            {
                var to = target - transform.position;
                to.y = 0f;
                var dist = to.magnitude;
                if (dist <= stop)
                    break;
                var dt = Time.deltaTime;
                var step = Mathf.Min(dist, speed * dt);
                var want = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * dt);
                transform.position += to.normalized * step;
                yield return null;
            }
        }

        /// <summary>Turns on the spot toward a world point.</summary>
        public IEnumerator FaceRoutine(Vector3 point, float degPerSecond = 220f)
        {
            while (true)
            {
                var to = point - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f)
                    yield break;
                var want = Quaternion.LookRotation(to.normalized, Vector3.up);
                if (Quaternion.Angle(transform.rotation, want) < 1f)
                    yield break;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degPerSecond * Time.deltaTime);
                yield return null;
            }
        }
    }
}
