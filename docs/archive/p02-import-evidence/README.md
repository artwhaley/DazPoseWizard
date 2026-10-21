# Retired P02 wardrobe import proof of concept

Preserved October 21, 2026. Art authorized retiring the cloned Unity project and then clarified that completing the main-editor conversion hookup may be deferred. The known import techniques, accepted calibration and preservation rules remain available here; they are not a supported command workflow to run against the main authoring project.

- `workflow-source/scripts/`: exact pre-retirement PowerShell orchestration, Python preparation, material/FBX helpers, source-copy repair helper and morph-export CSVs. These are historical source references; relative repository paths reflect their original locations. Do not run these archived scripts or recreate the project clone.
- `workflow-docs/`: the full previous quickstart, playbook and status, including original source-contract decisions, user approvals, toe/heel geometry lessons, material baseline merge and unresolved trials.
- `recipes/`: snapshot of the existing recipe/calibration JSON files. Active recipes remain under repository configs/wardrobe.
- `legacy-source/`: clone-specific versions of first-party C#/assembly files that differ from main or are absent there. Keep main authoritative; these are not files to copy back wholesale.
- `reports/`: clone-specific wardrobe import/source/reimport reports and logs.
- [RETENTION.json](RETENTION.json): every differing/missing-path clone file preserved locally, with hashes and destinations; files byte-identical to the main project were not duplicated.

Nonidentical clone Assets/Packages/ProjectSettings/reports/logs/user settings are preserved in the ignored repository folder `.dazposewizard/p02-retirement-recovery/`. Generated meshes, textures, imported proprietary data and old scene bytes stay local; they are not newly committed as documentation. Library/cache/build outputs are not retained. The source clone `.dazposewizard/p0c-native-generation` is retired.

Main still contains WardrobeImporter, WardrobeImportRecipe, WardrobeGeometry, material baseline merge, footwear preparation, catalog/fit/generation tools and the DAZ conversion adapter. No Unity C# source or existing asset was changed in this retirement. Existing Clothing Setup and published outfits remain available.

Current `scripts/import-lara-outfit.ps1` supports Preflight/Inspect/Prepare/Status against the main project without Unity. Conversion/validation/publication actions stop with an explicit deferred message. Preparation does not mean an outfit was converted, imported or published. The old execution runner is a clear retirement message.

See [the migration design](../../plan/P02_MAIN_PROJECT_IMPORT_PLAN.md) and [active status](../../plan/STATUS.md) for future work. Main-editor import context, owned-asset rollback and new-package candidate registration remain deferred; no per-outfit fixture or cloned Unity project is the intended replacement.
