# Current authoritative work plan

The user approved `docs/SEVEN_DAY_PRODUCTION_CONSOLIDATION_PLAN.md` on
2026-10-05. It is the authoritative TODO list and game plan until its required
completion gates are satisfied or the user explicitly changes the plan.
Start planned development there. Execute its days in dependency order; a calendar
day ending does not complete a gate. Do not substitute older sprint proposals,
wrap-up reviews, codemaps or stale handoff claims for this plan.

The user revised the remaining work after accepting Days 1–5. Before the composed
API-authored scene, follow
`docs/production-consolidation/PRE_AUTHORING_REMEDIATION_PLAN.md`: investigate the
second-outfit toe regression and remediate reassessment issues 1, 2, 3, 5 and 6.
Other-character portability (issue 4) is explicitly excluded. Preserve working
Lara rig conventions; do not build a generalized character controller. This
changes remaining work order, not the required execution or preservation gates.

Detailed execution packets live under `docs/production-consolidation/`.
Day 1 starts at `docs/production-consolidation/DAY_1.md`; shared progress is
`docs/production-consolidation/progress.json`. Read the active packet and progress
before starting, and update progress plus evidence/handoff before yielding.
Packets make the approved plan executable; they may not silently expand its scope
or weaken its acceptance gates. Do not mark a day complete from source inspection
when its gate requires execution evidence. Record technical passes, human visual
approval and independent clean-context import acceptance separately.

Existing subsystem contracts, accepted artistic decisions, source/GUID protection
and launch procedures below still apply. The approved plan governs work priority
and its explicitly requested changes, including the Clothing Setup UI rebuild.
If a genuine unresolved product decision conflicts with the plan, preserve the
specific conflict for the user; proceed with independent work that does not depend
on that decision. No commit, push or publication to an external service is implied.

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
