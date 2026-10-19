# Unity API anchors and critical feasibility risks

This packet specifies the **required outcome**, not a claim all editor-time behaviors have been proven. The following official Unity docs establish possible mechanisms. Executor may inspect **source/API metadata outside Unity** but **must never launch/operate Unity**. Only Art checks behavior in the Editor at **G1–G4** after explicit stop-and-ask, including manual compilation. An unrun native gate blocks next dependent work.

## Primary documentation

- [Game view reference (Unity 6000)](https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-windows-views-reference/game-view): Game view is rendered from scene cameras; Edit Mode options include "Clear Every Frame in Edit Mode" and display routing. **Not proof** that current FirstContact HDRP camera renders when staged.
- [EditorSceneManager (Unity 6000.5)](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityeditor/scenemanagement/editorscenemanager): open edit scenes and sceneSaving/sceneSaved lifecycle. **Not proof** of save callback sequencing with active AnimationMode/Prefab overrides.
- [AnimationMode.StartAnimationMode (Unity 6000.5)](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityeditor/animationmode/startanimationmode): supports explicit `AnimationModeDriver` ownership. **Not proof** that all bone/root/material/visibility properties auto-restore; explicit ledger needed.
- [QueuePlayerLoopUpdate (Unity 6000)](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/editorapplication/queueplayerloopupdate): queues Editor player loop without requiring a modified scene. **Not** a reason to run actual game sequences or create permanent `ExecuteAlways` components.
- [OnWillSaveAssets (Unity 6000.5)](https://docs.unity.com/ko-kr/engine/6000.5/script-reference/unityeditor/assetmodificationprocessor/onwillsaveassets): the processor can filter file paths on save. Use only if required; respect project-wide save behavior. **Not** a replacement for the normal Ctrl+S save contract.
- [AssetDatabase.SaveAssetIfDirty (Unity 6000.5)](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityeditor/assetdatabase/saveassetifdirty): explicit asset save **does not invoke** `OnWillSaveAssets`. Scene-copy fixture must not share binding/index asset mutation with production assets.
- [Scene view overlays (Unity 6000.5)](https://docs.unity.com/en-us/engine/6000.5/manual/working-with-scenes/scenes-manage-gameobjects/overlays): native overlays can expose minimal authoring chrome. Optional, not an excuse for custom 3D rendering.

## Distinguish three proof levels

1. **Documented API existence:** a method/event is available. This is good architectural feasibility evidence, but not scene behavior proof.
2. **Art-operated native integration:** a disposable saved scene within **independently copied project** demonstrates stage/edit/save/restore. Diagnostic menu code can assist but **Art clicks it**; executor does not run Editor or batch tests.
3. **Art visual judgment:** FirstContact renders through actual camera and Lara is physically seated/standing. Source inspection cannot certify composition or taste.

## Most consequential pitfalls to proactively test

- Staging the **saved player root** and **viewRig** must not make the camera render twice or leave no enabled camera, especially when the player hierarchy starts inactive. Treat required temporary activation as lease-owned and restore.
- If the Game view's apparent camera is actually a runtime-instantiated/prefab-only camera, there may be no exact saved camera to stage. N00/N01 must discover this and repair proper saved camera ownership, not make up an Editor-only camera.
- Unity's AnimationMode may create sampled modified properties in ways that sceneSaving sees differently from direct transforms. Save-only preview must not persist; test no-Undo, Ctrl+S, undo/restage, close/reload and prefab overrides.
- `[ExecuteAlways]` Lara wardrobe/outfit/heel scripts can write `renderer.enabled`, blendshape values and materials during Edit Mode `LateUpdate`; define ownership, snapshot all affected preview properties **before sampling**, stop owned AnimationMode **before** explicit ledger restoration and test writer race. Do not assume `SetActive(false)` is harmless to rig lifecycle.
- `PerformerMagicShaderPresentation` swaps `renderer.sharedMaterials` with transient instances; property blocks alone cannot model this safely. Restore references, dispose clones and test actual serialized material arrays. Unimplemented supported visual cues are **INCOMPLETE**, not labelled PASS after no-op.
- A scripted preview write can trigger `EditorApplication.hierarchyChanged` or `Undo.postprocessModifications`. Mark these writes as stage-owned and prevent a self-invalidating event loop without masking **actual authored mark edits**.
- `EditorSceneManager.sceneSaving` and `.sceneSaved` may not cover every Prefab stage, Save As, or asset save variant. Explicitly test all save paths used in the authoring workflow. Fail closed on unknown contexts.
- Scene data can contain post-processing/Volume and renderer states with execution-time parts. Keep the existing scene and existing camera so static HDRP matches, but disclose effects that need real runtime animation/solver.
- `PerformerSeatingProfile` baked motion pelvis root offsets may be sampled on a bone hierarchy whose source rig differs from Unity's Generic Lara; validate clips/root offset before assuming simple world transforms.
- Persistent seating (`AnchorPersistentPose`) resolves **pose-sampled live hip to seat** after root yaw, distinct from `SitAt` default CrossLegs baked stable rest. FirstContact Intro Lounger arrival is standing, Warmup first sit, Main later seated dissolve; do not conflate those tests.
- If FirstContact in Edit Mode already has separate scene camera object and Game view uses a player rig, `Camera.StartAt` must move **the rig that actually drives the Camera**, not merely a `Camera` transform. Track uses the camera/head position, not the actor root.
- Real Scene and Game tabs can show different frame rates/updates while editor idle; request a bounded repaint when cue state or authored marks change. Do not run editor loop constantly or force the scene dirty for refresh.
- Broad `CaptureStamp` scans and stage-owned revisions are a feedback hazard; watch relevant referenced anchors' matrices (including parent transforms), not every component dirty count. `laraFocus` is a head descendant, so compare authored local value and track aim **after** pose sampling.
- A clean scene's Ctrl+S can skip serialization; **Art** must force `SaveScene` **on isolated disposable fixture** for the leak test. New authored chair-prefab overrides must survive Stop, even if original scene was clean; never restore original dirty flag/whole override list over user edits.
- Current rehearsal queue catches visual errors and continues. New stage must propagate first visual failure to transport, discard later queued effects and leave offending cue visible.

## If a native safety claim fails

**Pause and ask Art**, preserve protected hashes, record actual reported native repro and minimal source fix. The executor may implement native Editor-only stage code but **never execute it**. Re-request the same G gate after repair. It **may not** count a custom Save button, fake preview, batch-mode CLI test or Play-required directing as meeting the brief. Report blocker for explicit review rather than silently changing requirements.
