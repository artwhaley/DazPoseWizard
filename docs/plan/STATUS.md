# Architecture sprint status

Updated October 21, 2026. Current ticket: **T01 â€” Workspace reset**. Approved local cleanup and P02 retirement/source preparation are delivered; **Artâ€™s native acceptance remains pending**. This is the single active status record; archived campaign progress is historical evidence.

## Delivered

- Exact architecture plan published unchanged at [ARCHITECTURE_SPRINT_PLAN.md](ARCHITECTURE_SPRINT_PLAN.md), local commit `e050848`.
- Approved decisions executed: P01 native-directing clone, P03 production-consolidation clone, P04 remediation clone and P05 SubtranceB retired. Empty root unity container removed. L06 pose input folder, L07 exports, L08 published converter binary and all B managed outputs deleted. Logical bytes represented by retired paths in the inventory: **75,298,171,867 bytes (70.127 GiB)**; this is not an allocated-disk-space measurement. Required checks subsequently recreated ignored bin/obj/TestResults outputs.
- P06 main project retained. Every primary-project scene and all five third-party dependency groups retained; all other D evidence/recovery kept (D17/D21 duplicate the explicitly retired P03/P04 paths). A01 deletion superseded by Artâ€™s later keep-all-scenes instruction, so the remaining GateB scene is retained at its existing path. It is not an active acceptance requirement.
- Player code and its root/descendant metas moved from Assets/Player to **Assets/DazPose/Player**; linked managed source and staging path consumers updated. Empty Content/meta subtree removed. No scene contents, production art, bindings, global index or released generation edited.
- Historical root docs, campaign folders, ticket packets, root TestOutput evidence and native fixture script moved under docs/archive; its README identifies each group. Existing user ticket moves were preserved and reconciled into the approved archive. AGENTS rewritten as one current 68-line document; old AGENTS archived verbatim. README and current authoring/wardrobe/runtime references corrected; live Help/reference/fixture paths updated.
- **P02 retired; main-editor conversion hookup deferred.** Art authorized migration, then clarified that the temporary proof-of-concept should not be perfected and that its actual main-only conversion integration may be deferred. The existing conversion C# algorithms are untouched. The active wrapper now supports main-project Preflight/Inspect/Prepare/Status only; Import/All/Validate/Assets/Publish/ReimportTest clearly report DEFERRED, with no Unity launch or fallback clone. The old execution runner clearly reports retirement.
- [P02 evidence/procedure archive](../archive/p02-import-evidence/README.md) preserves exact original wrappers/helpers/recipes/manuals and clone-specific source/reports. The full clone comparison checked 12,297 files, found 7,427 byte-identical at the same main paths, and retained 1,550 differing/missing-path source/data/evidence files (3,127,228,779 bytes, about 2.91 GiB) under ignored `.dazposewizard/p02-retirement-recovery/`, with verified hashes. Derived player builds and Library/cache outputs were discarded. No proprietary mesh/texture data is newly committed. No clone code/art was copied into main.

## Local commits and working tree

- `e050848` — unchanged architecture plan.
- `f7aa2e4` — historical archive, current guidance and P02 analysis/plan.
- `1ba9232` — GUID-preserving Player organization and approved path retirements.
- All commits remain local and unpushed. Only the pre-existing untracked `Assets/__WardrobeProtectedCards` scene/metas remain outside Git; they were explicitly retained. Ignored local art/recovery and rebuilt check outputs remain local; the P02 project itself is retired.
- Automatic approval review initially rejected staging the pre-existing ticket moves. Art then explicitly authorized the three named native/visual/wardrobe packet relocations and their 54 old-path deletions; exact-path staging succeeded. No blocked commit remains.

## Technical evidence

[Approved manifest](../workspace/CLEANUP_MANIFEST.md), [decisions](../workspace/T01_DECISIONS.json), [retirements](../workspace/T01_RETIREMENTS.json), [moves](../workspace/T01_MOVES.json), [baseline](../workspace/T01_BASELINE.json), [checks](../workspace/T01_CHECKS.json), [managed results](../workspace/T01_MANAGED_RESULTS.json).

- Managed solution build: passed, 0 warnings / 0 errors. It includes only the converter suite; all five test projects were also run explicitly.
- Managed tests: **323 passed, 1 skipped, 0 failed**, 324 total. Player 24; Rehearsal 98; Story 108; converter 68 passed / 1 skipped; Wardrobe 25. The skipped licensed Vintage Glamour test formerly used the retired stack3 poses input. It now looks in retained fixtures/private and reports a skip if absent. No proprietary fixture recreated. Two suite compilations report the existing CS0649 StoryCatalogs.Header.version warning.
- NuGet restore initially hit sandbox profile permissions even with explicit config. Temporary APPDATA/DOTNET_CLI_HOME and the populated offline package cache resolved it. Temporary config stayed outside Git; no app was launched.
- All seven Unity-source assemblies compiled through scripts/check-unity-source.py using existing reference inputs; no Unity executable, batch mode, Play or native test was run.
- 689 protected/moved-input hashes match baseline. All 16 scenes unchanged. 4,740 remaining GUIDs, no duplicates or changed GUIDs. Only the four approved empty Content metas were removed. Asset/meta pair audit is clean.
- Original plan SHA-256 remains `6c448dbc7d82ca58a7fbf5f394ee23d146e9f2f3df04fc3966e1212a05a2dc8b`; archived AGENTS matches its original content with existing Git newline normalization accounted for.

## Artâ€™s acceptance â€” not run by the executor

Art confirmed Unity was closed for the filesystem changes. No post-cleanup Unity/visual result has been reported yet. Please perform T01 C2:

1. Open the main project in Unity and let it import.
2. Open Assets/Scenes/FirstContact.unity; confirm no new missing-script/reference errors, then Play the intro and compare with previous behavior.
3. Open Assets/Scenes/ClothingSetup.unity and Assets/Production/Scenes/MainMenu.unity; confirm no new errors.
4. Open DazPose > Story > Scene Director Studio on First Contact and confirm cue projection.

Record Artâ€™s observations here. Practical earlier Studio and single-Lara wardrobe acceptance remain historical; unrun extended/native integration checks are not claimed passed by cleanup.

## P02 revised-scope verification

[Retirement/check record](../workspace/P02_RETIREMENT.json) and [retention index](../archive/p02-import-evidence/RETENTION.json). Ten existing Python geometry/calibration/source-contract tests pass. Both active PowerShell wrappers parse; Status resolves main with P02 absent; all six legacy conversion actions and the retired runner clearly refuse unsupported work. No active script/config references the retired clone. The 689 protected input hashes still match and tracked Unity Assets/C# has no changes in this follow-up. No Unity or asset conversion/rebake was run; prior T01 managed/source build evidence above was not rerun for this script/docs-only change.

## S01 complete — accepted October 21, 2026

Art accepted S01's recommendation on **October 21, 2026**. Agents may run headless EditMode tests and read-only probes in the single persistent sandbox at `C:\Users\artwh\DazPoseWizard-Sandbox` (50 GiB cap), using `scripts/sandbox-sync.ps1`. Art also authorized headless sandbox editor-command dry runs: show a text diff of every changed asset after each run, restore sandbox assets to match the main working copy, and confirm parity. Main-project Unity operations and sandbox asset transfers into main remain prohibited; Art runs main editor commands and owns visual acceptance. The active boundary is recorded in `AGENTS.md`.

[S01 report and native evidence](S01_UNITY_SPIKE.md): five unchanged runs passed 9/9 at about 12 seconds each; a deliberately broken assertion failed with exit 2 and the restored suite passed; the FirstContact read-only probe resolved all seven locations with no binding issues. Final sandbox usage was 15.354 GiB; all 689 protected hashes matched. HDRP/VFX warnings remain, and no visual acceptance is claimed. S01 implementation/evidence commits: `9051088`, `5604610`. S02 has not run and must reuse this sandbox.

## Next bounded work

Art checks that the main project still opens and existing First Contact/Clothing Setup/MainMenu/Studio behavior is intact. Full new-outfit import integration is deferred; the [design](P02_MAIN_PROJECT_IMPORT_PLAN.md) preserves the remaining scene-context/asset-preservation/new-package registration work for a later bounded task. Do not recreate a clone or claim preparation imported/published an outfit. T02 onward and spike S02 were not executed; S01 is complete and its rule accepted as recorded above. SuccubusPerformer/StoryRunner/binding refactors remain out of scope.
