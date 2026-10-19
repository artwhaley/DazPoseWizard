# N02–N04 source delivery — native acceptance pending G2

## Ownership and restoration

`NativePreviewLease` is exclusive, bound to the active saved host scene and owns a distinct AnimationModeDriver. It captures local transform components, active state, renderer enabled/forceRenderingOff, material references **per slot**, all blendshape weights, and skinned updateWhenOffscreen before sampling. Baseline global/per-slot MaterialPropertyBlocks are retained; this adapter does not mutate any MPB. Dirty flags and prefab override lists are observed by diagnostics, never replaced or cleared. Author location/seat/camera/focus local transforms are not staged.

Every reapplication restores baseline first. Partial adapter writes are recorded in a finally block. Before stopping AnimationMode, the ledger captures external edits; after stopping only its own driver and destroying its manual sampling graph, it restores only owned values while preserving external values. Deleted objects cannot block surviving-object restoration. New material clones are destroyed on normal or exceptional cleanup. Author material replacements in another slot survive. The shared managed ledger tests cover these mechanisms; Unity native acceptance is separate.

Lara's saved Generic Animator has no RuntimeAnimatorController. Sampling uses AnimationMode.SamplePlayableGraph with an exclusively owned manual AnimationClipPlayable/AnimationPlayableOutput, not the unsafe controllerless SampleAnimationClip path. No RuntimeAnimatorController is assigned and no saved Animator configuration is changed. Clip object-reference/property curves outside Transform and skinned blendshapes are explicitly INCOMPLETE. Active/inactive curves are rejected. Root curves cannot replace commanded placement or scale. An already bound graph, Animation Window/Timeline ownership, inactive Animator or another preview owner is refused.

The editor-only NativePreviewWriterGuard cooperates with LaraWardrobe, LaraFirstOutfit, LaraHeelReview and LaraAnatomyControls while a lease owns Lara. Apply/heel reset/disable hooks cannot overwrite sampled bones, visibility or morphs. Components stay enabled; serialized artist controls and footwear assets are unchanged. The currently authored wardrobe/heel presentation is captured and held while posing; no secondary heel/wardrobe evaluation is claimed. Their appearance and all added property families still require G2/G4 observation. Player body transforms/bones are not staged by the standing adapter; G2 explicitly hides its renderers because FirstContact setup requests StartHiddenAt(player_start).

## Normal save and lifecycle

OnWillSaveAssets suspends before returning all paths unchanged; sceneSaving also suspends direct forced SaveScene. StopAnimationMode precedes ledger restoration. The guard remains held during serialization. sceneSaved/update resumes the same immutable projection after synchronous serialization completes; cancelled/failed save paths release suspension on subsequent updates. SaveAssetIfDirty may bypass OnWillSaveAssets, but no persistent asset is a preview write target. Shared materials are never modified. Prefab Apply stops the lease before applying. Save As stops on scene-path change; scene/Prefab Mode changes, reload/compile, attempted Play transition, window close, missing host and editor quit restore. The executor has not exercised any Unity callback or save ordering.

Native mark Undo/Redo reapplies the same projection, without interpreter replay or random draws. Preview uses no Undo.RecordObject, RecordPrefabInstancePropertyModifications, MarkSceneDirty or dirty clearing. Editing a preview-owned actor field stops directing and preserves detected external values. A conflicting recurrent writer is checked at 10 Hz and stops with a visible error. Actor edits during AnimationMode are not claimed as general authoring support; the G2 authoring targets are actual marks and chair overrides.

## Runtime contracts and fixed diagnostic projection

| Endpoint | Runtime source | Native adapter rule |
|---|---|---|
| Camera.StartAt | DazPose/Runtime/Performer/Authoring/ScopedPlayer.cs StartAt; PlayerView.ResetPerformanceView | PlayerView root world position, ViewRig full mark world rotation; never child camera position/projection |
| Camera.Track | PlayerView.DesiredRotation, AdvanceOrientation | Shared PlayerViewEndpoint.DesiredRotation; actual MainCamera origin, post-sample laraFocus world position; existing zero/vertical fallback; smoothing unchanged |
| Lara.StartHiddenAt | SuccubusPerformer.StoryPresentation.cs StartHiddenAt | Full mark rotation, renderer forceRenderingOff; actor root stays active |
| Lara.DissolveIn | SuccubusPerformer.cs DissolveInAtAsync | Full mark rotation, settled visible renderer endpoint |
| Lara.StartIdle | SuccubusPerformer.StoryPresentation.cs StartConfiguredIdle | Requires actual initialPose/Clip; no synthesized fallback |
| Lara.LocomoteTo | PerformerLocomotion.BeginFinalAlignment/BeginSettling | Destination yaw and horizontal endpoint, preceding actor height; locomotion layer relinquishes ownership to underlying persistent pose |
| Standing DissolveTo | SuccubusPerformer.SeatedPresentation.cs RelocatePresentationAsync; ScopedPerformer.StoryDissolveToAsync; PerformerDissolve | Packet's explicit standing yaw + configured initialPose rule; simple standing runtime route resolves arrival pose through existing dissolve owner |
| PersistentPose | PerformerPose.Clip; real configured Generic Animator | Actual clip at time zero under owned AnimationMode/manual graph |
| Say / completed gesture | Runtime persistent pose and gesture layer ownership | Preserve composed root/pose/camera; awaited gesture after-state is underlying pose, not terminal gesture clip |
| Non-awaited gesture | FirstContact source Disapproval call | Explicit in-progress / indeterminate boundary; no standalone gesture sample |
| MagicShader | PerformerMagicShaderPresentation.Apply / RestoreSurfaces | Per-renderer/slot transient clone substitution; restore original references and destroy clones. Native/material acceptance pending G4 |

G2 resolves the saved StoryProgramSequence bound to Game Session, validates its actual compiled FirstContact source and SexyVamp binding, then stages a small fixed projection. This is explicitly **not N06 cue transport**. Buttons expose actual Intro DissolveIn, locomotion idle, SexyVamp and standing Lounger dissolve endpoints, Camera.StartAt versus Track, and select the live authored marks. Polling watches semantic marks and focus **local** offsets, not sampled head/focus world churn. Mark drags reapply the same projection; no StoryMachine or Director is run by this diagnostic.

Dissolve MPBs must already be settled (enabled/progress zero). A residual authored dissolve block is an explicit INCOMPLETE error instead of overwriting unknown block data. Material visual/MPB effects beyond that condition remain unapproved until the relevant native gate. Seating and full transport remain held for G2/G3.

## Evidence

N04_SOURCE_COMPILE.json: seven standalone Roslyn assembly compiles; no Unity executable.
N04_MANAGED_RESULTS.json and logs: Story 90, Player 24, Rehearsal 29, Application 69 = 212 passing managed checks, no failures/skips. The 16 new checks cover ledger idempotence, conflict/partial-error/material-slot/deletion/save restoration, exact extracted camera formula, actual origin with zero/nonzero child offsets, vertical/coincident target and standing full-rotation/yaw rules. Math shims are test-only; these do not prove Unity rendering, clip sampling or serialization.

N04_ASSET_AUDIT.json/log: source/meta/reference audit; historical p2-p3 report preserved. PROTECTED_CHECK.json: original protected files checked separately. G2_CODE_SYNC.json records the synchronized source fingerprint; fixture marker records the actual local revision. Art alone imports/compiles/checks the fixture in Unity. Native N02–N04 status stays provisional until G2 PASS, and later seating/material families require G3/G4.

Snapshot reference comparison normalizes Editor JSON instance IDs to GlobalObjectIds so a legitimate scene reopen does not fabricate actor/reference differences. Transient references retain their distinct instance identity and cannot be confused with saved material assets.
