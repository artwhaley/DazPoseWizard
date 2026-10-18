param([switch]$SyncOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'validation/DazPoseUnityValidation'
$project = Join-Path $repo '.dazposewizard/handgrip-validation/project'
if (!(Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The existing isolated handgrip project is missing. Do not create a replacement silently.'
}
. (Join-Path $PSScriptRoot 'RemediationSourceMoveSync.ps1')
$null = Remove-StaleRemediationSourceCopies -RepositoryRoot $repo -StagingProjectRoot $project
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
    'Assets/DazPose/Validation/Legacy/FirstPerformanceVoidControls.cs',
    'Assets/Production/Performers/Lara/Contact/LaraRightHandGrip.asset',
    'Assets/Scenes/HandGripAcceptance.unity',
    'Packages/manifest.json', 'Packages/packages-lock.json'
)
$codeRoots = @('Assets/DazPose','Assets/Player','Assets/FirstPerformanceVoid') | ForEach-Object { Join-Path $source $_ }
$codeFiles = foreach ($root in $codeRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $_.Extension -eq '.cs' -or $_.Name -like '*.cs.meta' -or
        $_.Extension -in @('.asmdef','.asmref') -or $_.Name -like '*.asmdef.meta' -or $_.Name -like '*.asmref.meta'
    }
}
$codeFolderMetas = foreach ($root in $codeRoots) {
    $folders = @((Get-Item -LiteralPath $root)) + @(Get-ChildItem -LiteralPath $root -Directory -Recurse -Force)
    foreach ($folder in $folders) {
        $folderMeta = $folder.FullName + '.meta'
        if (Test-Path -LiteralPath $folderMeta -PathType Leaf) { Get-Item -LiteralPath $folderMeta }
    }
}
$codeFiles += @($codeFolderMetas)
$adapterRoot = Join-Path $source 'Assets/Daz3D/Scripts/Editor'
foreach ($relativeAdapter in @('DazPoseDtuConversionAdapter.cs','DazPoseDtuConversionAdapter.cs.meta')) {
    $adapterPath = Join-Path $adapterRoot $relativeAdapter
    if (Test-Path -LiteralPath $adapterPath) { $codeFiles += Get-Item -LiteralPath $adapterPath }
}
foreach ($file in $codeFiles) {
    $relative = $file.FullName.Substring($source.Length + 1)
    $target = Join-Path $project $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
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
