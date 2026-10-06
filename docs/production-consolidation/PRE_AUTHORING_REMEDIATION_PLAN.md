# Remediation before the composed authoring scene

## Authority and outcome

The user explicitly changed the remaining work order after the six-review reassessment: repair issues 1, 2, 3, 5 and 6, investigate the second-outfit toe regression, and then begin the all-up public-API authored scene. Issue 4 (other-character portability) is excluded: Lara is the supported performer, and working rig/name conventions are left alone. This packet governs the remaining corrective work alongside the seven-day plan and preserves its required integration/player gates.

This request authorizes a grounded plan. The work below is planned, not implemented or execution-certified. Days 1–5 retain their accepted status; the newly reported wardrobe defect is a tracked follow-up rather than a retroactive claim that earlier checks caught it.

The outcome is a small owned authoring surface with reliable Stop/replay, meaningful preflight, content assets that do not depend on development folder names, and a production build free of required validation code. No DSL, timeline, replacement controller, generalized rig system, new contact deformation or broad asset relocation is part of this work.

## Order and completion gates

| Order | Work packet | Concrete output | Gate |
| --- | --- | --- | --- |
| A | Diagnose and fix second-outfit toe state | Measured reproduction, smallest ownership/fit repair, regression using actual layer assignment | Bare feet return while Base clothing stays on; adding Layer 1 restores the reviewed shoe state; hosiery-only policy still works |
| B | Complete declared-capability preflight and diagnostics | Shared readiness checks and useful production errors | Known invalid required bindings/assets fail before commands, with owner/property/reason; valid declared subsets pass |
| C | Put lifetime plumbing behind semantic author calls | Thin run-bound adapter over the existing facade/scope | Authors await normal verbs without channel enums or cancellation callbacks; Stop/failure cleans only that run's work |
| D | Remove content-folder validity coupling | Location-independent prepared gesture/action validation | A compatible clip moved to an ordinary content folder works; malformed/incompatible content still fails |
| E | Separate production, editor and opt-in validation dependencies | Verified assembly closure, preserved GUIDs, production and validation build paths | Production player compiles without validation code; focused validation still works in editor/player |
| F | Verify readiness and hand off to authored-scene work | One consolidated execution/report set and author reference | Required current gates pass; repaired seams work in a Windows player; toe appearance separately reviewed |

Do A first. B precedes C so the new run can use meaningful preflight. D precedes the final assembly extraction because its editor validation seams are part of that closure. Only test bounded, documented work before F; do not start the full authored scene during these packets.

## A — second-outfit toes

### Current source and saved-data findings

The current main assets contain the user's edit: second-outfit stockings, stocking shells and shoes are Layer 1; other clothing remains Base. The reachable masks are 3 → 1 → 0. The saved mask-1 fit already has `footwearActive=0`, `bentFootPoseActive=0`, `standingLift=0`, and `footShrink=0`. The fully dressed mask-3 state enables both support and pose. Those observations are source/asset inspection, not a new playback reproduction.

Relevant paths beneath `validation/DazPoseUnityValidation/Assets/DazPose/`:

- `Editor/Wardrobe/WardrobeFitCompiler.cs`: derives foot state from visible parts and clones `package.reviewBody` for every nonzero clothing mask; naked uses the canonical body.
- `Runtime/Performer/Wardrobe/PerformerWardrobe.cs`: swaps fitted body, restores morphs, configures footwear, then commits; failed commits restore prior fitted state.
- `Runtime/Performer/Wardrobe/FootwearPoseDriver.cs`: disables pose/support on mask 1 in this assignment.
- `Runtime/Performer/LaraHeelReview.cs`: applies reference toe positions/rotations when constrained; `UndoPrevious` restores rotations only. This is a plausible retained-transform path, not yet the demonstrated cause.
- `Editor/WardrobeImporter.cs`, `CaptureHeel`: copies exported toe joint positions and recalculates mesh bind poses. Therefore an active support flag is not the only source of foot shape.

The reproduction must distinguish retained bone position/rotation, morph weight, fitted mesh/bind pose and active support. Do not declare a fix from boolean/height checks alone. Do not treat all bent feet as wrong: accepted policy retains the exported bent-foot pose while hosiery requiring it remains visible, with shoe support/lift removed. The user's case removes both shoes and stockings, so that exception does not apply.

### Investigation and implementation

1. Capture hashes and copies of the user's current catalog/configuration/presets and fitted dependencies. Preserve those edits. Use their mask-3/mask-1 assets in isolated staging; do not restage an older Day 5 configuration over them.
2. Reproduce in the actual saved Clothing Setup scene or a saved copy with its existing production prefab. Fix the pose/placement so the comparison does not conflate gait with footwear. Record fully dressed, after Layer 1 removal, after Base removal, and after re-addition.
3. At each state record fit identity/flags, body mesh and bind-pose identity, relevant body morph weights, pelvis/root lift, local foot/toe/metatarsal positions and rotations, and evaluated foot/toe geometry. Compare mask-1 feet with canonical naked feet under the same pose; normalize root translation. Sample before and after the late footwear application to identify the writer.
4. If constraints retain state, give footwear a reversible record of the actual position AND rotation channels it changes. Release its writes on support/pose transitions, rollback and disable, respecting later animation/other-owner writes. Do not force every toe to identity or replay old captured transforms indiscriminately.
5. If the nonzero fitted mesh retains exported shoe-specific geometry/bind state, make the affected fit compiler generate a correct footwear-inactive state while preserving body topology, UV coverage, other morphs and remaining garments. Preserve the reviewed fully dressed/hosiery states. Do not assume the canonical body can replace every fitted body safely.
6. Recompile affected drafts into new attempt assets; publish a new release only through the existing protected publisher if release fixtures need it. Never patch an immutable release in place or change source imports/calibration to hide the defect.

### Acceptance

- Second outfit 3 → 1: stockings/shoes gone, remaining Base garments visible, zero shoe lift/support/shrink, and evaluated toe geometry follows the correct barefoot pose.
- 1 → 3 restores reviewed footwear without accumulated offsets; 1 → 0 → 1 stays barefoot.
- Existing third-outfit hosiery-only state still retains its intended foot shape without shoe support. Use actual role/requirement data, not a global rule tied to outfit IDs.
- Failed transition/rollback, actor disable/re-enable and a second actor do not retain or share foot overrides.
- Run in editor and Windows player. Provide one useful foot close-up for user appearance approval; a saved runner starts on Play. Earlier mask/height checks are extended with actual transform/geometry assertions, not merely repeated.

## B — preflight and production diagnostics

Reuse `PerformanceSceneBindings`, `BindingIssue`, the existing asset `IsReady` checks, `PartnerContactTarget.TryEvaluate`, and wardrobe generation/compile diagnostics. Avoid a second readiness system with different rules.

1. Separate edit-time structural readiness from runtime readiness. Edit-time validation must not start commands, bake meshes, instantiate effects, mutate assets or require an initialized actor. Runtime validation checks the enabled bound owners and current supported state after initialization.
2. Add a narrow optional target-readiness capability or production-target validator that the prepared target can implement. Keep `IGripTarget` evaluation source-neutral; do not downcast every target to partner anatomy or require a replacement generic controller. Validate profile/calibration, body/shell identity/visibility, scale and supported endpoint before acquisition. Runtime commands retain their live checks when state changes after preflight.
3. Wardrobe validation uses the same catalog/configuration-generation checks and initialized binding diagnostic as runtime. Required outfit declarations resolve against released data and check actual reachable fitted dependencies. Candidate preview remains an explicit setup exception.
4. Add required content declarations for gestures, actions, spells and auras using existing readiness validators, including target dependencies and actor mask compatibility where relevant. Pose/expression/clip/seat checks remain in place. Only requested capabilities/assets should block a sequence.
5. Replace installer/scene-specific failure instructions with the actor, missing binding/profile/asset and a production repair. Preserve working Lara/head discovery; this changes messages and readiness, not rig support. Expected Busy, unsupported contact and wardrobe no-op outcomes remain distinct from missing configuration.

Acceptance cases: missing/disabled contact body, wrong profile meshes, unsupported endpoint/scale; mismatched released config or stale candidate; malformed action/gesture/magic content; valid speech-only sequence without optional contact/toys; live target loss after a successful preflight. Check owner/property/reason, not only failure count. Link messages to existing inspectors where practical, without building a new repair framework.

## C — simple author calls with owned lifetime

Keep `SuccubusPerformer`, `PlayerController`, `ToyControlService`, `MotionDriver` and `PerformanceExecutionScope`. Add a thin per-run author adapter, rather than replacing the underlying facade. The established public calls remain usable; low-level completion enums remain available for advanced callers.

### Author-visible contract

The names below illustrate the intended shape and are not a rename of existing vocabulary:

```csharp
using var run = /* begin a run from typed scene bindings */;
var succubus = run.Performer;
await succubus.DissolveInAtAsync(startLocation);
await succubus.SayAsync(welcomeClip);
await succubus.TryRemoveLayerAsync();
await succubus.GripAsync(run.ContactTarget);
succubus.FollowGripMotion();
await run.Delay(2f);
```

The sequence chooses actors/assets/targets and ordinary timing. It supplies no `PerformanceCommand` enum, expected terminal enum, cancellation delegate or solver component. The host provides Stop and declared reset/replay. Optional branching can inspect a structured result, especially safe wardrobe no-ops.

### Implementation rules

1. Begin the run through B's preflight, then bind run-scoped actor/player/playback/toy adapters. Two runs with separate actors/clocks have no global current-run state.
2. One private command adapter maps existing completion types to success/no-op/cancel/failure and captures exact request cancellation at acceptance. Consume each underlying Unity Awaitable once; the author's wait and the scope's observation must never await the same pooled object twice.
3. Finite wrappers preserve existing completion points and FIFO/replacement policies. Grip finishes on acquisition; successful acquisition automatically retains release ownership. Gaze, tracking, held arrival, aura and followed playback retain exact owned cleanup without an author remembering `persistent: true`.
4. First unexpected failure records actor/verb/reason and cancels pending siblings plus owned persistent work. Stop is idempotent, works while gameplay is paused, and does not cancel newer replacement requests or another run's external clock/output. Expected wardrobe boundaries remain successful no-ops.
5. Explicit scoped playback commands distinguish starting owned motion from consuming external motion. Use existing driver revisions and toy route generations. Detaching a consumer does not restart/seek/stop another owner's playback. Required/optional devices follow the declared binding policy.
6. Provide simple overlapping work without introducing a global command queue. Speech/gaze/contact can legitimately overlap. Keep existing strict/replace/reject policies for body/root/arm contention.
7. Reset/replay uses the declared actor defaults and initial wardrobe/placement already established by First Contact; adapt that implementation rather than snapshotting every inspector field or creating a general state engine. Temporary overrides introduced through the run must have declared instance cleanup. Do not mutate shared profiles.
8. Cover the current verb inventory by category: pose/expression, speech, move/turn/seat/stand, visibility/teleport, action/gesture, wardrobe, grip/hold/follow/release, gaze/tracking, spell/aura, player view and explicit playback/toys. Reconcile each with `VERBS.csv` and `DAY_3_COMMAND_BEHAVIOR.md`; do not advertise a wrapper whose lifetime is not implemented.

Before the all-up scene, migrate the existing First Contact sequence to this surface as a bounded demonstration of removed plumbing, retaining its behavior and saved bindings. It is an existing sequence, not the new every-capability content exercise.

Acceptance: ordinary serial calls; concurrent speech and gaze; failed command cancels siblings; Stop during speech/reveal/wardrobe/grip staging and after acquisition; replacement survives stale Stop; gameplay pause; owned versus external clocks; no optional hardware; repeat run after Stop. Verify terminal outcomes and cleanup. Use existing Day 3/4 regressions as support, not a new fixture for every overload.

## D — content location independent of validity

`PerformerGesture.IsReady` and `PerformerAction.IsReady` currently accept only clips in specific `Assets/DazPose/Generated/...` folders during editor validation. Those prefixes are not checked in a player.

Remove the directory prefix as a validity requirement. Keep compatible baked Generic/nonlegacy clips, standalone `.anim` ownership where genuinely required, finite skeletal curves, correct additive reference, root trajectory metadata, and rejection of object-reference curves/events. Keep the existing baking/preparation tools and asset names.

If persistent preparation identity is needed to replace a path check, add only the narrow metadata necessary and populate it for existing assets through an explicit, idempotent editor migration. Do not create a general content registry. Editor curve checks remain stronger author-time validation; serialized executable data must be enough to reject missing/invalid content in a player.

Acceptance: clone a valid prepared gesture/action into an ordinary content folder and a folder with spaces, save/reopen, and execute through the normal API in editor/player. Malformed curves, bad reference/root metadata and unsupported clips still fail. Moving an existing asset with its meta preserves its GUID; no broad relocation is required.

## E — production boundary without changing working Lara behavior

Start from `DEPENDENCIES.md`, refreshed for the new adapter and current imports. A Runtime directory name is not evidence that a class belongs to runtime; a `Review` name alone is not evidence that it is a test. `LaraHeelReview` supplies real footwear behavior and stays a production dependency until A's bounded repair is complete.

1. Inventory actual source/type references across DazPose runtime, Player runtime, First Contact, editors, self-tests, legacy demo controls and saved runners. Classify by dependency and use. Confirm SALSA, Buttplug, HDRP, Animation Rigging, Unity modules and any vendor source edges before adding definitions.
2. Establish a small first-party runtime assembly containing the real capability closure. Player may share that assembly if it avoids a needless cycle; do not split every subsystem into its own assembly. Runtime cannot reference code left in `Assembly-CSharp`.
3. Establish editor assembly closure and a dedicated validation assembly usable in the editor and opt-in validation players. Move scripts together with their original meta files where needed. Keep serialized class names/namespaces stable unless a real dependency demands change; no Lara renaming campaign.
4. Keep runtime tests that need a player in validation. Preserve internal access with narrowly named friend assemblies. Move legacy acceptance/demo controls with their dependencies. Demote public acceptance-only entry points to the supported test seam; retain legitimate semantic runtime calls.
5. Isolate runtime asset validators' `UNITY_EDITOR` portions without introducing runtime-to-editor assembly cycles. Their preparation/editor helpers stay available to current inspectors and import tools.
6. Define explicit production and validation build selection. Validation compilation is opt-in for players; production compilation must not need its code or symbols. Update the shared staging/import/validation scripts to synchronize new assembly files and moved sources, not merely their old directories. Selected validation scenes retain saved runners.
7. Compile after each coherent boundary change in isolation. Open/save/reopen representative production prefabs and Clothing Setup to check serialized script resolution. Verify existing meta GUIDs, catalog references, user layer assignments and protected artistic inputs. Do not use Missing Script warnings as an acceptable intermediate deliverable.

Acceptance: production player builds with validation excluded; compiled assembly references contain no runtime → validation edge; the production composition dependency closure has no required smoke panel/test controller; editor utilities still compile; saved validation scenes and current assertions run with validation enabled. SALSA/Buttplug and HDRP behavior remain usable in Windows. Do not infer physical hardware acceptance from DLL presence.

## F — focused execution, remaining real issues and readiness

Use one isolated synchronized Unity project, one Unity process per project, and source/asset identity checks. Do not operate the Unity editor UI. Preserve the original Void environment and Lara visibility lights. Never overwrite user-edited main assets from a stale staging copy.

Run targeted A–E checks as their code changes require. Once the boundary settles, run the existing required wardrobe gates in order once: contract/migration, runtime/layers, setup/persistence, integration/recall/effects, stress/cache and walking/culling, and player coverage. Reuse existing implementations; a failure becomes a specific repair, a pass is not a reason for another round. Keep actual user-facing Save/Compile regressions because the older suites alone did not prove them.

Deliver a small saved remediation validation scene only if the existing saved scenes cannot host these checks cleanly. Its runner is saved, the authored Void is retained, and a foot close-up is useful from the initial camera. Build and test the exact delivered scene. Separately build a minimal production smoke composition with validation excluded; it demonstrates dependency independence without claiming the later all-up performance exists. Use an explicit scene list/build entry point rather than the empty main build settings. Do not invent a scene name or claim it has been delivered before creating it.

Take a short count/allocation observation during the existing repeated operation checks to identify accumulation. No speedup claim, exhaustive benchmark, cache-pruning project or new performance budget is justified by an unknown. Only measured defects expand implementation work.

Write a compact author reference for the scoped calls: completion, overlap, Stop, defaults, no-ops and supported contact/device limits. Update current handoffs and report user approvals separately from execution. The known contradictory historical Day 3/4/wardrobe status paragraphs should be labeled/superseded so the next agent does not restart completed work.

### Ready to begin the all-up API scene means

- The second-outfit foot regression is reproduced and repaired through its actual layer transition, with a focused human visual check still recorded separately.
- Simple scoped calls own their work and have consistent failure/Stop/replay behavior.
- Required capability/content failures identify a production reference before playback where possible; live invalidation remains safely guarded.
- Compatible gesture/action content can live in ordinary content folders.
- Production runtime and build do not depend on validation code; validation coverage remains runnable.
- Current required gates pass or name an exact remaining release blocker. No source-inspection pass substitutes for an executed gate.
- The next task can write the full performance using only the public semantic surface, without first repairing infrastructure.

## Explicit exclusions and delegation

No other-character adaptation, generic rig map, bone renaming, new squeezing/compliance behavior, flaccid contact, left-hand contact, content interpreter, line registry, timeline or automatic generated-asset pruning. Preserve working artist choices and protected releases.

Root owns toe diagnosis, lifetime/ownership decisions, dependency extraction and integration evidence. Luna is appropriate only for bounded tasks with actual savings, such as replacing an enumerated set of stale diagnostic strings or adding a precisely specified content-location regression after the interface is fixed. Do not delegate architectural design or create multiple competing implementations. No subagents were needed to write this plan.
