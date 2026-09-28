# Running DazPoseTool for manual testing

On this Windows machine, the default sandbox PowerShell runs as `CodexSandboxOffline`. Starting the GUI from that shell can put the window on an invisible desktop, so a running process alone does not mean the user can test it.

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
