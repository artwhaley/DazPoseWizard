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

Chosen layout: one physical copy of Assets/Packages/ProjectSettings and DazPoseWizard.project.json, including ignored art/poses/FBXs. Exclude Library/Temp/Logs/UserSettings/Build*/TestOutput. Initial copy: **9,272 files, 9,695,184,839 bytes (9.029 GiB)** including Packages and settings; copy plus initial sync took **24.580 seconds**. The existing main Library size is about 7.6 GiB, leaving ample headroom within 50 GiB. A plain worktree would omit required local inputs. No further clones are authorized.

`scripts/sandbox-sync.ps1` initializes only the agreed separate path and subsequently synchronizes tracked code/metas plus deliberately changed source package files. It never writes to main or the sandbox Library, protects independent sandbox code edits, and keeps Unity-resolved or S02-edited package files when main's source package graph is unchanged. The sandbox identity marker stores source, destination, approved bytes and source hashes.

## Main preparation

Declared the already-resolved `com.unity.test-framework` **1.7.0** directly, retaining its resolved version. Existing transitive consumers are Animation Rigging, Collections and Test Framework Performance. Lock-file depth for Test Framework and NUnit is updated for the direct dependency.

Added editor-only `DazPose.Editor.Tests` referencing DazPose.Editor and both test runners, following the installed framework’s test-assembly pattern. Nine hand-computed placement tests cover BakedRoot with nonuniform scale/yaw, AlignLiveHip world deltas/no-op, full mark rotation, yaw-only projection and invalid forward vectors. Normalized production scenes/assets remain unchanged. The main editor imports the new package declaration/test assembly on its next refresh; the executor does not open main.

## Native results

Unity **6000.5.9f1**, `-batchmode -nographics`, against `C:\Users\artwh\DazPoseWizard-Sandbox` only. Every test invocation selected `DazPose.Editor.Tests`, `EditMode`, with a fresh result path and log path, without `-quit`. The probe used `-executeMethod DazPose.Editor.Studio.S01SceneProbe.Run -quit`, without `-runTests`. The recorded launch arguments are in `s01-evidence/*.launch.json`; the measurement harness is `s01-evidence/run-harness.ps1`.

| Run | Wall seconds | Exit | Actual result |
|---|---:|---:|---|
| 01, full cold import | approximately 463.062 | 0, from Unity log | 9 passed, 0 failed |
| 02, unchanged | 12.336 | 0 | 9 passed, 0 failed |
| 03, unchanged | 12.032 | 0 | 9 passed, 0 failed |
| 04, unchanged | 12.035 | 0 | 9 passed, 0 failed |
| 05, unchanged | 11.816 | 0 | 9 passed, 0 failed |
| 06, unchanged | 11.795 | 0 | 9 passed, 0 failed |
| 07, trivial code edit plus sync | 14.923 | 0 | 9 passed, 0 failed |
| 08, deliberately wrong assertion plus sync | 13.128 | 2 | 8 passed, **1 failed** |
| 09, restored source plus sync | 13.128 | 0 | 9 passed, 0 failed |
| 10, read-only FirstContact scene probe | 29.614 | 0 | Seven locations resolve; both diagnostic arrays empty |

Five unchanged cached runs averaged **12.003 seconds** (11.795–12.336). Run 07 changed only a positive integer marker from 1 to 2 in the test class. Run 08 changed the first hand-computed expected x coordinate from 8 to 9. Unity reported `BakedRoot_UsesEachScaleAxis`: **expected 9, actual 8**, with process exit 2. Run 09 restored both edits to the committed original; no deliberately failing source remains. See the actual [failure XML](s01-evidence/08-deliberate-failure.xml) and [restored XML](s01-evidence/09-restored.xml).

The first run's test XML and successful Unity shutdown log survived, but the wrapper lost its summary when a transient file disappeared during recursive disk enumeration at shutdown. Its wall time above is reconstructed from launch UTC to log last-write UTC, so it is approximate, not a recovered stopwatch measurement. Exit 0 is the explicit Unity log result rather than a captured process exit property. The wrapper was adjusted to tolerate transient enumeration entries; all subsequent runs have captured stopwatch/exit summaries. This limitation does not affect the preserved first-run XML (9/9).

## Scene probe and failure modes

The probe opened the sandbox copy of `Assets/Scenes/FirstContact.unity`, outside Play. The compiled story was available. `lara_start`, `player_start`, `camera_start`, `stepforward`, `Lounger` (ApproachAnchor), `MainPosition1` and `Bedside` all resolved. `StoryContentBindings.Diagnostics` and every discovered `PerformanceSceneBindings.Validate` returned no issues. The scene was not dirty after diagnostics, and its on-disk SHA-256 was identical before and after, also matching main: `3ABA29A644FD0027BF4D6A58E350E61E329BF5D74C25EB6051B03C37DFA51DC0`. See [probe output](s01-evidence/scene-probe.json) and [hash evidence](s01-evidence/scene-hash.json).

The scene loaded successfully headless; **HDRP did not run without warnings**, and this is not rendering or visual acceptance:

- Null graphics emitted HDRP shader texture-count limits and unsupported conservative-rasterization warnings. No attempt was made to fix graphics or change authored art.
- Cold import logged a VFX slot sanitization `InvalidOperationException` (`Unable to create slot for property s`). Import continued, the test assembly compiled, and all nine tests ran. That third-party VFX issue remains uninvestigated within this bounded spike.
- Licensing logged `Access token is unavailable; failed to update` and entitlement-group 404 messages, but also successfully resolved entitlement details. Licensing did **not block** a run; no licensing intervention was performed.
- No Unity hang, timeout, project-lock failure or C# compilation failure occurred. The only wrapper failure was the first-run enumeration race described above. The intentional assertion failure was correctly observable.

Selected log lines are preserved in `s01-evidence/*.log-excerpt.txt`; full native logs remain in `C:\Users\artwh\DazPoseWizard-Sandbox\Logs\s01-*.log`. Test XML, launch records and per-run summaries are preserved in `s01-evidence/`. Existing Unity process records in committed evidence contain IDs only, excluding unrelated process command lines and authentication arguments.

## Final cost and preservation

Final sandbox logical size: **16,486,663,506 bytes (15.354 GiB)** of the approved **50 GiB**, including **6,785,351,896 bytes (6.319 GiB)** of Library. All ignored poses, import inputs and FBXs were copied once. This is the persistent S02 sandbox; no second copy was created. Cached-run sampled sizes were stable around 15.35 GiB; the cold-run wrapper failure prevents claiming a complete cold-run peak measurement. Final measurement was taken after Unity exited and succeeded without the transient enumeration race.

No owned Unity process remains running. Pre-existing main Editor/import-worker processes were left alone. All **689 protected baseline file hashes match**, accounting for the prior T01 path moves; see [preservation evidence](s01-evidence/protected-hashes.json). Main's saved FirstContact scene also matches the sandbox hash. Main changes are limited to the declared framework/lock depths, test assembly/metas, sync script, read-only probe/metas, this report and evidence. The main project will import the explicit framework declaration and new test/probe source on its next refresh. No main Unity operation was performed. No push was made.

## Verdict and proposed boundary

**Adopt** sandbox headless EditMode tests and read-only `-executeMethod` probes. Evidence supports a useful approximately 12-second cached feedback loop, real negative-result detection, incremental compilation, and a working scene-binding probe at 15.354 GiB. This grants no evidence about rendering, Play behavior, hardware, or visual approval.

Exact proposed replacement/overriding boundary text for Art's acceptance:

> Agents may run Unity 6000.5.9f1 only with `-batchmode -nographics` against the single persistent sandbox at `C:\Users\artwh\DazPoseWizard-Sandbox`, within its approved 50 GiB disk cap. Allowed operations are EditMode tests and reviewed read-only `-executeMethod` probes. Never open or operate the main project `validation/DazPoseUnityValidation` in Unity, launch Editor GUI or Hub, enter Play, build or launch players, or touch hardware. Make source changes in the main repository, commit them locally, and use `scripts/sandbox-sync.ps1` to synchronize tracked code and deliberate package changes without writing the sandbox Library or main project. Do not create another sandbox. Review any sandbox change before copying it into main. Use unique result/log paths, record process exit and actual test XML, impose a timeout, monitor disk usage, and stop on the cap or blocking licensing failure. On completion or abort, terminate only Unity processes started by the agent; never terminate Art's processes. Headless results do not establish rendering quality or Art's visual acceptance. No remote writes are authorized by this rule.

For **T03**, add focused hand-computed EditMode coverage for placement semantics and failure cases in the existing test assembly; sync and run it in this sandbox, inspect the actual XML/exit code, then run the read-only FirstContact binding probe. Preserve scene GUIDs and authored content. Art still performs native visual/interaction acceptance. T03 must wait for Art's S01 rule decision; S02 reuses this sandbox and remains a separate sequential spike.

**Decision pending Art.** `AGENTS.md`, `docs/plan/STATUS.md` and the architecture plan are unchanged. Do not treat this proposed text as accepted authority. An unrelated untracked `newtickets/T02_PLAYER_CAMERA_MOUNTS.md` appeared during execution and is left untouched and uncommitted.
