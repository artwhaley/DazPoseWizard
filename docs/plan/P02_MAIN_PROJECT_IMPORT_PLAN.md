# P02 — remove the cloned-project requirement from wardrobe importing

Analysis and proposed implementation sequence, October 21, 2026. Requested by Art during T01. **Analysis is delivered; migration is not yet implemented.** No Unity operation or asset regeneration was performed. P02 remains temporarily at `.dazposewizard/p0c-native-generation` until this replacement and its data-retention steps are implemented and Art verifies the main-project workflow. Do not create another clone.

The importer implementation is already in the main project. Nothing in the inspected importer engine needs copying back from P02. The missing work is a safe main-project entry point, removal of scene-changing/global assumptions, transactional asset publication and registration of a newly imported package in Clothing Setup. Changing `-projectPath` alone would be unsafe.

## Verified source and content comparison

Full comparison evidence: [P02_ANALYSIS.json](../workspace/P02_ANALYSIS.json). Paths there reflect the pre-cleanup Player location; Player now lives under `Assets/DazPose/Player`.

- P02 contains 399 C#/assembly/DLL files. Ten have no same-path main counterpart; one is the moved DazPoseEditorCommands script, identified by the same GUID at `DazPose/Validation/Editor/Authoring`. The other nine are old setup/build/acceptance helpers, not a hidden wardrobe import engine. Do not copy them into main.
- 127 same-path source/assembly files differ, even after newline normalization. The clone is not authoritative over current runtime/editor code. Copying its code back would overwrite later work.
- The six core files `WardrobeImporter.cs`, `WardrobeImportRecipe.cs`, `WardrobeGeometry.cs`, `WardrobeImportValidation.cs`, `WardrobeImportRenderValidation.cs` and `Daz3D/Scripts/Editor/DazPoseDtuConversionAdapter.cs` are present in main and byte-identical to P02. Their helper implementations are also in main; main source compilation was checked separately by T01.
- Package manifest, package lock and Unity version files are byte-identical. No extra Unity package or different editor version must be migrated.
- Across P02’s `Assets/TestData/Wardrobe`, `Assets/TestData/LaraCandidate` and wardrobe-import reports, 2,147 files were compared: 2,115 are byte-identical at the same main path, six differ, and 26 are absent at the same main path.
- The six differing files are three `Outfit.asset` files and their three `Review.unity` hosts. Outfit diffs include old assembly serialization identifiers and `requiresBentFootPose` fields in P02; these are asset-version differences, not authority to overwrite main’s published assets or artist settings. Preserve main; inspect runtime interpretation if later import parity fails.
- Of the 26 absent paths, eight are old candidate-body/profile/bindings assets and a candidate preview scene plus metas; some represent prior moves to production paths. Eighteen are historical import/source-hash/reimport logs. The JSON records GUID matches for moved files. None of this justifies restoring obsolete candidate art paths or scenes.

## What currently makes P02 mandatory

| Component | Actual behavior | Main-project replacement |
|---|---|---|
| `scripts/import-lara-outfit.ps1:12–22` | Hardcodes P02, requires its ProjectVersion file, creates reports/lock there, even before Preflight/Inspect. | Main-project paths by default; no source-code sync, cloned project or alternate project discovery. |
| Wrapper code/FBX/override staging | Copies main C#/asmdefs/asmrefs, selected FBXs and material/footwear overrides to P02; legacy stale-source repair helper patches the clone. | Read current source/assets directly. Shared preparation tools use explicit main paths. No staging code copy or repair step. |
| `WardrobeImporter.Build():30–50` | Refuses non-batch use; reads recipe from process args; opens `characterScene` with `OpenSceneMode.Single`; finds performer globally. | Public recipe/context API called by a main-editor command; temporary in-memory actor/context, with no scene replacement. |
| Import body/presentation helpers | Mutate a temporary body, attach donor garments/local bones, call global presentation helpers, and save the active scene to `Review.unity`. | Keep mesh/material/footwear algorithms; pass explicit actor/body/scene context, destroy temporary objects in finally, and publish equipment assets without saving a review scene. |
| `WardrobeImportRecipe` | CLI argument reader; Scene is destination/Review.unity; Output is project-relative TestOutput. | Reuse schema/read(path), add explicit workspace/report paths and context. Existing recipe characterScene fields become legacy compatibility, not a required fixture. |
| Asset/render validators | Open recipe.Scene in single mode and look up wardrobe globally. Reimport proof temporarily changes saved material/footwear values. | Validate explicit transient context; use preserved hashes and actual repeat-import parity. Artificial mutation proofs must use transient copies, never artist assets. |
| `LaraWardrobeMotionDiagnostics.RunRecipe` | Opens review scene, registers callbacks and enters Play. | Explicit optional Art-operated motion review in existing Clothing Setup; not a prerequisite to package generation. No new fixture, auto-Play or mandatory render campaign. |
| Wrapper Publish branch | Copies generated tree/reports back, guards override hashes, backs up main, versions review scenes, writes ProjectSettings review metadata. | Same-project owned-asset commit with those preservation/hash checks retained; no project-to-project publish or automatic new review scene. |
| Catalog integration | `WardrobeCatalogBuilder.BuildKnownPackages` hardcodes the three historical package IDs. `WardrobeCandidateReimportPostprocessor` updates mutable candidates only if they already reference the imported package. Clothing Setup’s Create Variant operates on an existing preset. | Add generic new-package-to-candidate registration using stable package identity; reuse configuration, fit compiler and immutable generation publisher. Merely producing Outfit.asset does not complete the workflow. |

## Proposed implementation sequence

### 1. Retain only needed P02 history and compare identities

Use the recorded full lists to archive only stage-exclusive import logs/source-hash reports and any specifically wanted review evidence under `docs/archive/p02-import-evidence/`. For missing-path candidate assets, resolve GUIDs against main before deciding whether they are already relocated. Preserve main assets; do not copy the clone’s scenes, body snapshots or old scripts back. Record the retention mapping and artifact hashes. P02 is not deleted until this step and the replacement are finished.

### 2. Separate recipe preparation from Unity orchestration

Reuse `wardrobe-recipe.py`, `prepare-lara-wardrobe.py`, `prepare-lara-heel-reference.py` and existing recipe schema. They already accept a project argument and default preparation can target main. Preflight/Inspect must work without P02, Unity, a project-wide copy or code synchronization. Keep source ownership, source/calibration/texture hashes, shoe policy and geometry guards.

Make PowerShell an optional preparation/status convenience using the main project; it must not launch a second Unity process against an open main project. Unity work is initiated from the main editor by Art. Preserve the Blender Python FBX-parser dependency as a documented preparation dependency; do not confuse it with needing another Unity project.

### 3. Expose the existing engine through one editor import action

Add **DazPose > Wardrobe > Import Outfit...** in the existing wardrobe tooling. Choose a recipe and show its input/output identity. Invoke a typed `Import(recipe, context)` API. Keep a compatibility batch adapter only if useful; it must call the same engine and must not be the production requirement.

Create a temporary preview-scene context with an instance of the canonical production Lara prefab/body and the retained point manifest, rather than opening the old candidate scene. Resolve actor/body/donor/presentation explicitly from that context. Audit called helpers for `FindAnyObjectByType`, active-scene lookups, global camera/lights and SaveScene calls; replace their import-side use with context arguments. Temporary scene state is not a saved fixture.

Capture the user’s open-scene setup/dirty flags/selection before import. Never clear dirty state, reset overrides, switch scenes, save a production scene or write to actor art. Stop any owned Studio preview cleanly before import and release it through its established owner; don’t bypass preview ownership. On success/cancellation/failure, destroy temporary actors/scenes and leave the original editing context intact.

### 4. Commit only owned equipment assets in main

Compute meshes/material changes/footwear/package data in temporary owned objects, then commit into the recipe’s existing owned destination. Keep existing GUIDs on reimport; merge source materials against stored baselines, retain edited opacity/roughness/maps, footwear height/fit and layer assignments. Snapshot/restore only the importer-owned asset changes if commit fails. New files are removed on rollback; pre-existing files and metadata are restored without new GUIDs.

Keep source/recipe/calibration/override hash checks and immutable release protection. Defer candidate postprocessing until the package commit is complete so delayCall cannot synchronize a half-written package. Reports go outside Assets in the main project. No global SaveAssets side effect may serialize unrelated dirty artist assets; save the explicit owned asset set. Do not relocate canonical/source/material paths or modify existing releases.

The existing saved Clothing Setup is the review surface. Importing a new outfit does not create Review.unity or any per-outfit test scene. Keep existing historical review scenes unchanged; retain their current metadata for browsing old evidence.

### 5. Register new packages in existing Clothing Setup

Add a generic package registration path instead of invoking the three-package migration builder. Create/update the preset by stable source package identity; reject ambiguous IDs and don’t infer artistic layer choices. Populate the existing mutable candidate/draft, retain variants and reimport mappings, then use existing layer assignment, fit compilation, rig asset building and immutable generation publication.

For a released selection, fork through the existing candidate workflow; never edit the release. New outfit and hair assets become available in the existing Studio selectors after explicit publication. Don’t add another fixture/catalog/import pipeline or silently change First Contact’s bound generation.

### 6. Verify, then retire P02 and its orchestration

Pure managed checks: recipe/path validation, source/material baseline merge, GUID/reimport identity preservation, cancellation rollback, stale source detection and package registration. Reuse existing coverage; add tests only for newly extracted behavior.

Art’s main-project observable: with FirstContact open and an unsaved mark edit, import one actual outfit from DUF+FBX through the new command; the original scene remains open/dirty and its file unchanged. In the existing Clothing Setup select the imported outfit, assign layers/hair, compile/publish a new generation, then select it through Studio. Reimport after an intentional material/footwear edit and verify values/GUIDs/layers survive. Cancel and force one controlled failure to verify cleanup/rollback. No extra disposable scene or clothing acceptance matrix.

Record technical evidence separately from Art’s observations. When this works with P02 renamed/unavailable, remove the clone, hardcoded path, source-sync/Publish-copy branches and any staging-only callers (including the historical run-wardrobe-execution path). Update quickstart/playbook/current status. Search active code/config for P02 or fallback clone dependencies; archived evidence may name the old path. No future “missing sandbox” repair is allowed to recreate it.

## Scope and completion

This plan does not change FirstContact, clothing art, bindings/index, generations or rig calibration. It does not reopen accepted single-Lara behavior or the waived second-Lara test. It needs no extra Unity package and no new Unity project.

Completion means a normal new outfit can be prepared, imported and reviewed/published in the main project with P02 absent, while preserving artist edits and immutable releases. Source presence alone is not that outcome. T01 delivered this analysis and retained P02 intentionally; implementing the migration is the next bounded follow-up to approve, not a claim that the dependency is already gone.
