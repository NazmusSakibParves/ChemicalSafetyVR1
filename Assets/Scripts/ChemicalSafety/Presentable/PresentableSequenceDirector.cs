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

        [Header("Drum labels + drum carrier")]
        [SerializeField] private LabelPickPopup labelPopup;
        [SerializeField] private PokeTarget[] drumPokes = new PokeTarget[4];
        [SerializeField] private FloatingTouchButton[] drumButtons = new FloatingTouchButton[4];
        [Tooltip("About a metre from the rack face so the floating Touch buttons are within arm's reach.")]
        [SerializeField] private Transform drumPokeStand;
        [SerializeField] private GameObject trolley;
        [SerializeField] private FloatingTouchButton carrierButton;
        [SerializeField] private GameObject carrierLoad;
        [SerializeField] private Transform trolleyStand;

        [Header("SC02")]
        [SerializeField] private Transform spawnSc02;
        [Tooltip("Stand Here marker beside the pallet (arm's reach of the containers).")]
        [SerializeField] private FloorHotspot hotspotSc02Pick;
        [Tooltip("Spot in front of the glass cabinet (arm's reach of the doors). No marker is drawn there.")]
        [SerializeField] private Transform shelfStand;
        [Tooltip("Seconds the user gets to walk to the cabinet before the rig glides there.")]
        [SerializeField] private float shelfPatience = 20f;
        [SerializeField] private ContainerPlacement placement;
        [SerializeField] private PresentableMcqPanel mcqSc02;
        [SerializeField] private GameObject envSc01;
        [SerializeField] private GameObject envSc02;
        [SerializeField] private CompartmentDoor[] doors = new CompartmentDoor[4];
        [SerializeField] private ChartViewer chart;
        [SerializeField] private ContainerInspect inspect;

        [Header("SC03")]
        [SerializeField] private GameObject envSc03;
        [Tooltip("Entrance of the old storage room, facing down the aisle.")]
        [SerializeField] private Transform spawnSc03;
        [Tooltip("About a metre from the old drum, facing it.")]
        [SerializeField] private Transform sc03InspectStand;
        [Tooltip("About 0.6 m from the drum label, within arm's reach for the hold-to-scan.")]
        [SerializeField] private Transform sc03ScanStand;
        [SerializeField] private Sc03Inspect sc03Inspect;
        [SerializeField] private Sc03Detonation sc03Detonation;
        [SerializeField] private PresentableMcqPanel mcqSc03;
        [Tooltip("Distance of the instruction card from the head in a headset (the desk uses a bit more).")]
        [SerializeField] private float sc03CardDistance = 0.8f;

        [Header("SC04")]
        [SerializeField] private GameObject envSc04;
        [SerializeField] private Transform spawnSc04;
        [SerializeField] private GloveInspect gloves;
        [SerializeField] private Sc04BenchHeight gloveBench;
        [SerializeField] private PresentableMcqPanel mcqSc04;

        [Header("SC05")]
        [SerializeField] private GameObject envSc05;
        [Tooltip("At the pillar with the respirators, about 1 m from the hook rail.")]
        [SerializeField] private Transform spawnSc05;
        [SerializeField] private MaskInspect masks;
        [SerializeField] private PresentableMcqPanel mcqSc05;
        [SerializeField] private Sc05Ambience sc05Ambience;

        [Header("SC06")]
        [SerializeField] private GameObject envSc06;
        [Tooltip("In front of the steel table, facing the back wall.")]
        [SerializeField] private Transform spawnSc06;
        [SerializeField] private Transform sc06TableStand;
        [SerializeField] private Transform sc06HoodStand;
        [Tooltip("Between the table and the hood, about 0.7 m from the control panel and the solvent.")]
        [SerializeField] private Transform sc06PanelStand;
        [SerializeField] private Sc06Pour sc06Pour;
        [SerializeField] private Sc06Gate sc06Gate;
        [SerializeField] private Sc06Lab lab;
        [SerializeField] private Sc06Jug sc06Jug;
        [SerializeField] private PresentableMcqPanel mcqSc06;
        [SerializeField] private Sc06Ambience sc06Ambience;

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
            yield return PlayVoice(_instructionClip != null ? _instructionClip : art.voInstruction);
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

#if UNITY_EDITOR
            // developer shortcut (menu Chemical Safety/Presentable/Dev: start at Scenario 6): skips straight to the lab
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc06Only", false) && envSc06 != null)
            {
                yield return RunScenario6();
                yield break;
            }
#endif
            foreach (var d in drums)
                d?.SetCalloutVisible(false, 0.01f);

            _instructionClip = art.voInstruction;
            PlaceRig(spawnStart.position, spawnStart.forward);
            yield return fader.FadeTo(0f, 1.4f);
            yield return PresentableFx.Wait(0.4f);

            Stage("salam");

            // Salam and the instruction share one card. It stays at eye level the whole time,
            // whether the learner is sitting or standing, so it does not jump up after the greeting.
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            yield return SubtitledVoice(art.bnWelcomeSub, "Assalamu Alaikum. Welcome to the chemical safety training.",
                art.voSalam);
            instructionPanel.SetSpeaking(false);

            Stage("gap");

            // 2. Instruction after a 2s gap. Same eye-level card and centred Continue as Scenario 3.
            yield return PresentableFx.Wait(gapAfterSalam);
            Stage("instruction");
            instructionPanel.SetContinueCentered(true);
            instructionPanel.SetSpeaking(true);
            yield return SubtitledVoice(art.bnInstructionSub,
                "Select the correctly labeled drum based on the provided Safety Data Sheet (SDS).", art.voInstruction);
            instructionPanel.SetSpeaking(false);

            // 2b. Voice-over finished: Continue appears (+ hint VO). Speaker replays the instruction any time.
            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
            instructionPanel.SetSpeaking(true);
            StartCoroutine(SubtitledVoice(art.bnContinueHint,
                "Click the speaker to hear the instructions again, or Continue to go on.", art.voContinueHint));
            while (!_continuePressed)
                yield return null;
            instructionPanel.SpeakerClicked -= OnSpeakerClicked;
            instructionPanel.Continue -= OnContinueClicked;
            StopAllVoiceCoroutines();
            StopVoice();
            instructionPanel.SetSpeaking(false);
            subtitle.Hide();
            yield return instructionPanel.Hide();
            instructionPanel.SetContinueCentered(false);
            EndEyeInstruction();

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
            _introVoice = StartCoroutine(PickLabelVoices());
            yield return Glide(drumPokeStand.position, 1.2f);
            yield return RunDrumPicking();

            Stage("trolley");
            yield return RunCarrier();

            Stage("mcq1");

            // 5. MCQ 1 — face the drums (step05: drums visible behind the panel, no name plates)
            foreach (var d in drums)
                d?.SetCalloutVisible(false);
            var toDrums = Flat(rackFocus.position - hotspotSc01.transform.position);
            yield return FadeReposition(hotspotSc01.transform, Vector3.Slerp(viewFwd, toDrums, 0.6f), mcqSc01);
            yield return RunQuestion(mcqSc01, BuildSc01Question(), true);

            Stage("sc02");

            // 6. SC02 storage room (the warehouse is hidden so only one room draws)
            foreach (var d in drums)
                d?.SetCalloutVisible(false);
            if (trolley != null)
                trolley.SetActive(false);
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            if (envSc01 != null)
                envSc01.SetActive(false);
            if (envSc02 != null)
                envSc02.SetActive(true);
            clipboard.SetSheet(art.sdsSc02);
            PlaceRig(spawnSc02.position, spawnSc02.forward);
            yield return fader.FadeTo(0f, 0.6f);

            Stage("sc02Intro");
            _instructionClip = art.voSc02Instruction != null ? art.voSc02Instruction : art.voInstruction;
            instructionPanel.SetContent(
                "<color=#5FD8FF><b>VOICE-OVER:</b></color> A delivery of six chemicals has arrived. " +
                "The supervisor asks you to place each container into the correct compartment.",
                art.bnSc02InstructionPanel);
            instructionPanel.SetContinueCentered(true);
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);

            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
            instructionPanel.SetSpeaking(true);
            StartCoroutine(PlayVoice(art.voContinueHint));
            while (!_continuePressed)
                yield return null;
            instructionPanel.SpeakerClicked -= OnSpeakerClicked;
            instructionPanel.Continue -= OnContinueClicked;
            StopAllVoiceCoroutines();
            StopVoice();
            instructionPanel.SetSpeaking(false);
            subtitle.Hide();
            yield return instructionPanel.Hide();
            instructionPanel.SetContinueCentered(false);
            EndEyeInstruction();

            Stage("sc02Explore");
            yield return RunPlacement();

            Stage("sc02Question");
            // the question opens in front of the user, back at the room entrance so the glass doors are not in the way
            yield return FadeReposition(spawnSc02, Flat(spawnSc02.forward), mcqSc02);
            yield return RunQuestion(mcqSc02, BuildSc02Question(), false, 2);

            if (envSc03 != null)
                yield return RunScenario3();

            Stage("complete");
            PresentableFx.Play(PresentableFx.Success);
            var doneLine = envSc04 != null ? Sc04Content.ModuleComplete
                : envSc03 != null ? Sc03Content.ModuleComplete
                : "Module complete. Well done!";
            subtitle.Show(null, doneLine, 8f);
        }

        // SC03: old storage room. Instruction card (eye level) -> inspect the crystallised peroxide drum ->
        // immediate-action question; a wrong answer detonates the drum and retries.
        private IEnumerator RunScenario3()
        {
            Stage("sc03");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            if (envSc02 != null)
                envSc02.SetActive(false);
            envSc03.SetActive(true);
            clipboard.SetSheet(art.sdsSc03);
            clipboard.SetVisible(false);
            PlaceRig(spawnSc03.position, spawnSc03.forward);
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc03Intro");
            _instructionClip = art.voSc03Instruction != null ? art.voSc03Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc03Content.InstructionEnglish, art.bnSc03InstructionPanel);
            instructionPanel.SetContinueCentered(true);
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);

            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
            instructionPanel.SetSpeaking(true);
            StartCoroutine(PlayVoice(art.voContinueHint));
            while (!_continuePressed)
                yield return null;
            instructionPanel.SpeakerClicked -= OnSpeakerClicked;
            instructionPanel.Continue -= OnContinueClicked;
            StopAllVoiceCoroutines();
            StopVoice();
            instructionPanel.SetSpeaking(false);
            subtitle.Hide();
            yield return instructionPanel.Hide();
            instructionPanel.SetContinueCentered(false);
            EndEyeInstruction();

            Stage("sc03Inspect");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.35f);
            PlaceRig(sc03ScanStand != null ? sc03ScanStand.position : sc03InspectStand.position,
                sc03ScanStand != null ? sc03ScanStand.forward : sc03InspectStand.forward);
            clipboard.SetVisible(true);
            yield return fader.FadeTo(0f, 0.5f);
            // the SDS is held up (X hides / shows it); the arrow in front of the drum invites a finger touch
            subtitle.Show(art.bnClipboardHint, Sc03Content.SdsHint, 5f);
            yield return PresentableFx.Wait(1.2f);
            sc03Inspect.ArmTouch();
            StartCoroutine(SubtitledVoice(art.bnSc03Touch, Sc03Content.TouchHint, art.voSc03Touch));
            while (!sc03Inspect.Touched)
                yield return null;
            StopVoice();
            subtitle.Hide();
            // back to the stand the ring, the report card and the question are laid out for
            if (sc03ScanStand != null)
            {
                yield return fader.FadeTo(1f, 0.25f);
                PlaceRig(sc03InspectStand.position, sc03InspectStand.forward);
                yield return fader.FadeTo(0f, 0.35f);
            }

            yield return sc03Inspect.ShowReport();
            StartCoroutine(PlayVoice(art.voSc03Inspect));
            while (!sc03Inspect.ContinuePressed)
                yield return null;
            StopVoice();
            // The finger is still on Continue. Wait until it lifts so it cannot poke a wrong
            // option the moment the question appears. The blast stays on a wrong answer only.
            yield return WaitUntilFingerOff(sc03Inspect.ReportCard);
            yield return sc03Inspect.HideReport();

            Stage("sc03Question");
            PlaceAtEyeLevel(mcqSc03.transform, panelDistance, false);
            yield return RunQuestion(mcqSc03, BuildSc03Question(), false, 3);

            if (envSc04 != null)
                yield return RunScenario4();
        }

        // SC04 starts only after the SC03 question is finished. SC01–SC03 rooms and questions are unchanged.
        private IEnumerator RunScenario4()
        {
            Stage("sc04");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            if (envSc03 != null)
                envSc03.SetActive(false);
            envSc04.SetActive(true);
            clipboard.SetSheet(art.sdsSc04);
            clipboard.SetVisible(false);
            PlaceRig(spawnSc04.position, spawnSc04.forward);
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc04Intro");
            _instructionClip = art.voSc04Instruction != null ? art.voSc04Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc04Content.InstructionEnglish, art.bnSc04InstructionPanel);
            instructionPanel.SetContinueCentered(true);
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);

            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
            instructionPanel.SetSpeaking(true);
            StartCoroutine(PlayVoice(art.voContinueHint));
            while (!_continuePressed)
                yield return null;
            instructionPanel.SpeakerClicked -= OnSpeakerClicked;
            instructionPanel.Continue -= OnContinueClicked;
            StopAllVoiceCoroutines();
            StopVoice();
            instructionPanel.SetSpeaking(false);
            subtitle.Hide();
            yield return instructionPanel.Hide();
            instructionPanel.SetContinueCentered(false);
            EndEyeInstruction();

            if (gloveBench != null && _cam != null)
                gloveBench.SnapTo(_cam.position.y);
            if (gloves != null)
                yield return Glide(gloves.StandInFront(), 0.7f);

            Stage("sc04Explore");
            if (gloves != null)
            {
                gloves.Begin();
                while (!gloves.NextChosen)
                {
                    if (gloveBench != null && _cam != null)
                        gloveBench.Follow(_cam.position.y);
                    yield return null;
                }
            }

            Stage("sc04Question");
            PlaceAtEyeLevel(mcqSc04.transform, panelDistance, false);
            yield return RunQuestion(mcqSc04, BuildSc04Question(), false, 4);

            if (envSc05 != null)
                yield return RunScenario5();
        }

        // SC05 starts only after the SC04 question's NEXT. Dyeing factory: pick the right respirator for toluene.
        private IEnumerator RunScenario5()
        {
            Stage("sc05");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            if (envSc04 != null)
                envSc04.SetActive(false);
            envSc05.SetActive(true);
            clipboard.SetSheet(art.sdsSc05);
            clipboard.SetVisible(false);
            PlaceRig(spawnSc05.position, spawnSc05.forward);
            if (sc05Ambience != null)
                sc05Ambience.Begin(); // machines run from the first second; the voice-over lowers them
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc05Intro");
            _instructionClip = art.voSc05Instruction != null ? art.voSc05Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc05Content.InstructionEnglish, art.bnSc05InstructionPanel);
            instructionPanel.SetContinueCentered(true);
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);

            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
            instructionPanel.SetSpeaking(true);
            StartCoroutine(PlayVoice(art.voContinueHint));
            while (!_continuePressed)
                yield return null;
            instructionPanel.SpeakerClicked -= OnSpeakerClicked;
            instructionPanel.Continue -= OnContinueClicked;
            StopAllVoiceCoroutines();
            StopVoice();
            instructionPanel.SetSpeaking(false);
            subtitle.Hide();
            yield return instructionPanel.Hide();
            instructionPanel.SetContinueCentered(false);
            EndEyeInstruction();

            if (masks != null)
                yield return Glide(masks.StandInFront(), 0.7f);

            Stage("sc05Explore");
            if (masks != null)
            {
                // opening a respirator: the Bangla voice says what it is and whether it suits toluene
                Coroutine maskVoice = null;

                IEnumerator SayMask(int index)
                {
                    masks.SetSpeaking(true);
                    yield return PlayVoice(art.voSc05Mask[index]);
                    masks.SetSpeaking(false);
                    maskVoice = null;
                }

                void OnMaskOpened(int index)
                {
                    StopVoice();
                    if (maskVoice != null)
                        StopCoroutine(maskVoice);
                    maskVoice = null;
                    masks.SetSpeaking(false);
                    if (art.voSc05Mask != null && index >= 0 && index < art.voSc05Mask.Length && art.voSc05Mask[index] != null)
                        maskVoice = StartCoroutine(SayMask(index));
                }

                void OnMaskClosed()
                {
                    if (maskVoice != null)
                        StopCoroutine(maskVoice);
                    maskVoice = null;
                    StopVoice();
                    masks.SetSpeaking(false);
                }

                masks.Opened += OnMaskOpened;
                masks.Closed += OnMaskClosed;
                masks.SpeakerClicked += OnMaskOpened; // the speaker on the card plays the same voice-over again
                masks.Begin();
                while (!masks.NextChosen)
                    yield return null;
                masks.Opened -= OnMaskOpened;
                masks.Closed -= OnMaskClosed;
                masks.SpeakerClicked -= OnMaskOpened;
                OnMaskClosed();
            }

            Stage("sc05Question");
            PlaceAtEyeLevel(mcqSc05.transform, panelDistance, false);
            yield return RunQuestion(mcqSc05, BuildSc05Question(), false, 5);
            if (sc05Ambience != null)
                sc05Ambience.End();

            // SC06 (chemistry lab) starts after the SC05 question's NEXT
            if (envSc06 != null)
                yield return RunScenario6();
        }

        // SC06 starts after the SC05 question's NEXT. Chemistry lab: switch the fume hood on, lift the solvent, open the sash, pour.
        private IEnumerator RunScenario6()
        {
            Stage("sc06");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05 })
                if (e != null)
                    e.SetActive(false);
            if (sc05Ambience != null)
                sc05Ambience.End();
            envSc06.SetActive(true);
            clipboard.SetSheet(art.sdsSc06);
            clipboard.SetVisible(false);
            lab.ResetLab();
            sc06Jug.ReturnHome();
            PlaceRig(spawnSc06.position, spawnSc06.forward);
            sc06Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc06Intro");
            _instructionClip = art.voSc06Instruction != null ? art.voSc06Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc06Content.InstructionEnglish, art.bnSc06InstructionPanel);
            instructionPanel.SetContinueCentered(true);
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);

            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
            instructionPanel.SetSpeaking(true);
            StartCoroutine(PlayVoice(art.voContinueHint));
            while (!_continuePressed)
                yield return null;
            instructionPanel.SpeakerClicked -= OnSpeakerClicked;
            instructionPanel.Continue -= OnContinueClicked;
            StopAllVoiceCoroutines();
            StopVoice();
            instructionPanel.SetSpeaking(false);
            subtitle.Hide();
            yield return instructionPanel.Hide();
            instructionPanel.SetContinueCentered(false);
            EndEyeInstruction();

            // the inspection: switch, solvent, sash, pour. BACK on the "Inspection completed" card runs it again from the start.
            var again = true;
            while (again)
            {
                yield return Sc06InspectionRound();

                // done: the sash closes, the user is taken back to the steel table and the card opens
                Stage("sc06Gate");
                yield return PresentableFx.Wait(0.6f);
                lab.CloseSash();
                yield return fader.FadeTo(1f, 0.35f);
                PlaceRig(sc06TableStand.position, sc06TableStand.forward);
                subtitle.Hide();
                yield return fader.FadeTo(0f, 0.5f);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc06Gate.Back += OnBack;
                sc06Gate.Next += OnNext;
                sc06Gate.Show();
                StartCoroutine(PlayVoice(art.voSc06Gate));
                while (choice == 0)
                    yield return null;
                sc06Gate.Back -= OnBack;
                sc06Gate.Next -= OnNext;
                sc06Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                if (choice == 1)
                {
                    // review again: the lab goes back to how it was at the start
                    yield return fader.FadeTo(1f, 0.3f);
                    lab.ResetLab();
                    sc06Jug.ReturnHome();
                    sc06Pour.ResetPour();
                    yield return PresentableFx.Wait(0.3f);
                }
                else
                {
                    again = false;
                }
            }

            Stage("sc06Question");
            PlaceAtEyeLevel(mcqSc06.transform, panelDistance, false);
            yield return RunQuestion(mcqSc06, BuildSc06Question(), false, 6);
            Stage("sc06Done");
        }

        private IEnumerator Sc06InspectionRound()
        {
            // to the corridor between the table and the hood: the control panel and the solvent are within arm's reach
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc06PanelStand.position, sc06PanelStand.forward);
            yield return fader.FadeTo(0f, 0.35f);

            // 1. switch the fume hood on (green arrow over its switch)
            Stage("sc06Hood");
            lab.SetSwitchesInteractable(true);
            lab.PointAt(lab.Rocker(0), new Vector3(0f, 0.2f, 0.06f));
            StartCoroutine(SubtitledVoice(art.bnSc06HoodHint, Sc06Content.HoodHint, art.voSc06Hood));
            while (!lab.HoodOn)
                yield return null;
            lab.HideArrow();
            StartCoroutine(SubtitledVoice(null, Sc06Content.HoodOnHint, art.voSc06HoodOn));

            // 2. lift the Volatile Solvent container into the left hand
            Stage("sc06Jug");
            sc06Jug.SetInteractable(true);
            lab.PointAt(sc06Jug.JugTransform, new Vector3(0f, 0.42f, 0.02f));
            yield return PresentableFx.Wait(2.2f);
            StartCoroutine(SubtitledVoice(art.bnSc06JugHint, Sc06Content.JugHint, art.voSc06Jug));
            while (!sc06Jug.Held)
                yield return null;
            lab.HideArrow();

            // 3. to the hood, touch the glass door; it opens from the bottom upwards (only while the hood runs)
            Stage("sc06Sash");
            StopVoice();
            subtitle.Hide();
            yield return Glide(sc06HoodStand.position, 0.9f);
            PlaceRig(sc06HoodStand.position, sc06HoodStand.forward);
            lab.SetSashInteractable(true);
            lab.PointAt(lab.Sash, new Vector3(0f, 0.85f, 0.08f));
            void OnBlocked()
            {
                StopVoice();
                StartCoroutine(SubtitledVoice(art.bnSc06HoodWarn, Sc06Content.HoodWarn, art.voSc06HoodWarn));
                lab.PointAt(lab.Rocker(0), new Vector3(0f, 0.2f, 0.06f));
            }

            lab.SashBlocked += OnBlocked;
            StartCoroutine(SubtitledVoice(art.bnSc06SashHint, Sc06Content.SashHint, art.voSc06Sash));
            while (!lab.SashOpen)
                yield return null;
            lab.SashBlocked -= OnBlocked;
            lab.HideArrow();
            lab.SetSashInteractable(false);

            Stage("sc06Pour");
            StartCoroutine(SubtitledVoice(art.bnSc06PourHint, Sc06Content.PourHint, art.voSc06Pour));
            sc06Pour.SetInteractable(true);
            lab.PointAt(sc06Pour.Jar, new Vector3(0f, 0.55f, 0.04f));
            var poured = false;
            void OnPoured() => poured = true;
            sc06Pour.PourFinished += OnPoured;
            while (!poured)
                yield return null;
            sc06Pour.PourFinished -= OnPoured;
            lab.HideArrow();
            Stage("sc06PourDone");
        }

        private bool _eyeInstruction;

        private void LateUpdate()
        {
            if (!_eyeInstruction || instructionPanel == null)
                return;
            // Reaching for the speaker or Continue: freeze the card so the target does not slide away.
            if (FingerCovers(instructionPanel.transform, 0.22f, 200f))
                return;
            PlaceAtEyeLevel(instructionPanel.transform, EyeCardDistance(), true);
        }

        private float EyeCardDistance()
        {
            // Headset: close enough to poke without a full arm extension. The desk preview stays further back.
            return XRSettings.isDeviceActive ? Mathf.Min(sc03CardDistance, 0.52f) : 0.95f;
        }

        private void BeginEyeInstruction()
        {
            _eyeInstruction = true;
            PlaceAtEyeLevel(instructionPanel.transform, EyeCardDistance(), false);
        }

        private void EndEyeInstruction()
        {
            _eyeInstruction = false;
        }

        // Eye height for both a seated and a standing user. Horizontal follow is damped so a small head
        // movement does not yank the card. A fingertip over the card skips this and holds the last pose.
        private void PlaceAtEyeLevel(Transform t, float distance, bool smooth)
        {
            if (_cam == null || t == null)
                return;
            var fwd = Flat(_cam.forward);
            var target = _cam.position + fwd * distance + Vector3.up * 0.03f;
            var look = Quaternion.LookRotation(fwd, Vector3.up);
            if (!smooth)
            {
                t.SetPositionAndRotation(target, look);
                return;
            }

            var k = 1f - Mathf.Exp(-4.5f * Time.deltaTime);
            t.position = Vector3.Lerp(t.position, target, k);
            t.rotation = Quaternion.Slerp(t.rotation, look, k);
        }

        // True while a fingertip is just in front of the panel, including the Continue pill that hangs below it.
        private static bool FingerCovers(Transform panel, float depthLimit, float belowPad)
        {
            if (panel == null)
                return false;
            return TipCovers(GlovedHandPose.Right, panel, depthLimit, belowPad) ||
                   TipCovers(GlovedHandPose.Left, panel, depthLimit, belowPad);
        }

        private static bool TipCovers(GlovedHandPose hand, Transform panel, float depthLimit, float belowPad)
        {
            if (hand == null || !hand.HasTip)
                return false;
            var rt = panel.GetComponentInChildren<RectTransform>();
            if (rt == null || !rt.gameObject.activeInHierarchy)
                return false;
            var local = rt.InverseTransformPoint(hand.PressPoint);
            var depth = -local.z * rt.lossyScale.z;
            if (depth < -0.03f || depth > depthLimit)
                return false;
            var r = rt.rect;
            return local.x >= r.xMin - 80f && local.x <= r.xMax + 80f &&
                   local.y >= r.yMin - belowPad && local.y <= r.yMax + 80f;
        }

        private IEnumerator WaitUntilFingerOff(Transform panel)
        {
            if (!FingerCovers(panel, 0.08f, 40f))
                yield break;
            var guard = 0f;
            while (FingerCovers(panel, 0.08f, 40f) && guard < 8f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
        }

        private int _pokedDrum;
        private int _pickedLabel;
        private bool _popupClosed;
        private bool _carrierPressed;
        private Coroutine _introVoice;
        private Coroutine _answerVoice;
        private AudioClip _instructionClip;

        private IEnumerator PickLabelVoices()
        {
            yield return SubtitledVoice(art.bnPickLabel, "Select the correct label for each drum.", art.voPickLabel);
            yield return PresentableFx.Wait(0.3f);
            yield return SubtitledVoice(art.bnTouchDrum, "Touch the glowing button in front of a drum with your finger.",
                art.voTouchDrum);
            _introVoice = null;
        }

        private IEnumerator RunDrumPicking()
        {
            var done = new bool[drums.Length];
            var remaining = drums.Length;
            for (var i = 0; i < drumPokes.Length; i++)
            {
                var index = i;
                drumPokes[i].Poked += _ => _pokedDrum = index;
                drumButtons[i].Pressed += _ => _pokedDrum = index;
                drumButtons[i].HoverChanged += on =>
                {
                    if (on)
                        drums[index].Highlight(PresentableUi.Cyan, true);
                    else
                        drums[index].ClearHighlight();
                };
            }

            void OnChosen(int label) => _pickedLabel = label;
            void OnClosed() => _popupClosed = true;
            labelPopup.Chosen += OnChosen;
            labelPopup.Closed += OnClosed;

            var firstRound = true;
            while (remaining > 0)
            {
                for (var i = 0; i < drumPokes.Length; i++)
                {
                    drumPokes[i].SetInteractable(!done[i]);
                    if (done[i])
                        drumButtons[i].Disarm();
                    else
                        drumButtons[i].Arm(firstRound);
                }

                _pokedDrum = -1;
                while (_pokedDrum < 0)
                    yield return null;

                if (_introVoice != null)
                {
                    StopCoroutine(_introVoice);
                    _introVoice = null;
                    StopVoice();
                }

                firstRound = false;
                var drum = _pokedDrum;
                foreach (var p in drumPokes)
                    p.SetInteractable(false);
                foreach (var btn in drumButtons)
                    btn.Disarm();
                foreach (var d in drums)
                    d.ClearHighlight();
                subtitle.Hide();

                // keep the popup in front of the rack face (rack front is about 0.4 m ahead of the focus point)
                var popupDist = 0.62f;
                if (_cam != null && rackFocus != null)
                    popupDist = Mathf.Clamp(rackFocus.position.z - 0.4f - 0.12f - _cam.position.z, 0.45f, 0.62f);
                PlaceInFrontOfHead(labelPopup.transform, popupDist);
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

                    // visuals first (popup turns red), the voice follows once it is on screen
                    labelPopup.SetWrong(true);
                    PresentableFx.Play(PresentableFx.Error, 0.7f);
                    PresentableFx.Haptic(true, 0.8f, 0.2f);
                    StopVoice();
                    yield return PresentableFx.Wait(0.35f);
                    StartCoroutine(SubtitledVoice(art.bnWrongLabel,
                        "Wrong label for this drum. Select the correct label again.", art.voWrongLabel));
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
                yield return PresentableFx.Wait(0.6f);
            }

            labelPopup.Chosen -= OnChosen;
            labelPopup.Closed -= OnClosed;
        }

        private IEnumerator RunCarrier()
        {
            foreach (var d in drums)
                d?.ClearHighlight();

            trolley.SetActive(true);
            PresentableFx.Play(PresentableFx.Chime, 0.8f);
            var baseScale = trolley.transform.localScale;
            yield return PresentableFx.Tween(0.5f, t => trolley.transform.localScale =
                baseScale * Mathf.LerpUnclamped(0.5f, 1f, t), PresentableFx.EaseOutBack);

            StartCoroutine(SubtitledVoice(art.bnCarrier, "Load the correct drum onto the trolley for transfer.",
                art.voCarrier));
            yield return Glide(trolleyStand.position, 1.2f);
            // the carrier is to the right of the rack; turn to face it behind a short fade (no smooth turn)
            yield return fader.FadeTo(1f, 0.2f);
            PlaceRig(trolleyStand.position, Flat(trolley.transform.position - trolleyStand.position));
            yield return fader.FadeTo(0f, 0.3f);

            _carrierPressed = false;
            void OnPressed(FloatingTouchButton _) => _carrierPressed = true;
            carrierButton.Pressed += OnPressed;
            carrierButton.Arm(true);
            while (!_carrierPressed)
                yield return null;
            carrierButton.Pressed -= OnPressed;
            carrierButton.Disarm();
            StopVoice();
            subtitle.Hide();

            // lift the drum from the floor onto the toe plate, then roll the trolley a short step
            PresentableFx.Play(PresentableFx.Whoosh, 0.55f);
            var tr = trolley.transform;
            var p0 = tr.position;
            var r0 = tr.rotation;
            Transform drum = null;
            var endPos = Vector3.zero;
            var endRot = Quaternion.identity;
            var startPos = Vector3.zero;
            var startRot = Quaternion.identity;
            if (carrierLoad != null)
            {
                carrierLoad.SetActive(true);
                drum = carrierLoad.transform;
                endPos = drum.localPosition;
                endRot = drum.localRotation;
                startPos = endPos + new Vector3(0f, -0.42f, 0.28f);
                startRot = Quaternion.identity;
                drum.localPosition = startPos;
                drum.localRotation = startRot;
            }

            yield return PresentableFx.Tween(0.8f, t =>
            {
                if (drum == null)
                    return;
                drum.localPosition = Vector3.Lerp(startPos, endPos, t);
                drum.localRotation = Quaternion.Slerp(startRot, endRot, t);
            }, PresentableFx.EaseOutCubic);
            PresentableFx.Play(PresentableFx.Whoosh, 0.35f);

            yield return PresentableFx.Tween(0.7f, t =>
            {
                tr.position = p0 + new Vector3(0f, 0f, 0.35f * t);
                tr.rotation = r0 * Quaternion.Euler(-4f * Mathf.Sin(t * Mathf.PI), 0f, 0f);
            });
            yield return PresentableFx.Wait(0.25f);
        }

        private void PlaceInFrontOfHead(Transform t, float distance)
        {
            if (_cam == null)
                return;
            var fwd = Flat(_cam.forward);
            t.position = new Vector3(_cam.position.x, _cam.position.y - 0.12f, _cam.position.z) + fwd * distance;
            t.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
        }

        // SC02: walk to the pallet, pick a container, carry it to the cabinet, place it; repeat for all six.
        // The Stand Here markers are walked onto; if the user does not get there the rig glides (comfort fallback).
        private IEnumerator RunPlacement()
        {
            subtitle.Show(null, "Touch a chemical container, then place it in the matching compartment.", 7f);
            SetRoomInteractable(true);
            inspect.SetCardAllowed(true);
            chart.SetDistance(0.58f);
            if (placement != null)
                placement.SetActive(true);
            yield return WalkOrGlide(hotspotSc02Pick);
            while (placement != null && placement.Placed < placement.Total)
            {
                while (!inspect.HasHeld)
                    yield return null;
                hotspotSc02Pick.Hide();
                // voice-over: put the chemical in the compartment that suits its type
                StartCoroutine(SubtitledVoice(null, "Place the chemical in the correct compartment for its type.",
                    art.voSc02PlaceHint));
                yield return WalkToShelf();
                if (placement != null)
                    placement.AdjustRows(_cam != null ? _cam.position.y - rig.position.y : 1.6f);
                // at the cabinet: no details card (its chart box sits where the finger works) and the large chart
                // opens close enough not to land inside the compartment
                // the details card stays available at the cabinet; it hides itself while a door is being tried
                chart.SetDistance(0.58f);
                var before = placement.Placed;
                while (placement.Placed == before)
                    yield return null;
                if (placement.Placed >= placement.Total)
                    break;
                inspect.SetCardAllowed(true);
                chart.SetDistance(0.58f);
                yield return WalkOrGlide(hotspotSc02Pick);
            }

            // let the last door finish closing before the question appears
            while (placement != null && placement.IsBusy)
                yield return null;
            yield return PresentableFx.Wait(0.4f);
            if (placement != null)
                placement.SetActive(false);
            SetRoomInteractable(false);
            subtitle.Hide();
            hotspotSc02Pick.Hide();
        }

        // Walk to the cabinet; if the user is not within reach after a while the rig glides there (no marker).
        private IEnumerator WalkToShelf()
        {
            if (shelfStand == null)
                yield break;
            var waited = 0f;
            while (waited < shelfPatience)
            {
                var d = new Vector2(_cam.position.x - shelfStand.position.x, _cam.position.z - shelfStand.position.z);
                if (d.magnitude < 0.8f)
                    yield break;
                // reading the details card or the large chart does not use up the time
                if (chart == null || !chart.IsOpen)
                    waited += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Glide(shelfStand.position, 1.1f);
        }

        private IEnumerator WalkOrGlide(FloorHotspot hotspot, float patience = 8f)
        {
            if (hotspot == null)
                yield break;
            _hotspotHit = false;
            void OnHit() => _hotspotHit = true;
            hotspot.Activated += OnHit;
            hotspot.Arm();
            var waited = 0f;
            while (!_hotspotHit && waited < patience)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_hotspotHit)
            {
                yield return Glide(hotspot.transform.position, 1.1f);
                hotspot.Activate();
            }

            hotspot.Activated -= OnHit;
        }

        private IEnumerator RunQuestion(PresentableMcqPanel panel, PresentableMcqPanel.Question q, bool useDrums,
            int scenario = 1)
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
                if (scenario == 3 && !correct && sc03Detonation != null)
                    sc03Detonation.Explode();
            }

            void OnWrongClosed()
            {
                if (scenario == 3 && sc03Detonation != null)
                    sc03Detonation.Clear();
            }

            // the voice follows the popup (it is not on screen yet when the answer is clicked)
            void OnPopupShown(bool correct)
            {
                if (_answerVoice != null)
                    StopCoroutine(_answerVoice);
                _answerVoice = StartCoroutine(AnswerVoice(correct, scenario));
            }

            void OnNext() => _nextPressed = true;

            panel.Answered += OnAnswered;
            panel.PopupShown += OnPopupShown;
            panel.NextPressed += OnNext;
            panel.WrongClosed += OnWrongClosed;
            panel.Show(q);
            while (!_nextPressed)
                yield return null;
            panel.Answered -= OnAnswered;
            panel.PopupShown -= OnPopupShown;
            panel.NextPressed -= OnNext;
            panel.WrongClosed -= OnWrongClosed;
            if (scenario == 3 && sc03Detonation != null)
                sc03Detonation.Clear();
            if (_answerVoice != null)
                StopCoroutine(_answerVoice);
            StopVoice();

            if (useDrums)
            {
                foreach (var d in drums)
                    d?.ClearHighlight();
            }

            panel.Hide();
            yield return PresentableFx.Wait(0.35f);
        }

        // No subtitle here: the always-on-top bar would cover the correct / wrong card, which already shows the text.
        private IEnumerator AnswerVoice(bool correct, int scenario)
        {
            yield return PresentableFx.Wait(0.25f);
            StopVoice();
            subtitle.Hide();
            var wrongClip = scenario == 3 && art.voSc03Wrong != null ? art.voSc03Wrong : art.voWrong;
            yield return PlayVoice(correct ? art.voCorrect : wrongClip);
            if (!correct)
                yield break;
            yield return PresentableFx.Wait(1f);
            AudioClip doneBn;
            AudioClip doneEn;
            if (scenario == 6)
            {
                doneBn = art.voSc06CompleteBn;
                doneEn = art.voSc06CompleteEn;
            }
            else if (scenario == 5)
            {
                doneBn = art.voSc05CompleteBn;
                doneEn = art.voSc05CompleteEn;
            }
            else if (scenario == 4)
            {
                doneBn = art.voSc04CompleteBn;
                doneEn = art.voSc04CompleteEn;
            }
            else if (scenario == 3)
            {
                doneBn = art.voSc03CompleteBn;
                doneEn = art.voSc03CompleteEn;
            }
            else if (scenario == 2)
            {
                doneBn = art.voSc02CompleteBn;
                doneEn = art.voSc02CompleteEn;
            }
            else
            {
                doneBn = art.voSc01CompleteBn;
                doneEn = art.voSc01CompleteEn;
            }

            yield return PlayVoice(doneBn);
            yield return PlayVoice(doneEn);
        }

        private void SetRoomInteractable(bool value)
        {
            if (doors != null)
            {
                foreach (var door in doors)
                    door?.SetInteractable(value);
            }

            chart?.SetInteractable(value);
            inspect?.SetInteractable(value);
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

        private int _subtitleToken;

        // Subtitle lives exactly as long as its voice-over; a newer subtitle is never hidden by an older one.
        private IEnumerator SubtitledVoice(Sprite bangla, string english, AudioClip clip)
        {
            var token = ++_subtitleToken;
            subtitle.Show(bangla, english);
            yield return PlayVoice(clip);
            if (token == _subtitleToken)
                subtitle.Hide();
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
                header = "PROFESSIONAL CHEMICAL SAFETY TRAINING - MCS Module",
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

        private static PresentableMcqPanel.Question BuildSc06Question()
        {
            var options = new PresentableMcqPanel.Option[Sc06Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc06Content.OptionLabels[i],
                    feedback = Sc06Content.OptionFeedback[i],
                    wrongTitle = Sc06Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc06Content.QuestionHeader,
                prompt = Sc06Content.QuestionPrompt,
                correctIndex = Sc06Content.CorrectIndex,
                correctTitle = Sc06Content.CorrectTitle,
                nextLabel = Sc06Content.FinishLabel,
                options = options,
                fancy = true // the wow look is Scenario 6 only; set it on another scenario's question to try it there
            };
        }

        private static PresentableMcqPanel.Question BuildSc05Question()
        {
            var options = new PresentableMcqPanel.Option[Sc05Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc05Content.OptionLabels[i],
                    feedback = Sc05Content.OptionFeedback[i],
                    wrongTitle = Sc05Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc05Content.QuestionHeader,
                prompt = Sc05Content.QuestionPrompt,
                correctIndex = Sc05Content.CorrectIndex,
                correctTitle = Sc05Content.CorrectTitle,
                nextLabel = Sc05Content.FinishLabel,
                options = options
            };
        }

        private static PresentableMcqPanel.Question BuildSc04Question()
        {
            var options = new PresentableMcqPanel.Option[Sc04Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc04Content.OptionLabels[i],
                    feedback = Sc04Content.OptionFeedback[i],
                    wrongTitle = Sc04Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc04Content.QuestionHeader,
                prompt = Sc04Content.QuestionPrompt,
                correctIndex = Sc04Content.CorrectIndex,
                correctTitle = Sc04Content.CorrectTitle,
                nextLabel = Sc04Content.FinishLabel,
                options = options
            };
        }

        private static PresentableMcqPanel.Question BuildSc03Question()
        {
            var options = new PresentableMcqPanel.Option[Sc03Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc03Content.OptionLabels[i],
                    feedback = Sc03Content.OptionFeedback[i],
                    wrongTitle = Sc03Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc03Content.QuestionHeader,
                prompt = Sc03Content.QuestionPrompt,
                correctIndex = Sc03Content.CorrectIndex,
                correctTitle = Sc03Content.CorrectTitle,
                nextLabel = Sc03Content.FinishLabel,
                options = options
            };
        }

        private static PresentableMcqPanel.Question BuildSc02Question()
        {
            return new PresentableMcqPanel.Question
            {
                header = "STORAGE DECISION CHALLENGE",
                prompt = "Question 2.1: Flammable Solvent Placement. Where does it go?",
                correctIndex = 1,
                nextLabel = "NEXT",
                correctTitle = "CORRECT PLACEMENT!",
                options = new[]
                {
                    new PresentableMcqPanel.Option
                    {
                        label = "A  With the oxidizers",
                        feedback = "Flammable + oxidizer = fire / explosion risk. Never store them together."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "B  Flammable compartment, away from oxidizers and acids",
                        feedback = "Best practice. Flammables stay in the fire-rated cabinet, segregated from " +
                                   "oxidizers and acids. This matches the compatibility chart."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "C  General chemical cabinet",
                        feedback = "General storage is not rated for a hazardous flammable solvent."
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = "D  Near the water station",
                        feedback = "Easy to reach, but a water station is the wrong place for a flammable solvent."
                    }
                }
            };
        }
    }
}
