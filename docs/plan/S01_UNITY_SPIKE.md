# S01 — agent headless Unity spike

Execution started October 21, 2026. Art authorized S01 and approved **50 GiB** at `C:\Users\artwh\DazPoseWizard-Sandbox`, outside the repository and OneDrive. S01 alone permits batch/nographics Unity against that sandbox. Main Unity, GUI, Hub, Play, builds/players and hardware remain prohibited. No general rule change is made before Art decides.

## Baseline and layout

| Item | Logical bytes | GiB |
|---|---:|---:|
| Main Assets | 9,201,640,300 | 8.570 |
| Main Library | 8,156,850,911 | 7.597 |
| Assets/Animations | 22,652,477 | 0.021 |
| Assets/DazPoseImports | 10,729,219 | 0.010 |
| Assets/DazExpressionImports | 0 | 0 |
| Assets/Generated | 4,600,180 | 0.004 |
| Assets/TestData | 3,078,358,559 | 2.867 |
| All 467 FBX inputs (already included in Assets) | 2,325,155,056 | 2.166 |
| Free drive space before creation | 593,873,768,448 | 553.088 |

Chosen layout: one physical copy of Assets/Packages/ProjectSettings and DazPoseWizard.project.json, including ignored art/poses/FBXs. Exclude Library/Temp/Logs/UserSettings/Build*/TestOutput. About 8.6 GiB of inputs fits within 50 GiB with headroom for a fresh Library; the existing main Library size is about 7.6 GiB. A plain worktree would omit required local inputs. No further clones are authorized.

`scripts/sandbox-sync.ps1` initializes only the agreed separate path and subsequently synchronizes tracked code/metas plus deliberately changed source package files. It never writes to main or the sandbox Library, protects independent sandbox code edits, and keeps Unity-resolved or S02-edited package files when main's source package graph is unchanged. The sandbox identity marker stores source, destination, approved bytes and source hashes.

## Main preparation

Declared the already-resolved `com.unity.test-framework` **1.7.0** directly, retaining its resolved version. Existing transitive consumers are Animation Rigging, Collections and Test Framework Performance. Lock-file depth for Test Framework and NUnit is updated for the direct dependency.

Added editor-only `DazPose.Editor.Tests` referencing DazPose.Editor and both test runners, following the installed framework’s test-assembly pattern. Nine hand-computed placement tests cover BakedRoot with nonuniform scale/yaw, AlignLiveHip world deltas/no-op, full mark rotation, yaw-only projection and invalid forward vectors. Normalized production scenes/assets remain unchanged. The main editor imports the new package declaration/test assembly on its next refresh; the executor does not open main.

## Runs

Not run yet. Measurements and result XML/log links will be recorded here; no test pass or verdict is claimed by source preparation.

## Verdict

Pending native measurements. AGENTS.md and STATUS.md stay unchanged until Art decides on the proposed boundary text.
