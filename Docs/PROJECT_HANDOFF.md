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
| 2026-10-16/17 | Presentable: designer art, poke flow, Instructions panel, warehouse polish (committed `52e6d71`); then set 4 from Quest test, verified + APK built 2026-10-17, pushed as `117226f` - see section 14 |
| 2026-10-18 | Presentable only, uncommitted: SC01 carrier text/VO, scenario-complete VO, new SC02 storage room (glass doors, wall chart, new SDS, MCQ 2.1). Container-in-hand inspect is NOT built yet - see §14.7 |

---

*If you update the demo significantly, append a row to §13 and refresh §5 / §9 so the next machine stays accurate.*


---

## 14. Presentable demo — full state & next steps (updated 2026-10-18)

> Read this section first if you are a new agent / Cursor session. It supersedes the older Presentable bullets in §2b.
> Rules that still apply: **change only the Presentable client demo** (never `ChemicalSafety_Demo` / the Learning APK), generate the scene with the builder menu (never hand-edit the `.unity`), never edit scripts while in Play mode, author is the user (no Claude / Cursor co-author trailers), do **not** merge to `master` unless the user says so. Branch: `feature/Modify-Scenario1-as-per-feedback`. Pushed through `117226f`. The 2026-10-18 SC02 work below is **uncommitted**. Do not build an APK unless the user asks.

### 14.1 How to work
- Regenerate the scene: Unity menu **Chemical Safety/Presentable/Build Presentable Scene** (`Editor/PresentableSceneBuilder.cs`). Runtime scripts: `Assets/Scripts/ChemicalSafety/Presentable/`.
- **Smoke test (editor only)**: menu **Chemical Safety/Presentable/Run Smoke Test** (`PresentableSmokeTest.cs`). It plays the whole flow on the desk (invokes the same events as finger touches), logs `[SMOKE] ...` lines to `Logs/Editor.log` and saves screenshots to `Temp/Smoke/`. Look for `[SMOKE] DONE runtime errors/exceptions = 0`.
- APK menus (`Editor/ChemicalSafetyApkBuilder.cs`): **Build Quest APK (Presentable)** = *release* build using `Assets/Settings/Presentable_RPAsset.asset` (MSAA 4x, render scale 1.0, HDR off, no real-time shadows) applied only for that build and restored afterwards; **(Presentable, Debug)** = development build; the Learning menu is unchanged. Output `Builds/Android/ChemicalSafetyVR_Presentable.apk` (gitignored). A harmless `NullReferenceException` from `MetaQuestFeature.cs` (OpenXR validation) appears in every build, build still succeeds.
- Unity MCP: if your tool list has no `unityMCP`, the server still answers plain HTTP: `python Tools/mcp_http.py <tool> '<json args>'` (e.g. `refresh_unity`, `read_console`, `execute_menu_item`). Domain reloads drop the MCP socket - wait, then retry. If Unity shows "License error: Token Exchange failed" open Unity Hub, sign out/in and check Licenses (network/VPN/clock).
- Python tools (need `pip install numpy pillow trimesh fast-simplification`; `edge-tts` for voices): `Tools/render_bangla_text.py` (Bangla text -> PNG, TMP cannot shape Bangla), `generate_env_art.py`, `generate_floor.py`, `generate_props_art.py`, `decimate_shelf.py`, `generate_ui_sprites.py`, `prepare_sc02_models.py` (shrinks the 147 MB container GLB to `Assets/Art/Presentable/3D/Sc02Containers.glb`, about 5.6 MB — never import the original), `generate_sc02_sds.py` (SC02 clipboard sheet).
- Voice clips in `Assets/Art/Audio/VO/`. Bangla uses edge-tts `bn-BD-NabanitaNeural`: `VO_AssalamuAlaikum`, `VO_SC01_Instruction`, `VO_Correct`, `VO_Wrong`, `VO_PickLabel`, `VO_WrongLabel`, `VO_ContinueHint`, `VO_TouchDrum`, `VO_Carrier`, `VO_Sc01Complete_bn`, `VO_SC02_Instruction`. English scenario-complete line is `VO_Sc01Complete_en` (`en-IN-NeerjaNeural`). `PresentableArt` slots are only overwritten when the file exists.

### 14.2 Flow (current, 2026-10-18)
Warehouse (`Environment_SC01`): fade in -> Instructions (salam VO, then instruction VO; speaker replays; Continue after the VO + hint VO) -> glide to drum view -> hotspot `Hotspot_SC01` (0,0,4.5) -> glide to `DrumPokeStand` -> floating Touch buttons; wrong label turns the popup red + wrong-label VO, right label puts the name plate on the drum -> after 4 correct the drum carrier appears. Card and VO: "সঠিক ড্রামটি ট্রান্সফারের জন্য ট্রলিতে তুলুন" / "Load the correct drum onto the trolley for transfer". Touch lifts the drum onto the toe plate (leans back) and rolls the trolley, then MCQ 1. A correct answer plays the existing correct VO, then 1 s later `VO_Sc01Complete_bn` and `VO_Sc01Complete_en`, with **no subtitle**. Next hides the warehouse and shows `Environment_SC02` (about 40 m to the side, so only one room draws).

SC02 room: instruction panel with `VO_SC02_Instruction_bn` (speaker replays, Continue; no extra subtitle on this card) -> explore. Left X / desk C shows the new SDS (`SDS_SC02.png`) via `SdsClipboardController.SetSheet`. Poking the wall chart opens `ChartViewer` (large chart + Close). Four glass doors (Flammables, Oxidizers, Acids, General Storage) swing open/shut on a finger poke (`CompartmentDoor`). Floor hotspot -> MCQ 2.1, answer B, FINISH. Containers on the pallet are **not** pokeable yet. `FlashWrong` / `FlashCorrect` exist and are not used. MCQ correct/wrong VO still starts ~0.25 s after the popup. Zoom (A/B) and SDS (left X, desk C) keep working.

### 14.3 What is DONE and verified (desk smoke test, 0 runtime errors, commit `52e6d71` pushed)
Explore subtitle + SdsHint removed; designer 2D art in `Art/Presentable/2D/Designer/` used everywhere (red variants of frames generated); hotspot moved to (0,0,4.5); drum label popup flow with correct map (1 Faded, 2 No label, 3 Mismatched GHS vs SDS, 4 Correctly labelled) and name pills; Instructions panel redesign (speaker + Continue + hint VO); MCQ panel rebuilt after step05/06/07; ceiling fans (4, large, spinning), lamps, sky-light shafts, signs, stains, wall extinguishers; side racks (2 runs) with drums + GHS stickers; baked floor; SDS clipboard upright; frame enlarge so MCQ header is not clipped. Stats at that commit: ~325k tris, ~148 draw calls (editor Game view).

### 14.4 Set 4 (from the Quest test) - tested and committed
Source of this work: the user's Quest screen recordings (they look different from in-headset: recorder A/V drift; in headset timing was fine). Scene rebuilt, smoke test passes (`[SMOKE] DONE runtime errors/exceptions = 0`, ~161k tris, ~168 draw calls), Presentable APK built 2026-10-17 (101 MB). Fixes added during verification: fan speed 240 deg/s; `RayVisualSuppressor` also disables XRI 3.x `CurveVisualController` (it re-enabled the Near-Far `LineRenderer` every frame, the visible white ray); no subtitle during MCQ correct/wrong cards; drum name plates hidden before Q1; carrier card lowered/shrunk and the rig turns to face the carrier behind a short fade.
1. **Clarity/smoothness** (root cause of "shaky, not crystal clear"): `Mobile_RPAsset` had MSAA off + render scale 0.8 + HDR on. New `Presentable_RPAsset.asset` (MSAA 4x, scale 1.0, HDR off, soft shadows off), swapped in only by the Presentable APK menu; OpenXR **Foveated Rendering** feature enabled (`OpenXR Package Settings.asset`); `QuestRenderTuning` sets foveation level 0.5 at start; Presentable build is now a release build; sun real-time shadows off; `RayVisualSuppressor` no longer polls `FindObjectsByType` every frame.
2. **Subtitle behind drums**: `WorldSubtitle` now 0.9 m away, smaller, `PresentableUi.AlwaysOnTop(canvas)` (UI Z-test Always) - also used for the carrier card and Touch buttons. `ScreenFader.SetExtra` + `HeadSolidGuard` fade the view if the head enters a solid prop.
3. **Finger poke only (no ray)**: `RayVisualSuppressor` now also sets `enableUIInteraction=false` on `NearFarInteractor` and `XRPokeInteractor`; new `FingerPokeSystem` presses uGUI buttons with the right fingertip (hover cursor, depth press, curl + haptic) on canvases created with `interactive:true` (registered in `PresentableUi.WorldCanvas`). `HoverLift.SetHover` added.
4. **Drum selection UX**: new `FloatingTouchButton` (ring + finger icon + pulse, `ICON_Touch.png`) floating 0.45 m in front of each drum at 1.12 m; hover glows the drum; `DrumPokeStand` moved to z=4.8; popup placed 0.62 m in front of the head; subtitle "touch the glowing button" (+`BN_TouchDrum.png`, `VO_TouchDrum_bn.mp3`).
5. **Solid props**: layer `SolidProps` (created by the builder via TagManager) with box/capsule colliders on hero drums/uprights/pallet, side-rack runs, floor pallets, carrier; `GlovedHandPose.LateUpdate` pushes the *rendered* glove out of solid colliders (fingertip stops at the surface); `PokeTarget` / buttons use the clamped `TipPosition`.
6. **Drum carrier**: `Art/Presentable/3D/DrumCarrier.glb` (user file `drum-carrier.glb`, 39k tris, 37 MB textures: set texture max size in Unity import if the APK gets big) replaces the primitive trolley; `BuildCarrier()` adds card + Touch button + a loaded-drum child. Card text, VO, and the loaded-drum pose were changed on 2026-10-18 — follow §14.5, not this paragraph, for those three. The carrier itself stays at (1.95, 0, 5.7), yaw 180, base on the floor. `TrolleyStand` is (0.7, 0, 4.15).
7. **SDS board**: `SdsClipboardController` now follows the left palm position but takes orientation from the head (upright, 12 deg back, facing the user, small sway, raise/lower animation) so wrist tilt cannot lay it over the fist. `forceHandMode` / `ForceHandMode()` is for editor tests.
8. **Side racks/drums**: posts every 1.3 m (`RACK_Upright.png`) + rear cross braces + orange beams from `ShelfLite_Beams.obj`; `RealisticDrumMesh` (hoops, rims, recessed lid) with `DRUM_Detail.png`, random yaw/tilt/colour (blue/red/yellow/grey), stickers. Hero rack + 4 hero drums untouched visually.

### 14.5 SC01 feedback + new SC02 room (2026-10-18, smoke test 0 errors, not committed)
Reference pack (do not copy the 147 MB GLB into Assets): `C:\Users\msi\Downloads\2D-20261018T075637Z-1-001\2D\Scene 02 Workflow\` (`BG.jpg`, `step001.jpg`, `step02.jpg`, `step03.jpg`, `step04.jpg`, `step05.jpg`, `step06.jpg`).

Done in this uncommitted work:
- SC01 carrier text + `VO_Carrier_bn` replaced. Loaded drum tweens onto the trolley and leans back. From the desk camera at `TrolleyStand` the handle still hides part of the drum; that view is not the reference photo yet.
- After correct MCQ 1 only: existing correct VO, 1 s gap, `VO_Sc01Complete_bn`, `VO_Sc01Complete_en`, no subtitle. Next stops the voice.
- Old `BuildSc02Zone` removed. `Environment_SC02` is inactive until that Next. Room has a simple eye-wash basin, three wall exhaust fans, an extinguisher, fluorescent tubes, and the pallet.
- `Sc02Containers.glb` (5.6 MB) is the pallet plus the six containers. `Sc02Compartment.glb` body mesh is hidden; glass doors, frames, and interior drums are built in `BuildGlassCabinet`. Door order facing the player: Flammables, Oxidizers, Acids, General Storage.
- `ChartViewer` enlarges `Assets/Art/Presentable/2D/Sc02/Chart.png`. Do not set the canvas `localScale` to 1; it must stay the world-canvas scale or the camera ends up inside a giant quad.
- New SDS sheet, SC02 instruction Bangla images (`BN_Sc02Instruction`, `BN_Sc02InstructionPanel`), and `InstructionPanelView.SetContent`.
- Smoke test covers the new stages (`sc02Intro`, `sc02Explore`, door poke, chart open, MCQ 2.1). Last run: `[SMOKE] DONE runtime errors/exceptions = 0`. SC02 about 140k tris and 90 draw calls. Warehouse still about 168 draw calls (target ~160).
- Presentable APK on disk (`Builds/Android/ChemicalSafetyVR_Presentable.apk`, 101 MB, 2026-10-17 21:48) does **not** include this SC02 work.

Not done:
- Step02 inspect: poke a container, it comes to the hand, details card beside it, chart box under the card opens the large chart like step03. See §14.7.
- Putting a container into a door (step05 correct / step06 wrong). `CompartmentDoor.FlashWrong` and `FlashCorrect` stay unwired until the user specifies that step.
- Rebuilding the APK.

Known risks on device: finger-poke thresholds (`FingerPokeSystem` hover 9 cm / press 1.8 cm, `FloatingTouchButton` press radius 7.5 cm), glove clamp jitter, carrier drum pose from the stand, VO timing vs popups, foveation level 0.5 vs sharpness, left-hand SDS pose.

### 14.6 Gotchas learned
- In the editor **without a headset the XR controllers are deactivated** (`Left/Right Controller` inactive) so gloves never spawn; tests must `SetActive(true)` and disable `TrackedPoseDriver` (it overwrites the transform before render).
- Quest screen recordings show audio slightly early and flatten near objects - judge timing in the headset, not the recording.
- `FindObjectsByType(FindObjectsInactive)` single-argument overload does not exist in Unity 6; use the two-argument form. `UnityStats` has no `batches` (use `drawCalls`).
- Shell/tooling on this PC: Python 3.11 with Pillow/numpy installed; `sleep`-chained commands are blocked by the harness - poll with until-loops.
- The shelf glb has ~88k tris per rack; never instantiate it many times (side racks use procedural posts + decimated beams for that reason).
- The `.utmp/`, `ProjectSettings/*` and `Assets/Settings/*` diffs shown by git are Unity/Android build noise - do not commit them (only `Presentable_RPAsset.asset` and the OpenXR foveation flag are intentional).
- This shell rewrites a `git commit` command and appends `Co-authored-by: Cursor`. To keep the author as NazmusSakibParves with no trailer, create the commit with `git commit-tree` via the full path `C:\Program Files\Git\cmd\git.exe`, then `git reset --soft` to that hash. Never `git commit --amend` a pushed commit.
- `ChartViewer` fade must scale from the canvas `localScale` captured at build. Setting `localScale = Vector3.one` makes a ~1500 m panel and the screenshot is one texel.

### 14.8 Update 2026-10-19 (done, desk smoke test 0 errors, Presentable APK built 2026-10-19 01:48, 136 MB)
- **Container inspect done** (`Presentable/ContainerInspect.cs`): six pallet products pokeable, held in the left hand (desk: fixed pose), details card with auto-fit text, chart box opens `ChartViewer`. Mapping by label: `Object_2` drum, `Tank.001` oxidizer can, `Tank.002` acid bottle, `Tank.003` green cleaner canister, `Tank.004` reactive jar, `Retopo_Cube.006` spray. Labels face +x in the glb; held rotation uses the item's own label direction.
- **SC02 room rebuilt after BG.jpg** (`BuildSc02Room`): viewer-left is +x (rig faces -z). Left wall door/vent/extinguisher, right wall fans/hand sign/eye-wash, chart on the back wall, glass doors with big GHS icon + label decals and stocked compartments, pallet across the view, textures from `Tools/generate_sc02_room_art.py`. About 164 draw calls.
- **SC01 UI**: finger icon replaced by the `ICON_Arrow.png` double chevron, centred over each drum in the gap between the lid (1.13 m) and the shelf beam (1.45 m); touch point is 0.4 m toward the user (`touchOffset`). `DrumPokeStand` z 5.1. Label popup bigger, spaced, always on top, kept in front of the rack. `Stand Here` badge (`UI_StandHere.png`). Trolley recoloured orange/gold with black grips (handle split in `ColorCarrier`), loaded drum is the real drum model with the Isopropanol label.
- Still not done: placing a container into a door (`FlashWrong`/`FlashCorrect` unwired); on-device check of arrow reach and trolley handle colour.
- Smoke test can drop with `XR_ERROR_FORM_FACTOR_UNAVAILABLE` when a headset runtime is half-connected; run it again.

### 14.7 Container inspect (task text, now done, see 14.8)

Do this during `sc02Explore` only. Presentable only. Reference: `step02.jpg` and `step03.jpg` in the Scene 02 Workflow folder.

- Make six pallet products pokeable, not the pallet: flammable drum, oxidizer jerrycan, acid bottle, caustic canister, reactive jar, general-cleaner spray. Match the label texture already on each mesh inside `Sc02Containers.glb`.
- A poke tweens that container from its pallet pose into the left hand (desk: a fixed pose in front of the camera). One at a time. Poking another sends the first home. Poking the held one again also sends it home.
- A cyan details card sits beside the held container (name, hazard, where to store it), like the OXIDIZER PROPERTIES card in step02.
- Under that card, a smaller CHEMICAL COMPATIBILITY CHART box. Poking it calls the existing `ChartViewer.Open()` (step03 large chart + Close). The wall chart stays.
- Stepping on the hotspot returns any held container before MCQ 2.1.
- Do not place containers into doors yet.

Put the behaviour in a new `Assets/Scripts/ChemicalSafety/Presentable/ContainerInspect.cs`. Tag the six products in `BuildSc02Room`. Enable them from `PresentableSequenceDirector.SetRoomInteractable`. Extend `PresentableSmokeTest`: poke one container, open the chart box, close it, poke a second container. Rebuild with **Chemical Safety/Presentable/Build Presentable Scene**, then **Run Smoke Test**. Stop when `[SMOKE] DONE runtime errors/exceptions = 0`.

Unity MCP: `refresh_unity` with `mode: force`, `compile: request`, `wait_for_ready: true`; `execute_menu_item`; `read_console`; `execute_code` needs `action: "execute"` and `compiler: "codedom"` (C# 6). If the tool list has no unityMCP, `python Tools/mcp_http.py <tool> '<json args>'` still reaches the local server. Domain reload drops the socket; retry. Play-mode logs are `Logs/Editor.log`.

### 14.9 Scenario 2 playable, glove styles, voice tools (2026-10-20, branch `feature/Implement-Scenario2-and-modify-UI-UX-as-per-feedback`)
- **SC02 flow** (`PresentableSequenceDirector.RunPlacement`, `ContainerPlacement.cs`): walk to the pallet "Stand Here" marker (rig glides after a timeout) -> touch a chemical; it tweens into the left hand (`ContainerInspect`), a details card with scan beam and typed text plus a chart box appear, VO "keep the chemical in its compartment" plays -> walk to the cabinet (no marker; rig glides after 20 s, timer paused while the chart is open) -> touch a glass door: wrong = red + warning card, container stays in hand; correct = green + blinking ghost slot on the eye-level shelf -> tap the slot, container flies in -> after six placements MCQ 2.1 (answer B) -> Bangla + English completion VO, button label NEXT.
- **Cabinet**: `BuildGlassCabinet` in the builder, rows by `CabinetRows.cs` (floor and top rows stocked, eye-level slots free), doors by `CompartmentDoor.cs`. `containerCompartment {0,1,2,3,3,3}`.
- **Chart**: opens in front of the user (`ChartViewer`, canvas 1120x1060); details card and chart box hide while it is open and return on close. Details card sits 0.52 m from the head so the chart box is touchable in VR.
- **Glove styles**: menu *Chemical Safety/Presentable/Glove Style* (`EditorPrefs` key `PresentableGloveStyle`): 0 black original (**default**), 1 blue on the stock hand with cuff and sleeve, 2 blue on a procedural smooth glove. Procedural mesh: `Tools/generate_glove_hand.py` -> `Art/Presentable/3D/GloveHand_L/R.bytes` -> menu *Import Glove Hand Meshes* (`Editor/GloveMeshImporter.cs`). Known flaw: thin slit at the wrist back in style 2. The user will supply a real 3D glove model later (a skinning importer is planned, not built).
- **Left-hand grasp**: `GlovedHandPose.SetGrasp` closes the fingers on pick; the held container is anchored to the left palm bone in VR (`ContainerInspect.HeldCenter`). Desk mode keeps a fixed pose. Roles unchanged: right hand points, left hand holds.
- **Voice tools**: `Tools/generate_vo.py` (edge-tts, `bn-BD-NabanitaNeural` Bangla, `en-IN-NeerjaNeural` English; `--only <clip>`, `--variants <folder>`). New clips: `VO_Sc02PlaceHint_bn`, `VO_Sc02Complete_bn/en`; first screens use respelled "শাগতম" and "শঠিক". Pronunciation can only be judged by listening; ask the user.
- **Other new tools**: `generate_glove_detail.py`, `generate_scan_beam.py`, `generate_touch_arrow.py`, `generate_stand_badge.py`, `generate_sc02_room_art.py`.
- **Smoke test** (`PresentableSmokeTest.cs`) now also covers: finger stops on panels, chart-box reach and press, grip screenshot, details-card visibility, all six placements, MCQ and voice checks. Last run: 0 errors, "placed 6 / 6".
- **Commit style**: this repo's `.gitignore`d `CLAUDE.md` holds the rules; commits must have no Claude trailer.
- Presentable APK built 2026-10-20 16:48 (127 MB), default glove style 0.
- Not done: wrist slit fix, real glove model import, on-device check of the grasp and palm anchor, user listening check of the new voices.

### 14.10 Scenario 3 playable (2026-10-21, branch `feature/Implement-Scenario3`, off the SC02 branch; not pushed)
Reference pack: `C:\Users\msi\Downloads\Scene03 workflow-20261020T164953Z-1-001\Scene03 workflow\` (`2D/Worklow/step01-08.jpg`, `2D/Resources`, `3D/*.glb`). Order the user wants: the Instructions card comes first, then the drum view.
- **Flow** (`PresentableSequenceDirector.RunScenario3`, stages `sc03`, `sc03Intro`, `sc03Inspect`, `sc03Question`): SC02 question NEXT -> fade -> `Environment_SC03` (x = 80, dark mood via `Sc03Mood`) -> Instructions card + Bangla VO + **Continue** (centred under the card, **eye level**: `PlaceAtEyeLevel`, yaw only, 0.8 m in a headset) -> glide to the drum stand, the new SDS is held up (**left X shows / hides it**, desk C; `SdsClipboardController.SetSheet(sdsSc03)`) -> touch the floating arrow -> gold ring, "Selected Label" scan frame with the enlarged label, typed **DRUM INSPECTION REPORT** card with Continue (`Sc03Inspect`) -> question "IMMEDIATE ACTION PROTOCOL" (answer B). A wrong answer detonates the drum (`Sc03Detonation`, see below) and Close retries; correct plays the correct VO, then the SC03 completion VO (Bangla + English), button label NEXT (the game has about 22 scenarios, SC03 is not the last).
- **Where to change the text**: every on-screen English string is in `Presentable/Sc03Content.cs` (instruction, subtitles, report title and the four label/value pairs - label status is "Expired", date code 12/08/2024 as printed on the label -, question, options, feedback, red-card titles, finish label). After editing run **Build Presentable Scene**. Bangla images: `Tools/render_bangla_text.py` (`BN_Sc03InstructionPanel`, `BN_Sc03Touch`). Voice: `Tools/generate_vo.py --only <clip>` (`VO_SC03_Instruction_bn`, `VO_Sc03Touch_bn`, `VO_Sc03Inspect_bn`, `VO_Sc03Wrong_bn`, `VO_Sc03Complete_bn/en`). The voices were generated, not listened to: the user should check the pronunciation.
- **Room**: `Editor/PresentableSceneBuilder.Sc03.cs` (partial class). Models are prepared by `Tools/prepare_sc03_models.py` (hero drum and old drums with textures capped at 1024, shelf decimated to 14k triangles; the originals stay out of Assets). Textures: `Tools/generate_sc03_room_art.py` (wall, floor, crystal residue, scan frame, flame).
- Small shared changes: `PresentableMcqPanel` has `WrongClosed` and a per-option `wrongTitle`; `InstructionPanelView.SetContinueCentered`; `PresentableArt` has the SC03 slots.
- **Smoke test** covers SC03 (card eye-level offsets, SDS toggle, arrow, report, both wrong answers and the detonation, correct answer and VO). Last run: `[SMOKE] DONE runtime errors/exceptions = 0`. Fix found on the way: `Sc03Detonation.Clear` must not run before the room was shown.
- Not done: on-device check (eye-level card for a seated user, arrow reach, fire look), Presentable APK rebuild, listening check of the SC03 voices.
- **Detonation VFX** (`Sc03Detonation.cs`, assets from `Tools/generate_sc03_fx.py`): flash + shock rings, intact drum swapped for `HeroDrumBurst` (mesh with the top peeled open and charred glowing materials, built in `BuildBurstDrum`), lid and 26 blue shards fly off, 6x6 flip-book fireball, flames, smoke, sparks, embers, crystal dust, scorch decals, red siren lamps, red screen-edge vignette, procedural blast / crackle / alarm sound. About 10 particle systems, under 450 live particles; the siren and vignette pulse smoothly (no hard strobe).
- **VOICE RULES FOR THE WHOLE GAME (user, 2026-10-21)**: one voice for every scenario (`bn-BD-NabanitaNeural`, English `en-IN-NeerjaNeural`) and one pronunciation per word. Respell words only in the `PRON` table of `Tools/generate_vo.py` (now: স্বাগতম -> স্বাগোতোম, সঠিক -> শঠিক, সফল -> শফল, সিদ্ধান্ত -> শিদ্ধান্ত, পারক্সাইড -> পারঅক্সাইড; listening variants: `python Tools/generate_vo.py --words DIR`); never inside a clip. The sentence that ends a scenario is one template (`complete_bn/en`): "আপনি সিনারিও N সফলভাবে সম্পন্ন করেছেন। সিনারিও N+1-এ যেতে নেক্সট বাটনে ক্লিক করুন।" / "You have successfully completed Scenario N. Click Next to go to Scenario N+1." The SC01-SC03 endings were all regenerated from it. Add the numbers of a new scenario to `NUM`, `TO_NUM`, `EN_NUM`. The voices still need the user's listening check (especially শিদ্ধান্ত).
- **SC03 environment realism (2026-10-21, after the Quest video)**: racks are a procedural steel rack mesh (`Sc03RackMesh`, one mesh instanced 5x, world-scale UV) with the designer's rust PBR textures (`Tools/extract_sc03_textures.py` -> `2D/Sc03Tex/Rust_*`); background drums are the designer's own 240-triangle rusty barrel (`Sc03BarrelMesh` bakes it from `Old drams.glb`, `Barrel_*` textures, 5 tint materials); floor / wall / ceiling use PBR textures with normal maps (`Tools/generate_sc03_env.py`: cracks, stains, salt crust, wet patches, damp streaks, peeled plaster); cobwebs, drips, blots, light shafts, floating dust motes, an old tube that dims softly (`Sc03Flicker`), floor clutter (pallet, fallen barrels, boxes). `Sc03Mood` also lowers the reflection intensity (the default sky made the wet floor blue). Helpers: `PresentableSceneBuilder.Sc03Env.cs`. Draw calls / triangles are low (props are batching-static, about 15k + 14k triangles).
- **Quest feedback round (2026-10-21, SC02 + SC03)**: (1) the cabinet is solid now (front wall at the glass + both sides, layer `SolidProps`), the glove stops at the glass; the empty slot is tapped through `ContainerPlacement` `SlotTapZone` (a trigger from the slot out to the glass) with a blinking dashed ring on the glass. (2) The left glove cradles the container: `GlovedHandPose.SetCradle` turns the glove (visual only) palm up under it, `TryGetPalmFrame` gives the palm frame, `ContainerInspect.HeldCenter` stands the container on the palm, fingers curl by `item.grasp` (less for wide containers). (3) Details card: eye level, 0.68 m, 17 cm right, yaw lazy-follow (24 degree dead zone) in `ContainerInspect.PlaceCard`; scan beam thinner. (4) `ChartViewer` opens at 0.58 m, eye level, 0.8 scale, big Close + a red X top right. (5) SC03: the floating arrow is replaced by `Sc03LabelScan` (hold a finger on the label 0.9 s: gold brackets, blinking ring, scan line; desk = hold the left mouse button); the rig steps to `Stand_SC03_Scan` (0.6 m from the label) for it and back to `Stand_SC03` for the report and the question; rack solids are 0.66 m deep so the finger reaches the label. (6) Blast: 0.26 s drum tremor, flat shock ring, 40 shards, warm smoke fog (`Sc03Detonation`). Smoke test covers the hold, lifting the finger, the cabinet and the chart.
- **Common right-answer voice** (user, 2026-10-22): `VO_Correct_bn` = "সঠিক। চমৎকার! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।" for every scenario (defined in `Tools/generate_vo.py` `CLIPS`; no scenario-specific words like label or drum). It is followed by the scenario-ending line.
