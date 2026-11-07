# Input mode switch (finger touch vs controller ray) and scenario feedback round

Branch: `feature/Scenario-Feedback-UI-Adjustments-and-Controller-Ray-Input` (cut from `feature/Implement-Multi-Language-Support-with-Login-UI` at `fa21aae`, 2026-11-04). Nothing of this plan is built yet. The user will hand out the work in other chats / to other agents, scenario by scenario, with their own feedback lists. Read this file first, then `CLAUDE.md` and `Docs/PROJECT_HANDOFF.md` section 14.22.

## The user's decisions (2026-11-04, keep them as written)
1. **Login stays exactly as it is** (finger touch, both index fingers, rig lock, focus-mode keyboard). Nothing in `LoginScreen` changes for the ray work.
2. **Controller ray input is only for Scenario 1 and Scenario 2 for now.** The client asked for it; the user personally prefers **finger-only touch**. So the ray must be an experiment that can be switched off at any time: if the client does not like it, the game goes back to finger-only touch **easily and without trouble**.
3. If the client likes the ray in SC01 and SC02, the ray will be extended to the other scenarios later. **Every time, finger touch must stay available and be the default**, so the user can return to finger-only at any moment. Never remove or rewrite the finger code to make room for the ray.
4. The first test is on SC01 and SC02 only.
5. Other work on this branch: changes from the scenario feedback (lists come from the user, per scenario) and UI adjustments, for example the **Instructions box** becomes a little smaller, or better: its size follows the text (dynamic box size).

## Rules for whoever builds the ray mode (design requirements)
- **One switch.** A single setting (for example `InputMode { FingerTouch, ControllerRay }`, default `FingerTouch`) decides the whole thing. Suggested: one static class (for example `PresentableInput`) with a build-time default constant, a PlayerPrefs / editor override, and a Dev menu entry *Chemical Safety/Presentable/Input Mode*. A second list says in which scenarios the ray is allowed (`{1, 2}` for now). Going back to finger-only = set the default to `FingerTouch` (or empty the scenario list); no other change.
- **Additive code only.** New files for the ray (pointer / interactor setup, visual line, hit handling). Do not delete or restructure `FingerPokeSystem`, `PokeTarget`, `GlovedHandPose` poke code, `RayVisualSuppressor` behaviour for the finger mode. In finger mode the game must behave byte-for-byte as before (SC01 to SC18 smoke tests prove it).
- **Same UI targets.** The ray presses the same uGUI `Button.onClick` and the same `PokeTarget.Poked` events as the fingertip, so scenario scripts do not know which input produced the click. Hover highlight, press feedback (`HoverLift`) and haptics stay shared.
- **Scope guard.** In ray mode the ray exists only in scenarios listed for it. Everywhere else (login, SC03 onwards) the finger system is active and the ray is hidden and its UI interaction off (`RayVisualSuppressor` as today).
- **Hands.** `CLAUDE.md` says: right hand points, left hand holds the chemical and the details card. With the ray: the ray comes from the right controller; the left hand keeps its holding role. The user wants this confirmed with the first SC01/SC02 test, not guessed.
- **Smoke test.** `Run Smoke Test` must end `[SMOKE] DONE runtime errors/exceptions = 0` in BOTH modes. Add a mode flag for the smoke test and check that switching the setting really restores the finger behaviour. SC13-18 and the login smoke checks must stay green.
- **Documentation.** When the ray mode is built, add a short section to `Docs/PROJECT_HANDOFF.md` (how to switch back, where the code lives) and note the result of the client decision.

## UI adjustments requested
- **Instructions box** (`InstructionPanelView` and the instruction cards): smaller by default and the box size follows its text (dynamic): short text, small box; long text, larger box up to a maximum, with the same padding rules as now (no text touching the frame, no clipping, Bangla and English both). Applies to the scenarios the user lists; check `LayoutCheck` in the smoke test for violations afterwards.
- Other UI items will come in the user's feedback lists.

## How the user will work
- The user opens new chats / agents with a scenario-wise feedback list or a plan for SC01 and SC02. Each agent: read `CLAUDE.md`, this file, the handoff section 14.22; work only on its scenario; keep the finger mode intact; verify with the smoke test; do not push without being asked.
- Quest verification is done by the user on the headset.

## Open points (to settle in the first SC01/SC02 chat)
- Ray button: trigger only, or trigger and grip? Left and right controller or right only? (Default proposal: right controller trigger.)
- Ray length / visual style, snap highlight on hover, and the physical 3D targets in SC01 (drum callouts, floor hotspot) and SC02 (container inspect): colliders must be ray-hittable.
- Whether the ray should also teleport / move the player (the user did not ask for that: no).

## Built 2026-11-04: Scenario 1 controller-ray flow (not committed, Quest check pending)
- **Switch:** `Presentable/PresentableInput.cs` (`BuildDefault` = ControllerRay on this branch, scenario list `{1}`). Back to finger-only: set `BuildDefault = InputMode.FingerTouch`, or use menu *Chemical Safety/Presentable/Input Mode*.
- **Ray:** `ControllerRayPointer.cs` (right controller, trigger, same `Button.onClick`/`HoverLift`). `FingerPokeSystem` only gets an early return while `PresentableInput.RayActive`; finger code is untouched.
- **Flow (ray only):** instruction card (`InstructionPanelView.SetCompact`: same shape, uniform scale, 1.7 m away) -> fade to `Sc01RayStand` (0,0,3.5) -> locomotion locked -> 5 s -> MCQ straight ahead, bottom edge at eye level -> correct answer: `Sc01DrumToTrolley` (trolley rolls in, the chosen hero drum lifts onto it). Hotspot, label picking and carrier button are skipped. Finger mode keeps the old flow.
- **Subtitles:** `WorldSubtitle.Dock` pins the bar under the instruction card, above the MCQ card (ray flow only).
- **Side-rack drums:** new `Mesh_RealisticDrum_v2` (28 segments, chimes, hoops, lid ring, bungs).
- Smoke: full run in ray mode = 0 errors; SC01-only run in finger mode = 0 errors.

## Built 2026-11-04: Scenario 2 controller-ray flow (not committed, Quest check pending)
- **Switch:** `PresentableInput.RayScenarios = { 1, 2 }`. Remove `2` (or set `BuildDefault = FingerTouch`) and SC02 is the old fingertip game again; the old `RunPlacement` is untouched.
- **Flow (ray only, `RunSc02RayFlow` in the director):** compact instruction card -> auto-placed at `Sc02RayStand` (40.3, 0, 1.3), locomotion locked -> `Sc02DecisionBox` "TAKE DECISION" (INSPECTION / CONTINUE) -> Inspection: ray-click a container, details card with CLOSE, repeat; when all six were seen the box returns by itself -> Continue: click a container, `LabelPickPopup` (second instance `Sc02DestPopup`, `Configure(...)`) lists the 4 compartments -> `ContainerPlacement.AutoPlace(door)` (door opens, container flies in, door closes; wrong compartment = red door + warning card, popup reopens) -> MCQ as before.
- **Ray on 3D props:** `ControllerRayPointer` also hits `PokeTarget` colliders (`PokeTarget.RayFire`); the fingertip poke is switched off while `PresentableInput.RayActive`.
- **Texts:** new keys in `Resources/Content/SC02.json` (`DecisionTitle`, `DecisionBody`, `InspectionButton`, `ContinueButton`, `PickContainerHint`, `InspectHint`, `DestTitle`, `DestTitleWrong`, `Compartments`).
- Smoke (`SC02 input mode = ControllerRay`): all six cards fit, Take Decision returns after inspection, wrong compartment shows Wrong and the popup reopens, 6/6 placed, MCQ opens. Finger mode: 6/6 placed and MCQ open as before.
- **Full smoke (ray mode, 2026-11-05):** `[SMOKE] DONE runtime errors/exceptions = 0`. The earlier SC10 stall was a harness artefact: the simulated right glove rests where the gate card's BACK button appears and pressed it by itself (SC10 looped a third time). `PresentableSmokeTest.FingerGuard` now switches the fingertips off while the SC10/11/12 gate is clicked by the test. Known and older: `LAYOUT FAIL ... 'CHECK SDS'` for SC13-18 (button 1-2 % outside its box).

- **2026-11-07:** `PresentableInput.RayScenarios` is now `{ 1, 2, 3, 4 }` (Scenario 4, the garment-factory glove scene, runs on the ray; see handoff 14.26). Remove `4` and SC04 is the finger-touch flow again.
