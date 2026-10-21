# DazPoseWizard

DazPoseWizard converts DAZ poses and supplies a Unity performance authoring/runtime system. Lara is the speaking performer; the reusable player body is the prepared male Partner variant. Choreography is authored in `.dazstory` and previewed through Scene Director Studio.

## Current work and status

- Plan: [Architecture sprint plan](docs/plan/ARCHITECTURE_SPRINT_PLAN.md).
- Current ticket: [T02 — Player camera mounts, fade, release and breathing](newtickets/T02_PLAYER_CAMERA_MOUNTS.md), three sessions. Session 1 (mounts and `Camera.StartAt(High|Low)`) is accepted; Sessions 2–3 (fade, `SetHeight`, `Release`, Player Setup scene, breathing, `Lara.Posture`/`Player.Posture`) are implemented and waiting for Art’s check. T01 cleanup is implemented; Art’s native checks for it remain pending.
- Single active status: [STATUS](docs/plan/STATUS.md). Technical checks and Art’s visual acceptance are separate.
- P02 clone is retired. [Main-project import design](docs/plan/P02_MAIN_PROJECT_IMPORT_PLAN.md) remains a deferred implementation: Art explicitly limited this pass to retirement, preservation and main-only preparation. New outfit conversion/publication is not currently supported through the wrapper; existing imported outfits are unchanged.
- Historical packets/evidence are under `docs/archive/`; their launch/gate instructions are not active authority.

## How we work

Art owns product decisions and performs Unity/visual tests. Work interactively, finish each session with a concrete observable, and write one ticket ahead. Follow the current ticket’s scope; do not resume retired native/wardrobe gate chains. Single-Lara wardrobe behavior was accepted and the second-Lara trial waived; new Studio/FirstContact wardrobe native use remains unverified.

## Repository

- `src/`: live Avalonia converter and managed core.
- `tests/`: five managed test projects; the solution includes only the converter test project, so run all five explicitly.
- `validation/DazPoseUnityValidation/`: authoritative Unity project, Unity 6000.5.9f1.
- `docs/`: current plan/status, author guides and cleanup safety references; `docs/archive/` holds history.
- `scripts/`: shared preparation, import and source-check helpers; their presence is not authorization to launch Unity.

## Unity folder rule and scenes

- `Assets/DazPose/`: first-party runtime/editor/validation code, including `DazPose/Player`.
- `Assets/Production/`: production art/content, performers, player, wardrobe, story, environments and motions.
- `Assets/Scenes/`: new user-facing scenes.
- Vendor packs remain at vendor paths. Keep Unity special `Resources`, `Plugins`, `Gizmos`, render `Settings` and TMP conventions.
- Protected/importer-controlled legacy roots (`TestCharacter`, `TestData`, `Generated`, `Animations`, `Wardrobe`) and mixed `FirstPerformanceVoid` remain explicit exceptions. Their names do not make them disposable.
- First Contact: **`Assets/Scenes/FirstContact.unity`**. Preserve contents and GUID.
- Clothing Setup: **`Assets/Scenes/ClothingSetup.unity`**.
- Main Menu: **`Assets/Production/Scenes/MainMenu.unity`**; protected existing folder exception.
- New Performance Scene copies **`Assets/Production/Environments/VoidEnvironment.unity`**, the saved art-only template. Preserve original `Assets/Scenes/FirstPerformanceVoid.unity`.
- Keep all existing primary-project scenes, including the remaining retired Gate B scene. Do not use it as a new clothing blocker.

## Boundaries and protected content

- **Sandbox-only headless Unity, accepted October 21, 2026 (S01).** Agents may run Unity 6000.5.9f1 only with `-batchmode -nographics` against the single persistent sandbox at `C:\Users\artwh\DazPoseWizard-Sandbox`, within its approved **50 GiB** disk cap. Allowed operations are EditMode tests and reviewed read-only `-executeMethod` probes. Use `scripts/sandbox-sync.ps1` to synchronize tracked code and deliberate package changes from main without writing the sandbox Library or main project.
- **Sandbox editor-command dry runs are allowed headlessly.** After each dry run, show Art a text diff of **every asset file the command changed**, including added/deleted files and `.meta` files. Then restore the sandbox assets to match the main working copy and confirm they match before the next dry run or handoff. Never use Unity to change main-project assets; Art runs editor commands in main. Debug code iteratively in main and provide clean handoff instructions for Art to reproduce the result in main.
- **Never** open or operate the main project `validation/DazPoseUnityValidation` in Unity in any mode; launch the Editor GUI or Hub; enter Play mode; build or launch players; create a second sandbox; use computer-use or touch hardware. Nothing flows from the sandbox back into the main repository. Code changes must be made in main and committed there; sandbox-generated assets are never copied back. Pure managed builds/tests and source checks remain allowed.
- Use unique result/log paths, record process exit and actual test XML, impose a timeout, monitor disk usage, and stop on the cap or blocking licensing failure. On completion or abort, kill any Unity process the agent started that remains running; never kill a process the agent did not start. Sandbox results are evidence for **logic and wiring only**; visual acceptance stays with Art. See [S01 report](docs/plan/S01_UNITY_SPIKE.md).
- Work on `main`; coherent local commits are authorized. No push, PR, merge or remote write without explicit separate authorization.
- No subagents unless Art explicitly authorizes them.
- Moves carry file/folder `.meta` files and preserve GUIDs. Confirm Unity closed before filesystem changes under Assets. Outside the authorized sandbox dry runs above, do not regenerate, reimport or rebake assets without asking.
- Preserve Lara imported/source materials, textures, manifests and their AssetDatabase paths: WardrobeCatalogBuilder includes material paths in CharacterSignature.
- Preserve immutable wardrobe releases and rig generations under Production/Wardrobe and existing legacy Wardrobe roots; artist-edited materials, footwear and layers remain authoritative.
- Preserve second-outfit toe fixtures, production art, bound `.dazstory`/`.bindings.asset` companions, story global index, and every entry in [protected manifest](docs/cleanup-task1/protected-manifest.json), accounting for already-completed historical moves.
- Preserve recovery baseline `18b268afe4df1ce11046a760b757002571a104c6` and its ignored-input snapshot. Historical record: [baseline](docs/archive/docs/production-consolidation/remediation/baseline.json).
- Toy output remains fire-and-forget; backend errors are silent successful no-ops. Explicit session Stop cancels local work/output. Do not restore/reset artist tuning on lifecycle cleanup.
- Respect the plan’s **section 5.8 VR-forward rules** on every change: player root/HeadPose ownership, replaceable forced view direction, posture eye heights, gaze to HeadPose, world-space-capable prompts and frame budget.

## Managed checks

From repository root, `dotnet build .\DazPoseTool.sln -c Release` builds the converter and its tests without opening the app. Run every managed suite:

```powershell
Get-ChildItem tests -Recurse -Filter *.csproj | ForEach-Object { dotnet test $_.FullName -c Release }
```

Run managed checks independently of the permitted sandbox Unity checks above; do not substitute Unity tests for the managed suites or launch other programs. Private fixture tests may skip when licensed local inputs are absent; report skips separately. Rebuilt bin/obj/TestResults remain ignored outputs. If sandbox profile config is unreadable, use temporary APPDATA/DOTNET_CLI_HOME plus the existing offline NuGet package cache; keep temporary config out of Git.

## Current references

- [Story authoring](docs/story-runtime/DAZSTORY_AUTHORING.md) and [Studio guide](docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md).
- [Production scene contract](docs/story-runtime/PRODUCTION_SCENES.md) and [fixed First Contact oracle](docs/story-runtime/FIRST_CONTACT_D8_ORACLE.md).
- [Unity pipeline guide](validation/DazPoseUnityValidation/README.md).
- [Wardrobe quickstart](validation/DazPoseUnityValidation/docs/LaraWardrobeImportQuickStart.md), [import status](validation/DazPoseUnityValidation/docs/LaraWardrobeImportStatus.md), and detailed playbook for exceptions. Current wrapper supports main-only Preflight/Inspect/Prepare/Status without Unity; conversion/validation/publication hookup is deferred. The full previous procedure is preserved under docs/archive/p02-import-evidence.
- [PlayerView API](validation/DazPoseUnityValidation/docs/PlayerView.md), [HandGrip notes](validation/DazPoseUnityValidation/docs/HandGripImplementationNotes.md).
- [Path safety](docs/cleanup-task1/path-consumers.md), [completed moves](docs/cleanup-task1/completed-moves.json), and [T01 decisions](docs/workspace/CLEANUP_MANIFEST.md).
