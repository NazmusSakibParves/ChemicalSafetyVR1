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
| 2026-10-23 | Presentable SC04 lab after commit `3372c17` (uncommitted): realistic lab props, glove colours, eye-level bench, poke fix, APK 171.5 MB at 22:37. Read §14.11. Do not treat §14.1–14.10 as the current scene. |

---

*If you update the demo significantly, append a row to §13 and refresh §5 / §9 so the next machine stays accurate.*


---

## 14. Presentable demo — full state & next steps (latest: 2026-10-23, §14.11)

> **Newest state: read §15 (roadmap: 22 scenarios, login, marking, server API, only scenario 22 says "training complete") and §14.17 (Scenario 9, 2026-10-27, commits `25dd6d2` + `c07789f`, not pushed) first, then §14.16 (Scenario 8, 2026-10-26/27, rounds 1-2, commits `bd90013` + `f4011c2`, not pushed) first, then §14.15 (Scenario 7, 2026-10-25/26, rounds 1-5), then §14.14 (Scenario 6, 2026-10-24), then §14.13 (Scenario 5, commit `1847c61`, pushed, no APK yet) and §14.12 first** (§14.12: 2026-10-24, SC02-SC04 fixes committed and pushed, APK built), then §14.11 for the SC04 lab background. The list "OPEN ITEMS TO REMIND THE USER" at the end of this file must be mentioned once at the start of a session. If a sentence in §14.11 or the line below says "uncommitted" or "not pushed", §14.12 supersedes it. §14.1–14.10 are history. This section supersedes the older Presentable bullets in §2b.
> Rules that still apply: **change only the Presentable client demo** (never `ChemicalSafety_Demo` / the Learning APK), generate the scene with the builder menu (never hand-edit the `.unity`), never edit scripts while in Play mode, author is the user (no Claude / Cursor co-author trailers), do **not** merge, push, or commit unless the user says so. Current branch: `Implement-Scenario3-Scenario4-Scenario5-and-fix-UI-feedback`. Last commit `3372c17` (not pushed). Everything in §14.11 after that commit is **uncommitted**. Do not build an APK unless the user asks.

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
- **VOICE RULES FOR THE WHOLE GAME (user, 2026-10-21)**: one voice for every scenario (`bn-BD-NabanitaNeural`, English `en-IN-NeerjaNeural`) and one pronunciation per word. Respell words only in the `PRON` table of `Tools/generate_vo.py` (now: স্বাগতম -> স্বাগোতোম, সফল -> শফল, সিদ্ধান্ত -> শিদ্ধান্ত, পারক্সাইড -> পারঅক্সাইড; listening variants: `python Tools/generate_vo.py --words DIR`); never inside a clip. The sentence that ends a scenario is one template (`complete_bn/en`): "আপনি সিনারিও N সফলভাবে সম্পন্ন করেছেন। সিনারিও N+1-এ যেতে নেক্সট বাটনে ক্লিক করুন।" / "You have successfully completed Scenario N. Click Next to go to Scenario N+1." The SC01-SC03 endings were all regenerated from it. Add the numbers of a new scenario to `NUM`, `TO_NUM`, `EN_NUM`. The voices still need the user's listening check (especially শিদ্ধান্ত).
- **SC03 environment realism (2026-10-21, after the Quest video)**: racks are a procedural steel rack mesh (`Sc03RackMesh`, one mesh instanced 5x, world-scale UV) with the designer's rust PBR textures (`Tools/extract_sc03_textures.py` -> `2D/Sc03Tex/Rust_*`); background drums are the designer's own 240-triangle rusty barrel (`Sc03BarrelMesh` bakes it from `Old drams.glb`, `Barrel_*` textures, 5 tint materials); floor / wall / ceiling use PBR textures with normal maps (`Tools/generate_sc03_env.py`: cracks, stains, salt crust, wet patches, damp streaks, peeled plaster); cobwebs, drips, blots, light shafts, floating dust motes, an old tube that dims softly (`Sc03Flicker`), floor clutter (pallet, fallen barrels, boxes). `Sc03Mood` also lowers the reflection intensity (the default sky made the wet floor blue). Helpers: `PresentableSceneBuilder.Sc03Env.cs`. Draw calls / triangles are low (props are batching-static, about 15k + 14k triangles).
- **Quest feedback round (2026-10-21, SC02 + SC03)**: (1) the cabinet is solid now (front wall at the glass + both sides, layer `SolidProps`), the glove stops at the glass; the empty slot is tapped through `ContainerPlacement` `SlotTapZone` (a trigger from the slot out to the glass) with a blinking dashed ring on the glass. (2) The left glove cradles the container: `GlovedHandPose.SetCradle` turns the glove (visual only) palm up under it, `TryGetPalmFrame` gives the palm frame, `ContainerInspect.HeldCenter` stands the container on the palm, fingers curl by `item.grasp` (less for wide containers). (3) Details card: eye level, 0.68 m, 17 cm right, yaw lazy-follow (24 degree dead zone) in `ContainerInspect.PlaceCard`; scan beam thinner. (4) `ChartViewer` opens at 0.58 m, eye level, 0.8 scale, big Close + a red X top right. (5) SC03: the floating arrow is replaced by `Sc03LabelScan` (hold a finger on the label 0.9 s: gold brackets, blinking ring, scan line; desk = hold the left mouse button); the rig steps to `Stand_SC03_Scan` (0.6 m from the label) for it and back to `Stand_SC03` for the report and the question; rack solids are 0.66 m deep so the finger reaches the label. (6) Blast: 0.26 s drum tremor, flat shock ring, 40 shards, warm smoke fog (`Sc03Detonation`). Smoke test covers the hold, lifting the finger, the cabinet and the chart.
- **Common right-answer voice** (user, 2026-10-22): `VO_Correct_bn` = "সঠিক। চমৎকার! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।" for every scenario (defined in `Tools/generate_vo.py` `CLIPS`; no scenario-specific words like label or drum). It is followed by the scenario-ending line.
- 2026-10-22: by listening the user chose plain "সঠিক" (no respelling any more, so SC01/SC02 instruction clips were regenerated) and "চমৎকার" -> "চমতকারর" (the voice drops the final র) at speaking rate -8% for `VO_Correct_bn`. `generate_vo.py --correct DIR` makes listening variants.
- **Panel layout (2026-10-22, after the Quest video)**: the `pop up screen` frame is drawn larger than its canvas, so text must stay inside the measured safe area: MCQ panel header y 0.845-0.93, prompt 0.738-0.822 (below the title underline ~0.83), rows from 0.715, height 0.13, gap 0.02 (last row ends 0.135, clear of the bottom arc ~0.06-0.10), row labels inset to y 0.1-0.9 with font 20-34; `pop02` correct/wrong cards: glass area x 0.12-0.88, y 0.12-0.83 (title 0.68-0.81, body from 0.16 / 0.28, wrong card X at the bottom right). `LabelPickPopup` (SC01) now uses the same step05 row style (dark fill + `answerBox`, centred text) with orange corner accents (`PresentableUi.CornerAccents`). The smoke test runs `LayoutCheck` on every panel (violations must be 0).

### 14.11 Scenario 4 lab, 2026-10-23 (done in the editor, uncommitted, APK built)

Last commit on the branch is `3372c17` (VR feedback: instruction card stays in reach, MCQ fills its frame, Scenario 3 blasts only on a wrong answer, first glove-lab pass). It is **not pushed**. Do not amend it. A later `git commit` from this environment injects `Co-authored-by: Cursor`; the clean commit was written with `git hash-object`, not `git commit`.

Everything below is **after** that commit and is **not committed**. The scene file `Assets/Scenes/ChemicalSafety_Presentable.unity` is dirty because the builder regenerated it. Do not hand-edit it. Change `PresentableSceneBuilder.Sc04.cs`, then menu **Chemical Safety/Presentable/Build Presentable Scene**.

**Done (Presentable only, SC01–SC03 questions, answers, rooms, and poke distances were not retuned):**

- SC04 room matches the lab photo more closely. Builder: `Assets/Scripts/ChemicalSafety/Editor/PresentableSceneBuilder.Sc04.cs`. Viewer spawn `Spawn_SC04` is `(120, 0, 3.15)`, yaw 180, facing -Z. Viewer-left is +X.
- Four blue pipettes upright on a white angled rack. White fume hood with glass and a dark opening (`HoodMouth`), pulled off the back wall (`min.z` about -2.88). SDS sheet `Assets/Art/Presentable/2D/Sc04/SDS_SC04.png` is the front face of the clipboard (quad faces +Z), on the viewer's right of the black glove, with coloured tabs. Glossy cool-gray floor texture `Assets/Art/Presentable/2D/Sc04/FloorGray.png`. Wall tiles stay, smoothness lowered from 0.58 to 0.22 so the Quest specular shimmer stops. Floor stays glossy. No new realtime shadows.
- Gloves, left to right in the player's view: white cotton, blue nitrile, larger black neoprene. Materials `M_Sc04GloveWhite/Blue/Black`. Black glove scale about 1.38. They sit on an unlit white towel (`Sc04Cloth`). Sort is still world X descending. Poke reach stays **0.07**.
- Glove poke was broken on device even though smoke passed. Smoke used to call `PokeTarget.Fire()` directly, so a collider the finger cannot reach still passed. The glove box was on `SolidProps`, and the palm push lifted the fingertip off the glove. Handles are triggers now, not `SolidProps`, padded upward and toward the player. `GloveParts.Parts` is public. The gold arrow uses those meshes (`GloveInspect.BoundsOf`). Smoke logs `SC04 glove N mesh in reach` and `Debug.LogError` if that is false. Do not put the glove handles back on `SolidProps`.
- Test tubes are real cylinders, not a shadow. They were ghostly (`LabGlass` alpha 0.28) and sat against the gloves. They are now at the viewer's left end of the glove table (`gloveBox.max.x - 0.22`) with `Sc04TubeGlass` (alpha 0.82) on a white rack.
- Fridge door faces the player (+Z), with a glass door, stainless trim, and a dark handle. The old door faced +X, so the player saw a blank white box. AC is right-way up on the back wall: dark bottom vent, `25°C` from `AC_Display.png`, pulsed about once a second by `Sc04DisplayPulse`. Hood interior/mesa is darker so the opening reads as open.
- Both `table02` benches are stainless (`Sc04Steel`). Only the glove bench moves with the head. `BenchRoot` holds the glove table, cloth, glove model, glove handles, SDS, and tubes. `Sc04BenchHeight` puts the tabletop 0.32 m below the eyes (clamp 0.55–1.45). An 8 cm dead zone ignores head bob; a sit/stand change eases. `PresentableSequenceDirector` calls `SnapTo` before the glide into SC04 and `Follow` while the glove stage runs. Glove home positions are **local** to the bench, so a height change does not leave a glove floating. The side bench, walls, fridge, and AC stay on the floor.
- Desk smoke after this pass: glove mesh in reach 3/3, holding 3/3, layout violations 0, `[SMOKE] DONE runtime errors/exceptions = 0`. Prefab `SetParent` errors on SC02 `Object_4.*` are old and expected.

**APK on disk (install this one):** `Builds/Android/ChemicalSafetyVR_Presentable.apk`, **171.5 MB**, 2026-10-23 22:37, release, errors 0. It includes the lab pass above. The 19:03 file (173.1 MB) was the earlier prop/glove-colour build and has been replaced. The user has not yet reported a Quest check of the 22:37 build.

**Pink Game view (fixed the same evening, do not "fix" it again by clearing a pipeline):** Unity paints the whole Game view magenta when the active quality level has no Universal RP asset. The Presentable APK build had saved `QualitySettings` with Mobile's `customRenderPipeline` set to `{fileID: 0}`. Mobile is what the editor uses on the Android target. Restored to `Assets/Settings/Mobile_RPAsset.asset` (guid `5e6cbd92db86f4b18aec3ed561671858`). PC stays `PC_RPAsset` (guid `4b83569d67af61e458304325a23e5dfd`). `ProjectSettings/QualitySettings.asset` matches git again. `ChemicalSafetyApkBuilder` now switches back to the Mobile quality level and refuses to save a null pipeline (falls back to `Mobile_RPAsset`). That builder change is uncommitted. Do not commit `ProjectSettings/` or `Assets/Settings/`.

**Not done (do not invent these as finished):**

- No commit and no push of the SC04 lab work or the APK-builder guard.
- Scenario 5 is not built. The branch name mentions it; the scene does not.
- Learning scene `ChemicalSafety_Demo` and `Builds/Android/ChemicalSafetyVR.apk` were not touched.
- Voice was not regenerated for this lab pass. ElevenLabs is paused (HTTP 402). Bangla voice stays edge-tts `bn-BD-NabanitaNeural`. Do not add সঠিক to the global `PRON` table. The শঠিক respelling is one clip only: `VO_SC01_Instruction_bn`. Screen text stays সঠিক.
- Instruction panel `Reflow`, glove style default 0 (black original), and SC01–SC03 question text were not changed.
- On-device confirmation of the 22:37 APK (seated and standing bench, glove poke, SDS readable, fridge door, AC 25°C, no wall shimmer) is waiting on the user.

### 14.12 Quest feedback round 2 + SC04 realism (2026-10-23/24, smoke test 0 errors, committed and pushed)

**Status in one place**
- Branch `Implement-Scenario3-Scenario4-Scenario5-and-fix-UI-feedback`. Commits of this round: `66e6c06` (SC02/SC03/SC04 fixes), `023e93e` (SC04 realism), plus this docs/handoff commit on top. Pushed to `origin` (no force, nothing merged). Author NazmusSakibParves, no Claude trailer (commits are made with `git commit-tree` + `git reset --soft`, see §14.6).
- APK on disk: `Builds/Android/ChemicalSafetyVR_Presentable.apk`, about 175 MB, built 2026-10-24 00:41, release, `Build Finished, Result: Success`. It has everything below. The user has **not yet** reported a Quest check of it.
- Scenarios 1-4 are playable end to end. Scenario 5 is not built.
- Language: the user writes Bangla and English; reply in Bangla (see `CLAUDE.md`, which is git-ignored).

**What the user reported on the Quest and what was done**

- SC02: the details card shows on every pick (also at the cabinet), keeps clear of the held container (eye level, 0.6-0.8 m, side chosen from the container position), and the only "CORRECT PLACEMENT" card appears after the container is down, with the counter already raised. `ContainerPlacement.TryPlace` no longer shows a card on the correct door.
- SC03: `PresentableMcqPanel.PopIn` takes input only after the pop-in and once no fingertip is in front of the panel (fixes the blast right after Continue). Blast stays on a wrong answer only.
- SC04: `Quad` primitives show their face toward -Z. AC display, SDS page, fridge door and bottle label were rotated with `LookRotation(forward)` and were culled for the player at +Z; now `LookRotation(back)`. SC04 MCQ is placed with `PlaceAtEyeLevel` like SC03. Gloves rest on a lit cloth with `ContactShadow.png` decals (`GroundGloves`).
- SC04 floating gloves: `GroundGloves` in `PresentableSceneBuilder.Sc04.cs` drops each glove so its lowest vertex is 2 mm above the cloth and puts a soft `ContactShadow.png` quad under it; the cloth is a lit material (`Sc04ClothLit`).
- The SDS binder, AC display, fridge door and bottle label were invisible only because of the Quad facing described above. If a future Quad "disappears" from the player's side, check `LookRotation(Vector3.back, ...)` first.

**How to verify (same as always)**: Unity menu *Chemical Safety/Presentable/Build Presentable Scene*, then *Run Smoke Test*, look for `[SMOKE] DONE runtime errors/exceptions = 0` in `Logs/Editor.log`. The smoke test now also saves SC04 screenshots `Temp/Smoke/29_sc04_instruction.png`, `30_sc04_lab.png`, `31_sc04_sds_held.png` and checks the details card at the cabinet (expected True) and that placements reach 6 / 6. After editing scripts, `refresh_unity` times out while Unity recompiles; wait for the domain reload (poll `http://127.0.0.1:8080/health`, then about 40 s), then run the menu items. The `Setting the parent of a transform which resides in a Prefab instance` errors for SC02 `Object_4.*` are old and expected.

**SC04 realism pass (2026-10-24, user asked for the lab to look like the designer's `Step01.jpg` and to feel less empty):**
- Not done: a Quest check of this round, the floor still shows bright specular streaks in the desk screenshot, the cloth is a flat white card, and the lab still has fewer props than the designer image. The user said it is "much better than before" but wanted the glass-door fridge with visible contents (done) and a fuller room (done in part).
- SC04 realism pass (2026-10-24): `Tools/generate_sc04_env.py` makes PBR textures in `Art/Presentable/2D/Sc04Tex` (30 cm wall tiles, epoxy floor, brushed steel, T-bar ceiling, AO decal). `PresentableSceneBuilder.Sc04Env.cs` holds the PBR material helper (`Sc04Pbr`), set dressing (cove base, ceiling panels, under-cabinet strip, door, extinguisher, sockets, floor AO) and the new props: hollow glass-door fridge stocked with bottles (`Fridge` + `StockFridge`), back bench with balance and centrifuge (`LabBackBench`), reagent shelves (`LabShelving`). The room is 7 m wide (right wall at ox-3.0) so it no longer feels empty. Smoke test 0 errors; Quest check of the look is pending.

### 14.13 Scenario 5 (dyeing factory, respirator selection), 2026-10-24, smoke test 0 errors, committed `1847c61` and pushed, no APK yet
Reference pack: `C:\Users\msi\Downloads\Scene05 workflow-20261023T120404Z-1-001\Scene05 workflow\` (`Workflow/step01-10.jpg`, `3D/*.glb`, `2D resources/`), content file `C:\Users\msi\Downloads\WhatsApp Image 2026-10-21 at 10.28.44.jpeg` (Respirator Properties: type -> compatibility with toluene).
- **Flow** (`PresentableSequenceDirector.RunScenario5`, chained after the SC04 question's NEXT; stages `sc05`, `sc05Intro`, `sc05Explore`, `sc05Question`): fade to `Environment_SC05` -> Instructions card + Bangla VO + Continue (same eye-level card as SC03/SC04; machine sound starts here) -> SDS in the left hand (X toggles, `SDS_SC05.png`) -> a green arrow ticks over the four respirators one by one (left to right for the player), touch the marked one: it comes to the hand, MASK INFORMATION card (name + compatibility, close X) and a **Bangla voice-over says which mask it is and whether it suits toluene** (`VO_Sc05Mask0..3_bn`, order N95, half-face, surgical, full-face) -> after all four a BACK/NEXT gate card -> MCQ 5.1 (answer B) with the shared correct/wrong cards and the shared right-answer voice, then `VO_Sc05Complete_bn/en` (the line says "go to Scenario six", SC06 is not built).
- **Where to change text**: `Presentable/Sc05Content.cs` (instruction, mask names and compatibility exactly as in the client's sheet, MCQ texts). Bangla images/voice: `Tools/render_bangla_text.py --only BN_Sc05InstructionPanel`, `Tools/generate_vo.py --only VO_SC05_Instruction_bn,VO_Sc05Mask0_bn,...`. The voices were generated, not listened to (the user must check). `NUM[6]` etc. are already in `generate_vo.py` for the SC06 ending.
- **Room**: `Editor/PresentableSceneBuilder.Sc05.cs` (partial class, `BuildSc05Room`, env at x = 160, spawn `Spawn_SC05` at the pillar, viewer faces -Z, viewer-left is +X). Concrete pillar with the Vapor Hazard sign (`2D/Sc05/VaporSign.png`), pulsing red gas detector (85 ppm), OSHA SDS holder, extinguisher, hook rail with the four respirators (`3D/Sc05/Masks.glb`; each mask is a group `Mask_i` with a trigger collider and `PokeTarget`, **not** on `SolidProps`); dye machines from `3D/Sc05/Factory.glb` (scale 0.5, rotated 180 so the row runs away from the viewer, parts outside the hall are hidden, `ControlPanel` and metal materials replaced by painted ones because metals render black here, a mesh without a material renders magenta); concrete hall shell, beams, pipes, three ceiling fans at 640-780 deg/s, flickering tube lights, steam emitters, drifting dust, barrels, cloth rolls, puddles. Scripts: `MaskInspect.cs` (modelled on `GloveInspect`), `Sc05FactoryFx.cs` (fans, shaking panels, swaying cloth, flicker, detector pulse), `Sc05Ambience.cs` (machine loop `Art/Audio/Sc05/Machine_Loop.wav`, volume 0.32, drops to 0.09 while the voice plays and fades back over 1.2 s; capped on purpose so it is never loud). Mood (dark hazy fog, ambient) reuses `Sc03Mood` with its own values.
- **Tools**: `Tools/prepare_sc05_models.py` (shrinks the 80 MB of GLBs to `Art/Presentable/3D/Sc05/`, makes `VaporSign.png` and the transparent `SDS_SC05.png`; `mashin01.glb` is just a rounded slab and is unused), `Tools/generate_sc05_env.py` (concrete/floor/ceiling PBR, steam sprites, SDS holder label, detector face -> `2D/Sc05Tex`), `Tools/generate_sc05_audio.py` (the machine loop).
- **Gotchas found**: glTF children of an instantiated prefab cannot be re-parented, so the builder unpacks the instance first (`SpawnSc05`). MCQ rows are named `Row1..Row4` in the smoke test (not Row0). `PresentableMcqPanel` now takes input only after pop-in and when no fingertip is in front of it (up to 2.5 s), so a fingertip parked by the smoke test can delay a click; real users are not affected. A `ParticleSystem` velocity curve must use the same mode on all three axes.
- **Smoke test** covers SC05: machine sound ducking, each mask (reach, hold, card text, voice clip name), gate, MCQ wrong then right, layout checks, screenshots `Temp/Smoke/40..49_sc05_*`. Last run: `[SMOKE] DONE runtime errors/exceptions = 0`.
- **Scenario 5 card style (2026-10-24, after the Quest test, committed and pushed)**: the MASK INFORMATION card and the RESPIRATOR INSPECTION COMPLETE card copy the designer's hologram look (`Workflow/step05.jpg`, `step06.jpg`) and are used **only in Scenario 5** (`MaskInspect.cs`; GloveInspect, MCQ, instruction card untouched). The properties card shows the picked mask's upright line-art picture (`MaskIcon_0..3`) instead of a tick/cross, a speaker button next to the close X (replays that mask's Bangla voice, pulses green while it plays) and the close X (stops the voice, mask goes back to the hook, the arrow moves to the next mask). The gate card has `BACK` with a circular-arrow icon and `NEXT` with a check after the text. **To go back to the old blue boxes set `Sc05Content.WorkflowStyleCards = false` and rebuild the scene** (the old builders are kept as `BuildCardClassic` / `BuildGateClassic`). Sprites come from `Tools/generate_sc05_ui.py` into `Art/Presentable/2D/Sc05` (`Holo_Panel`, `Holo_Button`, `Holo_ButtonPrimary` are 9-slice, borders set in `ConfigureImports`; icons and the four hand-drawn mask pictures; the mask pictures are clean vector-style drawings, not renders of the 3D models). A held mask now stays at least 0.5 m in front of the rail and is scaled to about 0.36 m tall so its straps no longer pass through the hanging masks (`MaskInspect.HeldOverlapsOthers()` is checked by the smoke test). The compatibility text keeps the green/red tint (the designer's picture has it white).
- **Not done / not verified**: Quest check of rounds 4-5 (arm and hose look of the CO2 spraying, card heights seated/standing at the new stand distance, the MCQ vantage point, workers' look and walk, footstep and alarm loudness, fire and smoke cost with 5 skinned people), a listening check of the Bangla clips, the full (all scenarios) smoke test after round 5. The hall is close to but not identical with the designer's pictures (own materials, primitive boilers).

### 14.14 Scenario 6 (chemistry lab, fume hood, engineering controls), 2026-10-24, uncommitted, no APK yet
Full task brief and the step-by-step log: `Docs/SC06_TASK_BRIEF.md`. Reference pack: `C:\Users\msi\Downloads\Scene06 workflow-20261024T090220Z-1-001\Scene06 workflow\` (`Workflow/step01-12.jpg` (no step05), `3D/*.glb`, `2D resource/`).
- **Flow** (`PresentableSequenceDirector.RunScenario6`, chained after the SC05 question's NEXT; stages `sc06`, `sc06Intro`, `sc06Hood`, `sc06Jug`, `sc06Sash`, `sc06Pour`, `sc06PourDone`, `sc06Gate`, `sc06Question`, `sc06Done`): Instruction card + Bangla VO + Continue -> fade to `Stand_SC06_Panel` (corridor between the table and the hood) -> green arrow over the **Fume Hood** switch (the LEV Arm and General Ventilation switches also work) -> hood on: lights, airflow streaks, paper strip, motor sound -> green glow + arrow on the **Volatile Solvent** jerrycan, a poke lifts it into the left hand -> glide to `Stand_SC06_Hood`, poke the glass sash (refused with a red flash and a Bangla warning while the hood is off), it slides up -> poke the jar: the jug flies over it, tilts, a stream pours in, the cap flies on, the vapor stops -> sash closes, fade to `Stand_SC06_Table`, **Inspection completed** card (BACK restarts the whole inspection with the lab reset, NEXT opens the MCQ) -> MCQ 6.1 (answer C, index 2) with the Scenario-6-only wow look -> `sc06Done`. Nothing follows Scenario 6 yet (no scenario 7).
- **Builder**: `PresentableSceneBuilder.Sc06.cs` (room, layout, stands, preview menu, dev menu, reflection probe bake), `.Sc06Env.cs` (PBR helper, jerrycan, chem groups, apparatus), `.Sc06Glass.cs` (lathe glassware, test-tube rack, glass material). Env at x = 200; viewer-left is +X. Back wall z = -2.9: hierarchy poster (+1.95), SDS poster + pocket (+0.35 / +0.78), hood centre -1.15, control panel on a pillar flush with the hood front (-2.62), spill-kit cabinet + signs (-3.55); table centre (-1.15, -0.35) straight in front of the hood.
- **The designer's models in use**: `FumeHood.glb` (one merged mesh: the builder drops its raised Glass/frame sub-meshes and builds its own sliding `Sash`), `Table.glb`, `LevArm.glb`, `SpillKit.glb` (scaled cube), and from `camical bottles1.glb` (never import the 26 MB original) via `Tools/prepare_sc06_chem.py` -> `3D/Sc06/Chem/*.obj`: the **5 L jerrycan** (`Mesh_2`, converted to the jug frame: base at the origin, label +Z, mouth toward -X; the cap is cut off so it can be open) and the bottles NaOH / Alcohol / Phenylhydrazine / napthol / ice-water beaker / pill bottle / dropper, each part (glass, liquid, cap, label band) with its own material. Their glb materials have no textures (only factor colours), so labels are our own quads (`Label_Bottle0..2`, `SolventLabel.png`).
- **Glass**: `Assets/Shaders/Sc06Glass.shader` (URP, fresnel, reflection, two-sided, no sun needed) on lathe-made vessels with wall thickness. A baked reflection probe `Generated/Sc06Probe.exr` is made by the builder (`Sc06BakeProbe`, skipped with a warning if the bake fails).
- **Scripts** (`Presentable/`): `Sc06Content` (all strings), `Sc06Lab` (switches, LEDs, sash open/close, airflow and vapor particles, suction into the hood, arrow), `Sc06Jug` (poke -> hand cradle, cap), `Sc06Pour`, `Sc06Gate`, `Sc06Ambience` (room hum, hood loop, click, slide, pour; ducks under the voice), `Sc06McqFx` (only for `Question.fancy`, set only in `BuildSc06Question`). Audio from `Tools/generate_sc06_audio.py`. Textures and signs from `Tools/generate_sc06_env.py`, Bangla images from `Tools/render_bangla_text.py` (`BN_Sc06*`).
- **Voice**: `Tools/generate_vo.py` clips `VO_SC06_Instruction_bn` (respelled **শঠিক**: the voice read সঠিক as ছঠিক there, same fix as the SC01 instruction clip; the screen text keeps সঠিক), `VO_Sc06Hood/HoodOn/HoodWarn/Jug/Sash/Pour/Gate_bn`, `VO_Sc06Complete_bn/en`. Generated, **not listened to by the user yet**.
- **Developer shortcut**: menu *Chemical Safety/Presentable/Dev: start at Scenario 6* (EditorPrefs `ChemSafetySc06Only`) starts Play directly in SC06. **Keep it OFF** (it is off when this was written). Editor preview without Play mode: menu *Preview SC06 (screenshots)* renders the views in `Temp/sc06_views.txt` to `Temp/Smoke/sc06_<name>.png`.
- **Smoke test** (`PresentableSmokeTest.Sc06Smoke`): real fingertip pokes on the switches, jug, sash and jar (`FingerPoke`), the wrong-order sash refusal, two full inspection rounds (the second after BACK), pour stream and fill, cap on, gate card layout, MCQ look flags (SC04, SC05 and SC06 are fancy), wrong C/right answers and the completion voice, screenshots `Temp/Smoke/49b..59_sc06_*`.
- **Second feedback round (2026-10-24, smoke test 0 errors again)**: (1) the jerrycan was pouring from the handle side and the cap went on the other side: **Unity's OBJ importer negates x**, so the OBJ written by `Tools/prepare_sc06_chem.py` is now written already mirrored (`x = z - zc`, no winding flip) and the mouth really is at -X; every new OBJ from a script must account for this. The pour tilts about Z so the mouth side goes down, the handle stays above it, the cap flies to the same mouth. (2) The red-marked part of the designer's picture was the **LEV arm on a counter with a duct to the ceiling**: a counter (`CounterBody`/`CounterTop`, white doors, steel top) now runs along the back wall under the poster, the arm stands on its left end reaching toward the room (the model is rotated so its reach points to -X), and `LevDuct` goes from the arm's highest point into the ceiling. (3) Jug label like the picture: smaller white label (`SolventLabel.png`) plus a big blue `5L` (`Label_5L`). (4) "Inspection completed" card restyled like the designer's step08: light teal frosted glass, chamfered corners, bright outline, white text, darker teal buttons (`2D/Sc06/Gate_Panel/Gate_Button/Gate_ButtonPrimary`, 9-slice, made by `generate_sc06_env.py`; borders set in `ConfigureImports`). (5) Ceiling like the picture: clean white T-bar tiles, egg-crate light panels (`Label_LightGrid`, unlit), a louvred square air diffuser (`Label_Diffuser`), ambient-occlusion strips (`Fx_AOStrip`) at the ceiling and floor edges and the corners, an exit door with a green sign in the right wall. (6) Glass shader got a darker absorption band at mid angles.
- **Third round (2026-10-25, after the Quest test of the APK, smoke test 0 errors)**: (1) the table was a thin glb sheet: `Sc06SolidTable` (`.Sc06Env.cs`) now builds a thick top with rolled edges, an apron, square legs, a lower shelf with a lip; only the drawer cabinet of `Table.glb` is kept (the thin part is found by its material name "Scratched silver metal"). (2) Switches follow the Bangladeshi habit: **down = ON, up = OFF** (panel labels OFF above / ON below, lever +16 deg = ON), the lamps glow (HDR lens, bezel, additive halo `LedGlow0..2`, breathing while on). (3) The gate card, MCQ and the Correct/Wrong popups open from `Stand_SC06_Table`, now the back spot (z = 1.35, same as the spawn) so the whole room is visible behind them; title text is "Inspection Completed". (4) New flow step `sc06Cap`: after the hood is on the user comes to `Stand_SC06_Bench` (in front of the table), touches the loose red cap (`CapPoke`, glow + arrow), the cap flies over the neck and screws on, the vapor stops (`Sc06Jug.CapOn`/`CapClosed`), then the closed jug is picked up (`sc06Jug`), carried to the hood (fade, not a glide through the table), and at the jar `Sc06Pour` first unscrews the cap (`CapOff`, vapor on again, pulled into the hood) and re-closes it after the pour. Voice `VO_Sc06Cap_bn`, `VO_Sc06HoodOn_bn` re-recorded, image `BN_Sc06CapHint`. Smoke test covers the lever angle, the lamp glow, the cap step with a real fingertip, vapor off after closing, cap off while pouring.
- **Not verified / open**: Quest check of the whole scenario (poke reach of the panel switches from `Stand_SC06_Panel`, the jar through the open sash, the jug hand-over, card heights for a seated user, comet position on the MCQ frame: `Sc06McqFx.cometInset`), listening check of the Bangla clips, the LEV arm is a floor-standing model (the designer's picture hangs it from a duct), the solvent is drawn colourless (`Sc06JarLiquid`, `Sc06Stream`, `Sc06ChemLiquid`) because the designer's pictures show clear liquid, a Presentable APK that includes SC06.

- **Fourth round (2026-10-25, after the Quest video, smoke test run after each step)**: (1) SC02: `ContainerPlacement.FitScale` shrinks a placed container until its footprint fits the shelf cell (0.19 m deep, 0.12 m half-width when the compartment has several slots), ghost and final pose share it; `ContainerInspect.HeldFootprintHalf`. (2) SC04 gloves use the SC01 floating arrow (`FloatingTouchButton`, one per glove, `SetBase` follows the glove and the moving bench) instead of the gold ring. (3) SC04 GLOVE PROPERTIES card is the SC05 hologram look with the four supplied icons (`Tools/generate_sc04_ui.py` turns `SC4 - icon1..4.jpeg` into `2D/Sc04/GloveCard_*.png`), eye level, right of the glove with a gap (`GloveInspect.ShowCard`). (4) SC04 "GLOVE INSPECTION COMPLETE" is a second `Sc06Gate` (`Sc04GateHost`, texts set in the builder, `Sc06Gate` has `titleText/questionText/bodyText`); `GloveInspect` no longer owns a gate (`AllSeen`), the director (`RunScenario4`) shows it after a fade to `Stand_SC04_Gate` (x of the glove table, z 3.15, whole lab in view), BACK restarts the gloves, NEXT opens the MCQ from there. (5) SC04 tables are solid steel (`Sc04SolidTable` in `.Sc04Env.cs`, same build as the SC06 table; the glove table is a follower with legs far below the floor so it never hangs when the bench rises); the glb table02 is only measured, then deleted. (6) Gloves rest 8 mm above the cloth top (the cloth is 1.55 x 0.82), `KeepGlovesOnCloth` shifts every glove inside the cloth, the black glove is grown about the pair centre (x1.25, not 1.38) so the two gloves of the pair keep their spacing. (7) SC04 and SC05 MCQ use the SC06 wow look (`Question.fancy = true`); the comet in `Sc06McqFx` is 0.72 of its old size in SC04/05/06.
- Not verified on the Quest: arrow reach over the gloves, card gap/height for seated and standing, whether the black-glove "coming out under the towel" is gone (the gloves of a pair still overlap a little inside the glb), solid table height at the extremes of `Sc04BenchHeight`, SC02 placed container size on the shelf (sizes are clamped by code, not measured).

- **Fifth round (2026-10-25, SC04 only)**: (1) the two SC04 benches now form an L like the user's photo: the glove table is stretched along +X to the wall and starts exactly at the near end of the return bench; both are solid steel with a lower shelf (`Sc04SolidTable`, movable variant: legs reach below the floor, the shelf hangs 0.7 m under the top). Both benches and everything on the return bench live under `BenchRoot` and follow the head together (`Sc04BenchHeight`, top = head - 0.32 clamped 0.75-1.1 m, so seated users do not see a high table and standing users do not see a low one); the back bench is fixed at 0.9 m. (2) A glove that is touched is carried upright (fingers up, leaning 15 degrees away) instead of lying flat: `GloveParts` follows rotation too, `GloveInspect.Hold/PutBack` tween the rotation; smoke test asserts upright while held. Smoke test 0 errors; not committed, Quest check pending.

### 14.15 Scenario 7 (factory fire, choose the extinguisher), 2026-10-25/26, rounds 1-5, all rounds committed and pushed (round 5 = `0c225a2`); the APK of 2026-10-26 02:41 has rounds 1-4, no APK with round 5 yet
Reference pack: `C:\Users\msi\Downloads\Scene07 workflow-20261025T065759Z-1-001\Scene07 workflow\` (`Step 01/02`, `step03..9.jpg`, `3D/` Waker, Factory02, Fire Alarm, fire extinguishers 1, electric panel). Starts when NEXT is pressed on the Scenario 6 MCQ (`RunScenario6` chains `RunScenario7`, `sc06Ambience.End()` is now called there). Nothing follows Scenario 7 (the completion line says "go to Scenario 8", key 8 was added in `generate_vo.py`).
- **Flow, current state** (`PresentableSequenceDirector.RunScenario7`, stages `sc07`, `sc07Intro`, `sc07Event`, `sc07Sds`, `sc07Explore`, `sc07Gate`, `sc07Question`, `sc07Extinguish`, `sc07Done`): calm empty hall + Instructions card (Bangla VO, Continue) -> **the event** (`Sc07Crowd.PlayEvent`): the fire ignites, the alarm rings, the hero and 4 workers RUN in from the back doorway along the walking lane (the hero comes round the fire, never through it), stop at irregular places and react differently (head, shocked, confused, frozen; two carry a tool), then 4 of them run along the lane to the FIRE EXIT door at the far end (the first runner opens it), the hero stays -> SDS (own `SDS_SC07.png`, 9 s or until X hides it) -> fade to the pillar stand (`Stand_SC07_Pillar`, 1.15 m from the pillar), green arrow over Water, CO2, ABC one at a time (`ExtinguisherInspect`; poke -> it comes to the hands, the other two wall units shrink away so nothing overlaps, hologram card with name / effective on / class badge, speaker replays the Bangla VO, X hangs it back) -> "EXTINGUISHER INSPECTION COMPLETE" = a third `Sc06Gate` host (BACK restarts the three) -> fade to `Stand_SC07_Question` (three extinguishers left of the MCQ, the hall right; the hero waits at `Sc07HeroWait`), MCQ "MCQ: Extinguisher Selection" (fancy, answer B = CO2, 0.7 m straight ahead at eye level, RIGHT CHOICE / WRONG CHOICE) -> **right answer**: `RunQuestion.OnAnswered` starts `Sc07Win` at once (panel hidden by scale 0, rig to `Spawn_SC07`, the hero fetches the CO2 extinguisher and puts the fire out, `VO_Sc07Hero_bn` when the flames are out, rig back to the question stand) while `PresentableMcqPanel.ShowCorrect` WAITS (`Question.holdCorrect`) and opens the RIGHT CHOICE card once afterwards (so the card sound/glow/comet play then); `AnswerVoice` plays the right-answer voice, then `VO_Sc07Complete_bn/en`. A wrong answer shows the wrong card as in the other scenarios.
- **Builder and layout**: `PresentableSceneBuilder.Sc07.cs` (room, markers, crowd/inspect/gate wiring, `Sc07SplitHorn`, `Sc07GiveTool`, `Preview SC07 (screenshots)` menu, `Dev: start at Scenario 7` menu), `.Sc07Human.cs` (skeleton fit, `Sc07People`, `Sc07Dress`), `.Sc07Fx.cs` (fire, beacon). Env at x = ox = 240, viewer faces -Z, viewer-left is +X. Hall x ox-19..ox+6.5, z -12..6. Partition wall z = -3 with the pillar (x ox+0.2..2.2) and the burning cabinet (ox-1.65) on it; to the viewer's right a walking lane (x ox-9.4..ox-3.7, yellow edge lines, green floor arrows) runs to the back wall: entry doorway at ox-4.6, hinged FIRE EXIT door at ox-7.6 under a big emissive sign (`FireExitSign.png`); machines on the right of the lane: Factory.glb blocks (long axis along Z), three big horizontal boilers and process tanks (primitives: saddles, flanges, bolts, gauge), drums, ceiling ducts, cable tray, clerestory windows. `Spawn_SC07` = (ox-0.8, 4.3) yaw 210; `Stand_SC07_Pillar` = (pillarCx, pillarFace+1.15) yaw 180; `Stand_SC07_Question` = (ox-1.0, 5.2) yaw 198; `Sc07HeroWait` = (ox-7.6, 1.2).
- **Preview without Play mode**: `Temp/sc07_views.txt` lines `name x y z yawDeg pitchDeg fov` (x, z relative to the origin; **preview yaw = 180 - game yaw**, yaw 0 faces -Z, positive turns LEFT) and optional `Temp/sc07_pose.txt` lines `index x z yawDeg mode` (index = hierarchy order, mode idle, walk, run, startle, carry, carrywalk, aim, reach, shocked, head, confused, frozen, `walk:0.25` = phase; carry/aim also set up the CO2 rig) -> `Temp/Smoke/sc07_<name>.png`. The command reloads the saved scene at its end (it used to leave the hero holding the extinguisher in the open scene, which then started Play that way).
- **People** (`Tools/prepare_sc07_models.py`): `Waker.glb` (the designer's one static T-pose worker, 386k tris) is cut into parts (head from the neck up, arms, uniform, collar, boots, helmet, eyes, lashes), decimated (quadric, UVs from the nearest original vertex) into `Worker_Hi.glb` (hero, 47k tris, uniform 24k with UVs) and `Worker_Lo.glb` (crowd, 13k); textures in `2D/Sc07Tex/Worker_*` (head, arms, uniform, boot, eye, lash). The Unity side (`Sc07MakeHuman`) fits a humanoid skeleton (joint numbers measured on the model, glb units, faces +Z, R = +X), computes smooth bone weights from the distance to the bone segments and saves skinned prefabs `Generated/Sc07/Worker_Hi/Lo.prefab` (these are regenerated by every scene build: commit them together with the scene). `Sc07Human` animates the bones with code (idle with glances, walk and run from the real ground speed, startle, four reactions, per-arm carry/pin/horn/aim/reach overlays, foot-plant boot steps): **bones are unrotated in the bind pose, a bone pose is `root rotation * delta`, the arm rest direction is the child's local offset** (never read it from world positions: the scene is saved in the idle pose). Everybody wears the original coveralls and a yellow helmet; only skin tone, height and build differ (`Sc07People`; facial hair was tried and dropped). Uniform scale only.
- **Fire / alarm / sound / CO2 rig**: `Sc07Fire` (flame flip-book column + base, black smoke, sparks, embers, light, soot decal, `Ignite/Extinguish`, CO2 jet `StartSpray` made at runtime on a nozzle transform, opaque at birth), `Sc07Beacon`, `Sc07Ambience` (hall hum, fire crackle by intensity, alarm; ducks under the voice), `Sc07Door` (hinged fire door, daylight, sound). `Tools/generate_sc07_audio.py`: synthesized fire bell, 6 walk + 6 run boot steps, fire loop, CO2 hiss, hall loop, door. Art: `Tools/generate_sc07_art.py` (SDS clipboard, icons, hazard stripes, signs, FIRE EXIT board, floor arrow, label bands, breaker face, oil stain, lighter copies of the SC5 concrete PBR `Floor07/Wall07/Ceil07`). **CO2 extinguisher rig**: the designer's CO2 model keeps its black hose + horn cone as one connected sub-mesh (`StingrayPBS1`); `Sc07SplitHorn` (builder) cuts it into a child `Co2Horn` with markers `HornTop/HornGrip/HornMouth` and a `BodyGrip` on the body, because imported meshes are not readable at runtime on Quest (never scan mesh vertices at runtime). `Sc07Crowd.HeroExtinguish` (execution order 100, so it runs after `Sc07Human` poses the bones): right hand takes the extinguisher from the wall -> it hangs from the LEFT hand socket (`ApplyRig`) -> walk to `Sc07SpraySpot` -> pin pull -> the horn goes to the RIGHT hand aimed at the fire with a procedural hose (LineRenderer, `hoseMaterial`) -> spray (sweep +-12 degrees, one small step closer, jet from `HornMouth` along the horn axis) -> horn back on the body. The three wall extinguishers are the designer's own; the glb faces +X, the builder turns each node -90 degrees about Y and scales 0.37 (0.74 m).
- **Voice**: `VO_SC07_Instruction_bn` (respelled শঠিক like SC06), `VO_Sc07Ext0..2_bn`, `VO_Sc07Gate_bn`, `VO_Sc07Hero_bn`, `VO_Sc07Complete_bn/en`; image `BN_Sc07InstructionPanel`. Generated, **not listened to**.
- **Smoke test**: `Sc07Smoke` (chained after `Sc06Smoke`; with the dev menu on, `Main` runs only Sc07Smoke): instruction, event screenshots every 2.5 s, door open after the flight, SDS, real fingertip attempts (desk falls back to the direct event), card text and layout, BACK once, MCQ placement (0.70 m ahead, 0 sideways, +0.03 m height), wrong (foam) then right (CO2), the hero extinguishes (fire intensity must reach 0, log of the horn separation from the body), completion voice. Screenshots `Temp/Smoke/60..74_sc07_*` (look at `66_*card*` for overlaps, `70_*question`, `73_*hero*`). Workflow: toggle `Dev: start at Scenario 7` ON, `Run Smoke Test`, toggle OFF. **Keep the dev menu OFF** when running the full smoke test or building. The Unity console is cleared when Play ends: read it right after the last stage or rely on the screenshots. The first Unity refresh after a script edit often times out; poll `EditorApplication.isCompiling` and wait for the scene file time after `Build Presentable Scene`.
- **Round 2 (2026-10-25, user feedback on SC07)**: (1) "আগুন নিভে গেছে" is now played only when the flames are out (`Sc07Crowd.FireOut` event, raised in `HeroExtinguish`; the director plays `VO_Sc07Hero_bn` from it). (2) "বড়" is respelled "বড়ো" in `PRON` of `generate_vo.py` (key `"বড় "` with the trailing space, whole word only), hero clip regenerated. (3) 12 people (hero + 11, `Sc07People`), 11 notice spots in three rows, workers walk in through the entry door (x0, z 3) and flee through a second door further back (x0, z -4.6) that is visible from the first view; machines moved back (z -10.5) so the flee path is open. (4) Running away is staged: one worker alone, two more one after the other, then the rest in groups (`Sc07Crowd.PlayEvent`, delays in the loop); the whole event takes about 28 s. (5) First view `Spawn_SC07` = (ox-0.8, 4.3) yaw 208 (crowd centred, cabinet left of centre, pillar at the left edge). (6) More life: contact shadow under every worker, standing workers glance about and shift weight (`Sc07Human.Evaluate`), calmer floor/wall textures. Smoke test 0 errors (SC07-only and full run before the last tweaks; the full run was not repeated after round 2).
- **Round 3 (2026-10-25, user feedback: robot walk, one line, everybody the same, MCQ to the side)**: (1) 5 people (hero + 4: `Sc07People`). (2) `Sc07Human` rewritten for a natural gait: pelvis against shoulders, hip sway and bounce (lowest at double support), knee flexion in stance and swing, heel-to-toe foot, forearm lag, steady head, a personal style per worker from the seed (stride, swing, lean, bounce), `WalkTo` has a 0.6 s acceleration, speed noise and a slow-down for the last metre (walking only), `WalkPath` for curved paths. (3) Four different reactions (`Sc07Reaction`: Head, Shocked, Confused, Frozen; `SetReaction`), the hero stays calm; bystanders enter on own lanes at own speeds with 0.8-1.6 s gaps, curved paths, four irregular stopping places (builder `noticeXZ`), flee in order: head man alone, shocked man 1.7 s later, then confused and frozen together. (4) The flee door is a hinged green "FIRE EXIT" door (`Sc07Door`, builder block "FIRE EXIT in the right wall", `FireExitSign.png`, `Door_Open.wav`): the first runner opens it about 3 m before it (daylight panel + white light behind it). Wall quads must face +X: rotation Y = -90 (the old +90 faced the wall). (5) The SC07 MCQ is placed straight ahead at eye level, 0.7 m (the side turn was removed; the right/wrong cards share the centred canvas); smoke test logs the placement (distance, sideways, height). Pose preview modes `shocked`, `head`, `confused`, `frozen` were added to `Temp/sc07_pose.txt` (note: the worker order there is the hierarchy order, not `Sc07People`). Smoke test (SC07-only) 0 errors; the full run was not repeated after round 3.
- **Round 4 (2026-10-26, user feedback: double right-answer popup, layout like the workflow images, run, sign, realism, tools, gestures)**: (1) The right-answer card opens ONCE, after the fire is out: `PresentableMcqPanel.Question.holdCorrect` (non-serialized Func) makes `ShowCorrect` wait; `RunScenario7` sets it, `RunQuestion.OnAnswered` starts `Sc07Win` (panel hidden by scale 0, not SetActive, so the panel coroutine survives), card sound/glow/comet (`_fx.OnResult`) now play when the card really opens; the hero line no longer says the word for 'correct' (`VO_Sc07Hero_bn` regenerated). (2) Layout like the reference: hall x0 = ox-19, back wall z = -12; walking lane x ox-9.4..ox-3.7 (yellow edge lines, green floor arrows `FloorArrow.png`), machine blocks (Factory.glb, long axis along Z) + three big horizontal boilers (primitives with saddles, flanges, bolts, gauge) + process tanks on the right, entry doorway at ox-4.6 and the hinged FIRE EXIT door straight ahead at ox-7.6 on the back wall (new 1024x384 sign, 2.6 m, emissive, `FireExitSign.png`); clerestory windows, ceiling ducts, cable tray, wall pipes. People come in along the lane (hero comes round the fire, never through it) and run away along it. Spawn yaw 210. (3) People run in (4.3-5 m/s, 0.25-0.5 s gaps, 0.7 s after the alarm), `WalkTo` slows for the last metres at any speed; run cycle fixed (knee flexion peaks early in the swing, crisp walk/run change, bounce lowest at mid-stance when running); two workers carry tools (toolbox, wrench: `Sc07GiveTool`); reactions have eased, staggered arm weights (second hand follows), tremble, chin tap, shrug. (4) Everybody wears the original Waker coveralls (texture `Worker_Uniform.jpg`, `Worker_Boot.jpg`, yellow helmets), only skin tone/height/build differ; no facial hair (tried, user did not like it). Hero LOD raised to 47k triangles (Uniform 24k with UVs) so it looks like Waker.glb. Preview note: `Temp/sc07_views.txt` yaw = 180 - game yaw. CO2 jet fix: the black horn/hose of the CO2 model is the `StingrayPBS1` sub-mesh at the model's local +X, hanging down along the cylinder; the jet starts at its lower mouth (`_hornLocal`, `Sc07Crowd.CaptureCo2`) and the carried extinguisher is tipped 52 degrees (`carryEuler` x = -52, +8 when aiming) so the horn points forward/down at the fire (the first attempt used the gooseneck at -X: wrong). The CO2 jet starts opaque at the horn (gradient alpha 0.8 at birth). **Gotcha:** `Preview SC07` poses workers and parents the CO2 extinguisher to the hero in the open scene; it now reloads the saved scene at its end, but never press Play right after a preview in an older build (the hero would start holding the extinguisher). Smoke (SC07-only) 0 errors, dev menu OFF again.
- **Round 5 (2026-10-26, Quest video feedback)**: (1) Inspect: stand B is now 1.15 m from the pillar (poke volumes padded +0.20/+0.45), the held extinguisher sits lower-left, the card further right, and the OTHER wall extinguishers shrink away while one is in the hands (`ExtinguisherInspect.ScaleOthers/ShowOthers/RestoreAll`), so nothing overlaps behind it. (2) CO2 spraying: the builder cuts the black hose + horn cone (`StingrayPBS1` sub-mesh, one connected piece) out of the CO2 body into a child `Co2Horn` (`Sc07SplitHorn`, meshes `Generated/Mesh_Sc07Co2Body/Horn.asset`; markers `HornTop/HornGrip/HornMouth`, `BodyGrip`), because imported meshes are not readable at runtime on Quest. `Sc07Crowd` (execution order 100, after the bones are posed) hangs the body from the LEFT hand socket and, after a pin-pull, moves the horn to the RIGHT hand aimed at the fire, with a procedural hose (LineRenderer, `hoseMaterial`) from the valve; sweep +-12 degrees, one small step closer, horn back on the body afterwards; jet starts at `HornMouth` along the horn axis. `Sc07Human` has per-arm overlays (`SetCarry` = left holds, `SetPin`, `SetHornReach`, `SetAim` = left holds + right aims) and `LeftSocket`. Smoke logs `horn separated from the body`. (3) MCQ: the player is moved to `Stand_SC07_Question` (ox-1.0, 5.2, yaw 198) so the three extinguishers are left of the MCQ and the hall right of it; the hero waits at `Sc07HeroWait` (lane, right of the MCQ); `Sc07Win` returns to this stand. Committed in `0c225a2`, no APK of this round.
- **Not done / not verified**: Quest check of everything (arrow reach at the pillar, card heights seated/standing, workers' look and walk, footstep and alarm loudness, fire and smoke cost with 6 skinned people), listening check of the Bangla clips, a Presentable APK that includes SC07. The hall is close to but not identical with the designer's pictures (concrete, floor and tanks are our own materials; the tanks are small and far).

### 14.16 Scenario 8 (acid dilution: PPE, fume hood, H2SO4 into water), 2026-10-26/27, rounds 1-2, committed locally (round 1 = `bd90013`, round 2 realism = `f4011c2`), NOT pushed, no APK yet
Reference pack: `C:\Users\msi\Downloads\Scene08 workflow-20261026T152746Z-1-001\Scene08 workflow\` (`Workflow/step01-13`, `3D/Safety Shower.glb` (the only 3D file), `2D resources/` Acid.png, Water.png, SDS Sheet.png, Wall poster.png). Starts when NEXT is pressed on the Scenario 7 MCQ (`RunScenario7` ends with `RunScenario8`; `doneLine` and `AnswerVoice` have an scenario-8 branch; the SC08 completion lines are custom because nothing follows).
- **Flow** (`PresentableSequenceDirector.RunScenario8` + `Sc08Round`, stages `sc08`, `sc08Intro`, `sc08Ppe`, `sc08Hood`, `sc08Tray`, `sc08Sash`, `sc08Mix`, `sc08MixDone`, `sc08Gate`, `sc08Question`, `sc08Done`): instruction card + Bangla VO + Continue -> fade to `Stand_SC08_Ppe` (right wall, 0.85 m from the board) -> **PPE Donning Station**: arrow over Splash Goggles, Respirator, Neoprene Gloves, Acid-resistant Apron one at a time (`Sc08Ppe`: poke -> item flies to the user, is put on, the row of the hologram PPE SELECTION PANEL gets a check mark) -> `Stand_SC08_Panel`: arrow over the Fume Hood switch (the SC06 `Sc06Lab` is reused as is: switches, LEDs, airflow, hum, sash) -> `Stand_SC08_Bench`: arrow over the white transparent tray (`Sc08Mix`: poke -> carried in front of the user, follows the head) -> `Stand_SC08_Hood`: touch the glass sash (refused with a red flash while the hood is off), tray is set down in the hood (`Sc08Mix.PlaceInHood`) -> green ring on the Distilled Water beaker, poke: the acid beaker lifts, tilts 62-69 degrees over the water, a thin wobbling stream, splash + ripples, water level rises (0.55 -> 0.84), acid falls (0.70 -> 0.20), a glass rod (RodA, the second rod stays) stirs in a circle on the +X side, heat haze, then it all settles -> sash closes, fade to `Stand_SC08_Table`, gate card **DILUTION PRACTICE COMPLETE.** (a `Sc06Gate` host with `titleText/questionText/bodyText`; BACK restarts from the PPE step with a reset lab) -> MCQ "MCQ: Acid Dilution" (fancy look, 0.7 m ahead at eye level, correct B = index 1, rows are named Row1..Row4 in the smoke test) -> `sc08Done`.
- **Builder**: `PresentableSceneBuilder.Sc08.cs` (room, hood, panel, table, tray, wiring, `BuildSc08Room`, env origin x = 280, same shell dimensions and SC06 PBR textures as SC06, warm cream walls `Sc08WallBackPbr`), `.Sc08Ppe.cs` (PPE board on the right wall + procedural goggles / gloves / apron + the half-face respirator node `Cylinder.002` of `Masks.glb`), `.Sc08Dev.cs` (menus *Dev: start at Scenario 8* (EditorPrefs `ChemSafetySc08Only`, keep OFF) and *Preview SC08 (screenshots)* with `Temp/sc08_views.txt`, lines `name x y z yawDeg pitchDeg fov`, **preview yaw = 180 - game yaw**). Gotchas: `QuadObj` takes a LOCAL position (use `Sc08QuadWorld` for parts of a moved parent), `Sc06Mesh` sets the local scale (pass `Vector3.one` under a scaled parent), a group pivot must sit at the item before it is rotated.
- **Resources**: `Tools/generate_sc08_art.py` (SDS sheet -> `2D/Sc08/SDS_SC08.png`; poster, Acid / Water labels, PPE board title and captions, green SAFETY SHOWER / EYE WASH signs, notebook -> `2D/Sc06Tex/Label_Sc08*`), `Tools/generate_vo.py` clips `VO_SC08_Instruction`, `VO_Sc08Ppe/PpeDone/Hood/HoodOn/Sash/HoodWarn/Mix/Gate`, `VO_Sc08Complete_bn/en` (instruction respelled **শঠিক**), `Tools/render_bangla_text.py` `BN_Sc08*`, `3D/Sc08/SafetyShower.glb` (copy of the designer's file, scaled to 2.45 m, yellow as supplied). The goggles, gloves and apron are our own procedural models (the designer supplied none); the apron is simple.
- **Scripts**: `Sc08Content`, `Sc08Ppe`, `Sc08Mix`; `Sc06Lab.vaporScale` (new field, 0.55 for the acid beaker: the vapor comes from the `Mouth` transform on the acid beaker); art slots `sdsSc08`, `bnSc08*`, `voSc08*` in `PresentableArt`.
- **Smoke test**: `Sc08Smoke` (chained after `Sc07Smoke`; dev menu: SC08-only run): four PPE pokes with check marks, hood switch, tray carried (0.6 m from the head), sash, tray in the hood, mix (stream, stirring, fill levels, acid beaker upright), gate card, BACK once, second full round, MCQ wrong (Row1) then right (Row2), completion voice. Screenshots `Temp/Smoke/75..87_sc08_*`. The SC08-only run ended with `runtime errors/exceptions = 0` (the "real fingertip did NOT fire" lines are normal at the desk).
- **Not done / not verified**: Quest check (poke reach of the PPE board from 0.85 m, the carried tray height for seated and standing users, hand animation: the pour and the stirring are scripted objects, the real hands are not driven, only the left glove cradles), listening check of the Bangla clips, the full-flow smoke test after the last changes (see the end of this section), a Presentable APK with SC08. Pre-existing and not from SC08: three `Setting the parent of a transform which resides in a Prefab instance` console errors in `BuildGlassCabinet` (SC02) at every build.

- **SC08 round 2 (2026-10-26, realism)**: (1) PPE models: gloves = the designer's `gloves1.glb` (Scenario 4) pieces `Plane.009/.004` tinted neoprene green and stood upright (`Sc08GlovePair`, flip flags `Sc08GloveFlipFront/Up`), goggles = curved wrap-around lens + rubber rims + foam + vents + strap + buckles, new face shield (curved acrylic visor, brow bar, headband, foam pad, ratchet), apron = hanging cloth mesh with folds, bib/armhole shape, pockets with stitching, ties, grommets, neck strap (`Sc08Apron`, texture `Apron_Albedo`); slot 0 is now "Goggles / Face Shield"; wall pegs instead of cubes, shelves under goggles and gloves, respirator hangs from its peg. Helpers in `PresentableSceneBuilder.Sc08Geo.cs` (`Sc08ArcShell`, `Sc08SweepTube`, `Sc08Cloth`). (2) Furniture (`.Sc08Furniture.cs`): island and wall counter are fully procedural now (carcass with shadow gaps, drawer + door panels, handles, kick plates, black epoxy top with rolled glossy edges), the counter has a real sink opening, steel basin, drain, gooseneck tap with levers, splashback. (3) Dressing (`.Sc08Dress.cs`): window blinds, exit door with vision panel / kick plate / lever / closer / EXIT sign, fire alarm pull station, first aid box, paper towel and soap dispensers, sockets and switch, sprinklers, floor wear decals, shower drain with yellow ring, a baked reflection probe `Generated/Sc08Probe.exr` (`Sc08BakeProbe`). Smoke test SC08-only: 0 errors. Scenarios 1-7 code untouched in this round.

- **How SC08 was verified and how to work on it** (2026-10-27): (a) build with menu *Build Presentable Scene*, preview with *Preview SC08 (screenshots)* (`Temp/sc08_views.txt`), then toggle *Dev: start at Scenario 8* ON, *Run Smoke Test* (SC08 only, about 3 min), toggle OFF; the full run (SC01 -> SC08) also passed with `runtime errors = 0` after round 1 (not repeated after round 2). (b) **Scenarios 1-7 were checked not to change**: a temporary editor script dumped every object of the old (HEAD) and new scene (path, local position/rotation/scale, components, material and mesh names, light intensity) and diffed them: identical except the saved idle bone pose of the five SC07 workers (1-3 degrees, overwritten at runtime) and the new `McqPanel_SC08`. The temp scene/script were deleted. The generated SC07 worker prefabs must be committed **together with** the scene (regenerated prefabs have different internal ids; an old scene with new prefabs shows null head materials). (c) Unity noise that must NOT be committed: `Mesh_Sc03BurstDrum.asset`, `Icon_Close.png.meta`, regenerated `M_Sc0x*.mat` of old scenarios, `.utmp/`, `ProjectSettings/`, `Assets/Settings/`, `Assets/Samples/`. (d) **Never edit scripts while Unity is in Play mode** (one edit during Play in this session produced an `IndexOutOfRange` in `Sc08Ppe.ResetAll`; the clean run did not; `ResetAll` now also guards arrays that are still empty before `Start`). (e) Automation loop used from the shell: `python Tools/mcp_http.py execute_menu_item '{"menu_path":"..."}'`, then poll `read_console` for `[Presentable] Built` (build) or count `[SMOKE] DONE` lines in `Logs/Editor.log` (old runs stay in the log). Compile with `refresh_unity` (compile request) and wait until the editor answers again.
- **Uncommitted edits by another session found on 2026-10-27** (not made by the SC08 work, left untouched): `Tools/generate_vo.py` (a `PRON` entry for প্রয়োজনীয়, `TO_NUM[9]`/`EN_NUM[9]`, and `VO_Sc08Complete_bn/en` changed to `complete_bn(8)`/`complete_en(8)`, which says "go to Scenario 9" although nothing follows SC08 - decide on purpose), `Tools/generate_sc08_art.py` (a different notebook page texture). Check `git diff` before committing them.

### 14.17 Scenario 9 (chemical splash on the face: broken flask, SDS, safety shower / eye wash), 2026-10-27, rounds 1-2, committed locally (`25dd6d2` base, `c07789f` round 2), NOT pushed, no APK yet
Reference pack: `C:\Users\msi\Downloads\Scene09 workflow-20261027T055804Z-1-001\Scene09 workflow\` (`Workflow/step01-13.jpg`, `3D/chemical lab 02.glb`, `2D resources/` board writings, flask level 1-4, SDS). Starts when NEXT is pressed on the Scenario 8 MCQ (`RunScenario8` ends with `RunScenario9`; the SC08 completion voice already says "go to Scenario 9"; SC09 has its own ending, `doneLine` / `AnswerVoice` have a scenario-9 branch). Nothing follows SC09.
- **Flow** (`PresentableSequenceDirector.RunScenario9` + `Sc09Round`, stages `sc09, sc09Intro, sc09Flask, sc09Spill, sc09Sds, sc09Walk, sc09Wash, sc09WashDone, sc09Gate, sc09Question, sc09Done`): instruction card + Bangla VO + Continue -> fade to `Stand_SC09_Bench` (island table, the user faces +X) -> arrow over the purple **B-Sample 2** flask, poke -> it lifts, tilts over B-Sample 1, thin purple stream (2 s) -> it **slips**, falls, hits the bench and **breaks** (jagged base + neck lying, 12 shards fly and settle, puddle spreads, splatter, drops burst up and spray at the face, glass-break + splash sounds, haptic) -> the **face shield is covered** (`Sc09Visor`: a quad on the head camera, 0.32 m ahead, droplet mask made at runtime, shader `Assets/Shaders/Sc09Visor.shader`, no grab pass) -> "INSTRUCTIONS" card (step06 text, Continue) -> SDS clipboard in the left hand (`SDS_SC09.png`, the designer's sheet with the typo "procenees" repainted; X or 10 s hides it) -> fade 2.4 m behind `Stand_SC09_Shower`, green **STAND HERE** floor mark, glide to the stand -> arrow on the eye wash **push plate** (the green plate of the designer's shower model), poke -> valve click, shower head + two eye wash jets + bowl/floor splash + mist, water loop; the shield is washed (`Sc09Visor.Wash`, 12 s: film with running streaks, drops lose their colour and shrink away) and the hologram "HOLD Eyelids OPEN ... 15 MINUTES" hangs 1 m ahead -> gate card (a `Sc06Gate` host, title "EYEWASH AND SHOWER INSPECTION COMPLETE.", BACK restarts the whole round, `Sc06Gate` title min font was lowered 30 -> 20 so this long title fits) -> MCQ "EMERGENCY PROTOCOL ASSESSMENT" (fancy look, correct B = index 1, RIGHT ANSWER "Standard first-aid for chemical eye exposure", wrong A "Wiping spreads contamination ..." as in step13).
- **Builder**: `PresentableSceneBuilder.Sc09.cs` (refs, `BuildSc09Room`), `.Sc09Env.cs` (`Sc09Shell`: the designer's `ChemLab02.glb` scaled 0.45 with **all glb materials replaced** by URP Lit / glass ones in `Sc09Remat` because they are fully metallic shader-graph materials that render black without probes; own floor, +Z and -X walls, ceiling, 9 light panels; measures the shower parts by their glb material names (`HandSign_MAt` = push plate, `Material` = nozzles, `ShowerSign_MAT` = pole, high vertices far from the pole = shower head); whiteboard from `board writings.png` and the two green bilingual signs on the back wall (`backWallZ` -3.21); `Sc09Logic` creates guide, visor, ambience, gate, flask and shower hosts and their clip / material refs), `.Sc09Props.cs` (island table, 3 flasks with the designer's B-Sample labels (flask 2 purple), tray with the Scenario 4 glove model in blue nitrile and the SC08 goggles, notebook, pen, timer, test-tube rack, the hidden `Sc09Broken` group with `Sc09JaggedShell` meshes, `Sc09ShardMesh`, puddle and splatter decals, shower push plate, poke volume, water particle jets `Sc09Jet`), `.Sc09Dev.cs` (menus *Dev: start at Scenario 9* (EditorPrefs `ChemSafetySc09Only`, keep OFF) and *Preview SC09 (screenshots)* reading `Temp/sc09_views.txt`, `name x y z yawDeg pitchDeg fov`, preview yaw = 180 - game yaw; extra switches: file `Temp/sc09_broken.txt` poses the broken flask (the scene is reloaded afterwards), `Temp/sc09_water.txt` simulates the water). Env origin x = 320; the glb room is x +-3.5, z +-3.5, height 3.02; the counters of the glb are along +X, the shower on the -Z wall at x +0.98.
- **Scripts** (`Presentable/`): `Sc09Content`, `Sc09Flask` (scripted ballistics, no rigidbodies; the pour direction and the fall direction are computed from the flask / camera positions), `Sc09Visor`, `Sc09Shower`, `Sc09Guide` (the green arrow, copy of the `Sc06Lab` one); art slots `sdsSc09`, `bnSc09*`, `voSc09*` in `PresentableArt`.
- **Assets**: `Tools/prepare_sc09_models.py` (glb 17 MB -> `3D/Sc09/ChemLab02.glb` 6.9 MB, textures downsized, geometry untouched: Mesh_156 is 101k triangles, **not decimated yet**), `Tools/generate_sc09_art.py` (SDS, labels, board, hologram text `Label_Sc09Holo`, STAND HERE, puddle and splatter, bilingual signs; run with `python -I`), `Tools/generate_sc09_audio.py` (`Art/Audio/Sc09`: glass break, knock, splash, water loop, valve), `Tools/generate_vo.py` clips `VO_SC09_Instruction_bn` ... `VO_Sc09Complete_bn/en` (edge-tts, generated, **not listened to**), `Tools/render_bangla_text.py` `BN_Sc09*`.
- **Smoke test**: `Sc09Smoke` (chained after `Sc08Smoke`; dev menu: SC09-only run): flask arrow, real fingertip poke at the flask, lift, break, visor dirty, spill card, SDS, push plate poke, jets running, hologram, wash done / visor clean, gate card, BACK once + second round, MCQ wrong (Row1) then right (Row2), completion voice. Screenshots `Temp/Smoke/88..102_sc09_*`. SC09-only run and the full run (SC01 -> SC09, dev menu OFF): `runtime errors/exceptions = 0`, completion voices of scenarios 2-9 heard (earlier full-run attempts of this session stopped by themselves after 1-4 minutes, Play mode was ended from outside; a plain re-run passed).
- **Shower floor drain (2026-10-27)**: recessed steel rim + dark sump + slotted grate under the eyewash (`Sc09Drain` in `Sc09Shower`, at `bowl.z + 0.38` on the ChemLab02 shower pad via `Sc09ProbeShowerFloorY` ~0.145 m; `FloorSplash` shares that spot). Decorative only; no flow change.
- **Not done / not verified**: Quest check (poke reach of the flask from `Stand_SC09_Bench` 0.65 m and of the push plate from `Stand_SC09_Shower` 0.88 m, the head-locked visor quad in the headset (comfort, stereo, strength), frame rate: lab glb 101k + 57k glass triangles undecimated, particles), listening check of the SC09 Bangla clips, the real hands are not driven (the pour, the slip and the wash are scripted objects), the designer's lab is close to but not identical with the pictures (dark steel instead of the white/blue look, the loose glassware on the counters belongs to the glb and cannot be moved), a Presentable APK with SC07 round 5, SC08 and SC09.

- **SC09 round 2 (2026-10-27, user feedback)**: (1) **Ending voice**: SC09 uses the shared line like every scenario (`VO_Sc09Complete_bn/en` = `complete_bn(9)/complete_en(9)`: "...Scenario nine... Click Next to go to Scenario ten"); `NUM[9]`, `TO_NUM[10]`, `EN_NUM[10]` were added to `generate_vo.py`. "প্রশিক্ষণ শেষ / The training is complete" must be said ONLY at scenario 22 (see §15). The on-screen `Sc09Content.ModuleComplete` is the generic "Module complete. Well done!". (2) **Hologram text** ("HOLD Eyelids OPEN ... 15 MINUTES") was a plain transparent quad and did not show in the Quest headset (it showed in the editor); it is now a runtime world canvas made by `Sc09Shower` (`PresentableUi.WorldCanvas` + `AlwaysOnTop`, sprite `2D/Sc09/Holo_EyeWash.png`, art slot `holoSc09`), placed 1 m ahead; the canvas must face `LookRotation(forward)` (not `-forward`, that mirrored the text). Quest result not yet reported by the user. Lesson: world content that must always be visible in the headset should use the `PresentableUi.WorldCanvas` + `AlwaysOnTop` pattern, not a plain quad. (3) **More realistic glassware**: own flask mesh `Sc09FlaskMesh` (3 mm wall, rounded heel, beaded rim), curved paper labels wrapped on the cone (`Sc09CurvedLabelMesh`, designer's B-Sample images), beaker, measuring cup and the designer's Alcohol / NaOH / Phenyl bottles (`Sc06ChemGroup`) on the island. (4) Smoke test: SC09-only runs 0 errors; the last full run SC01-SC09 (before round 2) 0 errors. Dev helpers `Tools/sc09_cycle.sh` (compile, build, preview) and `Tools/sc09_wait.sh` (wait until the editor is idle) are untracked on purpose. Tooling notes: python on this machine is `python`; the Bash tool breaks on heredocs that contain apostrophes (use the Write tool); a smoke run can be ended from outside (rerun it); never call the Unity MCP while Play mode runs.

### 14.18 Scenario 10 (acid spill response: SDS, PPE, ventilate, spill kit, contain / neutralize / absorb / clean glass / dispose), 2026-10-27/28, NOT committed yet, no APK
Reference pack: `C:\Users\msi\Downloads\Scene10 workflow-20261027T155559Z-1-001\Scene10 workflow\` (`step01-15.jpg`, `2D resource/` = the five neon icons + `SDS10.png`, no 3D file). Starts when NEXT is pressed on the Scenario 9 MCQ (`RunScenario9` ends with `RunScenario10`; `doneLine` / `AnswerVoice` have a scenario-10 branch). The shared completion line says "go to Scenario eleven" (nothing follows yet, same as the SC08 -> SC09 precedent).
- **Flow** (`PresentableSequenceDirector.RunScenario10` + `Sc10Round`, stages `sc10, sc10Intro, sc10Sds, sc10Ppe, sc10Vent, sc10Kit, sc10Collect, sc10Contain, sc10Neutralize, sc10Absorb, sc10Clean, sc10Dispose, sc10Disposed, sc10Gate, sc10Question, sc10Done`): instruction card (reuses `Sc09Card`) -> SDS clipboard (`SDS_SC10.png`) -> PPE Donning Station (SC08 `Sc08Ppe` as is) -> fume hood switch (SC08 hood + `Sc06Lab` copied into the SC10 builder) -> spill kit door (poke) -> five items (poke each, hologram checklist `SPILL KIT COLLECTION`) -> five neon icons that float 0.62 m in front of the user and are poked: CONTAIN (absorbent sock laid round the spill as a runtime tube mesh, the free end swings in and settles), NEUTRALIZE (soda ash bag tilts, powder stream + fizz particles, powder decal grows, puddle shrinks/fades), ABSORB (4 pads flutter down one by one and darken, puddle gone), CLEAN-UP GLASS (brush strokes sweep 18 glass pieces into the dustpan), DISPOSE (sock + pads + pan glass fly into the yellow bag, neck twists shut, bag goes into the bin, lid closes) -> gate card (`Sc06Gate`, BACK restarts the whole round) -> MCQ "MCQ: Spill Response" (fancy look, correct B = index 1; wrong A -> WRONG ACTION text from step15).
- **Builder**: `PresentableSceneBuilder.Sc10.cs` (shell, tile floor, concrete left wall, hood/panel/Sc06Lab block generated from the Sc08 builder lines by `scratchpad/gen_sc10_builder.py`, gate, guide, PPE station, `Sc10Bay`), `.Sc10Props.cs` (red SPILL KIT with glass door and items, bag mesh `Sc10BagMesh` (clamp the sine, a NaN there made empty meshes), puddle quad, bottle pieces, shards, pads, dustpan/brush, drain, CAUTION sign, desk, bin with lid, open waste bag, `Sc10SpillLogic` host), `.Sc10Dev.cs` (menus *Dev: start at Scenario 10* (EditorPrefs `ChemSafetySc10Only`, keep OFF) and *Preview SC10 (screenshots)*, `Temp/sc10_views.txt`, `name x y z yawDeg pitchDeg fov`, preview yaw = 180 - game yaw). Env origin x = 360. Stand markers: `Spawn_SC10`, `Stand_SC10_Spill` (x1-3.3, z 1.3, faces +X), `Stand_SC10_Kit`, `Stand_SC10_Ppe`, `Stand_SC10_Panel`.
- **Scripts** (`Presentable/`): `Sc10Content`, `Sc10Spill` (all animations, scripted, no rigidbodies; the icon canvas must keep its world scale 0.00085, never set it to 1), `PresentableArt` slots `sdsSc10`, `iconSc10*`, `bnSc10*`, `voSc10*`. The guide arrow host is scaled 0.55.
- **Assets**: `Tools/generate_sc10_art.py` (16 `Label_Sc10*` textures in `2D/Sc06Tex`), `Tools/generate_sc10_audio.py` (`Art/Audio/Sc10` Fizz/Powder/Sweep/Rustle, procedural), `Tools/generate_vo.py` clips `VO_SC10_Instruction_bn`, `VO_Sc10Sds/Ppe/Vent/Kit/Collect/Contain/Neutralize/Absorb/Clean/Dispose/Gate_bn`, `VO_Sc10Complete_bn/en` (NUM[10], TO_NUM[11], EN_NUM[11] added; **never listened to**), `Tools/render_bangla_text.py` `BN_Sc10*`. Designer icons copied to `2D/Sc10/` (Icon_Contain, Icon_Neutralize, Icon_Absorb, Icon_Clean, Icon_Dispose, SDS_SC10). Dev helpers `Tools/sc10_cycle.sh` (compile, build, SC10-only smoke) and `Tools/sc10_full.sh` are untracked on purpose.
- **Smoke test**: `Sc10Smoke` (chained after `Sc09Smoke`; dev menu: SC10-only run): SDS, 4 PPE pokes, hood switch, kit door, 5 items, the five icons with state checks (sock laid, neutralized, absorbed, glass cleaned, disposed), gate BACK once, second round, MCQ wrong (Row1) then right (Row2), completion voice. Screenshots `Temp/Smoke/103..118_sc10_*`. SC10-only runs: `runtime errors/exceptions = 0`.
- **Not done / not verified**: Quest check (reach of the kit door/items from `Stand_SC10_Kit` 0.75 m, icon 0.62 m in front, scale of the neon icons, frame rate), real hands are not driven (everything is scripted objects), listening check of every `VO_Sc10*` clip (words: spill, neutralize, absorbent sock, dispose, broom), the designer's room is only approximated (no real window/door, concrete wall instead of the exact picture), the puddle is a decal (no refraction), a Presentable APK with SC07 round 5 + SC08 + SC09 + SC10.

## 15. Roadmap for the whole game (stated by the user on 2026-10-27; NOT built yet, do not start without being asked)
- **22 scenarios in total.** Scenarios 1-10 exist (SC10 = §14.18). Scenario 11 is next (the user will bring a new reference pack: `Workflow/`, `3D/`, `2D resources/`, same routine as §14.14-14.17). Every scenario ends with the shared completion line (`complete_bn(n)` / `complete_en(n)`, only the number changes; add the numbers to `NUM / TO_NUM / EN_NUM`). **Only scenario 22 says "প্রশিক্ষণ শেষ / the training is complete".** When a scenario is added, the previous last scenario's `AnswerVoice` / `doneLine` branches and the chain (`RunScenarioN` ends with `RunScenarioN+1`) must be updated the way SC08 -> SC09 was.
- **Login page at the very start (user will describe it later)**: user name plus e-mail OR phone number to start the game; Bangla + English language choice, and the voice-overs must follow the chosen language (so every clip needs a Bangla and an English version; Bangla stays Bangladeshi, see the voice rules).
- **Marking**: a mark is shown after every scenario; at the very end the total mark is shown.
- **Server upload**: at the very end an external API call sends the details (user, marks per scenario, total) to a server. Endpoint, payload and auth will be given by the user later.
- These four items are only hints for planning (keep marks per question, a language setting and a results object in mind when touching the flow); no code exists for them.

### OPEN ITEMS TO REMIND THE USER (do NOT work on these until the user asks)
The user said (2026-10-24): "I will tell you these later; remember them and remind me, but do not work on them unless I ask." At the start of a session, mention this list once in Bangla and wait.
1. **Scenario 4 look**: bright specular streaks on the epoxy floor, the cloth under the gloves is a flat white card, the lab still has fewer props than the designer's `Step01.jpg`. The user will add more SC04 problems later.
2. **Quest checks not yet reported by the user**: the 2026-10-24 APK (SC02 details card distance and single placement popup, SC03 no blast after Continue, SC04 glass-door fridge contents, AC 25°C, SDS, gloves resting on the cloth).
3. **Voice**: listen check of every new Bangla clip (Scenario 5, 6, 7 and 8 included; the SC08 clips `VO_SC08_Instruction`, `VO_Sc08Ppe/PpeDone/Hood/HoodOn/Sash/HoodWarn/Mix/Gate/Complete` were never listened to).
4. **Scenario 8 on the Quest**: poke reach of the PPE board from `Stand_SC08_Ppe` (0.85 m), tray carry height for seated and standing users, draw calls / frame rate after the realism pass (not measured), whether the apron (mesh with folds, no cloth simulation) and the procedural goggles / face shield look good enough, the pour and stir are scripted (real hands are not driven). Also: a Presentable APK that includes SC07 round 5 and SC08 does not exist yet.
5. **Not pushed**: the branch `feature/Implement-Scenario-wise-Marking-and-UI-adjustment-feedback-and-Scenario7` is ahead with the two SC08 commits; push only when the user says so. ElevenLabs is paused (HTTP 402); edge-tts is used.
6. **Scenario 9 on the Quest** (2026-10-27): the hologram text "HOLD Eyelids OPEN... 15 MINUTES" (now a world canvas, previously invisible in the headset), the head-locked visor drops (comfort, stereo, strength), poke reach of the flask (0.65 m) and the push plate (0.88 m), frame rate with the undecimated lab glb, listening check of all `VO_Sc09*` clips, a Presentable APK with SC07 round 5 + SC08 + SC09. The user said they will check in VR and report.
7. **Roadmap** in §15 (22 scenarios, login with Bangla/English, marks, server API): the user will give the details later; do not start before being asked.
