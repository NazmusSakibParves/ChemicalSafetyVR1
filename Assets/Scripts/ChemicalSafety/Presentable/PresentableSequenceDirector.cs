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
        [SerializeField] private Transform sc02RayStand; // controller-ray flow: where the learner stands to see the 6 containers and the cabinet
        [SerializeField] private Sc02DecisionBox sc02Decision;
        [SerializeField] private LabelPickPopup sc02DestPopup;
        [SerializeField] private Transform sc01RayStand; // controller-ray flow: where the learner is placed to view the 4 drums
        [SerializeField] private PresentableMcqPanel mcqSc01;
        [SerializeField] private DrumCallout[] drums = new DrumCallout[4];

        [Header("Drum labels + drum carrier")]
        [SerializeField] private LabelPickPopup labelPopup;
        [SerializeField] private PokeTarget[] drumPokes = new PokeTarget[4];
        [SerializeField] private FloatingTouchButton[] drumButtons = new FloatingTouchButton[4];
        [Tooltip("About a metre from the rack face so the floating Touch buttons are within arm's reach.")]
        [SerializeField] private Transform drumPokeStand;
        [Tooltip("Scenario 1: the yellow flat-platform cart of the reference hall (drum arcs onto the platform, the cart is pulled away).")]
        [SerializeField] private bool flatCart;
        [Tooltip("Scenario 1: the glass INSTRUCTIONS card + CONTINUE button (client picture). Off = the previous pop-up frame. Set by the scene builder.")]
        [SerializeField] private bool sc01GlassCard;
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
        [Header("SC09 (controller ray, in the Scenario 6/8 factory room)")]
        [SerializeField] private Sc09AutoSplash sc09Auto;
        [SerializeField] private Sc09Shower sc09ShowerRay;
        [SerializeField] private Sc09Visor sc09VisorRay;
        [SerializeField] private Sc06Gate sc09GateRay;
        [SerializeField] private Transform sc09NearStand;
        [SerializeField] private Transform sc09ShowerStandRay;
        [SerializeField] private GameObject sc09ShowerMount;
        [SerializeField] private float sc09GlideSeconds = 4.0f;

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
        [Header("Scenario 10 (ray, in the Scenario 6/8/9 factory room)")]
        [SerializeField] private Sc10Spill sc10RaySpill;
        [SerializeField] private Sc10Accident sc10RayAccident;
        [SerializeField] private Sc06Gate sc10RayGate;
        [SerializeField] private GameObject sc10RayMount;
        [SerializeField] private Transform sc10RayKitStand;
        [SerializeField] private Transform sc10RaySpillStand;
        [SerializeField] private float sc10GlideSeconds = 4.0f;
        [Tooltip("Seconds a step icon (CONTAIN, NEUTRALIZE ...) is shown before it is 'pressed' by itself.")]
        [SerializeField] private float sc10IconSeconds = 1.8f;
        [SerializeField] private PresentableMcqPanel mcqSc10;
        [SerializeField] private GameObject envSc11;
        [SerializeField] private Transform spawnSc11;
        [SerializeField] private Transform sc11YardStand;
        [SerializeField] private Transform sc11GateStand;
        [SerializeField] private Transform sc11OutsideStand;
        [SerializeField] private Transform sc11ExitTarget;
        [SerializeField] private Sc11Yard sc11Yard;
        [SerializeField] private Sc09Guide sc11Guide;
        [SerializeField] private Sc06Gate sc11Gate;
        [SerializeField] private PresentableMcqPanel mcqSc11;
        [SerializeField, Tooltip("Set by the builder (Sc11UseYard): the scene holds the client's chlorine yard, played on the controller ray")]
        private bool sc11YardLook;
        [SerializeField] private GameObject envSc12;
        [SerializeField] private Transform spawnSc12;
        [SerializeField] private Transform sc12StartStand;
        [SerializeField] private FloorHotspot sc12Hotspot;
        [SerializeField] private Sc12Station sc12Station;
        [SerializeField] private Sc06Gate sc12Gate;
        [SerializeField] private Sc12Assessment assessmentSc12;
        [SerializeField] private GameObject envScCheck;
        [SerializeField] private Transform spawnScCheck;
        [SerializeField] private ScCheckRunner scCheck;

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
            // developer shortcut (EditorPref ChemSafetySc04Dev, set it from code / the console): starts at Scenario 4 and stops after it
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc04Dev", false) && envSc04 != null)
            {
                Loc.SetLanguage(UnityEditor.EditorPrefs.GetBool("ChemSafetySmokeBangla", false) ? Lang.Bn : Lang.En);
                // a different head height for the test (1.6 + offset): seated 1.1-1.3 m, standing 1.7 m
                if (rig != null)
                    rig.position += Vector3.up * UnityEditor.EditorPrefs.GetFloat("ChemSafetySc04HeadDy", 0f);
                PresentableInput.ActiveScenario = 4; // as the real flow does before Scenario 4: controller ray (or finger touch, by the input mode)
                yield return RunScenario4();
                yield break;
            }

            // developer shortcut (menu Chemical Safety/Presentable/Dev: start at Scenario 5): skips straight to the dyeing hall
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc05Only", false) && envSc05 != null)
            {
                Loc.SetLanguage(UnityEditor.EditorPrefs.GetBool("ChemSafetySmokeBangla", false) ? Lang.Bn : Lang.En);
                yield return RunScenario5();
                yield break;
            }

            // developer shortcut (menu Chemical Safety/Presentable/Dev: start at Scenario 6): skips straight to the lab
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc06Only", false) && envSc06 != null)
            {
                yield return RunScenario6();
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetyScCheckOnly", false) && envScCheck != null && scCheck != null)
            {
                Loc.SetLanguage(UnityEditor.EditorPrefs.GetBool("ChemSafetySmokeBangla", false) ? Lang.Bn : Lang.En);
                yield return RunScenarioCheck(13);
                Stage("complete");
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc12Only", false) && envSc12 != null)
            {
                Loc.SetLanguage(UnityEditor.EditorPrefs.GetBool("ChemSafetySmokeBangla", false) ? Lang.Bn : Lang.En);
                yield return RunScenario12();
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc11Only", false) && envSc11 != null)
            {
                Loc.SetLanguage(UnityEditor.EditorPrefs.GetBool("ChemSafetySmokeBangla", false) ? Lang.Bn : Lang.En);
                yield return RunScenario11();
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
                // the welcome greeting belongs to the login page: English first (the default language), 1 s gap, then Bangla
                _welcomeDone = false;
                _welcomeVoice = StartCoroutine(LoginWelcomeVoice());
                yield return loginScreen.Run(_cam, rig != null ? rig.position.y : 0f, fader, rig);
                // the greeting is for the login page only: if it is still running when the learner leaves, it stops here
                // and never carries into Scenario 1
                if (_welcomeVoice != null)
                    StopCoroutine(_welcomeVoice);
                _welcomeVoice = null;
                if (!_welcomeDone)
                    StopVoice();
                _welcomeDone = true;
                if (clipboard != null)
                    clipboard.SetSuspended(false);
            }

            // Scenario 1 may run on the controller ray (PresentableInput). The fingertip flow below stays as it was.
            PresentableInput.ActiveScenario = 1;
            instructionPanel.SetGlass(sc01GlassCard); // client: Scenario 1 instruction box + Continue in the glass style of the reference picture
            var rayFlow = PresentableInput.RayActive;
            if (rayFlow)
            {
                LockLocomotion(true);
                instructionPanel.SetCompact(true, rayCardScale);
                // subtitles sit in the clear space under the card, never inside its frame
                subtitle.Dock(instructionPanel.transform, () => -(instructionPanel.BottomExtentMeters + 0.16f), rayCardScale);
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
            // (the greeting belongs to the login page only; it was stopped when the login ended)
            Stage("gap");

            // 2. Instruction. Same eye-level card and centred Continue as Scenario 3.
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

            if (rayFlow)
            {
                yield return RunSc01RayFlow();
            }
            else
            {

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
            }

            Stage("sc02");
            instructionPanel.SetGlass(false); // the other scenarios keep their own frame
            if (rayFlow)
            {
                LockLocomotion(false);
                mcqSc01.SetWorldScale(1f);
                instructionPanel.SetCompact(false);
                subtitle.Dock(null, null, 1f);
            }

            PresentableInput.ActiveScenario = 2;

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

            var ray2 = PresentableInput.RayActive;
            if (ray2)
            {
                LockLocomotion(true);
                instructionPanel.SetCompact(true, rayCardScale);
                subtitle.Dock(instructionPanel.transform, () => -(instructionPanel.BottomExtentMeters + 0.16f), rayCardScale);
            }

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
            if (sc02CalloutFlow)
            {
                // client: no Take Decision, no clicking the containers one by one. Each chemical gets a callout and its own question.
                yield return RunSc02Callouts();
            }
            else
            {
                if (ray2)
                    yield return RunSc02RayFlow();
                else
                    yield return RunPlacement();
                yield return RunSc02ClassicQuestion(ray2);
            }

            if (ray2)
            {
                LockLocomotion(false);
                mcqSc02.SetWorldScale(1f);
                instructionPanel.SetCompact(false);
                subtitle.Dock(null, null, 1f);
            }

            PresentableInput.ActiveScenario = 3;

            if (envSc03 != null)
                yield return RunScenario3();

            Stage("complete");
            PresentableFx.Play(PresentableFx.Success);
            var doneLine = envScCheck != null ? Loc.T("SC18", "ModuleComplete")
                : envSc12 != null ? Sc12Content.ModuleComplete
                : envSc11 != null ? Sc11Content.ModuleComplete
                : envSc10 != null ? Sc10Content.ModuleComplete
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

            var ray3 = PresentableInput.RayActive;
            if (ray3)
            {
                LockLocomotion(true);
                instructionPanel.SetCompact(true, rayCardScale);
                subtitle.Dock(instructionPanel.transform, () => -(instructionPanel.BottomExtentMeters + 0.16f), rayCardScale);
            }

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

            if (ray3)
            {
                yield return RunSc03RayFlow();
            }
            else
            {
                yield return RunSc03ClassicInspectAndQuestion();
            }

            if (ray3)
            {
                LockLocomotion(false);
                mcqSc03.SetWorldScale(1f);
                instructionPanel.SetCompact(false);
                subtitle.Dock(null, null, 1f);
            }

            PresentableInput.ActiveScenario = 4; // Scenario 4 and later are finger touch again

            if (envSc04 != null)
                yield return RunScenario4();
        }

        // The Scenario 3 finger flow as it was (touch & hold scan, report with Continue, popup question + blast on a wrong answer).
        private IEnumerator RunSc03ClassicInspectAndQuestion()
        {
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
        }

        // ------------------------------------------------------------------ Scenario 3 on the controller ray

        [SerializeField] private Transform sc03RayStand;
        [Tooltip("Scenario 3 on the ray: false = a wrong answer no longer blows the drum up (client). The blast code stays for the finger flow.")]
        [SerializeField] private bool sc03BlastOnWrong = false;
        [SerializeField] private float sc03AutoScanDelay = 2.5f;
        [SerializeField] private float sc03AutoScanSeconds = 2.2f;
        [SerializeField] private float sc03ReportMinSeconds = 6f;

        private bool _sc03Protocol;
        private Sc03Protocol _sc03Prot;

        // Popups float at the learner's own eye height (a seated learner is not looking up, a standing one not down).
        private float Sc03ViewHeight()
        {
            var eye = _cam != null ? _cam.position.y : 1.6f;
            return Mathf.Clamp(eye - 0.05f, 1.05f, 1.7f);
        }

        // Where a popup goes: beside the drum (to the learner's right, towards the aisle), at eye height, facing the learner.
        private void Sc03PopupPose(float sideways, float towardsAisle, out Vector3 position, out Quaternion rotation)
        {
            var anchor = sc03Inspect.LabelAnchor;
            var label = anchor.position;
            var aisle = Flat(anchor.forward);
            var cam = _cam != null ? _cam.position : rig.position;
            var viewDir = Flat(new Vector3(label.x - cam.x, 0f, label.z - cam.z));
            var right = Vector3.Cross(Vector3.up, viewDir);
            position = new Vector3(label.x, Sc03ViewHeight(), label.z) + aisle * towardsAisle + right * sideways;
            rotation = Quaternion.LookRotation(Flat(position - cam), Vector3.up);
        }

        private IEnumerator RunSc03RayFlow()
        {
            Stage("sc03Inspect");
            var stand = sc03RayStand != null ? sc03RayStand : sc03InspectStand;
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.35f);
            PlaceRig(stand.position, Flat(stand.forward));
            clipboard.SetVisible(false);
            yield return fader.FadeTo(0f, 0.5f);

            // one stand for everything: the whole room and the drum in view, then it goes on by itself (no touch and hold)
            yield return PresentableFx.Wait(sc03AutoScanDelay);
            Stage("sc03Scan");
            yield return sc03Inspect.AutoScan(sc03AutoScanSeconds);
            yield return PresentableFx.Wait(0.4f);

            Stage("sc03Report");
            Sc03PopupPose(1.45f, 0.45f, out var cardPos, out var cardRot);
            sc03Inspect.SetCardPose(cardPos, cardRot, 3.2f);
            yield return sc03Inspect.ShowReport(false);
            var t0 = Time.time;
            yield return PlayVoice(art.voSc03Inspect);
            while (Time.time - t0 < sc03ReportMinSeconds)
                yield return null;
            yield return sc03Inspect.HideReport();
            sc03Inspect.ClearCardPose();

            Stage("sc03Question");
            // beside the drum, at eye height: the drum and the label stay in view
            Sc03PopupPose(1.6f, 0.6f, out var mcqPos, out var mcqRot);
            mcqSc03.SetWorldScale(2.0f);
            mcqSc03.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc03, BuildSc03Question(), false, 3);
        }

        // Right answer: the SDS line "Emergency action if degraded: DO NOT OPEN - Evacuate - Notify supervisor - Contact specialist
        // disposal" plays as an emergency response protocol around the drum. NEXT comes when it has finished.
        private IEnumerator Sc03ProtocolRoutine()
        {
            _sc03Protocol = true;
            if (_sc03Prot == null)
                _sc03Prot = Sc03Protocol.Create(art, envSc03 != null ? envSc03.transform : null);
            yield return PresentableFx.Wait(1.0f);
            StartCoroutine(PlayVoice(art.voCorrect));
            yield return PresentableFx.Wait(1.6f);
            var anchor = sc03Inspect.LabelAnchor;
            Sc03PopupPose(-1.5f, 0.6f, out var panelPos, out var panelRot);
            var drumCentre = sc03Detonation != null && sc03Detonation.Drum != null ? sc03Detonation.Drum.position : anchor.position - anchor.forward * 0.29f;
            StartCoroutine(PlayVoice(art.voSc03Protocol));
            yield return _sc03Prot.Play(drumCentre, anchor, panelPos, panelRot, 2.4f, spawnSc03 != null ? spawnSc03.position : rig.position);
            _sc03Protocol = false;
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

            // controller ray (as Scenarios 1-3): head free, card world-fixed, no arrows, glove properties run by themselves
            var ray4 = PresentableInput.RayActive;
            if (ray4)
            {
                LockLocomotion(true);
                instructionPanel.SetCompact(true, sc06CardScale);
                instructionPanel.SetGlass(sc04GlassCard);
                subtitle.Dock(instructionPanel.transform, () => -(instructionPanel.BottomExtentMeters + 0.16f), sc06CardScale);
            }

            Stage("sc04Intro");
            _instructionClip = art.voSc04Instruction != null ? art.voSc04Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc04Content.InstructionEnglish, art.bnSc04InstructionPanel);
            instructionPanel.SetContinueCentered(true);
            BeginEyeInstruction();
            if (ray4)
            {
                Sc05AsideInstruction(sc06CardRight, sc06CardUp); // small, to the left and up: the table and the bottle stay free
                Sc04KeepAboveTable(instructionPanel.transform, instructionPanel.BottomExtentMeters, 0.30f);
            }

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
            if (ray4)
                instructionPanel.SetGlass(false);

            if (ray4 && gloves != null)
            {
                yield return RunSc04RayFlow();
            }
            else
            {
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
            }

            if (ray4)
            {
                LockLocomotion(false);
                mcqSc04.SetWorldScale(1f);
                instructionPanel.SetCompact(false);
                instructionPanel.SetGlass(false);
                subtitle.Dock(null, null, 1f);
            }

#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc04Dev", false))
                yield break;
#endif
            if (envSc05 != null)
                yield return RunScenario5();
        }

        // ------------------------------------------------------------------ Scenario 4 on the controller ray

        [Header("Scenario 4 (ray)")]
        [Tooltip("Glass instruction card like Scenario 1.")]
        [SerializeField] private bool sc04GlassCard = true;
        [SerializeField] private float sc04FirstGloveDelay = 3.5f;
        [SerializeField] private float sc04GloveMinSeconds = 5f;
        [SerializeField] private float sc04BetweenGloves = 0.6f;
        [SerializeField] private float sc04PopupScale = 1.85f;

        // Popups beside the table, high and to the right of the learner's view: gloves and bottle stay clear.
        private void Sc04PopupPose(float sideways, float rise, float distance, out Vector3 position, out Quaternion rotation)
        {
            var cam = _cam != null ? _cam.position : rig.position;
            var fwd = _cam != null ? Flat(_cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            position = new Vector3(cam.x, Sc03ViewHeight() + rise, cam.z) + fwd * distance + right * sideways;
            rotation = Quaternion.LookRotation(Flat(position - cam), Vector3.up);
        }

        private float Sc04TableTop() => gloveBench != null ? gloveBench.TopY : 0.9f;

        // A card is lifted when its lowest point (the Continue button hangs below the centre by bottomExtent) would come closer than
        // minClearance to the table top: the table must never cut through a card, whatever the learner's eye height.
        private void Sc04KeepAboveTable(Transform card, float bottomExtent, float minClearance)
        {
            if (card == null)
                return;
            var need = Sc04TableTop() + minClearance + bottomExtent;
            if (card.position.y < need)
                card.position += Vector3.up * (need - card.position.y);
        }

        private IEnumerator RunSc04RayFlow()
        {
            var again = true;
            var first = true;
            while (again)
            {
                Stage("sc04Explore");
                if (gloveBench != null && _cam != null)
                    gloveBench.SnapTo(_cam.position.y);
                if (first && _cam != null)
                {
                    // the learner already starts at the glove stand (fixed bench, no forced motion); glide only if a long way off
                    var stand = gloves.StandInFront();
                    var off = new Vector2(_cam.position.x - stand.x, _cam.position.z - stand.z).magnitude;
                    if (off > 0.6f)
                        yield return Glide(stand, 0.7f);
                }

                first = false;
                gloves.BeginAuto();
                yield return PresentableFx.Wait(sc04FirstGloveDelay);
                for (var i = 0; i < gloves.Count; i++)
                {
                    var index = i;
                    yield return gloves.AutoInspect(index, () => Sc04GloveShown(index));
                    yield return PresentableFx.Wait(sc04BetweenGloves);
                }

                Stage("sc04Gate");
                yield return PresentableFx.Wait(0.4f);
                Sc04PopupPose(0.78f, 0.15f, 1.6f, out var gatePos, out var gateRot);
                gatePos.y = Mathf.Max(gatePos.y, Sc04TableTop() + 0.55f);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc04Gate.Back += OnBack;
                sc04Gate.Next += OnNext;
                sc04Gate.Show(gatePos, gateRot, sc04PopupScale);
                StartCoroutine(PlayVoice(art.voSc04Gate));
                while (choice == 0)
                    yield return null;
                sc04Gate.Back -= OnBack;
                sc04Gate.Next -= OnNext;
                StopVoice();
                sc04Gate.Hide();
                again = choice == 1;
            }

            Stage("sc04Question");
            Sc04PopupPose(0.78f, 0.32f, 1.6f, out var mcqPos, out var mcqRot);
            mcqPos.y = Mathf.Max(mcqPos.y, Sc04TableTop() + 0.95f);
            mcqSc04.SetWorldScale(sc04PopupScale);
            mcqSc04.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc04, BuildSc04Question(), false, 4);
        }

        // Voice-over of one glove (its minimum read time runs at the same time).
        private IEnumerator Sc04GloveShown(int index)
        {
            var clip = art.voSc04Glove != null && index < art.voSc04Glove.Length ? art.voSc04Glove[index] : null;
            var t0 = Time.time;
            yield return PlayVoice(clip);
            while (Time.time - t0 < sc04GloveMinSeconds)
                yield return null;
        }

        // SC05 starts only after the SC04 question's NEXT. Dyeing factory: pick the right respirator for toluene.
        private IEnumerator RunScenario5()
        {
            PresentableInput.ActiveScenario = 5; // the controller ray (RayScenarios) or finger touch, by the input mode
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

            var ray5 = PresentableInput.RayActive;
            Stage("sc05Intro");
            _instructionClip = art.voSc05Instruction != null ? art.voSc05Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc05Content.InstructionEnglish, art.bnSc05InstructionPanel);
            if (ray5)
            {
                // controller ray: head free, a SMALL glass card at the left / up of the view (clear of the rail and the masks),
                // the voice reads it, then it goes by itself: no Continue click
                LockLocomotion(true);
                instructionPanel.SetCompact(true, sc05CardScale);
                instructionPanel.SetGlass(sc04GlassCard);
                BeginEyeInstruction();
                Sc05AsideInstruction(sc05CardLeft, sc05CardUp);
                yield return instructionPanel.Show();
                instructionPanel.SetSpeaking(true);
                yield return PlayVoice(_instructionClip);
                instructionPanel.SetSpeaking(false);
                yield return RayInstructionContinue(); // Continue appears after the voice; the game play starts with the click
                yield return instructionPanel.Hide();
                EndEyeInstruction();
                instructionPanel.SetGlass(false);
                instructionPanel.SetCompact(false);
                yield return RunSc05RayFlow();
                LockLocomotion(false);
                mcqSc05.SetWorldScale(1f);
                if (sc05Ambience != null)
                    sc05Ambience.End();
#if UNITY_EDITOR
                if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc05Only", false))
                    yield break;
#endif
                if (envSc06 != null)
                    yield return RunScenario6();
                yield break;
            }

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

#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc05Only", false))
                yield break;
#endif
            // SC06 (chemistry lab) starts after the SC05 question's NEXT
            if (envSc06 != null)
                yield return RunScenario6();
        }

        // ------------------------------------------------------------------ Scenario 5 on the controller ray

        [Header("Scenario 5 (ray)")]
        [SerializeField] private float sc05CardScale = 0.9f;
        [SerializeField] private float sc05CardLeft = 0.85f;
        [SerializeField] private float sc05CardUp = 0.28f;
        [SerializeField] private float sc05FirstMaskDelay = 3.5f;
        [SerializeField] private float sc05MaskMinSeconds = 5f;
        [SerializeField] private float sc05BetweenMasks = 0.6f;
        [SerializeField] private float sc05PopupScale = 1.85f;

        // The small instruction card: eye level in front, then to the left and up, turned to the learner.
        private void Sc05AsideInstruction(float left, float up)
        {
            if (_cam == null)
                return;
            var t = instructionPanel.transform;
            var right = Vector3.Cross(Vector3.up, Flat(_cam.forward));
            t.position += -right * left + Vector3.up * up;
            t.rotation = Quaternion.LookRotation(Flat(t.position - new Vector3(_cam.position.x, t.position.y, _cam.position.z)), Vector3.up);
        }

        private IEnumerator RunSc05RayFlow()
        {
            var again = true;
            while (again)
            {
                Stage("sc05Explore");
                masks.BeginAuto();
                yield return PresentableFx.Wait(sc05FirstMaskDelay);
                foreach (var i in masks.LeftToRight())
                {
                    var index = i;
                    yield return masks.AutoInspect(index, () => Sc05MaskShown(index));
                    yield return PresentableFx.Wait(sc05BetweenMasks);
                }

                Stage("sc05Gate");
                yield return PresentableFx.Wait(0.4f);
                // the rail is on the right of the view: cards go to the LEFT, above the table-free part of the scene
                Sc04PopupPose(-0.8f, 0.15f, 1.6f, out var gatePos, out var gateRot);
                masks.ShowGateAt(gatePos, gateRot, sc05PopupScale);
                StartCoroutine(PlayVoice(art.voSc05Gate));
                while (!masks.NextChosen && !masks.BackChosen)
                    yield return null;
                StopVoice();
                masks.HideGatePublic();
                again = masks.BackChosen;
            }

            Stage("sc05Question");
            Sc04PopupPose(-0.8f, 0.32f, 1.6f, out var mcqPos, out var mcqRot);
            mcqSc05.SetWorldScale(sc05PopupScale);
            mcqSc05.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc05, BuildSc05Question(), false, 5);
        }

        // Voice-over of one mask (the minimum read time runs at the same time).
        private IEnumerator Sc05MaskShown(int index)
        {
            var clip = art.voSc05Mask != null && index < art.voSc05Mask.Length ? art.voSc05Mask[index] : null;
            var t0 = Time.time;
            yield return PlayVoice(clip);
            while (Time.time - t0 < sc05MaskMinSeconds)
                yield return null;
        }

        // ------------------------------------------------------------------ Scenario 6 on the controller ray

        [Header("Scenario 6 (ray)")]
        [SerializeField] private Sc06AutoPour sc06Auto;
        [SerializeField] private Sc06ExhaustFan sc06Fan;
        [SerializeField] private float sc06CardScale = 0.9f;
        [SerializeField] private float sc06CardRight = 0.85f;
        [SerializeField] private float sc06CardUp = 0.28f;
        [SerializeField] private float sc06PopupScale = 1.85f;
        [Header("Scenarios 6, 8, 9: the card sits over the bright hierarchy poster, so it is bigger, lower and has the clear look")]
        [SerializeField] private float sc689CardScale = 1.0f;
        [SerializeField] private float sc689CardRight = 0.9f;
        [SerializeField] private float sc689CardUp = -0.18f;
        [SerializeField] private float sc06AutoDelay = 3f;
        [Tooltip("On = after the containers stand in the hood the learner is carried by a slow glide in front of the fume hood for the pour, and back to the table afterwards (as in Scenario 8). Off = stays at the start spot.")]
        [SerializeField] private bool sc06MoveToHood = true;
        [SerializeField] private float sc06GlideSeconds = 4.5f;

        // Automatic demonstration 3 s after the instruction: hood on (green lamp), exhaust fan on (fan spins), the sash opens, the three
        // containers go into the hood one after another and the jar is filled. "PREVIEW" on the gate card plays it all again.
        private IEnumerator RunSc06RayFlow()
        {
            var again = true;
            while (again)
            {
                Stage("sc06Auto");
                lab.ResetLab();
                lab.SetVaporOn(false);
                sc06Auto.ResetAll();
                if (sc06Fan != null)
                    sc06Fan.ResetFan();
                yield return PresentableFx.Wait(sc06AutoDelay);

                Stage("sc06Hood");
                lab.Toggle(0);
                yield return PlayVoice(art.voSc06AutoHood);
                yield return PresentableFx.Wait(0.5f);
                Stage("sc06Fan");
                lab.Toggle(1);
                yield return PlayVoice(art.voSc06AutoFan);
                yield return PresentableFx.Wait(0.8f);
                lab.OpenSash();
                yield return PresentableFx.Wait(0.2f);
                while (lab.SashMoving || !lab.SashOpen)
                    yield return null;

                Stage("sc06Move");
                StartCoroutine(PlayVoice(art.voSc06AutoMove));
                sc06Auto.StartPlay(sc06MoveToHood);
                if (sc06MoveToHood)
                {
                    // the three containers go into the hood from here; then the learner glides in front of the hood for the pour
                    while (!sc06Auto.Carried)
                        yield return null;
                    Stage("sc06GlideIn");
                    yield return Glide(sc06HoodStand.position, sc06GlideSeconds);
                    yield return PresentableFx.Wait(0.4f);
                    sc06Auto.ContinuePour();
                }

                var pourVoice = false;
                while (!sc06Auto.Finished)
                {
                    if (!pourVoice && sc06Auto.Step >= 4)
                    {
                        pourVoice = true;
                        Stage("sc06Pour");
                        StartCoroutine(PlayVoice(art.voSc06AutoPour));
                    }

                    yield return null;
                }

                yield return PresentableFx.Wait(1.2f);
                StopVoice();
                subtitle.Hide();
                lab.CloseSash();
                yield return PresentableFx.Wait(0.8f);
                if (sc06MoveToHood)
                {
                    // the hood is closed: back in front of the table (the start spot) where the card opens
                    Stage("sc06GlideOut");
                    yield return Glide(sc06TableStand.position, sc06GlideSeconds);
                    yield return PresentableFx.Wait(0.3f);
                }

                Stage("sc06Gate");
                Sc04PopupPose(-0.95f, 0.12f, 1.6f, out var gatePos, out var gateRot);
                var choice = 0; // 1 = PREVIEW, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc06Gate.Back += OnBack;
                sc06Gate.Next += OnNext;
                sc06Gate.Show(gatePos, gateRot, sc06PopupScale);
                StartCoroutine(PlayVoice(art.voSc06Gate));
                while (choice == 0)
                    yield return null;
                sc06Gate.Back -= OnBack;
                sc06Gate.Next -= OnNext;
                sc06Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                again = choice == 1;
            }

            Stage("sc06Question");
            Sc04PopupPose(-0.95f, 0.25f, 1.6f, out var mcqPos, out var mcqRot);
            mcqSc06.SetWorldScale(sc06PopupScale);
            mcqSc06.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc06, BuildSc06Question(), false, 6);
        }

        // SC06 starts after the SC05 question's NEXT. Chemistry lab: switch the fume hood on, lift the solvent, open the sash, pour.
        private IEnumerator RunScenario6()
        {
            PresentableInput.ActiveScenario = 6; // finger touch again (the ray ends with Scenario 5)
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
            if (sc06Auto != null)
            {
                lab.SetVaporOn(false); // the old solvent jug is not in the factory room
                sc06Auto.ResetAll();
                if (sc06Fan != null)
                    sc06Fan.ResetFan();
            }

            PlaceRig(spawnSc06.position, spawnSc06.forward);
            sc06Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc06Intro");
            _instructionClip = art.voSc06Instruction != null ? art.voSc06Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc06Content.InstructionEnglish, art.bnSc06InstructionPanel);
            if (PresentableInput.RayActive && sc06Auto != null)
            {
                // controller ray: head free, a SMALL glass card on the RIGHT, the voice reads it, then it goes by itself (no Continue click)
                LockLocomotion(true);
                instructionPanel.SetCompact(true, sc689CardScale);
                instructionPanel.SetGlass(sc04GlassCard);
                instructionPanel.SetClearLook(true); // over the bright poster: nearly solid backing, words one size larger
                BeginEyeInstruction();
                Sc05AsideInstruction(sc689CardRight, sc689CardUp); // to the LEFT and lower: the hood stays free, clear of the poster
                yield return instructionPanel.Show();
                instructionPanel.SetSpeaking(true);
                yield return PlayVoice(_instructionClip);
                instructionPanel.SetSpeaking(false);
                yield return RayInstructionContinue(); // Continue appears after the voice; the game play starts with the click
                yield return instructionPanel.Hide();
                EndEyeInstruction();
                instructionPanel.SetGlass(false);
                instructionPanel.SetCompact(false);
                yield return RunSc06RayFlow();
                LockLocomotion(false);
                mcqSc06.SetWorldScale(1f);
                Stage("sc06Done");
                if (sc06Ambience != null)
                    sc06Ambience.End();
#if UNITY_EDITOR
                if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc06Only", false))
                    yield break;
#endif
                if (envSc07 != null)
                    yield return RunScenario7();
                yield break;
            }

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

        [Header("SC07 (ray)")]
        [SerializeField] private float sc07PopupScale = 1.75f;
        [SerializeField] private float sc07GateScale = 1.7f;
        [SerializeField] private float sc07SdsSeconds = 2f;
        [SerializeField] private float sc07ExtMinSeconds = 3.2f;

        // Controller-ray flow of SC07: SDS 2 s then hidden, the three extinguishers one by one with their voice (no clicks), the
        // "inspection complete" card (BACK = SDS and all three again, NEXT = the MCQ).
        private IEnumerator Sc07RayInspect()
        {
            var again = true;
            while (again)
            {
                Stage("sc07Sds");
                clipboard.SetVisible(true);
                yield return PresentableFx.Wait(sc07SdsSeconds);
                clipboard.SetVisible(false);
                yield return PresentableFx.Wait(0.6f);

                Stage("sc07Explore");
                extinguishers.BeginAuto();
                foreach (var i in extinguishers.LeftToRight())
                {
                    yield return extinguishers.AutoHold(i);
                    var t0 = Time.time;
                    extinguishers.SetSpeaking(true);
                    yield return PlayVoice(art.voSc07Ext != null && i < art.voSc07Ext.Length ? art.voSc07Ext[i] : null);
                    extinguishers.SetSpeaking(false);
                    while (Time.time - t0 < sc07ExtMinSeconds)
                        yield return null;
                    yield return extinguishers.AutoRelease();
                    yield return PresentableFx.Wait(0.35f);
                }

                extinguishers.Stop();
                Stage("sc07Gate");
                yield return PresentableFx.Wait(0.35f);
                // the plain wall above the extinguishers (the client's arrow): the card floats there, the extinguishers stay free
                Sc04PopupPose(0f, 0.40f, 1.6f, out var gatePos, out var gateRot);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc07Gate.Back += OnBack;
                sc07Gate.Next += OnNext;
                sc07Gate.Show(gatePos, gateRot, sc07GateScale);
                StartCoroutine(PlayVoice(art.voSc07Gate));
                while (choice == 0)
                    yield return null;
                sc07Gate.Back -= OnBack;
                sc07Gate.Next -= OnNext;
                sc07Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                again = choice == 1;
            }
        }

        // SC07: factory fire. Instruction card, the fire starts and the workers flee, SDS, inspect the three extinguishers,
        // "inspection complete" card, the MCQ; the right answer makes the hero put the fire out.
        private IEnumerator RunScenario7()
        {
            Stage("sc07");
            PresentableInput.ActiveScenario = 7; // the controller ray (RayScenarios) or finger touch, by the input mode
            var ray7 = PresentableInput.RayActive && sc07Crowd != null && extinguishers != null;
            if (ray7)
                LockLocomotion(true); // no walking / turning / teleport from the very start of the scenario: the head is free, the body stays
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
            if (ray7)
            {
                // controller ray: head free, a SMALL glass card to one side (the hall stays visible)
                instructionPanel.SetCompact(true, sc06CardScale);
                instructionPanel.SetGlass(sc04GlassCard);
            }

            BeginEyeInstruction();
            if (ray7)
                Sc05AsideInstruction(sc06CardRight, sc06CardUp);
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
            if (ray7)
            {
                instructionPanel.SetGlass(false);
                instructionPanel.SetCompact(false);
            }

            // the fire starts, the alarm rings, the workers see it and run; the hero stays
            Stage("sc07Event");
            if (sc07Crowd != null)
                yield return sc07Crowd.PlayEvent();
            else
                yield return PresentableFx.Wait(3f);

            if (ray7)
            {
                // no clicks: the player is walked to the extinguisher pillar, the SDS shows for 2 s and hides, the extinguishers come one by one
                Stage("sc07Walk");
                if (sc07PillarStand != null)
                    yield return Glide(sc07PillarStand.position, 1.8f);
                yield return Sc07RayInspect();
            }
            else
            {
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
            if (ray7)
            {
                // centred in the aisle: the burning pillar stays left, the extinguishers right, nothing important is covered
                Sc04PopupPose(0f, 0.05f, 2.0f, out var mcq7Pos, out var mcq7Rot);
                mcqSc07.SetWorldScale(sc07PopupScale);
                mcqSc07.transform.SetPositionAndRotation(mcq7Pos, mcq7Rot);
            }
            else
            {
                PlaceAtEyeLevel(mcqSc07.transform, 0.7f, false);
            }

            var q7 = BuildSc07Question();
            _sc07WinDone = false;
            q7.holdCorrect = () => !_sc07WinDone; // the right-answer card opens only after the fire is out
            q7.holdNext = () => !_sc07WinDone;   // ray flow (inline reveal): NEXT shows only after the fire is out
            yield return RunQuestion(mcqSc07, q7, false, 7);
            if (ray7)
            {
                LockLocomotion(false);
                mcqSc07.SetWorldScale(1f);
            }

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

        // ------------------------------------------------------------------ Scenario 8 on the controller ray (in the Scenario 6 factory room)

        [Header("Scenario 8 (ray)")]
        [SerializeField] private Sc08AutoDilute sc08Dilute;
        [SerializeField] private Sc08Ppe sc08PpeRay;
        [SerializeField] private Sc06Gate sc08GateRay;
        [SerializeField] private float sc08AutoDelay = 2f;
        [Tooltip("On = the learner is carried by a slow smooth glide (no black screen) in front of the fume hood for the pour, and back afterwards. Off = stays at the start spot.")]
        [SerializeField] private bool sc08MoveToHood = true;
        [SerializeField] private float sc08GlideSeconds = 4.5f;

        // The instruction card sits where Scenario 6's does (small, aside, the room stays free). 2 s after it the PPE station on the left
        // wall hands out the four items one by one (check marks), then the hood and the fan go on (as in Scenario 6), the containers go
        // into the hood, the water goes into the empty jar first, the acid follows slowly while the glass rod stirs. "PREVIEW" replays it.
        private IEnumerator RunSc08RayFlow()
        {
            var again = true;
            while (again)
            {
                Stage("sc08Auto");
                lab.ResetLab();
                lab.SetVaporOn(false);
                sc06Auto.ResetAll();
                sc08Dilute.ResetAll();
                sc08Dilute.SetRodVisible(true);
                if (sc06Fan != null)
                    sc06Fan.ResetFan();
                sc08PpeRay.ResetAll();
                yield return PresentableFx.Wait(sc08AutoDelay);

                Stage("sc08Ppe");
                var ppeVoice = StartCoroutine(PlayVoice(art.voSc08AutoPpe));
                yield return sc08PpeRay.AutoRun(0.8f);
                yield return ppeVoice;
                yield return PresentableFx.Wait(0.6f);
                sc08PpeRay.End();
                yield return PresentableFx.Wait(0.5f);

                Stage("sc08Hood");
                lab.Toggle(0);
                yield return PlayVoice(art.voSc06AutoHood);
                yield return PresentableFx.Wait(0.5f);
                Stage("sc08Fan");
                lab.Toggle(1);
                yield return PlayVoice(art.voSc06AutoFan);
                yield return PresentableFx.Wait(0.8f);
                lab.OpenSash();
                yield return PresentableFx.Wait(0.2f);
                while (lab.SashMoving || !lab.SashOpen)
                    yield return null;

                // guided move (a slow glide, no black screen, no walking): after the carrying the learner is taken in front of the fume hood
                Stage("sc08Move");
                StartCoroutine(PlayVoice(art.voSc06AutoMove));
                sc08Dilute.StartPlay(sc08MoveToHood);
                if (sc08MoveToHood)
                {
                    // the three containers go into the hood from here; then the learner glides in front of the hood for the pour
                    while (!sc08Dilute.Carried)
                        yield return null;
                    Stage("sc08GlideIn");
                    yield return Glide(sc06HoodStand.position, sc08GlideSeconds);
                    yield return PresentableFx.Wait(0.4f);
                    sc08Dilute.ContinuePour();
                }

                var waterVoice = false;
                var acidVoice = false;
                while (!sc08Dilute.Finished)
                {
                    if (!waterVoice && sc08Dilute.Step >= 4)
                    {
                        waterVoice = true;
                        Stage("sc08Water");
                        StartCoroutine(PlayVoice(art.voSc08AutoWater));
                    }

                    if (!acidVoice && sc08Dilute.Step >= 6)
                    {
                        acidVoice = true;
                        Stage("sc08Acid");
                        StartCoroutine(PlayVoice(art.voSc08AutoAcid));
                    }

                    yield return null;
                }

                yield return PresentableFx.Wait(1.2f);
                StopVoice();
                subtitle.Hide();
                lab.CloseSash();
                yield return PresentableFx.Wait(0.8f);
                // the hood is closed: back in front of the table (the start spot) where the cards open
                if (sc08MoveToHood)
                {
                    Stage("sc08GlideOut");
                    yield return Glide(sc06TableStand.position, sc08GlideSeconds);
                    yield return PresentableFx.Wait(0.3f);
                }

                Stage("sc08Gate");
                Sc04PopupPose(-0.95f, 0.12f, 1.6f, out var gatePos, out var gateRot);
                var choice = 0; // 1 = PREVIEW, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc08GateRay.Back += OnBack;
                sc08GateRay.Next += OnNext;
                sc08GateRay.Show(gatePos, gateRot, sc06PopupScale);
                StartCoroutine(PlayVoice(art.voSc08GatePrev));
                while (choice == 0)
                    yield return null;
                sc08GateRay.Back -= OnBack;
                sc08GateRay.Next -= OnNext;
                sc08GateRay.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                again = choice == 1;
            }

            Stage("sc08Question");
            Sc04PopupPose(-0.95f, 0.25f, 1.6f, out var mcqPos, out var mcqRot);
            mcqSc08.SetWorldScale(sc06PopupScale);
            mcqSc08.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc08, BuildSc08Question(), false, 8);
        }

        private IEnumerator RunScenario8Ray()
        {
            Stage("sc08");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc07, envSc08 })
                if (e != null)
                    e.SetActive(false);
            if (sc07Ambience != null)
                sc07Ambience.End();
            envSc06.SetActive(true); // the same factory room as Scenario 6
            clipboard.SetSheet(art.sdsSc08);
            clipboard.SetVisible(false);
            lab.ResetLab();
            lab.SetVaporOn(false);
            sc06Auto.ResetAll();
            sc08Dilute.ResetAll();
            sc08Dilute.SetRodVisible(true);
            sc08PpeRay.SetStationVisible(true);
            sc08PpeRay.ResetAll();
            if (sc06Fan != null)
                sc06Fan.ResetFan();
            PlaceRig(spawnSc06.position, spawnSc06.forward);
            sc06Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc08Intro");
            _instructionClip = art.voSc08Instruction != null ? art.voSc08Instruction : art.voInstruction;
            instructionPanel.SetContent(Sc08Content.InstructionEnglish, art.bnSc08InstructionPanel);
            LockLocomotion(true); // the head is free, the body stays: no walking about in VR
            instructionPanel.SetCompact(true, sc689CardScale);
            instructionPanel.SetGlass(sc04GlassCard);
            instructionPanel.SetClearLook(true);
            BeginEyeInstruction();
            Sc05AsideInstruction(sc689CardRight, sc689CardUp); // same place as Scenario 6: the hood and the table stay free
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);
            yield return RayInstructionContinue(); // Continue appears after the voice; the game play starts with the click
            yield return instructionPanel.Hide();
            EndEyeInstruction();
            instructionPanel.SetGlass(false);
            instructionPanel.SetCompact(false);

            yield return RunSc08RayFlow();

            LockLocomotion(false);
            mcqSc08.SetWorldScale(1f);
            // leave the shared room as Scenario 6 expects it
            sc08Dilute.ResetAll();
            sc08Dilute.SetRodVisible(false);
            sc08PpeRay.ResetAll();
            sc08PpeRay.SetStationVisible(false);
            lab.ResetLab();
            if (sc06Fan != null)
                sc06Fan.ResetFan();
            if (sc06Ambience != null)
                sc06Ambience.End();
            Stage("sc08Done");
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc08Only", false))
                yield break;
#endif
            if (envSc09 != null)
                yield return RunScenario9();
        }

        // SC08: acid dilution. Instruction card, PPE donning station (four items), fume hood on, carry the tray to the hood,
        // open the sash, add the acid to the water slowly while stirring, "practice complete" card, the MCQ.
        private IEnumerator RunScenario8()
        {
            if (PresentableInput.AllowedIn(8) && sc08Dilute != null && sc08PpeRay != null && sc08GateRay != null)
            {
                PresentableInput.ActiveScenario = 8; // controller ray, in the Scenario 6 factory room
                yield return RunScenario8Ray();
                yield break;
            }

            PresentableInput.ActiveScenario = 0; // the old lab (Sc08UseFactory = false) is finger touch
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

        // ------------------------------------------------------------------------------------------------------------------------
        // SC09 on the controller ray: the factory room of Scenario 6 and 8. Compact instruction card, the learner is taken in front of the
        // table, the acid can slips while pouring into the empty jar (the jar breaks, acid on the face), a voiced card, the SDS (opens, hides
        // 2 s after its voice), the learner is turned round to the RIGHT to the emergency shower behind (pushed automatically, water, drain),
        // back to the table (can, broken jar and acid on the table and the floor), the gate card (PREVIEW replays), the question.
        // ------------------------------------------------------------------------------------------------------------------------
        private IEnumerator RunScenario9Ray()
        {
            Stage("sc09");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc07, envSc08, envSc09 })
                if (e != null)
                    e.SetActive(false);
            if (sc07Ambience != null)
                sc07Ambience.End();
            envSc06.SetActive(true); // the same factory room as Scenario 6 and 8
            clipboard.SetSheet(art.sdsSc09);
            clipboard.SetVisible(false);
            lab.ResetLab();
            lab.SetVaporOn(false);
            sc06Auto.ResetAll();
            if (sc08Dilute != null)
            {
                sc08Dilute.ResetAll();
                sc08Dilute.SetRodVisible(false);
            }

            if (sc08PpeRay != null)
                sc08PpeRay.SetStationVisible(false);
            sc09Auto.ResetAll();
            sc09VisorRay.Hide();
            if (sc09ShowerMount != null)
                sc09ShowerMount.SetActive(true); // the shower only exists in Scenario 9
            sc09ShowerRay.ResetAll();
            if (sc06Fan != null)
                sc06Fan.ResetFan();
            PlaceRig(spawnSc06.position, spawnSc06.forward);
            sc06Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc09Intro");
            _instructionClip = art.voSc09RayInstruction != null ? art.voSc09RayInstruction : art.voInstruction;
            instructionPanel.SetContent(Sc09Content.RayInstruction, art.bnSc09RayInstructionPanel);
            LockLocomotion(true); // the head is free, the body stays: no walking about in VR
            instructionPanel.SetCompact(true, sc689CardScale);
            instructionPanel.SetGlass(sc04GlassCard);
            instructionPanel.SetClearLook(true);
            BeginEyeInstruction();
            Sc05AsideInstruction(sc689CardRight, sc689CardUp); // to the left and lower: the table stays free
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);
            yield return RayInstructionContinue(); // Continue appears after the voice; the game play starts with the click
            yield return instructionPanel.Hide();
            EndEyeInstruction();
            instructionPanel.SetGlass(false);
            instructionPanel.SetCompact(false);

            yield return RunSc09RayFlow();

            LockLocomotion(false);
            mcqSc09.SetWorldScale(1f);
            // leave the shared room as Scenario 6 and 8 expect it
            sc09Auto.ResetAll();
            sc09ShowerRay.StopWater();
            sc09ShowerRay.ResetAll();
            sc09VisorRay.Hide();
            if (sc09ShowerMount != null)
                sc09ShowerMount.SetActive(false);
            clipboard.SetVisible(false);
            lab.ResetLab();
            if (sc06Fan != null)
                sc06Fan.ResetFan();
            if (sc06Ambience != null)
                sc06Ambience.End();
            Stage("sc09Done");
            if (envSc10 != null)
                yield return RunScenario10();
        }

        // The ray intros of Scenarios 5, 6, 8 and 9: after the voice a Continue button appears (as in Scenarios 1-4 and 7) and the game play
        // starts only when the learner clicks it (client: the card must not hide by itself). The speaker replays the instruction.
        private IEnumerator RayInstructionContinue()
        {
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
        }

        private IEnumerator RunSc09RayFlow()
        {
            // guided move (a slow glide, no black screen): the learner is taken in front of the table
            Stage("sc09Move");
            yield return Glide(sc09NearStand.position, sc09GlideSeconds);
            yield return PresentableFx.Wait(0.5f);

            var again = true;
            while (again)
            {
                Stage("sc09Auto");
                sc09Auto.ResetAll();
                sc09ShowerRay.ResetAll();
                sc09VisorRay.Hide();
                clipboard.SetVisible(false);
                yield return PresentableFx.Wait(1.0f);

                Stage("sc09Pour");
                sc09Auto.StartPlay();
                while (!sc09Auto.Splashed)
                    yield return null;

                // the voiced "INSTRUCTIONS" card hides by itself when its voice ends
                Stage("sc09Spill");
                yield return PresentableFx.Wait(0.9f);
                yield return Sc09RayCard(Sc09Content.RaySpill, art.bnSc09RaySpill, art.voSc09RaySpill);

                // the safety data sheet opens with its voice and hides 2 s after it
                Stage("sc09Sds");
                clipboard.SetVisible(true);
                yield return PlayVoice(art.voSc09Sds);
                yield return PresentableFx.Wait(2f);
                clipboard.SetVisible(false);
                while (!sc09Auto.Finished)
                    yield return null;

                // turned round to the right, to the emergency shower behind the table
                Stage("sc09Walk");
                var walkVoice = StartCoroutine(PlayVoice(art.voSc09RayWalk));
                yield return GlideTurn(sc09ShowerStandRay.position, sc09ShowerStandRay.forward, sc09GlideSeconds, 1f);
                yield return walkVoice;
                yield return PresentableFx.Wait(0.4f);

                Stage("sc09Wash");
                var pushVoice = StartCoroutine(PlayVoice(art.voSc09RayPush));
                sc09ShowerRay.Push(); // the plate is pressed by itself
                yield return pushVoice;
                StartCoroutine(PlayVoice(art.voSc09Flush));
                yield return sc09ShowerRay.RunWash();
                Stage("sc09WashDone");
                yield return PresentableFx.Wait(0.8f);
                sc09ShowerRay.StopWater();
                sc09ShowerRay.HideHolo();
                StopVoice();

                // back to the table: the acid can, the broken jar and the acid on the table and the floor
                Stage("sc09Back");
                var backVoice = StartCoroutine(PlayVoice(art.voSc09RayBack));
                yield return GlideTurn(sc09NearStand.position, sc09NearStand.forward, sc09GlideSeconds, -1f);
                yield return backVoice;
                yield return PresentableFx.Wait(1.2f);

                Stage("sc09Gate");
                Sc04PopupPose(-0.95f, 0.12f, 1.6f, out var gatePos, out var gateRot);
                var choice = 0; // 1 = PREVIEW, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc09GateRay.Back += OnBack;
                sc09GateRay.Next += OnNext;
                sc09GateRay.Show(gatePos, gateRot, sc06PopupScale);
                StartCoroutine(PlayVoice(art.voSc09RayGate));
                while (choice == 0)
                    yield return null;
                sc09GateRay.Back -= OnBack;
                sc09GateRay.Next -= OnNext;
                sc09GateRay.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                again = choice == 1;
            }

            Stage("sc09Question");
            Sc04PopupPose(-0.95f, 0.25f, 1.6f, out var mcqPos, out var mcqRot);
            mcqSc09.SetWorldScale(sc06PopupScale);
            mcqSc09.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc09, BuildSc09RayQuestion(), false, 9);
        }

        // A small instruction card at the left (as the first card): it shows, its voice plays, it hides by itself.
        private IEnumerator Sc09RayCard(string english, Sprite bangla, AudioClip clip)
        {
            instructionPanel.SetContent(english, bangla);
            instructionPanel.SetCompact(true, sc689CardScale);
            instructionPanel.SetGlass(sc04GlassCard);
            instructionPanel.SetClearLook(true);
            BeginEyeInstruction();
            Sc05AsideInstruction(sc689CardRight, sc689CardUp);
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            _instructionClip = clip;
            yield return PlayVoice(clip);
            instructionPanel.SetSpeaking(false);
            yield return PresentableFx.Wait(0.6f);
            yield return instructionPanel.Hide();
            EndEyeInstruction();
            instructionPanel.SetGlass(false);
            instructionPanel.SetCompact(false);
        }

        // Glide to a floor spot while turning round the head to the stand's direction (direction +1 = turn RIGHT, -1 = turn LEFT).
        private IEnumerator GlideTurn(Vector3 floorTarget, Vector3 targetForward, float duration, float direction)
        {
            if (rig == null || _cam == null)
                yield break;
            var startHead = new Vector3(_cam.position.x, rig.position.y, _cam.position.z);
            var target = new Vector3(floorTarget.x, rig.position.y, floorTarget.z);
            var startYaw = Mathf.Atan2(_cam.forward.x, _cam.forward.z) * Mathf.Rad2Deg;
            var endYaw = Mathf.Atan2(targetForward.x, targetForward.z) * Mathf.Rad2Deg;
            var total = Mathf.Repeat((endYaw - startYaw) * direction, 360f) * direction;
            var lastOffset = Vector3.zero;
            var lastAngle = 0f;
            yield return PresentableFx.Tween(duration, t =>
            {
                var angle = total * t;
                rig.RotateAround(_cam.position, Vector3.up, angle - lastAngle);
                lastAngle = angle;
                var offset = (target - startHead) * t;
                rig.position += offset - lastOffset;
                lastOffset = offset;
                Physics.SyncTransforms();
            });
            PlaceRig(floorTarget, targetForward); // exact final spot and direction
        }

        private static PresentableMcqPanel.Question BuildSc09RayQuestion()
        {
            var labels = Sc09Content.RayOptionLabels;
            var options = new PresentableMcqPanel.Option[labels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = labels[i],
                    feedback = Sc09Content.RayOptionFeedback[i],
                    wrongTitle = Sc09Content.RayOptionWrongTitle[i],
                    reason = Sc09Content.RayOptionReasons != null && i < Sc09Content.RayOptionReasons.Length ? Sc09Content.RayOptionReasons[i] : string.Empty
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc09Content.RayQuestionHeader,
                prompt = Sc09Content.RayQuestionPrompt,
                correctIndex = Sc09Content.RayCorrectIndex,
                correctTitle = Sc09Content.RayCorrectTitle,
                nextLabel = Sc09Content.FinishLabel,
                options = options,
                fancy = true,
                // ray flow: as Scenarios 1-8 - tick / cross, a reason under every option, NEXT (no retry)
                inlineReveal = PresentableInput.RayActive,
                tagCorrect = Sc09Content.TagCorrect,
                tagWrong = Sc09Content.TagWrong
            };
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
            if (PresentableInput.AllowedIn(9) && sc09Auto != null && sc09ShowerRay != null && sc09GateRay != null && sc09NearStand != null &&
                sc09ShowerStandRay != null && envSc06 != null)
            {
                PresentableInput.ActiveScenario = 9; // controller ray, in the Scenario 6/8 factory room (finger touch: set the input mode / RayScenarios)
                yield return RunScenario9Ray();
                yield break;
            }

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

        [Header("Controller ray (Scenario 1)")]
        [SerializeField] private float rayCardDistance = 1.4f;
        [SerializeField] private float rayCardScale = 1.45f;
        [SerializeField] private float rayPanelDistance = 1.05f;
        [SerializeField] private float rayPanelScale = 1.3f;
        [Tooltip("Scenario 1: the drums stand below the card, so it is a little smaller and its centre a little above the eyes.")]
        [SerializeField] private float rayPanelScaleSc01 = 1.15f;
        [SerializeField] private float rayPanelRiseSc01 = 0.15f;
        [Tooltip("Scenario 1 (ray): the instruction card and the question card sit this far to the LEFT of the view (metres) so the drums and the hall stay uncovered. 0 = centred like before.")]
        [SerializeField] private float rayPanelLeftSc01; // set by the scene builder (Sc01CardsLeftOffset), 0 = centred
        [Tooltip("Scenario 2: the card is centred on the eyes.")]
        [SerializeField] private float rayPanelRiseSc02 = 0f;
        [SerializeField] private float rayStartDelay = 5f;

        private bool _sc01Moved;
        private bool _sc01Moving;
        private readonly List<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider> _lockedProviders =
            new List<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>();

        // The learner keeps the head free but does not move: no thumbstick walking / turning / teleport.
        private void LockLocomotion(bool lockIt)
        {
            if (rig == null)
                return;
            if (lockIt)
            {
                _lockedProviders.Clear();
                foreach (var p in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
                {
                    if (!p.enabled)
                        continue;
                    p.enabled = false;
                    _lockedProviders.Add(p);
                }

                if (zoom != null)
                    zoom.ZoomEnabled = false;
            }
            else
            {
                foreach (var p in _lockedProviders)
                    if (p != null)
                        p.enabled = true;
                _lockedProviders.Clear();
            }
        }

        // ------------------------------------------------------------------ Scenario 2, controller-ray flow
        // Fixed stand with a view of the 6 containers and the cabinet. Take Decision (Inspection / Continue); inspecting shows a
        // container's properties card with CLOSE; Continue: pick a container, pick a compartment in the popup, and the container
        // is stored automatically (door opens, container flies in, door closes). Wrong compartment: red door + warning, try again.
        private bool _sc02Back;

        // The pre-callout Scenario 2 question (one MCQ after the placement game). Kept as it was; sc02CalloutFlow = false brings it back.
        private IEnumerator RunSc02ClassicQuestion(bool ray2)
        {
            Stage("sc02Question");
            // the question opens in front of the user, back at the room entrance so the glass doors are not in the way
            if (ray2)
            {
                var qStand = sc02RayStand != null ? sc02RayStand : spawnSc02;
                yield return fader.FadeTo(1f, 0.25f);
                PlaceRig(qStand.position, Flat(qStand.forward));
                mcqSc02.SetWorldScale(rayPanelScale);
                PlacePanelRay(mcqSc02.transform, qStand.position, Flat(qStand.forward), rayPanelRiseSc02);
                subtitle.Dock(mcqSc02.transform, () => 0.45f, 1.0f);
                yield return fader.FadeTo(0f, 0.35f);
            }
            else
            {
                yield return FadeReposition(spawnSc02, Flat(spawnSc02.forward), mcqSc02);
            }

            yield return RunQuestion(mcqSc02, BuildSc02Question(), false, 2);
        }

        // ------------------------------------------------------------------ Scenario 2: callout + question per chemical

        [Tooltip("Scenario 2 as the client wants it: the learner stands at the cabinet, then a callout opens over each chemical in turn " +
                 "(picture, properties) with its own question to the right. false = the old Take Decision / inspect / place game.")]
        [SerializeField] private bool sc02CalloutFlow = true;
        [Tooltip("World scale of the Scenario 2 callout. The callout is about 2 m from the learner (Scenario 1's card is about 1 m away at 1.15), " +
                 "so it needs roughly this much to keep the question text as readable.")]
        [SerializeField] private float sc02CalloutScale = 2.0f;
        [SerializeField] private float sc02FirstCalloutDelay = 3f;
        [SerializeField] private float sc02NextCalloutDelay = 3f;
        [SerializeField] private float sc02ReadSeconds = 7f;

        private Sc02Callout _sc02Callout;
        private bool _sc02Placing;

        private IEnumerator RunSc02Callouts()
        {
            var stand = sc02RayStand != null ? sc02RayStand : spawnSc02;
            var fwd = Flat(stand.forward);
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.4f);
            PlaceRig(stand.position, fwd);
            if (placement != null)
                placement.AdjustRows(_cam != null ? _cam.position.y - rig.position.y : 1.6f);
            yield return fader.FadeTo(0f, 0.6f);

            Stage("sc02Stand");
            subtitle.Hide();
            inspect.SetCardAllowed(false);
            inspect.SetShowCardOnPick(false);
            inspect.SetInteractable(false);
            if (_sc02Callout == null)
                _sc02Callout = Sc02Callout.Create(art);

            // left to right as the learner sees the pallet
            var right = Vector3.Cross(Vector3.up, fwd);
            var order = new List<int>();
            for (var i = 0; i < inspect.Count; i++)
                order.Add(i);
            order.Sort((a, b) => Vector3.Dot(inspect.ModelOf(a).position, right).CompareTo(Vector3.Dot(inspect.ModelOf(b).position, right)));

            SessionResults.BeginScenario("SC02", Loc.Suffix());
            yield return PresentableFx.Wait(sc02FirstCalloutDelay);
            for (var n = 0; n < order.Count; n++)
            {
                var last = n == order.Count - 1;
                yield return Sc02Chemical(order[n], n, last);
                if (!last)
                    yield return PresentableFx.Wait(sc02NextCalloutDelay);
            }

            SessionResults.EndScenario();
            inspect.SetInteractable(false);
            while (placement != null && placement.IsBusy)
                yield return null;
        }

        private static Vector3 TopCentre(Transform model)
        {
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
                return model.position + Vector3.up * 0.8f;
            var b = rs[0].bounds;
            foreach (var r in rs)
                b.Encapsulate(r.bounds);
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        // true when the learner's answer to the current callout question was right: only then does the chemical go to its compartment
        private bool _sc02AnswerCorrect;

        private IEnumerator Sc02Chemical(int chem, int n, bool last)
        {
            Stage("sc02Callout" + (n + 1));
            _sc02AnswerCorrect = false;
            var model = inspect.ModelOf(chem);
            var s = sc02CalloutScale;
            var icon = art.sc02ChemIcons != null && chem < art.sc02ChemIcons.Length ? art.sc02ChemIcons[chem] : null;

            // One large callout over the chemical: picture + properties on the left, the question box on the right, same height.
            // It is centred on the container but kept near the middle of the view; its tail points down at the container.
            var cam = _cam != null ? _cam.position : rig.position;
            var standAxis = Flat(sc02RayStand != null ? sc02RayStand.forward : spawnSc02.forward);
            var axisRight = Vector3.Cross(Vector3.up, standAxis);
            var top = TopCentre(model);
            var rel = Vector3.Dot(top - cam, axisRight);
            var center = top + axisRight * (Mathf.Clamp(rel, -0.5f, 0.5f) - rel);

            mcqSc02.SetEmbedded(true);
            mcqSc02.SetWorldScale(s);
            _sc02Callout.Follower = mcqSc02.transform;
            mcqSc02.EmbeddedResult += _sc02Callout.ShowResult;
            yield return _sc02Callout.Open(top, center, Sc02Content.ChemChip(n + 1, inspect.Count), Sc02Content.ChemName(chem),
                Sc02Content.ChemProps(chem), icon, s);

            _sc02Placing = false;
            yield return AskSc02(chem, n, last);

            mcqSc02.EmbeddedResult -= _sc02Callout.ShowResult;
            mcqSc02.Hide();
            if (!last)
            {
                yield return _sc02Callout.Close();
                _sc02Callout.Follower = null;
                mcqSc02.SetEmbedded(false);
                // client: a RIGHT answer sends the chemical to its compartment; a wrong answer is only told it is wrong and the
                // chemical stays where it is (it is never moved to the correct place for the learner)
                if (_sc02AnswerCorrect)
                    yield return Sc02PlaceChemical(chem);
            }
            else
            {
                yield return _sc02Callout.Close();
                _sc02Callout.Follower = null;
                mcqSc02.SetEmbedded(false);
                mcqSc02.SetWorldScale(1f);
                yield return PresentableFx.Wait(0.35f);
            }
        }

        private PresentableMcqPanel.Question BuildSc02ChemQuestion(int chem, int n, bool last)
        {
            var labels = Sc02Content.QuestionOptions(chem);
            var reasons = Sc02Content.QuestionReasons(chem);
            var options = new PresentableMcqPanel.Option[labels.Length];
            for (var k = 0; k < options.Length; k++)
                options[k] = new PresentableMcqPanel.Option { label = labels[k], reason = reasons[k], feedback = reasons[k] };
            return new PresentableMcqPanel.Question
            {
                header = Sc02Content.QuestionHeader,
                prompt = Sc02Content.QuestionText(chem, n + 1),
                options = options,
                correctIndex = 1,
                nextLabel = Sc02Content.FinishLabel,
                fancy = true,
                inlineReveal = true,
                hideNext = !last,
                tagCorrect = Sc02Content.TagCorrect,
                tagWrong = Sc02Content.TagWrong,
                holdNext = last ? (System.Func<bool>)(() => _sc02Placing) : null
            };
        }

        // One question: shuffled, marked, the answer revealed on the rows with a reason under each option. Questions 1-5 carry on by
        // themselves after the reading time; question 6 waits for NEXT (and for the chemical to reach its compartment).
        private IEnumerator AskSc02(int chem, int n, bool last)
        {
            var panel = mcqSc02;
            var q = BuildSc02ChemQuestion(chem, n, last);
            var questionId = "SC02.Q" + (n + 1);
            var order = ShuffleOptions(q, questionId);
            q.sourceOrder = order;
            var shown = new List<OptionPair>();
            for (var i = 0; i < q.options.Length; i++)
                shown.Add(new OptionPair { key = ((char)('A' + i)).ToString(), text = OptionText(q.options[i].label), optionId = order[i].ToString() });
            SessionResults.OpenQuestion(questionId, q.prompt, shown, ((char)('A' + q.correctIndex)).ToString());

            var revealed = false;
            var nextPressed = false;
            void OnAnswered(int index, bool correct)
            {
                SessionResults.RecordTry(((char)('A' + index)).ToString(), correct);
                StopVoice();
                _sc02AnswerCorrect = correct;
                if (last && correct)
                    StartCoroutine(Sc02PlaceChemical(chem)); // question 6, right answer: the chemical moves while the reasons are read, NEXT follows
            }

            void OnPopupShown(bool correct)
            {
                if (_answerVoice != null)
                    StopCoroutine(_answerVoice);
                _answerVoice = StartCoroutine(Sc02AnswerVoice(correct, last));
            }

            void OnRevealed(bool correct) => revealed = true;
            void OnNext() => nextPressed = true;

            panel.Answered += OnAnswered;
            panel.PopupShown += OnPopupShown;
            panel.Revealed += OnRevealed;
            panel.NextPressed += OnNext;
            panel.Show(q);
            if (last)
            {
                while (!nextPressed)
                    yield return null;
            }
            else
            {
                while (!revealed)
                    yield return null;
                yield return PresentableFx.Wait(sc02ReadSeconds);
            }

            panel.Answered -= OnAnswered;
            panel.PopupShown -= OnPopupShown;
            panel.Revealed -= OnRevealed;
            panel.NextPressed -= OnNext;
            if (_answerVoice != null)
                StopCoroutine(_answerVoice);
            StopVoice();
        }

        private IEnumerator Sc02AnswerVoice(bool correct, bool last)
        {
            yield return PresentableFx.Wait(0.25f);
            StopVoice();
            if (!last)
            {
                yield return PlayVoice(correct ? art.voCorrect : art.voAnswerWrongSee);
                yield break;
            }

            if (!correct)
            {
                yield return PlayVoice(Loc.IsBangla ? art.voSc02WrongNextBn : art.voSc02WrongNextEn);
                yield break;
            }

            yield return PlayVoice(art.voCorrect);
            yield return PresentableFx.Wait(1f);
            yield return PlayVoice(Loc.IsBangla ? art.voSc02CompleteBn : art.voSc02CompleteEn);
        }

        // The chemical is picked up and goes into its compartment (door opens, container flies in, door closes).
        private IEnumerator Sc02PlaceChemical(int chem)
        {
            _sc02Placing = true;
            inspect.ReturnAll();
            inspect.SetShowCardOnPick(false);
            inspect.SetInteractable(true);
            var poke = inspect.PokeOf(chem);
            if (poke != null)
                poke.RayFire();
            var wait = 0f;
            while (!inspect.HasHeld && wait < 3f)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            if (inspect.HasHeld)
                yield return placement.AutoPlace(placement.CompartmentOf(chem));
            inspect.SetInteractable(false);
            _sc02Placing = false;
        }

        private IEnumerator RunSc02RayFlow()
        {
            var stand = sc02RayStand != null ? sc02RayStand : spawnSc02;
            var fwd = Flat(stand.forward);
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.4f);
            PlaceRig(stand.position, fwd);
            if (placement != null)
                placement.AdjustRows(_cam != null ? _cam.position.y - rig.position.y : 1.6f);
            yield return fader.FadeTo(0f, 0.6f);

            Stage("sc02Explore");
            chart?.SetDistance(0.58f);
            var inspected = new HashSet<int>();
            void OnHeld()
            {
                var i = inspect.HeldIndex;
                if (i >= 0)
                    inspected.Add(i);
            }

            inspect.HeldChanged += OnHeld;
            var total = inspect.Count;
            bool AllInspected()
            {
                for (var i = 0; i < total; i++)
                    if (!inspected.Contains(i) && !inspect.IsPlaced(i))
                        return false;
                return true;
            }

            var eye = _cam != null ? _cam.position.y : 1.6f;
            var cardPos = new Vector3(stand.position.x, 0f, stand.position.z) + fwd * 1.1f + Vector3.up * (eye + rayPanelRiseSc02);
            var cardRot = Quaternion.LookRotation(fwd, Vector3.up);

            var firstTime = true;
            while (placement != null && placement.Placed < placement.Total)
            {
                Stage("sc02Decision");
                var choice = 0;
                void OnInsp() => choice = 1;
                void OnCont() => choice = 2;
                sc02Decision.Inspection += OnInsp;
                sc02Decision.Continue += OnCont;
                sc02Decision.Show(cardPos, cardRot, rayPanelScale, !AllInspected());
                StopVoice();
                StartCoroutine(PlayVoice(art.voSc02Decision));
                if (firstTime)
                {
                    subtitle.Dock(sc02Decision.Card, () => -0.52f, 1.0f);
                    firstTime = false;
                }

                while (choice == 0)
                    yield return null;
                sc02Decision.Inspection -= OnInsp;
                sc02Decision.Continue -= OnCont;
                sc02Decision.Hide();
                StopVoice();

                if (choice == 1)
                {
                    Stage("sc02Inspect");
                    var closeRequested = false;
                    void OnClose() => closeRequested = true;
                    inspect.CloseRequested += OnClose;
                    inspect.SetShowCardOnPick(true);
                    inspect.SetCardAllowed(true);
                    inspect.SetInteractable(true);
                    StartCoroutine(SubtitledVoice(null, Sc02Content.InspectHint, art.voSc02Inspect));
                    do
                    {
                        closeRequested = false;
                        while (!closeRequested)
                            yield return null;
                        inspect.ReturnAll();
                        inspect.SetInteractable(true);
                    } while (!AllInspected());

                    inspect.CloseRequested -= OnClose;
                    inspect.SetInteractable(false);
                    subtitle.Hide();
                    continue; // everything inspected: Take Decision comes back by itself
                }

                // Continue: store the six containers one after the other
                _sc02Back = false;
                while (!_sc02Back && placement.Placed < placement.Total)
                    yield return Sc02PlaceOne(stand.position, fwd);
            }

            inspect.HeldChanged -= OnHeld;
            while (placement != null && placement.IsBusy)
                yield return null;
            yield return PresentableFx.Wait(0.4f);
            inspect.SetShowCardOnPick(true);
            inspect.SetInteractable(false);
            subtitle.Hide();
        }

        private IEnumerator Sc02PlaceOne(Vector3 standPos, Vector3 fwd)
        {
            Stage("sc02Place");
            inspect.ReturnAll();
            inspect.SetShowCardOnPick(false);
            inspect.SetInteractable(true);
            StartCoroutine(SubtitledVoice(null, Sc02Content.PickContainerHint, art.voSc02Pick));
            while (!inspect.HasHeld)
                yield return null;
            StopVoice();
            subtitle.Hide();

            var eye = _cam != null ? _cam.position.y : 1.6f;
            sc02DestPopup.Configure(Sc02Content.Compartments, Sc02Content.DestTitle, Sc02Content.DestTitleWrong);
            sc02DestPopup.SetWorldScale(rayPanelScale);
            sc02DestPopup.transform.SetPositionAndRotation(
                new Vector3(standPos.x, 0f, standPos.z) + fwd * 1.05f + Vector3.up * (eye + rayPanelRiseSc02),
                Quaternion.LookRotation(fwd, Vector3.up));
            while (true)
            {
                var chosen = -1;
                var closed = false;
                void OnChosen(int c) => chosen = c;
                void OnClosed() => closed = true;
                sc02DestPopup.Chosen += OnChosen;
                sc02DestPopup.Closed += OnClosed;
                sc02DestPopup.Open();
                StartCoroutine(PlayVoice(art.voSc02Dest));
                while (chosen < 0 && !closed)
                    yield return null;
                sc02DestPopup.Chosen -= OnChosen;
                sc02DestPopup.Closed -= OnClosed;
                StopVoice();
                sc02DestPopup.Close();
                if (closed)
                {
                    inspect.ReturnAll();
                    _sc02Back = true;
                    yield break;
                }

                yield return placement.AutoPlace(chosen);
                if (placement.LastCorrect)
                    yield break;
                yield return PresentableFx.Wait(0.3f);
            }
        }

        private IEnumerator RunSc01RayFlow()
        {
            Stage("transition");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.4f);
            var stand = sc01RayStand != null ? sc01RayStand : spawnDrumView;
            var fwd = Flat(stand.forward);
            PlaceRig(stand.position, fwd);
            yield return fader.FadeTo(0f, 0.6f);

            Stage("explore");
            // the four name plates (1. Faded Label ... 4.) float above the drums for the whole scenario
            for (var i = 0; i < drums.Length; i++)
                drums[i]?.RevealPlate();

            yield return PresentableFx.Wait(rayStartDelay); // the learner takes in the room and the four drums first

            Stage("mcq1");
            mcqSc01.SetWorldScale(rayPanelScaleSc01);
            PlacePanelRay(mcqSc01.transform, stand.position, fwd, rayPanelRiseSc01, rayPanelLeftSc01);
            // above the question card: below it the learner is looking at the drums
            subtitle.Dock(mcqSc01.transform, () => 0.45f, 1.0f);
            yield return RunQuestion(mcqSc01, BuildSc01Question(), true);
            while (_sc01Moving)
                yield return null;
        }

        // Straight ahead of the learner, bottom edge at eye level so the drums below it stay uncovered.
        private void PlacePanelRay(Transform panel, Vector3 standPos, Vector3 forward, float rise, float left = 0f)
        {
            var fwd = Flat(forward);
            var eye = _cam != null ? _cam.position.y : 1.6f;
            var origin = new Vector3(standPos.x, 0f, standPos.z);
            var right = Vector3.Cross(Vector3.up, fwd);
            panel.position = origin + fwd * rayPanelDistance - right * left + Vector3.up * (eye + rise);
            // moved to the left: turn it toward the learner so it stays readable and the ray still hits it square-on
            var look = left > 0.001f ? Flat(panel.position - origin) : fwd;
            panel.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        // Correct answer: the trolley rolls to the rack, the right drum lifts off and settles on the toe plate, the trolley rolls off.
        private IEnumerator Sc01DrumToTrolley()
        {
            _sc01Moved = true;
            var idx = Mathf.Clamp(Sc01Content.CorrectIndex, 0, drums.Length - 1);
            var hero = drums[idx] != null ? drums[idx].transform : null;
            if (hero == null || trolley == null || carrierLoad == null)
                yield break;
            _sc01Moving = true;
            if (flatCart)
            {
                yield return Sc01DrumToFlatCart(hero);
                _sc01Moving = false;
                yield break;
            }

            var tr = trolley.transform;
            carrierLoad.SetActive(false);
            trolley.SetActive(true);
            PresentableFx.Play(PresentableFx.Chime, 0.8f);
            var baseScale = tr.localScale;
            yield return PresentableFx.Tween(0.5f, t => tr.localScale = baseScale * Mathf.LerpUnclamped(0.5f, 1f, t),
                PresentableFx.EaseOutBack);

            // Hand truck geometry measured on DrumCarrier.glb (root local): width along z, depth along x, the toe plate
            // reaches to x = +0.21, the rails and handle are at x <= +0.10. A drum stands on the plate, back against the rails.
            // (The old pose put the drum 0.18 m along z, i.e. beside the frame and through its side: "the trolley is inside the drum".)
            const float seatX = 0.417f;   // drum centre in front of the rails, over the plate
            const float seatY = 0.57f;    // centre height with the rim on the plate
            const float seatTilt = 6f;    // leans back on the rails
            const float faceRack = 270f;  // yaw that points the toe plate (+x) at the rack (+z)
            const float stagingGap = 0.30f;

            var rackRenderers = hero.GetComponentsInChildren<Renderer>(true);
            var hb = rackRenderers.Length > 0 ? rackRenderers[0].bounds : new Bounds(hero.position, Vector3.zero);
            foreach (var r in rackRenderers)
                hb.Encapsulate(r.bounds);
            var hc = hb.center;
            var yawFace = Quaternion.Euler(0f, faceRack, 0f);
            var parked = tr.position;
            var parkedYaw = tr.eulerAngles.y;
            var seatPos = new Vector3(hc.x, 0f, hc.z - seatX); // the trolley root when loaded
            var staging = seatPos + new Vector3(0f, 0f, -stagingGap); // toe plate still clear of the drum

            // roll in, turning to face the rack
            PresentableFx.Play(PresentableFx.Whoosh, 0.4f);
            yield return PresentableFx.Tween(1.3f, t =>
            {
                var p = Vector3.Lerp(parked, staging, t);
                p.y = parked.y;
                tr.position = p;
                tr.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(parkedYaw, faceRack, Mathf.SmoothStep(0f, 1f, t)), 0f);
            }, PresentableFx.EaseOutCubic);
            tr.position = new Vector3(staging.x, parked.y, staging.z);
            tr.rotation = yawFace;

            // the drum is lifted a hand's breadth, the toe plate slides under it, the drum settles and leans back on the rails
            carrierLoad.SetActive(true);
            drums[idx].SetCalloutVisible(false, 0.3f); // its plate leaves with the drum
            var load = carrierLoad.transform;
            var startY = hc.y;
            var peakY = Mathf.Max(startY, seatY) + 0.17f;
            var faceCamera = Quaternion.Euler(0f, 180f, 0f); // the label side towards the learner
            var leanAxis = yawFace * Vector3.forward;         // the truck's width axis
            load.SetPositionAndRotation(new Vector3(hc.x, startY, hc.z), faceCamera);
            foreach (var r in rackRenderers)
                r.enabled = false;
            PresentableFx.Play(PresentableFx.Whoosh, 0.5f);
            yield return PresentableFx.Tween(1.8f, t =>
            {
                var slide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.72f, t));
                tr.position = new Vector3(Mathf.Lerp(staging.x, seatPos.x, slide), parked.y, Mathf.Lerp(staging.z, seatPos.z, slide));
                float y;
                if (t < 0.30f)
                    y = Mathf.Lerp(startY, peakY, Mathf.SmoothStep(0f, 1f, t / 0.30f));
                else if (t < 0.72f)
                    y = peakY;
                else
                    y = Mathf.Lerp(peakY, seatY, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t)));
                var lean = seatTilt * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));
                load.SetPositionAndRotation(new Vector3(hc.x, y, hc.z), Quaternion.AngleAxis(lean, leanAxis) * faceCamera);
            }, x => x);
            tr.position = new Vector3(seatPos.x, parked.y, seatPos.z);
            load.SetPositionAndRotation(new Vector3(hc.x, seatY, hc.z), Quaternion.AngleAxis(seatTilt, leanAxis) * faceCamera);
            PresentableFx.Haptic(true, 0.5f, 0.08f);
            PresentableFx.Haptic(false, 0.5f, 0.08f);
            PresentableFx.Play(PresentableFx.Tick, 0.9f);
            StartCoroutine(Sc01LandRing(new Vector3(hc.x, 0f, hc.z)));
            yield return PresentableFx.Wait(0.4f);

            // loaded: like a real hand truck it tips back a little, is pulled handle first to the right (never toward the
            // learner's face: it used to roll to under a metre from the camera and its drum filled the view), and folds away
            var p0 = tr.position;
            var away = p0 + new Vector3(1.5f, 0f, 0.15f);
            var scale0 = tr.localScale;
            yield return PresentableFx.Tween(2.0f, t =>
            {
                var e = Mathf.SmoothStep(0f, 1f, t);
                tr.position = Vector3.Lerp(p0, away, e);
                // the handle (local -x) ends up pointing along the way it is pulled (+x of the room)
                tr.rotation = Quaternion.Euler(0f, Mathf.Lerp(faceRack, 180f, e), 0f) *
                              Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(Mathf.Min(1f, t * 3f) * Mathf.PI * 0.5f));
                tr.localScale = scale0 * Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, t)));
            }, x => x);
            trolley.SetActive(false);
            tr.localScale = scale0;
            _sc01Moving = false;
        }

        // Reference-hall cart: it rolls up beside the correct drum, the drum arcs over onto the platform (upright, label to the
        // learner) and the cart is pulled away handle first.
        private IEnumerator Sc01DrumToFlatCart(Transform hero)
        {
            var tr = trolley.transform;
            var load = carrierLoad.transform;
            var seatLocal = load.localPosition; // set by the builder: centre of the drum standing on the platform
            carrierLoad.SetActive(false);
            if (!trolley.activeSelf) // the cart normally stands in the corner from the start (reference): no pop-in then
            {
                trolley.SetActive(true);
                PresentableFx.Play(PresentableFx.Chime, 0.8f);
                var baseScale = tr.localScale;
                yield return PresentableFx.Tween(0.5f, t => tr.localScale = baseScale * Mathf.LerpUnclamped(0.5f, 1f, t),
                    PresentableFx.EaseOutBack);
            }
            else
            {
                PresentableFx.Play(PresentableFx.Chime, 0.8f);
            }

            var rackRenderers = hero.GetComponentsInChildren<Renderer>(true);
            var hb = rackRenderers.Length > 0 ? rackRenderers[0].bounds : new Bounds(hero.position, Vector3.zero);
            foreach (var r in rackRenderers)
                hb.Encapsulate(r.bounds);
            var hc = hb.center;
            const float yaw = -90f; // platform front toward the drums
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var parked = tr.position;
            var parkedYaw = tr.eulerAngles.y;
            var stage = new Vector3(hc.x + 1.0f, parked.y, hc.z);

            // rolls out of the corner and turns its platform toward the drums
            PresentableFx.Play(PresentableFx.Whoosh, 0.4f);
            yield return PresentableFx.Tween(1.4f, t =>
            {
                var p = Vector3.Lerp(parked, stage, Mathf.SmoothStep(0f, 1f, t));
                p.y = parked.y;
                tr.position = p;
                tr.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(parkedYaw, yaw, Mathf.SmoothStep(0f, 1f, t)), 0f);
            }, x => x);
            tr.position = stage;
            tr.rotation = rot;

            // the drum leaves the pallet in an arc and settles on the platform
            var seat = stage + rot * Vector3.Scale(seatLocal, tr.localScale);
            carrierLoad.SetActive(true);
            drums[Mathf.Clamp(Sc01Content.CorrectIndex, 0, drums.Length - 1)].SetCalloutVisible(false, 0.3f);
            var face = Quaternion.Euler(0f, 180f, 0f);
            load.SetPositionAndRotation(hc, face);
            foreach (var r in rackRenderers)
                r.enabled = false;
            PresentableFx.Play(PresentableFx.Whoosh, 0.5f);
            yield return PresentableFx.Tween(1.5f, t =>
            {
                var p = Vector3.Lerp(hc, seat, Mathf.SmoothStep(0f, 1f, t));
                p.y += Mathf.Sin(t * Mathf.PI) * 0.5f;
                load.SetPositionAndRotation(p, face);
            }, x => x);
            load.SetPositionAndRotation(seat, face);
            PresentableFx.Haptic(true, 0.5f, 0.08f);
            PresentableFx.Haptic(false, 0.5f, 0.08f);
            PresentableFx.Play(PresentableFx.Tick, 0.9f);
            StartCoroutine(Sc01LandRing(new Vector3(seat.x, 0f, seat.z)));
            yield return PresentableFx.Wait(0.4f);

            // pulled away handle first (the handle is local -z), folds away at the end
            var p0 = tr.position;
            var away = p0 + new Vector3(1.2f, 0f, -0.9f); // the tall wall is just to the right: leave along it, not through it
            var scale0 = tr.localScale;
            yield return PresentableFx.Tween(2.0f, t =>
            {
                var e = Mathf.SmoothStep(0f, 1f, t);
                tr.position = Vector3.Lerp(p0, away, e);
                tr.rotation = Quaternion.Euler(0f, yaw + 18f * e, 0f);
                tr.localScale = scale0 * Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, t)));
            }, x => x);
            trolley.SetActive(false);
            tr.localScale = scale0;
            tr.rotation = rot;
        }

        // A soft ring of light on the floor where the drum settles on the trolley.
        private IEnumerator Sc01LandRing(Vector3 floorPos)
        {
            var canvas = PresentableUi.WorldCanvas("Sc01LandRing", null, new Vector2(400f, 400f), 0.0024f, false, 5);
            canvas.transform.position = new Vector3(floorPos.x, 0.012f, floorPos.z);
            canvas.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var img = PresentableUi.Img(PresentableUi.Rect("Glow", canvas.transform, Vector2.zero, Vector2.one), art.radialGlow,
                new Color(PresentableMcqPanel.EmeraldMain.r, PresentableMcqPanel.EmeraldMain.g, PresentableMcqPanel.EmeraldMain.b, 0f));
            var s0 = canvas.transform.localScale;
            yield return PresentableFx.Tween(1.0f, t =>
            {
                canvas.transform.localScale = s0 * Mathf.Lerp(0.5f, 1.5f, t);
                img.color = new Color(PresentableMcqPanel.EmeraldMain.r, PresentableMcqPanel.EmeraldMain.g, PresentableMcqPanel.EmeraldMain.b,
                    0.7f * (1f - t));
            }, PresentableFx.EaseOutCubic);
            Destroy(canvas.gameObject);
        }


        private void LateUpdate()
        {
            if (!_eyeInstruction || instructionPanel == null)
                return;
            if (PresentableInput.RayActive)
                return; // ray: the card stays where it appeared (world fixed), the ray aims at it
            // Reaching for the speaker or Continue: freeze the card so the target does not slide away.
            if (FingerCovers(instructionPanel.transform, 0.22f, 200f))
                return;
            PlaceAtEyeLevel(instructionPanel.transform, EyeCardDistance(), true);
        }

        private float EyeCardDistance()
        {
            if (PresentableInput.RayActive)
                return rayCardDistance; // pointing from a distance: the learner sees the whole room behind the card
            // Headset: close enough to poke without a full arm extension. The desk preview stays further back.
            return XRSettings.isDeviceActive ? Mathf.Min(sc03CardDistance, 0.52f) : 0.95f;
        }

        private void BeginEyeInstruction()
        {
            _eyeInstruction = true;
            PlaceAtEyeLevel(instructionPanel.transform, EyeCardDistance(), false);
            // Scenario 1 on the controller ray: the card sits to the left of the view, the hall stays visible
            if (PresentableInput.RayActive && PresentableInput.ActiveScenario == 1 && _cam != null && rayPanelLeftSc01 > 0.001f)
            {
                var origin = new Vector3(_cam.position.x, 0f, _cam.position.z);
                var t = instructionPanel.transform;
                var right = Vector3.Cross(Vector3.up, Flat(_cam.forward));
                t.position -= right * rayPanelLeftSc01;
                t.rotation = Quaternion.LookRotation(Flat(t.position - new Vector3(_cam.position.x, t.position.y, _cam.position.z)), Vector3.up);
            }
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
                startPos = endPos + (flatCart ? new Vector3(0f, -0.3f, 0.55f) : new Vector3(0f, -0.42f, 0.28f));
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
        /// on options (letters exist only in the results file).
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
                    // the answer is shown, not retried: after a wrong pick the right drum lights up green as well
                    if (!correct && q.inlineReveal)
                    {
                        var right = order[q.correctIndex];
                        if (right < drums.Length && drums[right] != null)
                            drums[right].Highlight(PresentableUi.Green, true);
                    }
                }

                if (scenario == 1 && correct && PresentableInput.RayActive && !_sc01Moved)
                    StartCoroutine(Sc01DrumToTrolley());

                StopVoice();
                // Scenario 3 on the ray: no blast any more (client); the answer is shown with its reasons, a right one plays the protocol
                if (scenario == 3 && !correct && sc03Detonation != null && (sc03BlastOnWrong || !PresentableInput.RayActive))
                    sc03Detonation.Explode();
                if (scenario == 3 && correct && PresentableInput.RayActive)
                    StartCoroutine(Sc03ProtocolRoutine());
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
                _answerVoice = StartCoroutine(AnswerVoice(correct, scenario, q.inlineReveal));
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
        private IEnumerator AnswerVoice(bool correct, int scenario, bool inlineReveal = false)
        {
            yield return PresentableFx.Wait(0.25f);
            // Scenario 7, right answer: no "success" / NEXT voice until the hero has put the fire out (the fire-out line plays by itself)
            if (scenario == 7 && correct)
                while (!_sc07WinDone)
                    yield return null;
            StopVoice();
            subtitle.Hide();
            var wrongClip = scenario == 3 && art.voSc03Wrong != null ? art.voSc03Wrong : art.voWrong;
            var correctClip = art.voCorrect;
            if (scenario == 1)
            {
                correctClip = Sc01(art.voCorrect, art.trialCorrect);
                wrongClip = Sc01(art.voWrong, art.trialWrong);
            }

            if (scenario == 1 && !correct && art.voSc01WrongNextBn != null)
            {
                // no retry in Scenario 1: it says why and sends the learner on with NEXT
                yield return PlayVoice(Loc.IsBangla ? art.voSc01WrongNextBn : art.voSc01WrongNextEn);
                yield break;
            }

            if (scenario == 3 && PresentableInput.RayActive)
            {
                // the "correct" line and the protocol narration already played during the animation; NEXT is up now
                if (!correct)
                    yield return PlayVoice(Loc.IsBangla ? art.voSc03WrongNextBn : art.voSc03WrongNextEn);
                else
                    yield return PlayVoice(Loc.IsBangla ? art.voSc03CompleteBn : art.voSc03CompleteEn);
                yield break;
            }

            // Scenario 4-9 on the ray: the answer is shown with its reasons and the scenario goes on (no retry), as in Scenario 1-3
            if (!correct && inlineReveal && scenario >= 4 && art.voWrongNextBn != null && scenario < art.voWrongNextBn.Length && art.voWrongNextBn[scenario] != null)
            {
                yield return PlayVoice(art.voWrongNextBn[scenario]);
                yield break;
            }

            yield return PlayVoice(correct ? correctClip : wrongClip);
            if (!correct)
                yield break;
            yield return PresentableFx.Wait(1f);
            if (scenario == 12)
            {
                yield return PlayVoice(Loc.IsBangla ? art.voSc12CompleteBn : art.voSc12CompleteEn);
                yield break;
            }

            if (scenario == 11)
            {
                yield return PlayVoice(Loc.IsBangla ? art.voSc11CompleteBn : art.voSc11CompleteEn);
                yield break;
            }

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

        // Login page greeting: the English clip, then a 1 s gap, then the Bangla clip, whatever language is selected.
        private bool _welcomeDone = true;
        private Coroutine _welcomeVoice;

        private IEnumerator LoginWelcomeVoice()
        {
            yield return PresentableFx.Wait(1.2f); // the login page fades in first
            var english = Resources.Load<AudioClip>("VoEn/VO_AssalamuAlaikum");
            Debug.Log("[Voice] login greeting: English clip " + (english != null ? english.name : "MISSING"));
            yield return PlayVoiceAsIs(english);
            yield return PresentableFx.Wait(1f);
            Debug.Log("[Voice] login greeting: Bangla clip " + (art.voSalam != null ? art.voSalam.name : "MISSING"));
            yield return PlayVoiceAsIs(art.voSalam);
            _welcomeDone = true;
        }

        // Plays exactly this clip (no English / Bangla swap by the selected language).
        private IEnumerator PlayVoiceAsIs(AudioClip clip)
        {
            if (clip == null || voice == null)
                yield break;
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();
            voice.Stop();
            voice.clip = clip;
            voice.Play();
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
            var reason = Sc01Content.OptionReasons;
            return new PresentableMcqPanel.Question
            {
                inlineReveal = true, // client: no right / wrong popup, tick / cross on the rows + a reason under each, then NEXT (also after a wrong answer)
                tagCorrect = Sc01Content.TagCorrect,
                tagWrong = Sc01Content.TagWrong,
                holdNext = () => _sc01Moving,
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
                        feedback = feedback[0],
                        reason = reason[0]
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = label[1],
                        wrongLabel = wrong[1],
                        icons = new[] { art.thumbNoLabel },
                        feedback = feedback[1],
                        reason = reason[1]
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = label[2],
                        wrongLabel = wrong[2],
                        icons = new[] { art.iconGhs, art.iconSds, art.thumbNoLabel },
                        overlays = new Sprite[] { null, null, art.labelMethanol },
                        feedback = feedback[2],
                        reason = reason[2]
                    },
                    new PresentableMcqPanel.Option
                    {
                        label = label[3],
                        icons = new[] { art.thumbNoLabel },
                        overlays = new[] { art.labelIsopropanol },
                        feedback = feedback[3],
                        reason = reason[3]
                    }
                },
                fancy = true
            };
        }

        private IEnumerator RunScenario10()
        {
            if (PresentableInput.AllowedIn(10) && sc10RaySpill != null && sc10RayAccident != null && sc10RayGate != null && sc10RayMount != null &&
                sc10RayKitStand != null && sc10RaySpillStand != null && sc08PpeRay != null && envSc06 != null)
            {
                PresentableInput.ActiveScenario = 10; // controller ray, in the Scenario 6/8/9 factory room (finger touch: set the input mode / RayScenarios)
                yield return RunScenario10Ray();
                yield break;
            }

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
            if (envSc11 != null)
                yield return RunScenario11();
        }

        // ------------------------------------------------------------------------------------------------------------------------
        // SC10 on the controller ray: the factory room of Scenarios 6, 8 and 9. The glass instruction card first (the room is still as in
        // Scenario 9's start); on Continue the room fades into the aftermath of the accident (can on the table, acid running over the
        // edge, jar broken on the floor, vapour). Then everything runs by itself: PPE station, spill kit (door, five items), the five
        // step icons (pressed automatically) with the animations, the "inspection complete" card (PREVIEW replays), the question.
        // ------------------------------------------------------------------------------------------------------------------------
        private IEnumerator RunScenario10Ray()
        {
            Stage("sc10");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc07, envSc08, envSc09, envSc10 })
                if (e != null)
                    e.SetActive(false);
            if (sc07Ambience != null)
                sc07Ambience.End();
            envSc06.SetActive(true); // the same factory room as Scenario 6, 8 and 9
            clipboard.SetSheet(art.sdsSc10);
            clipboard.SetVisible(false);
            lab.ResetLab();
            lab.SetVaporOn(false);
            sc06Auto.ResetAll();
            if (sc08Dilute != null)
            {
                sc08Dilute.ResetAll();
                sc08Dilute.SetRodVisible(false);
            }

            if (sc09Auto != null)
                sc09Auto.ResetAll();
            if (sc09VisorRay != null)
                sc09VisorRay.Hide();
            if (sc09ShowerMount != null)
                sc09ShowerMount.SetActive(false); // Scenario 9's shower is not part of Scenario 10
            // the room is shown AFTER the accident from the very first frame (the instruction card opens over it): the acid can lies on the
            // table, the acid is already running and dripping, the glass jar is broken on the floor, vapour rises
            sc08PpeRay.SetStationVisible(true);
            sc08PpeRay.ResetAll();
            sc10RayMount.SetActive(true);
            sc10RayAccident.ResetAll();
            sc10RaySpill.ResetAll();
            sc10RayAccident.Show();
            if (sc06Fan != null)
                sc06Fan.ResetFan();
            PlaceRig(spawnSc06.position, spawnSc06.forward);
            sc06Ambience.Begin();
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc10Intro");
            _instructionClip = art.voSc10RayInstruction != null ? art.voSc10RayInstruction : art.voInstruction;
            instructionPanel.SetContent(Sc10Content.RayInstruction, art.bnSc10RayInstructionPanel);
            LockLocomotion(true); // the head is free, the body stays: no walking about in VR
            instructionPanel.SetCompact(true, sc689CardScale);
            instructionPanel.SetGlass(sc04GlassCard);
            instructionPanel.SetClearLook(true);
            BeginEyeInstruction();
            Sc05AsideInstruction(sc689CardRight, sc689CardUp);
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);
            yield return RayInstructionContinue(); // Continue appears after the voice; the game play starts with the click
            yield return instructionPanel.Hide();
            EndEyeInstruction();
            instructionPanel.SetGlass(false);
            instructionPanel.SetCompact(false);

            yield return RunSc10RayFlow();

            LockLocomotion(false);
            mcqSc10.SetWorldScale(1f);
            // leave the shared room as Scenarios 6, 8 and 9 expect it (the can, the jar and the cap are put back by the accident component)
            sc10RayAccident.ResetAll();
            sc10RaySpill.ResetAll();
            sc10RayMount.SetActive(false);
            sc08PpeRay.ResetAll();
            sc08PpeRay.SetStationVisible(false);
            sc06Auto.ResetAll();
            if (sc09Auto != null)
                sc09Auto.ResetAll();
            clipboard.SetVisible(false);
            lab.ResetLab();
            if (sc06Fan != null)
                sc06Fan.ResetFan();
            if (sc06Ambience != null)
                sc06Ambience.End();
            Stage("sc10Done");
            if (envSc11 != null)
                yield return RunScenario11();
        }

        private IEnumerator Sc10RayStep(Sprite icon, string label, string sub, AudioClip voice, IEnumerator action, string stage)
        {
            Stage(stage);
            var v = voice != null ? StartCoroutine(PlayVoice(voice)) : null;
            yield return sc10RaySpill.WaitIcon(icon, label, sub, sc10IconSeconds); // pressed by itself
            yield return action;
            if (v != null)
                yield return v;
            yield return PresentableFx.Wait(0.5f);
        }

        private IEnumerator RunSc10RayFlow()
        {
            var again = true;
            var first = true;
            while (again)
            {
                // the first round starts straight from the instruction card (the accident is already on screen); PREVIEW starts the
                // whole thing again from the accident (a short dip to black so nothing pops)
                Stage("sc10Reveal");
                if (!first)
                {
                    yield return fader.FadeTo(1f, 0.4f);
                    sc10RayAccident.ResetAll();
                    sc10RaySpill.ResetAll();
                    sc08PpeRay.SetStationVisible(true);
                    sc08PpeRay.ResetAll();
                    sc10RayAccident.Show();
                    PlaceRig(spawnSc06.position, spawnSc06.forward);
                    yield return fader.FadeTo(0f, 1.0f);
                }

                first = false;
                clipboard.SetVisible(false);

                Stage("sc10Look");
                yield return PlayVoice(art.voSc10RayLook);
                yield return PresentableFx.Wait(1.2f);

                Stage("sc10Ppe");
                var ppeVoice = StartCoroutine(PlayVoice(art.voSc10RayPpe));
                yield return sc08PpeRay.AutoRun(0.8f);
                yield return ppeVoice;
                yield return PresentableFx.Wait(0.6f);
                sc08PpeRay.End();
                yield return PresentableFx.Wait(0.4f);

                // turned left to the spill kit on the left wall, next to the PPE station
                Stage("sc10ToKit");
                yield return GlideTurn(sc10RayKitStand.position, sc10RayKitStand.forward, sc10GlideSeconds, -1f);
                yield return PresentableFx.Wait(0.4f);
                Stage("sc10Kit");
                var kitVoice = StartCoroutine(PlayVoice(art.voSc10RayKit));
                yield return sc10RaySpill.AutoOpenKit();
                yield return PresentableFx.Wait(0.4f);
                Stage("sc10Collect");
                sc10RaySpill.ShowCollection(true);
                yield return sc10RaySpill.AutoCollectAll(0.35f);
                yield return kitVoice;
                yield return PresentableFx.Wait(0.9f);
                sc10RaySpill.ShowCollection(false);

                // back to the table: contain, neutralize, absorb, clean the glass, dispose
                Stage("sc10ToSpill");
                yield return GlideTurn(sc10RaySpillStand.position, sc10RaySpillStand.forward, sc10GlideSeconds, 1f);
                yield return PresentableFx.Wait(0.4f);
                // the source first: the acid can is stood upright on the table so no more acid runs (the table is clean after it)
                Stage("sc10Can");
                yield return sc10RayAccident.StandCanUp(); // no voice line for this step (client)
                yield return PresentableFx.Wait(0.6f);
                yield return Sc10RayStep(art.iconSc10Contain, Sc10Content.ContainLabel, null, art.voSc10RayContain, sc10RaySpill.Contain(), "sc10Contain");
                yield return Sc10RayStep(art.iconSc10Neutralize, Sc10Content.NeutralizeLabel, null, art.voSc10RayNeutralize, sc10RaySpill.Neutralize(), "sc10Neutralize");
                yield return Sc10RayStep(art.iconSc10Absorb, Sc10Content.AbsorbLabel, null, art.voSc10RayAbsorb, sc10RaySpill.Absorb(), "sc10Absorb");
                yield return Sc10RayStep(art.iconSc10Clean, Sc10Content.CleanLabel, null, art.voSc10RayClean, sc10RaySpill.CleanGlass(), "sc10Clean");
                yield return Sc10RayStep(art.iconSc10Dispose, Sc10Content.DisposeLabel, Sc10Content.DisposeSub, art.voSc10RayDispose, sc10RaySpill.Dispose(), "sc10Dispose");
                Stage("sc10Disposed");
                yield return PresentableFx.Wait(1.0f);
                // back to the start spot, facing the table: the cards open there
                yield return GlideTurn(spawnSc06.position, spawnSc06.forward, sc10GlideSeconds, -1f);
                yield return PresentableFx.Wait(0.4f);

                Stage("sc10Gate");
                Sc04PopupPose(-0.95f, 0.12f, 1.6f, out var gatePos, out var gateRot);
                var choice = 0; // 1 = BACK (the whole process again), 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc10RayGate.Back += OnBack;
                sc10RayGate.Next += OnNext;
                sc10RayGate.Show(gatePos, gateRot, sc06PopupScale);
                StartCoroutine(PlayVoice(art.voSc10RayGate));
                while (choice == 0)
                    yield return null;
                sc10RayGate.Back -= OnBack;
                sc10RayGate.Next -= OnNext;
                sc10RayGate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                again = choice == 1;
            }

            Stage("sc10Question");
            Sc04PopupPose(-0.95f, 0.25f, 1.6f, out var mcqPos, out var mcqRot);
            mcqSc10.SetWorldScale(sc06PopupScale);
            mcqSc10.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc10, BuildSc10RayQuestion(), false, 10);
        }

        private static PresentableMcqPanel.Question BuildSc10RayQuestion()
        {
            var labels = Sc10Content.RayOptionLabels;
            var options = new PresentableMcqPanel.Option[labels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var reason = Sc10Content.RayOptionReasons != null && i < Sc10Content.RayOptionReasons.Length ? Sc10Content.RayOptionReasons[i] : string.Empty;
                options[i] = new PresentableMcqPanel.Option
                {
                    label = labels[i],
                    feedback = reason,
                    wrongTitle = Sc10Content.RayOptionWrongTitle[i],
                    reason = reason
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc10Content.RayQuestionHeader,
                prompt = Sc10Content.RayQuestionPrompt,
                correctIndex = Sc10Content.RayCorrectIndex,
                correctTitle = Sc10Content.RayCorrectTitle,
                nextLabel = Sc10Content.FinishLabel,
                options = options,
                fancy = true,
                inlineReveal = PresentableInput.RayActive,
                tagCorrect = Sc10Content.TagCorrect,
                tagWrong = Sc10Content.TagWrong
            };
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

        private IEnumerator RunScenario11()
        {
            if (sc11YardLook && PresentableInput.AllowedIn(11))
            {
                yield return RunScenario11Ray();
                yield break;
            }

            Stage("sc11");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06, envSc07, envSc08, envSc09, envSc10 })
                if (e != null)
                    e.SetActive(false);
            if (sc10Ambience != null)
                sc10Ambience.End();
            envSc11.SetActive(true);
            clipboard.SetSheet(art.sdsSc11);
            clipboard.SetVisible(false);
            sc11Yard.Begin();
            PlaceRig(sc11YardStand.position, sc11YardStand.forward);
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc11Intro");
            yield return Sc09Card(Sc11Content.InstructionEnglish, art.bnSc11InstructionPanel,
                art.voSc11Instruction != null ? art.voSc11Instruction : art.voInstruction);

            var again = true;
            while (again)
            {
                yield return Sc11Round();

                Stage("sc11Gate");
                yield return PresentableFx.Wait(0.8f);
                yield return fader.FadeTo(1f, 0.35f);
                sc11Yard.ShowOutside(false);
                sc11Yard.RadioHide();
                PlaceRig(sc11YardStand.position, sc11YardStand.forward);
                subtitle.Hide();
                yield return fader.FadeTo(0f, 0.5f);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc11Gate.Back += OnBack;
                sc11Gate.Next += OnNext;
                sc11Gate.Show();
                StartCoroutine(PlayVoice(art.voSc11Gate));
                while (choice == 0)
                    yield return null;
                sc11Gate.Back -= OnBack;
                sc11Gate.Next -= OnNext;
                sc11Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                if (choice == 1)
                {
                    yield return fader.FadeTo(1f, 0.3f);
                    sc11Yard.ResetAll();
                    PlaceRig(sc11YardStand.position, sc11YardStand.forward);
                    yield return PresentableFx.Wait(0.3f);
                }
                else
                {
                    again = false;
                }
            }

            Stage("sc11Question");
            PlaceAtEyeLevel(mcqSc11.transform, 0.7f, false);
            yield return RunQuestion(mcqSc11, BuildSc11Question(), false, 11);
            sc11Yard.End();
            Stage("sc11Done");
            if (envSc12 != null)
                yield return RunScenario12();
        }

        // ------------------------------------------------------------------------------------------------------------------------
        // SC11 on the controller ray: the client's chlorine cylinder yard. The glass instruction card over the live leak (hiss, gas), on
        // Continue everything runs by itself: alarm (siren icon pressed automatically), everybody leaves and the learner steps back from the
        // plume, the supervisor is called by radio (no conversation), then the "response completed" card (BACK replays) and the question.
        // ------------------------------------------------------------------------------------------------------------------------
        private IEnumerator RunScenario11Ray()
        {
            PresentableInput.ActiveScenario = 11;
            Stage("sc11");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06, envSc07, envSc08, envSc09, envSc10 })
                if (e != null)
                    e.SetActive(false);
            if (sc10Ambience != null)
                sc10Ambience.End();
            envSc11.SetActive(true);
            clipboard.SetSheet(art.sdsSc11);
            clipboard.SetVisible(false);
            sc11Yard.Begin();
            PlaceRig(sc11YardStand.position, sc11YardStand.forward);
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc11Intro");
            _instructionClip = art.voSc11RayInstruction != null ? art.voSc11RayInstruction : art.voInstruction;
            instructionPanel.SetContent(Sc11Content.RayInstruction, art.bnSc11RayInstructionPanel);
            LockLocomotion(true); // the head is free, the body stays: no walking about in VR
            instructionPanel.SetCompact(true, sc689CardScale);
            instructionPanel.SetGlass(sc04GlassCard);
            instructionPanel.SetClearLook(true);
            BeginEyeInstruction();
            Sc05AsideInstruction(sc689CardRight, sc689CardUp + 0.42f); // raised: the cylinders and their labels stay uncovered
            yield return instructionPanel.Show();
            instructionPanel.SetSpeaking(true);
            subtitle.Hide();
            yield return PlayVoice(_instructionClip);
            instructionPanel.SetSpeaking(false);
            yield return RayInstructionContinue(); // Continue appears after the voice; the game play starts with the click
            yield return instructionPanel.Hide();
            EndEyeInstruction();
            instructionPanel.SetGlass(false);
            instructionPanel.SetCompact(false);

            yield return RunSc11RayFlow();

            LockLocomotion(false);
            mcqSc11.SetWorldScale(1f);
            sc11Yard.End();
            Stage("sc11Done");
            PresentableInput.ActiveScenario = 12; // Scenario 12 is finger touch (not in RayScenarios)
            if (envSc12 != null)
                yield return RunScenario12();
        }

        private IEnumerator Sc11RayStep(Sprite icon, string label, AudioClip voice, IEnumerator action, string stage)
        {
            Stage(stage);
            var v = voice != null ? StartCoroutine(PlayVoice(voice)) : null;
            yield return sc11Yard.WaitIcon(icon, label, sc10IconSeconds); // pressed by itself
            yield return action;
            if (v != null)
                yield return v;
            yield return PresentableFx.Wait(0.5f);
        }

        private IEnumerator Sc11RayAlarm()
        {
            sc11Yard.RaiseAlarm();
            yield return PresentableFx.Wait(0.8f);
        }

        private IEnumerator Sc11RayCall()
        {
            yield return sc11Yard.RadioRaise();
            yield return sc11Yard.RadioDial();
            sc11Yard.RadioTalk(true); // the learner reports; no conversation, only the narration
            yield return PresentableFx.Wait(1.8f);
            sc11Yard.RadioTalk(false);
            yield return PresentableFx.Wait(0.4f);
            sc11Yard.RadioHide();
        }

        private IEnumerator RunSc11RayFlow()
        {
            var again = true;
            var first = true;
            while (again)
            {
                // the first round starts straight from the instruction card (the leak is already running); BACK starts the whole thing again
                Stage("sc11Reveal");
                if (!first)
                {
                    yield return fader.FadeTo(1f, 0.4f);
                    sc11Yard.ResetAll();
                    PlaceRig(sc11YardStand.position, sc11YardStand.forward);
                    yield return fader.FadeTo(0f, 1.0f);
                }

                first = false;
                clipboard.SetVisible(false);

                Stage("sc11Look");
                yield return PresentableFx.Wait(1.2f); // hissing, the plume drifts

                yield return Sc11RayStep(art.iconSc11Siren, Sc11Content.AlarmLabel, art.voSc11RayAlarm, Sc11RayAlarm(), "sc11Alarm");

                Stage("sc11Evacuate");
                var evacVoice = StartCoroutine(PlayVoice(art.voSc11RayEvacuate));
                StartCoroutine(sc11Yard.Evacuate());
                yield return Glide(sc11GateStand.position, 3.2f); // the learner steps back from the plume while everybody leaves
                var evacGuard = 0f;
                while (sc11Yard.WorkersOut < sc11Yard.WorkerCount && evacGuard < 2.5f) // the last ones keep walking off during the next step
                {
                    evacGuard += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return evacVoice;

                yield return Sc11RayStep(art.iconSc11Call, Sc11Content.CallLabel, art.voSc11RayCall, Sc11RayCall(), "sc11Call");
                Stage("sc11Called");
                yield return PresentableFx.Wait(0.4f);

                Stage("sc11Gate");
                Sc04PopupPose(-0.95f, 0.12f, 1.6f, out var gatePos, out var gateRot);
                var choice = 0; // 1 = BACK (the whole process again), 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc11Gate.Back += OnBack;
                sc11Gate.Next += OnNext;
                sc11Gate.Show(gatePos, gateRot, sc06PopupScale);
                StartCoroutine(PlayVoice(art.voSc11Gate));
                while (choice == 0)
                    yield return null;
                sc11Gate.Back -= OnBack;
                sc11Gate.Next -= OnNext;
                sc11Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                again = choice == 1;
            }

            Stage("sc11Question");
            Sc04PopupPose(-0.95f, 0.25f, 1.6f, out var mcqPos, out var mcqRot);
            mcqSc11.SetWorldScale(sc06PopupScale);
            mcqSc11.transform.SetPositionAndRotation(mcqPos, mcqRot);
            yield return RunQuestion(mcqSc11, BuildSc11RayQuestion(), false, 11);
        }

        private static PresentableMcqPanel.Question BuildSc11RayQuestion()
        {
            var labels = Sc11Content.RayOptionLabels;
            var reasons = Sc11Content.RayOptionReasons;
            var options = new PresentableMcqPanel.Option[labels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var reason = reasons != null && i < reasons.Length ? reasons[i] : string.Empty;
                options[i] = new PresentableMcqPanel.Option
                {
                    label = labels[i],
                    feedback = reason,
                    wrongTitle = Sc11Content.RayOptionWrongTitle[i],
                    reason = reason
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc11Content.QuestionHeader,
                prompt = Sc11Content.RayQuestionPrompt,
                correctIndex = Sc11Content.RayCorrectIndex,
                correctTitle = Sc11Content.RayCorrectTitle,
                nextLabel = Sc11Content.FinishLabel,
                options = options,
                fancy = true,
                inlineReveal = PresentableInput.RayActive,
                tagCorrect = Sc11Content.TagCorrect,
                tagWrong = Sc11Content.TagWrong
            };
        }

        // SC12: PPE Donning Station. Instruction card -> SDS -> stand on the arrow -> four pick rounds (goggles / gloves / respirator /
        // apron, wrong picks go back to the locker) -> review hologram -> gate card -> multi-select MCQ.
        private IEnumerator RunScenario12()
        {
            Stage("sc12");
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06, envSc07, envSc08, envSc09, envSc10, envSc11 })
                if (e != null)
                    e.SetActive(false);
            if (sc10Ambience != null)
                sc10Ambience.End();
            envSc12.SetActive(true);
            clipboard.SetSheet(art.sdsSc12);
            clipboard.SetVisible(false);
            sc12Station.ResetAll();
            PlaceRig(sc12StartStand.position, sc12StartStand.forward);
            yield return fader.FadeTo(0f, 0.9f);

            Stage("sc12Intro");
            yield return Sc09Card(Sc12Content.InstructionEnglish, art.bnSc12InstructionPanel,
                art.voSc12Instruction != null ? art.voSc12Instruction : art.voInstruction);

            var again = true;
            while (again)
            {
                yield return Sc12Round();

                Stage("sc12Gate");
                yield return PresentableFx.Wait(0.6f);
                var choice = 0; // 1 = BACK, 2 = NEXT
                void OnBack() => choice = 1;
                void OnNext() => choice = 2;
                sc12Gate.Back += OnBack;
                sc12Gate.Next += OnNext;
                sc12Gate.Show();
                StartCoroutine(PlayVoice(art.voSc12Gate));
                while (choice == 0)
                    yield return null;
                sc12Gate.Back -= OnBack;
                sc12Gate.Next -= OnNext;
                sc12Gate.Hide();
                StopAllVoiceCoroutines();
                StopVoice();
                if (choice == 1)
                {
                    yield return fader.FadeTo(1f, 0.3f);
                    sc12Station.ResetAll();
                    PlaceRig(sc12StartStand.position, sc12StartStand.forward);
                    yield return fader.FadeTo(0f, 0.4f);
                }
                else
                {
                    again = false;
                }
            }

            Stage("sc12Question");
            yield return Sc12Question();
            Stage("sc12Done");
            if (envScCheck != null && scCheck != null)
                yield return RunScenarioCheck(13);
        }

        // SC13-SC18: the Supervisor Pre-Task Safety Check. One hall and one runner, the scenario number only selects the content
        // (Resources/Content/SCnn.json). The round results button moves on to the next number; after SC18 the module is complete.
        private IEnumerator RunScenarioCheck(int n)
        {
            Stage("sc" + n);
            PresentableFx.Play(PresentableFx.Whoosh, 0.6f);
            yield return fader.FadeTo(1f, 0.45f);
            foreach (var e in new[] { envSc01, envSc02, envSc03, envSc04, envSc05, envSc06, envSc07, envSc08, envSc09, envSc10, envSc11, envSc12 })
                if (e != null)
                    e.SetActive(false);
            if (sc10Ambience != null)
                sc10Ambience.End();
            envScCheck.SetActive(true);
            clipboard.SetVisible(false);
            PlaceRig(spawnScCheck.position, spawnScCheck.forward);
            yield return fader.FadeTo(0f, 0.9f);
            yield return scCheck.Run(n, _cam);
            Stage("sc" + n + "Done");
            if (n < 18)
                yield return RunScenarioCheck(n + 1);
        }

        private IEnumerator Sc12Round()
        {
            // 1. the safety data sheet
            Stage("sc12Sds");
            StartCoroutine(SubtitledVoice(null, Sc12Content.SdsHint, art.voSc12Sds));
            clipboard.SetVisible(true);
            var sdsWait = 0f;
            while (clipboard.IsVisible && sdsWait < 12f)
            {
                sdsWait += Time.unscaledDeltaTime;
                yield return null;
            }

            clipboard.SetVisible(false);
            StopVoice();
            yield return PresentableFx.Wait(0.4f);

            // 2. stand on the green arrow in front of the lockers
            Stage("sc12Stand");
            StartCoroutine(SubtitledVoice(null, Sc12Content.StandHint, art.voSc12Stand));
            yield return WalkOrGlide(sc12Hotspot);
            sc12Hotspot.Hide();
            StopVoice();
            subtitle.Hide();
            yield return PresentableFx.Wait(0.3f);

            // 3. four pick rounds
            for (var round = 0; round < 4; round++)
            {
                Stage("sc12Pick" + round);
                sc12Station.BeginRound(round);
                StopAllVoiceCoroutines();
                StartCoroutine(PlayVoice(art.voSc12Pick[round]));
                while (true)
                {
                    while (sc12Station.Result == 0)
                        yield return null;
                    if (sc12Station.Result == 2)
                    {
                        StopAllVoiceCoroutines();
                        StartCoroutine(PlayVoice(art.voSc12Wrong));
                        yield return PresentableFx.Wait(1.4f);
                        sc12Station.Retry();
                        continue;
                    }

                    break;
                }

                StopAllVoiceCoroutines();
                StartCoroutine(PlayVoice(art.voSc12Right));
                yield return PresentableFx.Wait(1.3f);
                sc12Station.EndRound();
            }

            // 4. review: the donned operator and the compatible-PPE list
            Stage("sc12Review");
            StopAllVoiceCoroutines();
            subtitle.Hide();
            sc12Station.ShowReview(true);
            StartCoroutine(SubtitledVoice(null, Sc12Content.ReviewHint, art.voSc12Review));
            var reviewDone = false;
            void OnReviewNext() => reviewDone = true;
            sc12Station.ReviewNext += OnReviewNext;
            while (!reviewDone)
                yield return null;
            sc12Station.ReviewNext -= OnReviewNext;
            sc12Station.ShowReview(false);
            StopAllVoiceCoroutines();
            StopVoice();
            subtitle.Hide();
        }

        // The multiple-answer assessment. Marks: one question, full mark when the first submission is exactly the right set; every
        // submission is recorded as the letters of the ticked rows ("A,C,D") in the shown order.
        private IEnumerator Sc12Question()
        {
            _nextPressed = false;
            var labels = Sc12Content.OptionLabels;
            var n = labels.Length;
            var order = OptionShuffle.Order(n, SessionResults.ShuffleSeed, "SC12.Q1", true);
            var shownLabels = new string[n];
            var shownCorrect = new bool[n];
            var shown = new List<OptionPair>();
            var correctLetters = new List<string>();
            for (var i = 0; i < n; i++)
            {
                shownLabels[i] = labels[order[i]];
                shownCorrect[i] = Sc12Content.CorrectSet[order[i]];
                var letter = ((char)('A' + i)).ToString();
                shown.Add(new OptionPair { key = letter, text = OptionText(shownLabels[i]), optionId = order[i].ToString() });
                if (shownCorrect[i])
                    correctLetters.Add(letter);
            }

            var q = new Sc12Assessment.Question
            {
                header = Sc12Content.QuestionHeader,
                prompt = Sc12Content.QuestionPrompt,
                labels = shownLabels,
                correct = shownCorrect,
                submitLabel = Sc12Content.SubmitLabel,
                nextLabel = Sc12Content.FinishLabel,
                rightTitle = Sc12Content.CorrectTitle,
                rightBody = Sc12Content.CorrectBody,
                wrongTitle = Sc12Content.WrongTitleMcq,
                wrongItemBody = Sc12Content.WrongBodyMcq,
                missedBody = Sc12Content.MissedBodyMcq
            };
            SessionResults.BeginScenario("SC12", Loc.Suffix());
            SessionResults.OpenQuestion("SC12.Q1", q.prompt, shown, string.Join(",", correctLetters));

            void OnSubmitted(bool[] ticked, bool correct)
            {
                var letters = new List<string>();
                for (var i = 0; i < ticked.Length; i++)
                    if (ticked[i])
                        letters.Add(((char)('A' + i)).ToString());
                SessionResults.RecordTry(string.Join(",", letters), correct);
                StopVoice();
                if (_answerVoice != null)
                    StopCoroutine(_answerVoice);
                _answerVoice = StartCoroutine(AnswerVoice(correct, 12));
            }

            void OnNext() => _nextPressed = true;

            PlaceAtEyeLevel(assessmentSc12.transform, 0.7f, false);
            assessmentSc12.Submitted += OnSubmitted;
            assessmentSc12.NextPressed += OnNext;
            assessmentSc12.Show(q);
            while (!_nextPressed)
                yield return null;
            assessmentSc12.Submitted -= OnSubmitted;
            assessmentSc12.NextPressed -= OnNext;
            if (_answerVoice != null)
                StopCoroutine(_answerVoice);
            StopVoice();
            SessionResults.EndScenario();
            assessmentSc12.Hide();
            yield return PresentableFx.Wait(0.35f);
        }


        private IEnumerator Sc11Round()
        {
            // 1. the safety data sheet (leak response and cylinder storage)
            Stage("sc11Sds");
            StartCoroutine(SubtitledVoice(null, Sc11Content.SdsHint, art.voSc11Sds));
            clipboard.SetVisible(true);
            var sdsWait = 0f;
            while (clipboard.IsVisible && sdsWait < 12f)
            {
                sdsWait += Time.unscaledDeltaTime;
                yield return null;
            }

            clipboard.SetVisible(false);
            StopVoice();
            yield return PresentableFx.Wait(0.4f);

            // 2. raise the alarm: the exit sign lights up, the siren icon is touched
            Stage("sc11Alarm");
            sc11Yard.ShowExit(true);
            StartCoroutine(SubtitledVoice(null, Sc11Content.AlarmHint, art.voSc11Alarm));
            yield return sc11Yard.WaitIcon(art.iconSc11Siren, Sc11Content.AlarmLabel);
            StopVoice();
            sc11Yard.RaiseAlarm();
            yield return PresentableFx.Wait(1.2f);

            // 3. everybody evacuates; the hero follows through the exit gate
            Stage("sc11Evacuate");
            StartCoroutine(SubtitledVoice(null, Sc11Content.EvacuateHint, art.voSc11Evacuate));
            StartCoroutine(sc11Yard.Evacuate());
            sc11Guide.PointAt(sc11ExitTarget, new Vector3(0f, 2.3f, 0f));
            yield return PresentableFx.Wait(1.2f);
            yield return Glide(sc11GateStand.position, 6f);
            sc11Guide.Hide();
            var evacGuard = 0f;
            while (sc11Yard.WorkersOut < sc11Yard.WorkerCount && evacGuard < 20f)
            {
                evacGuard += Time.unscaledDeltaTime;
                yield return null;
            }

            StopVoice();

            // 4. at the exit: call the supervisor
            Stage("sc11Call");
            StartCoroutine(SubtitledVoice(null, Sc11Content.CallHint, art.voSc11Call));
            yield return sc11Yard.WaitIcon(art.iconSc11Call, Sc11Content.CallLabel);
            StopVoice();

            // 5. outside the fence: the wireless call
            yield return fader.FadeTo(1f, 0.4f);
            sc11Yard.ShowOutside(true);
            PlaceRig(sc11OutsideStand.position, sc11OutsideStand.forward);
            yield return fader.FadeTo(0f, 0.7f);
            Stage("sc11Radio");
            yield return sc11Yard.RadioRaise();
            sc11Yard.ShowMap(true);
            yield return sc11Yard.RadioDial();
            // the technician calls in, the supervisor answers, the technician reports, the supervisor replies (two men)
            subtitle.Show(null, Sc11Content.RadioCall);
            sc11Yard.RadioTalk(true);
            yield return PlayVoice(art.voSc11RadioPlayer1);
            sc11Yard.RadioTalk(false);
            subtitle.Show(null, Sc11Content.RadioAck);
            yield return sc11Yard.RadioReply(LocalizedClip(art.voSc11RadioReply1));
            subtitle.Show(null, Sc11Content.RadioPlayer);
            sc11Yard.RadioTalk(true);
            yield return PlayVoice(art.voSc11RadioPlayer);
            sc11Yard.RadioTalk(false);
            subtitle.Show(null, Sc11Content.RadioReply);
            yield return sc11Yard.RadioReply(LocalizedClip(art.voSc11RadioReply));
            subtitle.Show(null, Sc11Content.RadioOut);
            sc11Yard.RadioTalk(true);
            yield return PlayVoice(art.voSc11RadioPlayer3);
            sc11Yard.RadioTalk(false);
            yield return PresentableFx.Wait(0.6f);
            subtitle.Hide();
            sc11Yard.ShowMap(false);
            sc11Yard.RadioHide();
            Stage("sc11Called");
            yield return PresentableFx.Wait(0.8f);
        }

        private static PresentableMcqPanel.Question BuildSc11Question()
        {
            var options = new PresentableMcqPanel.Option[Sc11Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc11Content.OptionLabels[i],
                    feedback = Sc11Content.OptionFeedback[i],
                    wrongTitle = Sc11Content.OptionWrongTitle[i]
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc11Content.QuestionHeader,
                prompt = Sc11Content.QuestionPrompt,
                correctIndex = Sc11Content.CorrectIndex,
                correctTitle = Sc11Content.CorrectTitle,
                nextLabel = Sc11Content.FinishLabel,
                options = options,
                fancy = true
            };
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
                    wrongTitle = Sc08Content.OptionWrongTitle[i],
                    reason = Sc08Content.OptionReasons != null && i < Sc08Content.OptionReasons.Length ? Sc08Content.OptionReasons[i] : string.Empty
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
                fancy = true,
                // ray flow: as Scenarios 1-7 - tick / cross, a reason under every option, NEXT (no retry)
                inlineReveal = PresentableInput.RayActive,
                tagCorrect = Sc08Content.TagCorrect,
                tagWrong = Sc08Content.TagWrong
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
                    wrongTitle = Sc07Content.OptionWrongTitle[i],
                    reason = Sc07Content.OptionReasons != null && i < Sc07Content.OptionReasons.Length ? Sc07Content.OptionReasons[i] : string.Empty
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
                fancy = true,
                // ray flow: as Scenarios 1-6 - tick / cross, a reason under every option, NEXT (no retry)
                inlineReveal = PresentableInput.RayActive,
                tagCorrect = Sc07Content.TagCorrect,
                tagWrong = Sc07Content.TagWrong
            };
        }

        private static PresentableMcqPanel.Question BuildSc06Question()
        {
            var ray = PresentableInput.RayActive;
            var reasons = Sc06Content.OptionReasons;
            var options = new PresentableMcqPanel.Option[Sc06Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc06Content.OptionLabels[i],
                    feedback = Sc06Content.OptionFeedback[i],
                    wrongTitle = Sc06Content.OptionWrongTitle[i],
                    reason = reasons != null && i < reasons.Length ? reasons[i] : string.Empty
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
                fancy = true, // shared wow look: SC01-SC10
                // ray flow: as Scenarios 1-5 - tick / cross, a reason under every option, NEXT (no retry)
                inlineReveal = ray,
                tagCorrect = Sc06Content.TagCorrect,
                tagWrong = Sc06Content.TagWrong
            };
        }

        private static PresentableMcqPanel.Question BuildSc05Question()
        {
            var ray = PresentableInput.RayActive;
            var reasons = Sc05Content.OptionReasons;
            var options = new PresentableMcqPanel.Option[Sc05Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc05Content.OptionLabels[i],
                    feedback = Sc05Content.OptionFeedback[i],
                    wrongTitle = Sc05Content.OptionWrongTitle[i],
                    reason = reasons != null && i < reasons.Length ? reasons[i] : string.Empty
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
                fancy = true,
                // ray flow: as Scenarios 1-4 - tick / cross, a reason under every option, NEXT (no retry)
                inlineReveal = ray,
                tagCorrect = Sc05Content.TagCorrect,
                tagWrong = Sc05Content.TagWrong
            };
        }

        private static PresentableMcqPanel.Question BuildSc04Question()
        {
            var ray = PresentableInput.RayActive;
            var reasons = Sc04Content.OptionReasons;
            var options = new PresentableMcqPanel.Option[Sc04Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc04Content.OptionLabels[i],
                    feedback = Sc04Content.OptionFeedback[i],
                    wrongTitle = Sc04Content.OptionWrongTitle[i],
                    reason = reasons != null && i < reasons.Length ? reasons[i] : string.Empty
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
                fancy = true,
                // ray flow: as Scenarios 1-3 - tick / cross, a reason under every option, NEXT (no retry)
                inlineReveal = ray,
                tagCorrect = Sc04Content.TagCorrect,
                tagWrong = Sc04Content.TagWrong
            };
        }

        private PresentableMcqPanel.Question BuildSc03Question()
        {
            var ray = PresentableInput.RayActive;
            var reasons = Sc03Content.OptionReasons;
            var options = new PresentableMcqPanel.Option[Sc03Content.OptionLabels.Length];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = new PresentableMcqPanel.Option
                {
                    label = Sc03Content.OptionLabels[i],
                    feedback = Sc03Content.OptionFeedback[i],
                    wrongTitle = Sc03Content.OptionWrongTitle[i],
                    reason = reasons != null && i < reasons.Length ? reasons[i] : string.Empty
                };
            }

            return new PresentableMcqPanel.Question
            {
                header = Sc03Content.QuestionHeader,
                prompt = Sc03Content.QuestionPrompt,
                correctIndex = Sc03Content.CorrectIndex,
                correctTitle = Sc03Content.CorrectTitle,
                nextLabel = Sc03Content.FinishLabel,
                options = options,
                fancy = true,
                // ray flow: as Scenarios 1 and 2 - tick / cross, a reason under every option, NEXT (also after a wrong answer, no retry)
                inlineReveal = ray,
                tagCorrect = Sc03Content.TagCorrect,
                tagWrong = Sc03Content.TagWrong,
                holdNext = ray ? (System.Func<bool>)(() => _sc03Protocol) : null
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
                },
                fancy = true
            };
        }
    }
}
