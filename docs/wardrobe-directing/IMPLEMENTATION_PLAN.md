# Reusable appearance and protected cards — implementation plan

Date: October 19, 2026. Source inspected at `110e2b4` on `main`.

Status: design plan with a prepared [execution packet](../../newtickets/DazPoseWizard_Wardrobe_Protected_Cards_Execution_Packet/START_HERE.md). Implementation has not started. The packet fixes API ownership, ticket boundaries, check IDs and Art's native gate handoffs. This document does not claim implementation or native acceptance.

## 1. Product contract

Art's final distinction governs the entire implementation:

- **Sequences can leave lasting presentation changes.** They orchestrate the scene and may call sequences or cards.
- **Every card returns Lara, the player body, and the camera to its entrance presentation state before it completes.** This applies to direct `play`, `Director.Execute`, and `Director.DrawAndExecute`, including nested cards. There is no special-card exemption or per-card opt-out.
- A sequence called inside a card may change presentation during that card; the enclosing card still restores its own entrance state.
- Persistent poses return with **0.5-second animation blends**. Changed locations return using the existing dissolve relocation mechanism. Camera return uses a smooth view transition; it does not invent a camera aim target or add a camera fade effect.
- State restoration includes control ownership and persistent modes, not just transform values.
- Story progress, mood, choices, RNG, profile edits, real user's clothing records, and equipment decisions remain story state. Returning a card restores the presentation of the three scene elements; it does not rewind gameplay or reverse a user's real actions.
- Explicit session Stop cancels immediately, including a return already in progress. It must never start or resume a return animation, toy output, or another card after Stop.

First Contact's `Intro` becomes `sequence Intro`. It therefore leaves Lara visible, posed, and in the intended location for the following scene. A directly inserted ordinary card provides exactly the same protection as a randomly drawn one.

Studio remains a responsive editor for bindings, poses, appearance, and transforms. It uses static endpoints and reports ambiguity; it does not evaluate story variables, choices, or random draws.

## 2. What exists today

All paths below are relative to the repository. `U` in the tables means `validation/DazPoseUnityValidation/Assets/`.

| Area | Existing code / asset | Verified behavior and limitation |
| --- | --- | --- |
| Named outfits | `U/DazPose/Runtime/Performer/Wardrobe/WardrobePreset.cs`, `WardrobeCatalog.cs` | Named presets, stable IDs, aliases, released generation validation, parts, layers, hair policy, footwear and fit states exist. Reuse these. |
| Runtime switching | `U/DazPose/Runtime/Performer/Wardrobe/PerformerWardrobe.cs` | Queued outfit, hair, add-layer, remove-layer and exact scene-recall operations already exist, with transaction rollback. Initialization is Play-only. |
| Renderer inventory | `PerformerWardrobe.BuildPieceInventory` | Creates renderers for every piece of every catalog preset, binds canonical/local bones, and keys hair as `presetId/pieceId`. It does not instantiate reusable appearance prefabs. |
| Body fitting | `WardrobeFitState.cs`, `WardrobeConfiguration.cs`, `PerformerWardrobe` transaction | Clothing changes may replace the body mesh, preserve morphs, rebind anatomy, dissolve/surface data, footwear and visibility. Merely toggling clothing renderers is insufficient. |
| Coverage / appearance | `U/DazPose/Runtime/Performer/LaraWardrobe.cs` | Applies coverage, visibility, material opacity, bounds and related presentation; contains an Edit Mode writer that the native preview guard currently suppresses. |
| Scoped C# API | `U/DazPose/Runtime/Performer/Authoring/ScopedPerformer.cs` | `OutfitAsync`, `SetHairAsync`, `TryRemoveLayerAsync`, `TryAddLayerAsync` already pass through `PerformanceRun` ownership/cancellation. |
| Wardrobe snapshots | `WardrobeState.cs`, `SceneWardrobeBinding.cs` | Exact preset, remembered layer ceiling and resolved hair are represented. `RecallAllAsync` validates/hides/commits/reveals multiple actors. It is not a full scene-presentation snapshot or an automatic scene startup hook. |
| Unused prefab field | `WardrobePreset.HairPrefab` | Serialized field exists, generation clears it, runtime does not consume it. Its existence is not implemented prefab support. |
| Production performer | `U/Production/Performers/Lara.prefab` | Already carries `PerformerWardrobe`, production catalog/configuration and the real body/rig. Preserve that rig and component identity. |
| Production catalog | `U/Production/Wardrobe/LaraCatalog.asset` | Currently points to released generation `70b0e228678e8bcd56e967d03d5bef45f63861ce4dabe6dd261afb48e14bbceb`, including three outfits. Candidate `U/Wardrobe/WardrobeCatalog.asset` is not the runtime release. |
| Outfit 1 | That release's `Presets/first-outfit/Preset.asset` | `first-outfit`: Emiko hair `EmikoHair_59875`, Peekaboo dress, Charlene's Closet panties. No aliases. Hair action Set; no hair prefab reference. |
| Layer ordering issue | Same released preset | Panties currently layer 2, dress layer 1. `RemoveHighestCeiling` removes panties first. Correct via candidate/release workflow; never rewrite the immutable released asset. |
| DSL | `U/DazPose/Runtime/Story/Language/CompiledStoryProgram.cs` | `StoryVocabulary` and `StoryBindingKind` have no wardrobe/hair commands or typed content bindings. |
| Source-owned bindings | `StoryContentBindings.cs`, `Editor/StoryStudio/SourceBindingReconciler.cs` | Typed asset rows, source GUID ownership and non-destructive reconciliation exist. Extend these rather than add a parallel binding store. |
| Card boundary | `Language/StoryMachine.cs`, `ExecuteCard` | Direct play and Director calls share one method. It currently executes the body and restores only `CardContext`; no visual return occurs. Director progress is credited after `ExecuteCard` returns, a useful existing boundary. |
| Sequence calls | `StoryParser.cs`, `DazStoryCompiler.cs`, `StoryProgramLinker.cs` | Sequence declarations exist, but `play` currently resolves only cards. Nested sequence invocation needs implementation, not just renaming Intro. |
| Current snapshot name | `Runtime/StoryRuntimeSnapshot.cs` | A debug/session observation model; it does not capture actor/camera presentation. Do not expand it into a Unity scene serializer. |
| Performer state | `SuccubusPerformer.cs`, `PerformerSeating.cs`, `SuccubusPerformer.SeatedPresentation.cs` | Desired/settled poses, expression, seat/style, visibility and several cancellation APIs exist. Complete capture/restore APIs, including persistent seated-pose identity, do not. |
| View state | `U/Player/Runtime/PlayerView.cs` | Exposes view rotation, position, tracking target and movement/orientation cancellation. Full orientation/tracking snapshot and quaternion return interpolation need explicit APIs. |
| Player body | `U/Player/Runtime/PlayerBodyPresence.cs` | Owns the original prepared male body's visibility and dissolve. It is separate from camera view control. |
| Studio state | `Editor/StoryStudio/AuthoringVisualState.cs`, `AuthoringCueIndex.cs` | Eight compact visual fields and bounded static summaries; all Director calls can currently introduce visual ambiguity. No appearance fields. |
| Native preview | `NativeSceneStage.Authoring.cs`, `NativePreviewLease.cs` | Retained pose sampling and separate spatial/presentation deltas. Ledger does not yet own wardrobe mesh swaps, attachment lifetime, or wardrobe MPB writes. |
| First Contact | `U/Production/Story/Programs/FirstContact.dazstory` | Hidden actors/camera/idle setup, `card Intro`, `play Intro`; no initial wardrobe commands. Preserve all dialogue and existing source/binding GUIDs. |

## 3. Prefab and asset model

### 3.1 What the reusable system prefab means

Yes: a reusable wardrobe rig references the available outfits and hair, and Lara accepts commands such as `Lara.SetOutfit(FrenchMaid)` and `Lara.Outfit.RemoveOneLayer()`.

The command receiver remains Lara's existing `SuccubusPerformer` / `PerformerWardrobe`. The prefab supplies reusable attachment structure and references. It must not introduce a second wardrobe controller or a second Lara skeleton.

Proposed structure:

```text
Lara.prefab — existing performer, skeleton, body, wardrobe controller
  WardrobeRig.prefab — reusable attachment roots / generated manifest
    ClothingRoot
    HairRoot

WardrobeRig manifest
  released preset reference -> clothing visual prefab
  stable hair key          -> hair visual prefab
  source catalog generation / compatible character signature

WardrobeCatalog -> WardrobePreset -> parts, layers, fit states, footwear
```

The manifest can reference every available outfit without keeping every outfit's renderer hierarchy live. Prepare the current/target attachments and retain a small bounded cache. The first-use cost is measured separately from switching already prepared assets. No asset search or prefab generation occurs during a cue selection or handle drag.

### 3.2 Separate reusable visual assets

- One hair prefab per style, including all its mesh parts and necessary local accessory bones. Outfit 1 produces an Emiko hair prefab.
- One clothing visual prefab per released outfit, containing its garment parts and local accessory bones, excluding hair. Outfit 1 produces a dress/panties prefab.
- Both use a shared attachment descriptor/binder extracted from the existing inventory construction. Descriptors contain stable piece IDs, role, canonical bone references by validated paths/names, local bone hierarchy, mesh/material references and compatibility metadata.
- Bind to the owning Lara skeleton. Local garment/hair bones stay scoped to their own attachment; duplicate local names in different attachments must not collide. Never clone a complete character or parent-bind everything to the head.
- References to original released meshes/materials remain references; do not duplicate or retune them merely to create prefabs.
- The catalog/presets remain authoritative for outfit identity, layer order, fitting and release state. The generated rig manifest is a presentation mapping, not a second editable wardrobe catalog.
- Keep `PerformerWardrobe` on its existing GameObject: existing `GetComponent` callers, queues and scoped APIs rely on that arrangement. The nested rig component receives its owner/body explicitly.
- Keep existing hair keys working. A clothing switch must not require that its preset owns the currently selected hair. New authoring uses a typed hair prefab reference whose metadata resolves the stable key; authors never type `first-outfit/EmikoHair_59875`.
- Do not revive `WardrobePreset.HairPrefab` as a competing lookup path. Retain it for serialized compatibility until a documented migration can remove it; the rig mapping supplies prefab references without editing old releases.

### 3.3 Independent clothing and hair semantics

New script `SetOutfit` changes clothing and fully dresses all populated layers; it **keeps the current hair**. `SetHair` changes hair independently; `ClearHair` explicitly clears it. Applying an outfit's suggested hair in an authoring action emits/assigns both choices explicitly.

Retain existing C# `OutfitAsync` compatibility, including its legacy preset hair policy. Add a request policy/overload in the same transaction path for clothing-only selection, used by new scoped `SetOutfitAsync`; do not duplicate transaction logic. Document the difference and migrate production story authoring to the independent commands.

Audit the current `AlreadyEquipped` short circuit before resolving final appearance: repeating a fully dressed outfit must not incorrectly bypass a requested hair change in legacy combined calls. No-change detection compares the complete resolved target relevant to the requested operation.

### 3.4 Fitting and release rules

Reuse `WardrobeCandidateSnapshot`, `WardrobeFitCompiler`, `WardrobeGenerationPublisher`, all validated body/surface/dissolve/footwear assets and rollback behavior. Extract shared preparation/commit code only as needed for the prefab binder and editor endpoint adapter.

Correct Outfit 1's removal order to **dress first, panties second** in a candidate, using outer layer 2 and inner layer 1. This changes the meaning of its intermediate visible mask. A fit for “dress only” cannot be relabelled as “panties only,” even if the numeric mask happens to match. Compile/validate the affected combinations through the existing fitting workflow before advancing the catalog/configuration together. Preserve the previous generation as recovery.

This is a focused release correction, not a reimport campaign or permission to retune other outfits, shoes, toes, body shape or art. If an affected fit cannot be generated from available data, report the exact missing combination at the native gate; never silently swap a naked fit or omit a layer.

## 4. Language and binding contract

Proposed supported spelling follows the existing case-sensitive DSL:

```dazstory
setup FirstContact
{
    Lara.StartHiddenAt(lara_start)
    PlayerBody.StartHiddenAt(player_start)
    Camera.StartAt(camera_start)
    Lara.StartIdle()
    await Lara.SetOutfit(OpeningOutfit)
    await Lara.SetHair(OpeningHair)
}

sequence Intro
{
    // Existing Intro choreography and dialogue, unchanged.
}

sequence FirstContact
{
    play Intro
    // Existing orchestration continues.
    // Directly played cards receive the same protection as drawn cards:
    // play SomeProtectedCard
}
```

`OpeningOutfit` and `OpeningHair` are typed source-owned binding slots in `FirstContact.bindings.asset`. Initially assign the released first-outfit preset and Emiko hair prefab. Replacing those object references changes the opening look without editing source names. `FrenchMaid` is another author-chosen binding alias, not a requirement to parse an asset display name or introduce a French maid asset that does not exist.

| New vocabulary | Meaning / existing implementation to reuse |
| --- | --- |
| `await Lara.SetOutfit(OpeningOutfit)` | Fully dress the selected named outfit, retain hair; same wardrobe request queue/fitting transaction. |
| `await Lara.SetHair(OpeningHair)` | Bind the typed hair asset to the existing hair operation. |
| `await Lara.ClearHair()` | Explicit hair clear. |
| `await Lara.Outfit.RemoveOneLayer()` | Existing highest-visible populated layer removal. Skip empty layers. |
| `await Lara.Outfit.AddOneLayer()` | Existing lowest-hidden populated layer addition. Skip empty layers. |

All five are awaited finite operations. Benign extremes are successful no-ops: already naked, fully dressed, or no remembered outfit. Preserve the remembered preset when removing its last garment so AddOneLayer can restore it. Hair is outside all clothing layers. New story commands use smooth wardrobe changes when visible, with a 0.25-second conceal and 0.25-second reveal default; hidden startup applies its prepared endpoint without an unnecessary reveal.

Implementation seams:

1. Append `WardrobePreset` and `Hair` binding enum values; preserve existing serialized numeric values. Extend vocabulary, command descriptions, compiler argument/type validation and source manifests.
2. Add typed outfit and hair rows to `StoryContentBindings`, its `Symbols`, accessors and diagnostics. A hair row references a prefab with the required hair attachment descriptor; reject scene objects and incompatible assets.
3. Extend `SourceBindingReconciler` to create missing rows, preserve assignments, reject type collisions and maintain source GUID ownership. Extend `StoryContentBindingsEditor`, Studio binding assignment and importer dependency tracking.
4. `PerformanceStoryHost` resolves content by the actual `Source.unit`, including published cards, and calls scoped performer methods. No string-name guessing or root-content fallback for another source's binding.
5. Validate declared wardrobe/hair dependencies through the existing readiness and linked-content diagnostics. Update `StoryLinkedContent.ResourceIssue` so an unusable outfit/hair binding can exclude a drawn candidate with a useful reason. Derive the declarations from source requirements; avoid a second manually maintained list on the scene host.
6. Extend resource ownership in `StoryVerb` / `StoryMachine.Start`: wardrobe can affect body fitting, seating/pose offsets, dissolve and materials. Represent all required resources rather than assigning it a cosmetic single channel. Conflicting body/material/wardrobe work must be awaited; ordinary camera tracking may continue.

### 4.1 `play` calls either declaration kind

Keep `play Name` as the single awaited invocation syntax. Resolve the declaration and dispatch by its kind:

- Local sequence: execute as a sequence, leaving its final presentation in place.
- Local or published card: execute inside the mandatory card return boundary.
- Ambiguous/missing names: source diagnostic; never silently prefer a card over a sequence.
- `Director.Execute` and random draws resolve cards only. Sequences cannot enter the Director pool.
- Published global files remain card-only. Helper sequences are local to their story source for this scope; no new global sequence registry.
- Root `setup FirstContact` runs once on root startup as today. Calling a helper sequence does not implicitly rerun setup blocks. Document this and diagnose any unsupported helper-setup expectation.
- A helper sequence does not create a new `CardContext`. Inside a card it inherits the active context; outside a card, card-only values remain invalid.

Update parser/export where necessary, compiler validation, the linker resolver, `StoryProgramReachability`, `StoryPlaybackWindow`, runtime dispatch and Studio call analysis together. The current `StoryCode.Play` may remain if it becomes explicitly declaration-kind aware. Bump compiled/importer schema or version so Unity reimports old programs into the new contract; stale serialized programs must not silently execute old semantics.

Apply the existing recursion bound to all playable entry calls, including sequences. Report call cycles/depth at source. Do not add an independent sequence interpreter.

## 5. Complete card presentation state

### 5.1 Snapshot meaning

Capture a compact immutable **runtime presentation snapshot** at each card entrance. It describes authored controls and resolved asset identities, not a hierarchy dump, every bone, a frozen rendered frame, or the entire game session.

Add proposed `PerformancePresentationSnapshot` and subsystem snapshot records under the runtime performer/player namespaces. Add an optional domain interface to the pure story host, e.g. `IStoryCardPresentationHost`; the production host must implement it. Managed story test hosts implement a recording model so the scope protocol can be checked without Unity. Do not claim protection for a production host that does not implement the boundary.

| Owner | Capture / restore requirements |
| --- | --- |
| Lara root | Exact entrance world position/rotation and relevant parent/local relationship. Preserve authored scale; do not use restoration to reset artist tuning. |
| Persistent pose | Base pose identity, settled target, transition configuration needed for the return, and seated overlay identity separately. Return targets use a 0.5-second blend. Live idle/breath/blink time continues; no graph rebuild or rewind of physiological animation. |
| Seating | Standing vs seated, seat reference, style, cached seat/contact frames, persistent seated-pose mode and anchoring ownership. Restore the owning mode so LateUpdate does not pull Lara back to the outgoing seat afterward. |
| Visibility | Stable visible/hidden state, independent of wardrobe. Enter only after finite visibility transitions settle. A hidden entrance must finish hidden even if the card reveals Lara. |
| Wardrobe | Released preset reference/ID, generation/revision, remembered outfit, layer ceiling, exact resolved hair/clear state, fit identity and footwear state. Resolve Keep at capture; never save a relative Keep instruction as the return target. |
| Persistent presentation | Exact material/shader profile, persistent expression/intensity, gaze target mode/reference or fixed target, and any supported aura/grip/procedural-follow mode that the production facade can leave active. Capture inside the owning subsystem, not by reflection. |
| Player body | Original male body root placement/rotation, stable visibility and supported body pose/control state. If the body has no current pose API, preserve that authored baseline and cover every new body-pose writer before exposing it to scripts. Preserve measured contact rig/profile/surface data. |
| Camera | Player/view root position and rotation, ViewRig local/world relationship, authored camera offsets/projection fields that scripts can change, and orientation mode. For tracking, capture the actual target and active settings; for fixed view, restore the exact quaternion. |
| Ownership | Finite card work, persistent card writers, temporary UI actions, subscriptions and procedural/motion consumers receive a scope identity so they can be settled or released without stopping the whole session. |

Camera tracking returns to the captured tracking rule and target; its correct rotation is evaluated against the restored Lara pose/position. A fixed camera returns to its captured rotation. Never substitute SeatFocus, head, Lara root or another target. Live user head tracking/input is not recorded and replayed; restore the authored view rig around it.

Inventory every persistent writer reachable through the current production facade, not only today's story vocabulary. Where the subsystem lacks readable control state, add a typed capture/restore API there. A new persistent visual command must declare its snapshot/restore coverage before being allowed in a protected card. Unsupported active modes fail at entry with a precise diagnostic before the body runs; they cannot be silently discarded and labelled full-state support. The final acceptance target covers the current production modes, including grip release/reacquisition and material/wardrobe interactions.

Do not capture or restore global tuning settings as card state. Actor appearance/pose targets are state; user-edited response speeds, anatomical calibration, shoe/toe artistic tuning, application settings and asset definitions are configuration.

### 5.2 Entrance barrier and ownership

Before capture, await finite presentation commands from the calling scope that affect these actors. For example, an unawaited persistent pose must reach its stable entrance target. Do not snapshot halfway through a dissolve or rebuild the intended entrance by replaying source. Stable ongoing tracking/idle is allowed.

The barrier waits only relevant finite operations, excluding the current invocation and all ancestor Director/play tasks. Waiting for the whole machine's `children` collection here would wait for the card itself and deadlock. Track affected operations by scope and resource explicitly.

Use a child presentation scope inside `PerformanceRun` and the host for each card. Associate machine-owned child tasks, cleanup registrations and persistent ownership handles with their scope. Existing session ownership remains the outermost scope. Nested cards use a stack; child cleanup cannot remove a parent's tracking, motion subscription or persistent pose ownership.

Returning through a subsystem can create a new command generation. Transfer the restored persistent ownership back to the parent scope and replace its stale cleanup handle accordingly. A parent's eventual Stop must still cancel that restored tracking/pose/grip; an old child cleanup must not cancel it afterward. Scope stacks are run-local, never global static state.

Persistent background sources need explicit ownership treatment. Stop card-owned loops/consumers before returning; do not rewind hardware output. If a parent-owned active motion mode cannot be safely retained while the card runs, report that conflict before entry rather than silently stopping/restarting it. Implement the current supported follow/grip modes without introducing an alternate motion engine.

### 5.3 Normal card completion protocol

All card entry paths call one boundary in `StoryMachine.ExecuteCard`:

1. Validate resources, reach the entrance barrier, capture snapshot, open a child scope, publish card context.
2. Execute the existing body through `Entry`, including nested sequence/card calls.
3. Await accepted finite body tasks. Stop/release only this card's persistent temporary writers and looping consumers. Remove its UI actions/subscriptions so they cannot fire during return or the next card.
4. Prepare the complete return target before changing presentation: asset/fit availability, valid seat/targets, subsystem ownership. Diff current vs entrance state; unchanged domains do nothing.
5. Restore through the owning subsystems using the algorithm below. Await all return work and verify the committed target state/control modes.
6. Close the child scope, restore parent `CardContext`, then publish card completion. Only then may Director history/progress advance or the next statement execute.

`Director.Complete` already follows `ExecuteCard`; preserve that ordering. Direct `play` does not gain Director progress/history credit as a side effect. Early normal return and fall-through use the same restoration path. There is no author command to suppress the return.

### 5.4 Return choreography

- **No location/structural change:** blend changed persistent pose/expression/gaze targets over 0.5 seconds. Avoid toggling visibility, rebuilding graphs, swapping wardrobes or restarting tracking when unchanged.
- **Changed Lara location / seat ownership:** use the established dissolve presentation: conceal, restore the captured root/seat frames and seating ownership while hidden, apply the correct target pose/fit, and reveal only if the entrance was visible. Use the existing dissolve profile's configured relocation duration; pose returns still use the 0.5-second target blend. Do not force walking back or use a transform snap while visible.
- **Changed wardrobe/hair:** reuse exact wardrobe recall/prepare/commit/rollback. If location also changes, coordinate one conceal/hidden commit/reveal instead of stacking two dissolves. If only appearance changes, use the smooth wardrobe default. Hidden entrance stays hidden.
- **Changed player body location:** extend its existing dissolve presenter to support an exact relocation endpoint; preserve its original meshes/contact rig. Coordinate visibility and pose restoration without replacing it with Lara.
- **Changed camera:** add an orientation endpoint that can interpolate to a quaternion over 0.5 seconds, alongside existing position interpolation. Restore captured tracking with its actual settings and a smooth reacquire after actor endpoint establishment. Sequence ordering must prevent one-frame wrong aim or competing tracking writers. Do not use LookAt to approximate a captured fixed rotation.
- **Materials:** preserve the exact entrance shader profile across any wardrobe refresh, not just Regular/Wireframe mode. `CaptureRestoration` currently restores Regular; that cancellation closure is not a state snapshot.
- **Seat/footwear coupling:** restore fit/footwear before final pose/contact anchoring and view tracking evaluation. Verify the seat and hip contact after both settle.

Return may span more than 0.5 seconds when a location dissolve is required. The fixed half second applies to animation blends, not an instruction to truncate existing dissolve effects. Settle exact transform/asset targets at the end within documented numerical tolerances; tracking is checked by mode/target/settings and the same runtime aiming rule, not equality to a stale entrance image.

Add a typed relocation endpoint overload for captured positions/quaternions and seat frames. Current `RelocatePresentationAsync` accepts a live mark and normalizes standing rotation to yaw; it cannot reproduce every captured entrance rotation. Reuse its dissolve machinery, but preserve the captured full rotation for restoration and do not generate temporary hierarchy marks to make the old API fit. If the entrance parent/seat moved or was destroyed during the card, validate the cached endpoint and ownership explicitly; do not silently recapture a different entrance.

Audit derived state mirrors as part of commit verification. The current story host writes `Session.world.posture` from Lara's pose/seating commands, so a return must not leave that mirror describing the outgoing card pose. Keep these presentation-derived observations consistent while preserving unrelated gameplay/user clothing and equipment decisions.

### 5.5 Stop, faults and missing targets

Stop is terminal and immediately cancels body/return work and all local/session outputs. A `finally` block must not use an uncancelled token to animate after Stop.

If a body or restore fails, no normal card completion, progress credit or next card is allowed. Perform bounded ownership/resource cleanup, surface the originating source plus failing domain, and terminate the run under existing fault handling. Do not continue from a partially restored state. If an entrance target was destroyed, report it; never pick a replacement camera or seat. Ensure nested snapshots/scopes are disposed on capture failure, body failure, return failure, Stop and scene unload.

## 6. Studio integration

This is feasible, but more than adding a dropdown. The wardrobe changes body meshes, fit and coverage, so native preview ownership must be extended first.

Execution refinement: the packet's API_AND_OWNERSHIP fixes MPB safety by using lease-owned appearance renderer proxies bound to the actual scene skeleton. The appearance adapter keeps original renderer meshes/material arrays/MPBs read-only and restores only its source-visibility lease. The write/restore list below describes required coverage for any other existing/native writers; it does not authorize generic original-MPB restoration. See the packet for sampling, ownership and cleanup order.

### 6.1 Static state and call semantics

Extend `AuthoringVisualState` with outfit, layer ceiling and independent hair state. Use a small typed appearance state/reducer shared with runtime preparation where practical. Empty layers and remembered outfit semantics use `WardrobeLayerState` exactly.

Replace hard-coded eight-field loops, `255` masks and fixed 128-worklist assumptions with field-count/all-fields constants and a bound derived from the actual finite lattice. Keep recipes compact; do not grow a command history or recurse through wardrobe deltas on selection.

Update `AuthoringCueIndex`:

- Every card call has identity output for protected presentation fields: caller After equals caller Before. This applies to direct plays, explicit Director execution and random draws.
- A sequence call composes the sequence's static output into the caller; Intro's final state therefore persists.
- Still analyze card bodies to display/edit their internal cues. Feed explicit incoming contexts into them and join conflicting contexts. Cards drawn from multiple/unknown entrances may have unknown fields inside; do not invent a canonical entrance.
- A choice/loop containing only card invocations does not make following protected presentation state ambiguous. Direct persistent sequence writes inside skipped logic still do, field by field.
- No candidate-card execution, random selection, story-state evaluation or source replay is needed to determine a Director call's protected output. Optional candidate browsing uses captured metadata only.
- Recursive/deep sequence calls remain bounded and produce a useful ambiguity diagnostic.

Studio exposes card return as a clearly labelled virtual cue, **Return to card entrance**, with the static endpoint and a short rule description. It cannot execute a runtime return animation. This gives authors an explicit place to inspect the guaranteed exit. Preserve source cue identity; virtual rows need stable IDs independent of row numbering.

### 6.2 Native appearance projection

Extract pure selection/preparation from `PerformerWardrobe` and a shared attachment binder. Runtime uses them through its queue; Studio uses an Edit Mode endpoint adapter under `NativePreviewLease`. Do not turn on the full Play-only controller in Edit Mode or create another wardrobe switching engine.

Extend the preview ledger/resource owner to cover:

- Body sharedMesh, bones/rootBone where changed, bounds and mesh-relative blendshape values by stable name.
- Original material arrays, renderer visibility and both global/per-slot property blocks when wardrobe coverage writes them.
- Temporary attachment hierarchy, local bones, instance materials, fit/footwear presentation and cleanup order.
- Existing authored component data that projection may temporarily affect; preferably compute endpoints without mutating saved configuration.

Restoration order matters: detach/stop preview writers, restore original mesh/bindings, restore blendshapes against that mesh, restore materials/MPBs/visibility, remove preview-owned instances, then resume normal writers. Extend the current guard to prevent competing footwear/coverage writers. Never destroy author-owned objects or restore over an external authored change; preserve the change and stop preview with a clear message.

Save, scene close, Undo/Redo, prefab apply, domain reload, Play entry and Stop Preview all restore temporary appearance before serialization and dispose resources. After save, reapply only the newest queued projection. Preview creates no saved wardrobe override or new production asset merely by selecting a cue.

### 6.3 Adaptive interface and performance

Keep the existing cue sidebar and context inspector. Add typed outfit/hair drag targets at their cues and inherited appearance rows where useful. Layer cues show current outfit, visible layer names, before/after results and a benign no-op explanation at extremes. Missing slots use the existing Create missing asset slots workflow. No new top-level bindings tab or permanent hair button.

Separate appearance revision/cache keys from pose and spatial keys. A mark drag must not recreate attachments, reevaluate fit, swap body mesh or resample pose unnecessarily. Adjacent cues with unchanged appearance reuse prepared renderers. Wardrobe change invalidates only the dependent pose/seat/material/focus calculations once; it must not reset roots to saved baseline between projections.

Measure source indexing, cue lookup, warm projection, cold asset preparation and mark updates separately. Targets: cue lookup under 10 ms; warm unchanged-appearance cue projection and spatial update under 50 ms on the current project, with no growing cost by cue number; report actual native measurements. These are acceptance targets, not claims from source inspection. Cold preparation must be bounded, visibly identified if perceptible, and reused. No promise of asynchronous Unity object mutation: Unity object work stays on the main thread.

## 7. Ordered implementation tickets

| Ticket | Deliverable / principal files | Exit evidence |
| --- | --- | --- |
| W00 — Baseline and contract | Capture current assets, source GUIDs, production/fixture distinction, fit generations and authored layer order. Create explicit writer/snapshot coverage matrix. Record this final card/sequence rule. | Scoped change manifest and recovery references; no asset edits or Unity operation. |
| W01 — Shared appearance preparation | Extract resolved selection and attachment binding from `PerformerWardrobe`; retain queue, rollback, fitting and C# compatibility. Add independent SetOutfit policy and complete no-change comparison. | Pure decision/transaction model coverage; source compiles without a new controller path. |
| W02 — Hair and clothing prefabs | Shared descriptors/binder, reusable WardrobeRig with separate roots, deterministic editor builder from released data. Generate Emiko and outfit visual assets through Art's editor action. | Binder compatibility, local-bone isolation, no skeleton duplication; source/assets audit. |
| W03 — Focused release correction | Fork Outfit 1 layer order into candidate, compile the correct reachable fit states, produce new immutable generation and matching catalog/configuration. Wire rig manifest to that release. Preserve old release. | **Appearance gate A**, below. No dependent delivery claims while the layer fit is missing. |
| W04 — Playable sequence semantics | Extend play resolution and all reachability/preview/link/export paths; bounded nested sequences. Change Intro declaration to sequence after compatibility checks. | Local sequence/card invocation, published-card-only rule, ambiguity and recursion cases covered. Root FirstContact remains explicit. |
| W05 — Wardrobe DSL and bindings | Typed aliases, vocabulary, descriptions, reconciler/importer, linked diagnostics, scoped runtime host calls and multi-resource conflicts. | Commands compile, preserve owner identity, reject bad assets and obey no-op/layer/hair rules. |
| W06 — Runtime snapshots and scopes | Typed subsystem capture/restore APIs, child PerformanceRun ownership, host boundary and entry barrier. Do not call session Stop to leave a card. | Coverage matrix implemented for current persistent modes; nested scope and fault lifecycle coverage. |
| W07 — Mandatory smooth card return | One ExecuteCard boundary for every card; exact diff/preparation/restore, 0.5-second pose blends, coordinated actor dissolves, camera quaternion/track restore. | **Card gate B**. No progress/next instruction until verified return. |
| W08 — Studio appearance and call summaries | Field-count/mask cleanup, card identity vs sequence composition, appearance reducers, safe native lease and adaptive binding UI; virtual return cue. | **Studio gate C**, including retained speed and save integrity. |
| W09 — First Contact and handoff | OpeningOutfit/OpeningHair assignments, hidden startup commands, Intro sequence conversion, source declarations/readiness, concise author directions and evidence report. | **Production gate D** on saved main FirstContact. |

Dependency order: W00 -> W01 -> W02 -> W03. W04 and W05 follow the agreed appearance contract; W06 follows shared state APIs; W07 requires W04–W06 and accepted appearance assets. W08 requires protected-card semantics and shared appearance preparation. W09 completes the saved production delivery after gates A–C. Execution remains on main with coherent local commits; this planning request does not authorize a remote write.

Do not adopt old instructions to operate Unity. Executor writes source/tooling and requests Art's specific compile/generation/manual actions at gates. If a builder must generate native assets, the deliverable includes that menu command and its exact inputs; source readiness alone is not an asset-generation success. No automatic gate bypass.

## 8. Acceptance and manual gates

Planned source checks use the existing `tests/DazPose.Story.Tests`, `tests/DazPose.Rehearsal.Tests`, relevant wardrobe pure checks and `scripts/check-unity-source.py` (standalone Roslyn, no Unity process). No checks were run for this planning turn. Native rendering, prefab generation, compilation in Unity and Play remain Art's actions.

### Gate A — Appearance assets and layer order

Deliver one explicit top-level DazPose editor action to prepare the proposed wardrobe rig assets from the selected released/candidate inputs, with a report of paths and differences before asset generation. Exact menu spelling becomes part of implementation handoff; do not send Art to a nonexistent menu today.

Art's bounded check in the prepared saved production composition:

1. Outfit 1 shows Emiko hair, dress and panties with accepted body/lighting.
2. Remove one layer removes the dress; next removes panties; another is a no-op. Add reverses the process, including empty-layer skipping.
3. Hair remains while undressed; switching clothing keeps hair. Explicit hair selection/clear affects only hair.
4. Two actor instances have independent attachment transforms/material state, no shared local-bone contamination, and use the same reusable prefab assets.
5. Relevant fitting/coverage/footwear behavior matches the existing released system. Old release and authored tuning remain intact.

### Gate B — Every card returns, sequences persist

Deliver a small, labelled saved diagnostic composition and scripts, using copies of production bindings without editing protected art. Art must be given the exact scene, object, menu and button names when implemented; diagnostic content uses the same runtime boundary, not a special restoration runner implementation.

Cases:

- Run the same deliberately disruptive card by direct play, explicit Director.Execute and Director draw. Each changes Lara location/rotation, pose, wardrobe/hair, visibility, player body visibility/placement, camera fixed/tracking mode and shader profile. All return before the next cue.
- Test standing, default seated and persistent seated entrance states, including footwear/fit changes while seated. Observe 0.5-second pose return and dissolve relocation, correct contact and no visible snap/frame at the wrong location.
- Fixed camera preserves authored roll/pitch/yaw; tracking camera restores the original target/settings after the actor return.
- Nested card returns to its immediate parent's state, then parent returns to its own entrance. Sequence inside a card is contained by the outer return; sequence outside a card persists.
- No-change cards incur no gratuitous dissolve, attachment rebuild or camera restart.
- Stop during body, wardrobe commit, relocation, and camera return prevents all subsequent animations/output/cards. Throwing/invalid target/failed fit cases get no progress credit and do not continue.
- Card-owned UI actions, motion/follow/grip consumers and event subscriptions cannot leak; parent scope state remains owned and valid.
- Verify no entrance-barrier self-wait with unawaited poses, nested cards or Director calls. After return, parent Stop still cancels the newly restored persistent command generations.
- Intro as sequence leaves the established scene state; converting the same diagnostic body to a card changes only its exit contract.

### Gate C — Studio wardrobe and responsiveness

- Click opening outfit/hair cues, drag alternate compatible assets into their slots, remove/add layer cues and inspect Before/After endpoints in the existing adaptive panel.
- Click a direct card call, a drawn-card boundary and the virtual Return to card entrance cue: the caller state remains known when its entrance was known. Sequence output persists. Unknown card entrances remain honestly labelled inside the card.
- Click cue 91 and later cues, then move/rotate Lara and camera marks. Appearance stays stable and prepared; no graph/inventory rebuild on each drag. Record warm/cold counters and elapsed times.
- Change a pose with clothes on: preserve root/camera placement and reanchor correctly. Change wardrobe with a seated pose: fit, pose, seat and focus update coherently.
- Stop Preview, save/reload, Undo/Redo, close/reopen, domain reload and enter Play. Saved scene/assets contain no temporary mesh/material/visibility/attachment overrides. Intentional binding/mark edits remain.
- Opening look matches Play using the same released assets and fitting rules. No runtime execution is used to find the cue.

### Gate D — Saved First Contact production scene

Main project: `validation/DazPoseUnityValidation`.
Scene: `Assets/Production/Scenes/FirstContact.unity`.
Script: `Assets/Production/Story/Programs/FirstContact.dazstory`.
Studio: `DazPose > Story > Scene Director Studio`.

OpeningOutfit initially references corrected released first-outfit; OpeningHair references Emiko. Lara is dressed before her first reveal; camera and player remain at authored startup transforms. Intro runs as a sequence and retains its final pose/location. Existing dialogue transcripts/audio bindings, equipment flow, phase orchestration and global card membership remain intact apart from the intentional Intro kind change. Subsequent cards return to the phase's presentation state. Art tunes scene marks and content through the same Studio used in production.

No fixture-only art or hierarchy overrides are copied into the main scene. Any code synchronization to a diagnostic project remains code/meta only; all content changes are individually named and reviewed.

## 9. Completion evidence and guardrails

The executor updates a report beside this plan with ticket status, exact files/assets, old/new release IDs, source-only check results, Art's actual observations, measured preview costs, unresolved limitations and recovery instructions. Keep source readiness, native technical results and user visual acceptance distinct.

Completion requires all six requested outcomes: independent reusable hair; reusable wardrobe rig/clothing assets; actual story vocabulary; First Contact initial look; mandatory return for every card with sequences for lasting changes; safe, responsive Studio wardrobe assignment/preview. Do not label a subset complete as the production system.

No new import pipeline, alternate story interpreter, runtime replay in Studio, generic scene serializer, silent fit substitution, replacement player body, global tuning reset, wardrobe assets copied into every card, or hard-coded Intro exception. Reuse the current catalog, fitting, queues, scoped runtime, native preview and source-owned bindings, extending their specific missing contracts.
