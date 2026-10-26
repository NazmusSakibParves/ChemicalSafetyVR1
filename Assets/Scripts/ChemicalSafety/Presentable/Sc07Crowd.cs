using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 story director for the people. PlayEvent(): the fire catches, the alarm rings, the hero and four workers come in
    /// through the far door one after the other on their own lanes and at their own pace, stop at irregular places and each reacts
    /// differently (shocked, hands on the head, confused, frozen); then they flee through the FIRE EXIT door, one by one, the rest
    /// in a bunch, while the hero stays beside the player. HeroExtinguish(): after the right answer the hero fetches the CO2
    /// extinguisher, walks up to the fire, sprays it out and the alarm stops.
    /// </summary>
    [DefaultExecutionOrder(100)] // after Sc07Human has posed the bones: the extinguisher follows the hands
    public sealed class Sc07Crowd : MonoBehaviour
    {
        [Tooltip("[0] is the hero, [1..4] the bystanders: head, shocked, confused, frozen.")]
        [SerializeField] private Sc07Human[] people = new Sc07Human[0];
        [SerializeField] private Transform doorStart;
        [Tooltip("Where bystanders 1..4 stop and stare (irregular places).")]
        [SerializeField] private Transform[] noticeSpots = new Transform[0];
        [SerializeField] private Transform exitPoint;
        [Tooltip("About three metres before the fire exit: the first runner opens the door here.")]
        [SerializeField] private Transform exitApproach;
        [SerializeField] private Sc07Door fleeDoor;
        [SerializeField] private Transform heroSpot;
        [SerializeField] private Transform pillarGrab;
        [SerializeField] private Transform spraySpot;
        [SerializeField] private Transform fireAt;
        [SerializeField] private Transform co2Extinguisher;
        [SerializeField] private Sc07Fire fire;
        [SerializeField] private Sc07Beacon beacon;
        [SerializeField] private Sc07Ambience ambience;
        [SerializeField] private AudioClip hiss;
        [Tooltip("Black material of the CO2 hose between the valve and the horn the hero holds.")]
        [SerializeField] private Material hoseMaterial;

        // reaction of bystander 1..4, seconds after they have stopped, speed on the way in, entry lane (z offset at the door)
        private static readonly Sc07Reaction[] Reactions = { Sc07Reaction.Head, Sc07Reaction.Shocked, Sc07Reaction.Confused, Sc07Reaction.Frozen };
        private static readonly float[] ReactDelay = { 0.4f, 0.0f, 0.9f, 0.5f };
        private static readonly float[] EntrySpeed = { 4.3f, 4.9f, 4.6f, 5.0f, 4.5f }; // they run in, hurrying to the fire
        private static readonly float[] Lane = { 0.2f, -1.1f, 0.9f, -0.4f, 0.65f };
        private static readonly float[] FaceJitter = { 10f, -18f, 0f, 22f };

        private AudioSource _hiss;
        private Transform _nozzle;
        private Vector3 _co2Home;
        private Quaternion _co2HomeRot;
        private Transform _co2HomeParent;
        private bool _co2Captured;
        // the CO2 extinguisher rig (markers made by the builder): body in the left hand, horn piece in the right
        private Transform _horn, _hornTop, _hornGrip, _hornMouth, _bodyGrip, _hornHomeParent;
        private Vector3 _hornHomeLocalPos, _hornHomeLocalScale, _valveLocal, _bodyGripLocal;
        private Quaternion _hornHomeLocalRot;
        private float _bodyBlend, _hornBlend, _sweepDeg;
        private Vector3 _fireTarget;
        private LineRenderer _hose;

        public Sc07Human Hero => people.Length > 0 ? people[0] : null;
        public int Count => people.Length;
        public bool HeroHoldsExtinguisher { get; private set; }
        public bool FleeDoorOpen => fleeDoor != null && fleeDoor.IsOpen;

        /// <summary>Raised when the last flame is out (the director plays the "fire is out" voice-over then).</summary>
        public event System.Action FireOut;

        private void Awake()
        {
            _hiss = gameObject.AddComponent<AudioSource>();
            _hiss.clip = hiss;
            _hiss.loop = true;
            _hiss.playOnAwake = false;
            _hiss.spatialBlend = 0.7f;
            _hiss.volume = 0f;
            _hiss.minDistance = 2f;
            _hiss.maxDistance = 25f;
        }

        private void CaptureCo2()
        {
            if (_co2Captured || co2Extinguisher == null)
                return;
            _co2Captured = true;
            _co2Home = co2Extinguisher.position;
            _co2HomeRot = co2Extinguisher.rotation;
            _co2HomeParent = co2Extinguisher.parent;
            // markers made by the builder: Co2Horn (cone + hose piece) with HornTop / HornGrip / HornMouth, and BodyGrip (top handle)
            _horn = FindByName(co2Extinguisher, "Co2Horn");
            if (_horn != null)
            {
                _hornTop = FindByName(_horn, "HornTop");
                _hornGrip = FindByName(_horn, "HornGrip");
                _hornMouth = FindByName(_horn, "HornMouth");
                _hornHomeParent = _horn.parent;
                _hornHomeLocalPos = _horn.localPosition;
                _hornHomeLocalRot = _horn.localRotation;
                _hornHomeLocalScale = _horn.localScale;
                if (_hornTop != null)
                    _valveLocal = co2Extinguisher.InverseTransformPoint(_hornTop.position);
            }

            _bodyGrip = FindByName(co2Extinguisher, "BodyGrip");
            if (_bodyGrip != null)
                _bodyGripLocal = co2Extinguisher.InverseTransformPoint(_bodyGrip.position);
        }

        /// <summary>Everything back to the calm start: no fire, no alarm, nobody in the hall, the door shut, the CO2 extinguisher on its wall.</summary>
        public void ResetScene()
        {
            StopAllCoroutines();
            CaptureCo2();
            if (fire != null)
                fire.SetIntensity(0f);
            if (beacon != null)
                beacon.SetOn(false);
            if (ambience != null)
            {
                ambience.SetAlarm(false);
                ambience.SetFire(0f);
            }

            if (fleeDoor != null)
                fleeDoor.CloseInstant();
            foreach (var p in people)
            {
                if (p == null)
                    continue;
                p.StopAllCoroutines();
                p.SetStartled(false);
                p.SetCarry(false);
                p.SetAim(false);
                p.SetReach(false);
                p.SetPin(false);
                p.SetHornReach(false);
                p.SetReaction(Sc07Reaction.None);
                p.gameObject.SetActive(false);
            }

            if (co2Extinguisher != null)
            {
                co2Extinguisher.SetParent(_co2HomeParent, true);
                co2Extinguisher.SetPositionAndRotation(_co2Home, _co2HomeRot);
                co2Extinguisher.gameObject.SetActive(true);
            }

            _bodyBlend = 0f;
            _hornBlend = 0f;
            _sweepDeg = 0f;
            RestoreHorn();
            if (_hose != null)
                _hose.enabled = false;
            HeroHoldsExtinguisher = false;
        }

        // ------------------------------------------------------------------ the event

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // a gently curved path: start -> a waypoint pushed sideways -> target
        private static List<Vector3> Curve(Vector3 from, Vector3 to, float side)
        {
            var mid = Vector3.Lerp(from, to, 0.5f);
            var dir = Flat(to - from);
            var perp = dir.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, dir.normalized) : Vector3.right;
            return new List<Vector3> { mid + perp * side, to };
        }

        public IEnumerator PlayEvent()
        {
            CaptureCo2();
            // the solvent can ignites, the alarm starts
            if (fire != null)
                fire.Ignite(3f);
            if (beacon != null)
                beacon.SetOn(true);
            if (ambience != null)
            {
                ambience.SetAlarm(true);
                ambience.SetFire(1f);
            }

            yield return PresentableFx.Wait(0.7f);

            var door = doorStart != null ? doorStart.position : transform.position;
            var firePos = fireAt != null ? fireAt.position : transform.position;
            var arrived = new bool[people.Length];
            // they come in one after the other, hurrying, each on a lane of his own
            for (var i = 0; i < people.Length; i++)
            {
                var p = people[i];
                if (p == null)
                    continue;
                p.gameObject.SetActive(true);
                p.SetReaction(Sc07Reaction.None);
                var start = door + new Vector3(Lane[Mathf.Min(i, Lane.Length - 1)] * 0.45f, 0f, 0f); // across the doorway
                p.transform.position = start;
                var spot = i == 0 ? (heroSpot != null ? heroSpot.position : start) : SpotOf(i - 1, start);
                p.transform.rotation = Quaternion.LookRotation(Flat(spot - start).sqrMagnitude > 0.01f ? Flat(spot - start).normalized : Vector3.right, Vector3.up);
                StartCoroutine(Arrive(i, start, spot, firePos, arrived));
                yield return PresentableFx.Wait(Random.Range(0.25f, 0.5f));
            }

            var wait = 0f;
            while (!AllTrue(arrived) && wait < 40f)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            // they stand and react, each in his own way, for a few seconds
            yield return PresentableFx.Wait(2.6f);

            // flee: the man holding his head runs first and alone, then the shocked man, then the other two together
            var gone = new bool[people.Length];
            gone[0] = true;
            if (people.Length > 1)
                StartCoroutine(RunAway(1, gone, 0f));
            if (people.Length > 2)
                StartCoroutine(RunAway(2, gone, 1.7f));
            if (people.Length > 3)
                StartCoroutine(RunAway(3, gone, 3.6f));
            if (people.Length > 4)
                StartCoroutine(RunAway(4, gone, 3.95f));

            // the hero stays calm, steadies himself and steps up next to the player while the others are running
            var hero = Hero;
            if (hero != null)
            {
                yield return PresentableFx.Wait(2.4f);
                yield return hero.FaceRoutine(firePos, 240f);
            }

            wait = 0f;
            while (!AllTrue(gone) && wait < 25f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
        }

        private Vector3 SpotOf(int bystander, Vector3 fallback)
        {
            if (noticeSpots != null && bystander >= 0 && bystander < noticeSpots.Length && noticeSpots[bystander] != null)
                return noticeSpots[bystander].position;
            return fallback;
        }

        private IEnumerator Arrive(int index, Vector3 start, Vector3 spot, Vector3 firePos, bool[] arrived)
        {
            var p = people[index];
            var speed = EntrySpeed[Mathf.Min(index, EntrySpeed.Length - 1)];
            var side = (index % 2 == 0 ? 1f : -1f) * Random.Range(0.5f, 1.2f);
            // the hero keeps to the lane and comes round the fire (never through it); the others take a gently curved line
            var path = index == 0
                ? new List<Vector3> { start + new Vector3(-0.7f, 0f, 6.8f), spot + new Vector3(-2.6f, 0f, -1.8f), spot }
                : Curve(start, spot, side);
            yield return p.WalkPath(path, speed, 0.08f, 300f);
            arrived[index] = true;
            if (index == 0)
            {
                yield return p.FaceRoutine(firePos, 240f);
                yield break;
            }

            // stops and stares at the fire (one looks at his neighbour first), then the reaction starts on its own timing
            var b = index - 1;
            var look = firePos;
            if (b == 2 && noticeSpots != null && noticeSpots.Length > 1 && noticeSpots[1] != null)
                look = noticeSpots[1].position;
            yield return p.FaceRoutine(look, 220f);
            var want = Quaternion.LookRotation(Flat(look - p.transform.position).normalized, Vector3.up) * Quaternion.Euler(0f, FaceJitter[Mathf.Min(b, FaceJitter.Length - 1)], 0f);
            p.transform.rotation = want;
            yield return PresentableFx.Wait(ReactDelay[Mathf.Min(b, ReactDelay.Length - 1)]);
            if (b == 2)
            {
                // Confused: now looks at the fire, now at the others; takes two small steps toward and away
                p.SetReaction(Sc07Reaction.Confused);
                StartCoroutine(ConfusedSteps(p, firePos));
            }
            else
            {
                p.SetReaction(Reactions[Mathf.Min(b, Reactions.Length - 1)]);
            }
        }

        private IEnumerator ConfusedSteps(Sc07Human p, Vector3 firePos)
        {
            var home = p.transform.position;
            var toward = Flat(firePos - home).normalized;
            var offsets = new[] { 0.55f, -0.35f, 0.3f, -0.5f };
            for (var k = 0; k < offsets.Length && p != null && p.Reaction == Sc07Reaction.Confused; k++)
            {
                yield return PresentableFx.Wait(0.5f);
                yield return p.WalkTo(home + toward * offsets[k], 0.7f, 0.05f, 120f, true);
            }
        }

        private IEnumerator RunAway(int index, bool[] gone, float delay)
        {
            var p = people[index];
            if (p == null)
            {
                gone[index] = true;
                yield break;
            }

            yield return PresentableFx.Wait(delay);
            // the confused one hesitates a moment more
            if (p.Reaction == Sc07Reaction.Confused)
                yield return PresentableFx.Wait(0.5f);
            p.SetReaction(Sc07Reaction.None);
            p.SetStartled(false);
            p.SetHeadYaw(0f);
            var exit = exitPoint != null ? exitPoint.position : p.transform.position + Vector3.left * 8f;
            var approach = exitApproach != null ? exitApproach.position : exit;
            // first a quick turn away from the fire, then the run: a curved line to the door, the first one opens it
            var speed = 3.2f + 0.35f * ((index * 7) % 3);
            var from = p.transform.position;
            var side = (index % 2 == 0 ? 1f : -1f) * Random.Range(0.6f, 1.3f);
            yield return p.WalkPath(Curve(from, approach, side), speed, 0.5f, 520f, false);
            if (fleeDoor != null && !fleeDoor.IsOpen)
            {
                fleeDoor.Open();
                yield return PresentableFx.Wait(0.1f);
            }

            yield return p.WalkTo(exit, speed, 0.35f, 520f, false);
            p.gameObject.SetActive(false);
            gone[index] = true;
        }

        private static bool AllTrue(bool[] a)
        {
            for (var i = 0; i < a.Length; i++)
                if (!a[i])
                    return false;
            return true;
        }

        // ------------------------------------------------------------------ the hero puts the fire out

        /// <summary>Stand the hero where the cutscene starts (beside the pillar), the director calls this behind a fade.</summary>
        public void PlaceHero(Vector3 at, Vector3 lookAt)
        {
            var hero = Hero;
            if (hero == null)
                return;
            hero.gameObject.SetActive(true);
            hero.SetStartled(false);
            hero.SetReaction(Sc07Reaction.None);
            hero.transform.position = at;
            var to = lookAt - at;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
                hero.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        /// <summary>
        /// Editor preview: the hero holds the CO2 extinguisher in the left hand (carry) or sprays with the horn in the right hand
        /// (aim) without playing the cutscene. Call after the pose (SnapPose) was set.
        /// </summary>
        public void PreviewHold(bool aim)
        {
            var hero = Hero;
            if (hero == null || co2Extinguisher == null)
                return;
            CaptureCo2();
            _fireTarget = (fireAt != null ? fireAt.position : transform.position) + Vector3.up * 0.5f;
            _bodyBlend = 1f;
            _hornBlend = aim ? 1f : 0f;
            _sweepDeg = 0f;
            ApplyRig();
        }

        public IEnumerator HeroExtinguish()
        {
            var hero = Hero;
            if (hero == null)
                yield break;
            CaptureCo2();
            var firePos = fireAt != null ? fireAt.position : transform.position;
            _fireTarget = firePos + Vector3.up * 0.5f;
            // to the pillar, reach for the CO2 extinguisher with the right hand
            if (pillarGrab != null)
                yield return hero.WalkTo(pillarGrab.position, 1.6f, 0.05f);
            if (co2Extinguisher != null)
                yield return hero.FaceRoutine(co2Extinguisher.position, 300f);
            hero.SetReach(true);
            yield return PresentableFx.Wait(0.85f);

            // it comes off the wall into the LEFT hand: the body hangs from the handle, the right arm is free again
            HeroHoldsExtinguisher = true;
            hero.SetReach(false);
            hero.SetCarry(true);
            for (var tb = 0f; tb < 0.5f; tb += Time.deltaTime)
            {
                _bodyBlend = Mathf.Clamp01(tb / 0.5f);
                yield return null;
            }

            _bodyBlend = 1f;
            yield return PresentableFx.Wait(0.4f);

            // walk up to the fire (about two metres from it) and face it
            if (spraySpot != null)
                yield return hero.WalkTo(spraySpot.position, 1.7f, 0.05f);
            yield return hero.FaceRoutine(firePos, 280f);
            yield return PresentableFx.Wait(0.25f);

            // pull the safety pin with the right hand, then take the horn off its clip on the body and stretch the hose out
            hero.SetPin(true);
            yield return PresentableFx.Wait(0.6f);
            hero.SetPin(false);
            hero.SetHornReach(true);
            yield return PresentableFx.Wait(0.55f);
            for (var th = 0f; th < 0.4f; th += Time.deltaTime)
            {
                _hornBlend = Mathf.Clamp01(th / 0.4f);
                yield return null;
            }

            _hornBlend = 1f;
            hero.SetHornReach(false);
            hero.SetAim(true);
            yield return PresentableFx.Wait(0.7f);

            // CO2 jet from the horn mouth, flames shrink and die; the hero sweeps the horn from side to side and steps a little closer
            if (_nozzle == null)
            {
                _nozzle = new GameObject("Co2Nozzle").transform;
                _nozzle.SetParent(transform, false);
            }

            fire?.StartSpray(_nozzle);
            if (_hiss != null)
            {
                _hiss.transform.position = hero.transform.position;
                _hiss.Play();
            }

            fire?.Extinguish(4.8f);
            var toFire = Flat(firePos - hero.transform.position);
            if (toFire.sqrMagnitude > 0.01f)
                StartCoroutine(hero.WalkTo(hero.transform.position + toFire.normalized * 0.5f, 0.4f, 0.05f, 90f));
            var t = 0f;
            while (t < 5.4f)
            {
                t += Time.deltaTime;
                _sweepDeg = Mathf.Sin(t * 1.5f) * 12f * Mathf.Clamp01(t / 0.8f);
                UpdateNozzle();
                if (_hiss != null)
                    _hiss.volume = Mathf.Min(0.75f, _hiss.volume + Time.deltaTime * 2f);
                if (ambience != null)
                    ambience.SetFire(Mathf.Clamp01(fire != null ? fire.Intensity : 0f));
                yield return null;
            }

            fire?.StopSpray();
            FireOut?.Invoke();
            while (_hiss != null && _hiss.volume > 0.01f)
            {
                _hiss.volume = Mathf.MoveTowards(_hiss.volume, 0f, Time.deltaTime * 2.5f);
                yield return null;
            }

            if (_hiss != null)
                _hiss.Stop();
            ambience?.SetFire(0f);
            ambience?.SetAlarm(false);
            beacon?.SetOn(false);
            // the horn goes back to its clip, the right arm drops; the body stays in the left hand
            hero.SetAim(false);
            for (var tr = 0f; tr < 0.6f; tr += Time.deltaTime)
            {
                _sweepDeg = Mathf.Lerp(_sweepDeg, 0f, 0.2f);
                _hornBlend = 1f - Mathf.Clamp01(tr / 0.6f);
                yield return null;
            }

            _hornBlend = 0f;
            _sweepDeg = 0f;
            RestoreHorn();
            UpdateHose();
            yield return PresentableFx.Wait(1.2f);
        }

        /// <summary>Metres between the hose end at the valve and the horn: more than a few centimetres while the hero holds the horn free.</summary>
        public float HornSeparation => _hornTop != null && co2Extinguisher != null
            ? Vector3.Distance(_hornTop.position, co2Extinguisher.TransformPoint(_valveLocal))
            : 0f;

        public bool HornInHand => _hornBlend > 0.99f;

        // ------------------------------------------------------------------ the rig: the extinguisher body in the left hand, the horn in the right

        private static Transform FindByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void LateUpdate()
        {
            if (_bodyBlend > 0f || _hornBlend > 0f)
                ApplyRig();
        }

        private void RestoreHorn()
        {
            if (_horn == null || _hornHomeParent == null)
                return;
            _horn.SetParent(_hornHomeParent, false);
            _horn.localPosition = _hornHomeLocalPos;
            _horn.localRotation = _hornHomeLocalRot;
            _horn.localScale = _hornHomeLocalScale;
        }

        private void ApplyRig()
        {
            var hero = Hero;
            if (hero == null || co2Extinguisher == null)
                return;
            // the body: from its place on the wall into the left hand, hanging from the handle, a slight swing
            if (_bodyBlend > 0f && _bodyGrip != null && hero.LeftSocket != null)
            {
                var rot = hero.transform.rotation * Quaternion.Euler(-6f + 1.5f * Mathf.Sin(Time.time * 1.3f), 0f, 0f);
                var pos = hero.LeftSocket.position - rot * _bodyGripLocal;
                var k = Mathf.SmoothStep(0f, 1f, _bodyBlend);
                co2Extinguisher.SetPositionAndRotation(Vector3.Lerp(_co2Home, pos, k), Quaternion.Slerp(_co2HomeRot, rot, k));
            }

            // the horn: rigid piece (cone + hose) that leaves the body and is held by the right hand, aimed at the fire
            if (_horn != null && _hornGrip != null && _hornMouth != null && _hornTop != null && hero.RightSocket != null)
            {
                if (_hornBlend > 0f)
                {
                    var axis = (_hornMouth.localPosition - _hornTop.localPosition).normalized;
                    var hand = hero.RightSocket.position;
                    var dir = (_fireTarget - hand).normalized;
                    dir = Quaternion.AngleAxis(_sweepDeg, Vector3.up) * dir;
                    var heldRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.FromToRotation(axis, Vector3.forward);
                    var restPos = co2Extinguisher.TransformPoint(_hornHomeLocalPos);
                    var restRot = co2Extinguisher.rotation * _hornHomeLocalRot;
                    var k = Mathf.SmoothStep(0f, 1f, _hornBlend);
                    // position the piece so that its grip point sits in the hand
                    var rotNow = Quaternion.Slerp(restRot, heldRot, k);
                    _horn.rotation = rotNow;
                    _horn.position = restPos;
                    var gripShift = hand - _hornGrip.position;
                    _horn.position = restPos + gripShift * k;
                }
                else if (_horn.parent == _hornHomeParent)
                {
                    _horn.localPosition = _hornHomeLocalPos;
                    _horn.localRotation = _hornHomeLocalRot;
                }
            }

            UpdateHose();
        }

        // A black hose from the valve on the body to the top of the horn piece (a sagging Bezier); hidden while the horn sits on the body.
        private void UpdateHose()
        {
            if (_hornTop == null || co2Extinguisher == null || hoseMaterial == null)
                return;
            var p0 = co2Extinguisher.TransformPoint(_valveLocal);
            var p3 = _hornTop.position;
            var show = _hornBlend > 0.02f && Vector3.Distance(p0, p3) > 0.04f;
            if (_hose == null)
            {
                var go = new GameObject("Co2Hose");
                go.transform.SetParent(transform, false);
                _hose = go.AddComponent<LineRenderer>();
                _hose.useWorldSpace = true;
                _hose.positionCount = 14;
                _hose.widthMultiplier = 0.024f;
                _hose.numCapVertices = 4;
                _hose.numCornerVertices = 3;
                _hose.sharedMaterial = hoseMaterial;
                _hose.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _hose.receiveShadows = false;
            }

            _hose.enabled = show;
            if (!show)
                return;
            var heroT = Hero != null ? Hero.transform : transform;
            var p1 = p0 + heroT.right * 0.10f + Vector3.up * 0.04f;
            var p2 = p3 - (_hornMouth != null ? (_hornMouth.position - _hornTop.position).normalized : heroT.forward) * 0.12f;
            for (var i = 0; i < 14; i++)
            {
                var u = i / 13f;
                var a = 1f - u;
                var p = a * a * a * p0 + 3f * a * a * u * p1 + 3f * a * u * u * p2 + u * u * u * p3;
                p += Vector3.down * (0.09f * Mathf.Sin(u * Mathf.PI) * Mathf.Clamp01(_hornBlend * 1.5f));
                _hose.SetPosition(i, p);
            }
        }

        // The jet leaves the horn mouth along the horn's axis (the horn is aimed at the fire by the rig).
        private void UpdateNozzle()
        {
            if (_nozzle == null)
                return;
            if (_hornMouth != null && _hornTop != null)
            {
                _nozzle.position = _hornMouth.position;
                var dir = _hornMouth.position - _hornTop.position;
                if (dir.sqrMagnitude > 1e-6f)
                    _nozzle.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                return;
            }

            var hero = Hero;
            var tip = (co2Extinguisher != null ? co2Extinguisher.position : Vector3.zero) + (hero != null ? hero.transform.forward * 0.34f : Vector3.zero);
            _nozzle.position = tip;
            _nozzle.rotation = Quaternion.LookRotation((_fireTarget - tip).normalized, Vector3.up);
        }
    }
}
