# Seven-day production consolidation plan

Prepared October 4, 2026. Approved by the user October 5, 2026 as the authoritative TODO list until complete. This approval establishes the work plan; it does not claim implementation or acceptance results.

Day 1 execution packet: [production-consolidation/DAY_1.md](production-consolidation/DAY_1.md). Current execution state: [production-consolidation/progress.json](production-consolidation/progress.json).

**User revision after accepting Days 1–5:** complete
[pre-authoring remediation](production-consolidation/PRE_AUTHORING_REMEDIATION_PLAN.md)
before the composed API-authored scene. This covers the reported second-outfit
toe defect and reassessment issues 1, 2, 3, 5 and 6. Issue 4 is excluded: no
other-character generalization. Remaining work is driven by concrete authoring
seams and measured failures; existing required integration/player gates remain.
The detailed packets below are historical ordering where this revision differs.

User direction, October 5: performance tests reuse the saved authored First
Performance Void environment, including the glowing floor, smoke and lighting.
Keep its scene data intact; Lara's visibility lights belong to the reusable
performer prefab. Generic replacement lighting is not accepted.

**Sprint outcome:** a reusable actor, a predictable performance API, trustworthy wardrobe editing, and real partner contact that can run together in a fresh scene and a Windows player. A content author should choose actors, assets and targets, then describe a sequence. Character calibration, component discovery, animation ownership and teardown belong to the runtime.

Keep `SuccubusPerformer`, `PlayerController`, the existing animation graph, `MotionDriver`, the contact solver and `PerformerWardrobe`. Consolidate their contracts and composition. Do not introduce a replacement engine, scripting language, timeline editor or general gameplay framework this week.

This is seven focused engineering days with daily acceptance gates. It is a credible consolidation target, not a promise that every unknown can be repaired inside seven days. Day 7 includes repair capacity. If a required gate fails, report the exact remaining defect rather than relabeling a demonstration production-ready.

**Review basis and confidence**

I read all six reviews and the codemap, then checked the relevant current source, serialized scene references, wardrobe contract, partner handoff and recorded preparation report. Baseline HEAD was `9037618`; partner preparation changes and the review folder were also present in the working tree. This plan accounts for that local work and does not discard it.

No Unity build, play session, hardware interaction or visual approval was performed for this review. Source findings below are static observations; historical reports are identified as historical evidence. The isolated wardrobe project expected by the current runner, `.dazposewizard/p0c-native-generation/ProjectSettings/ProjectVersion.txt`, was absent in this checkout at review time. Restaging the validation environment is actual Day 1 work.

| Review claim | Current finding | Consequence for the plan |
| --- | --- | --- |
| Speech/outfit/grip verbs are missing | `SuccubusPerformer` already exposes `SayAsync`, outfit/layer methods, `GripAsync`, and dissolve methods | Preserve the facade and repair setup/lifecycle gaps; do not implement duplicate verbs |
| Grip needs a new motion-following engine | The grip controller already samples its bound driver's current position and evaluates its target each advance | Add explicit binding/ownership and supported target geometry, not another clock |
| The prepared partner prefab does not exist; the shell is unskinned | `PartnerCharacter.prefab` exists locally; the preparation report records copied shell weights and verified prefab references | Reuse this preparation, while retaining pending visual acceptance and its stated limitations |
| Wardrobe integration and stress runners are unimplemented | Recall, dissolve integration, 100-cycle checks, walk/culling checks and a player path exist in current source | Reconcile stale progress and run the code before scheduling its reimplementation |
| Wardrobe Save always fails because its hash cannot match a released catalog | Save forks a released catalog and rebinds the setup preview to the candidate; compiler/publisher use cloned parts | The blanket diagnosis is wrong. Reproduce candidate/release behavior; preserve release protection and fix effective-data coherence |
| Returning cloned preset parts is itself a bug | Defensive copying protects asset state; Save currently passes the previous parts back while configuration and compiled masks can differ | Keep defensive copies; explicitly produce a resolved candidate snapshot used by all consumers |
| All tests are manual sliders; there are no tests | .NET tests, assertion-based Unity self-tests and automated runners exist; first-party Unity assembly boundaries are missing | Retain useful tests and improve packaging/evidence, rather than rebuild testing from zero |
| A live bone chain alone solves contact | The partner proof records up to 150.23 mm bone/geometry mismatch at the flaccid endpoint; current contact samples do not track bending | Do not promise general morph/bend contact from bone positions alone |

The actual scene wiring supports the composition concern. In the inspected scene set, the grip component is on `HandGripAcceptance`, the wardrobe component is on `ClothingSetup`, and partner anatomy is on its own acceptance scene and prepared prefab. `FirstPerformanceVoid` does not currently assemble those components together.

**The architecture to converge on**

```text
Content sequence + typed scene bindings
                |
     Small performance execution scope
     completion / cancellation / diagnostics
                |
     SuccubusPerformer       PlayerController
                |                    |
 existing capability owners      View / Toys / body reference
                |                    |
 one actor animation graph      explicit shared MotionDriver
                |
 rig bindings + existing profiles + resolved wardrobe assets

Inspectors and setup previews call these same owners.
Validation code calls them too; actors do not depend on validation code.
```

Proposed helper names in this plan describe responsibilities; they are not a request to rename established public methods.

- **One facade, multiple focused owners.** Keep common verbs easy to discover on the performer. Keep animation, speech, wardrobe, contact and effects implementation in their existing owners. Extract demonstrated initialization/diagnostic responsibilities from the facade where helpful; do not split it solely to meet a line-count target.
- **Typed scene bindings.** One small scene component binds actor, player, optional partner body, location/seat targets, audio/content assets and motion. Resolve it before playback. Stable IDs support future scripting; runtime code must not select an arbitrary object with `FindAnyObjectByType`.
- **Defaults with clear precedence.** Asset/profile defaults → serialized actor or scene overrides → explicit command options. Runtime changes affect the instance, never the shared asset. A new performance scope clears its temporary overrides and restores its declared initial state.
- **Consistent temporal rules.** Finite commands complete when their stated outcome is reached. Grip acquisition completes on acquisition, while the grip remains active until released. Persistent pose/expression/gaze/aura are state. Speech and wardrobe retain their intentional FIFO semantics; competing work uses a documented replacement/rejection policy. One global queue would prevent legitimate concurrent speech, gaze and motion.
- **Owned cancellation.** Stopping a sequence cancels the operations it started, not just the code waiting for them. Every accepted operation reaches one terminal result. Teardown restores valid visibility, releases contact and owned motion/toy output, and clears temporary ownership. Existing typed completion enums may remain; a small adapter gives the sequence consistent success/cancel/failure handling.
- **One clock per shared playback.** Performer and toy consumers can follow the same explicit playback. Attaching/detaching a consumer does not restart, seek or stop another owner's clock. Separate interactions can own separate clocks without scene-global discovery.
- **A finite ownership policy.** Consolidate existing guard rules for root movement, full-body actions, right-arm contact, visibility/effect rebinding and wardrobe commit. Keep independent speech/head/face channels independent. Use explicit ownership and deterministic release; do not create a generic dependency scheduler.
- **Authoring data versus runtime data.** A wardrobe configuration is editable source; a validated, resolved generation is executable output. Both are legitimate representations. The defect to remove is independent or silent disagreement, not the existence of a compiler boundary.

**Where settings belong**

Many life/gaze/speech settings already exist on `SuccubusPerformer`. Reorganize and validate those settings instead of copying them into a second configuration system. Day 1 must account for every meaningful harness control with one of these destinations.

| Setting family | Persistent home and inspector | Author-facing access | Default rule |
| --- | --- | --- | --- |
| Skeleton, body/shell renderers, speech anchor, effect bindings | Serialized rig binding and existing character profiles; advanced setup inspector | Capability/readiness information, not bone paths | Editor setup resolves and saves references; missing required bindings fail preflight |
| Initial pose, expression, visibility, outfit | Actor/scene initial-state fields | Existing state/transition vocabulary | A saved, tested initial state; no test panel must initialize it |
| Breathing, blink, attention, gaze limits/response | Grouped performer inspector and existing backing fields/profiles | Validated semantic properties; per-scene overrides when needed | Preserve current reviewed values; reset-to-default is explicit |
| Walk, seat, arrival behavior | Existing locomotion/seating profiles and typed marks/seats | Existing move/seat verbs plus optional transition settings | Arrival blending/pose retention handled by runtime policy |
| Speech/lip sync | Dedicated AudioSource and SALSA bridge configuration | `Say`/`SayAsync` with current clip assets | Existing FIFO and real-time lip sync; no new mandatory viseme-track format |
| Grip strength, transition timing, supported span | Existing grip profile plus actor settings; calibration in advanced inspector | Target, strength and optional supported motion settings | Calibrated right-hand profile, bounded span, smooth acquire/release |
| Partner anatomy state and contact geometry | Production body component and generated contact profile | Supported normalized state and named contact target | Preserve prepared asset state; unsupported contact configurations fail explicitly |
| Wardrobe layers, hair, materials, footwear | Single Clothing Setup workflow over configuration/compiled generation | Existing outfit/layer/query methods | Base for new clothing, retained hair, default Cut transition; preserve accepted shoe/hosiery policy |
| Motion program and toy calibration | Scene playback binding; player/device configuration | Playback reference and semantic toy commands | No implicit playback; unavailable hardware follows declared optional/required policy |
| Probe positions, shader phases, calibration curls, acceptance counters | Validation tooling and read-only diagnostics | Not ordinary scene vocabulary | Disabled/absent in production scenes |

**Day 1 — Establish the executable contract and a trustworthy baseline**

Work:

1. Inventory the existing verbs across pose, expression, life/gaze, speech, gesture/action, movement/seating, visibility, magic, wardrobe, grip, player view and toys. Record owner, required assets, result, concurrency policy, cancellation/disable behavior and whether production scene wiring exists. Inventory harness controls alongside this table.
2. Lock the small command contract described above. Preserve established names and the wardrobe fixed contract. Explicitly define the gap between acquiring contact, holding a position, following motion, stopping motion and releasing contact.
3. Restage the isolated validation environment with the existing recipes and licensed local inputs; preserve GUIDs and hashes. Run the current baseline gates in their dependency order, record the first failing prerequisite, and compare source to stale handoff claims. Do not count an old report or cached Roslyn compile as a new player acceptance run.
4. Reproduce wardrobe assignment → Save → reload → preview → Publish → fresh runtime binding. Record effective layers and fit masks at each step. Specifically cover the released-catalog fork and a mismatched configuration. Check whether `Needs Fit` prevents saving valuable draft work.
5. Begin a minimal clean-scene Windows build probe and map runtime/editor/vendor dependencies. This exposes packaging issues before Day 6. Keep the current project location; a repository relocation has no authoring benefit this week.

Deliverables: verb/setting inventory, ownership policy, evidence ledger keyed to source/assets, baseline failure list and the minimal build probe.

Exit gate: every advertised capability has a known owner and status; the three integration risks—contact geometry, wardrobe data, player packaging—have concrete reproduction/build results. Missing licensed input or validation setup is named precisely.

**Day 2 — Make actors reusable and put settings in the right inspectors**

Work:

1. Create a production performer prefab/variant using the accepted character assets and existing graph. Bind speech/SALSA, locomotion/seating, effects, wardrobe and calibrated contact as appropriate. Keep character-specific data in the prefab/profiles. No smoke panel, acceptance installer or scene-name branch is required for ordinary behavior.
2. Add the small typed scene-binding component and capability preflight. It identifies exactly which actor/asset/reference is missing and offers an editor repair at that reference. Do not require every scene to carry unused capabilities; validate the capabilities that its sequence declares.
3. Replace production-path global discovery with serialized/local bindings. Editor auto-wiring is acceptable when it is scoped, unambiguous and persists the result. Two actors must never acquire each other's wardrobe or motion source.
4. Implement the grouped performer inspector: setup/readiness, initial state, life/gaze, speech, movement, effects, contact and advanced diagnostics. Each meaningful existing control receives a documented persistence policy and script property/options path. Promote behavior out of harnesses only where the inventory proves it still lives there.
5. Make Play Mode tuning explicitly temporary. Provide a deliberate apply-to-defaults path for supported values with Undo and prefab override behavior; pose/camera/calibration probes never become wardrobe defaults. Preserve legacy serialized fields or migrate them explicitly.

Deliverables: reusable actor, scene binding/preflight, grouped inspector and setting migration table.

Exit gate: instantiate the actor in a fresh scene, assign its scene targets, and use speech, pose/expression, movement and appearance without running an acceptance setup menu. Save/reopen preserves defaults. A second actor has independent state. Missing capabilities produce useful preflight errors.

**Day 3 — Make commands compose, stop and replay predictably**

Work:

1. Extract the reusable execution lifetime, waits, result observation and diagnostics from `FirstContactPerformance`. Keep it a small C# execution scope. Consume each Unity awaitable once; do not hide polling loops and result checks in every scene script.
2. Add explicit stop/cancellation paths wherever an advertised finite operation lacks them. Test acquisition and teardown as well as the active state. Adapt backend `Task` completion at the boundary instead of rewriting the toy transport to match Unity internals.
3. Consolidate the finite ownership matrix. Preserve speech/wardrobe queues and pose/gaze replacement behavior. Define movement during grip as releasing contact before root movement, or returning a specific busy result when an option requests strict ownership. Ensure all entrances use the same policy, including direct facade calls and setup previews.
4. Make a named appearance-at-location operation possible: prepare placement and optional persistent pose while hidden, then reveal through the existing dissolve owner. The present `DissolveInAsync(float)` only reveals at the current location. Keep this coordination behind the public seam; ordinary scripts should not write the actor transform.
5. Bind shared playback explicitly. Remove the production fallback where toy `Play` finds and restarts an arbitrary driver. Consumers can follow/detach independently; only the playback owner can start/seek/reset it. Preserve source revision and hitch behavior.
6. Migrate First Contact to the execution scope and make replay/reset possible without restarting Play Mode. Encapsulate the current arrival-pose hold flag so future scripts do not need to reproduce its workaround.

Deliverables: command behavior table, reusable execution scope, owned cancellation, explicit playback binding and migrated First Contact.

Exit gate: run/stop/run First Contact; stop during speech, movement, appearance and grip acquisition; destroy an actor during a wait. No orphaned wait, stale continuation, stuck arm owner or hidden actor remains. Independent speech/gaze can continue alongside compatible body actions. Toy detach does not restart or stop performer playback.

**Day 4 — Connect contact to the prepared partner through a real runtime target**

Work:

1. Extract anatomy state, renderer bindings and donor-pelvis following from `PartnerAnatomyTestController` into a focused runtime component. Convert the acceptance controller into a client. Do not carry its camera manipulation or pelvis test slider into gameplay initialization.
2. Add a partner contact adapter implementing existing `IGripTarget`. Return world-space center/tangent/normal/radius in the established units and preserve frame continuity. Validate target lifetime, scale policy and a calibrated usable span that excludes unsupported end geometry.
3. First prove contact on the actual prepared geometry at the supported erect endpoint with zero procedural bend. Compare the adapter's sampled envelope to the evaluated body/shell, including interior span samples. Keep the existing approximate cylindrical solver honest; do not imply arbitrary mesh collision support.
4. Replace fixture-specific diagnostic writes in the controller with a diagnostic client. Retain rod certification where justified, but represent any reusable straight-span guarantee through geometry capability data rather than concrete fixture identity. General targets remain sampled unless a stronger guarantee is implemented and tested.
5. Expose contact acquisition, position hold, playback following and release through the performer surface. The existing driver remains authoritative. Preserve motion-layer suppression while contact owns the arm.
6. Only after rigid contact passes, add bounded compliance if the same contact representation can follow the deformed surface with a tested update order. Member deformation remains owned by the member component. Default compliance to zero until validated; do not reduce the reported radius as a substitute for deforming visible geometry. Mesh squeeze and broad morph-state contact are separate asset/geometry work.

7. Fix the validation scene's initial camera position/orientation and field of view so both actors are visible immediately on Play. Provide useful close-up contact framing. Preserve the authored Void environment and lighting. User accepted Day 3 with this framing defect deferred explicitly to Day 4.

Deliverables: production partner body component/profile, real `IGripTarget`, combined contact fixture and author-facing controls.

Exit gate: acquire, hold, follow the shared playback and release against the real partner in the clean composition scene, including root translation/rotation, invalid/destroyed target and bounded out-of-reach recovery. Contact-frame/probe metrics and close-up visual review are both recorded. The supported anatomy state/span is explicit; unsupported states cannot report successful contact. Right-hand, rigid contact is the required floor; compliance is accepted only with separate evidence.

**Day 5 — Make wardrobe data trustworthy and rebuild Clothing Setup around the artist's task**

Work:

1. Implement one explicit resolver that combines a preset and authoring configuration into a resolved candidate snapshot. Compilation, preview, Save and Publish use that result. Keep defensive copies. Add assertions that resolved parts, populated mask and compiled reachable masks agree.
2. Preserve the lifecycle: editable working configuration → saved candidate → compiled/validated candidate → immutable release. Saving a draft must not overwrite a release. A failed fit may block preview/publication while still allowing explicit draft persistence. Make failure atomic for bound runtime state and last released data; generated temporary artifacts are tracked separately.
3. Make the release generation guard meaningful and visible. A mismatched provided configuration should produce an explicit diagnostic/rebind requirement, not silently choose different layers. Released runtime can use its self-contained resolved snapshot without a mutable draft overlay. Do not simply delete the hash guard or mutate released presets in place.
4. Rebuild the existing Clothing Setup window with an outfit list, garment rows grouped into Base/1/2, and a visible dressing sequence. Each row has an explicit layer selector. Show actual garment names in the next removal/addition step. Fix the current inverted Base explanation: removal order is 2 → 1 → Base, skipping empty layers.
5. Put preview controls and persistent state at the top: dress, remove next, add next, naked preview, selected outfit, edited revision and currently previewed revision. Naked preview preserves outfit selection; explicit Unclothed is separately labeled. Retain independent hair and accepted hosiery/footwear behavior.
6. Provide Undo/Redo, dirty indication, draft/released badges, visible compile progress/error location, Save Draft, Revert and Publish. Advanced opacity/footwear calibration moves behind focused panels. The removal-order preview updates immediately; expensive compiled appearance updates explicitly with stale-preview labeling. Avoid a mandatory UI framework migration.

Deliverables: resolved wardrobe data path, transactional regression coverage and rebuilt setup workflow.

Exit gate: change a garment's layer, save, close/reopen, preview removal, publish, instantiate a fresh actor and observe the same sequence. Repeat from an already released catalog and verify the previous release remains usable after a failed candidate. Reimport retains stable assignments/material/footwear edits. Submit the rebuilt workflow for user review separately from technical checks.

This changes the old window's checkbox presentation in response to the user's explicit UI rebuild request. It preserves the fixed three-slot runtime semantics and accepted artistic choices; update the UI-related contract wording with implementation.

**Day 6 — Prove integration and enforce the production boundary**

Work:

1. Run the existing wardrobe gates in order against current synchronized source/assets. Repair gaps instead of duplicating the existing integration implementation. Include transformed actor, two-actor isolation, speech/expression/gaze/pose preservation, rollback, 100-cycle disposal/cache behavior, 181-frame walking and live culling evidence, scene recall and dissolve ownership.
2. Exercise interactions the isolated fixtures cannot establish: speech during a wardrobe change; gaze/expression during contact; movement requested during contact; wardrobe/effects contention; recall while hidden; interruption during reveal; target removal; playback source replacement; toy disconnect/reconnect; independent actors and clocks. Test defined outcomes and cleanup, not just absence of exceptions.
3. Finish assembly/build separation using Day 1's dependency map. Target first-party runtime, editor, automated tests and opt-in validation-player code. Preserve `.meta` GUIDs, account for SALSA/HDRP/Animation Rigging and internal test access, and compile after each move. An asmdef cannot reference source left in the predefined assembly: resolve those edges rather than toggling a define and hoping.
4. Keep runtime validation fixtures that genuinely need a player in a dedicated validation build. Production scene dependencies contain no acceptance panels or required test controllers. Do not move every runtime test into Editor if it would destroy player coverage.
5. Build and launch a clean Windows player from the production composition scene. Collect a short warm-up and steady-state profile for frame time, allocations and material/mesh/object counts. Require bounded counts across repeat cycles; establish the target frame-time budget from the actual baseline/hardware rather than claim a speedup from editor graph timing.

Deliverables: current integration reports, production/validation build boundaries, Windows player and performance baseline.

Exit gate: the composed performance works without the Unity editor or debug panels; shipped runtime does not require validation code. Required gate failures remain release blockers. Mock toy transport proves lifecycle behavior; any physical-device check is recorded separately and is not inferred from a mock pass.

**Day 7 — Author through the public surface, repair friction, and hand off**

Work:

1. Create a fresh scene from the production actor and binding setup. Write a roughly 90–120 second technical sequence using only public semantic calls and bound content assets: appearance at a mark, speech, repositioning, pose/gaze/expression, wardrobe removal/restoration, real contact with shared playback, optional toy participation, release and disappearance. Use a companion smoke sequence for gesture/action, seating, teleport and magic/aura if including every verb would make the main sequence artificial.
2. Give the sequence ordinary serialization for actor/assets/marks. No DSL, graph editor, line-selection system or content database is needed to prove the API. Every reach into a solver component, mask, generated path, renderer or test controller is a concrete missing seam to repair.
3. Run it from cold start, replay it, cancel at several stages and run again. Run with no optional toy, invalid content, missing required capability and a second actor present. Check capability errors before playback and readable command errors when live state changes.
4. Reserve the remaining day for defects found by this exercise. Remove temporary bypasses, update stale handoffs and publish a compact authoring reference: what a command does, when it completes, what it can overlap, how it stops, defaults and supported limits.
5. Review the contact visuals, outfit workflow and transition quality with the user. Record technical pass, visual approval and independent clean-context import acceptance as separate statuses. This sprint does not retroactively pass the unrelated import trial.

Deliverables: minimal example sequence/scene, authoring API reference, current evidence/handoff, known limits and a prioritized authoring-sprint backlog.

Exit gate: someone writing the sequence needs actor/asset/target choices and ordinary timing/branching, with no harness knowledge. The same sequence runs in a player, can stop and restart cleanly, and exposes failures at the correct level.

**Release decisions and scope control**

Required before calling the runtime ready for the authoring sprint:

- Fresh-scene production composition and useful preflight.
- Persisted defaults and instance-safe script overrides for meaningful settings.
- Consistent completion/cancellation, deterministic contention, clean replay.
- Real partner contact in its explicitly supported geometry/state envelope.
- Explicit shared playback with independent consumers and reliable toy stop/detach behavior.
- Wardrobe save/reload/preview/publish parity, protected releases and understandable UI.
- Existing required wardrobe integration/stress/build gates, current source/asset evidence and a composed player run.
- Human visual acceptance recorded honestly; numeric probe success does not approve finger/skin appearance.

Defer first if time is tight: dynamic member compliance, squeeze assets, contact across unsupported morph states, left/two-handed contact, elaborate inspector styling, automatic generated-cache pruning and broad folder renaming. Keep safe geometry limits rather than claim those deferred features work.

Do not defer cancellation, saved-data correctness, current required integration gates or the player build merely to fit the calendar. Do not include new retargeting generations, FinalIK, terrain IK, a new import pipeline, personality/memory, a content browser, a script interpreter or a timeline editor. Those are different milestones.

If a gate overruns, the next work period finishes that gate and optional work drops. Day 7 reports the genuine readiness status. Scope limits are part of a dependable API; hidden setup requirements and unexplained failures are not.

**Source anchors for implementation**

All paths below are relative to the repository root. They identify existing seams, not files already modified by this plan.

- `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Performer/SuccubusPerformer.cs`: facade, serialized defaults, capability guards, wardrobe effect ownership, graph lifecycle.
- `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/FirstContactPerformance.cs`: reusable execution/lifetime candidate and current arrival hold workaround.
- `validation/DazPoseUnityValidation/Assets/Player/Runtime/PlayerController.cs` and `PlayerView.cs`: view facade and per-channel ownership.
- `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Motion/MotionDriver.cs`; `Runtime/Toys/ToyControlService.cs`: shared clock, toy Play fallback and consumer lifetime.
- `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Performer/HandGrip/`: existing `IGripTarget`, frame, planner, controller and solver.
- `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Partner/PartnerAnatomyTestController.cs`; `Assets/PartnerProof/PartnerCharacter.prefab`; `docs/PARTNER_CHARACTER_HANDOFF.md` under the Unity project: prepared partner and extraction boundary.
- `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Performer/Wardrobe/`: runtime, immutable state and existing scene recall.
- `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Wardrobe/ClothingSetupWindow.cs`, `WardrobeEditSession.cs`, `WardrobeFitCompiler.cs`, `WardrobeGenerationPublisher.cs`: editing and generation lifecycle.
- `validation/DazPoseUnityValidation/docs/wardrobe-execution/`: ordered contract/gates; reconcile `progress.json` with current source and new reports.
- `scripts/run-wardrobe-execution.ps1`: existing isolated runner and hash checks; requires restaged local validation project.
- `validation/DazPoseUnityValidation/docs/LaraWardrobeImportQuickStart.md`, `LaraWardrobeImportStatus.md`, `TargetDrivenGripHandoff.md`, `G8M_ARTIST_AUTHORED_ERECTION_MORPH_PROOF.md`: preserve accepted import choices and distinguish current evidence from historical claims.
