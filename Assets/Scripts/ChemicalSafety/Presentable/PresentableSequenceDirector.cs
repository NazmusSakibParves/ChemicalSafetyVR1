using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ChemicalSafety.Localization;
using ChemicalSafety.Results;
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
        [SerializeField] private LoginScreen loginScreen;
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
        [Tooltip("Scenario 4 'Glove inspection complete' card (the Scenario 6 card style) and the back spot it opens from.")]
        [SerializeField] private Sc06Gate sc04Gate;
        [SerializeField] private Transform sc04GateStand;
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
        [Tooltip("In front of the table: the loose cap and the solvent container are within arm's reach.")]
        [SerializeField] private Transform sc06BenchStand;
        [SerializeField] private Sc06Pour sc06Pour;
        [SerializeField] private Sc06Gate sc06Gate;
        [SerializeField] private Sc06Lab lab;
        [SerializeField] private Sc06Jug sc06Jug;
        [SerializeField] private PresentableMcqPanel mcqSc06;
        [SerializeField] private Sc06Ambience sc06Ambience;

        [Header("SC07")]
        [SerializeField] private GameObject envSc07;
        [SerializeField] private Transform spawnSc07;
        [Tooltip("In front of the pillar: the extinguishers are within arm's reach.")]
        [SerializeField] private Transform sc07PillarStand;
        [Tooltip("Where the player stands for the MCQ: the three extinguishers and the whole hall are in view.")]
        [SerializeField] private Transform sc07QuestionStand;
        [Tooltip("Where the hero waits during the question: right of the MCQ, not in front of the player.")]
        [SerializeField] private Transform sc07HeroWait;
        [Tooltip("Where the hero appears for the extinguishing cutscene.")]
        [SerializeField] private Transform sc07HeroStart;
        [SerializeField] private Transform sc07FireAt;
        [SerializeField] private ExtinguisherInspect extinguishers;
        [SerializeField] private Sc06Gate sc07Gate;
        [SerializeField] private Sc07Crowd sc07Crowd;
        [SerializeField] private Sc07Fire sc07Fire;
        [SerializeField] private Sc07Ambience sc07Ambience;
        [SerializeField] private PresentableMcqPanel mcqSc07;

        [Header("SC08")]
        [SerializeField] private GameObject envSc08;
        [SerializeField] private Transform spawnSc08;
        [SerializeField] private Transform sc08TableStand;
        [Tooltip("In front of the island table: the tray is within arm reach.")]
        [SerializeField] private Transform sc08BenchStand;
        [SerializeField] private Transform sc08HoodStand;
        [SerializeField] private Transform sc08PanelStand;
        [Tooltip("In front of the PPE Donning Station on the right wall.")]
        [SerializeField] private Transform sc08PpeStand;
        [SerializeField] private Sc06Lab sc08Lab;
        [SerializeField] private Sc08Mix sc08Mix;
        [SerializeField] private Sc08Ppe sc08Ppe;
        [SerializeField] private Sc06Gate sc08Gate;
        [SerializeField] private Sc06Ambience sc08Ambience;
        [SerializeField] private PresentableMcqPanel mcqSc08;

        [Header("SC09")]
        [SerializeField] private GameObject envSc09;
        [SerializeField] private Transform spawnSc09;
        [SerializeField] private Transform sc09BenchStand;
        [SerializeField] private Transform sc09ShowerStand;
        [SerializeField] private Sc09Flask sc09Flask;
        [SerializeField] private Sc09Shower sc09Shower;
        [SerializeField] private Sc09Visor sc09Visor;
        [SerializeField] private Sc09Guide sc09Guide;
        [SerializeField] private Sc06Gate sc09Gate;
        [SerializeField] private Sc06Ambience sc09Ambience;
        [SerializeField] private PresentableMcqPanel mcqSc09;

        [Header("SC10")]
        [SerializeField] private GameObject envSc10;
        [SerializeField] private Transform spawnSc10;
        [SerializeField] private Transform sc10SpillStand;
        [SerializeField] private Transform sc10KitStand;
        [SerializeField] private Transform sc10PpeStand;
        [SerializeField] private Transform sc10PanelStand;
        [SerializeField] private Sc06Lab sc10Lab;
        [SerializeField] private Sc08Ppe sc10Ppe;
        [SerializeField] private Sc10Spill sc10Spill;
        [SerializeField] private Sc09Guide sc10Guide;
        [SerializeField] private Sc06Gate sc10Gate;
        [SerializeField] private Sc06Ambience sc10Ambience;
        [SerializeField] private PresentableMcqPanel mcqSc10;

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

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc10Only", false) && envSc10 != null)
            {
                yield return RunScenario10();
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc09Only", false) && envSc09 != null)
            {
                yield return RunScenario9();
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc08Only", false) && envSc08 != null)
            {
                yield return RunScenario8();
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc07Only", false) && envSc07 != null)
            {
                yield return RunScenario7();
                yield break;
            }
#endif
            if (loginScreen != null)
            {
                Stage("login");
                if (clipboard != null)
                    clipboard.SetSuspended(true); // the SDS sheet is not part of the login view
                yield return loginScreen.Run(_cam, rig != null ? rig.position.y : 0f, fader);
                if (clipboard != null)
                    clipboard.SetSuspended(false);
            }

            foreach (var d in drums)
                d?.SetCalloutVisible(false, 0.01f);

            _instructionClip = Sc01(art.voInstruction, art.trialInstruction);
            PlaceRig(spawnStart.position, spawnStart.forward);
            yield return fader.FadeTo(0f, 1.4f);
            yield return PresentableFx.Wait(0.4f);

            Stage("salam");

            // Salam and the instruction share one card. It stays at eye level the whole time,
            // whether the learner is sitting or standing, so it does not jump up after the greeting.
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            yield return SubtitledVoice(art.bnWelcomeSub, Sc01Content.WelcomeSub,
                Sc01(art.voSalam, art.trialSalam));
            instructionPanel.SetSpeaking(false);

            Stage("gap");

            // 2. Instruction after a 2s gap. Same eye-level card and centred Continue as Scenario 3.
            yield return PresentableFx.Wait(gapAfterSalam);
            Stage("instruction");
            instructionPanel.SetContinueCentered(true);
            instructionPanel.SetSpeaking(true);
            yield return SubtitledVoice(art.bnInstructionSub,
                Sc01Content.InstructionSub, _instructionClip);
            instructionPanel.SetSpeaking(false);

            // 2b. Voice-over finished: Continue appears (+ hint VO). Speaker replays the instruction any time.
            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
            instructionPanel.SetSpeaking(true);
            StartCoroutine(SubtitledVoice(art.bnContinueHint,
                Sc01Content.ContinueHint,
                Sc01(art.voContinueHint, art.trialContinueHint)));
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
            instructionPanel.SetContent(Sc02Content.InstructionEnglish, art.bnSc02InstructionPanel);
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
            var doneLine = envSc10 != null ? Sc10Content.ModuleComplete
                : envSc09 != null ? Sc09Content.ModuleComplete
                : envSc08 != null ? Sc08Content.ModuleComplete
                : envSc07 != null ? Sc07Content.ModuleComplete
                : envSc04 != null ? Sc04Content.ModuleComplete
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
                var again4 = true;
                while (again4)
                {
                    while (!gloves.AllSeen)
                    {
                        if (gloveBench != null && _cam != null)
                            gloveBench.Follow(_cam.position.y);
                        yield return null;
                    }

                    // all three seen: back to the spot behind the table (whole lab in view) for the card and the question
                    Stage("sc04Gate");
                    yield return PresentableFx.Wait(0.4f);
                    if (sc04GateStand != null)
                    {
                        yield return fader.FadeTo(1f, 0.3f);
                        PlaceRig(sc04GateStand.position, sc04GateStand.forward);
                        yield return fader.FadeTo(0f, 0.4f);
                    }

                    var choice4 = 0; // 1 = BACK, 2 = NEXT
                    void OnBack4() => choice4 = 1;
                    void OnNext4() => choice4 = 2;
                    sc04Gate.Back += OnBack4;
                    sc04Gate.Next += OnNext4;
                    sc04Gate.Show();
                    while (choice4 == 0)
                        yield return null;
                    sc04Gate.Back -= OnBack4;
                    sc04Gate.Next -= OnNext4;
                    sc04Gate.Hide();
                    if (choice4 == 1)
                    {
                        // review again: back to the glove table, the three gloves are new again
                        yield return fader.FadeTo(1f, 0.3f);
                        if (gloveBench != null && _cam != null)
                            gloveBench.SnapTo(_cam.position.y);
                        yield return Glide(gloves.StandInFront(), 0.01f);
                        gloves.Begin();
                        yield return fader.FadeTo(0f, 0.4f);
                        Stage("sc04Explore");
                    }
                    else
                    {
                        again4 = false;
                    }
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
            if (sc06Ambience != null)
                sc06Ambience.End();

            // SC07 (factory fire) starts after the SC06 question's NEXT
            if (envSc07 != null)
                yield return RunScenario7();
        }

        // SC07: factory fire. Instruction card, the fire starts and the workers flee, SDS, inspect the three extinguishers,
        // "inspection complete" card, the MCQ; the right answer makes the hero put the fire out.
        private IEnumerator RunScenario7()
        {
            Stage("sc07");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06 })
                if (e != null)
                    e.SetActive(false);
            if (sc06Ambience != null)
                sc06Ambience.End();
            envSc07.SetActive(true);
            clipboard.SetSheet(art.sdsSc07);
            clipboard.SetVisible(false);
            if (sc07Crowd != null)
                sc07Crowd.ResetScene();
            PlaceRig(spawnSc07.position, spawnSc07.forward);
            if (sc07Ambience != null)
                sc07Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc07Intro");
            _instructionClip = art.voSc07Instruction != null ? art.voSc07Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc07Content.InstructionEnglish, art.bnSc07InstructionPanel);
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

            // the fire starts, the alarm rings, the workers see it and run; the hero stays
            Stage("sc07Event");
            if (sc07Crowd != null)
                yield return sc07Crowd.PlayEvent();
            else
                yield return PresentableFx.Wait(3f);

            // the safety data sheet comes up in the left hand (X shows and hides it any time)
            Stage("sc07Sds");
            clipboard.SetVisible(true);
            var sdsWait = 0f;
            while (clipboard.IsVisible && sdsWait < 9f)
            {
                sdsWait += Time.unscaledDeltaTime;
                yield return null;
            }

            clipboard.SetVisible(false);

            // to the pillar: the arrow ticks over the extinguishers one by one
            yield return fader.FadeTo(1f, 0.3f);
            if (sc07PillarStand != null)
                PlaceRig(sc07PillarStand.position, sc07PillarStand.forward);
            yield return fader.FadeTo(0f, 0.4f);

            Stage("sc07Explore");
            var again7 = true;
            Coroutine extVoice = null;

            IEnumerator SayExt(int index)
            {
                extinguishers.SetSpeaking(true);
                yield return PlayVoice(art.voSc07Ext != null && index < art.voSc07Ext.Length ? art.voSc07Ext[index] : null);
                extinguishers.SetSpeaking(false);
                extVoice = null;
            }

            void OnExtOpened(int index)
            {
                StopVoice();
                if (extVoice != null)
                    StopCoroutine(extVoice);
                extVoice = null;
                extinguishers.SetSpeaking(false);
                if (art.voSc07Ext != null && index >= 0 && index < art.voSc07Ext.Length && art.voSc07Ext[index] != null)
                    extVoice = StartCoroutine(SayExt(index));
            }

            void OnExtClosed()
            {
                if (extVoice != null)
                    StopCoroutine(extVoice);
                extVoice = null;
                StopVoice();
                extinguishers.SetSpeaking(false);
            }

            if (extinguishers != null)
            {
                extinguishers.Opened += OnExtOpened;
                extinguishers.Closed += OnExtClosed;
                extinguishers.SpeakerClicked += OnExtOpened;
                extinguishers.Begin();
                while (again7)
                {
                    while (!extinguishers.AllSeen)
                        yield return null;
                    Stage("sc07Gate");
                    yield return PresentableFx.Wait(0.35f);
                    var choice7 = 0; // 1 = BACK, 2 = NEXT
                    void OnBack7() => choice7 = 1;
                    void OnNext7() => choice7 = 2;
                    sc07Gate.Back += OnBack7;
                    sc07Gate.Next += OnNext7;
                    sc07Gate.Show();
                    StartCoroutine(PlayVoice(art.voSc07Gate));
                    while (choice7 == 0)
                        yield return null;
                    sc07Gate.Back -= OnBack7;
                    sc07Gate.Next -= OnNext7;
                    sc07Gate.Hide();
                    StopAllVoiceCoroutines();
                    StopVoice();
                    if (choice7 == 1)
                    {
                        extinguishers.Begin();
                        Stage("sc07Explore");
                    }
                    else
                    {
                        again7 = false;
                    }
                }

                extinguishers.Opened -= OnExtOpened;
                extinguishers.Closed -= OnExtClosed;
                extinguishers.SpeakerClicked -= OnExtOpened;
                OnExtClosed();
                extinguishers.Stop();
            }

            // to a vantage point from where the three extinguishers and the whole hall can be seen next to the MCQ
            yield return fader.FadeTo(1f, 0.3f);
            if (sc07QuestionStand != null)
                PlaceRig(sc07QuestionStand.position, sc07QuestionStand.forward);
            if (sc07Crowd != null && sc07HeroWait != null && sc07FireAt != null)
                sc07Crowd.PlaceHero(sc07HeroWait.position, sc07FireAt.position);
            yield return fader.FadeTo(0f, 0.4f);
            Stage("sc07Question");
            // straight ahead at eye level, 0.7 m: a slightly extended arm reaches it, it is not at the eyes (the right / wrong cards share the canvas)
            PlaceAtEyeLevel(mcqSc07.transform, 0.7f, false);
            var q7 = BuildSc07Question();
            _sc07WinDone = false;
            q7.holdCorrect = () => !_sc07WinDone; // the right-answer card opens only after the fire is out
            yield return RunQuestion(mcqSc07, q7, false, 7);
            if (sc07Ambience != null)
                sc07Ambience.End();
            Stage("sc07Done");

            // SC08 (acid dilution) starts after the SC07 question's NEXT
            if (envSc08 != null)
                yield return RunScenario8();
        }

        // The right answer: back to the first view, the hero fetches the CO2 extinguisher and puts the fire out.
        private bool _sc07WinDone = true;

        private IEnumerator Sc07Win(PresentableMcqPanel panel)
        {
            Stage("sc07Extinguish");
            yield return PresentableFx.Wait(0.4f);
            // hidden by scale (the panel keeps running its own coroutine, which waits for this scene)
            var panelScale = panel.transform.localScale;
            panel.transform.localScale = Vector3.zero;
            yield return fader.FadeTo(1f, 0.35f);
            if (spawnSc07 != null)
                PlaceRig(spawnSc07.position, spawnSc07.forward);
            if (sc07Crowd != null && sc07HeroStart != null && sc07PillarStand != null)
                sc07Crowd.PlaceHero(sc07HeroStart.position, sc07PillarStand.position + Vector3.back * 2f);
            yield return fader.FadeTo(0f, 0.5f);
            // "the fire is out" is said only when the flames are really out
            void OnFireOut() => StartCoroutine(PlayVoice(art.voSc07Hero));
            if (sc07Crowd != null)
            {
                sc07Crowd.FireOut += OnFireOut;
                yield return sc07Crowd.HeroExtinguish();
                sc07Crowd.FireOut -= OnFireOut;
            }
            yield return fader.FadeTo(1f, 0.35f);
            if (sc07QuestionStand != null)
                PlaceRig(sc07QuestionStand.position, sc07QuestionStand.forward);
            panel.transform.localScale = panelScale;
            _sc07WinDone = true;
            yield return fader.FadeTo(0f, 0.45f);
        }

        // SC08: acid dilution. Instruction card, PPE donning station (four items), fume hood on, carry the tray to the hood,
        // open the sash, add the acid to the water slowly while stirring, "practice complete" card, the MCQ.
        private IEnumerator RunScenario8()
        {
            Stage("sc08");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06, envSc07 })
                if (e != null)
                    e.SetActive(false);
            if (sc07Ambience != null)
                sc07Ambience.End();
            envSc08.SetActive(true);
            clipboard.SetSheet(art.sdsSc08);
            clipboard.SetVisible(false);
            sc08Lab.ResetLab();
            sc08Mix.ResetAll();
            sc08Ppe.ResetAll();
            PlaceRig(spawnSc08.position, spawnSc08.forward);
            sc08Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc08Intro");
            _instructionClip = art.voSc08Instruction != null ? art.voSc08Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc08Content.InstructionEnglish, art.bnSc08InstructionPanel);
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

            var again = true;
            while (again)
            {
                yield return Sc08Round();

                Stage("sc08Gate");
                yield return PresentableFx.Wait(0.6f);
                sc08Lab.CloseSash();
                yield return fader.FadeTo(1f, 0.35f);
                PlaceRig(sc08TableStand.position, sc08TableStand.forward);
                subtitle.Hide();
                yield return fader.FadeTo(0f, 0.5f);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc08Gate.Back += OnBack;
                sc08Gate.Next += OnNext;
                sc08Gate.Show();
                StartCoroutine(PlayVoice(art.voSc08Gate));
                while (choice == 0)
                    yield return null;
                sc08Gate.Back -= OnBack;
                sc08Gate.Next -= OnNext;
                sc08Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                if (choice == 1)
                {
                    yield return fader.FadeTo(1f, 0.3f);
                    sc08Lab.ResetLab();
                    sc08Mix.ResetAll();
                    sc08Ppe.ResetAll();
                    yield return PresentableFx.Wait(0.3f);
                }
                else
                {
                    again = false;
                }
            }

            Stage("sc08Question");
            PlaceAtEyeLevel(mcqSc08.transform, 0.7f, false);
            yield return RunQuestion(mcqSc08, BuildSc08Question(), false, 8);
            if (sc08Ambience != null)
                sc08Ambience.End();
            Stage("sc08Done");

            // SC09 (chemical splash, safety shower) starts after the SC08 question's NEXT
            if (envSc09 != null)
                yield return RunScenario9();
        }

        private IEnumerator Sc08Round()
        {
            // 1. the PPE donning station: the four items one by one
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc08PpeStand.position, sc08PpeStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
            Stage("sc08Ppe");
            sc08Ppe.Begin();
            StartCoroutine(SubtitledVoice(art.bnSc08PpeHint, Sc08Content.PpeHint, art.voSc08Ppe));
            while (!sc08Ppe.AllDone)
                yield return null;
            StopVoice();
            StartCoroutine(PlayVoice(art.voSc08PpeDone));
            yield return PresentableFx.Wait(1.4f);
            sc08Ppe.End();

            // 2. switch the fume hood on
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc08PanelStand.position, sc08PanelStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
            Stage("sc08Hood");
            sc08Lab.SetSwitchesInteractable(true);
            sc08Lab.PointAt(sc08Lab.Rocker(0), new Vector3(0f, 0.2f, 0.06f));
            StartCoroutine(SubtitledVoice(art.bnSc06HoodHint, Sc08Content.HoodHint, art.voSc08Hood));
            while (!sc08Lab.HoodOn)
                yield return null;
            sc08Lab.HideArrow();
            sc08Lab.SetSwitchesInteractable(false); // the hood stays on while the user works
            StartCoroutine(SubtitledVoice(art.bnSc08TrayHint, Sc08Content.TrayHint, art.voSc08HoodOn));

            // 3. to the table: lift the tray (the acid is fuming in the open beaker)
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc08BenchStand.position, sc08BenchStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
            Stage("sc08Tray");
            sc08Mix.SetTrayInteractable(true);
            sc08Lab.PointAt(sc08Mix.Tray, new Vector3(0f, 0.42f, 0.02f));
            while (!sc08Mix.Carrying)
                yield return null;
            sc08Lab.HideArrow();

            // 4. carry it to the hood, touch the glass door (only works while the hood runs)
            Stage("sc08Sash");
            StopVoice();
            subtitle.Hide();
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc08HoodStand.position, sc08HoodStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
            sc08Lab.SetSashInteractable(true);
            sc08Lab.PointAt(sc08Lab.Sash, new Vector3(0f, 0.85f, 0.08f));
            void OnBlocked()
            {
                StopVoice();
                StartCoroutine(SubtitledVoice(art.bnSc06HoodWarn, Sc08Content.HoodWarn, art.voSc08HoodWarn));
                sc08Lab.PointAt(sc08Lab.Rocker(0), new Vector3(0f, 0.2f, 0.06f));
            }

            sc08Lab.SashBlocked += OnBlocked;
            StartCoroutine(SubtitledVoice(art.bnSc08SashHint, Sc08Content.SashHint, art.voSc08Sash));
            while (!sc08Lab.SashOpen)
                yield return null;
            sc08Lab.SashBlocked -= OnBlocked;
            sc08Lab.HideArrow();
            sc08Lab.SetSashInteractable(false);
            yield return sc08Mix.PlaceInHood();

            // 5. add the acid slowly while the other hand stirs
            Stage("sc08Mix");
            StopVoice();
            StartCoroutine(SubtitledVoice(art.bnSc08MixHint, Sc08Content.MixHint, art.voSc08Mix));
            sc08Mix.SetMixInteractable(true);
            sc08Lab.PointAt(sc08Mix.WaterBeaker, new Vector3(0f, 0.5f, 0.06f));
            var mixed = false;
            void OnMixed() => mixed = true;
            sc08Mix.MixFinished += OnMixed;
            while (!mixed)
                yield return null;
            sc08Mix.MixFinished -= OnMixed;
            sc08Lab.HideArrow();
            Stage("sc08MixDone");
        }

        // SC09: chemical splash. Pour the purple sample, the flask slips and breaks, the liquid lands on the face shield, read the SDS,
        // walk to the safety shower, push it, wash the shield clean; gate card; MCQ.
        private IEnumerator Sc09Card(string english, Sprite bangla, AudioClip clip)
        {
            instructionPanel.SetContent(english, bangla);
            instructionPanel.SetContinueCentered(true);
            BeginEyeInstruction();
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            _instructionClip = clip;
            if (clip != null)
                yield return PlayVoice(clip);
            instructionPanel.SetSpeaking(false);

            _continuePressed = false;
            instructionPanel.SpeakerClicked += OnSpeakerClicked;
            instructionPanel.Continue += OnContinueClicked;
            yield return instructionPanel.ShowContinue();
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
        }

        private IEnumerator RunScenario9()
        {
            Stage("sc09");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06, envSc07, envSc08 })
                if (e != null)
                    e.SetActive(false);
            if (sc08Ambience != null)
                sc08Ambience.End();
            envSc09.SetActive(true);
            clipboard.SetSheet(art.sdsSc09);
            clipboard.SetVisible(false);
            sc09Flask.ResetAll();
            sc09Shower.ResetAll();
            PlaceRig(spawnSc09.position, spawnSc09.forward);
            sc09Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc09Intro");
            _instructionClip = art.voSc09Instruction != null ? art.voSc09Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc09Content.InstructionEnglish, art.bnSc09InstructionPanel);
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

            var again = true;
            while (again)
            {
                yield return Sc09Round();

                Stage("sc09Gate");
                yield return PresentableFx.Wait(0.8f);
                sc09Shower.StopWater();
                sc09Shower.HideHolo();
                yield return fader.FadeTo(1f, 0.35f);
                PlaceRig(spawnSc09.position, spawnSc09.forward);
                subtitle.Hide();
                yield return fader.FadeTo(0f, 0.5f);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc09Gate.Back += OnBack;
                sc09Gate.Next += OnNext;
                sc09Gate.Show();
                StartCoroutine(PlayVoice(art.voSc09Gate));
                while (choice == 0)
                    yield return null;
                sc09Gate.Back -= OnBack;
                sc09Gate.Next -= OnNext;
                sc09Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                if (choice == 1)
                {
                    yield return fader.FadeTo(1f, 0.3f);
                    sc09Flask.ResetAll();
                    sc09Shower.ResetAll();
                    yield return PresentableFx.Wait(0.3f);
                }
                else
                {
                    again = false;
                }
            }

            Stage("sc09Question");
            PlaceAtEyeLevel(mcqSc09.transform, 0.7f, false);
            yield return RunQuestion(mcqSc09, BuildSc09Question(), false, 9);
            if (sc09Ambience != null)
                sc09Ambience.End();
            Stage("sc09Done");
            if (envSc10 != null)
                yield return RunScenario10();
        }

        private IEnumerator Sc09Round()
        {
            // 1. at the bench: touch the purple sample flask, it is lifted and poured
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc09BenchStand.position, sc09BenchStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
            Stage("sc09Flask");
            sc09Flask.SetInteractable(true);
            sc09Guide.PointAt(sc09Flask.Flask, new Vector3(0f, 0.34f, 0f));
            StartCoroutine(SubtitledVoice(art.bnSc09FlaskHint, Sc09Content.FlaskHint, art.voSc09Flask));
            var splashed = false;
            void OnSplashed() => splashed = true;
            sc09Flask.Splashed += OnSplashed;
            while (!splashed)
            {
                if (sc09Flask.Carrying || sc09Flask.Broken)
                    sc09Guide.Hide();
                yield return null;
            }

            sc09Flask.Splashed -= OnSplashed;
            sc09Guide.Hide();

            // 2. the accident: "INSTRUCTIONS" card (the shield is covered with drops)
            Stage("sc09Spill");
            StopVoice();
            subtitle.Hide();
            yield return PresentableFx.Wait(0.9f);
            yield return Sc09Card(Sc09Content.SpillInstruction, art.bnSc09SpillInstruction, art.voSc09Spill);

            // 3. the safety data sheet in the left hand
            Stage("sc09Sds");
            StartCoroutine(PlayVoice(art.voSc09Sds));
            clipboard.SetVisible(true);
            var sdsWait = 0f;
            while (clipboard.IsVisible && sdsWait < 10f)
            {
                sdsWait += Time.unscaledDeltaTime;
                yield return null;
            }

            clipboard.SetVisible(false);
            StopVoice();

            // 4. to the safety shower: the green STAND HERE mark, then the push plate
            yield return fader.FadeTo(1f, 0.3f);
            PlaceRig(sc09ShowerStand.position - sc09ShowerStand.forward * 2.4f, sc09ShowerStand.forward);
            sc09Shower.ShowStandMark(true);
            yield return fader.FadeTo(0f, 0.5f);
            Stage("sc09Walk");
            StartCoroutine(SubtitledVoice(art.bnSc09WalkHint, Sc09Content.WalkHint, art.voSc09Walk));
            yield return PresentableFx.Wait(2.4f);
            yield return Glide(sc09ShowerStand.position, 1.8f);
            sc09Shower.ShowStandMark(false);

            Stage("sc09Wash");
            sc09Shower.SetInteractable(true);
            sc09Guide.PointAt(sc09Shower.PushPlate, new Vector3(0f, 0.28f, 0.05f));
            StopVoice();
            StartCoroutine(SubtitledVoice(art.bnSc09PushHint, Sc09Content.PushHint, art.voSc09Push));
            while (!sc09Shower.Running)
                yield return null;
            sc09Guide.Hide();
            StopVoice();
            StartCoroutine(SubtitledVoice(art.bnSc09FlushHint, Sc09Content.FlushHint, art.voSc09Flush));
            yield return sc09Shower.RunWash();
            Stage("sc09WashDone");
        }

        private void TurnAroundHead(Transform t, float degrees)
        {
            if (_cam == null)
                return;
            t.RotateAround(_cam.position, Vector3.up, degrees);
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

            // 2. to the table: close the open container with its cap (the vapor stops), then lift it into the left hand
            Stage("sc06Cap");
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc06BenchStand.position, sc06BenchStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
            sc06Jug.SetCapInteractable(true);
            lab.PointAt(sc06Jug.CapTransform, new Vector3(0f, 0.2f, 0.04f));
            yield return PresentableFx.Wait(2.4f);
            StartCoroutine(SubtitledVoice(art.bnSc06CapHint, Sc06Content.CapHint, art.voSc06Cap));
            var closed = false;
            void OnClosed() => closed = true;
            sc06Jug.CapClosed += OnClosed;
            while (!closed)
                yield return null;
            sc06Jug.CapClosed -= OnClosed;
            lab.HideArrow();
            lab.SetVaporOn(false);
            yield return PresentableFx.Wait(0.5f);

            Stage("sc06Jug");
            sc06Jug.SetInteractable(true);
            lab.PointAt(sc06Jug.JugTransform, new Vector3(0f, 0.42f, 0.02f));
            StartCoroutine(SubtitledVoice(art.bnSc06JugHint, Sc06Content.JugHint, art.voSc06Jug));
            while (!sc06Jug.Held)
                yield return null;
            lab.HideArrow();

            // 3. to the hood, touch the glass door; it opens from the bottom upwards (only while the hood runs)
            Stage("sc06Sash");
            StopVoice();
            subtitle.Hide();
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc06HoodStand.position, sc06HoodStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
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
            yield return SubtitledVoice(art.bnPickLabel, Sc01Content.PickLabelHint,
                Sc01(art.voPickLabel, art.trialPickLabel));
            yield return PresentableFx.Wait(0.3f);
            yield return SubtitledVoice(art.bnTouchDrum, Sc01Content.TouchDrumHint,
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
                        Sc01Content.WrongLabelHint,
                        Sc01(art.voWrongLabel, art.trialWrongLabel)));
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

            StartCoroutine(SubtitledVoice(art.bnCarrier, Sc01Content.CarrierHint,
                Sc01(art.voCarrier, art.trialCarrier)));
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
            subtitle.Show(null, Sc02Content.PlaceHint, 7f);
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
                StartCoroutine(SubtitledVoice(null, Sc02Content.PlaceHint2,
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

        private static readonly Regex LetterPrefix = new Regex(@"^[A-D](\s+)", RegexOptions.Compiled);

        /// <summary>
        /// Anti-cheating: the options of every question appear in a different order for every session (same texts, the correct
        /// one is not always in the same slot). Everything that belongs to an option (feedback, wrong-card label, icons) moves with
        /// it. Returns, for each displayed slot, the index the option had in the unshuffled question. The user never sees A / B / C / D
        /// (the letters exist only in the results file); numbered drum labels ("1. ...") keep their number because it names the drum.
        /// </summary>
        private static int[] ShuffleOptions(PresentableMcqPanel.Question q, string questionId)
        {
            var n = q.options.Length;
            var order = OptionShuffle.Order(n, SessionResults.ShuffleSeed, questionId, true);
            var shuffled = new PresentableMcqPanel.Option[n];
            for (var i = 0; i < n; i++)
                shuffled[i] = q.options[order[i]];
            q.correctIndex = Array.IndexOf(order, q.correctIndex);
            q.options = shuffled;
            return order;
        }

        private static string OptionText(string label) => LetterPrefix.Replace(label ?? string.Empty, string.Empty).Trim();

        private IEnumerator RunQuestion(PresentableMcqPanel panel, PresentableMcqPanel.Question q, bool useDrums,
            int scenario = 1)
        {
            _nextPressed = false;

            var lessonId = "SC" + scenario.ToString("00");
            var questionId = lessonId + ".Q1";
            var order = ShuffleOptions(q, questionId);
            q.sourceOrder = order;
            SessionResults.BeginScenario(lessonId, Loc.Suffix());
            var shown = new List<OptionPair>();
            for (var i = 0; i < q.options.Length; i++)
                shown.Add(new OptionPair { key = ((char)('A' + i)).ToString(), text = OptionText(q.options[i].label), optionId = order[i].ToString() });
            SessionResults.OpenQuestion(questionId, q.prompt, shown, ((char)('A' + q.correctIndex)).ToString());

            void OnAnswered(int index, bool correct)
            {
                SessionResults.RecordTry(((char)('A' + index)).ToString(), correct);
                if (useDrums)
                {
                    var drum = order[index]; // the drum the option stands for, not the slot it is shown in
                    foreach (var d in drums)
                        d?.ClearHighlight();
                    if (drum < drums.Length && drums[drum] != null)
                        drums[drum].Highlight(correct ? PresentableUi.Green : PresentableUi.Red, true);
                }

                StopVoice();
                if (scenario == 3 && !correct && sc03Detonation != null)
                    sc03Detonation.Explode();
                if (scenario == 7 && correct)
                    StartCoroutine(Sc07Win(panel)); // the fire is put out first, then the right-answer card opens
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

            SessionResults.EndScenario();
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
            var correctClip = art.voCorrect;
            if (scenario == 1)
            {
                correctClip = Sc01(art.voCorrect, art.trialCorrect);
                wrongClip = Sc01(art.voWrong, art.trialWrong);
            }

            yield return PlayVoice(correct ? correctClip : wrongClip);
            if (!correct)
                yield break;
            yield return PresentableFx.Wait(1f);
            if (scenario == 10)
            {
                yield return PlayVoice(Loc.IsBangla ? art.voSc10CompleteBn : art.voSc10CompleteEn);
                yield break;
            }

            if (scenario == 9)
            {
                yield return PlayVoice(Loc.IsBangla ? art.voSc09CompleteBn : art.voSc09CompleteEn);
                yield break;
            }

            if (scenario == 8)
            {
                yield return PlayVoice(Loc.IsBangla ? art.voSc08CompleteBn : art.voSc08CompleteEn);
                yield break;
            }

            if (scenario == 7)
            {
                // the fire is already out (the card opened after it); the completion lines follow
                yield return PlayVoice(Loc.IsBangla ? art.voSc07CompleteBn : art.voSc07CompleteEn);
                yield break;
            }

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
                doneBn = Sc01(art.voSc01CompleteBn, art.trialSc01CompleteBn);
                doneEn = art.voSc01CompleteEn;
            }

            yield return PlayVoice(Loc.IsBangla ? doneBn : doneEn);
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

        // Scenario 1 trial recordings. Scenarios 2-8 keep calling the original slots directly.
        private AudioClip Sc01(AudioClip original, AudioClip trial)
        {
            if (art != null && !art.useSc01OriginalVoice && trial != null)
                return trial;
            return original;
        }

        private static readonly Dictionary<string, AudioClip> EnglishClips = new Dictionary<string, AudioClip>();

        /// <summary>English mode plays the English twin (Resources/VoEn/&lt;name&gt;) of every Bangla clip; Bangla mode plays the clip as is.</summary>
        private static AudioClip LocalizedClip(AudioClip clip)
        {
            if (clip == null || Loc.IsBangla)
                return clip;
            var n = clip.name;
            if (n.EndsWith("_en"))
                return clip;
            var baseName = n.EndsWith("_bn") ? n.Substring(0, n.Length - 3) : n;
            if (!EnglishClips.TryGetValue(baseName, out var en) || en == null)
            {
                en = Resources.Load<AudioClip>("VoEn/" + baseName);
                EnglishClips[baseName] = en;
            }

            if (en == null)
            {
                Debug.LogError("[Voice] No English clip for " + n + " (Resources/VoEn/" + baseName + ")");
                return clip;
            }

            return en;
        }

        private IEnumerator PlayVoice(AudioClip clip)
        {
            clip = LocalizedClip(clip);
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
            var label = Sc01Content.OptionLabels;
            var wrong = Sc01Content.OptionWrongLabels;
            var feedback = Sc01Content.OptionFeedback;
            return new PresentableMcqPanel.Question
            {
                header = Sc01Content.QuestionHeader,
                prompt = Sc01Content.QuestionPrompt,
                correctTitle = Sc01Content.CorrectTitle,
                wrongTitle = Sc01Content.WrongTitle,
                nextLabel = Sc01Content.FinishLabel,
                correctIndex = Sc01Content.CorrectIndex,
                options = new[]
                {
                    new PresentableMcqPanel.Option
                    {
                        label = label[0],
                        wrongLabel = wrong[0],
                        icons = new[] { art.thumbNoLabel },
                        overlays = new[] { art.labelFaded },
                        feedback = feedback[0]
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = label[1],
                        wrongLabel = wrong[1],
                        icons = new[] { art.thumbNoLabel },
                        feedback = feedback[1]
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = label[2],
                        wrongLabel = wrong[2],
                        icons = new[] { art.iconGhs, art.iconSds, art.thumbNoLabel },
                        overlays = new Sprite[] { null, null, art.labelMethanol },
                        feedback = feedback[2]
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = label[3],
                        icons = new[] { art.thumbNoLabel },
                        overlays = new[] { art.labelIsopropanol },
                        feedback = feedback[3]
                    }
                }
            };
        }

        private IEnumerator RunScenario10()
        {
            Stage("sc10");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06, envSc07, envSc08, envSc09 })
                if (e != null)
                    e.SetActive(false);
            if (sc09Ambience != null)
                sc09Ambience.End();
            envSc10.SetActive(true);
            clipboard.SetSheet(art.sdsSc10);
            clipboard.SetVisible(false);
            sc10Spill.ResetAll();
            sc10Lab.ResetLab();
            sc10Ppe.ResetAll();
            PlaceRig(sc10SpillStand.position, sc10SpillStand.forward);
            sc10Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc10Intro");
            yield return Sc09Card(Sc10Content.InstructionEnglish, art.bnSc10InstructionPanel,
                art.voSc10Instruction != null ? art.voSc10Instruction : art.voInstruction);

            var again = true;
            while (again)
            {
                yield return Sc10Round();

                Stage("sc10Gate");
                yield return PresentableFx.Wait(0.8f);
                yield return fader.FadeTo(1f, 0.35f);
                PlaceRig(sc10SpillStand.position, sc10SpillStand.forward);
                subtitle.Hide();
                yield return fader.FadeTo(0f, 0.5f);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc10Gate.Back += OnBack;
                sc10Gate.Next += OnNext;
                sc10Gate.Show();
                StartCoroutine(PlayVoice(art.voSc10Gate));
                while (choice == 0)
                    yield return null;
                sc10Gate.Back -= OnBack;
                sc10Gate.Next -= OnNext;
                sc10Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                if (choice == 1)
                {
                    yield return fader.FadeTo(1f, 0.3f);
                    sc10Spill.ResetAll();
                    sc10Lab.ResetLab();
                    sc10Ppe.ResetAll();
                    yield return PresentableFx.Wait(0.3f);
                }
                else
                {
                    again = false;
                }
            }

            Stage("sc10Question");
            PlaceAtEyeLevel(mcqSc10.transform, 0.7f, false);
            yield return RunQuestion(mcqSc10, BuildSc10Question(), false, 10);
            if (sc10Ambience != null)
                sc10Ambience.End();
            Stage("sc10Done");
        }

        private IEnumerator Sc10Round()
        {
            // 1. the safety data sheet (spill response steps)
            yield return fader.FadeTo(1f, 0.25f);
            PlaceRig(sc10SpillStand.position, sc10SpillStand.forward);
            yield return fader.FadeTo(0f, 0.35f);
            Stage("sc10Sds");
            StartCoroutine(SubtitledVoice(art.bnSc10SdsHint, Sc10Content.SdsHint, art.voSc10Sds));
            clipboard.SetVisible(true);
            var sdsWait = 0f;
            while (clipboard.IsVisible && sdsWait < 12f)
            {
                sdsWait += Time.unscaledDeltaTime;
                yield return null;
            }

            clipboard.SetVisible(false);
            StopVoice();

            // 2. PPE donning station
            yield return fader.FadeTo(1f, 0.3f);
            PlaceRig(sc10PpeStand.position, sc10PpeStand.forward);
            yield return fader.FadeTo(0f, 0.4f);
            Stage("sc10Ppe");
            StartCoroutine(SubtitledVoice(art.bnSc10PpeHint, Sc10Content.PpeHint, art.voSc10Ppe));
            sc10Ppe.Begin();
            while (!sc10Ppe.AllDone)
                yield return null;
            sc10Ppe.End();
            StopVoice();
            yield return PresentableFx.Wait(0.5f);

            // 3. ventilate: the fume hood switch
            yield return fader.FadeTo(1f, 0.3f);
            PlaceRig(sc10PanelStand.position, sc10PanelStand.forward);
            yield return fader.FadeTo(0f, 0.4f);
            Stage("sc10Vent");
            StartCoroutine(SubtitledVoice(art.bnSc10VentHint, Sc10Content.VentHint, art.voSc10Vent));
            sc10Lab.SetSwitchesInteractable(true);
            sc10Lab.PointAt(sc10Lab.Rocker(0), new Vector3(0f, 0.2f, 0.06f));
            while (!sc10Lab.HoodOn)
                yield return null;
            sc10Lab.HideArrow();
            sc10Lab.SetSwitchesInteractable(false);
            StopVoice();
            yield return PresentableFx.Wait(1.0f);

            // 4. the spill kit: open it, take the five items
            yield return fader.FadeTo(1f, 0.3f);
            PlaceRig(sc10KitStand.position, sc10KitStand.forward);
            yield return fader.FadeTo(0f, 0.4f);
            Stage("sc10Kit");
            StartCoroutine(SubtitledVoice(art.bnSc10KitHint, Sc10Content.KitHint, art.voSc10Kit));
            sc10Spill.SetKitInteractable(true);
            sc10Guide.PointAt(sc10Spill.KitPoke.transform, new Vector3(0f, 0.72f, 0f));
            while (!sc10Spill.KitOpen)
                yield return null;
            sc10Guide.Hide();
            StopVoice();
            yield return PresentableFx.Wait(0.4f);

            Stage("sc10Collect");
            StartCoroutine(SubtitledVoice(art.bnSc10CollectHint, Sc10Content.CollectHint, art.voSc10Collect));
            sc10Spill.ShowCollection(true);
            sc10Spill.SetItemsInteractable(true);
            while (!sc10Spill.AllCollected)
            {
                var next = -1;
                for (var i = 0; i < 5; i++)
                {
                    var t = sc10Spill.Item(i);
                    if (t != null && t.gameObject.activeSelf)
                    {
                        next = i;
                        break;
                    }
                }

                if (next >= 0)
                    sc10Guide.PointAt(sc10Spill.Item(next), new Vector3(0f, 0.32f, 0f));
                else
                    sc10Guide.Hide();
                yield return null;
            }

            sc10Guide.Hide();
            StopVoice();
            yield return PresentableFx.Wait(0.9f);
            sc10Spill.ShowCollection(false);

            // 5. contain, neutralize, absorb
            yield return fader.FadeTo(1f, 0.3f);
            PlaceRig(sc10SpillStand.position, sc10SpillStand.forward);
            yield return fader.FadeTo(0f, 0.4f);
            Stage("sc10Contain");
            StartCoroutine(SubtitledVoice(art.bnSc10ContainHint, Sc10Content.ContainHint, art.voSc10Contain));
            yield return sc10Spill.WaitIcon(art.iconSc10Contain, Sc10Content.ContainLabel);
            StopVoice();
            yield return sc10Spill.Contain();
            yield return PresentableFx.Wait(0.5f);

            Stage("sc10Neutralize");
            StartCoroutine(SubtitledVoice(art.bnSc10NeutralizeHint, Sc10Content.NeutralizeHint, art.voSc10Neutralize));
            yield return sc10Spill.WaitIcon(art.iconSc10Neutralize, Sc10Content.NeutralizeLabel);
            StopVoice();
            yield return sc10Spill.Neutralize();
            yield return PresentableFx.Wait(0.5f);

            Stage("sc10Absorb");
            StartCoroutine(SubtitledVoice(art.bnSc10AbsorbHint, Sc10Content.AbsorbHint, art.voSc10Absorb));
            yield return sc10Spill.WaitIcon(art.iconSc10Absorb, Sc10Content.AbsorbLabel);
            StopVoice();
            yield return sc10Spill.Absorb();
            yield return PresentableFx.Wait(0.5f);

            // 6. clean the glass, dispose
            Stage("sc10Clean");
            StartCoroutine(SubtitledVoice(art.bnSc10CleanHint, Sc10Content.CleanHint, art.voSc10Clean));
            yield return sc10Spill.WaitIcon(art.iconSc10Clean, Sc10Content.CleanLabel);
            StopVoice();
            yield return sc10Spill.CleanGlass();
            yield return PresentableFx.Wait(0.5f);

            Stage("sc10Dispose");
            StartCoroutine(SubtitledVoice(art.bnSc10DisposeHint, Sc10Content.DisposeHint, art.voSc10Dispose));
            yield return sc10Spill.WaitIcon(art.iconSc10Dispose, Sc10Content.DisposeLabel, Sc10Content.DisposeSub);
            StopVoice();
            yield return sc10Spill.Dispose();
            Stage("sc10Disposed");
            yield return PresentableFx.Wait(1.0f);
        }

        private static PresentableMcqPanel.Question BuildSc10Question()
        {
            var options = new PresentableMcqPanel.Option[Sc10Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc10Content.OptionLabels[i],
                    feedback = Sc10Content.OptionFeedback[i],
                    wrongTitle = Sc10Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc10Content.QuestionHeader,
                prompt = Sc10Content.QuestionPrompt,
                correctIndex = Sc10Content.CorrectIndex,
                correctTitle = Sc10Content.CorrectTitle,
                nextLabel = Sc10Content.FinishLabel,
                options = options,
                fancy = true
            };
        }

        private static PresentableMcqPanel.Question BuildSc09Question()
        {
            var options = new PresentableMcqPanel.Option[Sc09Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc09Content.OptionLabels[i],
                    feedback = Sc09Content.OptionFeedback[i],
                    wrongTitle = Sc09Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc09Content.QuestionHeader,
                prompt = Sc09Content.QuestionPrompt,
                correctIndex = Sc09Content.CorrectIndex,
                correctTitle = Sc09Content.CorrectTitle,
                nextLabel = Sc09Content.FinishLabel,
                options = options,
                fancy = true
            };
        }

        private static PresentableMcqPanel.Question BuildSc08Question()
        {
            var options = new PresentableMcqPanel.Option[Sc08Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc08Content.OptionLabels[i],
                    feedback = Sc08Content.OptionFeedback[i],
                    wrongTitle = Sc08Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc08Content.QuestionHeader,
                prompt = Sc08Content.QuestionPrompt,
                correctIndex = Sc08Content.CorrectIndex,
                correctTitle = Sc08Content.CorrectTitle,
                nextLabel = Sc08Content.FinishLabel,
                options = options,
                fancy = true
            };
        }

        private static PresentableMcqPanel.Question BuildSc07Question()
        {
            var options = new PresentableMcqPanel.Option[Sc07Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc07Content.OptionLabels[i],
                    feedback = Sc07Content.OptionFeedback[i],
                    wrongTitle = Sc07Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc07Content.QuestionHeader,
                prompt = Sc07Content.QuestionPrompt,
                correctIndex = Sc07Content.CorrectIndex,
                correctTitle = Sc07Content.CorrectTitle,
                wrongTitle = Sc07Content.WrongTitle,
                nextLabel = Sc07Content.FinishLabel,
                options = options,
                fancy = true
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
                fancy = true // the wow look is shared by Scenarios 4, 5 and 6
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
                options = options,
                fancy = true
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
                options = options,
                fancy = true
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
            var label = Sc02Content.OptionLabels;
            var feedback = Sc02Content.OptionFeedback;
            return new PresentableMcqPanel.Question
            {
                header = Sc02Content.QuestionHeader,
                prompt = Sc02Content.QuestionPrompt,
                correctIndex = Sc02Content.CorrectIndex,
                nextLabel = Sc02Content.FinishLabel,
                correctTitle = Sc02Content.CorrectTitle,
                options = new[]
                {
                    new PresentableMcqPanel.Option { label = label[0], feedback = feedback[0] },
                    new PresentableMcqPanel.Option { label = label[1], feedback = feedback[1] },
                    new PresentableMcqPanel.Option { label = label[2], feedback = feedback[2] },
                    new PresentableMcqPanel.Option { label = label[3], feedback = feedback[3] }
                }
            };
        }
    }
}
