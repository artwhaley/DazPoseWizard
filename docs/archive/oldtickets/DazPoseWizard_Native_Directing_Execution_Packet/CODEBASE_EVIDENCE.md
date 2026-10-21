# Grounding notes from pushed source — not an implementation claim

These observations were checked against the GitHub repository `artwhaley/DazPoseWizard`, branch `main`, pinned SHA `4656eb56fe1cfa2201104e960b4d7e8dc41cb766`. They explain why the replacement is targeted and why some apparently simple proposals are incorrect. Reviewers must check any newer HEAD before commenting on code drift.

## Where the unwanted architecture is

- `validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/ScenePreviewSession.cs`: has `PreviewRenderUtility`, its own `root`/transform map, copied `MeshRenderer`/`SkinnedMeshRenderer`/lights/Camera, materials, particle snapshots, marker meshes, orbit camera, drag-on-ground, camera frustum rendering, seating and animation sampling. The private stage does **not** render the saved scene through the same Game camera.
- `.../SceneDirectorStudioWindow.cs`: `DrawStage()` allocates an internal EditorGUILayout 3D pane, custom drag input, and `BuildPreview()`. It uses `SceneView.duringSceneGui` for preview handles while retaining the private view. It has a separate `Commit Mark` button for authored position changes, unlike the requested normal native drag/save flow.
- `.../SceneDirectorStudioWindow.Rehearsal.cs`: `ApplyPreviewCommand()` forwards canonical story commands to old stage `Arrival()/Sample()/SeatedArrival()/Magic()/TrackLara()`. `DrainRehearsal` currently **catches a visual error and continues draining**, risking later scene changes overwriting a failed cue. Keep upstream canonical events, replace receiver with **ordered fail-fast** projection transport.
- `.../SceneDirectorStudioWindow.Revisions.cs`: `CaptureStamp()` currently hashes **all scene transforms and every component's dirty count** upon hierarchy/Undo changes. Mark edits must reproject **without rerunning Director**; stage-owned bone/rig changes must not invalidate logical story. Replace broad stamp with separate content revision and watched authored-anchor world matrices; adjust previous `StudioCorrectiveChecks` that expected a mark drag to change same stamp.

## Working systems to preserve

- `.../RehearsalSession.cs` and `RehearsalStoryHost.cs`: canonical virtual session (detached player/profile/world), output-free host, deterministic scenario and source event stream.
- `.../Runtime/Story/Language/StoryMachine.cs` and `StoryRehearsal.cs`: interpreter execution, before/after hooks, source-aware visit/encounter identity and explicit branch/Director choice handling.
- `.../Runtime/Story/Language/StoryCards.cs` and `.../Runtime/Story/Runtime/GlobalStoryCardCatalog.cs`: tagged global eligible-card publication/selection. No scene library membership.
- `.../Editor/StoryStudio/SourceBindingReconciler.cs` and `.../Editor/Application/DazStoryImporter.cs`: source-first symbol manifest and managed typed asset binding ownership.
- `.../Editor/StoryStudio/SceneMarkAuthoring.cs`: real `StorySceneBindings` host integrity, native Unity Undo for authored mark creation/commits and cross-scene validation. Keep these safety ideas while making target editing normal Unity.

## Actual runtime view hierarchy and camera semantics

- `validation/DazPoseUnityValidation/Assets/Player/Runtime/PlayerController.cs`: `MainCamera` and `HeadTransform` expose underlying `PlayerView`. Script commands go through `ScopedPlayer` and `PlayerView`.
- `.../Assets/Player/Runtime/PlayerView.cs`: serialized `viewRig`, `headPose`, `mainCamera`; `ResetPerformanceView(position,rotation)` changes **player root position** and `RequireViewRig().rotation`. `Track()` maintains an orientation operation; `DesiredRotation(worldTarget)` derives aim from `mainCamera.transform` (fallback `headPose`) and a documented vertical-up fallback. Runtime `LateUpdate` manages smooth movement/tracking, so calling it from Edit Mode without running the game is wrong.
- `.../Assets/DazPose/Runtime/Story/Runtime/PerformanceStoryHost.cs`: `Camera.StartAt` resolves `StoryLocationBinding.placement` and calls `run.Player.StartAt(...)`, `Camera.Track` resolves `StorySceneBindings.laraFocus` and invokes runtime tracking. Current source vocabulary primarily uses these camera commands; do not invent unsupported camera animation commands as a shortcut.
- The saved `Player -> ViewRig -> HeadPose -> MainCamera` chain has **zero child local offsets** in the checked FirstContact scene; the serialized `viewRig/headPose/mainCamera` references exist. This is source/YAML wiring evidence, **not G1 rendering proof**. `PlayerView.ResetPerformanceView` has straightforward edit-safe transform endpoint logic; prefer a minimal accessor/refactor, not redundant camera infrastructure.

## Actual performance actor/seating ownership

- `.../Assets/DazPose/Runtime/Story/Runtime/StorySceneBindings.cs`: scene locations are `{id, placement, PerformerSeat seat, Transform focus}`; actor roles are `lara`, `laraFocus`, `playerBody` and scene-run services. Positions are **scene-specific**, content source companion is distinct.
- `.../Assets/DazPose/Runtime/Performer/PerformerSeat.cs`: `ApproachAnchor` and `SeatAnchor` are separate transforms; `TryCaptureFrames` yields usable horizontal rotations and validates the baked seating profile. The difference matters physically.
- `.../Assets/DazPose/Runtime/Performer/PerformerSeating.cs`: runtime seat transitions include approach, align, sit/start, cross-legs and final seated states, with baked frame offsets. The end frame must be derived from these semantics.
- `PerformerSeating.AnchorPersistentPose()` (called via `SuccubusPerformer.LateUpdate`) aligns actor to seat **yaw**, then adjusts actor root by `SeatAnchor.position - live sampled hip.position`. Baked `SitStart.PelvisOffsetAt(1)` is a placement/transition offset, **not** persistent Lounging final correction. `SitAt` ends in default seat style/CrossLegs with its own baked stable solve. `PerformerSeat.TryCaptureFrames` is internal but the Editor assembly already has `InternalsVisibleTo("DazPose.Editor")` access.
- `SuccubusPerformer.SeatedPresentation.RelocatePresentationAsync`: standing `DissolveTo(location)` sets destination/yaw and **snaps to `initialPose`**; contrary to an old matrix claim. `StartHiddenAt`, `DissolveInAt` and `Camera.StartAt` take full placement rotation. `LocomoteTo` uses destination facing yaw. Compare code method for each row.
- `PerformerGestureLayer`: completed awaited gesture fades additive masked gesture layer weight back to zero before waiter resolution; the **after state is underlying persistent pose**, not gesture terminal frame. FirstContact also has **non-awaited** gesture invocation, whose visible observation point must be declared.
- `PerformerMagicShaderPresentation`: temporarily swaps renderer `sharedMaterials` references to runtime-created material clones; a mere property block cannot model every endpoint. The preview must own clone references and restore without serialized leaks.
- `[ExecuteAlways]` `LaraWardrobe`, `LaraFirstOutfit` and `LaraHeelReview` write renderer enabled/blendshape/material-related state in `LateUpdate/Apply` on real Lara, outside existing preview ledgers. Their interplay with AnimationMode/save/Stop is a native safety risk. Don't hide it by blanket Lara-root deactivation.
- `StorySceneBindings.laraFocus` (`Story Face Target`) lives below Lara's **head bone**. Pose sampling changes its world position without an authored local edit. Camera must aim **after pose** and the revision detector must not count the stage-driven world motion as anchor edit.
- The Lounger transforms live within a **Validated Lounge Chair prefab instance**. User dragging seat/approach/focus produces legitimate prefab overrides; old acceptance prohibition on all new overrides was wrong.
- `SourceBindingReconciler.ReconcileAsset` can fill missing `ownerSourceGuid` in FirstContact binding assets and call `AssetDatabase.SaveAssetIfDirty`; the scratch global tagged-card/index test writes a **shared global catalog**. **Copying only a scene does not isolate these assets.**
- `.../Assets/DazPose/Runtime/Performer/PerformerSeatingLayer.cs`: owns persistent seated clip, blended seated motion and weights through a PlayableGraph. **Do not try to run full performance PlayableGraph in Edit Mode** merely to get a static end frame; extract shared endpoint data where necessary.
- `.../Assets/DazPose/Runtime/Story/Runtime/StoryProgramSequence.cs`: runs the actual `CompiledStoryAsset` via `StoryRunner` in `PerformanceRun` and uses `GameApplication`; the Edit Mode Studio should not invoke gameplay session lifecycles.

## Previous execution status / reason for this packet

Latest completed corrective source report is `docs/studio-corrective/EXECUTION_REPORT.md`, which claims tests passed but labels **native Unity / Game/Scene view / Undo / rendering / seat acceptance as PARTIAL or unrun**. The issue is not that it needs another round of custom-preview polish: it's that the private renderer was the wrong directing surface. This packet replaces that adapter and preserves the competent logic/authoring work around it.

## Review boundary

The above facts are grounded in **source/YAML inspected on the pinned commit** and require fresh confirmation if HEAD moves. Exact camera activation, pose contact, Edit Mode HDRP rendering and Ctrl+S/AnimationMode behavior remain **Art-operated G1–G4** observations. **Executor may never launch Unity**, regardless of whether a process seems easy to start; ask Art via `MANUAL_GATE_PROTOCOL.md`.
