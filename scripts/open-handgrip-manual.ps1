param([switch]$SyncOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'validation/DazPoseUnityValidation'
$project = Join-Path $repo '.dazposewizard/handgrip-validation/project'
if (!(Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The existing isolated handgrip project is missing. Do not create a replacement silently.'
}
$owners = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/')) -and
    $_.CommandLine -notmatch '-adb2'
}
if ($owners) { throw 'Close the isolated HandGrip Unity editor before syncing; its main editor is unaffected.' }
$paths = @(
    'Assets/DazPose/Runtime/Performer/HandGrip',
    'Assets/DazPose/Editor/HandGrip',
    'Assets/DazPose/Runtime/Performer/SuccubusPerformer.cs',
    'Assets/DazPose/Runtime/Performer/PerformerMotionLayer.cs',
    'Assets/DazPose/Runtime/Performer/PerformerExpressionLayer.cs',
    'Assets/FirstPerformanceVoid/FirstPerformanceVoidControls.cs',
    'Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset',
    'Assets/Scenes/HandGripAcceptance.unity',
    'Packages/manifest.json', 'Packages/packages-lock.json'
)
foreach ($relative in $paths) {
    $from = Join-Path $source $relative
    $to = Join-Path $project $relative
    if (Test-Path -LiteralPath $from -PathType Container) {
        Get-ChildItem -LiteralPath $from -File | Where-Object { $_.Extension -in '.cs', '.meta' } |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $to $_.Name) -Force }
    } else {
        Copy-Item -LiteralPath $from -Destination $to -Force
        if (Test-Path -LiteralPath ($from + '.meta')) {
            Copy-Item -LiteralPath ($from + '.meta') -Destination ($to + '.meta') -Force
        }
    }
}
Write-Output "Synced grip sources and fixture into $project"
if (!$SyncOnly) {
    Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe' -WindowStyle Hidden -ArgumentList @(
        '-projectPath', ('"' + $project + '"'),
        '-logFile', ('"' + (Join-Path $repo '.dazposewizard/handgrip-validation/manual-editor.log') + '"')
    )
}
