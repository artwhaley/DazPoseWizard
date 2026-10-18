# Current cleanup execution — October 18

Task 1 C00–C10 is the active cleanup queue on main. Use [the execution report](docs/cleanup-task1/EXECUTION_REPORT.md) and [current ownership map](docs/cleanup-task1/SCENE_AND_CONTENT_OWNERSHIP.md) before following older plan links below. The October 7 authoring-readiness plan remains preserved historical/product context; it does not supersede the active cleanup gates. The moved D1–D5 ticket packet is under [oldtickets/D1-D5](oldtickets/D1-D5/README_FIRST.md).

The user authorized the reviewed Task 1 stack in `newtickets/DazPoseWizard_Task1_Cleanup_Execution_Packet/START_HERE.md`. Execute C00–C10 on main; no new branch or push. See `docs/cleanup-task1/EXECUTION_REPORT.md` for verified state and outstanding gates. The manual-testing boundary below remains authoritative. Historical D1-D5 tickets are under oldtickets.

# Manual testing boundary - latest October 17 user instruction

The user is the tester. Prepare code and the saved scene for manual Play IN UNITY.
Do not start Unity Play, automated scene playback, standalone builds, or standalone
players without a new explicit user request. No more standalone builds or launches
are authorized this turn. The user intentionally closed the previous player.
This correction overrides older instructions to build or execute delivered scenes.
Technical evidence already recorded remains historical; visual approval is pending.

# Current story work - D6-D8 execution

The user explicitly authorized complete D6-D8 execution on October 17. Work on main;
do not create a branch or stop at the former Intro/setup checkpoint. The revised
execution stack at docs/story-runtime/d5_5-d8/REVISED_EXECUTION_STACK.md and complete
FIRST_CONTACT_D8_ORACLE.md govern. Preserve the exact original sequence and all 19
fixed dialogue transcripts. Card choreography belongs in .dazstory and executes via
the same flat interpreter. General binding UX redesign remains deferred.

The six existing random imported stand/sit assignments are authorized. MainPosition1
and Bedside may be created as ordinary movable scene transforms; the user will tune
them. Explicitly labelled engineering funscripts are authorized through the normal
importer. No additional user asset work is required. Complete and validate the full
saved scene, clear its preview cutoff, and distinguish technical playback from user
visual approval. See docs/story-runtime/D6_D8_HANDOFF.md for current status.

# Authoring-readiness plan reference (after active cleanup gates)

The October 7 authoring-readiness revision remains the reference for that separate product plan after active Task 1 cleanup gates; it is not the current C00–C10 execution queue.

Start at [AUTHORING_READINESS_BUILD_PLAN.md](AUTHORING_READINESS_BUILD_PLAN.md)

and execute B01–B08 in dependency order when implementation is requested.

[CONTENT_AUTHORING_API.md](CONTENT_AUTHORING_API.md) is the author-facing guide;

it explicitly distinguishes current source from planned APIs. The October 8 review reconciliation is recorded in docs/production-consolidation/AUTHORING_PLAN_REVIEW_RESOLUTION.md; use the reviewed dependency order and runner dispositions. B05a and B05b are separately recorded parts of B05.

The user authorized implementation on October 8, 2026. Execute the reviewed

plan in order, including its bounded checks and delivered-scene/player playback.

Fixed user decisions: toy output is fire and forget and all toy errors are silent

successful no-ops to the app; explicit session Stop cancels all local work and toy

output; Intiface connection/settings belong to the application; production editor

commands belong under top-level DazPose; pose commands use configured smooth

defaults; tuning is never restored/reset by lifecycle cleanup; Lara is the speaking

performer; the player body uses the prepared male model and its measured contact-column

setup; actual saved-scene Play is the authoring acceptance path. Do not reintroduce

toy acknowledgement failures, temporary tuning restoration or generalized

capability/preflight gates from older packets.

Days 1–5 of docs/SEVEN_DAY_PRODUCTION_CONSOLIDATION_PLAN.md remain accepted.

The completed R01–R13 remediation stack and its evidence are historical context,

not the next implementation queue. The second-outfit toe appearance observation

remains unapproved until the user observes it. Do not turn that observation into a

new generalized validation or import campaign. Preserve historical import evidence

without claiming independent clean-context acceptance.

Read and update docs/production-consolidation/progress.json and HANDOFF.md for

implementation status. Record technical results, actual delivered-scene/player

playback and user visual approval separately. Do not mark execution complete from

source inspection. The new plan governs wherever an older packet conflicts.

Preserve Lara rig conventions, artistic settings, source/metas/GUIDs, user-edited

wardrobe layers and immutable releases. Preserve the authorized recovery baseline

commit 18b268afe4df1ce11046a760b757002571a104c6 and its ignored-input snapshot

recorded in docs/production-consolidation/remediation/baseline.json. No push or

external publication is implied.

# Production scene shape — October 17 correction

Production scenes contain production content. New Performance Scene copies the saved

`Assets/Production/Environments/VoidEnvironment.unity` art-only composition, never the

old test scene and its marker/controller hierarchy. Preserve the original Void art.

Keep only authored semantic marks actually used by the scene. Use the reusable

`Assets/Production/Player/PlayerBody.prefab` variant of the prepared male Partner,

including its original measured contact profile/surface and penis rig; do not use a

Lara visual copy as player body. This supersedes the older all-actors-Lara wording

for the player body. Story program flow must be visible in clear English in Console,

without per-frame spam or toy error output. See docs/story-runtime/PRODUCTION_SCENES.md.

# Authored performance test environment

Future delivered test scenes must contain their saved test runner. Pressing Play

must start the intended test without an editor helper injecting the runner or

using a separate disposable-only scene. Validate the exact delivered scene and

build that scene when claiming its playback works. The user accepted Day 2's

known missing-runner omission after investigation; this does not waive the

requirement for future tests.

The user's authored `validation/DazPoseUnityValidation/Assets/Scenes/FirstPerformanceVoid.unity`

is the environment authority for future performance tests: retain its glowing

floor, smoke, lighting, camera and volume settings. Reuse its saved scene data;

do not replace it with a generic stage or regenerate/retune it from builder defaults.

Keep the original scene untouched when making test compositions. Lara's

`Character Visibility Lights` belong to the reusable Lara prefab, with their

authored settings and Lara rendering-layer restriction preserved. Functional

validation passing does not mean the user approved replacement lighting.

# Running DazPoseTool for manual testing

For Lara character/wardrobe import work, first read

`validation/DazPoseUnityValidation/docs/LaraWardrobeImportQuickStart.md` and the

Current handoff in `validation/DazPoseUnityValidation/docs/LaraWardrobeImportStatus.md`.

Use `LaraWardrobeImportPlaybook.md` only for the relevant detailed operation or exception. These carry

the verified procedure, accepted artistic choices and active import state for

fresh-context agents. Update them with verified lessons and handoff state;

reuse the shared tools instead of rediscovering the import pipeline.

For a requested clean-context import trial, use shared tools with recipe/data

changes only. Record independent-agent completion separately from technical

checks and user visual approval. A run requiring shared-code repairs or another

agent takeover does not pass that trial; preserve the specific missing capability.

On this Windows machine, the default sandbox PowerShell runs as `CodexSandboxOffline`. Starting the GUI from that shell can put the window on an invisible desktop, so a running process alone does not mean the user can test it.

Before each manual run, publish the current source with `.\scripts\publish-win-x64.ps1` from the repository root. Do not launch an old `artifacts\win-x64` build after source changes. If the sandbox cannot read the signed-in user's NuGet config, use a temporary offline NuGet config backed by the already populated package cache; set `AVALONIA_TELEMETRY_OPTOUT=1` for the publish command so Avalonia does not try writing its build log under the protected user profile. Keep the temporary config out of Git.

Launch `artifacts\win-x64\DazPose.App.exe` with `Start-Process` through `exec_command` using `sandbox_permissions: "require_escalated"`. State in the justification that the sandbox account cannot show the GUI and the app needs to run in the signed-in user's desktop. For example:

```powershell

$app = Join-Path (Get-Location) 'artifacts\win-x64\DazPose.App.exe'

$proc = Start-Process -FilePath $app -PassThru

Start-Sleep -Seconds 3

$proc.Refresh()

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()

[pscustomobject]@{

    User = $identity.Name

    Session = [Diagnostics.Process]::GetCurrentProcess().SessionId

    AppSession = $proc.SessionId

    WindowHandle = $proc.MainWindowHandle

    WindowTitle = $proc.MainWindowTitle

    Responding = $proc.Responding

}

```

Confirm the launcher identity is the signed-in Windows user (currently `DESKTOP-MQUSNML\artwh`), the app and launcher session IDs match, and `WindowHandle` is nonzero with title `DazPoseTool V0` and `Responding = True`. If those checks fail, do not tell the user the app is open for testing.

# Wardrobe runtime and setup work

For runtime outfit/layer vocabulary, scene wardrobe recall or the persistent

Clothing Setup scene, start with

`validation/DazPoseUnityValidation/docs/wardrobe-execution/START.md` and its

`progress.json`. Follow its ordered stages and fixed contract. The broader

`validation/DazPoseUnityValidation/docs/WardrobeRuntimeAndSetupSpec.md` is the

product/rationale reference. The execution package describes work to implement;

its presence is not proof that runtime capabilities or validation runners exist.

# Launching the Unity validation editor

For the Unity harness at `validation\DazPoseUnityValidation`, launch `C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe` with `-projectPath` set to that project, using `exec_command` with `sandbox_permissions: "require_escalated"`. The default sandbox desktop is invisible to the signed-in user. Confirm the Unity process is running in the signed-in user's interactive session and has a visible main window before saying it is ready for manual testing. Unity batch mode is only for imports, diagnostics, and automated checks; it does not satisfy a request to open the editor on the user's desktop.
