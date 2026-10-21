# Reassessment of the six architectural reviews after Days 1–5

This is a read-only code review, with documentation changes only. All six original reviews were reread, then checked against current source, saved assets, Day 1 reproductions and Days 2–5 execution reports. No new Unity run was needed to evaluate these claims. Current source observations are not new runtime passes. The user accepted Days 4 and 5 separately from their automated evidence.

The ranking measures useful, supported findings and actionable architectural judgment, discounted for false positives and unsafe prescriptions. It is not a ranking of writing quality or a model benchmark. A claim that Days 1–5 fixed is still credited if it was true when raised. Reviews are identified by their actual filenames.

Subsequent user direction: repair issues 1, 2, 3, 5 and 6 before the composed scene, investigate the second-outfit toe regression, and explicitly exclude issue 4. The execution scope is now [PRE_AUTHORING_REMEDIATION_PLAN.md](PRE_AUTHORING_REMEDIATION_PLAN.md); portability observations below are historical analysis, not authorized implementation work.

## Ranking

| Rank | Review | Useful findings | Errors, misses and questionable prescriptions |
| --- | --- | --- | --- |
| 1 | `Deepseek41.txt` | Best concrete synthesis: scene/component wiring, preset/configuration/fit disagreement, inverted Base explanation, release provenance, missing target adapter, stale progress, motion ownership and bone/surface mismatch. Explicitly warned against trusting the flaccid bone chain and against assuming squeeze channels survived export. | Claimed the prepared partner prefab was absent despite existing local preparation; exaggerated per-call discovery; described candidate generation as happening per keystroke when the expensive path was explicit Save. Proposed compliance, squeeze exports and presentation splitting beyond what rigid production contact needed. |
| 2 | `grok.txt` | Strongest practical authoring-gap review: reusable prefab, hidden placement before reveal, real target adapter, generic target certification, semantic arrival behavior, explicit motion ownership, layer writeback and Naked versus Unclothed. Correctly resisted a new director and warned that flaccid bones do not represent the mesh. | Used stale shell/preparation status. Proposed a radius-squeeze approximation that cannot deform the body, and a bone-based erect target insufficient by itself for collision. Suggested deferring existing integration/transition coverage too broadly. Its useful authoring concerns did not establish that the proposed Stroke method names were necessary rather than an explicit playback binding. |
| 3 | `sol56.txt` | Best disciplined architecture judgment: validators must be clients, generic contact separate from compliance, rigid contact first, squeeze optional, temporal contracts, early capability checks, canonical wardrobe data, and a public-API authored sequence as the decisive exercise. Few hard factual false positives because it labeled suspicions as questions. | It did not identify the concrete Save/hash/fit implementation defect or establish which suspicious seams were actually broken. Useful direction, weaker forensic review. Its broad “guilty until demonstrated” posture can encourage unnecessary validation unless narrowed to specific findings. The numbered-layer replacement was a proposal rather than a defect in the fixed three-slot contract. |
| 4 | `glm53.txt` | Recognized the verbs already existed. Correctly identified mixed validation/runtime code, hardcoded rig assumptions, missing member adapter, copied orchestration, silent generation fallback, Save blocked by Needs Fit, and stale preview provenance. | Recommended live bone contact across erection states despite measured bone/geometry divergence. Suggested deformation controls not established as exported Unity channels. Repeated stale claims that Clothing Setup existed only in isolation and partner geometry remained unskinned. Prescribed UI Toolkit and a central activity service without demonstrating either was necessary. |
| 5 | `spacebunnyalpha.txt` | Caught actual layer metadata disagreement, fixture-only targets, root-local contact limitations, absent assembly boundaries, TestData asset provenance and author-facing nouns. Later corrected some shell claims. | Claimed there were no tests, although assertion-based Unity tests and a .NET test project existed. Its “released Save can never match” diagnosis missed the working-candidate fork/rebind. Claimed toy verbs were absent. Recommended bone geometry as a general solution, plus a mandatory timeline and line/viseme registry. Called ordering nondeterministic although runtime ceiling ordering was deterministic. Its focus-loss allegation did not establish dirty edits were discarded. Claimed the prepared prefab was absent. |
| 6 | `fledge alpha.txt` | Identified split layer data, unclear save/publish state, missing production partner/contact wiring and the value of extracting runtime behavior from the proof controller. | Claimed Say and other requested vocabulary were missing; confused stale progress with absent implementation; treated candidate/release separation and defensive copies as failures in themselves. Required an interpreter/editable sequence asset to test authoring. Proposed unsupported squeeze channels, deferred finger/surface correctness as “not an authoring blocker,” and underestimated actual geometry contact. These recommendations would create redundant work or weaken correctness. |

The top two are close: Deepseek supplied the better concrete diagnosis; Grok supplied the better small production exercise. Sol's restraint makes it more trustworthy for prioritization than the more confidently incorrect lower reviews.

## What the original reviews got right, and what is now resolved

| Original concern | Current disposition |
| --- | --- |
| Missing production performer composition; tests supplying required setup | Day 2 delivered a serialized production Lara prefab, typed bindings, explicit clocks/services and grouped defaults. Ordinary initialization does not require the smoke panels. The combined authored exercise remains outstanding. |
| One-shot copied waiter/ownership machinery; inconsistent cancellation | Day 3 extracted `PerformanceExecutionScope`, command-specific cancellation, ownership checks and First Contact reset/replay. This is implemented, although author ergonomics remain unfinished below. |
| Hidden actor cannot appear at a requested mark | Day 3 delivered `DissolveInAtAsync`. |
| Toys locate/restart another consumer's playback implicitly | Production Player/Toy paths now use explicit references and distinguish owned playback from following. Mock lifecycle/cancellation checks passed. Legacy demo controls still contain global searches; that is not evidence of production service discovery. |
| Member is only a proof controller; no real contact target | Day 4 extracted `PartnerBody`, added `PartnerContactTarget` and a prepared body/shell triangle contact surface. The planner uses a source-neutral straight-span capability rather than a rod type check. The user accepted the result. |
| Layer/configuration/compiled fit disagreement; silent mismatched release binding | Day 5 uses resolved snapshots, explicit compilation, coherent saved preset metadata and explicit generation diagnostics. Releases are self-contained and accept null authoring configuration. |
| Needs Fit loses draft edits; Save/Publish unclear; Naked forgets selection | Day 5 separates draft save, compilation and immutable publication; rebuilds layer rows, sequence display, Undo/Redo, preview provenance and separate Naked/Unclothed actions. User accepted the workflow. |
| Reimport loses artist edits | Current real reimport checks pass for three outfits; candidate refresh also covers release-derived drafts and variant IDs. Independent clean-context import acceptance remains separate. |

Source and execution evidence: [Day 1 baseline](BASELINE.md), [Day 2 report](DAY_2_REPORT.md), [Day 3 report](DAY_3_REPORT.md), [Day 4 report](DAY_4_REPORT.md), [Day 5 report](DAY_5_REPORT.md), and [progress](progress.json).

The reviews were not exhaustive. They missed the actual imported scalar-opacity path, prefab override persistence, and release-derived package-reference reimport failure repaired on Day 5. They also did not establish that an analytic circular contact approximation would clear the evaluated partner mesh; Day 4 exposed that limitation and added mesh-surface collision. These misses do not prove the whole reviews were useless, but they invalidate any claim of comprehensive coverage.

## Remaining architectural issues

### 1. Authoring still exposes operation-management plumbing

`PerformanceExecutionScope` is real and reusable, and underlying cancellation exists. But a correctly managed sequence still writes `Observe(actor, channel, start, expectedResult, label, persistent)`, `Wait`, and explicit ownership/cancellation callbacks. `FirstContactPerformance` demonstrates that machinery. A bare `await SayAsync()` returns a typed terminal result; it does not automatically turn every non-success into scene failure or register the command with the sequence's Stop.

This is a remaining API ergonomics gap relative to the desired short script. The next sequence should establish the smallest useful semantic wrappers around the existing scope. It does not justify a new interpreter, timeline, global queue or engine.

Anchors: `PerformanceExecutionScope.cs:35`, `:73`; `FirstContactPerformance.cs:86`; performer `CaptureCancellation` at `SuccubusPerformer.cs:941`. Source paths are beneath `validation/DazPoseUnityValidation/Assets/`.

### 2. Preflight does not fully validate executable capability state

`PerformanceSceneBindings.Validate` verifies that contact is an `IGripTarget`, but does not call the prepared target's readiness check. `PartnerContactTarget.TryEvaluate` has additional requirements for supported state, profile, visible renderer/mesh identity and calibration. Thus implementing the interface alone can pass the binding check even when contact will reject execution. Wardrobe preflight checks catalog validity/body ownership but does not use `TryValidateConfigurationBinding` or the initialized `BindingDiagnostic`. Action/spell/aura content declarations are not covered by its capability enum/assets in the same way as poses, expressions and speech.

The remedy is targeted preflight for the capabilities/assets the new sequence actually uses. Existing runtime guards remain valuable; this is a gap in early diagnosis, not proof that supported execution currently fails.

Anchors: `DazPose/Runtime/Performer/PerformanceSceneBindings.cs:130`, `:144`; `DazPose/Runtime/Partner/PartnerContactTarget.cs:51`; `DazPose/Runtime/Performer/Wardrobe/PerformerWardrobe.cs:78`.

### 3. Production and validation compilation boundaries remain mixed

The only `.asmdef` found under Assets is the vendor SimpleJSON definition. First-party runtime, demo controls, self-tests and player validation therefore still share predefined assemblies. Public shader-only acceptance control remains on `SuccubusPerformer`, and runtime production classes still occupy `DazPose.UnityValidation.Partner`.

The production prefab no longer requires a smoke harness to initialize. That resolves the load-bearing setup problem, but not compile/API boundaries. Assembly membership does not prove every unused class survives Unity's managed stripping, so the reviews' claim that every harness necessarily ships was too strong. Separate the actual dependency closure, preserve internal test access and player coverage, and verify runtime does not need validation types. Do not merely relocate folders or rename namespaces for appearances.

Anchors: `SuccubusPerformer.cs:773`; runtime harness/self-test sources; Day 1 [dependency map](DEPENDENCIES.md). SALSA, HDRP, Rigging, Player, Motion and Toys edges must be accounted for together.

### 4. Character-specific rig conventions remain in generic runtime paths

Even with an assigned speech source, `ResolveSpeechAudioSource` requires an ancestor literally named `head`; its fallback searches `Genesis8Female`. Wardrobe body fallback uses `CapturedOpening`; footwear discovers `lFoot/rFoot/lToe/rToe` and uses `LaraHeelReview`; wardrobe invokes `LaraAnatomyControls`. These are stronger assumptions than a reusable performer plus serialized binding would suggest.

The accepted Lara prefab works, and a G8M partner does not need to become another `SuccubusPerformer`. This is a real second-performer portability limit, not a present Lara failure or justification for a universal rig framework. Explicit rig references and character-specific adapters/profiles are the appropriate eventual seam.

Anchors: `SuccubusPerformer.cs:1511`; `PerformerWardrobe.cs:144`, `:476`; `FootwearPoseDriver.cs:28`. A class named Review is not automatically test machinery: `LaraHeelReview` currently implements real footwear behavior.

### 5. Content validity partly depends on development folder paths

Gesture and action readiness require clips beneath `Assets/DazPose/Generated/Gestures/` and `Generated/Actions/` in editor code. The same folder restrictions are absent in a player. A compatible prepared clip moved into an ordinary content folder can therefore be rejected in the editor. Preparation should establish compatibility/provenance; folder placement should not substitute for that contract.

Keep the valid requirements for baked Generic clips, finite skeletal curves and prepared metadata. This matters when authoring new content assets, not simply selecting the already prepared catalog.

Anchors: `DazPose/Runtime/Performer/PerformerGesture.cs:46`; `PerformerAction.cs:103`.

### 6. Failure diagnostics still direct authors to acceptance installers

Missing speech, teleport and dissolve configuration still tells the caller to run specific acceptance menus or save `PoseValidation.unity`. Those instructions are obsolete for the production prefab workflow. Errors should identify the missing production binding/profile and supported repair, without requiring harness knowledge.

This is a small, concrete fix with direct author value. It does not require redesigning the capability owners.

Anchors: `SuccubusPerformer.cs:1521`, `:1684`, `:1704`.

## Outstanding proof, not an established architectural failure

There is no delivered composed public-API sequence yet that combines real partner contact, wardrobe changes, speech, transitions, shared playback and Stop/replay. The component tests do not establish that composition. Conversely, its absence does not prove those combinations are broken. The agreed next step should answer this by writing the actual sequence and fixing demonstrated missing seams.

No current evidence establishes runaway allocations or cache growth. The old broader wardrobe integration/stress/walk gates have not all been rerun against the final Day 5 source. Run the useful existing gates once as required, investigate failures, and do not call unknown performance a diagnosed leak.

The main `EditorBuildSettings.asset` still has an empty scene list. Existing builds supply exact validation scenes explicitly. A selected production composition and reproducible production build entry point remain to be delivered; standalone building itself has already worked.

## Limits and deferred features, not unfinished corrective architecture

- Contact is right-hand, prepared rigid endpoint, zero compliance and unit-scale. Root movement is supported. Partial/flaccid/deforming contact is rejected explicitly rather than silently using incorrect bone geometry.
- There is no grip-driven member compliance or mesh squeeze. `PartnerBody.SetCompliance` exists, but the certified contact path rejects nonzero compliance; no exported squeeze channel is established. These are intentionally deferred capability expansions, not a missing rigid contact adapter.
- General male dissolve/full performer behavior and first-person body visibility modes are not established. They are requirements only if the authored content calls for them.
- Generated fit/release attempts accumulate without automatic pruning. That is a disk-hygiene task; no current result establishes a runtime leak. Draft edits no longer compile a mesh per ordinary UI change.
- No content DSL, line registry, timeline or personality system exists. The user explicitly put those after performance consolidation. Raw bound clips plus SALSA are sufficient for this sequence; a separate viseme-track format is not a missing prerequisite.
- Physical device certification and independent recipe-only import acceptance remain distinct from architecture and mock/reimport regression passes.

## Consequence for the remaining work

Drive the rest through the authored sequence. Prioritize simple owned commands, accurate required-capability preflight and useful error messages. Establish the real production/validation dependency boundary while maintaining player coverage. Encounter content-path and rig portability limitations only where the sequence or intended authoring contract requires them. Keep deferred deformation features deferred. Existing regression gates support the exercise; they do not become its product.

This reassessment supplements the authoritative plan; it does not claim Day 6 implementation or waive existing required gates.

