# ChemicalSafetyVR1 — Project Handoff & Memory

**Last updated:** 2026-10-14  
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

---

*If you update the demo significantly, append a row to §13 and refresh §5 / §9 so the next machine stays accurate.*
