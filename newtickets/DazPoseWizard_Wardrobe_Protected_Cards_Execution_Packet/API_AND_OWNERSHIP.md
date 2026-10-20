# API and ownership decisions

These names and responsibilities are fixed for the executor. Ordinary private helper names and splitting a large class into partials may vary. If a public signature must change for an evidenced source constraint, record the exact reason and equivalent contract in the report; do not silently choose a different architecture.

Path abbreviations: `A=validation/DazPoseUnityValidation/Assets`, `P=A/DazPose/Runtime/Performer`, `L=A/DazPose/Runtime/Story/Language`, `S=A/DazPose/Editor/StoryStudio`, `E=A/DazPose/Editor/Wardrobe`. Keep current assembly definitions; the Language layer has no Unity dependency.

## 1. Appearance data and prefab binder

New types under `P/Wardrobe/`, namespace `DazPose.Performer`:

| File/type | Fixed responsibility |
| --- | --- |
| `WardrobeSelection.cs` | Immutable selection: generation ID, resolved preset ID or unclothed, ceiling -1..2, exact hair key or empty. No relative Keep value. Copy inputs; equality includes generation. |
| `WardrobeSelectionResolver.cs` | Pure selection reduction for SetClothing, LegacyOutfit, SetHair, ClearHair, AddLayer, RemoveLayer, ExactRecall. Reuse WardrobeLayerState. Input contains already captured catalog metadata; no renderer, scene or AssetDatabase work. Output distinguishes change, benign no-op and structured failure. |
| `WardrobeSelectionPreparer.cs` | Shared side-effect-free preparation of a resolved target: released preset/fit refs, full/visible masks, per-piece bind/mesh/material/coverage overrides, footwear and dissolve/surface refs. Return `WardrobePreparedSelection`. Cache by selection+generation+configuration/asset revision. No controller initialization. |
| `WardrobeAttachmentDescriptor.cs` | Prefab-root MonoBehaviour with serialized private metadata and read-only accessors: schema=1, role Hair/Clothing, generation, character signature, stable key, preset/source identity, parts/local-bone descriptions. No Update/Awake mutation. |
| `WardrobeRigManifest.cs` | ScriptableObject, schema=1, generation/catalog identity, signature, preset->clothing-prefab rows, hair-key->hair-prefab rows. Generated complete map, exact-key lookups and validation. No independent layer/fit authority. |
| `WardrobeRigBinding.cs` | Passive prefab component, explicit manifest/ClothingRoot/HairRoot. `BindOwner(PerformerWardrobe owner, SkinnedMeshRenderer body)` validates same actor and compatible generation. No second queue or selected-outfit state. |
| `WardrobeAttachmentBinder.cs` | Shared attachment construction/binding and resource ownership. Canonical lookup validated once per body skeleton; local bones scoped per instance. Returns `WardrobeAttachmentLease : IDisposable`. Disposing destroys only its own created instances/materials. |

`PerformerWardrobe` gains a serialized `WardrobeRigBinding` reference. Runtime inventory records are produced by the shared binder; replace eager `BuildPieceInventory` on the new path. Legacy scenes lacking a rig retain the same extracted binder via a descriptor synthesized from their released package, without creating Project assets. This is one binder, not two maintained implementations; production Lara migrates to the prefab path in W09.

Runtime keeps the existing transactional body/fit/effect/footwear commit and rollback, using prepared selections. Editor projection consumes the same prepared selection/binder with its own lease adapter; it must not call the runtime queue/Awake/EnsureInitialized.

Add the following API at `SuccubusPerformer` and `ScopedPerformer` (existing OutfitAsync overloads retain behavior):

```csharp
Awaitable<WardrobeChangeResult> SetOutfitAsync(WardrobePreset preset, WardrobeTransition transition = default);
// Existing SetHairAsync(string stableHairId, WardrobeTransition transition = default) is reused.
// ClearHair maps to SetHairAsync(string.Empty, transition).
// Existing TryRemoveLayerAsync / TryAddLayerAsync are reused.
```

SetOutfit uses Keep hair policy in the existing request queue. Legacy Outfit uses its preset policy. Resolve hair before comparing targets for AlreadyEquipped. Existing cancellation/failure semantics stay intact.

**Cache policy:** per actor/preview owner, at most two unpinned clothing and two unpinned hair instances, MRU eviction. Current target and active entrance snapshots pin required resources for their scope. Maximum card nesting remains 128; pinned resources are bounded by active scopes, not historical cards. Dispose releases pins and resources. Never evict something a current renderer or rollback still needs. No AssetDatabase lookup in runtime or cue/mark update.

Asset layout for each generated rig:

```text
Assets/Production/Wardrobe/Rigs/<generation>/
  WardrobeRig.prefab             # WardrobeRigBinding + ClothingRoot + HairRoot
  WardrobeRigManifest.asset
  Clothing/<presetId>.prefab
  Hair/<stable-key-path>.prefab  # e.g. Hair/first-outfit/EmikoHair_59875.prefab
```

Generate all current catalog entries, only referencing released assets. Existing same-path output with matching content is a no-op; a differing existing generated asset is never overwritten in place. Report mismatch and use a new versioned derivative root recorded in the manifest/report. Old consumers keep their assets. Keep metas stable on rerun.

## 2. Bindings, vocabulary and compiled version

- Append `StoryBindingKind.WardrobePreset=8`, `Hair=9`; values 0–7 unchanged.
- `StoryWardrobeContent { string id; WardrobePreset value; }` and `StoryHairContent { string id; GameObject value; }` in StoryContentBindings.cs. Arrays `wardrobe`, `hair`, default empty. Hair asset must be a prefab root carrying role Hair descriptor, matching the actor's manifest/release/signature. A scene object or descriptor-free prefab fails assignment/diagnostics.
- Accessors `Wardrobe(string id)` and `Hair(string id)` resolve exact source-owned aliases. Preserve ownerSourceGuid and all existing rows. No filename/name fallback.
- Importer version becomes 3 (current 2); compiled program `CurrentVersion=2` (current 1). Default/builder/link/filtered views preserve version; IsCompiled/runtime entry rejects mismatched programs with reimport guidance. Do not rewrite unrelated legacy JSON semantics.
- Add `StoryVerb.requiredChannels` (string array). Existing `channel` remains the fallback for old test data; one `RequiredChannels` helper computes distinct channels. New wardrobe verbs claim Body, Visual, Wardrobe. Dissolve's existing Body+Visual rule moves into the same helper. Locks release on failure/cancel and do not lock the ancestor Director invocation itself.
- All five new commands require await; their parameter types are bound assets where relevant. Use no automatic unawaited wardrobe queueing from DSL.
- `StoryContentBindings.Diagnostics`, linked mandatory diagnostics and ResourceIssue use exact owner content and actor readiness. Reachable wardrobe/hair declarations are derived; do not ask Art to duplicate the list manually in hidden scene arrays.

## 3. Playable entry resolution

Add `StoryPlayableEntry` (declaration kind, entry, optional card definition) and `StoryProgramLinker.ResolvePlayable(program, name, unit, source)`. Resolve a local sequence or card, or a public global card. A colliding local sequence/public card is an error, just as local/public card shadowing is. Keep `ResolveCard` for Director-only calls.

Use ResolvePlayable in compiler/link validation, runtime Play, reachability, playback windows and Studio summaries. Keep `StoryCode.Play`; do not add a second interpreter. Root helper setup is never invoked implicitly. Shared entry-depth counter/limit includes sequences and cards. Card invocation count stays distinct from Director history; direct/nested cards receive unique invocation IDs even though direct play does not earn Director history/progress.

Rename only First Contact's `card Intro` declaration to `sequence Intro`; `play Intro` stays. Preserve exact body/transcripts and bindings. Root `sequenceName` remains FirstContact. Helper sequence declarations must not cause Studio to guess the wrong root.

## 4. Pure-language card boundary

Add `L/StoryCardPresentation.cs` in namespace `DazPose.Story.Language` with no Unity types:

```csharp
public interface IStoryCardPresentationHost {
    Task<IStoryCardPresentationScope> EnterCardAsync(CardPresentationRequest request, CancellationToken cancellation);
}
public interface IStoryCardPresentationScope : IDisposable {
    Task ReturnAsync(CancellationToken cancellation);
}
```

`CardPresentationRequest` is immutable: qualified CardId, unique invocation ID, nesting depth, card Source and call-site Source. The scope owns its captured entrance snapshot; it is not stored on the machine's debug snapshot.

All card execution requires this interface. Production host implements it with native services; pure story/retained rehearsal test hosts implement an explicit recording/state model. Missing interface fails before the card body; no silent successful no-op scope. The authoring Studio does not instantiate this machine/host.

`StoryMachine.ExecuteCard` protocol:

```text
eligibility -> EnterCardAsync (barrier/capture/push scope)
           -> install CardContext -> Entry (normal body + finite child settlement)
           -> scope.ReturnAsync -> Check -> normal completion trace
finally: dispose scope without animation; restore prior CardContext; decrement depth
```

Enter installs the runtime child owner before returning. Return performs cleanup/restore and transfers ownership to parent, then marks Returned. Dispose of an unreturned scope releases owned resources/registrations only; it never returns presentation, launches work or hides a failure. Body fault/cancellation skips Return, retains original error, terminates the run and grants no progress. Preserve existing terminal failure/Stop behavior instead of attempting a new recovery engine.

Every path, including direct play and Director.Execute, dispatches a card through this method. Director.Complete and next caller statement remain after awaited successful Return. No return implementation in Direct or individual card bodies.

## 5. PerformanceRun ownership

New `P/Authoring/PerformanceOperationScope.cs`; split PerformanceRun into a partial if needed. Run owns root scope ID 0 and a run-local stack of child scopes. New APIs are internal:

```csharp
PerformanceOperationScope CurrentScope { get; }
PerformanceOperationScope BeginCardScope(string cardId, long invocationId);
Task WaitForPresentationIdleAsync(PerformanceOperationScope owner, CancellationToken cancellation);
Task SettleCardBodyAsync(PerformanceOperationScope card, CancellationToken cancellation);
void ReleaseCardWriters(PerformanceOperationScope card);
void AdoptRestoredOwnership(PerformanceOperationScope parent, PresentationOwnership ownership);
```

Each accepted operation captures owner scope/resource at start. Delayed completions and cleanup registrations use that captured owner; never consult the ambient current scope in a completion callback. Retain overloads/default-root behavior for existing session registrations.

Scope state: Open -> Returning -> Returned -> Disposed, or Open/Returning -> Cancelled/Faulted -> Disposed. Only one Return allowed; duplicate disposal harmless. Stop atomically invalidates all generations, cancels operations/return and clears all scopes. No state resurrection.

Barrier waits finite relevant ancestor presentation operations, not all StoryMachine children: exclude current/ancestor play/Director task, persistent tracking, idle loops and pure story waits. This prevents self-wait deadlocks. Runtime run operations hold the finite command records needed for the barrier.

Once a finite operation settles, remove it from the active list; persistent ownership moves to scoped handles. Prune StoryMachine's completed global children as well, retaining fault information through its existing failure signal and only active Tasks needed for final settlement. Entry-local task lists die with that entry. No ever-growing historical operation collection. At successful return, restored tracking/pose/grip generations receive new cleanup handles adopted by parent; invalidate stale child and superseded parent handles. Parent Stop still cancels the new generation. Root session cleanup (context cancellation, phase services) stays root-owned even if registered while a card is active; pass root explicitly.

Card-owned UI actions/subscriptions/looping consumers are scoped. Do not clear phase-wide parent actions/signals on a child exit. Extend action/subscription scope tokens rather than call BeginPhase/ClearActions globally. Ordinary finite speech/gesture tasks settle before return. Motion/timing/hardware policy is in section 6.

## 6. Native snapshots and restore coordinator

New `P/Authoring/PerformancePresentationSnapshot.cs`, `PerformancePresentationService.cs`, `CardPresentationScope.cs`; subsystem partials `SuccubusPerformer.PresentationSnapshot.cs` / `PlayerView.PresentationSnapshot.cs` / `PlayerBodyPresence.PresentationSnapshot.cs` in their current namespaces/directories. `PerformanceStoryHost` implements the pure interface and bridges Task/Awaitable on Unity's main thread; no Task.Run for native mutation.

Snapshot is immutable runtime-only data. Service signatures:

```csharp
PerformancePresentationSnapshot Capture(); // after entrance barrier
PresentationReturnPlan PrepareReturn(PerformancePresentationSnapshot entrance);
Task RestoreAsync(PresentationReturnPlan plan, PerformanceOperationScope returnOwner, CancellationToken cancellation);
PresentationVerification Verify(PerformancePresentationSnapshot entrance);
```

`PrepareReturn` validates every required reference/generation/fit before any visible mutation. Return plan carries domain diff flags, prepared wardrobe selection, exact endpoints and ownership restoration targets. Verification is mandatory for normal completion. Transform tolerance 1 mm/0.1 degrees; exact identity for assets, layer ceiling, visibility, modes and targets. Tracking verifies restored rule and target with existing runtime acquisition tolerances, not a stale image quaternion.

Subsystem records are fixed by field category (private field representation may adapt to existing types):

| Record | Fields / owner |
| --- | --- |
| `PerformerPresentationSnapshot` | Root parent ref/local/world frames, scale observation, base desired/settled pose target, seated overlay pose, seat/style/cached frames/anchoring mode, stable hidden, WardrobeSelection/fit pin, material profile, expression target/intensity, typed gaze target mode/ref/fixed point, aura asset/target, grip target/held position/follow mode and bound driver identity. SuccubusPerformer coordinates its private subsystems. |
| `PlayerBodyPresentationSnapshot` | Original male body root parent/local/world frames, stable hidden and supported pose/control state. PlayerBodyPresence coordinates its actual animation owner where present. No new player pose vocabulary is introduced. |
| `PlayerViewPresentationSnapshot` | Player root and ViewRig parent/local/world frames; authored camera local offset/projection fields touched by commands; fixed quaternion or tracking target+active settings. Live user head input remains external. PlayerView owns orientation state. |

Presentation capture also stores `Session.world.posture` as the existing presentation-derived observation for reconciliation after restored Lara control state. Do not restore other session/world fields.

Add exact endpoint relocation overloads to the existing performer/body dissolve presenters; current yaw-normalizing mark-based RelocatePresentationAsync is insufficient. No temporary GameObjects as return marks. Add `PlayerView.RestorePresentationAsync` using quaternion interpolation plus position, and genuine captured tracking reacquisition. Never emulate fixed rotation with LookAt.

Restoration phases, fixed order:

1. Settle finite card work; release this card's grip/follow/temporary writers/actions; freeze only competing orientation writers. Capture return cancellation generation.
2. Prepare all targets/diff. If none changed, transfer surviving ownership and return with no visible work.
3. Conceal Lara/body only where relocation, seat structural change or wardrobe rebind needs it. One conceal/reveal per actor, not stacked wardrobe+move dissolves. Hidden entrance never reveals.
4. While hidden where required, apply exact prepared fit/bindings/material baseline and cached root/seat frames; restore owning pose/seat modes. Pose/expression/gaze blends use 0.5 seconds, including zero-location-change returns, through per-request transition values rather than changing user defaults/tuning. Preserve full captured rotation. Reacquire supported grip/aura/follow intent through its owner; no duplicate animation graph.
5. Reveal only actors visible at entrance. Restore material profile after wardrobe refresh against correct renderers. Move/rotate view smoothly over 0.5 seconds and restore tracking from its true origin/target once actor endpoint is established; avoid competing writers and wrong-aim frames.
6. Verify, reconcile presentation posture mirror, adopt current-generation persistent ownership into parent, release snapshot pins, pop child scope. Only then complete.

**Motion/grip policy:** capture persistent grip intent/held value or FollowMotion+driver reference. Never rewind driver time or replay hardware. Parent-owned active driver continues on its live clock if compatible; card commands trying to replace/stop that parent-owned driver fail before mutation with owner/source guidance. On return, reacquire the captured grip and reattach to the same still-valid driver. A card-owned driver/consumer is stopped/released before return. A missing/stopped parent driver needed by the captured follow mode is a terminal return error. No blanket rejection of all active grip states as a substitute for implementing capture/restore.

Transient action/locomotion/seating/visibility transitions reach stable barrier; their ephemeral playback time is not replayed. Stable idle/breath/blink continue. Snapshot targets include all persistent facade modes. Complete the writer coverage table in W00; any persistent mode discovered outside these categories must be added to that subsystem's record before W06 is complete, not silently omitted.

Faults/Stop use existing terminal cancellation. Dispose releases resources in all states. Nothing in finally starts a return or outputs. Configuration/tuning is never restored from lifecycle snapshots.

## 7. Studio state and preview ownership

Extend `AuthoringField` with Outfit, Layers, Hair, then `Count`. Add `AuthoringFields.AllMask` derived from Count and replace every hard-coded 8/255/work-limit assumption in current index/reducer/tests. Appearance values are compact resolved target recipes, not history chains. Use the pure resolver and WardrobeLayerState for layer decisions. Card call output identity is fixed; sequence output composition is kind-aware.

Virtual exit row ID: `<declaration-id>|return-to-entrance`. Source rows keep existing IDs. Before exit is the statically known outgoing card body state; After exit is the joined entrance state. Direct-call caller Before/After stays identical. Unknown entrance stays unknown inside/at virtual exit. The row is read-only for generated behavior; bindings remain editable at their defining cues.

New `S/NativeWardrobeProjection.cs` consumes prepared selections and attachment leases. Extend `NativePreviewLease`/`PreviewPropertyLedger` with an appearance group and dependency order; ordinary spatial deltas never run it. Add counters: AppearancePrepareCount, AttachmentBuildCount, AppearanceCommitCount, appearance cache hits, ColdPrepareMilliseconds. Preserve existing pose/spatial counters and latest-request coalescing.

State ownership/restoration table:

| Property | Preview snapshot/restore |
| --- | --- |
| body/attachment mesh, bones, rootBone, bounds | Capture original refs/values before first write; restore before mesh-relative weights. Compare array contents, not reference identity only. |
| blendshape weights | Capture/apply by stable name with mesh identity; never apply old numeric indices to a different mesh. |
| material arrays and renderer visibility | Existing ledger extended for new attachments; preserve original references. Instance materials lease-owned. |
| global/per-slot MPBs | Original renderer blocks are read-only. Appearance writes go to lease-owned renderer proxies, initialized from captured blocks. Original blocks are never cleared/replaced on save or disposal. See the fixed isolation decision below. |
| temporary attachments/local bones | Lease-owned HideAndDontSave; explicit registration/disposal. Never serialize or destroy author objects. |
| coverage/footwear/pose writers | Suppress conflicting writers under NativePreviewWriterGuard; use shared endpoint math, not ExecuteAlways initialization of runtime queue. Resume only after original presentation restored. |

**Fixed MPB isolation decision:** Unity MPB has no general public semantic equality/merge/removal API. Do not claim arbitrary external block edits can be merged, or restore a whole old block over them. NativeWardrobeProjection creates lease-owned SkinnedMeshRenderer proxies only for appearance surfaces that need replacement. They share the real actor's skeleton and placement; no cloned actor, skeleton, camera, story host or runtime graph. Hide original renderers via the existing visibility ledger while proxies show the same shared prepared fit endpoint. Original meshes/material arrays/MPBs remain untouched by the appearance adapter. New hair/garment instances are already lease-owned. All proxies are HideAndDontSave and registered before first write.

Initialize proxy blocks from the source renderer's current global/per-slot blocks, then apply wardrobe coverage/opacity only to proxy blocks using explicit shader properties: _WardrobeCoverageEnabled, _WardrobeDressCoverageEnabled, and WardrobeMaterialOpacity's selected _Alpha/_AlphaStrength/_BaseColor/_Color. Preserve non-alpha color channels from the effective source value. Resample source blocks when an intentional preview refresh occurs; the existing Preview button must force this cheap refresh even if the cue-state key is unchanged, without re-preparing fit/rebuilding attachments. Never copy blocks back. Source external block changes survive all exit paths even if their property IDs are unknown.

Pose sampling still targets actual scene paths/bones; copy required sampled blendshape values to the prepared proxy mesh by stable name. Copy authored renderingLayerMask, shadow/light-probe/sorting/bounds settings and other relevant renderer presentation flags so Lara's restricted visibility lights still affect exactly her surfaces. Route presentation visibility/material application through the effective renderer set so Present never re-enables hidden originals accidentally. Proxy-body anatomy/fit math uses prepared data and captured source morph values, not a runtime rebind of original saved components. Source renderers' authored enabled-state edits follow the existing conflict ledger and are preserved on stop. Saving removes/hides owned proxies and restores original visibility before serialization, then resumes only the latest projection. All reload/Play/Undo/prefab/scene exits follow this same disposal protocol.

This refines the plan's earlier proposal to write/restore original MPBs: isolation is the required implementation, and there is no generic original-block restoration path. It adds a renderer adapter to the native preview, not another wardrobe/state engine. Mesh/skin restoration ledger support remains for existing/new non-appearance preview writers; the appearance adapter itself keeps source mesh/bindings read-only.

## 8. Editor delivery and native actions

One window under `DazPose > Wardrobe > Appearance Delivery`, implemented in `E/WardrobeAppearanceDeliveryWindow.cs` with generation/build services under E. Diagnostic saved runners live under `A/DazPose/Validation/WardrobeProtectedCards/` and editor composition code under `A/DazPose/Validation/Editor/WardrobeProtectedCards/`. Runtime production code must not reference validation assemblies.

Fixed window controls:

- `Inspect inputs` — side-effect-free report of current catalog/config, candidate changes, outputs and protected refs.
- `Prepare corrected candidate` — clones current released inputs to owned diagnostics Candidates; dress=2/panties=1 only. No production reference advance.
- `Compile and publish candidate` — invokes existing compiler/publisher; new unique release outputs only, report previous/new generation and fits. No native auto-run after publication.
- `Generate reusable prefabs` — deterministic manifest/visual/rig assets for selected released catalog; no old asset overwrite.
- `Create Gate A scene`, `Create Gate B scene`, `Create Gate C scene` — exact compositions below; create once or refuse to overwrite authored diagnostic scenes. A button may be disabled until dependencies compile/are available.
- `Apply accepted appearance to First Contact` — enabled only after report has A–C PASS and explicit Art click; bounded transactional W09 production update, no automatic save of unrelated scenes/assets.

Saved scenes: `Assets/__WardrobeProtectedCards/Scenes/GateA_Appearance.unity`, `GateB_ProtectedCards.unity`, `GateC_Studio.unity`. Clone main FirstContact composition using shared scene utilities and copy diagnostic source/binding companions into that output root; disable/replace its auto-start runner in the diagnostic copy. Never mutate shared source companions/prefabs via SerializedObject or ApplyPrefabInstance. Keep production art as referenced art; any mutable diagnostic binding/material is a diagnostic copy. Do not regenerate the authored environment from defaults.

Diagnostic card files stay outside GlobalStoryCardIndexBuilder.PublishedRoot (currently Assets/Production/Story/Cards). To test genuine draws without polluting production membership, add `StoryLinkedContent.Create(CompiledStoryAsset root, IReadOnlyList<CompiledStoryAsset> cardSources)`; the existing Create(root) obtains GlobalStoryCardCatalog.Require().sources and delegates to the same validated factory. GateB runner passes its explicit saved diagnostic card source list and calls the existing StoryRunner.ExecuteAsync(linked, context, sequence). No modified Director selection policy or alternate executor. GateC uses local direct cards plus dynamic-call identity, so the ordinary Studio root/global capture path remains sufficient; do not add a user-facing diagnostic catalog selector.

Runners provide on-screen, explicitly named controls and report path; no testing by screenshots. Art starts Play/clicks controls at gates. No automatic hardware services/output or player builds; diagnostic scripts must not issue toy commands. Runtime integration tests use the production service/boundary, not a duplicate return implementation.
