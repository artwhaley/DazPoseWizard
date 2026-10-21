# N02 — Reversible preview lease; Ctrl+S, Undo, and lifecycle ownership

**Depends on:** Art-confirmed **G1 PASS**. **Deliverable:** `NativePreviewLease`/`NativeSceneStage` safety substrate, source tests and **user-invoked** save/Undo diagnostics, to be verified at G2 and repeated at G3/G4.

## Execute

1. One exclusive Edit Mode preview lease bound to saved scene/host. Take **per-property** snapshot **before the first pose sample** of actor/body/camera roots, bones/root curves, enabled/active/`forceRenderingOff`, renderer `sharedMaterials`/property blocks, blendshape weights and all stage-owned values. Record dirty/prefab overrides as baselines for comparison **not** wholesale restoration. Cross-reference `[ExecuteAlways]` wardrobe/outfit/heel writers and MagicShader material-clone writes from N00; define how they coexist. Never move authored location/seat/camera anchor via stage.
2. Own sampling with distinct `AnimationModeDriver`; detect Animation Window/Timeline interference, root curves and editor writers. **Stop owned AnimationMode before restoring ledger** on suspend/stop/scene save, including exceptional paths; baseline reestablish before resampling. Don't assume driver handles materials or visibility. Avoid root `SetActive(false)` unless separately proven safe—may disturb actor lifecycle/runtime-equivalent pose.
3. Reapply **baseline + the current immutable cue projection** to real saved actor/body/camera. Preview changes never use Undo.RecordObject or PrefabUtility.RecordPrefabInstancePropertyModifications; authored mark edits always do.
4. Normal Ctrl+S/Save: suspend preview before serialization, preserve authored edits, restore preview after save. Handle Save As/cancel/failure/multi-scene/prefab/asset-save and callback ordering/reentrancy. `OnWillSaveAssets` does **not** guarantee interception of `SaveAssetIfDirty` (different asset mutation). Never mark scene dirty for preview; stage-only clean scene stays clean. An **explicit Art-operated forced SaveScene on disposable copy** is required because Ctrl+S with clean scene may do nothing.
5. Observe editor reload/compile, attempted Play transition (don't enter Play), OnDisable, sceneClosing, Undo redo and host invalidation. Restore only preview-owned values, **never revert legitimate new prefab overrides** or outstanding unsaved mark changes. Stop preserves dirty state caused by author. Detect write conflicts rather than overwriting user edits. Ignore stage-owned transform/bone churn in revision detector (N06).
6. Prepare user-invoked diagnostics to validate no authored edit Ctrl+S **and forced serialized save**, deliberate mark edit Ctrl+S, Undo, new chair prefab overrides, Stop with unsaved mark edit (must remain dirty), reopened references/bones/renderers, exceptions. These are **N04/G2 evidence**, not automated execution by executor; repeat with seat/material/gesture properties at G3/G4.

## Gate

- A scene saved during active preview has **no preview-only changes** in serialized data; editing one mark retains **only the authored edit**. Ctrl+S behaves normally, not through special Studio Save.
- Preview-only staging does not create Undo history or mark a formerly clean scene dirty. Native Undo/Redo of an authored transform preserves expected stack; **Stop preserves authored dirty state** even if originally clean.
- Closing Studio, changing source/scene, entering Play, assembly reload and exceptions fully restore all preview-owned properties and leave authored edits intact.
- Restore does not overwrite unrelated user edits to a non-preview-owned field or erase newly authored prefab overrides; per-property lease detects/reports conflicts instead of clobbering.
- **N02 source status is provisional** until Art's G2 and then G3/G4 cover every new property family. Executor must not claim native PASS from API inspection or CLI tests.

## Failure handling

If the Unity callback ordering does not permit safe suspend/restore, block unsafe saving as a temporary **fail-closed** measure; do not mark N02 or the sprint complete. Resolve the normal Ctrl+S semantics before N10 PASS. Never silently serialize performance pose.
