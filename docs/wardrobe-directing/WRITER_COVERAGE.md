# Persistent presentation writer coverage — seed for W00/W06

Status: source-grounded ownership map, **not implementation evidence**. W00 records exact current methods/fields and W06 fills actual capture/restore APIs and check outputs. All implementation/native status starts NOT_STARTED/NOT_RUN.

| Domain | Existing owner/seam | Fixed snapshot/restore owner | Required checks | Status |
| --- | --- | --- | --- | --- |
| Base pose | SuccubusPerformer / PerformerBodyPose | PerformerPresentationSnapshot; 0.5-second target blend | RT01, RT07, RT13 | NOT_STARTED |
| Seated overlay/anchor | PerformerSeating / PerformerSeatingLayer / SeatedPresentation partial | Same performer record: separate overlay identity + seat/style/cached frames/ownership | RT07, RT14 | NOT_STARTED |
| Root locomotion/relocation | PerformerLocomotion / PerformerDissolve / StoryDissolveTo | Exact captured endpoint; finite barrier and one actor conceal/reveal | RT01, RT02, RT14 | NOT_STARTED |
| Stable visibility | PerformerDissolve / PerformerVisibilityState | Stable hidden state, no reveal for hidden entrance | RT14, RT17 | NOT_STARTED |
| Wardrobe/hair/fit | PerformerWardrobe / LaraWardrobe / FootwearPoseDriver | WardrobeSelection + prepared fit/asset pins; original transaction | AP01–AP12, RT08, RT14 | NOT_STARTED |
| Shader/material mode | PerformerMagicShaderPresentation | Exact profile identity and refresh against returned renderer set | RT07, RT14 | NOT_STARTED |
| Expression | PerformerExpressionLayer / SuccubusPerformer | Target/intensity, 0.5-second blend, no tuning reset | RT07, RT13 | NOT_STARTED |
| Gaze | PerformerGaze | Target mode/ref/fixed point; preserve gaze settings | RT07, RT13 | NOT_STARTED |
| Aura | SuccubusPerformer / Magic/PerformerAura | Persistent asset/target ownership | RT07, RT05 | NOT_STARTED |
| Grip / follow | HandGrip/PerformerHandGripController | Target, held position/follow mode/driver identity; release/reacquire owner | RT07, RT10, RT17 | NOT_STARTED |
| Finite speech/action/gesture/teleport | Existing performer sub-runtimes | Settle barrier or terminal cancellation; no ephemeral time replay | RT01–RT03, RT17 | NOT_STARTED |
| Player body | Assets/Player/Runtime/PlayerBodyPresence and actual pose owner if present | PlayerBodyPresentationSnapshot; original male body | RT07, RT14 | NOT_STARTED |
| Camera movement/orientation | Assets/Player/Runtime/PlayerView | PlayerViewPresentationSnapshot; exact quaternion or original track target/settings | RT07, RT15 | NOT_STARTED |
| Card UI/actions/signals | StoryPromptService / StorySignalHub / PerformanceStoryHost | Child token ownership; parent phase services retained | RT03–RT05, RT09 | NOT_STARTED |
| Motion driver | PerformanceRun / MotionDriver | Owner lifetime; no rewind/hardware replay; live parent compatibility | RT10, RT17 | NOT_STARTED |
| Presentation-derived posture mirror | PerformanceStoryHost writing Session.world.posture | Capture/reconcile this mirror only | RT18 | NOT_STARTED |
| Preview renderer/bone/blendshape writes | NativePreviewLease / sampler / NativeWardrobeProjection | Actual skeleton sampling + temporary effective renderer set | AU07–AU11 | NOT_STARTED |
| Preview coverage/MPBs/footwear writers | LaraWardrobe / FootwearPoseDriver / NativePreviewWriterGuard | Shared endpoint math on proxies, source MPBs read-only, source visibility ledger | AU08–AU11 | NOT_STARTED |

W00 must inspect all persistent facade commands and add any uncovered writer before W06 can close. Unsupported current persistent modes cannot simply be omitted to pass the table. Tuning, profile/world/user clothing decisions, RNG and live head input are excluded by product contract, not forgotten writer coverage.

## W00 baseline source map (2026-10-20)

This table records the current source seam and baseline gaps. **There is no full-presentation capture/restore API yet** at baseline; do not infer one from per-command cancellation handles. W06 owns that implementation. Stable readable values listed here are candidate snapshot inputs, not a claim that existing readers expose all of them.

| Coverage | Current writer/source methods | Stable values that must be captured | Baseline barrier/snapshot/restore/cleanup status | Check IDs |
| --- | --- | --- | --- | --- |
| Body and persistent/seated pose | `Runtime/Performer/Authoring/ScopedPerformer.cs`: `PoseAsync`, `PersistentSitPoseAsync`, `SitAtAsync`, `StandUpAsync`; delegated into `SuccubusPerformer` / `PerformerBodyPose` / `PerformerSeating` | pose asset identity; seated overlay identity; seat/style; local and cached seat frames; root transform/rotation | Finite command uses `PerformanceRun.Execute`; no immutable combined snapshot or restore method exists. Per-command cancellation is not return. W06 must expose value capture and use the shared pose/seating endpoints. | RT01, RT07, RT13, RT14 |
| Expression and gaze | `ScopedPerformer.ExpressionAsync`, `Expression`, `ClearExpression`, `LookAtAsync`, `LookAt`, `ClearGaze`; `SuccubusPerformer` facade; `PerformerExpressionLayer`, `PerformerGaze` | expression asset/intensity; gaze mode, target reference or fixed point, authored settings | Finite async commands have completion and cancellation hooks; persistent setters register ownership in `PerformanceRun`. Exact target/profile snapshot and restore absent. | RT07, RT13 |
| Locomotion, relocation, dissolve, visibility | `ScopedPerformer.WalkToAsync`, `TurnToAsync`, `StoryDissolveToAsync`, `DissolveToAsync`, `DissolveInAtAsync`, `DissolveInAsync`; `SuccubusPerformer` -> `PerformerLocomotion` / `PerformerDissolve` | world position and full rotation; stable visibility; active location/seat; exact presentation endpoint | Async operations are tracked, but no card-level settle barrier or combined return. Restore must run once with conceal/commit/reveal and hidden entrance preservation. | RT01, RT02, RT14, RT17 |
| Wardrobe, hair, footwear and coverage | `ScopedPerformer.OutfitAsync`, `SetHairAsync`, `TryAddLayerAsync`, `TryRemoveLayerAsync`; `SuccubusPerformer` delegates to `PerformerWardrobe`; `LaraWardrobe` and `FootwearPoseDriver` write renderer/pose state | generation and preset ID; remembered selection; ceiling/mask; hair key; fit/coverage/footwear IDs and renderer endpoint | `PerformerWardrobe` has transaction/rollback for a single change; no cross-command card snapshot exists. Preview writer guard and native proxy isolation are also absent. | AP01–AP12, RT08, RT14, AU08–AU11 |
| Shader/material mode and aura | `ScopedPerformer.MagicShaderAsync`, `Aura`, `ClearAura`; `PerformerMagicShaderPresentation`, `PerformerAura` | exact profile identity; aura asset, target and ownership | Shader exposes `CaptureMagicShaderRestoration` for scoped cancellation but no exact-profile full snapshot; aura has cancellation ownership only. W06 must preserve selected profile/target and restore against returned renderer set. | RT05, RT07, RT14 |
| Grip, follow and motion ownership | `ScopedPerformer.GripAsync`; `ScopedToys.Follow`; `PerformanceRun` owner registration; `PerformerHandGripController` | grip source identity/validity, position, follow driver identity and whether parent-owned | scoped cancellation/ownership exists; no entrance snapshot/adoption contract. Return may reattach only to the same valid live source; no replay. | RT03–RT05, RT10, RT17 |
| Player body | `PlayerBodyPresence.StartHiddenAt`, `DissolveInAsync`, `DissolveOutAsync`; `ScopedPlayer` | root position/rotation; stable visibility and prepared male-body source | `PlayerBodyPresence` reports stable visibility but has no position/presentation snapshot or restore. | RT07, RT14 |
| Camera | `ScopedPlayer.Track`, `StopTracking`; `PlayerView.MoveToAsync`, `LookAtAsync`, `Track`, `StopTracking` | world position; full quaternion; target or fixed point; tracking settings and operation mode | `PlayerView` exposes current position/rotation/target and per-channel cancellation revisions, but no complete immutable snapshot/restore API. | RT07, RT15 |
| Speech/action/UI/signals and motion driver | `ScopedPerformer.SayAsync`, action/gesture methods; `ScopedUI`; `StoryPromptService`; `StorySignalHub`; `PerformanceRun` | child-owned action/signal tokens; parent phase services and driver identity | runtime owners exist; full card child-scope boundary is W06/W07 work. Finite operations require settle or terminal cancellation and must not replay time/hardware. | RT02–RT06, RT09, RT10, RT17 |
| Story posture mirror | `PerformanceStoryHost` presentation callbacks writing `Session.world.posture` | presentation-derived posture only | capture/reconcile this mirror; do not snapshot or rewind arbitrary world/session choices. No card return hook at baseline. | RT18 |
| Native preview renderer/bones/blendshapes/material blocks | `NativePreviewLease`; preview sampler; `LaraWardrobe`; `FootwearPoseDriver`; `NativePreviewWriterGuard` | owned proxy and attachment set, sampled body/pose values by stable blendshape name, effective visible renderer set | current lease tracks scene-preview mutations; appearance proxy projection and source-MPB read-only guarantee are not implemented. W08 owns this. | AU07–AU11 |

No source-level finite global barrier, immutable entrance snapshot, card return or sequence/card kind dispatch was found at this baseline. These are explicit W04–W08 deliverables, not preexisting capabilities.


## W06 source checkpoint - October 20

Typed immutable records and capture seams now exist in PerformancePresentationSnapshot.cs, SuccubusPerformer.PresentationSnapshot.cs and the PlayerView/PlayerBodyPresence snapshot partials. PerformancePresentationService validates captured references/release/fit and compares domains; it does not yet execute restoration. PerformanceRun scopes capture operation owners, bound active collections, retain root services and isolate UI/motion ownership. Four pure tests cover scope cleanup/adoption/cancellation decisions. All baseline table return/animation/native acceptance rows remain incomplete until W06/W07 finish; no full-state gate is claimed from this checkpoint.


## W06/W07 implementation closure - October 20

The baseline rows above are historical findings. Runtime return seams now exist:

| Writers | Capture / restore owner | Evidence |
| --- | --- | --- |
| Base pose, seat/overlay/anchor, arrival hold | SuccubusPerformer.PresentationSnapshot, PerformerSeating/Layer, PerformerLocomotion and BodySourceMixer | Typed controls; existing native endpoints; .5 second blends |
| Actor placement/visibility and wardrobe/fit | PerformancePresentationService.Restore, PerformerWardrobe snapshot preparation/recall | Conceal once, exact frame/selection, reveal once; hidden stays hidden |
| Expression/gaze/material/aura | Native performer sub-runtimes and exact profile/target records | Changed control replacement without default snap; unchanged ownership retained |
| Grip/follow and driver | Native grip restore, scoped writer adoption, parent driver checks | Live source/generation retained; no rewind/output replay |
| Original male body and camera | PlayerBodyPresence/PlayerView snapshot partials | Full quaternion, fixed or original track rule; body local frame handled under player root |
| UI/actions/signals and finite operations | PerformanceRun scope; PerformanceStoryHost; StoryMachine card boundary | Child cancellation; parent services retained; finite settlement before return |
| Posture mirror | CardPresentationScope callback | Presentation-derived mirror only |

Managed scope/interpreter checks pass. Native endpoint behavior remains pending Gate B. Studio appearance proxies/summary integration remain W08 and are not complete.


## Production Studio wardrobe delivery

Earlier W08-incomplete statements are historical. NativeWardrobeProjection owns transient effective body/attachment renderers, shared-bone binding, stable-name morph transfer and proxy-only MPB/coverage/opacity writes. NativePreviewLease hides surfaces before serialization and disposes them on exit. NativePreviewWriterGuard suppresses original anatomy/wardrobe/footwear writers; shared LaraHeelReview endpoint math supplies static footwear support. Original visibility and real pose-bone writes remain ledger-owned. Mark drags reuse prepared appearance. Static authoring includes outfit/hair/layers and protected-card identity summaries. Source/managed checks pass; actual new native Studio/save behavior remains unobserved. Art superseded disposable Gate C with actual Studio use.
