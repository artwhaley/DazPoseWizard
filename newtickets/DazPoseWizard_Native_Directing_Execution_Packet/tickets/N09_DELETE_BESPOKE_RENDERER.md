# N09 — Delete Studio's duplicate scene/rendering implementation

**Depends on:** Art-confirmed **G1, G2 and G3 PASS**, plus N06–N08 source work. **Deliverable:** one authoring stage; custom viewport implementation retired. Do **not** delete old renderer during an unresolved camera/seat/save gate.

## Execute

1. Remove `DazPose/Editor/StoryStudio/ScenePreviewSession.cs` and its `.meta` after verifying all Studio callers are replaced and no independent supported public code consumes the class. Remove associated `PreviewRenderUtility`, cloned environment/renderers/materials, custom orbit/zoom/input, preview ground raycasts, fake frustum drawing and `Build preview` workflow.
2. Refactor `SceneDirectorStudioWindow.cs` and `.Rehearsal.cs` / `.Revisions.cs` to depend on **one** `NativeSceneStage` lease with actual saved scene objects. Replace old `stage.Sample/Arrival/SeatedArrival/Magic/TrackLara` calls with typed visual-projection stage calls; don't keep a permanent compatibility shim that itself clones a scene.
3. Remove obsolete custom pane splitters if they only existed for the preview renderer. Keep source tree, requirements and transport compact. Remove stale documentation and tests claiming a separate Viewport, Preview Camera or orbit controls.
4. Search `Assets/DazPose/Editor/StoryStudio` for `ScenePreviewSession`, `PreviewRenderUtility`, `BeginPreview`, `stage.Draw`, `DragOnGround`, `Project` and old preview helpers. Record every match/disposition. Existing **independent clip-only/asset-specific** preview windows elsewhere are out of scope.
5. Ensure no authored scenes, GUIDs, published bindings, pose data or other production assets need replacement or cleanup. No asset regeneration.

## Gate

- No secondary performance-scene renderer or option to switch between old/new modes remains.
- Native Scene view and Game view remain fully usable; performance cue end states work after deletion.
- Seven Unity assemblies compile; editor tool loads, no missing menu/item/type references; no data migrations required.
- Executor proves source checks; **Art** confirms Unity assemblies/import/menu health at G4. Don't invoke Unity CLI. No claim of actual native compile without Art's report.
