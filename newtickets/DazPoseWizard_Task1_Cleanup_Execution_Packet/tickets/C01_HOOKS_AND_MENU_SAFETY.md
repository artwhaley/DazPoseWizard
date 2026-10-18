# C01 — Retire unintended historical Editor mutation hooks

**Dependency:** C00. Goal: reopening Unity or a production scene must not trigger old validation builders/migrations.

## Targets and discovery

Inspect `Assets/FirstPerformanceVoid/Editor/FirstPerformanceVoidLighting.cs`, `SmokeFrontSettingsCopy.cs`, `Assets/DazPose/Editor/LaraFirstOutfitBuilder.cs`, `LaraSecondOutfitRepair.cs`, partner `.once` request handlers, and all `InitializeOnLoad`, `InitializeOnLoadMethod`, `DidReloadScripts`, `EditorApplication.delayCall`, `EditorSceneManager.sceneOpened`, `AssetPostprocessor`, `OnPostprocessAllAssets`, and automatic `AssetDatabase.Refresh` triggers in this project. Do not assume these are the only hooks.

## Implementation

1. Classify every auto-run hook: **supported current import synchronization**, **one-shot historical migration**, **editor UI registration**, or **test-only**. Save this table in `docs/cleanup-task1/hook-audit.md` with source path and method.
2. Remove automatic activation of historical scene/lighting/smoke/outfit/partner migrations. If a method still supplies live importer functionality, move only its reusable logic behind an explicit supported tool/independent shared service; avoid deleting needed processing.
3. Ensure supported *normal* pose reconciliation, wardrobe synchronization and persistence still function. A needed fresh-import hook may stay if scoped by current content changes, not every scene open/reload.
4. Ensure neither opening an ordinary scene nor reloading scripts can silently retune, overwrite, reimport or copy from protected art/materials or change user's wardrobe layer decisions.
5. Keep accepted source file `.meta`/GUID references and license content; removal of an old hook is not authorization to delete the builder's useful helpers.

## Tests

- EditMode probe for new/old hooks: import/reload/open main menu, First Contact, Void template and Clothing Setup without asset dirties to protected assets; hash/scene dirtiness before/after. Running scene Play is forbidden.
- Supported importer reconciliation still processes changed temporary input and skips unchanged successful input; automatic reconciliation also skips unchanged failed input, while explicit retry intentionally retries failures.
- No hidden historical migration runs on domain reload, open scene or asset refresh; no unexpected rebuilding of accepted outfits.

## Gate B1

Pass only if auto hooks are classified and unwanted ones deactivated with existing valid import behavior retained. Include before/after hook table and dirty-asset report. Any hook relying on user-unapproved data is blocked rather than invoked.

## Current import and observer exceptions

Keep `DazPoseBrowserImportProcessor`'s live scoped importer, including startup/reference-model `Reconcile(false, false)` and the content-hash guard that skips unchanged failed work. Only the explicit reconcile command opts into retry. Add a temporary failed-input regression proving focus/reload/reference-model notification does not retry an unchanged failure, changed input is processed, and explicit retry remains available. Do not use the archived user's inputs as fixtures.

Audit application-level reload observers as well as historical material hooks: `AuthoringBatch`, `AuthoringNavigationCheck`, `AuthoringPointerCheck`, `AutomaticTuningCheck`, `StoryCheckpointPlaybackChecks`, and `StoryFullSessionChecks` register callbacks through SessionState/InitializeOnLoad. Move retained test observers behind test ownership and explicit invocation; stale state must not start/resume playback, restore tuning, save scenes or write profiles on ordinary reload. Do not remove normal UI/import/authoring registrations indiscriminately.

Inspect and disable unwanted mutation hooks before exercising scene-open checks. Capture all loaded scene dirty states and on-disk hashes; never save the user's existing unsaved work to make a check pass. Test mutating callbacks with temporary assets or an isolated copy, and report that scope honestly.
