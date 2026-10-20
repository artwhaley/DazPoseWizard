# C10 — Final QA, delivery audit, and human manual acceptance handoff

**Dependency:** C00–C09. Do not claim Play/visual success.

## Automated/non-Play gates

1. Managed story tests: `dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj` and `dotnet test tests/DazPose.Tests/DazPose.Tests.csproj` (adjust only documented restore path if environment requires). Include added global-card catalog/linker tests.
2. Unity Editor **compilation and EditMode** checks on installed Unity `6000.5.9f1` without `-runTests` tests that start gameplay or loading standalone player. The user is the tester. Capture the actual Unity log and results; report any missing environment instead of inventing passes.
3. GUID duplicate/missing audit, `.meta` preservation for every moved retained asset, original-vs-final critical component references and transform data, prefab variants, artist tuning and visibility/material settings. Require exact bytes for frozen scenes/releases; for editable data record the authorized serialized field/path changes or rollback.
4. Source scan for retired scene paths/old menu commands/migration hooks. Separate historical docs (allowed) from executable code/config/current author docs (must be fixed).
5. `SceneCatalog.asset`, Editor Build Settings and computed `GamePlayerBuild` scene membership consistency; no example/test scenes exposed as public app choices.
6. First Contact `.dazstory`: combined linked content retains the original 19 fixed dialogue transcripts/clip IDs; full Intro and phases; no repurposed `submissionScore`; six imported poses; Disapproval gesture; all seven named semantic locations; first-contact source-to-binding diagnostics clean; motion files and GlowyWireframe/Regular profiles bound; normal prepared male model/contact preserved; no preview cutoff. **No Play**.
7. Director global pool G1–G7 passes. New tag-bearing card can join pre-existing scene without editing its source or scene asset. Removed card stops appearing after index refresh. No per-scene card library references anywhere.
8. First Contact visual art, clothing setups and frozen artistic source file hashes/author settings compared with baseline; any difference must be explained and approved when aesthetic.
9. Protected `RemediationToeValidation` pending human observation retained; 77.8 GiB `.dazposewizard` working data not bulk-deleted.
10. `git status`, `git diff --stat`, tracked/untracked changes, `git diff --check`, commit list, retained/retired scene counts, bytes saved **only from actually verified deletions**, blocked manifest, generated evidence paths, and recovery commit/ref.

## Manual Unity checklist for the user (prepare, DO NOT perform)

- Open actual saved MainMenu and inspect scene choice navigation; in a *later user-controlled Play* verify First Contact starts through the normal menu.
- Open actual saved FirstContact scene and inspect untouched Void art, Lara/materials/poses, prepared male model/contact setup, mark positions, sound and prompts. During user Play check full scene from Intro through Resolution and debug F8; compare with D8 oracle.
- Open ClothingSetup and inspect draft/release configurations, outfit preview, published review pointers and saved modifications.
- Open the maintained Showcase example and ensure it shows supported commands without old fixture hierarchies.
- Open frozen original FirstPerformanceVoid and compare its artistic settings without overwriting it.
- Separately inspect/approve the pending toe-appearance fixture; cleanup does not count as that approval.

## Reporting

Populate `templates/EXECUTION_REPORT.md` as an actual final report in `docs/cleanup-task1/EXECUTION_REPORT.md`, with SHA, commit IDs, passes/failures, blocked moves/deletions, exact paths and user's manual steps. End with a single concise human handoff and **no claim of human visual approval**.

## Gate E — definition of done

Production Editor no longer depends on validation/example assemblies; historical auto migrations no longer fire; supported Clothing Setup and menu paths are coherent; selectable card pool is globally tag-discovered; surviving scenes and app catalog are clearly separated; verified disposable material is retired with manifest; production-owned assets moved safely or blocked with evidence; full First Contact remains intact; current docs reflect actual delivered paths; all required technical gates pass for the declared delivered scope. Required BLOCKED/NOT RUN checks make the technical result PARTIAL; baseline failures must be distinguished, not relabeled as success. Intentional protected HOLDs and human manual acceptance are reported separately.

## Required final review additions

- Check retained frozen/reference scenes for script and subasset resolution, including FirstContactPerformance; production assembly independence is not permission to break them.
- Verify import failure suppression and archive command/recovery preservation; no restored failed jobs or unwanted automatic retries.
- Verify review metadata producers/readers and legacy fallback, saved Clothing Setup startup ownership, and ordinary opens without artistic retuning or report-driven selection changes.
- Check no stale historical builder/menu/CLI path can recreate a retired fixture. Record retained compatibility/reference code with its real consumers instead of calling it an orphan.
- Recheck C05 through the compiled StoryProgramSequence/StoryRunner host adapters and Inspector diagnostics, not only a provider unit test. Technical tests use fake-host or EditMode execution; actual Unity Play remains MANUAL PENDING.
- Freeze the final move/retirement manifest, audit all still-required paths and regenerate this packet's FILE_MANIFEST if the packet is revised. Exclude pre-existing changes from cleanup attribution; never stage the user's unrelated work as part of the final commit.

Do not call the project ready for manual testing if it has new compiler errors, broken saved references or an unverified required compile gate. State the exact limitation and remaining work. This review does not assert that any cleanup ticket has been executed.
