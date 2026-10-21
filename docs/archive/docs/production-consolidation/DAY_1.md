# Day 1 execution packet: contract and current baseline

Approved plan: `docs/SEVEN_DAY_PRODUCTION_CONSOLIDATION_PLAN.md`.
Execution state: `docs/production-consolidation/progress.json`.
Packet prepared October 5, 2026. Execution has not started merely because this packet exists.

**Assignment**

Establish the current executable baseline and make the performance contract concrete enough for Days 2–7. Account for every advertised capability and meaningful harness setting. Reproduce wardrobe persistence/release behavior. Measure contact readiness and player packaging from current inputs.

The output is a verified inventory, contract, failure list and evidence-backed handoff. Existing product defects are expected findings. Day 1 does not require making all subsystem tests pass; it requires attempting the applicable checks, identifying the first actual failures and preserving sufficient evidence to repair them in the assigned later days.

**Authority and scope**

Root `AGENTS.md` makes the seven-day plan authoritative. Routine read-only audit, documentation, isolated staging, diagnostic fixtures and baseline runs are in scope. Use the installed tools and existing validators. Preserve the working tree and accepted source assets.

Do not implement Days 2–7 during this packet: no production prefab extraction, command-lifetime refactor, new partner runtime, wardrobe resolver/UI rebuild, assembly migration or scripting language. Minimal validation/staging repairs are allowed only when necessary to observe the baseline; record each repair and label subsequent results as obtained after that repair. If an observation requires a product repair, record the defect and continue independent tasks.

No automatic commit, stage, push, broad restore, cleanup, package upgrade or external publication. No physical toy output is required for Day 1; device transport can be inspected/tested through its existing mock path.

**How to resume**

1. Read root `AGENTS.md`, the approved plan's architecture/settings/Day 1 sections, this packet and progress.
2. Inspect actual current files and existing outputs. The plan's October 4 observations and review claims are leads, not fresh facts.
3. Set Day 1/task status to `in_progress`. On resume reuse completed work if its inputs still match. Rerun only affected checks after source/input changes.
4. At every substantial task boundary update progress with the command, result, report path, input identity and next action. Keep a concise user update roughly once per minute during ongoing work.

Progress statuses are `not_started`, `in_progress`, `complete`, `blocked`. Findings/tests separately use `passed`, `failed`, `not_run`, or `inconclusive`. A task can be complete after it has produced an evidenced failing baseline. A task prevented from making its required observation remains blocked; do not conceal that as a pass.

**Required tracked deliverables**

Create these files beside this packet as execution proceeds; do not fill them with invented results:

| File | Required contents |
| --- | --- |
| `VERBS.csv` | One row per public command family, distinguishing overloads when behavior differs; schema below |
| `SETTINGS.csv` | Every meaningful control in the relevant harnesses; persistence/API destination and current backing owner |
| `COMMAND_CONTRACT.md` | Chosen behavior for timing/results/defaults/cancellation; current behavior and missing implementation clearly separated |
| `OWNERSHIP.csv` | Concrete resource/channel rules for conflicting and compatible command families |
| `BASELINE.md` | Current identity, commands, fresh report summaries, failed/not-run checks, wardrobe reproductions and contact readiness |
| `DEPENDENCIES.md` | Actual Unity assembly/vendor/build edges and Windows probe result |
| `HANDOFF.md` | Concise verified findings, prioritized later-day repairs, exact resume instructions and completion checklist |

Large logs, captures, generated scenes/assets, manifests and executables belong in the appropriate ignored `TestOutput` or `.dazposewizard` roots. Tracked summaries must include their paths and relevant hashes so ignored local evidence can still be assessed. Do not commit licensed sources or texture copies.

**Task A — Record identity and protect active work**

Record current HEAD/branch, current Git status and already modified/untracked files. If Git reports dubious ownership, use the command-local safe-directory value from the existing handoff; do not change global configuration. Treat other work as owned by others.

Record installed Unity version, main project path, available isolated project paths, required source asset presence and running Unity project owners. The intended project is `validation/DazPoseUnityValidation`; Unity is `C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe`.

Do not operate the user's active main-editor scene or launch batch Unity on the main project. Do not kill Unity to obtain a lock. Use a currently free isolated project. If an existing isolation path has an owner, wait briefly/back off or stage a distinct diagnostic project and explicitly configure the runner for it; do not silently share it.

Record source/input hashes after staging and before execution. Do not use HEAD alone to identify a dirty working tree. Include current first-party C#/.meta, relevant runner scripts, package manifest/lock, scene/profile/catalog/config dependencies and actual imported inputs in a stable sorted hash manifest. Existing report hashes remain useful but state their coverage, including anything their runner omits.

Output: baseline identity in `BASELINE.md` and `progress.json`. Gate: active work is protected and the evidence has a reproducible input identity.

**Task B — Inventory verbs and settings against source**

Start at these existing seams; use targeted search and read owners as needed rather than scanning every vendor file:

- `Assets/DazPose/Runtime/Performer/SuccubusPerformer.cs` and its focused owners.
- `Assets/Player/Runtime/PlayerController.cs`, `PlayerView.cs`.
- `Assets/DazPose/Runtime/Motion/MotionDriver.cs` and `Runtime/Toys/ToyControlService.cs`.
- `Assets/DazPose/Runtime/Performer/HandGrip/{IGripTarget,GripFrame,GripReachPlanner,PerformerHandGripController}.cs`.
- `Assets/DazPose/Runtime/Performer/Wardrobe/{PerformerWardrobe,SceneWardrobeBinding,WardrobePreset,WardrobeConfiguration}.cs`.
- `Assets/DazPose/Runtime/Partner/PartnerAnatomyTestController.cs` and `Assets/PartnerProof/PartnerCharacter.prefab`.
- `Assets/FirstPerformanceVoid/{FirstContactPerformance,FirstPerformanceVoidControls}.cs`.
- `Assets/DazPose/Runtime/Performer/PerformerPoseSmokeHarness.cs`, grip acceptance/calibration controls and `Assets/DazPose/Editor/Wardrobe/ClothingSetupWindow.cs`.

These paths are under the Unity project. Read the appropriate subsystem handoff when following its operation. For wardrobe, read execution `START.md`, `progress.json`, `CONTRACT.md` and `VALIDATION.md`; use detailed stage files only for the operation at hand. For import staging, read the import QuickStart and Current handoff. For partner/contact, read `PARTNER_CHARACTER_HANDOFF.md` and `TargetDrivenGripHandoff.md`.

`VERBS.csv` columns:

```text
family,public_entry,owner,required_bindings,current_completion,current_overlap_policy,current_cancel_disable,scene_wiring,evidence_kind,evidence_path,target_contract,planned_day
```

Required families: pose, expression, breathing/blink/attention controls, gaze, speech, walk/turn, sit/stand, gesture, action, teleport, dissolve in/out/to, spell/aura, outfit/hair/layer changes/queries, scene wardrobe recall, grip/release, playback start/stop/seek/reset, player move/look/track and toy direct/ramp/play/follow/stop. Distinguish persistent state, finite action and playback consumer. Do not mark an existing verb missing because an accepted scene lacks its component.

`SETTINGS.csv` columns:

```text
control,source_path,current_backing_owner,current_storage,current_default,classification,target_storage,author_access,instance_or_shared,reset_behavior,planned_day
```

Classification: `persistent_default`, `scene_binding`, `runtime_override`, `diagnostic_only`, or `unresolved`. Group repetitive homogeneous controls only if the individual property names and defaults are enumerated. Include rig/effects bindings, initial state, life/gaze, speech, movement/seating, contact, anatomy, wardrobe/material/footwear and motion/toy settings. Trace each slider/toggle's actual write path. “A slider exists” does not prove its value saves or is available to scripts.

Verify scene wiring by script GUIDs and serialized references, including prefab instances. Record actual asset path/reference validity. Do not infer wiring from folder names.

Output: both inventories. Gate: every required family and meaningful control has a traced owner/destination or a precise unresolved item.

**Task C — Write the execution and ownership contract**

Use the approved plan's decisions. Keep established names and wardrobe semantics. Document:

- What each finite completion means, especially acquisition versus persistent contact.
- Speech/wardrobe FIFO; persistent-state replacement; body/action rejection or coordinated release. No global queue for unrelated channels.
- Explicit shared playback ownership. Following is independent of starting/restarting/stopping the clock.
- Defaults precedence, instance-only runtime overrides and reset/replay boundaries.
- Expected no-op versus unavailable/invalid/busy/failure outcomes. Preserve typed result detail; do not invent a universal result replacement now.
- Cancellation/disable/destruction: complete accepted operations once, clear owned work, prevent stale continuations. Distinguish cancelling a wait from cancelling its underlying behavior.
- Gameplay versus wall-clock timing as currently used and the target pause behavior.
- Hidden placement/pose preparation and appearance; no renderer/transform manipulation by ordinary content scripts.
- Optional toy absence versus a sequence declaring required hardware.

`OWNERSHIP.csv` columns:

```text
resource_or_channel,command_family,acquire_point,release_point,compatible_families,conflicting_family,target_resolution,current_implementation,evidence_path,planned_day
```

Cover actor root, persistent body pose/full-body action, seating, contact arm/fingers, head/gaze, facial expression/lip sync/blink, visibility/effects, wardrobe commit/rebind, shared playback and toy output. Pay special attention to movement during grip, clothing/effects contention and teardown. Write exact policies for these combinations, not “implement an arbitration manager.”

If an unresolved product decision cannot be inferred from the approved plan/current accepted contract, list the alternatives and the smallest decision required. Continue unrelated work. Do not silently alter an accepted artistic choice.

Output: contract and ownership table. Gate: Days 2–7 have actionable behavior requirements with current gaps identified.

**Task D — Stage and run the existing isolated baseline**

Discover current isolation first. If the expected wardrobe project is missing, stage a copy from main using preserved `Assets`/folder `.meta`, `Packages` (including local packages) and `ProjectSettings`. Include vendor binaries, required source/generated assets and licensed inputs locally. Omit main `Library`, `Temp`, `Logs`, `obj`, builds and generated output. Let Unity rebuild the isolated cache. Do not recursively delete a preexisting target; inventory and reuse or choose a fresh workspace-contained target.

Before running, compare synchronized scene/profile/package/config dependencies against main. The existing wardrobe wrapper copies selected C# and package files but is not a complete asset/bootstrap synchronizer. Its current copy list also omits `Assets/Player` source. Fresh full staging prevents a stale isolated actor/player implementation from giving a misleading pass. Record this coverage in the ledger.

Use signed-in-user/escalated execution for Unity licensing/GPU processes as required by local instructions. Batch checks do not require a visible editor. Do not install a new Unity/package version to sidestep the baseline. Use bounded sessions/polls so commentary continues during long imports/builds.

Existing wardrobe commands, from repository root:

```powershell
.\scripts\run-wardrobe-execution.ps1 -Stage Runner
.\scripts\run-wardrobe-execution.ps1 -Stage Contract
.\scripts\run-wardrobe-execution.ps1 -Stage Migration
.\scripts\run-wardrobe-execution.ps1 -Stage Runtime
.\scripts\run-wardrobe-execution.ps1 -Stage Layers
.\scripts\run-wardrobe-execution.ps1 -Stage Setup
.\scripts\run-wardrobe-execution.ps1 -Stage Persistence
.\scripts\run-wardrobe-execution.ps1 -Stage Integration
```

Run sequentially according to prerequisite gates. Inspect each result before starting dependent work; the wrapper automatically runs Runtime for some later stages and Player after Integration. If a prerequisite fails, record the failure and dependent checks as `not_run` with that reason. Independent source audits and targeted reproductions continue. A failed baseline is not permission to relax thresholds or fabricate data.

Reports live in the isolated project's `TestOutput/wardrobe-execution/<stage>/report.json`. Verify fresh timestamp, exact stage, exit code, `complete`, `passed`, source/input hashes, assertion results and limitations. Capture the first named failing assertion and supporting log. Required skipped/missing assertions do not pass. Keep existing geometric/image tolerances.

Use existing grip checks after inspecting their mutation/exit behavior. `DazPose.Editor.HandGrip.HandGripValidationRunner.BatchCalibration` recalibrates/saves profile data; run it only in isolated disposable assets, capture before/after hashes, and label that behavior. It is not a read-only check of the current saved profile. `HandGripLiveProof.Run` opens a scene and drives live execution; use isolation and its existing completion logic, not main or a guessed `-quit` flag. Recover current saved-profile evidence separately where a runner actually supports it. Do not publish recalibration back.

The cached Roslyn script `scripts/verify-handgrip-main-compile.ps1` is supplemental compile evidence, not gameplay/player evidence. .NET tests can be run through `tests/DazPose.Tests/DazPose.Tests.csproj` if the current cached SDK/packages permit; distinguish their scope from Unity runtime and avoid unrelated dependency upgrades.

For contact readiness, verify current `IGripTarget` implementations, partner prefab components, geometry data and motion binding. Report whether the production adapter exists. Do not implement it in Day 1 or treat rod passes as real-partner contact acceptance.

Output: baseline command/report ledger and findings. Gate: applicable existing checks were run with matching inputs, or their precise prerequisite/input failure is recorded. Current contact readiness is evidenced.

**Task E — Reproduce wardrobe editing through the actual path**

Do this in isolated copies, preserving source/released bytes and GUIDs. Inspect `ClothingSetupWindow.SaveConfiguration`, `WardrobeEditSession`, `WardrobeFitCompiler`, `WardrobeGenerationPublisher` and runtime `ConfigurationApplies`/layer resolution first. Use the actual window Save path; editing asset YAML or directly invoking configuration setters does not reproduce that path.

Prefer a small isolated editor diagnostic that drives the real window/session method (reflection on a private editor method is acceptable for the diagnostic) and observes persisted/runtime state. If UI interaction is needed, operate an isolated editor only and record the exact sequence. Do not replace Save/Publish with a test-only implementation. Keep any helper in the diagnostic project/output rather than landing runtime changes in main.

Execute these cases and record per-piece effective layers, configuration revision/hash, candidate/released catalog identity, populated/reachable masks and actual runtime results:

1. Candidate: assign a supported existing garment to another layer → actual Save → reload from disk → preview removal/addition. Identify any mismatch among configuration, preset inspector and compiled/runtime state.
2. Released: publish the supported candidate → change an assignment → Save again. Observe the candidate fork and setup rebind. A separate release-bound actor must retain the old release until explicit publication/rebinding. That intentional behavior is not itself a bug.
3. Publication: publish the changed valid candidate → reload → fresh actor binding → compare removal order to edited choices. Old release references/bytes remain intact.
4. Mismatch: provide a configuration with a different generation in isolated preview/binding → observe diagnostic/fallback/no-op/failure, without deleting the guard.
5. Invalid draft: introduce a known assignment/dependency issue on a disposable copy → attempt Save/compile/publication → observe which work persists and verify the previous release remains unchanged. Do not invent unsupported mesh inputs merely to force a failure.
6. Naked preview versus explicit `Outfit("unclothed")`: query selected outfit and ability to add back layers. Compare the window control to the fixed contract.

Use the existing three slots and highest-first removal semantics. Leave accepted shoe/hosiery/hair policy intact. Results can reveal a correct intentional behavior, a reproducible defect or an inconclusive observation; write which one. If a required case cannot run because of an earlier fit/assets failure, record its exact dependency and keep this task incomplete until the case is observed or an external input blocker is established.

Output: reproduction steps, input hashes and observed values in `BASELINE.md`. Gate: the persistence/release risks have actual observations rather than copied review diagnoses.

**Task F — Dependency map and Windows packaging probe**

Map actual first-party runtime/editor/validation relationships, current asmdefs/predefined assemblies, SALSA binaries, Animation Rigging, HDRP, Buttplug, importer/editor API dependencies and player-exclusion guards. Do not say every class in Runtime ships all its behavior; inspect preprocessor guards and scene/build dependencies.

If prerequisites pass, use the existing wardrobe Player gate as additional build/runtime evidence. If it is prevented by a failed Runtime gate, retain that dependency failure and create an independent minimal packaging probe in isolation.

For that independent probe, create a new generated scene using accepted serialized actor assets/references where available plus camera/light, with no dev control panel and no newly invented missing capabilities. Build `StandaloneWindows64` using current pinned settings. Keep BuildSettings changes local; put the executable/log under ignored output. A diagnostic build/launch helper is allowed in isolation.

Record build outcome and errors, build input scene/settings/package hashes, compiler/runtime logs, and whether the actor initializes, renders and can accept the existing configured speech/pose operation. Missing wiring is an observed result, not an invitation to perform Day 2. A successful bare packaging build is only packaging evidence, not full production composition acceptance. A built executable without a run is `built_not_run`.

For visible manual runs use the signed-in desktop/session procedure. Do not claim an interactive window from a sandbox process or use batch mode as visual acceptance. Do not launch the Avalonia tool for this Unity-only assignment.

Output: `DEPENDENCIES.md`, build/launch evidence and Day 6 packaging risks. Gate: an actual Windows build attempt (and launch if successful) has produced specific results; a proven external input/installation blocker is explicitly retained if execution cannot proceed.

**Task G — Integrate findings and finish the handoff**

Check all deliverables for evidence and contradictions. Each finding needs a trigger, observed result, expected contract, source seam, evidence path and assigned repair day. Clearly distinguish current behavior, target behavior and historical reports. Correct the subsystem handoff/progress only where a new result supports the correction; never overwrite an old pass with a fabricated new one.

Finish `HANDOFF.md` with:

- Current source/input identity and exact owned changed files.
- Passed/failed/not-run/inconclusive observations and local evidence paths.
- Decisions fixed by the approved plan, plus any genuinely unresolved product decision.
- Repairs assigned to Days 2–7, with the highest-risk prerequisite first.
- Remaining external blockers, exact missing input/capability and useful work completed despite them.
- A short next-executor prompt referencing the correct next task/packet.

Day 1 is `complete` only when A–G have their required outputs and observations. Real product failures may remain; that is the baseline. Unperformed mandatory observations caused by an external blocker keep the day blocked/incomplete. Do not start Day 2 by silently pretending the Day 1 gate passed.

**Delegation contract, if a supervising agent uses Luna**

The supervisor owns architecture decisions, `COMMAND_CONTRACT.md`, final ownership policies, shared progress integration and acceptance. Luna can own a bounded inventory, dependency map or isolated baseline/reproduction assignment. Give each worker one output path and an explicit read/write scope; workers must not share a Unity project concurrently or edit the shared facade/progress independently. Independent audit workers read main source and write only their assigned deliverables. The supervisor verifies their claims against evidence before integrating them.

A standalone Luna executor may run the entire packet sequentially. It must use the same scope and gates and hand off unresolved architecture decisions explicitly. Merely being able to launch tests does not authorize it to redesign the production contract.

**Copyable executor prompt**

> Execute Day 1 of the approved production consolidation plan by following root AGENTS.md, docs/production-consolidation/DAY_1.md and its progress.json. Establish the current contract and baseline, produce all required inventories/reproductions/build evidence, and update progress/handoff. Work in isolation for Unity mutation. Preserve current source/assets and accepted choices. Keep product repairs assigned to their later days. Continue independent tasks after a failed baseline, and do not declare unrun observations or historical reports current passes. Report the completed deliverables, exact baseline failures and remaining external inputs.
