# Build plan

All paths below are repository-relative. Existing symbols are inspected at `e36a6d2`; proposed files/types are explicitly marked **new**.

Path abbreviations:

- **Studio** = `validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/`
- **Language** = `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/`
- **Story runtime** = `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Runtime/`
- **Performer** = `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Performer/`

## 1. Findings that determine the implementation

| Existing seam | Observed behavior | Required disposition |
|---|---|---|
| Studio/SceneDirectorStudioWindow.Authoring.cs: `SelectCue`, `AdjacentCue` | Selects a compiled PC, then calls `NavigateSelected`; rows disable while `transportBusy` | Keep the adaptive layout; select an authoring cue immediately, enqueue only the latest visual projection |
| Studio/SceneDirectorStudioWindow.Rehearsal.cs: `NavigateSelected` (line 210), `StartNativeSession` | Creates `RehearsalSession`, profile/catalog/scenario state; reaches a cue through `RunBeforeSelectedAsync` / `RunThroughSelectedAsync` | Remove this dependency from all Studio authoring paths |
| Studio/RehearsalSession.cs: `RunBeforeSelectedAsync`, `RunThroughSelectedAsync` | Advances `StoryMachine` instruction by instruction; restart can replay from the beginning | Preserve only for real rehearsal consumers if any; never use as a preview fallback |
| Studio/RehearsalVisualState.cs: `After`, `PresentationKey` | Copies the accumulated effect list on each visual command and grows a history string | Replace authoring state with compact current fields and structural keys |
| Studio/NativeSceneStage.Cumulative.cs: `Apply`, `SampleProperties`, `StageCumulative` | Re-scans all effects, resolves old bindings, considers earlier samples before staging | Add direct state projection; no conversion back to a full effects history |
| Studio/SceneDirectorStudioWindow.Native.cs: `CaptureAnchorStamp` | Walks all historical effects every anchor poll | Observe only selected-state dependencies |
| Studio/SceneDirectorStudioWindow.Revisions.cs: `CaptureStamp`, `CaptureLogicStamp` | Serializes/hashes source, global content and dependency graphs | Replace authoring refresh with scoped revisions; no graph scans on cue clicks or mark drags |
| Language/StoryParser.cs | Internal structured AST contains `if`, `otherwise`, `while`, `repeat`, `phase`, `play`, and calls | Export a small read-only authoring structure from the existing frontend |
| Language/StorySourceAnalysis.cs: `DazStoryCompiler.Analyze` | Already parses the AST, then returns compiled program and source-owned binding requirements | Add nonserialized authoring structure here; do not introduce another parser |
| Story runtime/GlobalStoryCardCatalog.cs: `StoryLinkedContent.Create`, `ContentFor` | Links published units and resolves aliases by source owner; creation requires a valid global catalog and companions | Reuse valid ownership/linking data, but allow the root inspector/index to work when an unrelated catalog/companion is incomplete |
| Studio/NativePreviewLease.cs | Owns baselines, AnimationMode, sampler, save suspension and restoration; full `Apply` restores everything; `ApplySpatial` keeps pose alive | Preserve ownership; add scoped pose/presentation updates and a complete restore/resume projection |
| Performer/PerformerBodyPose.cs: `SamplePose` (line 336) | Restores neutral state, samples standing pose at `StaticPoseSampleTimeSeconds = 0.5f`, then captures target | Current preview's default zero sample and historical sparse-pose accumulation are not the standing runtime contract |
| Performer/PerformerSeatingLayer.cs: `SetPersistentPose` | Custom seated clip uses time zero | Keep separate standing and seated sample rules |

These are source findings, not a profile of Art's hung editor. Both interpreter traversal and history-dependent presentation are removed; native sampling/render costs must then be measured separately.

## 2. Authoring state contract

### 2.1 New data types

Add **Studio/AuthoringVisualState.cs**, with plain managed types linked into the existing .NET test project:

- `AuthoringCueId`: source GUID/unit + declaration + structural statement identity. Include source span and duplicate ordinal; PC/line alone is not identity. After a source edit, remap only unambiguous matches; otherwise clear selection with a useful message.
- `AuthoringBindingRef`: source owner + binding kind + alias, with originating source location. Scene location aliases continue to resolve on the selected `StorySceneBindings`; asset aliases resolve in their own source companion.
- `AuthoringValue<T>`: `Known`, `Baseline`, or `Unknown`, with an origin and short reason. `Baseline` means the lease's saved scene value, not the previously clicked cue.
- `AuthoringVisualState`: Lara placement recipe, Lara pose recipe, Lara visibility; body placement/visibility; camera placement and aim mode. Retain the existing stable material profile as one optional presentation field so current shader cues remain useful.
- `AuthoringCueState`: cue identity, display text/span, direct binding requirements, compact Before/After states, and diagnostics.
- `AuthoringDependencySet`: the selected state's binding IDs, clips/profiles, scene marks, parent transforms, seat anchors/profile, and tracking offset.

No variables, profiles, scores, inventories, card usage, RNG, events, elapsed story time, visits, effect-history lists, or simulated speech belong in these types.

The present vocabulary has no player-body pose command. Show the body's real saved pose and explicit placement/visibility; do not invent a new script feature. Future vocabulary support must be explicit.

### 2.2 Visual commands and endpoints

Use a finite command-to-field table, verified against runtime source:

| Command | Authoring result |
|---|---|
| `Lara.StartHiddenAt(location)` | Full mark transform, hidden; clear seated placement |
| `Lara.StartIdle()` | Configured InitialPose with standing pose sampling rules |
| `Lara.DissolveIn(location)` | Visible at full mark transform; preserve pose according to existing runtime endpoint |
| `Lara.LocomoteTo(location)` | Destination X/Z and yaw, inherited root height as required by `NativeStandingRules` / runtime locomotion; no travel animation |
| `Lara.DissolveTo(location)` | Visible, destination/yaw and configured InitialPose; clear seated placement |
| `Lara.SitAt(location)` | Actual seat default rest and baked SitStart landing-root rule |
| `Lara.PersistentSitPose(pose)` | Known seat plus explicit seated pose; sampled hip alignment and seat yaw |
| `Lara.DissolveTo(location, seated: pose)` | Visible explicit seat/pose endpoint, sampled hip alignment |
| `Lara.PersistentPose(pose)` | Standing base-pose endpoint; retain appropriate placement. Invalid standing/seated combinations produce a local diagnostic |
| `PlayerBody.StartHiddenAt(location)` | Body placement and hidden |
| `PlayerBody.DissolveIn/Out()` | Body visible/hidden at retained placement |
| `Camera.StartAt(location)` | Saved PlayerRoot position / ViewRig full authored rotation; clears tracking |
| `Camera.Track(Lara)` | Actual `laraFocus`, after Lara's pose and root placement |
| `Lara.MagicShader(profile)` | Current stable material profile; no material-history playback |
| Speech, popup, waits, scores, clothing/equipment/session assignments | Visible source rows and relevant binding controls, but no permanent visual-state change |
| Gesture, Grip, motion/funscript | Expose bindings; explicitly disclose that transient gesture/contact/motion execution is not represented. Do not run solvers, devices, or timed playback |

Do not turn setup semantics into editable script commands on arbitrary cues. Inspector edits change existing asset/scene bindings, as today.

Placement recipes must be bounded. A move can refer to its retained height source (baseline, absolute placement, or seat endpoint), not a linked list of every preceding move. If that source is unknown, root height is unknown. A persistent seated pose consumes a known seat; its own assignment alone does not resolve an unknown seat.

### 2.3 Skipped logic, cards and ambiguity

Use structured source, never an interpreter or expression evaluator:

- Build setup state, then scan the selected sequence in source order. `phase` is a grouping container.
- For an unconditional literal `play Intro` or `Director.Execute(LiteralCard)`, apply a cached visual transfer summary of that named card once. This is static composition of assignments, not runtime execution. No eligibility/requirement evaluation.
- `if` / `else` are choice blocks: skip their branches for downstream visual accumulation. Inspect syntax for permanent visual writes only. If such writes exist, flag the authoring-rule violation and mark those affected fields unknown. Branch-local writes never leak into following cues.
- `while` / `repeat`: never calculate the condition/count or iterate. No visual writes means no visual change. Possible persistent writes mean those fields become unknown after the block. Card-summary write sets count too.
- `Director.DrawAndExecute`: do not draw a card. Mark only fields that possibly change unknown. Compute possible write sets from statically identifiable published cards; literal tags may narrow to selectable tagged cards containing **all** required tags (`Language/StoryCards.cs`, `Candidates`), but do not use runtime eligibility/weights/state. Dynamic equipment/filter expressions cannot justify excluding a candidate. When the catalog is missing/invalid, explicitly mark potentially affected visual fields unknown.
- Recursion, unresolved calls, and dynamic card IDs produce bounded diagnostics/write masks. Do not recursively expand or chase runtime paths. Cache summaries and detect call cycles.
- An explicit later assignment clears uncertainty only for the fields it actually establishes. `DissolveTo(Lounger, seated: Lounging)` can resolve placement/pose after a dynamic block; `PersistentPose` alone cannot resolve position.

Maintain one source row per authored statement (including statements inside skipped blocks), retaining the existing declaration sections. Use labels/indentation to show block membership; hide compiler-only bookkeeping. This ticket does not require a second execution-order list or expandable repeated visits.

For selecting a cue *inside* a skipped branch/loop, show that source-local editing context once from the block's entry state, with a clear “Inside skipped conditional/loop” label. Never apply it to the downstream outer state, execute a loop, or silently choose its siblings.

For cards:

- A card with one known unconditional call context inherits it (`Intro` from FirstContact setup is the main concrete example).
- If multiple explicit call contexts agree on a field, retain that value; disagreement is `Unknown`. Use memoized summaries and field joins, not enumeration of execution paths.
- Cards reached only dynamically have unknown incoming visual fields until their own commands establish them. Keep those card rows and binding controls available without requiring a Director draw.
- Do not pretend the previous declaration in the file supplies a card's incoming state. Declaration order and call order differ in FirstContact.

Unknown-field display is deterministic: restore that field's lease baseline, apply independent known fields, and show “Lara position unknown after [file:line]; saved position shown.” Never retain the last clicked cue as an unlabelled substitute. If a dependent operation cannot be resolved (seated pose without a seat, tracking without a known target position), mark it unresolved too; a diagnostic must remain visible while that fallback is displayed. Keep unrelated mark/asset controls functional.

This deliberately conservative analysis is acceptable. Do not add branch simulation to eliminate warnings. Report each warning's source and the explicit assignment needed to resolve it.

### 2.4 Index and complexity

Add **Studio/AuthoringCueIndex.cs** and **Studio/AuthoringVisualReducer.cs**.

Compute compact Before/After states at source-index build time. Reuse identical state objects for nonvisual rows. Use bounded, memoized declaration summaries; do not expand all call paths or duplicate histories. Large call graphs must have explicit cycle/work limits and a diagnostic instead of unbounded work.

A cue click is a dictionary/array lookup plus comparison/application of current visual fields. Its work must not depend on the number of earlier cues, loop counts, branch reachability, or story duration. Index work scales with source statements/call edges and bounded visual fields; projection work scales with the selected actors' rig/clip data. Compare visual values by binding/recipe identity and relevant revision, separately from diagnostic provenance: identical shots on different source lines must compare equal.

## 3. Ordered implementation tickets

### A00 — Baseline and authoritative handoff

1. Inspect HEAD, status, active instructions, existing fixture marker and old G4 evidence.
2. Record the superseding product decision in a new `docs/visual-authoring/IMPLEMENTATION_REPORT.md`; mark implementation pending and preserve old failures.
3. On execution authorization, add this packet as the top active section of `AGENTS.md`, retaining all execution/protection restrictions. Update `docs/native-directing/ACCEPTANCE_STATUS.md` to point to this correction; do not mark G4 passed.
4. Record owned files and existing unrelated changes. Capture source baseline and `python scripts/native-directing-fixture.py verify`.

Exit: baseline preserved, no Unity or content mutation.

### A01 — Structured authoring metadata from the existing parser

1. Add **Language/StoryAuthoringStructure.cs**, public plain read-only DTOs for declarations, statement kind/span, normalized call parameters/literal binding refs, nested bodies/otherwise and deterministic source identities.
2. Extend `StorySourceAnalysis` with optional authoring structure populated by `DazStoryCompiler.Analyze` from the AST it already parsed. Reuse vocabulary parameter naming and validation.
3. Keep `StoryParser` internal. Do not expose its mutable AST, copy its grammar into the editor, or reconstruct `if` versus `while` by heuristics over jumps.
4. This metadata is an analysis result only: no change to `CompiledStoryProgram` serialization/version or runtime instruction semantics.
5. Extend source-manifest caching to retain it. Missing companion bindings must not prevent syntactically valid source rows from appearing.

Checks: nested blocks, multiline named args, semicolon-separated statements on the same line, duplicate aliases across owners, parse errors, unchanged compiled instructions for existing fixtures.

Exit: structural source data available to the editor without StoryMachine.

### A02 — Compact visual index and diagnostics

Implement section 2 in the new managed files. Keep Unity object loading out of the reducer.

Add **Studio/AuthoringSourceCatalog.cs** to capture root and available published source analyses plus owner-to-companion mappings. Reuse existing source identities/reconciler lookup. `StoryLinkedContent.Create` can supply valid linked metadata but must not be the sole all-or-nothing gate for authoring. Never automatically reindex, create companions, reconcile, or mutate assets while indexing; those remain explicit inspector actions.

Include declaration summaries, direct requirements, inherited binding provenance, branch/loop-local contexts, missing/dynamic context warnings, and Before/After lookup. No root-source hardcoding.

Run focused managed checks before wiring Unity. Do not build UI choices for seed, profile, eligibility or loop occurrence.

Exit: direct random access to any source cue; tests prove skipped logic cannot wait or execute.

### A03 — Direct native projection and scoped updates

1. Add **Studio/NativeSceneStage.Authoring.cs** with `ApplyAuthoringState(...)` accepting compact resolved state and scoped bindings. Keep the native stage/context/lease; do not construct a replacement camera, actor clone, or custom viewport.
2. Reuse `NativeStandingRules`, `NativeSeatEndpoint` / `NativeSeatedProjection`, `NativeSeatingMath`, `PlayerViewEndpoint`, material ownership, and actual saved camera. Refactor shared pure endpoint helpers if necessary; do not call runtime async commands or start performer runtime services.
3. Resolve only bindings needed by the selected endpoint. Missing audio, pose or seat in an unrelated cue must not stop it.
4. Give the lease separate dirty groups for pose, placement, camera, visibility and material, while preserving full restoration before serialization/final disposal. A changed pose restores only pose-owned values to the proper neutral baseline, samples the new pose, then solves dependent placement/aim. Unchanged roots/materials/body stay in place.
5. Maintain one complete projection callback for save resume. A spatial or pose delta must also update that desired complete state: saving after a mark edit must resume the newest shot, not an older captured closure.
6. Retain the sampling graph across navigation/assignment; no lease or graph reacquisition for a normal cue change. Clear old sampled-property contributions for sparse clips. Standing uses runtime's neutral-baseline and 0.5-second convention; seated custom poses retain time zero. Verify the seat-default sample time using existing seat frame resolution. Add a small shared convention/accessor if required, without changing Play behavior.
7. Audit neutral-baseline capture against `PerformerBodyPose`/`SuccubusPerformer` initialization. Do not assume the current preview pose is neutral, reset authored artistic settings, or accumulate all historical clips to fill absent curves. Reuse the lease's pre-preview capture where it matches runtime's neutral capture, explicitly documenting any unresolved difference.
8. Retain release-only mark updates by default and optional Live drag. Reanchor from the selected dependency set. Pose/seat geometry changes require resampling; pure anchor changes use cached local hip/root measurements and current transforms.
9. A failed selected-field resolution produces a local diagnostic and safe baseline fallback. Ownership/lifecycle violations still stop and restore preview. Do not weaken the latter to keep a picture visible.

Exit: state projection has no dependency on `RehearsalVisualState.Effects` or executed history. All writes remain owned and restorable.

### A04 — Replace Studio transport and invalidation

1. Add **Studio/SceneDirectorStudioWindow.VisualAuthoring.cs** (or equivalent controller) to own index generation, desired cue, selected snapshot and projection scheduling.
2. Replace `SelectCue`/`NavigateSelected`/Previous/Next/Before/After/Preview with direct index lookup. Select/highlight/update the inspector immediately. Coalesce requests so only the latest requested cue is projected; old callbacks cannot overwrite it after selection, Stop, source change or reload.
3. Remove `Finding cue`, global `transportBusy` disabling, pending-response UI, branch overrides, seed/loop controls, and advanced runtime transport from the authoring window. Do not hide an interpreter behind a fallback or retain it in background ticks.
4. Render inherited controls from compact state origins, not `Effects.LastOrDefault`. Keep direct pose drag slots, missing-mark creation, hierarchy assignment, Select/Frame, source ownership, shared-alias explanation, undo, and wrapped left-aligned source text.
5. In `QueueBindingRefresh` / `QueueCueRefresh` and `.Revisions.cs`, replace whole-program resets with dependency invalidation:

   | Change | Work |
   |---|---|
   | Select/Before/After | Lookup cached state; apply changed fields only |
   | Selected mark/parent transform or focus offset | Spatial update only |
   | Pose assignment or clip reimport | Invalidate relevant pose recipe/sample/seat solve; no source-index rebuild |
   | Material assignment | Relevant presentation field only |
   | Seat/profile/posture metadata | Re-resolve dependent geometry/validation; no interpreter or source parse |
   | Source text / declaration / published source set changes | Rebuild affected static analysis/index, then resolve current selection |
   | Unrelated asset or audio changes | No native shot rebuild |
   | Host/actor/camera identity or active scene change | Restore/dispose old lease, reconnect explicitly |

6. Do not serialize every asset/global dependency on each repaint, click or drag. Capture Unity inputs/revisions on the main thread at change events; pure analysis can run over immutable captured text/DTOs in a cancellable worker. Never access AssetDatabase, transforms or Unity objects from that worker.
7. Bound main-thread indexing preparation and sidebar layout. Cache cue text, requirements, row sizes and search results; virtualize rows if needed. No filesystem reads/analysis in OnGUI. While a source rebuild is pending, preserve inspector responsiveness, show updating/stale status, and don't present old state as current source.
8. Replace active `Tick`/revision/report callbacks that still depend on rehearsal fields. Audit all partials and diagnostics with `rg`, not just the visible methods.

Exit: actual Studio works without constructing `RehearsalSession`, `RehearsalStoryHost`, `StoryMachine`, or `StoryDirector`.

### A05 — Regression, performance evidence, and native handoff

1. Follow [CHECKS_AND_ACCEPTANCE.md](CHECKS_AND_ACCEPTANCE.md).
2. Update `SceneDirectorStudioWindow` native reporting and `NativeDirectingG4Window.cs` to record cue identity, state uncertainty, selected dependencies, generation, projection kind and timings. Replace old scenario/visit/Director acceptance checks with authoring checks; preserve baseline/save comparison tools.
3. Audit `StudioCorrectiveChecks.cs`, the rehearsal partial, and tests for reflection/callers of removed methods. Delete editor-only dead transport and obsolete authoring tests after accounting for callers. Preserve runtime interpreter and meaningful existing managed runtime tests. Do not build a new “advanced rehearsal” product in this packet.
4. Run standalone checks, inspect the owned diff, locally commit coherent work, and sync code only using `python scripts/native-directing-fixture.py sync-code --gate G4`.
5. Replace the current top G4 handoff with precise instructions for the actual UI and fixture. Preserve old directions as historical evidence, explicitly superseded.
6. Stop at **WAITING ON ART**. Report code identity and source-check results separately from unverified native behavior.

### A06 — Art-reported repair and completion

If Art reports failure, repair the failing behavior and repeat the relevant checks. Do not demand screenshots or use source results to overrule native observations.

After Art's explicit G4 acceptance, finish documentation/retirement and the prior N11 delivery disposition. Record which native cases were actually observed; do not infer Play timing, gesture/contact behavior, or whole-scene runtime approval from visual authoring acceptance.
