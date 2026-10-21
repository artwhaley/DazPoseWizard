# C07 — Move genuinely production-owned assets out of test/proof folders

**Dependency:** C00–C06. This is the high-risk phase. Execute as small named asset groups, never one entire directory move.

## Destination ownership

Proposed targets (confirm actual consumers first):

```
Assets/Production/Environments/Void/{Materials,Meshes,Prefabs,Settings,Shaders,Textures}
Assets/Production/Performers/Lara/{Rig,Materials,Surfaces,Contact,Configuration}
Assets/Production/Player/{Meshes,Materials,Contact,Support}
Assets/Production/Content/{Poses,Actions,Gestures,Motions,Magic,Audio}
Assets/Production/Story/{Programs,Cards,Motion}  # companions stay beside their .dazstory
Assets/Authoring/{Scenes,Wardrobe/Drafts,Preparation}
Assets/Examples/{Scenes,Content}
```

Folder names are suggestions, not authorization to move immutable releases/vendor roots into new locations. The existing configured pose/wardrobe import roots may have to remain until their consumers are made relocation-safe.

## Grouped moves

1. **Void art:** ownership-transfer the active `FirstPerformanceVoid/Materials` dark/neon/smoke/gloss assets, `StageGlowRing.asset`, `ValidatedLoungeSeat.prefab`, Void volume profile, smoke shadergraph/license and smoke density/billow assets. Check what the frozen original `Scenes/FirstPerformanceVoid.unity` references; if it uses the same GUIDs, **do not alter that original scene file**. Moving shared asset with same GUID may keep reference intact, but audit shaders/path generators and verify rendering data unchanged. Do not duplicate material assets or retune.
2. **Prepared male:** group supported `PartnerProof/Generated/G8M_ErectBody.asset`, `G8M_ErectShell.asset`, `PartnerShell.shadergraph`, `TestData/PartnerCharacter` live support materials/textures/metadata, player prefab and measured contact. Preserve source `TestCharacter/playererect.fbx`, skin references and morph/contact calibrations. Retire `PartnerProof` naming only after all runtime/prep paths repaired.
3. **Lara:** group live `TestData/LaraCandidate` canonical rig/surface/material/dissolve support, canonical pose FBX references, character import manifest and runtime look. Avoid moving `TestCharacter/lara.fbx` until importer configuration and all downstream references make it safe; preserving it at old path is acceptable in Task 1.
4. **Prepared performer content:** inventory `Animations/Performer` walking/seating, imported poses, selected grip profile, motion and gesture masks, compiled Magic catalogs, `motiondrive.funscript` and `teleportsound.mp3`. Move only data with known owning product subsystem. Avoid re-baking 92 MiB animations merely to change paths.
5. **Story content:** retain `FirstContact.dazstory`, its same-basename `.bindings.asset`, dialogue map, all 19 recordings and their `.meta`, story marks, engineering gestures/motions/shaders. Align global selectable cards with C05 without breaking authored script/source companion naming. Do not add manual scene library lists.
6. **Wardrobe:** keep immutable releases, `...Remediation/R03/second-outfit` and the pending toe fixture closure at their current paths throughout Task 1. Record their production/reference ownership explicitly; this is a deliberate HOLD, not unfinished folder housekeeping. Only mutable support/draft data outside those protected closures is a move candidate after consumer verification. Keep draft and released config separated.

## Procedure for EACH move group

A. List exact source paths, destination paths, original asset+meta GUIDs, sizes, serializer dependencies, code strings, importer/recipe routes, ignored references and published review paths.  
B. Verify a recoverable version including ignored/generated inputs if applicable.  
C. Change path-sensitive consumers; perform Unity `AssetDatabase.MoveAsset` operations with error checks. Preserve GUID and original asset bytes when appropriate. Never do raw `File.Move` while Unity has unhandled meta references.  
D. Refresh/import only the changed group with reviewed hooks; never force a full reconciliation, failed-input retry, historical rebake or model reimport just to check a move. Compile and inspect saved references. Frozen scenes/releases must remain byte-identical; for editable moved assets allow only explicitly manifested path/import changes and compare required serialized values (rig/material/scene).  
E. Run relevant managed + EditMode tests and GUID audit. Record the exact diff, successful path replacements, and before/after dependent asset list.  
F. If a dependency cannot be moved safely, rollback only that move and mark blocked; keep the rest of the already accepted groups.

## Gate D2

All *movable* verified production data has clear ownership with preserved GUIDs. Unmovable live data is explicitly documented—not abandoned, copied without reason, deleted, or mislabeled as cleanup success. No references to nonexistent asset paths or import roots.

## Move identity and importer checks

Keep `.dazstory` source and same-basename `.bindings.asset` adjacent as a move unit; a separate generic Bindings folder breaks the current importer. Move related maps/audio only with their recorded path consumers. Preserve import subasset identity (`compiled` in DazStoryImporter), script GUID/type/assembly mappings, FBX model subasset fileIDs, external object remaps, prefab overrides and folder metas. GUID equality alone does not prove a model/clip/component still resolves.

After source-path rewrites, update importer configuration, producer output destinations and recipes too: otherwise the next import regenerates a second copy at the retired path. Use a disposable import fixture to prove current output ownership; never reimport the real accepted character merely for this check. If moving an asset would trigger an unbounded bake or rewrite a protected release, HOLD it at the existing location.

Shared assets used by the frozen original may move only if their bytes/import settings, GUID/fileIDs and reference resolution remain intact without resaving that scene. Retained reference-scene dependencies are never ignored when calculating move/delete closure. No broad directory moves or deletion of an apparently empty folder containing required metas/ignored inputs.
