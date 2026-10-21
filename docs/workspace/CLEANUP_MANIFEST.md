# T01 workspace cleanup manifest — approved cleanup executed

Inventory October 20, 2026; Art’s decisions and Unity-closed confirmation received October 21. Inventory HEAD was `2dee09b8302091480c59aaa6887246e7fe601d3e`. Approved archive/move/deletion actions are executed; see [current status](../plan/STATUS.md), [retirement log](T01_RETIREMENTS.json), [move log](T01_MOVES.json), and [checks](T01_CHECKS.json). Native checks remain Art-operated and pending.

Art retired P01/P03/P04/P05, their now-unused unity container, L06/L07/L08 and every B output; all other D evidence and all primary-project scenes/vendor dependencies remain. P02 is deliberately retained pending its requested [migration plan](../plan/P02_MAIN_PROJECT_IMPORT_PLAN.md), not as the desired ongoing import architecture. A01 was changed to keep because Art’s final instruction was “Keep all scenes.” X04 remains until P02 migration. `??` means untracked and `!!` ignored, neither disposable; no blanket Git-status cleanup occurred.

Tables below incorporate the final approved actions. Inventory sizes/evidence remain historical observations; the original Phase A approval instructions and proposed disk totals below describe preparation, not current pending approvals. Phase B has now been executed.
## Proposed folder rule

- `Assets/DazPose/`: first-party runtime/editor/validation code; preserve assembly boundaries.
- `Assets/Production/`: production performer/player/wardrobe/story/environment/motion content.
- `Assets/Scenes/`: new user-facing scenes. First Contact remains `Assets/Scenes/FirstContact.unity`.
- Vendor packs stay at vendor paths. Unity special folders (`Resources`, `Plugins`, `Gizmos`, render `Settings`, TMP) keep their loading/import semantics.
- Protected/importer-controlled roots remain explicit exceptions. Never move Lara source/imported art, immutable releases, toe fixtures or protected-manifest entries merely to enforce the rule. MainMenu remains a protected existing exception at `Assets/Production/Scenes/MainMenu.unity`. Mixed `FirstPerformanceVoid` remains a legacy exception until individually reviewed.

## Approval rule

Mark each decision ID **approved**, **changed** (give replacement action), or **rejected**. Approval of proposed actions leaves **ask Art** rows unresolved until an explicit choice. Appendix observations add no mutations; parent/subtree rows overlap. Art must confirm Unity closed before approved `Assets/` changes. Moves carry asset/folder metas. Preserve scene contents/GUIDs, art, bound stories, global index, releases and recovery baseline. No regeneration, reimport or rebake. Local commits only, after approval; no push/PR/merge.

D17 and D21 duplicate the exact P03/P04 project paths; their explicit P03/P04 retirements control those same physical directories. This does not retire the separate recovery/evidence directories.

## A1 — whole workspace

Physical traversal: **373,354 files**, excluding `.git`; no traversal errors, symlinks or junctions. Sizes are logical file bytes, not guaranteed allocated/free space (OneDrive/compression/allocation can differ). Every project identified by `ProjectSettings/ProjectVersion.txt` inside the repository follows; no external directories were searched.

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| P01 | `.dazposewizard/native-directing/G1Project` | 15.250 GiB | Created by scripts/native-directing-fixture.py:9,50–90; docs/native-directing/N00_BASELINE.md:34–36 and visual-authoring/G4_CODE_SYNC.json identify the independent fixture. PRODUCTION_HANDOFF.md:23 says staging edits may exist. | delete | High: retire after deciding whether any fixture-only art/staging needs preservation. |
| P02 | `.dazposewizard/p0c-native-generation` | 25.201 GiB | Still required by scripts/import-lara-outfit.ps1:12–18 and run-wardrobe-execution.ps1:12–16; also referenced by wardrobe quickstart. | keep | Temporary retention for analysis/migration only; see docs/plan/P02_MAIN_PROJECT_IMPORT_PLAN.md. No importer implementation merged from clone. |
| P03 | `.dazposewizard/production-consolidation` | 26.363 GiB | docs/production-consolidation/DAY_2.md:15 and DAY_2_REPORT.md:18 identify fresh main-project copy; validation/README.md:5 still names it. | delete | High: apparent retired clone; clone-only art/evidence uniqueness unproved. |
| P04 | `.dazposewizard/remediation-validation` | 22.459 GiB | docs/production-consolidation/remediation/R01_STAGE_AND_RUNNER.md:7 defines remediation copy. | delete | High: apparent retired clone; decide retention of clone-only art/evidence. |
| P05 | `unity/SubtranceB` | 3.287 GiB | UNITY_PROJECT_CLEANUP_PLAN.md:91 identifies separate ignored project with OutdoorsScene; creator/ownership not established. | delete | High: not shown disposable. |
| P06 | `validation/DazPoseUnityValidation` | 18.269 GiB | Authoritative source/assets and two enabled Build Settings scenes. | keep | Protected |

The “Synchronized 1132 code/meta files to the existing independent fixture” statement means **P01**, not a seventh project. `native-directing-fixture.py` creates only that fixed destination. P02/P03/P04 were separate staging campaigns. Do not delete `.dazposewizard/` wholesale: it also holds the protected ignored-input recovery snapshot.

### Top-level local inputs and outputs

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| L01 | `.dazposewizard` | 93.145 GiB | Ignored clones, recovery and diagnostics; child decisions below. | keep | Never recursively delete this container. |
| L02 | `unity` | 3.287 GiB | Ignored container holding P05. | delete | P05 unresolved |
| L03 | `G8F-Base` | 588.753 MiB | Ignored proprietary converter reference fixture; README names it. | keep | User input |
| L04 | `G8F-inCherishReferencePose` | 75.553 MiB | Ignored proprietary pose/reference fixture; README names it. | keep | User input |
| L05 | `fixtures` | 3.546 MiB | Private DSF/DUF source inputs plus tracked tests. | keep | User input |
| L06 | `stack3 poses` | 1.464 MiB | Ignored DAZ pose inputs; ownership/retirement not established. | delete | Do not infer disposable. |
| L07 | `output` | 1.914 MiB | Ignored exported converter JSON/BVH/reports/CSV. | delete | Decide whether exports/evidence are needed. |
| L08 | `artifacts/win-x64` | 206.130 MiB | Ignored published converter binaries; publish script and source retained. | delete | Removes runnable distribution until later authorized publish. |
| L09 | `TestOutput` | 15.702 MiB | Tracked diagnostic evidence, not runtime assets. | archive to `docs/archive/TestOutput` | Preserve evidence, update live links. |
| L10 | `.codex-remote-attachments` | 0.099 MiB | Existing attachment inputs; prior instruction forbids indiscriminate staging. | keep | User inputs |
| L11 | `.freebuff` | 37 B | Small tool configuration; provenance unestablished. | keep | No ownership assumption |
| L12 | `src` | 1.364 GiB | Live converter/core, child output proposals follow. | keep | Live source |
| L13 | `tests` | 1.200 GiB | Five live managed test projects. | keep | Live tests |
| B01 | `src/DazPose.App/bin` | 1.361 GiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B02 | `src/DazPose.App/obj` | 2.215 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B03 | `src/DazPose.Core/bin` | 0.221 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B04 | `src/DazPose.Core/obj` | 0.354 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B05 | `tests/DazPose.Player.Tests/bin` | 5.142 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B06 | `tests/DazPose.Player.Tests/obj` | 0.246 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B07 | `tests/DazPose.Rehearsal.Tests/bin` | 5.497 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B08 | `tests/DazPose.Rehearsal.Tests/obj` | 0.692 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B09 | `tests/DazPose.Story.Tests/TestResults` | 0.068 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B10 | `tests/DazPose.Story.Tests/bin` | 5.420 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B11 | `tests/DazPose.Story.Tests/obj` | 0.604 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B12 | `tests/DazPose.Tests/TestResults` | 0.094 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B13 | `tests/DazPose.Tests/bin` | 1.177 GiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B14 | `tests/DazPose.Tests/obj` | 0.817 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B15 | `tests/DazPose.Wardrobe.Tests/bin` | 5.173 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |
| B16 | `tests/DazPose.Wardrobe.Tests/obj` | 0.267 MiB | Ignored generated managed build/test outputs; source and package cache retained. | delete | Future managed builds/tests recreate outputs. |

### Other immediate `.dazposewizard/` directories

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| D01 | `.dazposewizard/appdata` | 181 B | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D02 | `.dazposewizard/authoring-evidence` | 9.542 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D03 | `.dazposewizard/cleanup-task1` | 91.707 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D04 | `.dazposewizard/d1-d5-evidence` | 2.229 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D05 | `.dazposewizard/d5_5-evidence` | 0.451 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D06 | `.dazposewizard/d5_5-pose-test` | 2.487 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D07 | `.dazposewizard/d6-d8` | 33.505 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D08 | `.dazposewizard/day1-evidence` | 4.047 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D09 | `.dazposewizard/day2-evidence` | 8.458 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D10 | `.dazposewizard/day3-evidence` | 0.266 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D11 | `.dazposewizard/day4-evidence` | 2.123 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D12 | `.dazposewizard/day5-evidence` | 8.286 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D13 | `.dazposewizard/import-archive-fix` | 0.086 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D14 | `.dazposewizard/native-directing` | 15.252 GiB | Contains P01–P04; surrounding evidence is not included in project deletion decisions. | keep | Retain evidence; no blanket sweep. |
| D15 | `.dazposewizard/p0c-native-generation` | 25.201 GiB | Contains P01–P04; surrounding evidence is not included in project deletion decisions. | keep | Retain evidence; no blanket sweep. |
| D16 | `.dazposewizard/p2-p3` | 7.746 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D17 | `.dazposewizard/production-consolidation` | 26.363 GiB | Contains P01–P04; surrounding evidence is not included in project deletion decisions. | delete | Duplicate listing of explicitly retired P03/P04, not separate retained evidence. |
| D18 | `.dazposewizard/recovery` | 3.339 GiB | Recovery/before-state material; preserve baseline and ignored-input snapshot. | keep | Retain evidence; no blanket sweep. |
| D19 | `.dazposewizard/remediation-evidence` | 338.067 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D20 | `.dazposewizard/remediation-planning` | 0.024 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D21 | `.dazposewizard/remediation-validation` | 22.459 GiB | Contains P01–P04; surrounding evidence is not included in project deletion decisions. | delete | Duplicate listing of explicitly retired P03/P04, not separate retained evidence. |
| D22 | `.dazposewizard/unity-diagnostics-cleanup` | 5.140 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D23 | `.dazposewizard/wardrobe-gate-recovery` | 0.573 MiB | Recovery/before-state material; preserve baseline and ignored-input snapshot. | keep | Retain evidence; no blanket sweep. |
| D24 | `.dazposewizard/wardrobe-production-before` | 0.299 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D25 | `.dazposewizard/wardrobe-retired` | 0.648 MiB | Local historical diagnostics/evidence; unique-content retention audit not performed. | keep | Retain evidence; no blanket sweep. |
| D90 | `.dazposewizard/<immediate files>` | 27.812 MiB | Exact names: `audit-graft-pair.py`, `audit-nude-graft.py`, `clean-reference-fbx.json`, `finalize-day5-docs.py`, `inspect-alpha-graph.py`, `inspect-anatomy.py`, `inspect-breast-options.py`, `inspect-breast-value.py`, `inspect-count.py`, `inspect-graph.py`, `inspect-input.py`, `inspect-mapping.py`, `inspect-material-keys.py`, `inspect-second-heel.py`, `inspect-shape-normals.py`, `lara-archive-candidates.json`, `larafirstoutfit-calibration-check.json`, `larasecondoutfit-calibration-check.json`, `p0c-lighting-generation.log`, `p0c-native-generation.log`, `p0c-smoke-generation.log`, `record-day5-completion.py`, `recovery_snapshot.py`, `second-outfit-fbx.json`, `second-outfit-materials.json`, `wardrobe-deferred-camera.patch`, `wardrobe-final-stage.json`, `wardrobe-manifest-regression.json`, `wardrobe-materials.json`, `wardrobe-offline.nuget.config`, `wardrobe-stage-paths.json`, `wardrobe-summary.py`. Includes calibration/patch/input records as well as temporary tools. | keep | No blanket sweep. |

### Exact untracked and ignored paths

`git status --porcelain --ignored` captured before this manifest. These observations inherit parent/item decisions and grant no additional action. Quoted git paths decoded before sizing.

| State | Path | Size |
|---|---|---:|
| ?? | `newtickets/ARCHITECTURE_SPRINT_PLAN.md` | 0.027 MiB |
| ?? | `newtickets/T01_WORKSPACE_RESET.md` | 0.014 MiB |
| ?? | `oldtickets/DazPoseWizard_Native_Directing_Execution_Packet` | 0.159 MiB |
| ?? | `oldtickets/DazPoseWizard_Visual_Authoring_Execution_Packet` | 0.040 MiB |
| ?? | `oldtickets/DazPoseWizard_Wardrobe_Protected_Cards_Execution_Packet` | 0.105 MiB |
| ?? | `validation/DazPoseUnityValidation/Assets/__WardrobeProtectedCards.meta` | 172 B |
| ?? | `validation/DazPoseUnityValidation/Assets/__WardrobeProtectedCards` | 0.589 MiB |
| !! | `.dazposewizard` | 93.145 GiB |
| !! | `G8F-Base` | 588.753 MiB |
| !! | `G8F-inCherishReferencePose` | 75.553 MiB |
| !! | `artifacts/win-x64` | 206.130 MiB |
| !! | `fixtures/private/Cherish Genesis 8 Female 16.duf` | 0.003 MiB |
| !! | `fixtures/private/Genesis8Female.dsf` | 3.444 MiB |
| !! | `fixtures/private/ST PD Smirk.duf` | 0.098 MiB |
| !! | `output/Cherish Genesis 8 Female 16.bvh` | 0.027 MiB |
| !! | `output/Cherish Genesis 8 Female 16.dazpose.json` | 0.453 MiB |
| !! | `output/Cherish Genesis 8 Female 16.report.txt` | 0.005 MiB |
| !! | `output/fat-lara-raw-blendshapes.csv` | 1.428 MiB |
| !! | `src/DazPose.App/bin` | 1.361 GiB |
| !! | `src/DazPose.App/obj` | 2.215 MiB |
| !! | `src/DazPose.Core/bin` | 0.221 MiB |
| !! | `src/DazPose.Core/obj` | 0.354 MiB |
| !! | `stack3 poses` | 1.464 MiB |
| !! | `tests/DazPose.Player.Tests/bin` | 5.142 MiB |
| !! | `tests/DazPose.Player.Tests/obj` | 0.246 MiB |
| !! | `tests/DazPose.Rehearsal.Tests/bin` | 5.497 MiB |
| !! | `tests/DazPose.Rehearsal.Tests/obj` | 0.692 MiB |
| !! | `tests/DazPose.Story.Tests/TestResults` | 0.068 MiB |
| !! | `tests/DazPose.Story.Tests/bin` | 5.420 MiB |
| !! | `tests/DazPose.Story.Tests/obj` | 0.604 MiB |
| !! | `tests/DazPose.Tests/TestResults` | 0.094 MiB |
| !! | `tests/DazPose.Tests/bin` | 1.177 GiB |
| !! | `tests/DazPose.Tests/obj` | 0.817 MiB |
| !! | `tests/DazPose.Wardrobe.Tests/bin` | 5.173 MiB |
| !! | `tests/DazPose.Wardrobe.Tests/obj` | 0.267 MiB |
| !! | `unity` | 3.287 GiB |
| !! | `validation/DazPoseUnityValidation/.dazposewizard` | 88.297 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/Animations.meta` | 172 B |
| !! | `validation/DazPoseUnityValidation/Assets/Animations` | 21.603 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/Crazy Minnow Studio/Examples/3D/boxHead.v2/boxHead.v2.fbx` | 0.128 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/Crazy Minnow Studio/Examples/3D/boxHead.v2/boxHead.v2.small.fbx` | 0.129 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/Daz3D/larabridge/larabridge.fbx` | 3.722 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/DazPoseImports.meta` | 172 B |
| !! | `validation/DazPoseUnityValidation/Assets/DazPoseImports` | 10.232 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/Generated` | 4.387 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Common VFX Graph/Meshes/INab_Mesh_.fbx` | 0.021 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Common VFX Graph/Meshes/INab_Mesh_Cube.fbx` | 0.011 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Common VFX Graph/Meshes/INab_Mesh_Cube1x1.fbx` | 0.014 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Common VFX Graph/Meshes/INab_Mesh_Rock.fbx` | 0.011 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Common VFX Graph/Meshes/INab_Mesh_Sphere.fbx` | 0.021 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/3D Gamekit/Crystal/Model/SwitchStanding.fbx` | 0.048 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/3D Gamekit/Ellen/Animations/@EllenIdle.fbx` | 2.264 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/3D Gamekit/Statue/Model/AlienStatue.fbx` | 0.341 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Box_350x250x200_Mesh.fbx` | 0.042 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Box_350x250x300_Mesh.fbx` | 0.044 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Ground_Mesh.fbx` | 0.040 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Ramp_100x100x200_Mesh.fbx` | 0.015 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Ramp_Mesh.fbx` | 0.040 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Stairs_200x100x200_Mesh.fbx` | 0.049 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Stairs_650_400_300_Mesh.fbx` | 0.079 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Structure_Mesh.fbx` | 0.062 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Tunnel_Mesh.fbx` | 0.043 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Models/Wall_Mesh.fbx` | 0.043 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Jump--InAir.anim.fbx` | 0.878 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Jump--Jump.anim.fbx` | 0.748 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Locomotion--Run_N.anim.fbx` | 0.577 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Locomotion--Run_N_Land.anim.fbx` | 0.577 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Locomotion--Run_S.anim.fbx` | 0.572 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Locomotion--Walk_N.anim.fbx` | 0.622 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Locomotion--Walk_N_Land.anim.fbx` | 0.623 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx` | 1.071 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Demo Assets/StarterAssets/ThirdPersonController/Character/Models/Armature.fbx` | 4.789 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Interactive Dissolve/Meshes/Plane.fbx` | 0.011 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Interactive Dissolve/Meshes/PreviewBox.fbx` | 0.023 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Interactive Dissolve/Meshes/PreviewSphere.fbx` | 0.097 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/INab Studio/Interactive Dissolve/Meshes/Rock.fbx` | 0.011 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/l.aranudeclosed.fbx` | 112.870 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/l.aranudeclosed.images` | 106.988 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/l.aranudeopen.fbx` | 112.869 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/l.aranudeopen.images` | 106.988 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/lara.fbx` | 77.562 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/lara.images` | 108.575 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/laraHumanoid.fbx` | 77.562 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/laracleanreference.fbx` | 73.393 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/laracleanreference.images` | 71.704 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/larafirstoutfit.fbx` | 170.077 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/larafirstoutfit.images` | 138.249 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/laramaid.fbx` | 271.247 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/laramaid.images` | 266.494 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/laranude.fbx` | 130.759 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/laranude.images` | 107.726 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/larasecondoutfit.fbx` | 267.214 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/larasecondoutfit.images` | 255.964 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/playererect.fbx` | 160.955 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/playererect.images` | 158.583 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/playerflacid.fbx` | 160.955 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/playerflacid.images` | 158.583 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/playernude.fbx` | 160.973 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestCharacter/playernude.images` | 158.583 MiB |
| !! | `validation/DazPoseUnityValidation/Assets/TestData` | 2.867 GiB |
| !! | `validation/DazPoseUnityValidation/Builds` | 1.460 GiB |
| !! | `validation/DazPoseUnityValidation/Library` | 7.599 GiB |
| !! | `validation/DazPoseUnityValidation/Logs` | 13.687 MiB |
| !! | `validation/DazPoseUnityValidation/Temp` | 292 B |
| !! | `validation/DazPoseUnityValidation/TestOutput/Authoring` | 877 B |
| !! | `validation/DazPoseUnityValidation/TestOutput/CleanupTask1` | 0.217 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/FullToyStackCompile` | 0.991 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/G8MArtistErectionProof/UnityBuildFailure.txt` | 0.001 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/PartnerCharacter` | 0.224 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/TargetDrivenGripMigration` | 3.109 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/appearance-evidence` | 61.636 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/first-outfit-live.log` | 0.042 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/first-outfit-render.log` | 0.047 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/lara-unity-skeleton.json` | 0.301 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/pose-application-report.json` | 0.379 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/second-outfit-build.log` | 0.596 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/second-outfit-final-motion.log` | 0.033 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/second-outfit-final-render.log` | 0.030 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/second-outfit-import.log` | 0.029 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/second-outfit-motion.log` | 0.033 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/second-outfit-render.log` | 0.030 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/unity-setup.log` | 0.018 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/unity-user-session.log` | 4.291 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/unity-validation.log` | 0.029 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/wardrobe-dissolve-live.log` | 0.042 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/wardrobe-import` | 10.592 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/wardrobe-motion.log` | 0.039 MiB |
| !! | `validation/DazPoseUnityValidation/TestOutput/wardrobe-presentation.log` | 0.034 MiB |
| !! | `validation/DazPoseUnityValidation/UserSettings` | 0.069 MiB |

Pre-existing tracked state includes 54 ticket deletions under `newtickets/` and untracked counterpart packets under `oldtickets/`. The new plan/T01 and Gate B scene also predate this inventory. Nothing was staged. Later approved archival must preserve bytes and deliberately reconcile those pre-existing moves.

## A2 — all top-level Assets folders

Every local `.meta` (including ignored assets and root folder metas) was read: **4744 distinct GUIDs**, no duplicates. Requested consumer scan: **140** scene/prefab/asset/controller/overrideController files under Production, Scenes, DazPose and Player. Counts are distinct (GUID, consumer file) pairs **outside the owning top-level folder**; internal references are excluded. Extended scan: **2331** serialized/source/material/shader/assembly files across all primary Assets. Zero references do not prove unused: binary files, dynamic loads and vendor imports require other evidence. This is textual source evidence, not native AssetDatabase validation.

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| A01 | `validation/DazPoseUnityValidation/Assets/__WardrobeProtectedCards` | 0.589 MiB | Untracked retired Gate B scene and folder metas only; no bound story source remains. All 3 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Art’s later keep-all-scenes instruction prevails; GateB scene and containing metadata retained. |
| A02 | `validation/DazPoseUnityValidation/Assets/Animations` | 21.603 MiB | Configured imported pose wrappers/clips; six wrappers bound by both story companions. All 51 GUIDs searched: 12 external pairs / 2 files in requested scope; 13 pairs / 3 files across all Assets. | keep | Bound production content and configured importer output. |
| A03 | `validation/DazPoseUnityValidation/Assets/Content` | 516 B | Only empty remediation folders/metas; no non-meta files. All 4 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | delete | Remove empty subtree plus Content.meta after approval. |
| A04 | `validation/DazPoseUnityValidation/Assets/Crazy Minnow Studio` | 1.025 MiB | SALSA auxiliary vendor assets; production DLL is under Plugins. All 19 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Zero scoped refs does not establish package dispensability. |
| A05 | `validation/DazPoseUnityValidation/Assets/Daz3D` | 53.715 MiB | Bridge conversion scripts, shaders/subgraphs, bridge imported art. All 117 GUIDs searched: 0 external pairs / 0 files in requested scope; 379 pairs / 296 files across all Assets. | keep | Live importer/material conversion dependency. |
| A06 | `validation/DazPoseUnityValidation/Assets/DazExpressionImports` | 0 B | Empty watched import folder with existing root meta. All 1 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Import contract; not orphaned. |
| A07 | `validation/DazPoseUnityValidation/Assets/DazPose` | 105.870 MiB | Live runtime/editor/validation code and retained generated/audit content. All 620 GUIDs searched: 237 external pairs / 109 files in requested scope; 1345 pairs / 872 files across all Assets. | keep | Legacy bakers and generated assets are not wholesale obsolete. |
| A08 | `validation/DazPoseUnityValidation/Assets/DazPoseImports` | 10.232 MiB | Configured canonical pose import inputs, ignored locally. All 24 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Importer path contract/user input. |
| A09 | `validation/DazPoseUnityValidation/Assets/Examples` | 0.566 MiB | Saved AuthoringShowcase intentional example. All 3 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Separate from production scene collection. |
| A10 | `validation/DazPoseUnityValidation/Assets/FemaleMovementAnimsetPro` | 81.347 MiB | Two animation FBXs consumed by four audit assets/controllers. All 291 GUIDs searched: 8 external pairs / 4 files in requested scope; 8 pairs / 4 files across all Assets. | keep | Source comparison/audit dependencies. |
| A11 | `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid` | 0.249 MiB | Mixed live legacy/example code and Lara skin art; frozen reference scene uses runtime script. All 35 GUIDs searched: 1 external pairs / 1 files in requested scope; 9 pairs / 9 files across all Assets. | keep | Explicit exception; split not cheap/safe here. |
| A12 | `validation/DazPoseUnityValidation/Assets/Generated` | 4.387 MiB | Ignored MP3s and pose wrappers/clips used by Lara and frozen reference. All 17 GUIDs searched: 8 external pairs / 3 files in requested scope; 58 pairs / 12 files across all Assets. | keep | Live art despite directory name. |
| A13 | `validation/DazPoseUnityValidation/Assets/Gizmos` | 0.007 MiB | Editor resource image. All 2 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Unity special folder. |
| A14 | `validation/DazPoseUnityValidation/Assets/INab Studio` | 346.253 MiB | Dissolve/VFX vendor shader graphs; production material/graph dependency chain. All 497 GUIDs searched: 9 external pairs / 3 files in requested scope; 49 pairs / 27 files across all Assets. | keep | Live visual dependency. |
| A15 | `validation/DazPoseUnityValidation/Assets/KAWAII_ANIMATIOMS_100` | 378.731 MiB | Original seating/locomotion/action sources and audit assets. All 462 GUIDs searched: 1678 external pairs / 7 files in requested scope; 1678 pairs / 7 files across all Assets. | keep | Needed for rebakes; never discard just because baked clips exist. |
| A16 | `validation/DazPoseUnityValidation/Assets/PartnerProof` | 172 B | Only residual empty metadata; protected manifest includes PartnerProof/Generated.meta. All 2 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Protected metadata; cannot sweep as empty. |
| A17 | `validation/DazPoseUnityValidation/Assets/Player` | 0.043 MiB | Only first-party C#/assembly refs; actual player art is in Production. All 16 GUIDs searched: 12 external pairs / 6 files in requested scope; 33 pairs / 16 files across all Assets. | move to `validation/DazPoseUnityValidation/Assets/DazPose/Player` | Carry Player.meta and all descendants; update source links/staging roots, preserve assembly ownership. |
| A18 | `validation/DazPoseUnityValidation/Assets/Plugins` | 0.348 MiB | SALSA-LipSync.dll bound by Lara and retained scenes. All 18 GUIDs searched: 3 external pairs / 3 files in requested scope; 11 pairs / 11 files across all Assets. | keep | Live lip sync/vendor path. |
| A19 | `validation/DazPoseUnityValidation/Assets/Production` | 1.382 GiB | Production performers, male body, wardrobe releases, story, environment/motion and menu. All 380 GUIDs searched: 39 external pairs / 5 files in requested scope; 300 pairs / 85 files across all Assets. | keep | Protected production content. |
| A20 | `validation/DazPoseUnityValidation/Assets/Resources` | 0.005 MiB | GameApplication, global index, UI/catalog assets. All 6 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Resources.Load semantics; protected index. |
| A21 | `validation/DazPoseUnityValidation/Assets/Scenes` | 1.963 MiB | FirstContact, ClothingSetup and frozen FirstPerformanceVoid. All 4 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | Preserve scene contents/GUIDs. |
| A22 | `validation/DazPoseUnityValidation/Assets/Settings` | 0.195 MiB | HDRP, render pipeline, volume configuration. All 10 GUIDs searched: 0 external pairs / 0 files in requested scope; 0 pairs / 0 files across all Assets. | keep | ProjectSettings consumers outside requested scope. |
| A23 | `validation/DazPoseUnityValidation/Assets/TestCharacter` | 3.335 GiB | DAZ FBXs, textures and source/imported metadata. All 191 GUIDs searched: 5 external pairs / 5 files in requested scope; 6 pairs / 6 files across all Assets. | keep | Character-signature path protection. |
| A24 | `validation/DazPoseUnityValidation/Assets/TestData` | 2.867 GiB | Ignored live outfit/candidate/player art, footwear, materials and review scenes. All 1900 GUIDs searched: 321 external pairs / 20 files in requested scope; 369 pairs / 32 files across all Assets. | keep | Production dependencies and authored settings. |
| A25 | `validation/DazPoseUnityValidation/Assets/TextMesh Pro` | 2.702 MiB | Fonts/settings/resources used by UI. All 39 GUIDs searched: 2 external pairs / 1 files in requested scope; 2 pairs / 1 files across all Assets. | keep | Vendor/resource semantics. |
| A26 | `validation/DazPoseUnityValidation/Assets/Wardrobe` | 0.028 MiB | Configuration/catalog, preset/release metadata. All 32 GUIDs searched: 16 external pairs / 7 files in requested scope; 16 pairs / 7 files across all Assets. | keep | Protected legacy release/configuration paths. |

### Third-party rebake/import dependencies

| Pack | Concrete source/path dependency | Deletion consequence |
|---|---|---|
| KAWAII_ANIMATIOMS_100 | PerformerSeatingBaker.cs:140; PerformerLocomotionBaker.cs:125; PerformerActionBaker.cs:22; AnimationAuditCatalogBuilder.cs:17 | Lose seating/locomotion/JumpForJoy rebakes and audit sources. |
| FemaleMovementAnimsetPro | AnimationAuditCatalogBuilder.cs:18; HumanoidLegSetupReport.cs:20–21; both FBX GUIDs referenced by AnimationAuditCatalog and three controllers | Not established as production seating bake source, but audit and source comparisons break. |
| Daz3D | DazPoseBridgeMaterialCopy.cs:17; LaraCandidateBuilder/PartnerCharacterPreparation call DazPoseDtuConversionBridge; vendor Scripts/Editor registers conversion adapter; importer stages it | Material conversion and shader/subgraph references break. Extended scan finds 379 external GUID/file pairs. |
| INab Studio | Production dissolve graphs/materials depend on vendor graph GUIDs: 49 external pairs in extended scan | Production dissolve/VFX chain breaks. |
| Crazy Minnow Studio + Plugins | PerformerLipSyncAudit.cs:223 reads Plugins version; SALSA DLL GUID referenced by Lara prefab, RemediationToeValidation and FirstPerformanceVoid | Live lip sync fails. Zero refs for auxiliary top-level vendor assets is not package-removal approval. |

Bakers under `DazPose/Validation/Editor/Legacy/AnimationAudit/` still produce production clips. Keep them. No rebake or import was run.

### Every primary-project scene

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| S01 | `validation/DazPoseUnityValidation/Assets/Examples/Scenes/AuthoringShowcase.unity` | 0.565 MiB | Intentional retained typed-authoring example; not Build Settings. Code: `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/AuthoringBatch.cs:17`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/AuthoringContentComposer.cs:15`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/CleanupReferenceChecks.cs:18`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/ExampleSceneCleanup.cs:16` | keep | Inherits containing A-row; no additional byte count. |
| S02 | `validation/DazPoseUnityValidation/Assets/Production/Environments/VoidEnvironment.unity` | 0.563 MiB | Art-only New Performance Scene template; not Build Settings; protected. Code: `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/ProductionSceneEnvironment.cs:12` | keep | Inherits containing A-row; no additional byte count. |
| S03 | `validation/DazPoseUnityValidation/Assets/Production/Scenes/MainMenu.unity` | 0.051 MiB | Production menu; Build Settings enabled; protected exception. Code: `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/DazPoseProductionMenu.cs:29`, `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/GameUiAssetBuilder.cs:133`, `validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/SceneCatalog.cs:10`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/AuthoringNavigationCheck.cs:27`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/AuthoringPointerCheck.cs:19`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/CleanupFinalChecks.cs:21`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/CleanupReferenceChecks.cs:18`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/CleanupSafetyChecks.cs:23` | keep | Inherits containing A-row; no additional byte count. |
| S04 | `validation/DazPoseUnityValidation/Assets/Production/Scenes/RemediationToeValidation.unity` | 0.840 MiB | Second-outfit toe fixture; not Build Settings; protected. Code: `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/CleanupReferenceChecks.cs:18` | keep | Inherits containing A-row; no additional byte count. |
| S05 | `validation/DazPoseUnityValidation/Assets/Scenes/ClothingSetup.unity` | 0.561 MiB | Persistent wardrobe setup; not Build Settings; protected. Code: `docs/production-consolidation/validation/Day5Build.cs:35`, `docs/production-consolidation/validation/Day5Validation.cs:47`, `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Wardrobe/ClothingSetupBuilder.cs:14`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/CleanupReferenceChecks.cs:18` | keep | Inherits containing A-row; no additional byte count. |
| S06 | `validation/DazPoseUnityValidation/Assets/Scenes/FirstContact.unity` | 0.573 MiB | Current authored production; Build Settings enabled; protected. Code: `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/StorySceneBuilder.cs:5`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/AuthoringBatch.cs:16`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/AuthoringContentComposer.cs:16`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/AutomaticTuningCheck.cs:23` | keep | Inherits containing A-row; no additional byte count. |
| S07 | `validation/DazPoseUnityValidation/Assets/Scenes/FirstPerformanceVoid.unity` | 0.829 MiB | Frozen authored environment/reference; not Build Settings; protected. Code: `validation/DazPoseUnityValidation/Assets/DazPose/Editor/LaraCandidateInstaller.cs:15`, `validation/DazPoseUnityValidation/Assets/DazPose/Editor/PerformerActionBaker.cs:23`, `validation/DazPoseUnityValidation/Assets/DazPose/Editor/PerformerGestureBaker.cs:19`, `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/GameSessionSceneSetup.cs:18`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/CleanupReferenceChecks.cs:18`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Motion/FunscriptAcceptanceRunner.cs:51`, `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/Editor/FirstContactPerformanceSetup.cs:14`, `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/Editor/FirstPerformanceVoidBuilder.cs:6`, `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/Editor/PerformerDissolveSetup.cs:15`, `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/Editor/PerformerMotionAcceptanceSetup.cs:17`, `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/Editor/PerformerParticleBodyAcceptanceSetup.cs:20`, `validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/Editor/PerformerTeleportSetup.cs:15` | keep | Inherits containing A-row; no additional byte count. |
| S08 | `validation/DazPoseUnityValidation/Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity` | 0.832 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Wardrobe/ClothingSetupBuilder.cs:15`, `validation/DazPoseUnityValidation/Assets/DazPose/Editor/Wardrobe/WardrobeCatalogBuilder.cs:21`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Wardrobe/Day5WardrobeValidation.cs:77`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Wardrobe/Day5WardrobeValidation.cs:113`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Wardrobe/WardrobeExecutionValidation.cs:177`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Wardrobe/WardrobeExecutionValidation.cs:393`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Wardrobe/WardrobeExecutionValidation.cs:658`, `validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Wardrobe/WardrobeRuntimeFixtureBuilder.cs:15` | keep | Inherits containing A-row; no additional byte count. |
| S09 | `validation/DazPoseUnityValidation/Assets/TestData/LaraCandidate/FirstOutfit/FirstPerformanceVoidLaraFirstOutfit.unity` | 0.844 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |
| S10 | `validation/DazPoseUnityValidation/Assets/TestData/LaraCandidate/SecondOutfit/FirstPerformanceVoidLaraSecondOutfit.unity` | 1.378 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |
| S11 | `validation/DazPoseUnityValidation/Assets/TestData/Wardrobe/first-outfit/Review.unity` | 0.851 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |
| S12 | `validation/DazPoseUnityValidation/Assets/TestData/Wardrobe/second-outfit/Review-20261004-155507.unity` | 0.893 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |
| S13 | `validation/DazPoseUnityValidation/Assets/TestData/Wardrobe/second-outfit/Review.unity` | 0.893 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |
| S14 | `validation/DazPoseUnityValidation/Assets/TestData/Wardrobe/third-outfit/Review-20261004-165635.unity` | 0.854 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |
| S15 | `validation/DazPoseUnityValidation/Assets/TestData/Wardrobe/third-outfit/Review.unity` | 0.854 MiB | Ignored wardrobe/candidate review host; not Build Settings; preserve imported/user review art. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |
| S16 | `validation/DazPoseUnityValidation/Assets/__WardrobeProtectedCards/Scenes/GateB_ProtectedCards.unity` | 0.589 MiB | Retired disposable Gate B; not Build Settings; no external GUID references. Code: No exact C# path literal match; importer/metadata/derived paths may still open it. | keep | Inherits containing A-row; no additional byte count. |

Build Settings: exactly two enabled scenes, MainMenu (`7b0ea0678e2a15044a1a6fad410ec264`) and FirstContact (`4fd2d2bc3572c1744a590a23e4e117c9`). **Neither moves.** MainMenu and RemediationToeValidation are explicitly protected-manifest entries; the folder rule is not permission to relocate them.

### Three reportedly orphan metas

- `Assets/DazExpressionImports.meta`: folder **exists locally**, 0 B, 0 files, 0 tracked descendants. Examples: . Keep: git absence is not filesystem absence.

- `Assets/Generated.meta`: folder **exists locally**, 4.387 MiB, 30 files, 0 tracked descendants. Examples: `Assets/Generated/A.mp3`, `Assets/Generated/A.mp3.meta`, `Assets/Generated/B.mp3`, `Assets/Generated/B.mp3.meta`, `Assets/Generated/C.mp3`. Keep: git absence is not filesystem absence.

- `Assets/TestData.meta`: folder **exists locally**, 2.867 GiB, 3454 files, 0 tracked descendants. Examples: `Assets/TestData/Cherish Genesis 8 Female 16.dazpose.json`, `Assets/TestData/Cherish Genesis 8 Female 16.dazpose.json.meta`, `Assets/TestData/GraftPairProbe.meta`, `Assets/TestData/LaraCandidate.meta`, `Assets/TestData/PartnerCharacter.meta`. Keep: git absence is not filesystem absence.

## A3 — path consumers

Confirmed `docs/cleanup-task1/path-consumers.md`: completed-moves.json has 163 historical mappings; never treat old source entries as current missing paths. WardrobeCatalogBuilder.cs:220–235 includes `AssetDatabase.GetAssetPath(material)` in CharacterSignature. Protected manifest has **671 records**, including prior locations already moved by earlier authorized work. Preserve recovery baseline `18b268afe4df1ce11046a760b757002571a104c6` and its ignored snapshot. Do not restore/regenerate/delete based on stale baseline paths.

Searched .cs/.json/.ps1/.asmdef/.asset/.md files for `Assets/`, plus Python/.csproj/.asmref and Windows-separated paths. Historical baseline inventories are not active literals to rewrite. Duplicate projects/caches were excluded from live-consumer classification. Only Player code is proposed for relocation; production content moves are deliberately limited by protection.

### Player: every matching consumer file

| File | Matching lines | Treatment |
|---|---|---|
| `docs/SEVEN_DAY_PRODUCTION_CONSOLIDATION_PLAN.md` | 225 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/cleanup-task1/asset-dispositions.json` | 54, 55, 261, 262, 316 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/native-directing/N00_ASSET_AUDIT.json` | 2599, 2604, 2609, 2614, 2619, 2624, 2629, 2634, 2639, 2644, 4930, 4931, 4932, 4933, 4934, 4935, 4936, 4937, 4938, 4939 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/native-directing/N00_BASELINE.json` | 14276, 14281 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/native-directing/N01_ASSET_AUDIT.json` | 2614, 2619, 2624, 2629, 2634, 2639, 2644, 2649, 2654, 2659, 4948, 4949, 4950, 4951, 4952, 4953, 4954, 4955, 4956, 4957 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/native-directing/N04_ASSET_AUDIT.json` | 2649, 2654, 2659, 2664, 2669, 2674, 2679, 2684, 2689, 2694, 2699, 4995, 4996, 4997, 4998, 4999, 5000, 5001, 5002, 5003, 5004, 5005 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/p2-p3/F00_SCENE_INVENTORY.json` | 77, 78, 239, 240, 309, 310, 375, 376, 475, 476, 541, 542, 641, 642, 691, 692, 786, 787, 852, 853, 952, 953, 1013, 1014, 1103, 1104, 1196, 1197, 1286, 1287, 1600, 1601, 1700, 1701, 1762, 1763, 1847, 1848, 1953, 1954, 2003, 2004, 2111, 2112, 2206, 2207, 2263, 2264, 2373, 2374, 2414, 2415, 2499, 2500, 2712, 2713, 2717, 2718, 2802, 2803, 2873, 2874, 2923, 2924, 2970, 2971, 3025, 3026, 3077, 3078, 3132, 3133, 3179, 3180, 3219, 3220, 3266, 3267, 3291, 3292, 3337, 3338, 3437, 3438, 3487, 3488, 3587, 3588 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/p2-p3/FINAL_ASSET_AUDIT.json` | 2584, 2589, 2594, 2599, 2604, 2609, 2614, 2619, 2624, 2629, 4912, 4913, 4914, 4915, 4916, 4917, 4918, 4919, 4920, 4921 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/p2-p3/PHASE1_DEPENDENCY_GRAPH.json` | 27, 28, 97, 98, 163, 164, 263, 264, 329, 330, 429, 430, 479, 480, 574, 575, 640, 641, 740, 741, 801, 802, 891, 892, 984, 985, 1074, 1075, 1388, 1389, 1488, 1489, 1550, 1551, 1635, 1636, 1741, 1742, 1791, 1792, 1899, 1900, 1994, 1995, 2051, 2052, 2161, 2162, 2202, 2203, 2287, 2288, 2500, 2501, 2505, 2506, 2590, 2591, 2661, 2662, 2711, 2712, 2758, 2759, 2813, 2814, 2865, 2866, 2920, 2921, 2967, 2968, 3007, 3008, 3054, 3055, 3079, 3080, 3125, 3126, 3225, 3226, 3275, 3276, 3375, 3376 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/DAY_1.md` | 63 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/DEPENDENCIES.md` | 16 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/progress.json` | 376 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/remediation/CONTRACT.md` | 57 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/remediation/PACKET_VALIDATION.json` | 290, 294 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/remediation/R10_SOURCE_MOVE_MANIFEST.md` | 5 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/remediation/SOURCE_INDEX.md` | 24, 25 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/production-consolidation/remediation/SOURCE_MOVE_MANIFEST.json` | 7, 2329, 4737, 5059, 5065, 5071, 6440, 11672, 12237, 12563, 12569, 14242, 14576, 14582, 15036, 15102, 15108, 15205, 15211, 15756, 15762, 15822, 15823, 15831, 15832, 15843, 15850, 15851, 15859, 15860, 15877, 15883, 15889, 15895, 15902, 15903, 15914, 15915, 15925, 15931, 15937, 15944, 15945, 15953, 15954, 15961, 15962, 15970, 15971, 15981, 15988, 15989, 15997, 15998, 16254, 16263, 18356, 18357, 31607, 31608, 31731, 31732, 31758, 31765, 31775, 31776, 31783, 31784, 31791, 31792, 31799, 31800, 31829, 31836, 31849, 31850, 31855, 31862, 31875, 31876, 31888, 31895, 31905, 32438, 37014, 37015, 37679, 38470, 38491, 38498, 38526, 38533, 38699, 38701, 38911, 38918, 39639, 39646, 40976, 41263, 41270, 42271, 42600 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/sprint1 wrapup reviews/CODEMAP.md` | 184 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/story-runtime/D1_D5_FILES.md` | 15, 110, 111, 112, 113 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/studio-corrective/BASELINE_ASSET_AUDIT.json` | 2584, 2589, 2594, 2599, 2604, 2609, 2614, 2619, 2624, 2629, 4912, 4913, 4914, 4915, 4916, 4917, 4918, 4919, 4920, 4921 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/studio-corrective/FINAL_ASSET_AUDIT.json` | 2599, 2604, 2609, 2614, 2619, 2624, 2629, 2634, 2639, 2644, 4930, 4931, 4932, 4933, 4934, 4935, 4936, 4937, 4938, 4939 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/wardrobe-directing/WRITER_COVERAGE.md` | 18, 19 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/wardrobe-directing/evidence/w06-w07-checks.json` | 449, 453 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `docs/wardrobe-directing/evidence/wardrobe-production-delivery.json` | 114 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `newtickets/ARCHITECTURE_SPRINT_PLAN.md` | 124 | Update current prose only; retain historical statements. |
| `oldtickets/D1-D5/03_REPOSITORY_MAP_AND_PRESERVATION.md` | 13, 14 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `oldtickets/D1-D5/tickets/T06_player_body_presence_and_dissolve_vocabulary.md` | 12 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `oldtickets/DazPoseWizard_Native_Directing_Execution_Packet/CODEBASE_EVIDENCE.md` | 22, 23 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `oldtickets/DazPoseWizard_Native_Directing_Execution_Packet/PRODUCT_CONTEXT.md` | 22 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `oldtickets/DazPoseWizard_Native_Directing_Execution_Packet/REVIEWER_BRIEF.md` | 64 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `oldtickets/DazPoseWizard_Native_Directing_Execution_Packet/TICKET_ORDER.md` | 35 | Historical snapshots/evidence: retain original meaning; update navigation links on archive. |
| `scripts/import-lara-outfit.ps1` | 44, 70 | Update live reference to approved destination. |
| `scripts/run-wardrobe-execution.ps1` | 104 | Update live reference to approved destination. |
| `tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj` | 27 | Update live reference to approved destination. |

Critical live consumers: `scripts/import-lara-outfit.ps1:44,70`, `scripts/run-wardrobe-execution.ps1:104`, `tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj:27` (linked PlayerViewEndpoint.cs). Preserve all code/meta/asmref GUIDs. Scripts remain unexecuted; only approved path strings may change. `check-unity-source.py` discovers assembly roots dynamically; native compilation remains Art-operated.

### Other candidate paths and exceptions

- Empty Content: no live C#/PowerShell consumer found; only historical cleanup/native-directing baseline paths remain. All files are folder metas.
- Retired __WardrobeProtectedCards: no live executable C# path consumer; zero external GUID refs for remaining Gate B scene. Historical gate evidence, recovery and release-input fixture JSON stay. No bound `.dazstory` is deleted.
- PartnerProof: protected residual metadata stays, despite empty contents.
- FirstPerformanceVoid stays: FirstPerformanceVoidLighting.cs:123,125 and PerformerDissolveSetup.cs:24–32 use material paths; both wardrobe scripts stage its code root. Runtime asmdef/script remains used by frozen reference.
- No scene relocation: no Build Settings edits.
- `DazPoseProductionMenu.cs:48` opens root CONTENT_AUTHORING_API.md; approved archive requires changing Help target to retained current story authoring guide. `:49` already opens docs/story-runtime/DAZSTORY_AUTHORING.md.
- Every approved archive must fix links in retained current docs and Help navigation. Keep archive-relative links resolvable; historical paths inside evidence JSON remain historical, not rewritten live paths.

## A4/A5 — documentation and tickets

Current means source-consistent workflow information, not active execution authority. References with stale sections must be corrected in the approved docs group before new AGENTS advertises them. Directory rows and child rows can overlap; selective child decisions take precedence.

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| R01 | `AGENTS.md` | 0.017 MiB | Stacked superseded packet rules; T01 governs current work. | archive to `docs/archive/AGENTS_2026-10-20.md` | B4: preserve verbatim, then replace with <150 lines of current instructions. |
| R02 | `README.md` | 0.002 MiB | Wrong FirstContact path and obsolete cleanup queue; .sln, converter sources and publish script still exist. | keep | B5: fix entry points/status; retain valid converter build/run instructions. |
| R03 | `AUTHORING_READINESS_BUILD_PLAN.md` | 0.052 MiB | Historical October 7 B01–B08 plan, superseded by implementations and architecture sprint. | archive to `docs/archive/root/AUTHORING_READINESS_BUILD_PLAN.md` | Fix retained inbound links. |
| R04 | `CONTENT_AUTHORING_API.md` | 0.017 MiB | Mixed historical C#/JSON reference/current intro; wrong scene path; Help menu opens this file. | archive to `docs/archive/root/CONTENT_AUTHORING_API.md` | Update Help target/current links; preserve API history. |
| R05 | `CONTENT_AUTHORING_WALKTHROUGH.md` | 0.014 MiB | Historical typed C# alternative, not current First Contact DSL workflow. | archive to `docs/archive/root/CONTENT_AUTHORING_WALKTHROUGH.md` | Preserve alternative reference. |
| R06 | `DazPoseWizard_Deferred_Work.md` | 0.018 MiB | Historical deferred backlog; no execution authority. | archive to `docs/archive/root/DazPoseWizard_Deferred_Work.md` | Reference only |
| R07 | `UNITY_PROJECT_CLEANUP_PLAN.md` | 0.031 MiB | Historical October 17 audit/proposal superseded by T01. | archive to `docs/archive/root/UNITY_PROJECT_CLEANUP_PLAN.md` | Preserve ownership cautions/evidence. |
| F01 | `docs/ARCHITECTURE.md` | 0.004 MiB | Reference: converter parsing/export ownership agrees with DazPose.Core/App source layout. | keep | Reference only; not full Unity architecture. |
| F02 | `docs/cleanup-task1` | 1.046 MiB | Reference: historical execution mixed with essential protection/path/move/ownership records. | keep | Keep safety evidence paths; remove old execution from active entry points. |
| F03 | `docs/HISTORICAL_HANDOFF_2026-10-01_FIRST_CONTACT_TO_VOCAB_SPRINT.md` | 0.053 MiB | Historical dated spec/status, not current queue. SALSA packet says implementation absent although current source integrates SALSA. | archive to `docs/archive/docs/HISTORICAL_HANDOFF_2026-10-01_FIRST_CONTACT_TO_VOCAB_SPRINT.md` | Preserve evidence; fix current links. |
| F04 | `docs/IMPORT_FAILURE_CLEANUP.md` | 0.002 MiB | Current failed-import archive guide; menu/source exist. | keep | Art operates Unity; no launch authority. |
| F05 | `docs/native-directing` | 4.245 MiB | Historical N00–N11 reports and partial native-case evidence. | archive to `docs/archive/docs/native-directing` | Preserve exact gaps; no blanket acceptance. |
| F06 | `docs/P0.8.1_FACIAL_BONE_EXPRESSION_PATCH.md` | 0.014 MiB | Historical dated spec/status, not current queue. SALSA packet says implementation absent although current source integrates SALSA. | archive to `docs/archive/docs/P0.8.1_FACIAL_BONE_EXPRESSION_PATCH.md` | Preserve evidence; fix current links. |
| F07 | `docs/P0.8_TYPED_POSE_EXPRESSION_PIPELINE.md` | 0.028 MiB | Historical dated spec/status, not current queue. SALSA packet says implementation absent although current source integrates SALSA. | archive to `docs/archive/docs/P0.8_TYPED_POSE_EXPRESSION_PIPELINE.md` | Preserve evidence; fix current links. |
| F08 | `docs/P0.9B_SALSA_REALTIME_LIP_SYNC_INTEGRATION.md` | 0.022 MiB | Historical dated spec/status, not current queue. SALSA packet says implementation absent although current source integrates SALSA. | archive to `docs/archive/docs/P0.9B_SALSA_REALTIME_LIP_SYNC_INTEGRATION.md` | Preserve evidence; fix current links. |
| F09 | `docs/p2-p3` | 1.395 MiB | Historical cleanup/player UI/old Studio delivery and manual gates. | archive to `docs/archive/docs/p2-p3` | Update guide links; preserve pending checks. |
| F10 | `docs/preauthoring patch review` | 0.016 MiB | Historical reviews. | archive to `docs/archive/docs/preauthoring patch review` | Preserve references. |
| F11 | `docs/production-consolidation` | 2.567 MiB | Historical Days 1–5/remediation/authoring-readiness reports and recovery baseline record. | archive to `docs/archive/docs/production-consolidation` | Ignored recovery snapshot stays; update live baseline record pointers. |
| F12 | `docs/SEVEN_DAY_PRODUCTION_CONSOLIDATION_PLAN.md` | 0.031 MiB | Historical dated spec/status, not current queue. SALSA packet says implementation absent although current source integrates SALSA. | archive to `docs/archive/docs/SEVEN_DAY_PRODUCTION_CONSOLIDATION_PLAN.md` | Preserve evidence; fix current links. |
| F13 | `docs/sprint1 wrapup reviews` | 0.106 MiB | Historical code map/reviews. | archive to `docs/archive/docs/sprint1 wrapup reviews` | Preserve references. |
| F14 | `docs/story-runtime` | 0.176 MiB | Mixed current authoring/Studio guides, fixed oracle/contracts and history. | keep | Selective child actions below. |
| F15 | `docs/studio-corrective` | 1.625 MiB | Historical bespoke viewport correction, superseded by native Studio. | archive to `docs/archive/docs/studio-corrective` | Preserve evidence. |
| F16 | `docs/VALIDATION.md` | 0.003 MiB | Historical dated spec/status, not current queue. SALSA packet says implementation absent although current source integrates SALSA. | archive to `docs/archive/docs/VALIDATION.md` | Preserve evidence; fix current links. |
| F17 | `docs/visual-authoring` | 0.034 MiB | Historical source delivery/practical acceptance; extended cases remain unverified. | archive to `docs/archive/docs/visual-authoring` | Fix Studio acceptance evidence link. |
| F18 | `docs/VOCABULARY_SPRINT_DIRECTION_2026-10-01.md` | 0.005 MiB | Historical dated spec/status, not current queue. SALSA packet says implementation absent although current source integrates SALSA. | archive to `docs/archive/docs/VOCABULARY_SPRINT_DIRECTION_2026-10-01.md` | Preserve evidence; fix current links. |
| F19 | `docs/wardrobe-directing` | 0.525 MiB | Historical W00–W09 delivery; single-Lara acceptance separate from pending new Studio/FirstContact native use. | archive to `docs/archive/docs/wardrobe-directing` | Carry exact outstanding acceptance into new STATUS. |
| T01 | `oldtickets/D1-D5` | 0.059 MiB | Historical D1-D5 packet, sometimes still labelled ready/active. Native/visual/wardrobe packet moves already exist uncommitted. | archive to `docs/archive/oldtickets/D1-D5` | Preserve bytes; reconcile pre-existing renames only after approval. |
| T02 | `oldtickets/DazPoseWizard_Execution_Packet` | 0.106 MiB | Historical DazPoseWizard_Execution_Packet packet, sometimes still labelled ready/active. Native/visual/wardrobe packet moves already exist uncommitted. | archive to `docs/archive/oldtickets/DazPoseWizard_Execution_Packet` | Preserve bytes; reconcile pre-existing renames only after approval. |
| T03 | `oldtickets/DazPoseWizard_Native_Directing_Execution_Packet` | 0.159 MiB | Historical DazPoseWizard_Native_Directing_Execution_Packet packet, sometimes still labelled ready/active. Native/visual/wardrobe packet moves already exist uncommitted. | archive to `docs/archive/oldtickets/DazPoseWizard_Native_Directing_Execution_Packet` | Preserve bytes; reconcile pre-existing renames only after approval. |
| T04 | `oldtickets/DazPoseWizard_Studio_UI_Corrective_Execution_Packet` | 0.036 MiB | Historical DazPoseWizard_Studio_UI_Corrective_Execution_Packet packet, sometimes still labelled ready/active. Native/visual/wardrobe packet moves already exist uncommitted. | archive to `docs/archive/oldtickets/DazPoseWizard_Studio_UI_Corrective_Execution_Packet` | Preserve bytes; reconcile pre-existing renames only after approval. |
| T05 | `oldtickets/DazPoseWizard_Task1_Cleanup_Execution_Packet` | 0.115 MiB | Historical DazPoseWizard_Task1_Cleanup_Execution_Packet packet, sometimes still labelled ready/active. Native/visual/wardrobe packet moves already exist uncommitted. | archive to `docs/archive/oldtickets/DazPoseWizard_Task1_Cleanup_Execution_Packet` | Preserve bytes; reconcile pre-existing renames only after approval. |
| T06 | `oldtickets/DazPoseWizard_Visual_Authoring_Execution_Packet` | 0.040 MiB | Historical DazPoseWizard_Visual_Authoring_Execution_Packet packet, sometimes still labelled ready/active. Native/visual/wardrobe packet moves already exist uncommitted. | archive to `docs/archive/oldtickets/DazPoseWizard_Visual_Authoring_Execution_Packet` | Preserve bytes; reconcile pre-existing renames only after approval. |
| T07 | `oldtickets/DazPoseWizard_Wardrobe_Protected_Cards_Execution_Packet` | 0.105 MiB | Historical DazPoseWizard_Wardrobe_Protected_Cards_Execution_Packet packet, sometimes still labelled ready/active. Native/visual/wardrobe packet moves already exist uncommitted. | archive to `docs/archive/oldtickets/DazPoseWizard_Wardrobe_Protected_Cards_Execution_Packet` | Preserve bytes; reconcile pre-existing renames only after approval. |
| T20 | `newtickets/ARCHITECTURE_SPRINT_PLAN.md` | 0.027 MiB | Current October 20 architecture plan; B1 requires canonical unchanged copy and local commit. | move to `docs/plan/ARCHITECTURE_SPRINT_PLAN.md` | Commit plan first; source delivered alongside current ticket. |
| T21 | `newtickets/T01_WORKSPACE_RESET.md` | 0.014 MiB | Current execution ticket; A6 requires manifest approval stop. | keep | Current authority |

### Current/reference guides checked against source

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| C01 | `docs/story-runtime/DAZSTORY_AUTHORING.md` | 0.011 MiB | DSL/global-card workflow agrees with DazStoryCompiler, GlobalStoryCardIndexBuilder, PerformanceStoryHost and FirstContact.dazstory. | keep | Correct stale active-plan/pending-Studio status and archive links. |
| C02 | `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md` | 0.005 MiB | Static cue controls/seat workflow agree with SceneDirectorStudioWindow.Authoring.cs and NativeSeatingMath. | keep | Step 2 scene path wrong: fix to Assets/Scenes/FirstContact.unity; preserve bounded acceptance. |
| C03 | `validation/DazPoseUnityValidation/README.md` | 0.016 MiB | Current import/settings/menu overview correct; later smoke/legacy tests are historical. | keep | Retain current guide, label/relocate obsolete manual recipes; replace old cleanup queue pointers. |
| C04 | `validation/DazPoseUnityValidation/docs/LaraWardrobeImportQuickStart.md` | 0.012 MiB | Recipe wrapper/configuration/menu exist; persistent setup/runtime layers falsely described as planned. ClothingSetupBuilder and PerformerWardrobe implement them. | keep | Correct delivery paragraphs; existing batch/import scripts remain Art-operated only. |
| C05 | `validation/DazPoseUnityValidation/docs/LaraWardrobeImportStatus.md` | 0.019 MiB | Historical approvals/failed independent trial recorded; body retains obsolete runtime-absent claims. | keep | Add clear current summary; preserve dated evidence and separate approvals. |
| C06 | `validation/DazPoseUnityValidation/docs/LaraWardrobeImportPlaybook.md` | 0.018 MiB | Detailed importer exception reference linked by quickstart, not routine current queue. | keep | Reference; no new clean-context trial or Unity operation. |
| C07 | `validation/DazPoseUnityValidation/docs/PlayerView.md` | 0.004 MiB | PlayerView.cs confirms root position, ViewRig orientation, HeadPose seam, latest-wins MoveTo/LookAt/Track. | keep | Keep API; label obsolete migration/test menu recipes. |
| C08 | `validation/DazPoseUnityValidation/docs/HandGripImplementationNotes.md` | 0.005 MiB | Grip API, in-graph IK and reach planner exist. HandGripAcceptance scene removed; documented profile path obsolete. | keep | Use Assets/Production/Performers/Lara/Contact/LaraRightHandGrip.asset; label historical acceptance recipes. |

### Remaining immediate story-runtime items

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| Y01 | `docs/story-runtime/D1_D5_FILES.md` | 0.011 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/D1_D5_FILES.md` | Fix inbound/archive-relative links; preserve actual technical/visual status. |
| Y02 | `docs/story-runtime/D1_D5_HANDOFF.md` | 0.011 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/D1_D5_HANDOFF.md` | Fix inbound/archive-relative links; preserve actual technical/visual status. |
| Y03 | `docs/story-runtime/d5_5-d8` | 0.076 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/d5_5-d8` | Fix inbound/archive-relative links; preserve actual technical/visual status. |
| Y04 | `docs/story-runtime/D5_5_HANDOFF.md` | 0.009 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/D5_5_HANDOFF.md` | Fix inbound/archive-relative links; preserve actual technical/visual status. |
| Y05 | `docs/story-runtime/D6_D8_HANDOFF.md` | 0.009 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/D6_D8_HANDOFF.md` | Fix inbound/archive-relative links; preserve actual technical/visual status. |
| Y07 | `docs/story-runtime/FIRST_CONTACT_D8_ORACLE.md` | 0.016 MiB | Fixed transcript/production scene ownership reference. | keep | Preserve contracts; not a future execution queue. |
| Y08 | `docs/story-runtime/history` | 0.013 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/history` | Fix inbound/archive-relative links; preserve actual technical/visual status. |
| Y09 | `docs/story-runtime/MANUAL_TEST_CHECKLIST.md` | 0.003 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/MANUAL_TEST_CHECKLIST.md` | Fix inbound/archive-relative links; preserve actual technical/visual status. |
| Y10 | `docs/story-runtime/PRODUCTION_SCENES.md` | 0.005 MiB | Fixed transcript/production scene ownership reference. | keep | Preserve contracts; not a future execution queue. |
| Y12 | `docs/story-runtime/SCHEMA_V1.md` | 0.005 MiB | Historical story implementation/planning/diagnostic/handoff material; current guides/oracle kept separately. | archive to `docs/archive/docs/story-runtime/SCHEMA_V1.md` | Fix inbound/archive-relative links; preserve actual technical/visual status. |

### Fixture/setup scripts

| Item | Path | Size | Evidence | Approved action | Risk |
|---|---|---:|---|---|---|
| X01 | `scripts/native-directing-fixture.py` | 0.008 MiB | Creates only P01; prepare/sync-code/verify belong to retired native campaign. | archive to `docs/archive/scripts/native-directing-fixture.py` | Do not invoke; preserve as historical source. |
| X02 | `scripts/RemediationSourceMoveSync.ps1` | 0.005 MiB | Historical repair helper still dot-sourced by both wardrobe scripts. | keep | Live importer wrapper dependency. |
| X03 | `scripts/import-lara-outfit.ps1` | 0.018 MiB | Live recipe import/publish wrapper; P02 dependency and staging roots. | keep | Do not run; only approved move literals change. |
| X04 | `scripts/run-wardrobe-execution.ps1` | 0.010 MiB | Historical execution runner; shares staging/protection helper. | keep | Retained with P02 orchestration until migration; no specific retirement action chosen. |
| X05 | `scripts/check-unity-source.py` | 0.004 MiB | Source-only compiler helper, no Unity launch; dynamically finds assembly definitions. | keep | Diagnostic source evidence only. |

## Phase B document work proposed for approval

- B1: publish exact unchanged plan bytes at `docs/plan/ARCHITECTURE_SPRINT_PLAN.md` and commit locally first. SHA-256: `6c448dbc7d82ca58a7fbf5f394ee23d146e9f2f3df04fc3966e1212a05a2dc8b`.
- B3: approved archives only; write `docs/archive/README.md` listing each item and supersession reason/date.
- B4: archive AGENTS verbatim and write one current document under about 150 lines: project/map, current T01/plan path, Art-operated observables, folder rules/exceptions, scene paths, protections, no-Unity pending S01, no hardware/subagents/remote writes, managed build/test commands, verified reference shortlist and plan §5.8 VR-forward rules.
- Create `docs/plan/STATUS.md` as sole active status. Carry forward pending wardrobe/new Studio/FirstContact native use and extended cases exactly; history stays as evidence.
- B5: correct README and current guides as specified in R/C rows; converter build/run text remains user instructions, not agent launch authorization.
- No refactor, scene edit, binding/index write, regeneration or later sprint ticket. Only necessary path-literal source/test-project corrections corresponding to approved moves/archives.

## Disk total — no double counting

Explicit proposed deletions: **2.765 GiB** (**2,969,256,510 logical bytes**). Includes ignored managed build/test outputs, published converter binaries, empty Content metadata and retired Gate B scene subtree. Overlapping scene/folder rows counted once. Adjacent Content/GateB root metas add 344 bytes, excluded from the conservative total. Archive moves within one volume free no disk.

| Deletion decision | Path | Logical bytes |
|---|---|---:|
| L08 | `artifacts/win-x64` | 216,143,445 |
| B01 | `src/DazPose.App/bin` | 1,461,045,101 |
| B02 | `src/DazPose.App/obj` | 2,322,565 |
| B03 | `src/DazPose.Core/bin` | 231,404 |
| B04 | `src/DazPose.Core/obj` | 371,102 |
| B13 | `tests/DazPose.Tests/bin` | 1,263,338,180 |
| B14 | `tests/DazPose.Tests/obj` | 856,947 |
| B10 | `tests/DazPose.Story.Tests/bin` | 5,683,783 |
| B11 | `tests/DazPose.Story.Tests/obj` | 633,566 |
| B05 | `tests/DazPose.Player.Tests/bin` | 5,391,502 |
| B06 | `tests/DazPose.Player.Tests/obj` | 258,010 |
| B12 | `tests/DazPose.Tests/TestResults` | 98,468 |
| B15 | `tests/DazPose.Wardrobe.Tests/bin` | 5,424,213 |
| B16 | `tests/DazPose.Wardrobe.Tests/obj` | 279,591 |
| B07 | `tests/DazPose.Rehearsal.Tests/bin` | 5,764,189 |
| B08 | `tests/DazPose.Rehearsal.Tests/obj` | 725,283 |
| B09 | `tests/DazPose.Story.Tests/TestResults` | 71,027 |
| A03 | `validation/DazPoseUnityValidation/Assets/Content` | 516 |
| A01 | `validation/DazPoseUnityValidation/Assets/__WardrobeProtectedCards` | 617,618 |

**Separate unresolved potential:** P01+P03+P04 hold **64.072 GiB** (68,797,032,315 bytes). They are **ask Art**, not proposed deletions, because fixture-only art/evidence retention remains undecided. P02 adds 25.201 GiB but is a live importer staging dependency. P05 adds 3.287 GiB with independent/unknown ownership. All four staging clones total 89.273 GiB. No Unity Library/cache deletion proposed: that would force reimport.

## Baseline and verification handoff

Read-only baseline: 4,744 distinct primary-project GUIDs, no duplicates; six project roots; 16 scenes in the primary project; exactly two Build Settings entries; all three reportedly orphaned folders exist. SHA-256 baselines:

| File | SHA-256 |
|---|---|
| `validation/DazPoseUnityValidation/Assets/Scenes/FirstContact.unity` | `3aba29a644fd0027bf4d6a58e350e61e329bf5d74c25eb6051b03c37dfa51dc0` |
| `validation/DazPoseUnityValidation/Assets/Scenes/FirstContact.unity.meta` | `38208e8c69dfc2734f4b2e750817c5910b5f2869d4b303cc51a567aa0e3da131` |
| `validation/DazPoseUnityValidation/Assets/Scenes/ClothingSetup.unity` | `e2c32b34dc89ab65330b376f73685e382dd9391abc9566d5d88854f5c5ae4daf` |
| `validation/DazPoseUnityValidation/Assets/Production/Scenes/MainMenu.unity` | `3646b91ea668c2d28830fe1bb71fb84ac249c95ae80dd7a45ff092237e854918` |
| `validation/DazPoseUnityValidation/Assets/Resources/DazPose/GlobalStoryCards.asset` | `f0b4d629ad5bd5b2518ec5a22e143b1f17a3f45ce287f116e82d8f840ae5b7b1` |

After approved cleanup: run solution build and all five managed test projects, record actual counts in STATUS; audit asset/meta pairs with protected/approved pre-existing exceptions; check duplicate GUIDs and surviving live paths; compare protected hashes; review git status; cold-read new AGENTS for scene path/folder rule/boundaries/plan/current ticket. No post-move check or native pass is claimed in Phase A; no build/test needed for this read-only inventory.

Art’s later checks: First Contact import/reference errors and intro playback; ClothingSetup and MainMenu errors; Studio cue projection. Native acceptance stays pending until Art reports it. Source inspection is not a visual pass.

**A6 resolved:** Art supplied per-item decisions October 21 and confirmed Unity closed. Approved work proceeded; only P02 migration implementation and Art’s native verification remain outside the completed cleanup scope. Historical proposed deletion totals above are superseded by STATUS and the retirement log.
