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
        [Tooltip("Where the extinguisher hangs in the hero's carry pose: metres in front of / above his feet, relative to his root.")]
        [SerializeField] private Vector3 carryOffset = new Vector3(0.02f, 1.02f, 0.40f);
        [SerializeField] private Vector3 carryEuler = new Vector3(8f, 0f, 0f);

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
                p.SetReaction(Sc07Reaction.None);
                p.gameObject.SetActive(false);
            }

            if (co2Extinguisher != null)
            {
                co2Extinguisher.SetParent(_co2HomeParent, true);
                co2Extinguisher.SetPositionAndRotation(_co2Home, _co2HomeRot);
                co2Extinguisher.gameObject.SetActive(true);
            }

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

        /// <summary>Editor preview: the hero holds the CO2 extinguisher in the carry (or aim) position without playing the cutscene.</summary>
        public void PreviewHold(bool aim)
        {
            var hero = Hero;
            if (hero == null || co2Extinguisher == null)
                return;
            CaptureCo2();
            co2Extinguisher.SetParent(hero.transform, true);
            var s = Mathf.Max(0.01f, hero.transform.lossyScale.y);
            var extra = aim ? new Vector3(0f, 0.08f, 0.05f) : Vector3.zero;
            co2Extinguisher.localPosition = (carryOffset + extra) / s;
            co2Extinguisher.localRotation = Quaternion.Euler(carryEuler.x - (aim ? 10f : 0f), 0f, 0f);
        }

        public IEnumerator HeroExtinguish()
        {
            var hero = Hero;
            if (hero == null)
                yield break;
            CaptureCo2();
            var firePos = fireAt != null ? fireAt.position : transform.position;
            // to the pillar, reach for the CO2 extinguisher
            if (pillarGrab != null)
                yield return hero.WalkTo(pillarGrab.position, 1.6f, 0.05f);
            if (co2Extinguisher != null)
                yield return hero.FaceRoutine(co2Extinguisher.position, 300f);
            hero.SetReach(true);
            yield return PresentableFx.Wait(0.85f);
            // take it: it hangs in front of the hero's chest, upright
            if (co2Extinguisher != null)
            {
                co2Extinguisher.SetParent(hero.transform, true);
                var s = Mathf.Max(0.01f, hero.transform.lossyScale.y);
                co2Extinguisher.localPosition = carryOffset / s;
                co2Extinguisher.localRotation = Quaternion.Euler(carryEuler);
            }

            HeroHoldsExtinguisher = true;
            hero.SetReach(false);
            hero.SetCarry(true);
            yield return PresentableFx.Wait(0.5f);

            // walk up to the fire (about two metres from it) and face it
            if (spraySpot != null)
                yield return hero.WalkTo(spraySpot.position, 1.7f, 0.05f);
            yield return hero.FaceRoutine(firePos, 280f);
            hero.SetCarry(false);
            hero.SetAim(true);
            if (co2Extinguisher != null)
            {
                co2Extinguisher.localPosition = (carryOffset + new Vector3(0f, 0.08f, 0.05f)) / Mathf.Max(0.01f, hero.transform.lossyScale.y);
                co2Extinguisher.localRotation = Quaternion.Euler(carryEuler.x - 10f, 0f, 0f);
            }

            yield return PresentableFx.Wait(0.6f);

            // CO2 jet from the horn, flames shrink and die
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
            var t = 0f;
            while (t < 5.4f)
            {
                t += Time.deltaTime;
                UpdateNozzle(hero, firePos);
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
            hero.SetAim(false);
            hero.SetCarry(true);
            if (co2Extinguisher != null)
            {
                co2Extinguisher.localPosition = carryOffset / Mathf.Max(0.01f, hero.transform.lossyScale.y);
                co2Extinguisher.localRotation = Quaternion.Euler(carryEuler);
            }

            yield return PresentableFx.Wait(1.2f);
        }

        private void UpdateNozzle(Sc07Human hero, Vector3 firePos)
        {
            if (_nozzle == null)
                return;
            var origin = co2Extinguisher != null ? co2Extinguisher.position : hero.transform.position + Vector3.up;
            var fwd = hero.transform.forward;
            // the horn tip: in front of the extinguisher and a little above its middle
            var tip = origin + fwd * 0.34f + Vector3.up * 0.14f;
            var aim = new Vector3(firePos.x, firePos.y + 0.55f, firePos.z) - tip;
            _nozzle.position = tip;
            _nozzle.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up);
        }
    }
}
