using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>How a bystander reacts to the fire (Scenario 7): each worker plays a different one.</summary>
    public enum Sc07Reaction
    {
        None,
        /// <summary>Both hands to the mouth, a stiff step back.</summary>
        Shocked,
        /// <summary>Both hands on the head, turned half away.</summary>
        Head,
        /// <summary>Does not know what to do: looks left and right, a hand at the chin, shifts his feet.</summary>
        Confused,
        /// <summary>Stands frozen and stares, arms down.</summary>
        Frozen
    }

    /// <summary>
    /// Scenario 7 worker, animated by code (no clips): idle with glances and weight shifts, a walk and a run cycle driven by
    /// the real ground speed (pelvis against shoulders, hip sway and bounce, knee flexion in stance and swing, heel-to-toe foot,
    /// forearms lagging the upper arms, a steady head), a personal style per worker (stride, swing, lean), four reaction poses,
    /// carrying / aiming / reaching poses for the hero, and boot steps on concrete played on the foot plants. The skeleton is made
    /// by the builder (bones are unrotated in the bind pose, +Z forward, R = +X), so a bone's pose is `root rotation * delta`.
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
        private float _pin, _pinT, _hornReach, _hornReachT;
        private float _shock, _headHands, _confused, _frozen;
        private Sc07Reaction _reaction;
        private float _reactionClock;
        private float _headYaw, _headYawT;
        private Vector3 _lastPos;
        private int _lastStep;
        private AudioSource _audio;
        private float _seed;
        private bool _ready;
        // personal style (from the seed)
        private float _stride = 1f, _swing = 1f, _lean = 0f, _bounce = 1f, _armLag = 0.2f;

        public float Speed => _speed;
        public Sc07Reaction Reaction => _reaction;
        public Transform Bone(string name) => _b.TryGetValue(name, out var t) ? t : null;

        /// <summary>Socket on the right hand where the hero holds the extinguisher.</summary>
        public Transform RightSocket => Bone("HandR_Socket");

        /// <summary>Socket on the left hand: the extinguisher hangs from it by its handle.</summary>
        public Transform LeftSocket => Bone("HandL_Socket");

        public void SetStartled(bool on) => _startleT = on ? 1f : 0f;
        public void SetCarry(bool on) => _carryT = on ? 1f : 0f;
        public void SetAim(bool on) => _aimT = on ? 1f : 0f;
        public void SetReach(bool on) => _reachT = on ? 1f : 0f;

        /// <summary>Right hand comes across to the valve (pulls the safety pin).</summary>
        public void SetPin(bool on) => _pinT = on ? 1f : 0f;

        /// <summary>Right hand goes to the horn that hangs on the right side of the extinguisher.</summary>
        public void SetHornReach(bool on) => _hornReachT = on ? 1f : 0f;

        /// <summary>Starts (or ends, with None) a bystander reaction; the pose blends in and out smoothly.</summary>
        public void SetReaction(Sc07Reaction r)
        {
            if (r != _reaction)
                _reactionClock = 0f;
            _reaction = r;
        }

        /// <summary>Head turn (degrees, + = to the character's right) on top of the body.</summary>
        public void SetHeadYaw(float degrees) => _headYawT = Mathf.Clamp(degrees, -70f, 70f);

        /// <summary>Sets every pose weight directly and poses the bones once (editor previews and tests).</summary>
        public void SnapPose(float walk, float run, float startle, float carry, float aim, float reach, float phase, Sc07Reaction reaction = Sc07Reaction.None)
        {
            Init();
            _walk = walk;
            _run = run;
            _startle = _startleT = startle;
            _carry = _carryT = carry;
            _aim = _aimT = aim;
            _reach = _reachT = reach;
            _pin = _pinT = 0f;
            _hornReach = _hornReachT = 0f;
            _reaction = reaction;
            _shock = reaction == Sc07Reaction.Shocked ? 1f : 0f;
            _headHands = reaction == Sc07Reaction.Head ? 1f : 0f;
            _confused = reaction == Sc07Reaction.Confused ? 1f : 0f;
            _frozen = reaction == Sc07Reaction.Frozen ? 1f : 0f;
            _speed = walk > 0f ? Mathf.Max(1.2f, run * 4f) : 0f;
            Evaluate(phase, 1.3f);
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
            // no two workers walk alike
            _stride = Mathf.Lerp(0.9f, 1.12f, Frac(_seed * 1.7f));
            _swing = Mathf.Lerp(0.75f, 1.25f, Frac(_seed * 2.9f));
            _lean = Mathf.Lerp(-2f, 3.5f, Frac(_seed * 4.3f));
            _bounce = Mathf.Lerp(0.8f, 1.3f, Frac(_seed * 5.1f));
            _armLag = Mathf.Lerp(0.12f, 0.3f, Frac(_seed * 6.7f));
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

        private static float Frac(float x) => x - Mathf.Floor(x);

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
            _run = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_speed - 1.9f) / 1.1f)); // a crisp walk -> run change, no half-jog
            _startle = Mathf.MoveTowards(_startle, _startleT, dt * 4f);
            _carry = Mathf.MoveTowards(_carry, _carryT, dt * 3f);
            _aim = Mathf.MoveTowards(_aim, _aimT, dt * 3f);
            _reach = Mathf.MoveTowards(_reach, _reachT, dt * 3.5f);
            _pin = Mathf.MoveTowards(_pin, _pinT, dt * 3f);
            _hornReach = Mathf.MoveTowards(_hornReach, _hornReachT, dt * 3f);
            _headYaw = Mathf.MoveTowards(_headYaw, _headYawT, dt * 160f);
            _reactionClock += dt;
            _shock = Mathf.MoveTowards(_shock, _reaction == Sc07Reaction.Shocked ? 1f : 0f, dt * 3.2f);
            _headHands = Mathf.MoveTowards(_headHands, _reaction == Sc07Reaction.Head ? 1f : 0f, dt * 2.6f);
            _confused = Mathf.MoveTowards(_confused, _reaction == Sc07Reaction.Confused ? 1f : 0f, dt * 2.2f);
            _frozen = Mathf.MoveTowards(_frozen, _reaction == Sc07Reaction.Frozen ? 1f : 0f, dt * 4f);
            var cycleLen = Mathf.Lerp(1.45f, 2.75f, _run) * _stride;
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
            var TAU = Mathf.PI * 2f;
            var s0 = Mathf.Sin(phase * TAU);
            var c0 = Mathf.Cos(phase * TAU);
            var w = _walk;
            var r = _run;
            var moving = Mathf.Clamp01(_speed);
            var thighSwing = Mathf.Lerp(23f * w, 54f, r) * _stride;
            var kneeMax = Mathf.Lerp(52f * w, 108f, r);
            var armSwing = Mathf.Lerp(16f * w, 52f, r) * _swing;
            var elbowBase = Mathf.Lerp(8f + 8f * w, 92f, r);
            var lean = r * 15f + w * 2.5f + _lean * w;
            var breathe = Mathf.Sin(time * 1.9f + _seed) * (1f - w);
            var still = 1f - Mathf.Clamp01(_walk * 2f);

            // ---- legs (forward angle positive). Knee: a little flexion when the foot takes the weight, a big one in the swing
            var thighR = thighSwing * s0;
            var thighL = -thighSwing * s0;
            var kneeR = kneeMax * Mathf.Max(0f, Mathf.Cos(phase * TAU + 0.45f)) + 9f * w * Mathf.Max(0f, -Mathf.Cos(phase * TAU + 0.35f));
            var kneeL = kneeMax * Mathf.Max(0f, Mathf.Cos(phase * TAU + Mathf.PI + 0.45f)) + 9f * w * Mathf.Max(0f, -Mathf.Cos(phase * TAU + Mathf.PI + 0.35f));
            // reactions on the legs: stiff step back (shocked), feet shifting (confused), a small step back (frozen)
            thighR -= 13f * _shock + 5f * _frozen * Mathf.Min(1f, _reactionClock * 3f);
            thighL += 7f * _shock + 2f * _frozen;
            kneeR += 8f * _shock;
            var shuffle = Mathf.Sin(time * 2.1f + _seed) * 6f * _confused;
            thighR += shuffle;
            thighL -= shuffle * 0.8f;
            kneeL += 6f * Mathf.Max(0f, Mathf.Sin(time * 2.1f + _seed + 1.2f)) * _confused;
            // feet: toes up at the heel strike, rolling over, toes down at the push-off
            var footR = s0 > 0f ? 10f * s0 : 26f * s0;
            var footL = -s0 > 0f ? 10f * -s0 : 26f * -s0;
            footR *= w;
            footL *= w;
            SetBone("ThighR", root * Pitch(thighR));
            SetBone("ThighL", root * Pitch(thighL));
            SetBone("ShinR", root * Pitch(thighR - kneeR));
            SetBone("ShinL", root * Pitch(thighL - kneeL));
            SetBone("FootR", root * Pitch(footR - kneeR * 0.12f));
            SetBone("FootL", root * Pitch(footL - kneeL * 0.12f));

            // ---- arms: gait swing (forearm lags the upper arm), then the overlay poses
            var lag = _armLag * TAU;
            var armPitchR = -armSwing * s0;
            var armPitchL = armSwing * s0;
            var elbowR = elbowBase + armSwing * 0.55f * (0.5f + 0.5f * Mathf.Sin(phase * TAU + lag + 0.4f));
            var elbowL = elbowBase + armSwing * 0.55f * (0.5f + 0.5f * Mathf.Sin(phase * TAU + Mathf.PI + lag + 0.4f));
            var inR = 0f;
            var inL = 0f;

            void Arms(float wgt, float pR, float eR, float iR, float pL, float eL, float iL, float wgtL = -1f)
            {
                if (wgtL < 0f)
                    wgtL = wgt;
                if (wgt > 0f)
                {
                    armPitchR = Mathf.Lerp(armPitchR, pR, wgt);
                    elbowR = Mathf.Lerp(elbowR, eR, wgt);
                    inR = Mathf.Lerp(inR, iR, wgt);
                }

                if (wgtL > 0f)
                {
                    armPitchL = Mathf.Lerp(armPitchL, pL, wgtL);
                    elbowL = Mathf.Lerp(elbowL, eL, wgtL);
                    inL = Mathf.Lerp(inL, iL, wgtL);
                }
            }

            float Ease(float x) => x * x * (3f - 2f * x);
            // the second arm follows the first a moment later (people never move both hands at once)
            float Follow(float x, float lag) => Ease(Mathf.Clamp01((x - lag) / (1f - lag)));

            Arms(_startle, 62f, 82f, 6f, 66f, 86f, 6f);
            // shocked: one hand flies to the mouth, the other follows; a slight tremble
            var trem = Mathf.Sin(time * 9f + _seed) * 2.2f * _shock;
            Arms(Ease(_shock), 74f + trem, 152f + trem, 22f, 66f - trem, 144f - trem, 30f, Follow(_shock, 0.35f));
            // hands on the head: right first, then left, elbows swaying a little
            var headSway = Mathf.Sin(time * 1.7f + _seed) * 4f * _headHands;
            Arms(Ease(_headHands), 158f + headSway, 110f - headSway, 8f, 152f - headSway, 118f + headSway, 10f, Follow(_headHands, 0.45f));
            // confused: one hand at the chin (the finger taps), the other lifts and drops in a shrug
            var chinFidget = Mathf.Sin(time * 1.3f + _seed) * 5f + Mathf.Sin(time * 4.1f + _seed) * 2f;
            var shrug = (0.5f + 0.5f * Mathf.Sin(time * 0.9f + _seed)) * 26f;
            Arms(Ease(_confused), 70f + chinFidget, 142f + chinFidget * 0.5f, 20f, 14f + shrug, 36f + shrug * 0.7f, -8f, Follow(_confused, 0.3f));
            // frozen: arms down, a little stiff
            Arms(_frozen, 4f, 12f, 3f, 4f, 12f, 3f);
            // hero: the extinguisher hangs from the left hand by its handle, the right arm swings free
            Arms(0f, 0f, 0f, 0f, 18f, 62f, 14f, Ease(_carry));
            // right hand comes across to the valve (safety pin), then down to the horn on the right side of the body
            Arms(Ease(_pin), 60f, 108f, 24f, 0f, 0f, 0f, 0f);
            Arms(Ease(_hornReach), 36f, 62f, 6f, 0f, 0f, 0f, 0f);
            // aiming: the left hand keeps the body, the right arm is stretched out at the fire holding the horn
            Arms(Ease(_aim), 80f, 14f, -4f, 18f, 62f, 14f, Ease(_aim));
            // reaching for the extinguisher on the wall with the right hand
            if (_reach > 0f)
            {
                armPitchR = Mathf.Lerp(armPitchR, 84f, _reach);
                elbowR = Mathf.Lerp(elbowR, 18f, _reach);
            }

            var inRq = Quaternion.AngleAxis(-inR, Vector3.forward);
            var inLq = Quaternion.AngleAxis(inL, Vector3.forward);
            SetBone("UpperArmR", root * Pitch(armPitchR) * inRq * _armDownR);
            SetBone("UpperArmL", root * Pitch(armPitchL) * inLq * _armDownL);
            SetBone("ForeArmR", root * Pitch(armPitchR + elbowR) * inRq * _armDownR);
            SetBone("ForeArmL", root * Pitch(armPitchL + elbowL) * inLq * _armDownL);
            SetBone("HandR", root * Pitch(armPitchR + elbowR) * inRq * _armDownR);
            SetBone("HandL", root * Pitch(armPitchL + elbowL) * inLq * _armDownL);

            // ---- torso: the shoulders turn against the pelvis, lean into the run, back when startled
            var hipTwist = -Mathf.Lerp(4f, 8f, r) * w * s0;
            var shoulderTwist = Mathf.Lerp(6f, 12f, r) * w * s0 * _swing;
            var roll = 2.5f * w * s0;
            var leanTot = lean - 9f * _startle - 7f * _shock - 6f * _headHands - 3f * _frozen + 5f * _aim + breathe * 0.8f;
            var bodyTurn = 24f * _headHands + 6f * Mathf.Sin(time * 0.7f + _seed) * _confused;
            SetBone("Spine", root * Quaternion.AngleAxis(shoulderTwist * 0.5f + bodyTurn * 0.4f, Vector3.up) * Quaternion.AngleAxis(roll * 0.5f, Vector3.forward) * Pitch(-leanTot * 0.5f));
            SetBone("Chest", root * Quaternion.AngleAxis(shoulderTwist + bodyTurn * 0.6f, Vector3.up) * Quaternion.AngleAxis(roll, Vector3.forward) * Pitch(-leanTot));
            // standing people are never statues: they glance about, except the frozen one; the confused one looks left and right
            var glanceScale = still * (1f - _frozen) * (1f - _confused) * (1f - _shock * 0.8f) * (1f - _startle * 0.7f);
            var glance = (Mathf.Sin(time * 0.45f + _seed * 3f) + 0.5f * Mathf.Sin(time * 1.1f + _seed)) * 9f * glanceScale;
            var lookAround = Mathf.Sin(time * 2.4f + _seed) * 42f * _confused * Mathf.Clamp01(Mathf.Sin(time * 0.8f + _seed) * 2f + 0.5f);
            var headYaw = _headYaw + glance + lookAround - 14f * _headHands;
            var headSteady = 0.1f; // the head hardly follows the shoulder twist
            SetBone("Neck", root * Quaternion.AngleAxis(shoulderTwist * -0.2f + headYaw * 0.4f, Vector3.up) * Pitch(leanTot * 0.6f));
            SetBone("Head", root * Quaternion.AngleAxis(shoulderTwist * headSteady + headYaw * 0.6f, Vector3.up) *
                            Pitch(leanTot * 0.5f + 6f * _startle + 5f * _shock + 8f * _headHands + 2.5f * Mathf.Sin(time * 0.8f + _seed) * still));

            // ---- hips: bounce at the foot plant (lowest in double support), sway over the standing leg, a little pitch
            var hips = Bone("Hips");
            if (hips != null && _restPos.TryGetValue("Hips", out var rp))
            {
                var unit = 1f / Mathf.Max(0.01f, transform.lossyScale.y);
                var drop = Mathf.Lerp(0.016f * Mathf.Abs(s0), 0.06f * Mathf.Abs(c0), r) * _bounce * moving; // walk: lowest with both feet down; run: lowest at mid-stance
                var sway = 0.017f * _bounce * s0 * w * (1f - r * 0.4f);
                hips.localPosition = rp + new Vector3(sway * unit, (-drop - 0.02f * r - 0.012f * _shock) * unit, 0f);
                var shift = Mathf.Sin(time * 0.6f + _seed) * 1.8f * still + 2.2f * Mathf.Sin(time * 0.9f + _seed) * _confused;
                hips.rotation = root * Quaternion.AngleAxis(hipTwist, Vector3.up) * Quaternion.AngleAxis(shift - 2f * s0 * w, Vector3.forward) * Pitch(-lean * 0.3f);
            }
        }

        private void SetBone(string name, Quaternion world)
        {
            if (_b.TryGetValue(name, out var t))
                t.rotation = world;
        }

        // ------------------------------------------------------------------ movement helpers (the crowd and the director drive these)

        /// <summary>
        /// Walks or runs (metres per second) to a floor point, turning toward it. Starts with a short acceleration, a little speed
        /// noise so it is never a robot's constant pace, and (for walking) slows down for the last metre; ends within 'stop' metres.
        /// </summary>
        public IEnumerator WalkTo(Vector3 target, float speed, float stop = 0.05f, float turnRate = 280f, bool slowEnd = true)
        {
            var cur = Mathf.Min(_speed, speed);
            var accel = speed / 0.6f;
            while (true)
            {
                var to = target - transform.position;
                to.y = 0f;
                var dist = to.magnitude;
                if (dist <= stop)
                    break;
                var dt = Time.deltaTime;
                var noise = 1f + 0.1f * (Mathf.PerlinNoise(Time.time * 0.6f, _seed) - 0.5f) * 2f;
                var cap = speed * noise;
                if (slowEnd)
                    cap = Mathf.Min(cap, Mathf.Max(0.7f, dist * (speed < 2.2f ? 1.6f : 2.4f)));
                cur = Mathf.MoveTowards(cur, cap, accel * dt);
                var step = Mathf.Min(dist, cur * dt);
                var want = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * dt);
                transform.position += to.normalized * step;
                yield return null;
            }
        }

        /// <summary>Walks along a curved path of waypoints (no sharp corners: the heading turns smoothly).</summary>
        public IEnumerator WalkPath(IList<Vector3> points, float speed, float stop = 0.08f, float turnRate = 200f, bool slowEnd = true)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var last = i == points.Count - 1;
                yield return WalkTo(points[i], speed, last ? stop : 0.45f, turnRate, last && slowEnd);
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
