using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Client demo flow:
    /// Scene01 view -> salam VO -> 2s -> instruction VO -> fade + glide to the drum rack (step04)
    /// -> zoom + floor hotspot -> MCQ (step05) -> correct (step06) / wrong (step07, retry)
    /// -> Next -> SC02 zone -> hotspot -> MCQ 2 -> finish.
    /// </summary>
    public sealed class PresentableSequenceDirector : MonoBehaviour
    {
        [Header("Shared")]
        [SerializeField] private PresentableArt art;
        [SerializeField] private Transform rig;
        [SerializeField] private AudioSource voice;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private WorldSubtitle subtitle;
        [SerializeField] private InstructionPanelView instructionPanel;
        [SerializeField] private SdsClipboardController clipboard;
        [SerializeField] private RigZoomController zoom;

        [Header("SC01")]
        [SerializeField] private Transform spawnStart;
        [SerializeField] private Transform spawnDrumView;
        [SerializeField] private Transform rackFocus;
        [SerializeField] private FloorHotspot hotspotSc01;
        [SerializeField] private PresentableMcqPanel mcqSc01;
        [SerializeField] private DrumCallout[] drums = new DrumCallout[4];

        [Header("Drum labels + trolley")]
        [SerializeField] private LabelPickPopup labelPopup;
        [SerializeField] private PokeTarget[] drumPokes = new PokeTarget[4];
        [Tooltip("Where the learner is glided to so the drums are within arm's reach.")]
        [SerializeField] private Transform drumPokeStand;
        [SerializeField] private GameObject trolley;
        [SerializeField] private PokeTarget trolleyPoke;
        [SerializeField] private FloorHotspot hotspotTrolley;
        [SerializeField] private Transform trolleyStand;

        [Header("SC02")]
        [SerializeField] private Transform spawnSc02;
        [SerializeField] private FloorHotspot hotspotSc02;
        [SerializeField] private PresentableMcqPanel mcqSc02;

        [Header("Timing")]
        [SerializeField] private float gapAfterSalam = 2f;
        [SerializeField] private float glideBackDistance = 1.3f;
        [Tooltip("Within arm's reach so the gloved fingertip can poke the options.")]
        [SerializeField] private float panelDistance = 0.62f;

        private Transform _cam;
        private bool _hotspotHit;
        private bool _nextPressed;
        private bool _continuePressed;
        private Coroutine _replay;

        private void OnContinueClicked() => _continuePressed = true;

        private void OnSpeakerClicked()
        {
            StopVoice();
            if (_replay != null)
                StopCoroutine(_replay);
            _replay = StartCoroutine(ReplayInstruction());
        }

        private IEnumerator ReplayInstruction()
        {
            instructionPanel.SetSpeaking(true);
            yield return PlayVoice(art.voInstruction);
            instructionPanel.SetSpeaking(false);
            _replay = null;
        }

        private void StopAllVoiceCoroutines()
        {
            if (_replay != null)
                StopCoroutine(_replay);
            _replay = null;
        }

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
        }

        private IEnumerator Start()
        {
            fader.SetImmediate(1f);
            yield return null;
            yield return null;

            foreach (var d in drums)
                d?.SetCalloutVisible(false, 0.01f);

            PlaceRig(spawnStart.position, spawnStart.forward);
            yield return fader.FadeTo(0f, 1.4f);
            yield return PresentableFx.Wait(0.4f);

            Stage("salam");

            // 1. Salam (panel floats within poking distance so the speaker / Continue can be touched)
            PlaceInFrontOfHead(instructionPanel.transform, XRSettings.isDeviceActive ? 0.7f : 0.95f);
            subtitle.SetHeightOffset(-0.9f);
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Show(art.bnWelcomeSub, "Assalamu Alaikum. Welcome to the chemical safety training.");
            yield return PlayVoice(art.voSalam);
            instructionPanel.SetSpeaking(false);

            Stage("gap");

            // 2. Instruction after a 2s gap
            yield return PresentableFx.Wait(gapAfterSalam);
            Stage("instruction");
            instructionPanel.SetSpeaking(true);
            subtitle.Show(art.bnInstructionSub,
                "Select the correctly labeled drum based on the provided Safety Data Sheet (SDS).");
            yield return PlayVoice(art.voInstruction);
            instructionPanel.SetSpeaking(false);

            // 2b. Voice-over finished: Continue appears (+ hint VO). Speaker replays the instruction any time.
            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            subtitle.Show(art.bnContinueHint,
                "Click the speaker to hear the instructions again, or Continue to go on.");
            yield return instructionPanel.ShowContinue();
            StartCoroutine(PlayVoice(art.voContinueHint));
            while (!_continuePressed)
                yield return null;
            instructionPanel.SpeakerClicked -= OnSpeakerClicked;
            instructionPanel.Continue -= OnContinueClicked;
            StopAllVoiceCoroutines();
            StopVoice();
            instructionPanel.SetSpeaking(false);
            subtitle.Hide();
            subtitle.SetHeightOffset(-0.38f);
            yield return instructionPanel.Hide();

            Stage("transition");

            // 3. Transition to the drum rack (step04) with a gentle zoom-in glide
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.4f);
            var viewFwd = Flat(spawnDrumView.forward);
            PlaceRig(spawnDrumView.position - viewFwd * glideBackDistance, viewFwd);
            yield return fader.FadeTo(0f, 0.5f);
            yield return Glide(spawnDrumView.position, 1.8f);

            Stage("explore");

            // 4. Explore + hotspot
            zoom.Configure(rig, rackFocus);
            zoom.ZoomEnabled = true;
            yield return WaitHotspot(hotspotSc01);
            zoom.ZoomEnabled = false;

            Stage("pickLabel");

            // 4b. Voice prompt (once), step up to the drums, then poke each drum and name its label.
            subtitle.Show(art.bnPickLabel, "Select the correct label for each drum.", 6f);
            StartCoroutine(PlayVoice(art.voPickLabel));
            yield return Glide(drumPokeStand.position, 1.2f);
            yield return RunDrumPicking();

            Stage("trolley");
            yield return RunTrolley();

            Stage("mcq1");

            // 5. MCQ 1 — face the drums (step05: drums visible behind the panel)
            var toDrums = Flat(rackFocus.position - hotspotSc01.transform.position);
            yield return FadeReposition(hotspotSc01.transform, Vector3.Slerp(viewFwd, toDrums, 0.6f), mcqSc01);
            yield return RunQuestion(mcqSc01, BuildSc01Question(), true);

            Stage("sc02");

            // 6. SC02
            foreach (var d in drums)
                d?.SetCalloutVisible(false);
            if (trolley != null)
                trolley.SetActive(false);
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            PlaceRig(spawnSc02.position, spawnSc02.forward);
            yield return fader.FadeTo(0f, 0.6f);
            subtitle.Show(art.bnNextZone, "Next: chemical storage area. Step on the green hotspot.", 6f);

            yield return WaitHotspot(hotspotSc02);
            yield return FadeReposition(hotspotSc02.transform, Flat(spawnSc02.forward), mcqSc02);
            yield return RunQuestion(mcqSc02, BuildSc02Question(), false);

            Stage("complete");
            PresentableFx.Play(PresentableFx.Success);
            subtitle.Show(null, "Module complete. Well done!", 8f);
        }

        private int _pokedDrum;
        private int _pickedLabel;
        private bool _popupClosed;

        private IEnumerator RunDrumPicking()
        {
            var done = new bool[drums.Length];
            var remaining = drums.Length;
            for (var i = 0; i < drumPokes.Length; i++)
            {
                var index = i;
                drumPokes[i].Poked += _ => _pokedDrum = index;
            }

            void OnChosen(int label) => _pickedLabel = label;
            void OnClosed() => _popupClosed = true;
            labelPopup.Chosen += OnChosen;
            labelPopup.Closed += OnClosed;

            while (remaining > 0)
            {
                for (var i = 0; i < drumPokes.Length; i++)
                    drumPokes[i].SetInteractable(!done[i]);
                _pokedDrum = -1;
                while (_pokedDrum < 0)
                    yield return null;

                var drum = _pokedDrum;
                foreach (var p in drumPokes)
                    p.SetInteractable(false);

                PlaceInFrontOfHead(labelPopup.transform, 0.48f); // the rack front is only ~0.6 m from the poke stand
                _pickedLabel = -1;
                _popupClosed = false;
                labelPopup.Open();
                var correct = false;
                while (!correct && !_popupClosed)
                {
                    yield return null;
                    if (_pickedLabel < 0)
                        continue;

                    var picked = _pickedLabel;
                    _pickedLabel = -1;
                    if (picked == drum)
                    {
                        correct = true;
                        continue;
                    }

                    labelPopup.SetWrong(true);
                    PresentableFx.Play(PresentableFx.Error, 0.7f);
                    PresentableFx.Haptic(true, 0.8f, 0.2f);
                    StopVoice();
                    subtitle.Show(art.bnWrongLabel,
                        "Wrong label for this drum. Select the correct label again.", 6f);
                    StartCoroutine(PlayVoice(art.voWrongLabel));
                }

                labelPopup.Close();
                if (!correct)
                    continue;

                done[drum] = true;
                remaining--;
                StopVoice();
                subtitle.Hide();
                PresentableFx.Play(PresentableFx.Success, 0.8f);
                PresentableFx.Haptic(true, 0.5f, 0.12f);
                drums[drum].Highlight(PresentableUi.Green, false);
                drums[drum].RevealPlate();
                yield return PresentableFx.Wait(0.5f);
            }

            labelPopup.Chosen -= OnChosen;
            labelPopup.Closed -= OnClosed;
        }

        private IEnumerator RunTrolley()
        {
            foreach (var d in drums)
                d?.ClearHighlight();

            trolley.SetActive(true);
            PresentableFx.Play(PresentableFx.Chime, 0.8f);
            var baseScale = trolley.transform.localScale;
            yield return PresentableFx.Tween(0.5f, t => trolley.transform.localScale =
                baseScale * Mathf.LerpUnclamped(0.5f, 1f, t), PresentableFx.EaseOutBack);

            void OnPoked(PokeTarget _) => hotspotTrolley.Activate();
            trolleyPoke.Poked += OnPoked;
            yield return Glide(trolleyStand.position, 1.2f);
            trolleyPoke.SetInteractable(true);
            yield return WaitHotspot(hotspotTrolley);
            trolleyPoke.SetInteractable(false);
            trolleyPoke.Poked -= OnPoked;
        }

        private void PlaceInFrontOfHead(Transform t, float distance)
        {
            if (_cam == null)
                return;
            var fwd = Flat(_cam.forward);
            t.position = new Vector3(_cam.position.x, _cam.position.y - 0.12f, _cam.position.z) + fwd * distance;
            t.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
        }

        private IEnumerator RunQuestion(PresentableMcqPanel panel, PresentableMcqPanel.Question q, bool useDrums)
        {
            _nextPressed = false;

            void OnAnswered(int index, bool correct)
            {
                if (useDrums)
                {
                    foreach (var d in drums)
                        d?.ClearHighlight();
                    if (index < drums.Length && drums[index] != null)
                        drums[index].Highlight(correct ? PresentableUi.Green : PresentableUi.Red, true);
                }

                StopVoice();
                if (correct)
                {
                    subtitle.Show(art.bnCorrectSub, "Correct! You selected the correctly labeled drum.", 5f);
                    StartCoroutine(PlayVoice(art.voCorrect));
                }
                else
                {
                    subtitle.Show(art.bnWrongSub, "Wrong selection. Read the explanation and try again.", 5f);
                    StartCoroutine(PlayVoice(art.voWrong));
                }
            }

            void OnNext() => _nextPressed = true;

            panel.Answered += OnAnswered;
            panel.NextPressed += OnNext;
            panel.Show(q);
            while (!_nextPressed)
                yield return null;
            panel.Answered -= OnAnswered;
            panel.NextPressed -= OnNext;

            if (useDrums)
            {
                foreach (var d in drums)
                    d?.ClearHighlight();
            }

            panel.Hide();
            yield return PresentableFx.Wait(0.35f);
        }

        public string CurrentStage { get; private set; } = "boot";

        private void Stage(string name)
        {
            CurrentStage = name;
            Debug.Log("[Presentable] Stage: " + name + " t=" + Time.time.ToString("0.0"));
        }

        private IEnumerator WaitHotspot(FloorHotspot hotspot)
        {
            _hotspotHit = false;
            void OnHit() => _hotspotHit = true;
            hotspot.Activated += OnHit;
            hotspot.Arm();
            while (!_hotspotHit)
                yield return null;
            hotspot.Activated -= OnHit;
        }

        private IEnumerator PlayVoice(AudioClip clip)
        {
            if (clip == null || voice == null)
            {
                yield return PresentableFx.Wait(2.5f);
                yield break;
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();

            voice.Stop();
            voice.clip = clip;
            voice.Play();

            // clip.length is unreliable while compressed audio is still loading, so track playback instead.
            var waited = 0f;
            while (!voice.isPlaying && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            waited = 0f;
            while (voice.isPlaying && voice.clip == clip && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return PresentableFx.Wait(0.1f);
        }

        private void StopVoice()
        {
            if (voice != null)
                voice.Stop();
        }

        /// <summary>Moves / rotates the rig so the head stands on floorPos looking along forward.</summary>
        private void PlaceRig(Vector3 floorPos, Vector3 forward)
        {
            if (rig == null || _cam == null)
                return;

            var camFwd = Flat(_cam.forward);
            var delta = Vector3.SignedAngle(camFwd, Flat(forward), Vector3.up);
            rig.RotateAround(_cam.position, Vector3.up, delta);

            var head = new Vector3(_cam.position.x, rig.position.y, _cam.position.z);
            var offset = new Vector3(floorPos.x, rig.position.y, floorPos.z) - head;
            rig.position += offset;
            Physics.SyncTransforms();
        }

        private IEnumerator Glide(Vector3 floorTarget, float duration)
        {
            if (rig == null || _cam == null)
                yield break;

            var startHead = new Vector3(_cam.position.x, rig.position.y, _cam.position.z);
            var target = new Vector3(floorTarget.x, rig.position.y, floorTarget.z);
            var lastOffset = Vector3.zero;
            yield return PresentableFx.Tween(duration, t =>
            {
                var offset = (target - startHead) * t;
                rig.position += offset - lastOffset;
                lastOffset = offset;
                Physics.SyncTransforms();
            });
        }

        private IEnumerator FadeReposition(Transform stand, Vector3 forward, PresentableMcqPanel panel)
        {
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(stand.position, forward);
            PlacePanel(panel.transform, stand, forward);
            yield return fader.FadeTo(0f, 0.35f);
        }

        private void PlacePanel(Transform panel, Transform stand, Vector3 forward)
        {
            var fwd = Flat(forward);
            var eye = _cam != null ? _cam.position.y : 1.6f;
            var distance = XRSettings.isDeviceActive ? panelDistance : 1.05f;
            panel.position = new Vector3(stand.position.x, 0f, stand.position.z) + fwd * distance +
                             Vector3.up * Mathf.Clamp(eye - 0.2f, 1.0f, 1.65f);
            panel.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        private PresentableMcqPanel.Question BuildSc01Question()
        {
            return new PresentableMcqPanel.Question
            {
                prompt = "Question 1: Based on GHS labeling standards, which drum should you select for transfer?",
                correctIndex = 3,
                options = new[]
                {
                    new PresentableMcqPanel.Option
                    {
                        label = "1. Faded label",
                        wrongLabel = "1. Faded label (Acetone)",
                        icons = new[] { art.thumbNoLabel },
                        overlays = new[] { art.labelFaded },
                        feedback = "You cannot verify the contents or specific hazards of a drum with a faded or " +
                                   "unreadable label. Never move unidentified chemicals."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "2. No label",
                        wrongLabel = "2. No label (Unknown)",
                        icons = new[] { art.thumbNoLabel },
                        feedback = "An unlabeled container must be treated as an unknown hazard. Report and " +
                                   "quarantine it. Never transfer it."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "3. Mismatched\nGHS vs SDS",
                        wrongLabel = "3. Mismatched (Methanol)",
                        icons = new[] { art.iconGhs, art.iconSds, art.thumbNoLabel },
                        overlays = new Sprite[] { null, null, art.labelMethanol },
                        feedback = "The drum label (Methanol) does not match the SDS (Isopropanol). If the label " +
                                   "does not match the SDS: DO NOT USE. Isolate and report."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "4. Correctly Labelled",
                        icons = new[] { art.thumbNoLabel },
                        overlays = new[] { art.labelIsopropanol },
                        feedback = "Well done! You have correctly identified the standard GHS label configuration. " +
                                   "The selected drum's GHS pictograms, UN number, and name match the Safety Data " +
                                   "Sheet (SDS) requirements precisely, ensuring safe transfer."
                    }
                }
            };
        }

        private static PresentableMcqPanel.Question BuildSc02Question()
        {
            return new PresentableMcqPanel.Question
            {
                prompt = "Question 2: Where does the flammable solvent go?",
                correctIndex = 1,
                nextLabel = "FINISH",
                options = new[]
                {
                    new PresentableMcqPanel.Option
                    {
                        label = "1. With the oxidizers",
                        feedback = "Flammable + oxidizer = fire / explosion risk. Never co-store."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "2. Flammable-rated compartment, away from oxidizers and acids",
                        feedback = "Correct! This matches the compatibility chart. Flammables are stored separately " +
                                   "on fire-rated shelving."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "3. With the acids",
                        feedback = "Acid + flammable solvent = potential ignition on contact."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "4. In any open compartment",
                        feedback = "Ignoring segregation defeats the entire storage system."
                    }
                }
            };
        }
    }
}
