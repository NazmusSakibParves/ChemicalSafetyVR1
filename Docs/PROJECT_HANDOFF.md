# ChemicalSafetyVR1 — Project Handoff & Memory

**Last updated:** 2026-10-17 (see section 14 for the current Presentable state)  
**Purpose:** Take this folder to another PC / new Cursor chat and recover full context: what was built, how to run it, what was planned, and what remains.

**Project path (authoring machine):** `E:\WorkArea\VR\ChemicalSafetyVR1`  
**Also read:** [`.cursor/rules/unity-vr-assistant.mdc`](../.cursor/rules/unity-vr-assistant.mdc)

---

## 1. Project goal

Factory **chemical-safety VR training** for Meta Quest:

1. Learner approaches a **hotspot**
2. Short **scene intro** (voice / subtitles / highlight) — currently **10 seconds**
3. **MCQ** (multiple choice)
4. Correct answer **unlocks** the next scenario
5. **Next** button teleports the player to the next scenario zone; that zone’s **hotspot** starts intro → MCQ

Content source for scenarios: GIZ / NOSHTRI HTML training material (22 scenarios planned; **SC01 + SC02** implemented in demo).

---

## 2. Tech stack

| Item | Value |
|------|--------|
| Engine | Unity **6000.6.5f1** (Unity 6) |
| Template | URP Universal 3D |
| Targets | **Windows** (editor / Quest Link) + **Android** (Quest APK) |
| XR | XR Interaction Toolkit **3.6.1**, XR Management **4.7.0**, OpenXR **1.18.0** |
| Input | Input System |
| UI text | TextMeshPro |
| Editor bridge | CoplayDev **unityMCP** (Cursor ↔ Unity), HTTP typically `:8080` |
| Extra | ProBuilder installed (package); warehouse shell currently built from primitives + murals |

**Android package name:** `com.chemicalsafety.vrtraining`  
**OpenXR Android:** Meta Quest Feature should be **enabled** (`Assets/XR/Settings/OpenXR Package Settings.asset`).

---

## 2b. Two scenes (do not merge)

| Scene | Purpose | APK menu | Output |
|-------|---------|----------|--------|
| `Assets/Scenes/ChemicalSafety_Demo.unity` | **Learning demo** (SO-driven `TrainingFlowController`, keep for study) | `Chemical Safety/Build Quest APK (Learning)` | `Builds/Android/ChemicalSafetyVR.apk` |
| `Assets/Scenes/ChemicalSafety_Presentable.unity` | **Client demo** (designer art, animated, gameable) | `Chemical Safety/Build Quest APK (Presentable)` | `Builds/Android/ChemicalSafetyVR_Presentable.apk` |

Each APK contains **only** its own scene. Git tag `learning-demo-baseline` = state before Presentable work.

### Presentable scene

> The bullets below are the original (2026-10-15) description. The current flow, tools, status and TODO are in **section 14**.

- **Generated** by menu `Chemical Safety/Presentable/Build Presentable Scene` (`Editor/PresentableSceneBuilder.cs`). Re-run after changing builder code; do not hand-edit the scene (it is overwritten).
- Art references collected in `Assets/Art/Presentable/PresentableArt.asset`; generated mats/meshes in `Assets/Art/Presentable/Generated/`.
- Runtime scripts: `Assets/Scripts/ChemicalSafety/Presentable/` — `PresentableSequenceDirector` (whole flow, questions hardcoded), `PresentableMcqPanel` (step05/06/07 states), `FloorHotspot`, `DrumCallout`, `WorldSubtitle`, `InstructionPanelView`, `SdsClipboardController`, `RigZoomController`, `GlovedHandPose`, `ScreenFader`, `DeskCameraFallback`, `PresentableFx` (procedural SFX, haptics, frame-counted `Wait`).
- Flow: fade in → instruction panel + salam VO → 2 s → instruction VO (BN) → glide to drum view (callouts 1–4) → yellow-line floor hotspot → MCQ (correct = 4 "Correctly Labelled") → wrong = red popup + X retry; correct = green popup + NEXT → SC02 zone hotspot → MCQ (correct = 2) → FINISH → "Module complete".
- SC02 here uses `PresentableMcqPanel`, **not** the SO-based `TrainingFlowController`.
- Controls: Quest A/B = zoom in/out, left X = SDS clipboard show/hide, trigger on hotspot / options. Desk: W/S zoom, C clipboard, E/Space hotspot.
- Bangla text is pre-rendered PNG: `Tools/render_bangla_text.py` (Noto Bengali + Segoe UI for Latin runs). UI sprites: `Tools/generate_ui_sprites.py`.
- Hands: generic XR Hands glove mesh with static poses (no designer hand animations).

## 3. How to open & test (desk)

1. Open project in Unity 6000.6.5f1.
2. Open scene: `Assets/Scenes/ChemicalSafety_Demo.unity`
3. Press **Play**.
4. Keyboard (desk fallback):
   - **E** → start **SC01** (10s intro → MCQ)
   - Correct answer for SC01 = **D** (index 3)
   - After correct, action button becomes **Next** → SC02 intro → MCQ
   - Or after unlock, **2** → SC02 hotspot path
   - SC02 correct answer = **B** (index 1)
5. Quest headset:
   - **Quest Link / Air Link** needs a PC that meets Meta Link hardware requirements (this authoring PC was reported **incompatible** with Link).
   - Use **Android APK** build + SideQuest / `adb install` for standalone Quest 3S testing.

### Useful Unity menus

| Menu | What it does |
|------|----------------|
| `Chemical Safety/Polish Demo (Warehouse + 10s Intro + Murals)` | Rebuilds warehouse shell, murals, intro panel, world MCQ, wires intro director |
| `Chemical Safety/Fix MCQ UI Clarity (Fullscreen + Drum Labels)` | Older fullscreen Overlay MCQ fixer (superseded for Quest by world MCQ in Polish) |
| `Chemical Safety/Create SC01 Scenario Asset` | Regenerates SC01 ScriptableObject |
| `Chemical Safety/Create SC02 Scenario Asset` | Regenerates SC02 ScriptableObject |

---

## 4. Learner flow (current)

```text
Hotspot SC01 / key E
    → ScenarioIntroDirector (10s)
         greeting: "আসসালামু আলাইকুম" (session start)
         EN + BN instruction text
         drum highlight pulse
    → McqPresenter (world-space panel)
    → correct → unlock SC02, button = Next
    → Next → teleport XR Origin to PlayerSpawn_SC02 (does not open MCQ)
    → Hotspot_SC02 (or key 2)
    → ScenarioIntroDirector SC02 (10s, no full greeting by default)
    → McqPresenter SC02
    → correct → no SC03 yet (nextScenarioId cleared on SC02)
```

**Important design note:** The four drums under SC01 are **not four scenarios**. They are the **four visual options (A–D)** for one MCQ. The colored cubes are **SC02 storage compartments**, not drums.

---

## 5. What has been done (feature history)

### 5.1 Core training architecture

- ScriptableObject content: `ScenarioDefinition`, `McqQuestion`
- Progress: in-memory `ScenarioProgressService` (starts with SC01 unlocked)
- Flow: `TrainingFlowController` — hotspot → **intro** → MCQ → unlock → **Next teleports** to `PlayerSpawn_{nextId}` → next hotspot opens intro + MCQ
- Interaction: `HotspotInteractable` (XR select) + desk keys `EditorHotspotDualKeyActivator` (E / 2)
- Unlock banner: `UnlockBannerView` + `UnlockEventBridge`

### 5.2 Content assets

- `Assets/Data/Scenarios/SC01_DrumTransfer.asset` — drum label / SDS verification; next = SC02; correct = D
- `Assets/Data/Scenarios/SC02_ChemicalStorage.asset` — storage compatibility; **nextScenarioId empty** (was SC03; cleared to avoid “not found” warning)

### 5.3 Demo scene & XR

- Scene: `Assets/Scenes/ChemicalSafety_Demo.unity`
- XR Origin (XR Rig), teleport floor, Interaction Manager
- OpenXR profiles for Quest Touch / Plus (Standalone + Android work done earlier)
- Warehouse shell: `WarehouseShell` (floor, walls, safety lines, rack frame, Bangla signs)
- Reference murals from user images (see §6)
- SC01 drum rack polished; SC02 zone moved aside for clarity
- World-space **Intro_InstructionPanel** + **MCQ_WorldCanvas** with `FaceCameraBillboard`
- Intro VO UI shows Bangla greeting + bilingual instructions (TTS not yet configured)

### 5.4 UI / UX iterations (resolved issues)

| Problem | Resolution |
|---------|------------|
| World MCQ overlapping drums | Moved to clearer panels; intro hides murals/labels while open |
| Drum labels tilted / crushed | Labels as siblings of thin boards with scale compensation |
| Title still visible during question | `McqPresenter.hideWhileOpen` |
| Close vs Next | Correct → label **Next**; Next teleports to next zone (hotspot starts that MCQ) |
| Key 2 still works after unlock | Unchanged dual-key activator |
| SC03 not found warning | Cleared SC02 `nextScenarioId` |
| Greybox only | Warehouse shell + user reference murals + polish builder |

### 5.5 Quest / APK prep (partial)

- Android IL2CPP + ARM64 (project settings)
- Package id set to `com.chemicalsafety.vrtraining`
- Meta Quest OpenXR feature enabled in settings asset
- **APK built:** `Builds/Android/ChemicalSafetyVR.apk` (development build, ~872 MB, 2026-10-14)
- Rebuild anytime: Unity menu **Chemical Safety → Build Quest APK**

### 5.6 What was attempted but blocked

| Item | Status |
|------|--------|
| Meta Quest **Link / Air Link** on authoring PC | PC hardware flagged incompatible by Meta Horizon Link app |
| AI 3D models (Tripo / Meshy / fal) | Providers listed but **not configured** (no API keys in Unity MCP secure store) |
| Bangla TTS audio (`generate_audio` / fal) | Provider **not configured** — subtitles only for now |

---

## 6. User reference art (in repo)

Copied into project so they travel with the project:

| File | Role |
|------|------|
| `Assets/Art/Reference/Ref_WarehouseAisle.jpg` | Wall mural / warehouse look |
| `Assets/Art/Reference/Ref_InstructionsPanel.jpg` | Intro UI strip / style reference |
| `Assets/Art/Reference/Ref_FourDrums.jpg` | Four-label drum cases mural / SC01 visual reference |

These are **2D references / murals**, not walkable 3D rooms. Geometry is built in-scene; JPGs decorate and guide look.

---

## 7. Key scripts (map)

### Runtime — `Assets/Scripts/ChemicalSafety/`

| Path | Role |
|------|------|
| `Data/ScenarioDefinition.cs` | Scenario SO |
| `Data/McqQuestion.cs` | Question data |
| `Progress/ScenarioProgressService.cs` | Unlock state |
| `Flow/TrainingFlowController.cs` | Orchestration + intro hook |
| `Flow/ScenarioIntroDirector.cs` | 10s intro beat, greeting, highlights |
| `Flow/PulseHighlight.cs` | Soft scale pulse on drums |
| `Flow/UnlockEventBridge.cs` | UnityEvent bridge for unlock UI |
| `UI/McqPresenter.cs` | MCQ panel, Close/Next, hideWhileOpen |
| `UI/IntroInstructionPresenter.cs` | Intro card EN/BN + countdown + optional AudioSource |
| `UI/FaceCameraBillboard.cs` | Face main/XR camera |
| `UI/UnlockBannerView.cs` | “Unlocked next” banner |
| `Interaction/HotspotInteractable.cs` | XR hotspot |
| `Interaction/EditorHotspotDualKeyActivator.cs` | E / 2 desk keys |

### Editor — `Assets/Scripts/ChemicalSafety/Editor/`

| Path | Role |
|------|------|
| `DemoPolishBuilder.cs` | Main polish rebuild menu |
| `McqUiClarityFixer.cs` | Legacy fullscreen Overlay MCQ |
| `QuestVrDemoUpgrader.cs` | XR Origin / Quest profile upgrades |
| `ChemicalSafetyDemoSceneBuilder.cs` | Original demo scene builder |
| `Sc01ContentFactory.cs` / `Sc02ContentFactory.cs` | Scenario asset writers |

---

## 8. Plans agreed with stakeholder (product)

Documented decisions from planning chats:

1. **Demo is greybox → polish**, then factory-quality art later; not final photoreal.
2. Use **user’s 3 reference images** wherever usable (murals/UI); do not require buying Asset Store packs for the first presentable pass.
3. Prefer **AI 3D models** when API keys are available; otherwise polished primitives (current).
4. Flow: **10s scene intro → question**; after correct **Next** teleports to SC02 zone; **Hotspot_SC02** starts **10s → question 2**.
5. Session start VO/subtitle begins with **"আসসালামু আলাইকুম"**.
6. Ship path for this PC: **Android APK to Quest 3S** (Link blocked by GPU/CPU).
7. SC01–SC22 reusable architecture: new scenarios = new ScriptableObjects + hotspots; avoid hardcoding each in flow code.

Cursor plan files (may live outside repo on the authoring machine):

- `next_button_ui_polish_*.plan.md` — Next button + labels (implemented)
- `reference_polish_demo_flow_*.plan.md` — warehouse + 10s intro + murals + AI optional (mostly implemented; AI/TTS pending keys)

---

## 9. Future work (backlog)

### P0 — before boss / Quest demo

- [ ] Build & install **Android APK** on Quest 3S; verify teleport, ray UI, intro, Next
- [ ] Confirm world-space MCQ readable in headset (overlay is weak on Quest)
- [ ] Replace or supplement murals with **real 3D props** when AI keys or art packs available
- [x] Bangla TMP font (`NotoSansBengali SDF`) + app-start welcome VO (`WelcomeVoicePlayer` / `VO_AssalamuAlaikum_bn.mp3`)
- [ ] Additional scenario Bangla VO clips beyond welcome salam
- [ ] Smoke-test full path: SC01 intro → D → Next (teleport SC02) → Hotspot_SC02 → intro → B

### P1 — content & pedagogy

- [ ] Author **SC03–SC22** from GIZ HTML (one SO + hotspot + intro text each)
- [ ] Persist progress (PlayerPrefs / save file) if multi-session training required
- [ ] Wrong-answer coaching VO in Bangla
- [ ] Skip intro only in Editor (optional debug)

### P2 — production look

- [ ] Factory environment art pass (real shelves, lighting, PBR materials)
- [ ] Proper GHS pictogram textures on drums
- [ ] SDS clipboard interactable prop matching reference
- [ ] Performance pass for Quest (batching, LODs, no overdraw on murals)

### P3 — platform / ops

- [ ] Meta store / App Lab packaging checklist
- [ ] CI or build script for APK
- [ ] Second PC with Link-capable GPU for iterative Play Mode XR

---

## 10. New machine checklist

When copying this project to another PC:

1. Install **Unity 6000.6.5f1** with **Windows + Android** modules, Android SDK/NDK/JDK as Unity Hub suggests.
2. Copy the whole project folder (or git clone). Prefer **excluding** `Library/` and regenerating (faster/cleaner), or copy Library if you need identical cache.
3. Open in Unity; let packages restore (`Packages/manifest.json`).
4. Install/enable **MCP for Unity** if using Cursor bridge; Start Server; configure Cursor client.
5. Open `ChemicalSafety_Demo`; if scene looks empty/broken, run menu **Chemical Safety/Polish Demo (...)**.
6. Desk test: Play → E → D → Next.
7. For Quest: Switch platform Android → Build APK → SideQuest install → Unknown Sources library.
8. Read **this file** again before changing architecture.

### Do not lose

- `Assets/Data/Scenarios/*.asset`
- `Assets/Art/Reference/*`
- `Assets/Scripts/ChemicalSafety/**`
- `Assets/Scenes/ChemicalSafety_Demo.unity`
- `Docs/PROJECT_HANDOFF.md` (this document)
- `.cursor/rules/unity-vr-assistant.mdc`

---

## 11. Known issues / gotchas

- **EventSystem:** only one UI input module should be active; XRUIInputModule preferred for Quest rays.
- **Play Mode + editor menus:** polish/fix menus must run **outside** Play Mode (`OpenScene` fails in play).
- **Duplicate LabelBoards:** older fixers could stack boards; Polish builder cleans LabelBoard children.
- **Intro panel inactive at edit time:** `Intro_InstructionPanel` starts disabled; `GameObject.Find` won’t see it — use inactive-inclusive search.
- **Link incompatible PC:** do not assume Air Link works; use APK.
- **AI / fal keys:** configure in Unity MCP secure store before expecting `generate_model` / `generate_audio` to work.
- **Open issue (2026-10-15):** the learning scene shows static white speckle noise in the Game View while the editor is on the Android target (Mobile_RPAsset). The Presentable scene renders cleanly. Not investigated yet, and not checked on the headset.
- **Never edit scripts during Play.** A domain reload kills coroutines and the Presentable director looks "stuck". EditorPrefs `ScriptCompilationDuringPlay = 1` (recompile after play) is set on the authoring PC; set it on new machines (Preferences → General → Script Changes While Playing).
- **Play-mode logs** don't show in MCP `read_console`; read `Logs/Editor.log`.
- **XR Origin has a CharacterController** — call `Physics.SyncTransforms()` after moving the rig directly.
- **GLB roots carry scale** (~0.01) — multiply, don't overwrite `localScale`.
- **`UI_RoundedPanel` is a 23 %-alpha frame**; use `UI_RoundedSolid` for any fill that must be opaque.

---

## 12. Quick command cheat sheet

```text
Desk demo:
  Play → E → wait 10s → answer D → Next → wait 10s → answer B

Rebuild polish:
  Unity menu → Chemical Safety → Polish Demo (Warehouse + 10s Intro + Murals)

Quest APK (manual):
  File → Build Settings → Android → Switch Platform
  Player: package com.chemicalsafety.vrtraining, ARM64, IL2CPP
  XR: OpenXR + Meta Quest Feature
  Build APK → adb install -r Your.apk
```

---

## 13. Changelog (high level)

| When | Change |
|------|--------|
| Early | Project + MCP + XR packages + SC01 desk demo |
| Mid | Full XR Origin, teleport, SC02, Quest profiles, dual keys |
| Mid | Fullscreen Overlay MCQ, drum labels, hide plaque |
| Mid | Close→Next + key 2 dual path |
| 2026-10-14 | 10s intro director, Bangla greeting, warehouse shell, user murals, world MCQ, SC03 dead-end cleared, Quest package/feature prep |
| 2026-10-14 | This handoff document added under `Docs/` |
| 2026-10-15 | Separate `ChemicalSafety_Presentable` client scene (designer art, VO, hotspot, step05/06/07 MCQ, SC02, gloves, SDS clipboard, zoom) + two APK menus |
| 2026-10-16/17 | Presentable: designer art, poke flow, Instructions panel, warehouse polish (committed `52e6d71`); then set 4 from Quest test (sharper render pipeline, finger-only UI, floating Touch buttons, solid props, drum carrier, SDS follow, realistic racks), verified + APK built 2026-10-17 - see section 14 |

---

*If you update the demo significantly, append a row to §13 and refresh §5 / §9 so the next machine stays accurate.*


---

## 14. Presentable demo — full state & next steps (updated 2026-10-17)

> Read this section first if you are a new agent / Cursor session. It supersedes the older Presentable bullets in §2b.
> Rules that still apply: **change only the Presentable client demo** (never `ChemicalSafety_Demo` / the Learning APK), generate the scene with the builder menu (never hand-edit the `.unity`), never edit scripts while in Play mode, author is the user (no Claude co-author trailers), do **not** merge to `master` unless the user says so. Branch: `feature/Modify-Scenario1-as-per-feedback` (set 4 committed on top of `52e6d71`).

### 14.1 How to work
- Regenerate the scene: Unity menu **Chemical Safety/Presentable/Build Presentable Scene** (`Editor/PresentableSceneBuilder.cs`). Runtime scripts: `Assets/Scripts/ChemicalSafety/Presentable/`.
- **Smoke test (editor only)**: menu **Chemical Safety/Presentable/Run Smoke Test** (`PresentableSmokeTest.cs`). It plays the whole flow on the desk (invokes the same events as finger touches), logs `[SMOKE] ...` lines to `Logs/Editor.log` and saves screenshots to `Temp/Smoke/`. Look for `[SMOKE] DONE runtime errors/exceptions = 0`.
- APK menus (`Editor/ChemicalSafetyApkBuilder.cs`): **Build Quest APK (Presentable)** = *release* build using `Assets/Settings/Presentable_RPAsset.asset` (MSAA 4x, render scale 1.0, HDR off, no real-time shadows) applied only for that build and restored afterwards; **(Presentable, Debug)** = development build; the Learning menu is unchanged. Output `Builds/Android/ChemicalSafetyVR_Presentable.apk` (gitignored). A harmless `NullReferenceException` from `MetaQuestFeature.cs` (OpenXR validation) appears in every build, build still succeeds.
- Unity MCP: if your tool list has no `unityMCP`, the server still answers plain HTTP: `python Tools/mcp_http.py <tool> '<json args>'` (e.g. `refresh_unity`, `read_console`, `execute_menu_item`). Domain reloads drop the MCP socket - wait, then retry. If Unity shows "License error: Token Exchange failed" open Unity Hub, sign out/in and check Licenses (network/VPN/clock).
- Python tools (need `pip install numpy pillow trimesh fast-simplification`; `edge-tts` for voices): `Tools/render_bangla_text.py` (Bangla text -> PNG, TMP cannot shape Bangla), `generate_env_art.py` (signs, stains, light shaft), `generate_floor.py` (baked 2048x4096 floor), `generate_props_art.py` (touch icon, rack post texture, drum detail), `decimate_shelf.py` (rack mesh), `generate_ui_sprites.py` (older UI sprites).
- Voice clips (edge-tts, `bn-BD-NabanitaNeural`) in `Assets/Art/Audio/VO/`: `VO_AssalamuAlaikum`, `VO_SC01_Instruction`, `VO_Correct`, `VO_Wrong`, `VO_PickLabel`, `VO_WrongLabel`, `VO_ContinueHint`, `VO_TouchDrum`, `VO_Carrier` (all `_bn.mp3`). `PresentableArt` slots are only overwritten when the file exists.

### 14.2 Flow (current)
Fade in -> Instructions panel (salam VO, then instruction VO; **speaker button replays**, **Continue** appears after the VO + hint VO) -> glide to drum view -> hotspot `Hotspot_SC01` (0,0,4.5) plays the pick-label VO once -> glide to `DrumPokeStand` -> **floating "Touch" buttons** in front of each drum; touching one opens the 4-name popup (wrong = popup turns red + wrong-label VO, right = dark name pill above the drum, any order, finished drums are done) -> after 4 correct the **drum carrier** (`DrumCarrier.glb`) appears with an instruction card (BN+EN + VO); touching its Touch button rolls it forward and opens MCQ Q1 -> correct -> NEXT -> SC02 zone -> hotspot -> Q2 -> finish. MCQ/popups/Instructions share the cyan "pop up screen.png" frame (`FrameScale` enlarges it so text is not clipped). Correct/wrong VO now starts ~0.25 s *after* the popup is on screen (`PresentableMcqPanel.PopupShown`). Zoom (A/B) and SDS (left X, desk C) keep working.

### 14.3 What is DONE and verified (desk smoke test, 0 runtime errors, commit `52e6d71` pushed)
Explore subtitle + SdsHint removed; designer 2D art in `Art/Presentable/2D/Designer/` used everywhere (red variants of frames generated); hotspot moved to (0,0,4.5); drum label popup flow with correct map (1 Faded, 2 No label, 3 Mismatched GHS vs SDS, 4 Correctly labelled) and name pills; Instructions panel redesign (speaker + Continue + hint VO); MCQ panel rebuilt after step05/06/07; ceiling fans (4, large, spinning), lamps, sky-light shafts, signs, stains, wall extinguishers; side racks (2 runs) with drums + GHS stickers; baked floor; SDS clipboard upright; frame enlarge so MCQ header is not clipped. Stats at that commit: ~325k tris, ~148 draw calls (editor Game view).

### 14.4 Set 4 (from the Quest test) - tested and committed
Source of this work: the user's Quest screen recordings (they look different from in-headset: recorder A/V drift; in headset timing was fine). Scene rebuilt, smoke test passes (`[SMOKE] DONE runtime errors/exceptions = 0`, ~161k tris, ~168 draw calls), Presentable APK built 2026-10-17 (101 MB). Fixes added during verification: fan speed 240 deg/s; `RayVisualSuppressor` also disables XRI 3.x `CurveVisualController` (it re-enabled the Near-Far `LineRenderer` every frame, the visible white ray); no subtitle during MCQ correct/wrong cards; drum name plates hidden before Q1; carrier card lowered/shrunk and the rig turns to face the carrier behind a short fade.
1. **Clarity/smoothness** (root cause of "shaky, not crystal clear"): `Mobile_RPAsset` had MSAA off + render scale 0.8 + HDR on. New `Presentable_RPAsset.asset` (MSAA 4x, scale 1.0, HDR off, soft shadows off), swapped in only by the Presentable APK menu; OpenXR **Foveated Rendering** feature enabled (`OpenXR Package Settings.asset`); `QuestRenderTuning` sets foveation level 0.5 at start; Presentable build is now a release build; sun real-time shadows off; `RayVisualSuppressor` no longer polls `FindObjectsByType` every frame.
2. **Subtitle behind drums**: `WorldSubtitle` now 0.9 m away, smaller, `PresentableUi.AlwaysOnTop(canvas)` (UI Z-test Always) - also used for the carrier card and Touch buttons. `ScreenFader.SetExtra` + `HeadSolidGuard` fade the view if the head enters a solid prop.
3. **Finger poke only (no ray)**: `RayVisualSuppressor` now also sets `enableUIInteraction=false` on `NearFarInteractor` and `XRPokeInteractor`; new `FingerPokeSystem` presses uGUI buttons with the right fingertip (hover cursor, depth press, curl + haptic) on canvases created with `interactive:true` (registered in `PresentableUi.WorldCanvas`). `HoverLift.SetHover` added.
4. **Drum selection UX**: new `FloatingTouchButton` (ring + finger icon + pulse, `ICON_Touch.png`) floating 0.45 m in front of each drum at 1.12 m; hover glows the drum; `DrumPokeStand` moved to z=4.8; popup placed 0.62 m in front of the head; subtitle "touch the glowing button" (+`BN_TouchDrum.png`, `VO_TouchDrum_bn.mp3`).
5. **Solid props**: layer `SolidProps` (created by the builder via TagManager) with box/capsule colliders on hero drums/uprights/pallet, side-rack runs, floor pallets, carrier; `GlovedHandPose.LateUpdate` pushes the *rendered* glove out of solid colliders (fingertip stops at the surface); `PokeTarget` / buttons use the clamped `TipPosition`.
6. **Drum carrier**: `Art/Presentable/3D/DrumCarrier.glb` (user file `drum-carrier.glb`, 39k tris, 37 MB textures: set texture max size in Unity import if the APK gets big) replaces the primitive trolley; `BuildCarrier()` adds card + Touch button + a loaded-drum child; director `RunCarrier()` plays `VO_Carrier_bn.mp3`, rolls the carrier forward with the drum, then opens Q1. Scale/orientation of the glb is **unverified** (placed at (1.95,0,5.7), yaw 180, base snapped to the floor).
7. **SDS board**: `SdsClipboardController` now follows the left palm position but takes orientation from the head (upright, 12 deg back, facing the user, small sway, raise/lower animation) so wrist tilt cannot lay it over the fist. `forceHandMode` / `ForceHandMode()` is for editor tests.
8. **Side racks/drums**: posts every 1.3 m (`RACK_Upright.png`) + rear cross braces + orange beams from `ShelfLite_Beams.obj`; `RealisticDrumMesh` (hoops, rims, recessed lid) with `DRUM_Detail.png`, random yaw/tilt/colour (blue/red/yellow/grey), stickers. Hero rack + 4 hero drums untouched visually.

### 14.5 Next
1. Next feedback round + new SC02 storage room (plan: SC01 carrier text/VO, realistic loaded trolley, scenario-complete VO after Q1; new SC02 room from the designer pack with glass 4-door compartment, wall compatibility chart, new SDS, MCQ 2.1).
2. Draw calls are slightly above the ~160 target (168); trim if the new room adds more.
3. Known risks to watch on device: finger-poke thresholds (`FingerPokeSystem` hover 9 cm / press 1.8 cm, `FloatingTouchButton` press radius 7.5 cm), glove clamp jitter, carrier model orientation, VO timing vs popups, foveation level (0.5) vs sharpness, hand pose of the left hand.

### 14.6 Gotchas learned
- In the editor **without a headset the XR controllers are deactivated** (`Left/Right Controller` inactive) so gloves never spawn; tests must `SetActive(true)` and disable `TrackedPoseDriver` (it overwrites the transform before render).
- Quest screen recordings show audio slightly early and flatten near objects - judge timing in the headset, not the recording.
- `FindObjectsByType(FindObjectsInactive)` single-argument overload does not exist in Unity 6; use the two-argument form. `UnityStats` has no `batches` (use `drawCalls`).
- Shell/tooling on this PC: Python 3.11 with Pillow/numpy installed; `sleep`-chained commands are blocked by the harness - poll with until-loops.
- The shelf glb has ~88k tris per rack; never instantiate it many times (side racks use procedural posts + decimated beams for that reason).
- The `.utmp/`, `ProjectSettings/*` and `Assets/Settings/*` diffs shown by git are Unity/Android build noise - do not commit them (only `Presentable_RPAsset.asset` and the OpenXR foveation flag are intentional).
