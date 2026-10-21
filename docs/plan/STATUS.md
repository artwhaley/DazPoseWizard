# Architecture sprint status

Updated October 21, 2026. Current ticket: **T01 â€” Workspace reset**. Approved local cleanup and P02 analysis are delivered; **Artâ€™s native acceptance remains pending**. This is the single active status record; archived campaign progress is historical evidence.

## Delivered

- Exact architecture plan published unchanged at [ARCHITECTURE_SPRINT_PLAN.md](ARCHITECTURE_SPRINT_PLAN.md), local commit `e050848`.
- Approved decisions executed: P01 native-directing clone, P03 production-consolidation clone, P04 remediation clone and P05 SubtranceB retired. Empty root unity container removed. L06 pose input folder, L07 exports, L08 published converter binary and all B managed outputs deleted. Logical bytes represented by retired paths in the inventory: **75,298,171,867 bytes (70.127 GiB)**; this is not an allocated-disk-space measurement. Required checks subsequently recreated ignored bin/obj/TestResults outputs.
- P06 main project retained. Every primary-project scene and all five third-party dependency groups retained; all other D evidence/recovery kept (D17/D21 duplicate the explicitly retired P03/P04 paths). A01 deletion superseded by Artâ€™s later keep-all-scenes instruction, so the remaining GateB scene is retained at its existing path. It is not an active acceptance requirement.
- Player code and its root/descendant metas moved from Assets/Player to **Assets/DazPose/Player**; linked managed source and staging path consumers updated. Empty Content/meta subtree removed. No scene contents, production art, bindings, global index or released generation edited.
- Historical root docs, campaign folders, ticket packets, root TestOutput evidence and native fixture script moved under docs/archive; its README identifies each group. Existing user ticket moves were preserved and reconciled into the approved archive. AGENTS rewritten as one current 68-line document; old AGENTS archived verbatim. README and current authoring/wardrobe/runtime references corrected; live Help/reference/fixture paths updated.
- P02 source/data analysis and [main-project migration plan](P02_MAIN_PROJECT_IMPORT_PLAN.md) delivered. Its importer engine already exists in main. The real work is removing batch/scene-replacement orchestration and adding safe equipment-asset commit/new-package registration in the existing Clothing Setup. **Migration is not implemented; P02 remains temporarily**, with no code/art copied back.

## Local commits and working tree

- `e050848` — unchanged architecture plan.
- `f7aa2e4` — historical archive, current guidance and P02 analysis/plan.
- `1ba9232` — GUID-preserving Player organization and approved path retirements.
- All commits remain local and unpushed. Only the pre-existing untracked `Assets/__WardrobeProtectedCards` scene/metas remain outside Git; they were explicitly retained. Ignored local art/recovery/P02 and rebuilt check outputs remain local.
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

## Next bounded work

Review/execute the requested P02 migration as its own implementation follow-up before deleting that final clone. No hidden project or fallback clone should be required for routine outfit import. T02 onward and spike S01 were not executed. SuccubusPerformer/StoryRunner/binding refactors remain out of scope.
