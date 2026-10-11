[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('R01','R02','R03','R04','R05','R06','R07','R08','R09','R10','R11','R12','R13')]
    [string]$Ticket,
    [switch]$AuthoringB01,
    [switch]$AuthoringB02,
    [switch]$AuthoringB03,
    [switch]$AuthoringB04,
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$mainProject = Join-Path $repo 'validation\DazPoseUnityValidation'
$isolatedProject = Join-Path $repo '.dazposewizard\remediation-validation'
$evidenceRoot = Join-Path $repo ('.dazposewizard\remediation-evidence\' + $Ticket)
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$runRoot = Join-Path $evidenceRoot $runId
$baselineCommit = '18b268afe4df1ce11046a760b757002571a104c6'
$baselineRecord = Join-Path $repo 'docs\production-consolidation\remediation\baseline.json'
$lockPath = Join-Path $isolatedProject 'remediation-validation.lock'
. (Join-Path $PSScriptRoot 'RemediationSourceMoveSync.ps1')

function Get-Hash([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function New-AssertionRecord([string]$Name, [string]$Expected, [string]$Actual, [bool]$Passed) {
    return [pscustomobject]@{ name=$Name; expected=$Expected; actual=$Actual; passed=$Passed }
}

function Invoke-R12Player([string]$Executable, [string]$LogPath, [string]$AdditionalArguments) {
    if (!(Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "R12 player executable is missing: $Executable" }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
    $resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
    $arguments = '-force-d3d12 -logFile "' + $LogPath + '"'
    if (![string]::IsNullOrWhiteSpace($AdditionalArguments)) { $arguments += ' ' + $AdditionalArguments }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $resolvedExecutable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $resolvedExecutable) -PassThru -WindowStyle Hidden
    if ($null -eq $process) { throw "R12 player did not start: $resolvedExecutable" }
    $waited = $process.WaitForExit(180000)
    if (!$waited) {
        $process.Refresh()
        if (!$process.HasExited) {
            $actualPath = $process.Path
            if ($actualPath -and [IO.Path]::GetFullPath($actualPath) -eq [IO.Path]::GetFullPath($resolvedExecutable)) {
                $process.Kill()
                [void]$process.WaitForExit(5000)
            }
        }
    }
    $timer.Stop()
    $process.Refresh()
    $exitCode = if ($waited -and $process.HasExited) { $process.ExitCode } else { -1 }
    $logText = if (Test-Path -LiteralPath $LogPath) { [IO.File]::ReadAllText($LogPath) } else { '' }
    $d3d12 = $logText.Contains('Forcing GfxDevice: Direct3D 12') -or $logText -match '(?s)Direct3D:\s*Version:\s*Direct3D 12'
    [pscustomobject]@{
        executable = $resolvedExecutable; processId = $process.Id; exited = ($waited -and $process.HasExited)
        timedOut = !$waited; exitCode = $exitCode; elapsedSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3)
        logPath = $LogPath; logExists = (Test-Path -LiteralPath $LogPath); graphicsDeviceDirect3D12 = $d3d12; logText = $logText
    }
}

function Get-TreeManifest([string]$root) {
    $resolved = (Resolve-Path -LiteralPath $root).Path.TrimEnd('\')
    $files = Get-ChildItem -LiteralPath $resolved -Recurse -File | Sort-Object FullName
    $items = foreach ($file in $files) {
        [pscustomobject]@{
            path = $file.FullName.Substring($resolved.Length).TrimStart('\').Replace('\','/')
            sha256 = Get-Hash $file.FullName
            bytes = $file.Length
        }
    }
    $lines = @($items | ForEach-Object { $_.path.ToLowerInvariant() + ':' + $_.sha256 })
    $shaBytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $treeHash = [BitConverter]::ToString($hasher.ComputeHash($shaBytes)).Replace('-','').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    [pscustomobject]@{ root = $resolved; treeHash = $treeHash; fileCount = @($items).Count; files = @($items) }
}

function Copy-ProjectInputs {
    if ($Ticket -eq 'R11') {
        $progress = Get-Content -LiteralPath (Join-Path $repo 'docs\production-consolidation\remediation\progress.json') -Raw | ConvertFrom-Json
        if ($progress.tickets.R10.status -ne 'passed') { throw 'R11 requires the recorded passing R10 manifest gate.' }
        Copy-Item -LiteralPath (Join-Path $repo 'docs\production-consolidation\remediation\SOURCE_MOVE_MANIFEST.json') -Destination (Join-Path $runRoot 'R10-SOURCE_MOVE_MANIFEST.json') -Force
        Remove-StaleRemediationSourceCopies -RepositoryRoot $repo -StagingProjectRoot $isolatedProject | Out-Null
    }
    New-Item -ItemType Directory -Force -Path $isolatedProject | Out-Null
    foreach ($name in @('Assets','Packages','ProjectSettings')) {
        $source = Join-Path $mainProject $name
        $target = Join-Path $isolatedProject $name
        if (!(Test-Path -LiteralPath $source)) { throw "Required input root is missing: $source" }
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $args = @($source, $target, '/E', '/COPY:DAT', '/DCOPY:DAT', '/R:2', '/W:2', '/XJ', '/NFL', '/NDL', '/NP', '/XD', 'Library', 'Temp', 'Logs', 'TestOutput', 'UserSettings')
        & robocopy @args | Out-Null
        if ($LASTEXITCODE -gt 7) { throw "Robocopy failed for $name with exit $LASTEXITCODE" }
    }
    Remove-MisclassifiedPreflightRunnerCopy -RepositoryRoot $repo -StagingProjectRoot $isolatedProject | Out-Null
    if ($Ticket -eq 'R11') {
        $legacySentinel = Join-Path $isolatedProject 'Assets\TestOutput\Remediation\R11\ValidationAssemblyBuildSentinel.cs'
        if (Test-Path -LiteralPath $legacySentinel) {
            if (!(Get-Content -LiteralPath $legacySentinel -Raw).Contains('// R11 isolated staging-only build sentinel.')) {
                throw "Preserved unexpected isolated staging file: $legacySentinel"
            }
            Remove-Item -LiteralPath $legacySentinel
            if (Test-Path -LiteralPath ($legacySentinel + '.meta')) { Remove-Item -LiteralPath ($legacySentinel + '.meta') }
        }
        $sentinel = Join-Path $isolatedProject 'Assets\DazPose\Validation\RemediationAssemblyBuildSentinel.cs'
        $sentinelMeta = $sentinel + '.meta'
        $marker = '// R11 isolated staging-only build sentinel.'
        $sentinelText = @'
// R11 isolated staging-only build sentinel.
#if UNITY_EDITOR || DAZPOSE_VALIDATION
using UnityEngine;
namespace DazPose.Validation
{
    public sealed class RemediationAssemblyBuildSentinel : MonoBehaviour { }
}
#endif
'@
        if (Test-Path -LiteralPath $sentinel) {
            if (!(Get-Content -LiteralPath $sentinel -Raw).Contains($marker)) { throw "Preserved unexpected isolated staging file: $sentinel" }
        } else {
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $sentinel) | Out-Null
            [IO.File]::WriteAllText($sentinel, $sentinelText, [Text.UTF8Encoding]::new($false))
        }
        if (!(Test-Path -LiteralPath $sentinelMeta)) {
            $allocated = [Guid]::NewGuid().ToString('N')
            $metaText = "fileFormatVersion: 2`nguid: $allocated`nMonoImporter:`n  externalObjects: {}`n  serializedVersion: 2`n  defaultReferences: []`n  executionOrder: 0`n  icon: {instanceID: 0}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n"
            [IO.File]::WriteAllText($sentinelMeta, $metaText, [Text.UTF8Encoding]::new($false))
        }
    }
}

function Assert-NoIsolatedOwner {
    try {
        $owner = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
            $_.CommandLine -and $_.CommandLine.Contains($isolatedProject)
        }
        if ($owner) { throw "The isolated project already has a Unity owner (PID $($owner.ProcessId)); wait and do not kill it." }
    }
    catch {
        if ($_.Exception.Message -like '*already has a Unity owner*') { throw }
        $activeUnity = Get-Process -Name Unity -ErrorAction SilentlyContinue
        if ($activeUnity) { throw 'Could not verify the Unity command line and at least one Unity process is active; wait rather than risking an overlapping project owner.' }
    }
}

New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
$lock = $null
$started = [DateTime]::UtcNow
$exitCode = 1
try {
    if (!(Test-Path -LiteralPath $Unity)) { throw "Unity executable is missing: $Unity" }
    $baseline = Get-Content -LiteralPath $baselineRecord -Raw | ConvertFrom-Json
    if ($baseline.commit -ne $baselineCommit) { throw 'baseline.json does not identify the approved recovery commit.' }
    $gitType = (& git -c safe.directory=$repo cat-file -t $baselineCommit 2>$null).Trim()
    if ($gitType -ne 'commit') { throw "Approved baseline commit is unavailable: $baselineCommit" }
    $archiveHash = Get-Hash $baseline.archive
    if ($archiveHash -ne $baseline.archiveSha256) { throw 'Recovery archive hash does not match baseline.json.' }
    $archiveManifest = Get-Content -LiteralPath $baseline.manifest -Raw | ConvertFrom-Json
    if ($archiveManifest.passed -ne $true) { throw 'Recovery archive manifest is not marked passed.' }

    if ($Ticket -ne 'R10') {
        Assert-NoIsolatedOwner
        New-Item -ItemType File -Force -Path $lockPath | Out-Null
        $lock = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        Copy-ProjectInputs
    }
    $mainManifest = Get-TreeManifest (Join-Path $mainProject 'Assets')
    $packagesManifest = Get-TreeManifest (Join-Path $mainProject 'Packages')
    $settingsManifest = Get-TreeManifest (Join-Path $mainProject 'ProjectSettings')
    $allManifests = @($mainManifest, $packagesManifest, $settingsManifest)
    $guidRows = foreach ($file in Get-ChildItem -LiteralPath (Join-Path $mainProject 'Assets') -Recurse -File -Filter '*.cs.meta' | Sort-Object FullName) {
        $guid = Select-String -LiteralPath $file.FullName -Pattern '^guid:\s*([0-9a-fA-F]+)' | Select-Object -First 1
        [pscustomobject]@{ path = $file.FullName.Substring($mainProject.Length + 1).Replace('\','/'); guid = if ($guid) { $guid.Matches[0].Groups[1].Value.ToLowerInvariant() } else { $null } }
    }
    $inputRecord = [pscustomobject]@{
        capturedUtc = $started.ToString('O'); baselineCommit = $baselineCommit; manifests = $allManifests
        scriptGuids = @($guidRows); currentUserEdits = @('Second-outfit stockings/shells/shoes remain Layer 1; other current layer assignments preserved.')
    }
    $inputRecordPath = Join-Path $runRoot 'current-input-manifest.json'
    $inputRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $inputRecordPath -Encoding UTF8

    $sourceLines = foreach ($manifest in $allManifests) { $manifest.treeHash }
    $sourceHashBytes = [Text.Encoding]::UTF8.GetBytes(($sourceLines -join "`n"))
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $sourceHash = [BitConverter]::ToString($hasher.ComputeHash($sourceHashBytes)).Replace('-','').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    $inputHash = $mainManifest.treeHash
    $sourceHashPath = Join-Path $runRoot 'source-input-hashes.json'
    [pscustomobject]@{ sourceHash = $sourceHash; inputHash = $inputHash; assets = $mainManifest.treeHash; packages = $packagesManifest.treeHash; projectSettings = $settingsManifest.treeHash } |
        ConvertTo-Json | Set-Content -LiteralPath $sourceHashPath -Encoding UTF8

    $report = Join-Path $runRoot 'report.json'
    if ($Ticket -eq 'R10') {
        & (Join-Path $repo 'scripts\generate-remediation-source-move-manifest.ps1') | Out-Null
        $manifestPath = Join-Path $repo 'docs\production-consolidation\remediation\SOURCE_MOVE_MANIFEST.json'
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $checks = [System.Collections.Generic.List[object]]::new()
        $addCheck = { param($name,$expected,$actual,$passed) $checks.Add((New-AssertionRecord -Name $name -Expected ([string]$expected) -Actual ([string]$actual) -Passed ([bool]$passed))) }

        $sourceRoots = @('DazPose','Player','FirstPerformanceVoid') | ForEach-Object { Join-Path $mainProject ('Assets\' + $_) }
        $currentSources = @(Get-ChildItem -LiteralPath $sourceRoots -Recurse -File -Force -Filter '*.cs' | ForEach-Object { $_.FullName.Substring($repo.Length + 1).Replace('\','/') } | Sort-Object)
        $currentSourceMetas = @(Get-ChildItem -LiteralPath $sourceRoots -Recurse -File -Force -Filter '*.cs.meta' | ForEach-Object { $_.FullName.Substring($repo.Length + 1).Replace('\','/') } | Sort-Object)
        $manifestSources = @($manifest.files | ForEach-Object source | Sort-Object)
        $manifestMetas = @($manifest.files | ForEach-Object meta | Sort-Object)
        $sourceCoverageExact = !(Compare-Object -ReferenceObject $currentSources -DifferenceObject $manifestSources)
        $metaCoverageExact = !(Compare-Object -ReferenceObject $currentSourceMetas -DifferenceObject $manifestMetas)
        & $addCheck 'everyCurrentSourceInventoriedOnce' "$($currentSources.Count) sources" "$($manifestSources.Count) rows; exactPathSet=$sourceCoverageExact" ($sourceCoverageExact -and $currentSources.Count -eq $manifestSources.Count -and @($manifestSources | Group-Object | Where-Object Count -gt 1).Count -eq 0)
        & $addCheck 'everyCurrentSourceMetaInventoriedOnce' "$($currentSourceMetas.Count) metas" "$($manifestMetas.Count) rows; exactPathSet=$metaCoverageExact" ($metaCoverageExact -and $currentSourceMetas.Count -eq $manifestMetas.Count)

        $hashMismatches = [System.Collections.Generic.List[string]]::new()
        foreach ($file in $manifest.files) {
            $sourcePath = Join-Path $repo $file.source
            $metaPath = Join-Path $repo $file.meta
            if (!(Test-Path -LiteralPath $sourcePath) -or !(Test-Path -LiteralPath $metaPath)) { $hashMismatches.Add($file.source + ' missing'); continue }
            if ((Get-Hash $sourcePath) -ne $file.sourceSha256) { $hashMismatches.Add($file.source + ' source hash') }
            if ((Get-Hash $metaPath) -ne $file.metaSha256) { $hashMismatches.Add($file.source + ' meta hash') }
        }
        & $addCheck 'sourceAndMetaHashesCurrent' 'all 267 source/meta hashes match' "$($hashMismatches.Count) mismatches" ($hashMismatches.Count -eq 0 -and $manifest.files.Count -eq $currentSources.Count)

        $duplicateDestinations = @($manifest.files | Group-Object { $_.destination.ToLowerInvariant() } | Where-Object Count -gt 1)
        $sourceGuids = @($manifest.files | ForEach-Object { $_.guid.ToLowerInvariant() })
        $plannedGuids = @($manifest.guidAllocations.PSObject.Properties | ForEach-Object { [string]$_.Value.ToLowerInvariant() })
        $allAssets = Join-Path $mainProject 'Assets'
        $existingGuidSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($metaFile in Get-ChildItem -LiteralPath $allAssets -Recurse -File -Force -Filter '*.meta') {
            $guidMatch = Select-String -LiteralPath $metaFile.FullName -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' | Select-Object -First 1
            if ($guidMatch) { [void]$existingGuidSet.Add($guidMatch.Matches[0].Groups[1].Value) }
        }
        $allocatedCollision = @($plannedGuids | Where-Object { $existingGuidSet.Contains($_) })
        $guidFormatValid = @($plannedGuids | Where-Object { $_ -notmatch '^[0-9a-f]{32}$' }).Count -eq 0
        $uniquePlannedGuids = @($plannedGuids | Group-Object | Where-Object Count -gt 1).Count -eq 0
        & $addCheck 'destinationPathsUnique' 'no case-insensitive destination collision' "$($duplicateDestinations.Count) collisions" ($duplicateDestinations.Count -eq 0)
        & $addCheck 'sourceGuidsUniqueAndPresent' 'all source meta GUIDs are unique 32-hex identifiers' "sourceGuids=$($sourceGuids.Count); unique=$(@($sourceGuids | Group-Object | Where-Object Count -gt 1).Count -eq 0)" ($sourceGuids.Count -eq $currentSources.Count -and @($sourceGuids | Where-Object { $_ -notmatch '^[0-9a-f]{32}$' }).Count -eq 0 -and @($sourceGuids | Group-Object | Where-Object Count -gt 1).Count -eq 0)
        & $addCheck 'plannedGuidsUniqueAndUnused' 'every allocated GUID is unique, valid and absent from current Assets metas' "allocations=$($plannedGuids.Count); collisions=$($allocatedCollision.Count); invalid=$(@($plannedGuids | Where-Object { $_ -notmatch '^[0-9a-f]{32}$' }).Count)" ($guidFormatValid -and $uniquePlannedGuids -and $allocatedCollision.Count -eq 0)

        $requiredAssemblies = @('DazPose.Runtime','DazPose.Editor','DazPose.Validation','DazPose.Validation.Editor','DazPose.Examples')
        $actualAssemblies = @($manifest.assemblyDefinitions | ForEach-Object name | Sort-Object)
        & $addCheck 'fiveAsmdefsHaveExactNames' ($requiredAssemblies -join ', ') ($actualAssemblies -join ', ') (!(Compare-Object ($requiredAssemblies | Sort-Object) $actualAssemblies) -and $manifest.assemblyDefinitions.Count -eq 5)
        $requiredAsmrefs = @('Assets/Player/Runtime/DazPose.Runtime.asmref','Assets/Player/Editor/DazPose.Editor.asmref','Assets/FirstPerformanceVoid/Editor/DazPose.Editor.asmref')
        $actualAsmrefs = @($manifest.assemblyReferences | ForEach-Object path | Sort-Object)
        & $addCheck 'threeAsmrefsTargetFixedAssemblies' ($requiredAsmrefs -join ', ') ($actualAsmrefs -join ', ') (!(Compare-Object ($requiredAsmrefs | Sort-Object) $actualAsmrefs) -and $manifest.assemblyReferences.Count -eq 3)

        $packageFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($package in $manifest.packageAssemblies) {
            $asmdefPath = Join-Path $repo $package.assetPath
            $metaPath = $asmdefPath + '.meta'
            if (!(Test-Path -LiteralPath $asmdefPath) -or !(Test-Path -LiteralPath $metaPath)) { $packageFailures.Add($package.assembly + ' missing asmdef/meta'); continue }
            $packageMeta = Select-String -LiteralPath $metaPath -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' | Select-Object -First 1
            if (!$packageMeta -or $packageMeta.Matches[0].Groups[1].Value -ne $package.guid) { $packageFailures.Add($package.assembly + ' GUID mismatch') }
            $asmdefJson = Get-Content -LiteralPath $asmdefPath -Raw | ConvertFrom-Json
            if ($asmdefJson.name -ne $package.assembly) { $packageFailures.Add($package.assembly + ' name mismatch') }
        }
        & $addCheck 'packageAsmdefsAndGuidsResolve' 'every listed package assembly resolves by exact name/GUID' "$($packageFailures.Count) mismatches" ($packageFailures.Count -eq 0)

        $fixedDependencies = @{
            'DazPose.Runtime'=@(); 'DazPose.Editor'=@('DazPose.Runtime','DazPose.Validation','DazPose.Examples')
            'DazPose.Validation'=@('DazPose.Runtime','DazPose.Examples')
            'DazPose.Validation.Editor'=@('DazPose.Runtime','DazPose.Validation','DazPose.Editor','DazPose.Examples')
            'DazPose.Examples'=@('DazPose.Runtime')
        }
        $assemblyGuid = @{}; foreach ($definition in $manifest.assemblyDefinitions) { $assemblyGuid[$definition.name] = $definition.guid }
        $packageGuid = @{}; foreach ($package in $manifest.packageAssemblies) { $packageGuid[$package.assembly] = $package.guid }
        $referenceFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($definition in $manifest.assemblyDefinitions) {
            $expectedReferences = [System.Collections.Generic.List[string]]::new()
            foreach ($dependency in $fixedDependencies[$definition.name]) { $expectedReferences.Add('GUID:' + $assemblyGuid[$dependency]) }
            $imports = $manifest.assemblyImportsByAssembly | Where-Object assembly -eq $definition.name | Select-Object -ExpandProperty directImports
            foreach ($import in $imports) { if ($packageGuid.ContainsKey([string]$import)) { $expectedReferences.Add('GUID:' + $packageGuid[[string]$import]) } }
            $expectedSorted = @($expectedReferences | Sort-Object -Unique)
            $actualSorted = @($definition.references | Sort-Object -Unique)
            if (Compare-Object $expectedSorted $actualSorted) { $referenceFailures.Add($definition.name + ' references') }
            if ((ConvertTo-Json -InputObject $definition.unityPayload -Depth 10 -Compress) -ne (ConvertTo-Json -InputObject ([ordered]@{
                name=$definition.name; references=@($definition.references); includePlatforms=@($definition.includePlatforms); excludePlatforms=@($definition.excludePlatforms)
                allowUnsafeCode=$definition.allowUnsafeCode; overrideReferences=$definition.overrideReferences; precompiledReferences=@($definition.precompiledReferences)
                autoReferenced=$definition.autoReferenced; defineConstraints=@($definition.defineConstraints); versionDefines=@($definition.versionDefines); noEngineReferences=$definition.noEngineReferences
            }) -Depth 10 -Compress)) { $referenceFailures.Add($definition.name + ' payload mismatch') }
            if (!$definition.autoReferenced -or $definition.overrideReferences) { $referenceFailures.Add($definition.name + ' auto/override settings') }
        }
        & $addCheck 'exactAsmdefPayloadAndReferenceArrays' 'all fixed first-party and directly used package GUID references match the inventory' "$($referenceFailures.Count) mismatches" ($referenceFailures.Count -eq 0)

        $asmrefFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($asmref in $manifest.assemblyReferences) {
            if ($asmref.reference -ne ('GUID:' + $assemblyGuid[$asmref.target])) { $asmrefFailures.Add($asmref.path + ' target mismatch') }
            if ($asmref.payload.reference -ne $asmref.reference) { $asmrefFailures.Add($asmref.path + ' payload mismatch') }
            if ($plannedGuids -notcontains ([string]$asmref.guid).ToLowerInvariant()) { $asmrefFailures.Add($asmref.path + ' meta GUID missing from allocation ledger') }
        }
        & $addCheck 'asmrefPayloadsAndMetaGuidsExact' 'all asmref target GUIDs and allocated meta GUIDs resolve' "$($asmrefFailures.Count) mismatches" ($asmrefFailures.Count -eq 0)

        $allowedEdges = @{
            'DazPose.Runtime'=@(); 'DazPose.Examples'=@('DazPose.Runtime')
            'DazPose.Editor'=@('DazPose.Runtime','DazPose.Validation','DazPose.Examples')
            'DazPose.Validation'=@('DazPose.Runtime','DazPose.Examples')
            'DazPose.Validation.Editor'=@('DazPose.Runtime','DazPose.Validation','DazPose.Editor','DazPose.Examples')
        }
        $edgeFailures = @($manifest.firstPartyAssemblyEdges | Where-Object { $_.to -notin $allowedEdges[$_.from] })
        $runtimeLeaks = @($manifest.firstPartyAssemblyEdges | Where-Object { $_.from -eq 'DazPose.Runtime' })
        & $addCheck 'firstPartyGraphHasNoForbiddenOrRuntimeLeak' 'only the one-way fixed assembly edges are present; Runtime has no first-party dependency' "edges=$($manifest.firstPartyAssemblyEdges.Count); forbidden=$($edgeFailures.Count); runtimeLeaks=$($runtimeLeaks.Count)" ($edgeFailures.Count -eq 0 -and $runtimeLeaks.Count -eq 0 -and $manifest.gate.unresolvedFirstPartyAssemblyEdges.Count -eq 0)
        $editorReferenceFailures = @($manifest.editorEntrypoints | Where-Object { $_.kind -eq 'MenuItem' -and [string]::IsNullOrWhiteSpace($_.method) })
        $executeTargets = @($manifest.editorEntrypoints | Where-Object kind -eq 'executeMethod')
        & $addCheck 'movedEditorEntrypointsRecorded' 'all moved menu methods are named and executeMethod targets resolve' "menuItems=$(@($manifest.editorEntrypoints | Where-Object kind -eq 'MenuItem').Count); unresolvedMethods=$($editorReferenceFailures.Count); executeMethods=$($executeTargets.Count)" ($editorReferenceFailures.Count -eq 0 -and $executeTargets.Count -ge 1)
        $pathReferenceFailures = @($manifest.sourcePathReferences | Where-Object { !$_.targetSource -or !$_.plannedAssetPath -or $_.plannedAssetPath -eq $_.currentAssetPath })
        & $addCheck 'literalMovedSourcePathsRecorded' 'every moved C# path reference identifies its new destination' "references=$($manifest.sourcePathReferences.Count); invalid=$($pathReferenceFailures.Count)" ($manifest.sourcePathReferences.Count -gt 0 -and $pathReferenceFailures.Count -eq 0)
        & $addCheck 'typeAndCompanionPlacementEvidenceComplete' "$($manifest.coverage.sourceCount) source rows and $($manifest.coverage.sourceCount) source meta rows with declared types, direct imports, and type placement evidence" "sources=$($manifest.files.Count); typedEvidence=$($manifest.typeClassificationEvidence.Count); missingImports=$(@($manifest.files | Where-Object { $null -eq $_.directTypeImports }).Count)" ($manifest.files.Count -eq $currentSources.Count -and $manifest.typeClassificationEvidence.Count -eq $manifest.files.declaredTypes.Count -and @($manifest.files | Where-Object { $null -eq $_.directTypeImports }).Count -eq 0)
        $repair = $manifest.dependencyRepairs | Where-Object id -eq 'vendor-daz3d-editor-boundary' | Select-Object -First 1
        $dazReferences = @($manifest.gate.requiredPredefinedAssemblyEdges | Where-Object namespace -eq 'Daz3D')
        & $addCheck 'predefinedDaz3DEditorEdgeHasExactRepair' 'both typed DAZ conversion callers have a narrow registered adapter repair and stable new script GUID' "uses=$($dazReferences.Count); repairSteps=$(@($repair.exactSteps).Count); adapterGUID=$($repair.introducedMetaGuid)" ($dazReferences.Count -eq 2 -and $repair -and @($repair.exactSteps).Count -ge 5 -and $repair.introducedMetaGuid -match '^[0-9a-f]{32}$')
        $reflectionRepair = $manifest.dependencyRepairs | Where-Object id -eq 'remediation-compiler-reflection-assembly' | Select-Object -First 1
        $reflectionRefs = @($manifest.reflectionAssemblyReferences | Where-Object {
            $_.source -like '*/RemediationValidation.cs' -and $_.type -eq 'DazPose.UnityValidation.WardrobeFitCompiler' -and
            $_.currentAssembly -eq 'Assembly-CSharp-Editor' -and $_.destinationAssembly -eq 'DazPose.Editor' -and
            $_.updatedAssemblyQualifiedName -eq 'DazPose.UnityValidation.WardrobeFitCompiler, DazPose.Editor' -and $_.status -eq 'classified'
        })
        $reflectionSourceCode = Get-Content -LiteralPath (Join-Path $repo $reflectionRepair.source) -Raw
        & $addCheck 'reflectionAssemblyEdgeClassifiedAndRepairSpecified' 'RemediationValidation reflection lookup is mapped to DazPose.Editor with an exact replacement' "classified=$($reflectionRefs.Count); repair=$($reflectionRepair.exactReplacement); sourceHasOldName=$($reflectionSourceCode.Contains('DazPose.UnityValidation.WardrobeFitCompiler, Assembly-CSharp-Editor'))" ($reflectionRefs.Count -eq 1 -and $reflectionRepair.destinationAssembly -eq 'DazPose.Editor' -and $reflectionRepair.exactReplacement -eq 'Type.GetType("DazPose.UnityValidation.WardrobeFitCompiler, DazPose.Editor")' -and $reflectionSourceCode.Contains('DazPose.UnityValidation.WardrobeFitCompiler, Assembly-CSharp-Editor'))
        $friendExpectations = @{
            'Assets/DazPose/Runtime/AssemblyInfo.cs'=@('DazPose.Editor','DazPose.Validation','DazPose.Validation.Editor')
            'Assets/DazPose/Editor/AssemblyInfo.cs'=@('DazPose.Validation.Editor','Assembly-CSharp-Editor')
        }
        $friendFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($friendInfo in $manifest.friendAssemblyInfos) {
            $expectedFriends = @($friendExpectations[$friendInfo.source] | Sort-Object)
            $actualFriends = @($friendInfo.friends | Sort-Object)
            if (Compare-Object $expectedFriends $actualFriends) { $friendFailures.Add($friendInfo.source + ' friends') }
            foreach ($friend in $expectedFriends) { if ($friendInfo.contents -notmatch ('InternalsVisibleTo\("' + [regex]::Escape($friend) + '"\)')) { $friendFailures.Add($friendInfo.source + ' declaration ' + $friend) } }
        }
        if ($manifest.newSourceMetas.Count -ne 4) { $friendFailures.Add('new source meta count') }
        foreach ($newSourceMeta in $manifest.newSourceMetas) {
            if ($plannedGuids -notcontains ([string]$newSourceMeta.guid).ToLowerInvariant()) { $friendFailures.Add($newSourceMeta.source + ' GUID allocation') }
            if ($newSourceMeta.payload -notmatch ('guid: ' + [regex]::Escape($newSourceMeta.guid))) { $friendFailures.Add($newSourceMeta.source + ' meta payload') }
        }
        & $addCheck 'newSourceMetasAndFriendsExact' 'bridge, adapter and both AssemblyInfo scripts have unique planned GUIDs and exact friend declarations' "$($manifest.newSourceMetas.Count) scripts; mismatches=$($friendFailures.Count)" ($friendFailures.Count -eq 0)

        $sourceCanonical = @($manifest.files | Sort-Object source | ForEach-Object { $_.source.ToLowerInvariant() + ':' + $_.sourceSha256 + ':' + $_.metaSha256 }) -join "`n"
        $sourceCanonicalBytes = [Text.Encoding]::UTF8.GetBytes($sourceCanonical)
        $sourceCanonicalHasher = [Security.Cryptography.SHA256]::Create()
        try { $currentManifestHash = [BitConverter]::ToString($sourceCanonicalHasher.ComputeHash($sourceCanonicalBytes)).Replace('-','').ToLowerInvariant() }
        finally { $sourceCanonicalHasher.Dispose() }
        & $addCheck 'manifestSourceTreeHashValid' $manifest.coverage.sourceTreeSha256 $currentManifestHash ($manifest.coverage.sourceTreeSha256 -eq $currentManifestHash)

        $passed = @($checks | Where-Object { !$_.passed }).Count -eq 0
        $result = [pscustomobject]@{
            ticket=$Ticket; startedUtc=$started.ToString('O'); completedUtc=[DateTime]::UtcNow.ToString('O')
            sourceHash=$sourceHash; inputAssetHashes=@($inputHash); complete=$true; passed=$passed
            exception=if($passed){$null}else{'One or more static source-move manifest assertions failed.'}
            runner='PowerShell read-only manifest gate'; manifestPath='docs/production-consolidation/remediation/SOURCE_MOVE_MANIFEST.json'
            manifestSourceTreeSha256=$manifest.coverage.sourceTreeSha256; assertions=@($checks)
        }
        $result | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath $report -Encoding UTF8
        $exitCode = if ($passed) { 0 } else { 1 }
    }
    else {
        Assert-NoIsolatedOwner
        $arguments = @('-batchmode','-projectPath',('"' + $isolatedProject + '"'),'-executeMethod','DazPose.Remediation.RemediationValidation.RunBatch','-remediationTicket',$Ticket,'-remediationReport',('"' + $report + '"'),'-remediationSourceHash',$sourceHash,'-remediationInputHash',$inputHash,'-logFile',('"' + (Join-Path $runRoot 'unity.log') + '"'))
        if ($Ticket -eq 'R11') {
            $arguments += @('-remediationMoveManifest',('"' + (Join-Path $runRoot 'R10-SOURCE_MOVE_MANIFEST.json') + '"'))
        }
        if ($Ticket -eq 'R03') {
            $priorEvidence = Get-ChildItem -LiteralPath (Join-Path $repo '.dazposewizard\remediation-evidence\R02') -Directory -ErrorAction Stop |
                Sort-Object Name -Descending | Select-Object -First 1
            if ($null -eq $priorEvidence) { throw 'R03 requires a completed R02 evidence directory.' }
            $priorReport = Join-Path $priorEvidence.FullName 'report.json'
            if (!(Test-Path -LiteralPath $priorReport)) { throw "R03 prior R02 report is missing: $priorReport" }
            $arguments += @('-remediationPriorReport',('"' + $priorReport + '"'))
        }
        if ($Ticket -eq 'R01') { $arguments += '-nographics' }
        if ($AuthoringB01) { $arguments += "-authoringB01" }
        if ($AuthoringB02) { $arguments += "-authoringB02" }
        if ($AuthoringB03) { $arguments += "-authoringB03" }
        if ($AuthoringB04) { $arguments += "-authoringB04" }
        $psi = [Diagnostics.ProcessStartInfo]::new(); $psi.FileName = $Unity; $psi.Arguments = $arguments -join ' '; $psi.WorkingDirectory = $repo; $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
        $process = [Diagnostics.Process]::new(); $process.StartInfo = $psi
        if (!$process.Start()) { throw 'Unity did not start.' }
        $watchdogMs = if ($Ticket -in @('R11','R12')) { 1200000 } else { 300000 }
        if (!$process.WaitForExit($watchdogMs)) { try { $process.Kill() } catch {}; throw "Unity exceeded the $([int]($watchdogMs / 1000)) second ticket watchdog. Log: $($runRoot)\unity.log" }
        $exitCode = $process.ExitCode
        if (!(Test-Path -LiteralPath $report)) { throw "Unity produced no fresh report (exit $exitCode). Log: $($runRoot)\unity.log" }
        $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if ($Ticket -eq 'R12') {
            $r12Checks = [System.Collections.Generic.List[object]]::new()
            $addR12 = { param($name,$expected,$actual,$passed) $r12Checks.Add((New-AssertionRecord -Name $name -Expected ([string]$expected) -Actual ([string]$actual) -Passed ([bool]$passed))) }
            $manifestPath = [string]$result.r12BuildManifestPath
            if ([string]::IsNullOrWhiteSpace($manifestPath) -or !(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
                throw "R12 build manifest is missing: $manifestPath"
            }
            $buildManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
            $validationBuild = $buildManifest.validation
            $productionBuild = $buildManifest.production

            $validationPlayerReportPath = Join-Path $runRoot 'validation-player-report.json'
            $validationPlayerLogPath = Join-Path $runRoot 'validation-player.log'
            $validationAdditionalArgs = '-remediationPlayerReport "' + $validationPlayerReportPath + '"'
            $validationRun = Invoke-R12Player ([string]$validationBuild.executablePath) $validationPlayerLogPath $validationAdditionalArgs
            $validationPlayerReport = $null
            if (Test-Path -LiteralPath $validationPlayerReportPath) {
                $validationPlayerReport = Get-Content -LiteralPath $validationPlayerReportPath -Raw | ConvertFrom-Json
            }
            & $addR12 'validationPlayerExitedZeroWithin180Seconds' 'exact validation player exits 0 before the external 180-second watchdog' `
                ('exit=' + $validationRun.exitCode + '; timedOut=' + $validationRun.timedOut + '; elapsedSeconds=' + $validationRun.elapsedSeconds + '; exe=' + $validationRun.executable) `
                ($validationRun.exited -and !$validationRun.timedOut -and $validationRun.exitCode -eq 0 -and $validationRun.elapsedSeconds -le 180)
            & $addR12 'validationPlayerUsedDirect3D12' 'graphics-enabled Direct3D12 runtime' `
                ('logDevice=' + $validationRun.graphicsDeviceDirect3D12 + '; reportDevice=' + $(if($validationPlayerReport){$validationPlayerReport.graphicsDevice}else{'missing'}) + '; log=' + $validationRun.logPath) `
                ($validationRun.graphicsDeviceDirect3D12 -and $validationPlayerReport -ne $null -and $validationPlayerReport.graphicsDevice.StartsWith('Direct3D12', [StringComparison]::Ordinal))
            $validationReportPassed = $validationPlayerReport -ne $null -and $validationPlayerReport.passed -eq $true `
                -and $validationPlayerReport.sourceHash -eq $sourceHash -and $validationPlayerReport.inputAssetHash -eq $inputHash
            & $addR12 'validationPlayerRuntimeReport' 'fresh passing report with matching source/input hashes' `
                ('exists=' + ($validationPlayerReport -ne $null) + '; passed=' + ($validationPlayerReport -and $validationPlayerReport.passed) `
                    + '; source=' + $(if($validationPlayerReport){$validationPlayerReport.sourceHash}else{'missing'}) `
                    + '; input=' + $(if($validationPlayerReport){$validationPlayerReport.inputAssetHash}else{'missing'}) `
                    + '; report=' + $validationPlayerReportPath) $validationReportPassed
            if ($validationPlayerReport -and $validationPlayerReport.assertions) {
                foreach ($assertion in $validationPlayerReport.assertions) {
                    $r12Checks.Add((New-AssertionRecord -Name ('validationPlayer.' + $assertion.name) -Expected ([string]$assertion.expected) `
                        -Actual ([string]$assertion.actual) -Passed ([bool]$assertion.passed)))
                }
            }

            $productionPlayerLogPath = Join-Path $runRoot 'production-player.log'
            $productionRun = Invoke-R12Player ([string]$productionBuild.executablePath) $productionPlayerLogPath ''
            $requiredProductionMarkers = @(
                'R12_PRODUCTION_SMOKE|READY|',
                'R12_PRODUCTION_SMOKE|SPEECH_STARTED|',
                'R12_PRODUCTION_SMOKE|STOPPED|',
                'R12_PRODUCTION_SMOKE|REPLAY_COMPLETE|',
                'R12_PRODUCTION_SMOKE|PASSED|'
            )
            $missingProductionMarkers = @($requiredProductionMarkers | Where-Object { !$productionRun.logText.Contains($_) })
            & $addR12 'productionPlayerExitedZeroWithin180Seconds' 'exact production player exits 0 before the external 180-second watchdog' `
                ('exit=' + $productionRun.exitCode + '; timedOut=' + $productionRun.timedOut + '; elapsedSeconds=' + $productionRun.elapsedSeconds + '; exe=' + $productionRun.executable) `
                ($productionRun.exited -and !$productionRun.timedOut -and $productionRun.exitCode -eq 0 -and $productionRun.elapsedSeconds -le 180)
            & $addR12 'productionPlayerUsedDirect3D12' 'graphics-enabled Direct3D12 runtime' `
                ('device=' + $productionRun.graphicsDeviceDirect3D12 + '; log=' + $productionRun.logPath) $productionRun.graphicsDeviceDirect3D12
            & $addR12 'productionReadySpeechStopReplayObserved' 'READY, speech start, Stop cancellation and successful replay markers in the external player log' `
                ('missing=' + ($missingProductionMarkers -join ',') + '; log=' + $productionRun.logPath) ($missingProductionMarkers.Count -eq 0)

            $validationManaged = Join-Path (Split-Path -Parent ([string]$validationBuild.executablePath)) `
                (([IO.Path]::GetFileNameWithoutExtension([string]$validationBuild.executablePath)) + '_Data\Managed')
            $productionManaged = Join-Path (Split-Path -Parent ([string]$productionBuild.executablePath)) `
                (([IO.Path]::GetFileNameWithoutExtension([string]$productionBuild.executablePath)) + '_Data\Managed')
            $validationDlls = if (Test-Path -LiteralPath $validationManaged) { @(Get-ChildItem -LiteralPath $validationManaged -File -Filter 'DazPose.Validation*.dll') } else { @() }
            $productionDlls = if (Test-Path -LiteralPath $productionManaged) { @(Get-ChildItem -LiteralPath $productionManaged -File -Filter 'DazPose.Validation*.dll') } else { @() }
            & $addR12 'validationPlayerContainsValidationAssembly' 'DazPose.Validation.dll present in validation player' `
                ('managed=' + $validationManaged + '; found=' + (($validationDlls | ForEach-Object Name) -join ',')) ($validationDlls.Count -gt 0)
            & $addR12 'productionPlayerContainsNoValidationAssembly' 'no DazPose.Validation assembly in production player managed files' `
                ('managed=' + $productionManaged + '; found=' + (($productionDlls | ForEach-Object Name) -join ',')) ($productionDlls.Count -eq 0)

            $referenceMap = @{}
            $assemblyReadFailures = [System.Collections.Generic.List[string]]::new()
            if (Test-Path -LiteralPath $productionManaged) {
                foreach ($dll in Get-ChildItem -LiteralPath $productionManaged -File -Filter '*.dll') {
                    try {
                        $loadedAssembly = [Reflection.Assembly]::LoadFile($dll.FullName)
                        $assemblyName = $loadedAssembly.GetName().Name
                        $referenceMap[$assemblyName] = @($loadedAssembly.GetReferencedAssemblies() | ForEach-Object { $_.Name })
                    } catch { $assemblyReadFailures.Add($dll.Name + ': ' + $_.Exception.Message) }
                }
            }
            $productionRoots = @('DazPose.Runtime','DazPose.Examples')
            $missingProductionRoots = @($productionRoots | Where-Object { !$referenceMap.ContainsKey($_) })
            $directValidationReferences = [System.Collections.Generic.List[string]]::new()
            foreach ($rootAssembly in $productionRoots) {
                if ($referenceMap.ContainsKey($rootAssembly)) {
                    foreach ($reference in $referenceMap[$rootAssembly]) {
                        if ($reference.StartsWith('DazPose.Validation', [StringComparison]::OrdinalIgnoreCase)) {
                            $directValidationReferences.Add($rootAssembly + ' -> ' + $reference)
                        }
                    }
                }
            }
            $closureValidationReferences = [System.Collections.Generic.List[string]]::new()
            $visitedAssemblies = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            $queue = [System.Collections.Generic.Queue[string]]::new()
            foreach ($rootAssembly in $productionRoots) { if ($referenceMap.ContainsKey($rootAssembly)) { $queue.Enqueue($rootAssembly) } }
            while ($queue.Count -gt 0) {
                $currentAssembly = $queue.Dequeue()
                if (!$visitedAssemblies.Add($currentAssembly) -or !$referenceMap.ContainsKey($currentAssembly)) { continue }
                foreach ($reference in $referenceMap[$currentAssembly]) {
                    if ($reference.StartsWith('DazPose.Validation', [StringComparison]::OrdinalIgnoreCase)) {
                        $closureValidationReferences.Add($currentAssembly + ' -> ' + $reference)
                    } elseif ($reference.StartsWith('DazPose.', [StringComparison]::OrdinalIgnoreCase) -and $referenceMap.ContainsKey($reference)) {
                        $queue.Enqueue($reference)
                    }
                }
            }
            & $addR12 'productionRuntimeAndExamplesHaveNoValidationReference' 'built Runtime and Examples metadata references omit DazPose.Validation' `
                ('missingRoots=' + ($missingProductionRoots -join ',') + '; directRefs=' + ($directValidationReferences -join ',') `
                    + '; metadataReadFailures=' + ($assemblyReadFailures -join ' | ')) `
                ($missingProductionRoots.Count -eq 0 -and $directValidationReferences.Count -eq 0 -and $assemblyReadFailures.Count -eq 0)
            & $addR12 'productionDependencyClosureHasNoValidationReference' 'transitive managed dependency closure from Runtime and Examples omits Validation' `
                ('validationRefs=' + ($closureValidationReferences -join ',') + '; inspectedAssemblies=' + $visitedAssemblies.Count) `
                ($closureValidationReferences.Count -eq 0 -and $visitedAssemblies.Count -gt 0)

            $behaviorStrings = @($buildManifest.productionSceneBehaviourTypes)
            $forbiddenBehaviourTypes = @($behaviorStrings | Where-Object {
                $_ -match '(?i)DazPose\.Validation|Acceptance|TestController|ValidationRunner'
            })
            & $addR12 'productionSceneContainsNoAcceptanceComponents' 'serialized production scene component inventory contains no validation/acceptance controllers' `
                ('forbidden=' + ($forbiddenBehaviourTypes -join ',') + '; componentCount=' + $behaviorStrings.Count) `
                ($buildManifest.productionBoundaryPassed -eq $true -and $forbiddenBehaviourTypes.Count -eq 0)

            $result.assertions = @($result.assertions) + $r12Checks.ToArray()
            $result | Add-Member -NotePropertyName r12Runtime -NotePropertyValue ([pscustomobject]@{
                validationPlayerReport = $validationPlayerReportPath; validationPlayerLog = $validationPlayerLogPath
                validationPlayerElapsedSeconds = $validationRun.elapsedSeconds; productionPlayerLog = $productionPlayerLogPath
                productionPlayerElapsedSeconds = $productionRun.elapsedSeconds; productionManagedDirectory = $productionManaged
                productionManagedAssembliesInspected = $visitedAssemblies.Count
            }) -Force
            $result.passed = @($result.assertions | Where-Object { !$_.passed }).Count -eq 0
            $result.complete = $true
            $result | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $report -Encoding UTF8
        }
        if ($Ticket -in @('R11','R12')) {
            $normalizationPassed = $true
            $normalizationActual = 'No Unity-generated serialized input rewrite detected.'
            $normalizationEvidence = @()
            $hdrpPath = 'Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset'
            $hdrpInput = $mainManifest.files | Where-Object path -eq $hdrpPath | Select-Object -First 1
            if ($hdrpInput) {
                $mainHdrp = Join-Path $mainProject ('Assets\' + $hdrpPath.Replace('/','\'))
                $stagedHdrp = Join-Path $isolatedProject ('Assets\' + $hdrpPath.Replace('/','\'))
                $currentMainHash = Get-Hash $mainHdrp
                $stagedHashBeforeRestore = Get-Hash $stagedHdrp
                if ($currentMainHash -ne $hdrpInput.sha256) {
                    $normalizationPassed = $false
                    $normalizationActual = "Main HDRP settings changed during $Ticket; no restoration attempted."
                } elseif ($stagedHashBeforeRestore -ne $hdrpInput.sha256) {
                    $mainText = [IO.File]::ReadAllText($mainHdrp)
                    $stagedText = [IO.File]::ReadAllText($stagedHdrp)
                    $knownUnityUpgrade =
                        ($mainText -match '(?s)m_ObsoleteDiffusionProfileSettingsList:.*fileID:\s*11400000') -and
                        ($stagedText -match '(?s)m_ObsoleteDiffusionProfileSettingsList:.*fileID:\s*0') -and
                        ($mainText -match '(?s)m_RuntimeSettings:\s*m_List:\s*\[\]') -and
                        ($stagedText -match '(?s)m_RuntimeSettings:\s*m_List:\s*-\s*rid:')
                    if ($knownUnityUpgrade) {
                        Copy-Item -LiteralPath $mainHdrp -Destination $stagedHdrp -Force
                        $stagedHashAfterRestore = Get-Hash $stagedHdrp
                        $normalizationPassed = $stagedHashAfterRestore -eq $hdrpInput.sha256
                        $normalizationActual = 'Restored the recognized Unity HDRP global-settings schema migration in the disposable copy; before=' + $stagedHashBeforeRestore + '; source/restored=' + $hdrpInput.sha256
                        $normalizationEvidence = @([pscustomobject]@{
                            path=$hdrpPath; reason='Unity rewrote obsolete diffusion-profile references and populated HDRP runtime settings while loading the staged project.'
                            sourceSha256=$hdrpInput.sha256; stagedBeforeRestoreSha256=$stagedHashBeforeRestore; stagedAfterRestoreSha256=$stagedHashAfterRestore
                        })
                    } else {
                        $normalizationPassed = $false
                        $normalizationActual = 'Staged HDRP settings differ, but the change does not match the recognized Unity schema migration; no restoration attempted. staged=' + $stagedHashBeforeRestore + '; source=' + $hdrpInput.sha256
                    }
                }
            } else {
                $normalizationPassed = $false
                $normalizationActual = 'Expected HDRP global settings asset is absent from the recorded inputs.'
            }
            ConvertTo-Json -InputObject @($normalizationEvidence) -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot 'unity-staging-normalizations.json') -Encoding UTF8
            $protectedExtensions = @('.unity','.prefab','.asset')
            $serializedInputs = @($mainManifest.files | Where-Object {
                $extension = [IO.Path]::GetExtension($_.path).ToLowerInvariant()
                $extension -in $protectedExtensions -and $_.path -notmatch '(^|/)TestOutput/'
            })
            $serializedDifferences = [System.Collections.Generic.List[string]]::new()
            foreach ($inputFile in $serializedInputs) {
                $staged = Join-Path $isolatedProject ('Assets\' + $inputFile.path.Replace('/','\'))
                if (!(Test-Path -LiteralPath $staged -PathType Leaf)) { $serializedDifferences.Add($inputFile.path + ' missing from isolated project'); continue }
                if ((Get-Hash $staged) -ne $inputFile.sha256) { $serializedDifferences.Add($inputFile.path + ' changed in isolated project') }
            }
            $settingsDifferences = [System.Collections.Generic.List[string]]::new()
            foreach ($setting in $settingsManifest.files) {
                $staged = Join-Path $isolatedProject ('ProjectSettings\' + $setting.path.Replace('/','\'))
                if (!(Test-Path -LiteralPath $staged -PathType Leaf)) { $settingsDifferences.Add($setting.path + ' missing from isolated project'); continue }
                if ((Get-Hash $staged) -ne $setting.sha256) { $settingsDifferences.Add($setting.path + ' changed in isolated project') }
            }
            $preservationAssertions = @(
                (New-AssertionRecord -Name 'knownUnityStagingSerializationWasRestored' -Expected 'No rewrite, or only the recognized HDRP global-settings schema migration restored from the untouched main input in the disposable copy' -Actual $normalizationActual -Passed $normalizationPassed),
                (New-AssertionRecord -Name 'serializedScenePrefabAssetInputsUnchanged' -Expected "$($serializedInputs.Count) existing .unity/.prefab/.asset inputs byte-identical to main project" -Actual "differences=$($serializedDifferences.Count); $(@($serializedDifferences | Select-Object -First 12) -join '; ')" -Passed ($serializedDifferences.Count -eq 0)),
                (New-AssertionRecord -Name 'projectSettingsAndPlayerDefinesUnchanged' -Expected "$($settingsManifest.fileCount) ProjectSettings files byte-identical; player scripting defines remain unstamped" -Actual "differences=$($settingsDifferences.Count); $(@($settingsDifferences | Select-Object -First 12) -join '; ')" -Passed ($settingsDifferences.Count -eq 0))
            )
            $result.assertions = @($result.assertions) + $preservationAssertions
            if (@($preservationAssertions | Where-Object { !$_.passed }).Count -gt 0) { $result.passed = $false; $result.complete = $true; $result.exception = "Isolated $Ticket execution changed a protected serialized input or ProjectSettings file." }
            $result | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $report -Encoding UTF8
        }
    }
    if ($result.ticket -ne $Ticket -or $result.sourceHash -ne $sourceHash -or $result.inputAssetHashes -notcontains $inputHash) { throw 'Report identity or provenance hash mismatch.' }
    if ($result.passed -ne $true -or $result.complete -ne $true -or $exitCode -ne 0) { throw "Ticket $Ticket failed (Unity exit $exitCode). Report: $report" }
    $reportPath = Join-Path $repo ("docs\production-consolidation\remediation\reports\$Ticket.md")
    if ($AuthoringB01 -or $AuthoringB02 -or $AuthoringB03 -or $AuthoringB04) { $authoringPacket = if ($AuthoringB01) { "B01" } elseif ($AuthoringB02) { "B02" } elseif ($AuthoringB03) { "B03" } else { "B04" }; $reportPath = Join-Path $repo ("docs\production-consolidation\authoring-reports\$authoringPacket.md") }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportPath) | Out-Null
    $summary = if ($Ticket -eq 'R10') {
        'R10 passed as a read-only source/assembly manifest gate. It inventoried 267 C# files and 267 companion metas across DazPose, Player and FirstPerformanceVoid; verified source/meta hashes, GUIDs, exact five-asmdef/three-asmref payloads, direct package references, menu/executeMethod routing, and the one-way assembly graph. The manifest records the 20 required folder metas, four new script GUIDs, two moved-source path references, the assembly-qualified reflection edge found in RemediationValidation, and the narrow DAZ material adapter needed to remove the Assembly-CSharp-Editor edge.'
    } elseif ($Ticket -eq 'R01') {
        'R01 staging and runner checks passed. No product behavior was changed by this ticket.'
    } elseif ($Ticket -eq 'R02') {
        'R02 reproduced the mask-1 toe residual with footwear flags inactive. The attribution report identifies stale toe local positions as the dominant measured contributor; canonical mesh substitution reduced the residual to the baked-mesh baseline.'
    } elseif ($Ticket -eq 'R03') {
        'R03 repaired owned toe-position rollback and regenerated footwear-inactive candidate fits with the canonical barefoot foot region. The report records the R02 failed-before residual, the measured after residual, cycle maximum, rollback and hosiery checks.'
    } elseif ($Ticket -eq 'R04') {
        'R04 executed the contact, wardrobe and scene-binding readiness preflight checks.'
    } elseif ($Ticket -eq 'R05') {
        'R05 executed declared content readiness and actor-scoped production diagnostic checks.'
    } elseif ($Ticket -eq 'R06') {
        'R06 executed the typed finite-command kernel checks, including cancellation, supersession, paused delay and invalid binding behavior.'
    } elseif ($Ticket -eq 'R07') {
        'R07 executed persistent performer/view/playback ownership, generation-safe cleanup, acknowledged fake toy delivery, no-device failure, deliberate Stop and grip release checks.'
    } elseif ($Ticket -eq 'R11') {
        'R11 applied the full five-assembly boundary in one source/meta-preserving move transaction. The isolated runner checked compiled editor assembly references, script GUID resolution, saved scene and prefab reloads, missing-script counts, production and DAZPOSE_VALIDATION Windows player builds, and byte-for-byte preservation of scenes, prefabs, serialized assets and ProjectSettings.'
    } elseif ($Ticket -eq 'R12') {
        'R12 passed the final wardrobe stages and remediation regressions, saved validation and Examples-only production smoke scenes from the authored FirstPerformanceVoid environment, built exact Windows x64 Mono2x players, launched both graphics-enabled on Direct3D12 with external 180-second watchdogs, verified scoped Stop/replay and ordinary-folder Gesture/Action execution, inspected production assembly references/dependency closure, and preserved ProjectSettings and authored inputs.'
    } else {
        "${Ticket} executed its registered checks."
    }
    if ($AuthoringB01) { $summary = "B01 narrow checks: silent output in all backend states, no acknowledgement-dependent choreography, complete local cancellation at zero time scale, stale facade/Dispose isolation and partial-failure cleanup. Historical R07 contract is superseded; this is not a rerun of that acceptance suite." }
    if ($AuthoringB04) { $summary = "B04 bounded host checks: natural completion, paused Stop, replacement isolation, retained tuning and non-toy failure. Exact saved scene playback follows in B05/B08." }
    if ($AuthoringB03) { $summary = "B03 bounded settings probe: one application owner, live cache precedence, explicit disk save, absent fields and scene identity isolation. Delivered scene navigation remains a B06/B08 check." }
    if ($AuthoringB02) { $summary = "B02 narrow checks: smooth configured pose default and tuning retained across Stop/Dispose and a new run." }
    $reportLines = @("# $Ticket report",'',"Status: passed", "Run: $runId", "Evidence: $runRoot", "Source hash: $sourceHash", "Input hash: $inputHash", "Assertions: $(@($result.assertions).Count)", '', $summary)
    if ($Ticket -eq 'R10') {
        $reportLines += @('', 'Manifest: `docs/production-consolidation/remediation/SOURCE_MOVE_MANIFEST.json`', '', 'The exact graph, GUID allocations, source/meta hashes, menu entrypoints, literal source-path references, first-party type imports including assembly-qualified reflection references, package assembly identities and Unity payloads are recorded there. All manifest assertions are in `report.json`.')
    }
    $reportLines | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Output "REMEDIATION_PASS: $Ticket; evidence $runRoot; report $reportPath"
}
catch {
    $failure = [pscustomobject]@{ ticket = $Ticket; status = 'failed'; startedUtc = $started.ToString('O'); completedUtc = [DateTime]::UtcNow.ToString('O'); exception = $_.Exception.ToString() }
    $failure | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot 'runner-failure.json') -Encoding UTF8
    throw
}
finally {
    if ($lock) { $lock.Dispose() }
}
