# DazPoseWizard Code Map (Sprint 1 wrap-up)

Purpose: give the synthesizing agent an orientation to the whole codebase. Companion
documents: the sibling analysis `*.txt` files in this folder, root
`AGENTS.md` (how to run the app / Unity), root `DazPoseWizard_Deferred_Work.md`,
`docs/VOCABULARY_SPRINT_DIRECTION_2026-10-01.md`, and the Unity-project docs under
`validation/DazPoseUnityValidation/docs/`.

---

## 1. Repository root

| Path | What it is |
|---|---|
| `DazPoseTool.sln` | .NET 10 solution (`DazPose.App` + `DazPose.Core` + tests) |
| `src/DazPose.Core` | Library: DAZ `.duf` pose/figure parsing, canonical export |
| `src/DazPose.App` | Avalonia 12 desktop GUI (library browser + converter) |
| `tests/` | .NET tests for Core |
| `validation/DazPoseUnityValidation` | **The actual game project** (Unity 6000.5.9f1, HDRP) |
| `configs/wardrobe/*.json` | Wardrobe import recipes (consumed by `scripts/import-lara-outfit.ps1`, NOT by the runtime) |
| `fixtures/private/` | Licensed G8F fixture characters (`.duf`/`.dsf`) |
| `docs/` | Architecture, P0.x pipeline docs, vocab sprint direction, this folder |
| `scripts/` | PowerShell/Python pipeline tooling (see §6) |
| `artifacts/win-x64` | Published app output |
| `G8F-Base`, `G8F-inCherishReferencePose`, `stack3 poses` | Reference pose trees |
| `TestOutput/`, `output/`, `.dazposewizard/` | Generated reports, isolated validation project copies |

## 2. `src/DazPose.Core` — pose conversion library

Pure library, no Unity dependency. Converts DAZ Studio `.duf`/`.dsf` into a canonical
JSON + approximate BVH, plus a report.

- `DazPoseParser`, `DazFigureParser` — parse `.duf` scene / `.dsf` figure
- `DsonFileReader` — DAZ DSON JSON reader
- `DazTransformEvaluator` — node transform evaluation
- `DazPoseExporter` — writes `.dazpose.json`, `.bvh`, `.report.txt`
- `PoseConversionService` — orchestrates conversion
- `DazModels` — DTOs

## 3. `src/DazPose.App` — Avalonia desktop tool

An Avalonia 12 GUI for browsing the DAZ content library and batch-converting poses into
the Unity project. **Not a viewer, not a scene authoring tool.**

- `Program.cs`, `MainWindow.axaml(.cs)` — shell
- `ViewModels/MainWindowViewModel.cs` — orchestration (807 lines): scan DAZ library,
  filter, conversion queue, watch Unity staging dirs
- `Services/LibraryIndexService` — SQLite index of `.duf` presets
- `Services/ThumbnailService` — loads DAZ `.png` previews
- `Services/ConversionQueueService` — queues conversions → canonical JSON into Unity
  staging path
- `Services/ConversionRegistryService` / `...CacheService` — per-destination
  converted/awaiting/failed state (`.dazposewizard/status`, local cache DB)
- `Services/UnityProjectService` — detects Unity project, resolves asset roots, writes
  bridge config
- `Services/RequiredMorphManifestService` — generates DAZ morph-export-rules CSV
- `Models/AppSettings` — DAZ root, figure DSF, Unity root, output dirs

Run/publish: `scripts/publish-win-x64.ps1` → `artifacts/win-x64/DazPose.App.exe`
(launch via `Start-Process`, confirm signed-in user session per AGENTS.md).

## 4. Unity project: `validation/DazPoseUnityValidation`

The production game codebase. Unity 6000.5.9f1, HDRP, Animation Rigging, SALSA LipSync.
Structure under `Assets/`:

```
Assets/
  DazPose/                 # all first-party code (Runtime / Editor / Effects / Generated / AnimationAudit)
  Player/Runtime/          # player-facing facade (camera/view)
  FirstPerformanceVoid/    # First Contact scene controller + its Editor builder
  Performer.meta...        # (see DazPose/Runtime/Performer)
  Scenes/                  # 7 scenes (see §4.3)
  Player/, TestCharacter/, PartnerProof/, DazPoseImports/, Generated/, Wardrobe/
  Animations/, TestData/, Wardrobe runtime assets
  motiondrive.funscript, teleportsound.mp3
```

### 4.1 `Assets/DazPose/Runtime/` — shipped runtime

**Performer (`Runtime/Performer/`)** — the performer (succubus) facade and its layers:
- `SuccubusPerformer.cs` — large facade: Pose, Expression, Gesture, Motion, Speech,
  Locomotion, Teleport, Dissolve, Breathing, Gaze. This is the C# command seam.
- `PerformerPose`, `PerformerPoseStateJob`, `PoseTransition`, `PoseTransitionTrajectory`,
  `PerformerBodyPose`, `PerformerBodySourceMixer`, `PerformerExpression(Layer)`,
  `PerformerGesture(Layer)`, `PerformerBlink`, `PerformerBreathing`, `PerformerGaze`,
  `PerformerAttentionLife`, `PerformerSeat(ing)Layer/Profile/Motion`,
  `PerformerLocomotion(Motion/Profile/...)`, `PerformerSpeech`, `PerformerSalsaLipSync`,
  `PerformerActionRuntime/Layer/State`, `PerformerControlSurface`,
  `PerformerVisibilityState` — animation layers / state jobs
- `PerformerTeleport(Profile)` + `TeleportCompletion` — teleport verb
- `PerformerDissolve(Profile/Rig)` + `DissolveCompletion` — dissolve verb (particles)
- `PerformerMotion(Consumer/Layer/MaskUtility/RuntimeSelfTests/Set/Variant)` — motion
  layer driven by the shared MotionDriver clock
- `PerformerLipSyncMorphCatalog`, `PerformerSurfaceBindingAsset`,
  `PerformerFootwearProfile`, `LaraAnatomyControls`, `LaraFirstOutfit`, `LaraHeelReview`,
  `LaraWardrobe`, `PerformerParticleBody`, `WardrobeOutfitDefinition` — glue + fixtures

**HandGrip (`Runtime/Performer/HandGrip/`)** — grip/contact:
- `IGripTarget.cs` — target contract: `GripFrame Evaluate(float position01)`
- `GripContactRod.cs` (analytic cylinder fixture), `GripPalmTarget.cs`,
  `GripReachPlanner.cs`, `HandGripSolver.cs` (digit contact, finite-probe approx),
  `HandGripAnimationJob`, `HandGripRuntimeSelfTests`, `HandGripAcceptanceHarness`,
  `HandGripSpatialProof`, `GripFrame`, `HandGripRigProfile`,
  `PerformerHandGripController` (Idle/Planning/Walking/Acquiring/Active/Releasing/Failed),
  `PerformerHandGripLayer`, `PerformerArmIKLayer`
- Profile asset: `Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset`

**Motion (`Runtime/Motion/`)** — the authoritative playback clock:
- `MotionDriver.cs` — publisher of `MotionSample`; Start/Stop/Restart/Reset/Seek;
  sources: `SineMotionSource` or `FunscriptPlayback`
- `IMotionSource`, `MotionSourceMode`, `MotionSample`, `MotionSourceSample`,
  `MotionTargetSegment`, `MotionDirection`
- `Motion/Funscript/` — funscript JSON parser, playback program, segment model,
  runtime self-tests

**Toys (`Runtime/Toys/`)** — player-owned toy control:
- `ToyControlService.cs` — Intiface transport; `Vibrate/Oscillate/MoveTo/Follow/Stop/...`
- `ToyMotionFollower.cs` — bounded consumer of one MotionDriver (drift-free clock)
- `IToyBackend`, `ToyDevice`, `ToyFeature`, `ToyDeviceRegistry`, `ToyMotionBinding`,
  `ToyOutputBinding/Capability/Range`, `ToyCommandReport`, `ToyStackAcceptanceSelfTests`
- `Toys/Buttplug/` — Buttplug backend + transport self-tests

**Wardrobe (`Runtime/Performer/Wardrobe/`)** — outfit/layer controller:
- `PerformerWardrobe.cs` — runtime owner: 32-cap FIFO request queue, atomic commits,
  mask-based fit-state application, rollback, `WardrobeChanged`, Cut/Dissolve transitions
- `WardrobeCatalog` (candidate vs released, `releasedGenerationId`),
  `WardrobeConfiguration` (per-preset assignments, `#if UNITY_EDITOR` mutators),
  `WardrobePreset` (note: `Parts` returns a clone — known stale-truth bug),
  `WardrobeFitState`, `WardrobeLayerState`, `WardrobeState` (immutable snapshot),
  `WardrobeResults`, `SceneWardrobeBinding` (per-scene partial snapshot),
  `FootwearPoseDriver`, `WardrobePlayerExecutionFixture`,
  `WardrobeRuntimeExecutionHarness` (editor-only report emitter)

**Magic (`Runtime/Performer/Magic/`)** — cast/aura:
- `PerformerMagicRuntime` (Cast/SetAura/ClearAura), `PerformerSpell/Aura/Style`,
  `MagicEffectInstance`, `MagicTargetGeometry`, `PerformerMagicCatalog`,
  `SpellCompletion`, runtime self-tests

**Partner (`Runtime/Partner/`)** — male avatar (acceptance-level only):
- `PartnerAnatomyTestController.cs` — erection slider, compliance offsets, GUI panel;
  lives only in `PartnerRigAcceptance.unity`

**Top-level runtime:** `DazPoseCharacterState.cs`, `DazPoseTransitionExtrapolationJob.cs`

**AnimationAudit (`Runtime/AnimationAudit/`)** — audit harness: catalog, orbit camera,
`AnimationAuditHarness` (scene `AnimationAudit.unity`)

### 4.2 `Assets/DazPose/Editor/` — tooling (never shipped)

- `Editor/HandGrip/` — calibration window, acceptance setup/builder/runner/visual proof
- `Editor/Motion/` — `FunscriptImporter`, `FunscriptAcceptanceRunner`
- `Editor/Partner/` — `PartnerRigAcceptanceBuilder`, `PartnerCharacterPreparation`
- `Editor/Toys/` — `ToyControlBt1TestMenu`
- `Editor/Wardrobe/` — `ClothingSetupWindow` (the editor UI), `WardrobeEditSession`,
  `ClothingSetupBuilder`, `WardrobeCatalogBuilder`, `WardrobeFitCompiler` (compiles fit
  states; has hidden mutation side effects — known issue), `WardrobeGenerationPublisher`
  (publishes candidate; mutates fork in place — known issue), `WardrobeExecutionValidation`,
  `WardrobeRuntimeFixtureBuilder`
- `Editor/AnimationAudit/`, `Editor/ParticleBody/` — audit/body tooling

### 4.3 Scenes (`Assets/Scenes/`)

| Scene | Wires |
|---|---|
| `FirstPerformanceVoid.unity` | The "current game": Player/ViewRig/HeadPose/MainCamera, Performer, MotionDriver, ToyControlService, FirstContactPerformance, PerformerParticleBody, pose acceptance harness |
| `HandGripAcceptance.unity` | PerformerHandGripController + HandGripAcceptanceHarness + MotionDriver + Toys |
| `ClothingSetup.unity` | PerformerWardrobe + MotionDriver + Toys + FirstContactPerformance base wiring |
| `PartnerRigAcceptance.unity` | `PartnerAnatomyTestController` only |
| `AnimationAudit.unity` | AnimationAuditHarness |
| `LaraHumanoidKawaiiTest.unity`, `PoseValidation.unity` | SuccubusPerformer / acceptance harnesses |

### 4.4 Effects (`Assets/DazPose/Effects/`)

- `Dissolve/` — dissolve shaders/materials (LaraRuntimeMaterials), used by teleport/dissolve/wardrobe transitions
- `Magic/` — 5 style folders (ArcCyan, Emberfire, RiftBloom, VerdantPulse, VioletSerenity) + Shared
- `ParticleBody/`, `Teleport/` — dissolve/teleport particle systems

### 4.5 Generated assets (`Assets/DazPose/Generated/`)

Content-generated ScriptableObjects only: `Actions/Acceptance`, `Gestures/Acceptance`,
`HandGrip/Profiles`, `Magic/{Auras,Spells,Styles}`, `Motion/Acceptance`.

### 4.6 `Assets/Player/Runtime/`

- `PlayerController.cs` — director-facing facade; exposes `public ToyControlService Toys`
  and `public PlayerView View`
- `PlayerView.cs` — latest-wins world-position + view-orientation channels
- `PlayerViewCompletion`, `ViewTransition`, `ViewTrackingSettings`
- Canonical hierarchy: `Player / ViewRig / HeadPose / MainCamera`

### 4.7 `Assets/FirstPerformanceVoid/`

- `FirstContactPerformance.cs` — the only "performance" today: one hardcoded async C#
  sequence (gaze → locomotion → speech → seating → dissolve …), run once per Play Mode
- `FirstPerformanceVoidControls.cs` — dev room UI ("RUN FIRST CONTACT"), gesture hook
- `Editor/FirstContactPerformanceSetup.cs`, `FirstPerformanceVoidBuilder.cs`,
  `FirstPerformanceVoidPlayerMigration.cs`
- Docs: `docs/FirstContactPerformance.md`, `docs/FirstPerformanceVoid.md`

### 4.8 Project docs (`validation/DazPoseUnityValidation/docs/`)

- Import/handoff: `LaraWardrobeImportQuickStart.md`, `LaraWardrobeImportPlaybook.md`,
  `LaraWardrobeImportStatus.md` (AGENTS.md-mandated reading)
- Vocabulary & handoffs: `VOCABULARY_SPRINT_DIRECTION_2026-10-01.md`,
  `HISTORICAL_HANDOFF_2026-10-01_FIRST_CONTACT_TO_VOCAB_SPRINT.md`
- Wardrobe execution: `WardrobeRuntimeAndSetupSpec.md`,
  `wardrobe-execution/{START,CONTRACT,01-runtime,02-layer-fitting,03-setup-and-save,04-integration,VALIDATION}.md`,
  `wardrobe-execution/progress.json`
- Systems: `HandGripImplementationNotes.md`, `TargetDrivenGrip*.md`, `ToyControlBT1.md`,
  `ToyAuthoringBuildPlan.md`, `MagicArtPass.md`, `G8M_ARTIST_AUTHORED_ERECTION_MORPH_PROOF.md`,
  `PARTNER_CHARACTER_HANDOFF.md`, `TeleportAcceptance.md`, `animation-audit/*`

## 5. Test hardware vs production (important context)

There is currently **no authoring script interpreter / director**. All "verbs" are typed
C# calls on `SuccubusPerformer` and `PlayerController`. Most capability harnesses live in
isolated project copies under `.dazposewizard/` (handgrip-validation, p0c-native-generation)
and editor-only `*Harness`/`*Runner` classes. The wardrobe "passed" gates in
`progress.json` point at that isolated PoC, while the main project's integration assertion
is FAILED. See the sibling review analyses for the full deficiency list.

## 6. `scripts/` tooling

- `publish-win-x64.ps1` — publish the Avalonia app
- `verify-handgrip-main-compile.ps1` — Roslyn-compile main project sources without editor
- `open-handgrip-manual.ps1` — sync HandGrip sources into isolated project + launch
- `import-lara-outfit.ps1` — recipe-driven wardrobe import/validate/publish via isolated Unity
- `build-buttplug-unity.ps1` — pinned offline Buttplug.dll rebuild (checksum)
- `run-wardrobe-execution.ps1` — staged wardrobe validation gates in isolated project
- `wardrobe-recipe.py` — resolve/cache wardrobe evidence from a recipe
- `prepare-partner-character.py` — G8M shell correspondence manifest (Blender)
- `prepare-lara-wardrobe.py`, `prepare-lara-heel-reference.py`, `prepare-lara-candidate.py`
- `inspect-fbx.py`, `inspect-daz-materials.py`, `compare-player-endpoints.py`,
  `characterize-playernude.py`, `analyze-player-weight-threshold.py`,
  `add-wardrobe-coverage.py`, `build-*dissolve*variants*.py`, `check-thumb-candidate.py`

## 7. Vocabulary → current implementation status

| Authoring verb | State | Code seam |
|---|---|---|
| `TeleportTo` | Implemented | `PerformerTeleport.cs` |
| `DissolveTo` | Implemented | `PerformerDissolve.cs` |
| `TurnTo` | Implemented | `PerformerLocomotion.cs` |
| `Emphasize()` / `Gesture(g)` | Accepted (one-shot gesture driver) | `PerformerGesture.cs` |
| `Cast/Aura/ClearAura` | Implemented | `PerformerMagicRuntime.cs` |
| `Outfit/TryRemoveLayer/TryAddLayer/CurrentWardrobe` | Spec complete; runtime incomplete; UI noisy | `PerformerWardrobe.cs` + `Editor/Wardrobe/*` |
| `Stroke/StopStroke` | Toy-side `Follow` done; performer stroke not shipped | `ToyControlService.Follow` + HandGrip |
| `Player.Toys.Play/SetLevel/Stop` | Implemented (BT1) | `ToyControlService` under `PlayerController.Toys` |
| `Say(clip)` (lip sync) | Runtime exists, no script seam | `PerformerSalsaLipSync` / `PerformerSpeech` |
| **Director / scene script** | **Not implemented** | `FirstContactPerformance.cs` is the only hand-authored sequence |

## 8. Key invariants / conventions

- One authoritative `MotionDriver` playback clock; all toy + performer consumers sample
  "now" (no cumulative drift).
- Wardrobe state is immutable; changes flow through `PerformerWardrobe`'s request queue.
- All editing of released wardrobe assets goes through candidate forks + explicit publish.
- The performer and player share a single persisted `ClothingSetup` scene; per-scene
  wardrobe recall via `SceneWardrobeBinding`.
- Editor tooling is isolated and runs in `.dazposewizard/<name>` copies so it never
  mutates main-project scenes.
