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
