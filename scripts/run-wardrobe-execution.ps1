[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Runner','Contract','Migration','Runtime','Layers','Setup','Persistence','Integration','Player')]
    [string]$Stage,
    [string]$Unity='C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$mainProject = Join-Path $taskRoot 'validation\DazPoseUnityValidation'
$isolatedProject = Join-Path $taskRoot '.dazposewizard\p0c-native-generation'
$unityProjectFile = Join-Path $isolatedProject 'ProjectSettings\ProjectVersion.txt'
if (!(Test-Path -LiteralPath $unityProjectFile)) {
    throw "Isolated Unity project is missing: $isolatedProject"
}
if (!(Test-Path -LiteralPath $Unity)) { throw "Unity executable is missing: $Unity" }

$output = Join-Path $isolatedProject ("TestOutput\wardrobe-execution\" + $Stage.ToLowerInvariant())
New-Item -ItemType Directory -Force -Path $output | Out-Null
$report = Join-Path $output 'report.json'
$log = Join-Path $output ('unity-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.log')
$lockPath = Join-Path $isolatedProject 'wardrobe-import.lock'
$lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)

function Get-TreeHash([string[]]$Roots) {
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($root in $Roots) {
        if (!(Test-Path -LiteralPath $root)) { continue }
        $resolved = (Resolve-Path -LiteralPath $root).Path.TrimEnd('\')
        $items = if ((Get-Item -LiteralPath $resolved).PSIsContainer) {
            Get-ChildItem -LiteralPath $resolved -Recurse -File
        } else { Get-Item -LiteralPath $resolved }
        foreach ($item in $items | Sort-Object FullName) {
            $relative = $item.FullName.Substring($resolved.Length).TrimStart('\')
            $lines.Add(($relative.ToLowerInvariant() + ':' + (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()))
        }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Copy-Changed([string]$Source, [string]$Target) {
    if ((Test-Path -LiteralPath $Target) -and
        (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash) { return }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Target) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Target -Force
}

function Run-EditorMethod([string]$Method, [string]$RunStage, [string]$SourceHash, [string]$InputHash, [bool]$Gpu) {
    $start = [DateTime]::UtcNow
    $runOutput = Join-Path $isolatedProject ("TestOutput\wardrobe-execution\" + $RunStage.ToLowerInvariant())
    New-Item -ItemType Directory -Force -Path $runOutput | Out-Null
    $runReport = Join-Path $runOutput 'report.json'
    $runLog = Join-Path $runOutput ('unity-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.log')
    if (Test-Path -LiteralPath $runReport) { Remove-Item -LiteralPath $runReport -Force }
    $active = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Contains($isolatedProject)
    }
    if ($active) { throw 'The isolated validation project already has an active Unity owner. Wait for it; do not kill it.' }

    $arguments = @(
        '-batchmode', '-projectPath', ('"' + $isolatedProject + '"'),
        '-executeMethod', $Method,
        '-wardrobeExecutionStage', $RunStage,
        '-wardrobeExecutionReport', ('"' + $runReport + '"'),
        '-wardrobeExecutionSourceHash', $SourceHash,
        '-wardrobeExecutionInputHash', $InputHash,
        '-logFile', ('"' + $runLog + '"')
    )
    if (!$Gpu) { $arguments += '-nographics' }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Unity
    $info.Arguments = $arguments -join ' '
    $info.WorkingDirectory = $taskRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    if (!$process.Start()) { throw "Unity did not start: $Method" }
    if (!$process.WaitForExit(900000)) {
        try { $process.Kill() } catch {}
        throw "Unity timed out in $Method. Its isolated-project process was stopped; inspect $runLog"
    }
    $exitCode = $process.ExitCode
    if (!(Test-Path -LiteralPath $runReport)) { throw "Unity produced no fresh report for $Method (exit $exitCode). See $runLog" }
    $result = Get-Content -LiteralPath $runReport -Raw | ConvertFrom-Json
    if ($result.stage -ne $RunStage -or $result.sourceHash -ne $SourceHash -or $result.inputAssetHashes -notcontains $InputHash) {
        throw "Report identity/hash mismatch for $Method. See $runReport"
    }
    if ($result.complete -ne $true -or $result.passed -ne $true -or $exitCode -ne 0) {
        throw "Wardrobe gate '$RunStage' failed in $Method (Unity exit $exitCode). See $runReport and $runLog"
    }
    if (@($result.assertions | Where-Object { !$_.passed }).Count -gt 0) { throw "A required assertion failed in $runReport" }
    Write-Output "WARDROBE_GATE_PASS: $RunStage ($(@($result.assertions).Count) assertions); report $runReport"
}

try {
    $sourceRoots = @(
        (Join-Path $mainProject 'Assets\DazPose'),
        (Join-Path $mainProject 'Assets\FirstPerformanceVoid')
    )
    $sourceAssetFiles = foreach ($sourceRoot in $sourceRoots) {
        Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {
            $_.Extension -eq '.cs' -or $_.Name -like '*.cs.meta' -or $_.FullName.Contains('\Effects\Dissolve\Shaders\')
        }
    }
    # The isolated validation project must compile against the same pinned package graph as the main project.
    $sourcePackageFiles = @('manifest.json', 'packages-lock.json') | ForEach-Object {
        $path = Join-Path $mainProject ('Packages\' + $_)
        if (Test-Path -LiteralPath $path) { Get-Item -LiteralPath $path }
    }
    $sourceFiles = @($sourceAssetFiles) + @($sourcePackageFiles)
    foreach ($file in $sourceFiles) { Copy-Changed $file.FullName (Join-Path $isolatedProject $file.FullName.Substring($mainProject.Length + 1)) }

    $sourceManifest = [Collections.Generic.List[string]]::new()
    foreach ($sourceFile in $sourceFiles | Sort-Object FullName) {
        $relative = $sourceFile.FullName.Substring($mainProject.Length + 1).Replace('\', '/').ToLowerInvariant()
        $contentHash = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $sourceManifest.Add($relative + ':' + $contentHash)
    }
    $sourceBytes = [Text.Encoding]::UTF8.GetBytes(($sourceManifest -join "`n"))
    $sourceHasher = [Security.Cryptography.SHA256]::Create()
    try { $sourceHash = [BitConverter]::ToString($sourceHasher.ComputeHash($sourceBytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sourceHasher.Dispose() }
    $inputRoots = @(
        (Join-Path $isolatedProject 'Assets\TestData\Wardrobe\first-outfit'),
        (Join-Path $isolatedProject 'Assets\TestData\Wardrobe\second-outfit'),
        (Join-Path $isolatedProject 'Assets\TestData\Wardrobe\third-outfit'),
        (Join-Path $isolatedProject 'Assets\Wardrobe'),
        (Join-Path $isolatedProject 'Assets\TestData\LaraCandidate\FirstPerformanceVoidLaraCandidate.unity'),
        (Join-Path $isolatedProject 'Assets\TestData\LaraCandidate\manifest.json')
    )
    $inputHash = Get-TreeHash $inputRoots

    $methods = @{
        Runner='DazPose.UnityValidation.WardrobeExecutionValidation.Runner'
        Contract='DazPose.UnityValidation.WardrobeExecutionValidation.Contract'
        Migration='DazPose.UnityValidation.WardrobeExecutionValidation.Migration'
        Runtime='DazPose.UnityValidation.WardrobeExecutionValidation.Runtime'
        Layers='DazPose.UnityValidation.WardrobeExecutionValidation.Layers'
        Setup='DazPose.UnityValidation.WardrobeExecutionValidation.Setup'
        Persistence='DazPose.UnityValidation.WardrobeExecutionValidation.Persistence'
        Integration='DazPose.UnityValidation.WardrobeExecutionValidation.Integration'
        Player='DazPose.UnityValidation.WardrobeExecutionValidation.Player'
    }
    $gpuStages = @('Runtime','Layers','Setup','Integration')
    if ($Stage -eq 'Layers' -or $Stage -eq 'Setup' -or $Stage -eq 'Integration' -or $Stage -eq 'Player') {
        # These gates consume fresh live runtime evidence; run it from the same copied source snapshot.
        Run-EditorMethod $methods['Runtime'] 'Runtime' $sourceHash $inputHash $true
    }
    Run-EditorMethod $methods[$Stage] $Stage $sourceHash $inputHash ($gpuStages -contains $Stage)
    if ($Stage -eq 'Integration') {
        # Integration is not complete until the standalone player proves serialized runtime-only assets.
        Run-EditorMethod $methods['Player'] 'Player' $sourceHash $inputHash $false
    }
} finally {
    $lock.Dispose()
}
