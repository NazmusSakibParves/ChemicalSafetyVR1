using System.Collections;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 story director for the people. PlayEvent(): the fire catches, the alarm rings, five workers and the hero
    /// walk in from the far door, see the fire, startle; the five run for the exit, the hero stays beside the player.
    /// HeroExtinguish(): after the right answer the hero fetches the CO2 extinguisher from the pillar, walks up to the fire,
    /// sprays it out and the alarm stops.
    /// </summary>
    public sealed class Sc07Crowd : MonoBehaviour
    {
        [Tooltip("[0] is the hero.")]
        [SerializeField] private Sc07Human[] people = new Sc07Human[0];
        [SerializeField] private Transform doorStart;
        [SerializeField] private Transform[] noticeSpots = new Transform[0];
        [SerializeField] private Transform exitPoint;
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

        private AudioSource _hiss;
        private Transform _nozzle;
        private Vector3 _co2Home;
        private Quaternion _co2HomeRot;
        private Transform _co2HomeParent;
        private bool _co2Captured;

        public Sc07Human Hero => people.Length > 0 ? people[0] : null;
        public int Count => people.Length;
        public bool HeroHoldsExtinguisher { get; private set; }

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

        /// <summary>Everything back to the calm start: no fire, no alarm, nobody in the hall, the CO2 extinguisher on its wall.</summary>
        public void ResetScene()
        {
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

            foreach (var p in people)
            {
                if (p == null)
                    continue;
                p.SetStartled(false);
                p.SetCarry(false);
                p.SetAim(false);
                p.SetReach(false);
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

            yield return PresentableFx.Wait(2.2f);

            var door = doorStart != null ? doorStart.position : transform.position;
            var runners = new Coroutine[people.Length];
            var arrived = new bool[people.Length];
            for (var i = 0; i < people.Length; i++)
            {
                var p = people[i];
                if (p == null)
                    continue;
                p.gameObject.SetActive(true);
                // they come in one after the other through the far door
                p.transform.position = door + new Vector3(-0.5f * i, 0f, (i % 2 == 0 ? 0.25f : -0.25f));
                p.transform.rotation = Quaternion.LookRotation(Vector3.right * 0f + (noticeSpots.Length > 0 ? noticeSpots[0].position - door : Vector3.forward), Vector3.up);
                var index = i;
                runners[i] = StartCoroutine(Arrive(index, arrived));
                yield return PresentableFx.Wait(0.45f);
            }

            var wait = 0f;
            while (!AllTrue(arrived) && wait < 40f)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            // they see the fire
            var firePos = fireAt != null ? fireAt.position : transform.position;
            for (var i = 0; i < people.Length; i++)
            {
                if (people[i] == null)
                    continue;
                StartCoroutine(people[i].FaceRoutine(firePos, 300f));
                people[i].SetStartled(true);
            }

            yield return PresentableFx.Wait(1.5f);

            // everyone but the hero runs for the door: first one man alone, then two more one after another, then the rest in groups
            var gone = new bool[people.Length];
            for (var i = 1; i < people.Length; i++)
            {
                var index = i;
                float delay;
                if (i <= 3)
                    delay = (i - 1) * 1.5f;
                else if (i <= 7)
                    delay = 4.4f + 0.14f * (i - 4);
                else
                    delay = 6.6f + 0.14f * (i - 8);
                StartCoroutine(RunAway(index, gone, delay));
            }

            yield return PresentableFx.Wait(2.6f);
            // the hero stays, steadies himself and steps up next to the player
            var hero = Hero;
            if (hero != null)
            {
                hero.SetStartled(false);
                var spot = heroSpot != null ? heroSpot.position : hero.transform.position;
                yield return hero.WalkTo(spot, 1.5f, 0.06f);
                yield return hero.FaceRoutine(firePos, 240f);
            }

            gone[0] = true;
            wait = 0f;
            while (!AllTrue(gone) && wait < 20f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator Arrive(int index, bool[] arrived)
        {
            var p = people[index];
            var spot = noticeSpots.Length > 0 ? noticeSpots[Mathf.Min(index, noticeSpots.Length - 1)].position : p.transform.position;
            yield return p.WalkTo(spot, 1.25f + 0.05f * (index % 3), 0.08f);
            arrived[index] = true;
        }

        private IEnumerator RunAway(int index, bool[] gone, float delay)
        {
            var p = people[index];
            yield return PresentableFx.Wait(delay);
            p.SetStartled(false);
            p.SetHeadYaw(0f);
            var exit = exitPoint != null ? exitPoint.position : p.transform.position + Vector3.left * 8f;
            yield return p.WalkTo(exit, 3.5f + 0.12f * (index % 5), 0.4f, 720f);
            p.gameObject.SetActive(false);
            gone[index] = true;
        }

        private static bool AllTrue(bool[] a)
        {
            for (var i = 0; i < a.Length; i++)
                if (!a[i] && true)
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
