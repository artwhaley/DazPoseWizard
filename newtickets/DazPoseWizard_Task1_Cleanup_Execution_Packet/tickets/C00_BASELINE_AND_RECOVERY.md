# C00 — Baseline, dependency inventory, and recoverable working state

**First implementation ticket, after explicit implementation authorization. No source/asset cleanup before its applicable recovery gate passes. This does not prevent the requested documentation-only packet review.**

## Inputs

`AGENTS.md`, repository-root `UNITY_PROJECT_CLEANUP_PLAN.md`, `docs/story-runtime/D6_D8_HANDOFF.md`, `docs/story-runtime/FIRST_CONTACT_D8_ORACLE.md`, repo `.gitignore`, current Unity ProjectSettings, scene catalog and active .dazstory.

## Actions

1. Record `git rev-parse HEAD`, `git status --porcelain=v1`, branch, Unity version, .NET SDK, installed test tools and all project roots. Record pre-existing deletions/renames as well as modified files; the reviewed checkout includes a D1-D5 → oldtickets reorganization and expression-import cleanup that must not be reverted or absorbed. Inspected HEAD was `bc596962aff6206fb54cc189de42defd270ddeda`; if newer, inspect diff and update report. Preserve dirty inputs without stashing/discarding undocumented work.
2. Stay on existing `main`; do not create a branch or require a clean checkout by discarding/stashing user work. Record HEAD as the committed recovery anchor and capture dirty/untracked deltas separately. Capture a *recoverable* snapshot for critical tracked and ignored inputs: the seven KEEP/HOLD scenes, all their `.meta` files, First Contact program/bindings/audio/marks, Lara/player original rig and supporting materials, accepted wardrobe drafts/releases, published review pointer sources, relevant project configs. A Git ref preserves only committed data: also snapshot current dirty tracked files and untracked inputs with hashes and original paths. Ignored or external data must receive a physical or verified existing-recovery snapshot **with absolute location and hashes**, not a claim that Git preserves them.
3. Inventory GUIDs in live `.meta`; detect duplicates and blank GUIDs. Record active scene and prefab dependency closure using Unity Editor `AssetDatabase.GetDependencies(..., true)` where available; supplement with serialized `guid:` scanning. Do not use raw text GUID scan as the only dependency check.
4. Inventory path-based/nonserialized references across C#, `.json`, `.dazstory`, `.asset`, YAML, manifests, editor builder constants and importer recipes. Explicitly check `DazStoryImporter` companion-by-filename, `StoryFoundationBuilder.ContactStaging()` Showcase dependency, `ClothingSetupBuilder` Day 2 dependency, `WardrobeImportReviewWindow` → `TestOutput` pointer, `SceneCatalog`/`GamePlayerBuild`, and DAZ/import configured roots.
5. Snapshot current scene catalog entries and Editor Build Settings, 19 First Contact dialogue IDs/transcripts, 18 currently compiled cards, six pose aliases+postures, critical transform positions/rotations and GUIDs, companion binding references, equipped wardrobe layer assignments and male contact references. Do not attempt reimport/rebuild merely to generate a snapshot.
6. Run existing non-play tests if supported (`dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj`, `dotnet test tests/DazPose.Tests/DazPose.Tests.csproj`), Unity compile/EditMode checks only if permitted in the environment. Record command, tool version, result and existing failures. No player launches.
7. Write `docs/cleanup-task1/baseline.json`, `dependency-inventory.md`, `protected-manifest.json`, `scene-manifest.md`, `path-consumers.md`; tracked reports must not contain personal paths/secrets. Keep absolute recovery locations and any local environment details in a private ignored recovery manifest, referenced by snapshot ID and hashes in the tracked report. Verify that local manifest and its recoverable payload exist. Include Git ignored/untracked input notes.

## Gate A

- Snapshot and recovery locations are real/verified, especially for ignored source data.
- All protected names/paths/GUIDs and original 19 dialogue entries captured.
- Current baseline failures distinguished from cleanup regressions.
- The edit plan lists dependencies and blocked moves rather than inferring safety from filenames.

If the needed ignored inputs cannot be recovered, mark the corresponding relocation/deletion blocked; the rest may proceed without discarding those inputs.

**Commit/report:** `C00 baseline manifest` (only manifests/docs, no moved assets).

## Inventory coverage required by review

- Treat every KEEP/HOLD/reference scene, reusable supported content and preparation/import input as a dependency root, including ignored published review scenes. Inspect incoming references as well as outgoing Unity dependency closures. A noncatalogued scene is still a consumer.
- Scan repository `src`, `tests`, `scripts`, root configuration/docs, Unity `Assets`, `ProjectSettings` and relevant `Packages`, plus configured ignored import recipes/manifests and local review pointers. `rg` normally skips ignored files: explicitly enumerate the bounded ignored roots recorded in this manifest.
- Record script GUID/type/assembly identity, subasset fileIDs, FBX importer/external-object mappings, folder metas, scene/actor persistence IDs, Resources/StreamingAssets and other configured automatic inclusion/load routes. Do not treat build-scene exclusion as proof assets are absent from a player.
- Read `docs/IMPORT_FAILURE_CLEANUP.md`; preserve `.dazposewizard/archived-expression-imports/` recovery manifests and payloads. Do not count already archived/deleted expression inputs as new cleanup savings.
- Capture the pure managed fixed-seed 82017 card selection/progress trace and declaration order before C05. Record the baseline's 18 declarations (17 tagged plus local Intro) and combined 19 dialogue entries, not an assumption that every future file has those counts.
- Inventory any currently dirty/open Unity scenes without saving them. Before executing an existing check, inspect its transitive calls for asset writes, scene saving, Play/build/launch and profile/toy effects (see VALIDATION_AND_HANDOFF.md).

C00 may PASS for the safe scope while individual unbacked candidate operations remain BLOCKED; protected HOLDs are intentional. No destructive operation may bypass its own recovery gate.
