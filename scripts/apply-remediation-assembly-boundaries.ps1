[CmdletBinding()]
param([switch]$ValidateOnly)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'validation\DazPoseUnityValidation'
$assets = (Resolve-Path -LiteralPath (Join-Path $project 'Assets')).Path.TrimEnd('\')
$remediationRoot = Join-Path $repo 'docs\archive\docs\production-consolidation\remediation'
$manifestPath = Join-Path $remediationRoot 'SOURCE_MOVE_MANIFEST.json'
$progressPath = Join-Path $remediationRoot 'progress.json'

function Get-Hash([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-UnderAssets([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (!$full.StartsWith($assets + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing a path outside the main Unity Assets root: $full"
    }
    return $full
}

function Add-Artifact([System.Collections.Generic.List[object]]$List, [string]$Path, [string]$Contents, [string]$Kind) {
    $full = Assert-UnderAssets (Join-Path $project $Path.Replace('/','\'))
    if (Test-Path -LiteralPath $full) { throw "R11 destination already exists; preserved it: $full" }
    $List.Add([pscustomobject]@{ path=$full; contents=$Contents; kind=$Kind })
}

if (!(Test-Path -LiteralPath $manifestPath)) { throw "R10 manifest is missing: $manifestPath" }
$progress = Get-Content -LiteralPath $progressPath -Raw | ConvertFrom-Json
if ($progress.tickets.R10.status -ne 'passed') { throw 'R11 requires a passing R10 source manifest gate.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.coverage.sourceCount -ne $manifest.files.Count -or $manifest.gate.duplicateDestinationPaths.Count -ne 0 -or $manifest.gate.duplicateGuids.Count -ne 0 -or $manifest.gate.missingMetas.Count -ne 0) {
    throw 'R10 manifest completeness, destination uniqueness or GUID gates are not satisfied.'
}

# Preflight every source/meta hash and every destination before the first move.
$moves = [System.Collections.Generic.List[object]]::new()
$reserved = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($row in $manifest.files) {
    $source = [IO.Path]::GetFullPath((Join-Path $repo $row.source.Replace('/','\')))
    $sourceMeta = $source + '.meta'
    $destination = Assert-UnderAssets (Join-Path $project $row.destination.Replace('/','\'))
    $destinationMeta = $destination + '.meta'
    if (!(Test-Path -LiteralPath $source -PathType Leaf) -or !(Test-Path -LiteralPath $sourceMeta -PathType Leaf)) { throw "R10 source/meta pair is missing: $($row.source)" }
    if ((Get-Hash $source) -ne $row.sourceSha256.ToLowerInvariant() -or (Get-Hash $sourceMeta) -ne $row.metaSha256.ToLowerInvariant()) {
        throw "R10 source/meta hashes changed before the assembly move: $($row.source)"
    }
    if (!$reserved.Add($destination.ToLowerInvariant()) -or !$reserved.Add($destinationMeta.ToLowerInvariant())) { throw "Duplicate destination in R10 manifest: $destination" }
    if (!$source.Equals($destination, [StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $destination) { throw "R11 source destination already exists; preserved it: $destination" }
        if (Test-Path -LiteralPath $destinationMeta) { throw "R11 meta destination already exists; preserved it: $destinationMeta" }
        $moves.Add([pscustomobject]@{ source=$source; sourceMeta=$sourceMeta; destination=$destination; destinationMeta=$destinationMeta; manifestSource=$row.source })
    }
}

$artifacts = [System.Collections.Generic.List[object]]::new()
foreach ($definition in $manifest.assemblyDefinitions) {
    $payload = ConvertTo-Json -InputObject $definition.unityPayload -Depth 12
    Add-Artifact $artifacts $definition.path ($payload + "`n") 'assemblyDefinition'
    Add-Artifact $artifacts ($definition.path + '.meta') ("fileFormatVersion: 2`nguid: $($definition.guid)`nAssemblyDefinitionImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n") 'assemblyDefinitionMeta'
}
foreach ($asmref in $manifest.assemblyReferences) {
    Add-Artifact $artifacts $asmref.path ((ConvertTo-Json -InputObject $asmref.payload -Depth 6) + "`n") 'assemblyReference'
    Add-Artifact $artifacts ($asmref.path + '.meta') ("fileFormatVersion: 2`nguid: $($asmref.guid)`nAssemblyDefinitionReferenceImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n") 'assemblyReferenceMeta'
}
foreach ($folder in $manifest.newFolderMetas) {
    $folderFull = Assert-UnderAssets (Join-Path $project $folder.path.Replace('/','\'))
    if (Test-Path -LiteralPath ($folderFull + '.meta')) { throw "Planned folder meta already exists; preserved it: $($folder.path).meta" }
    # The folder itself may be created by source moves; its meta identity is still new.
    Add-Artifact $artifacts ($folder.path + '.meta') $folder.payload 'folderMeta'
}
foreach ($scriptMeta in $manifest.newSourceMetas) {
    Add-Artifact $artifacts $scriptMeta.meta $scriptMeta.payload 'scriptMeta'
}
foreach ($friend in $manifest.friendAssemblyInfos) {
    Add-Artifact $artifacts $friend.source $friend.contents 'friendAssemblyInfo'
}

if ($ValidateOnly) {
    [pscustomobject]@{
        status='preflightPassed'
        sourceRows=$manifest.files.Count
        movedSourceMetaPairs=$moves.Count
        assemblyDefinitions=$manifest.assemblyDefinitions.Count
        assemblyReferences=$manifest.assemblyReferences.Count
        folderMetas=$manifest.newFolderMetas.Count
        newScriptMetas=$manifest.newSourceMetas.Count
        friendAssemblyInfos=$manifest.friendAssemblyInfos.Count
        assemblyDefinitionNames=@($manifest.assemblyDefinitions | ForEach-Object name)
    } | ConvertTo-Json -Depth 8
    return
}

$appliedMoves = [System.Collections.Generic.List[object]]::new()
$createdArtifacts = [System.Collections.Generic.List[object]]::new()
$createdDirectories = [System.Collections.Generic.List[string]]::new()
try {
    foreach ($move in $moves) {
        $parent = Split-Path -Parent $move.destination
        if (!(Test-Path -LiteralPath $parent)) {
            $missing = [System.Collections.Generic.List[string]]::new()
            $walk = $parent
            while (!(Test-Path -LiteralPath $walk)) { $missing.Add($walk); $walk = Split-Path -Parent $walk }
            [void](New-Item -ItemType Directory -Path $parent -Force)
            foreach ($directory in $missing) { $createdDirectories.Add($directory) }
        }
        $appliedMoves.Add($move)
        Move-Item -LiteralPath $move.source -Destination $move.destination
        Move-Item -LiteralPath $move.sourceMeta -Destination $move.destinationMeta
    }

    foreach ($artifact in $artifacts) {
        $parent = Split-Path -Parent $artifact.path
        if (!(Test-Path -LiteralPath $parent)) {
            $missing = [System.Collections.Generic.List[string]]::new()
            $walk = $parent
            while (!(Test-Path -LiteralPath $walk)) { $missing.Add($walk); $walk = Split-Path -Parent $walk }
            [void](New-Item -ItemType Directory -Path $parent -Force)
            foreach ($directory in $missing) { if (!$createdDirectories.Contains($directory)) { $createdDirectories.Add($directory) } }
        }
        [IO.File]::WriteAllText($artifact.path, $artifact.contents, [Text.UTF8Encoding]::new($false))
        $createdArtifacts.Add($artifact)
    }
}
catch {
    $failure = $_
    foreach ($artifact in @($createdArtifacts | Select-Object -Reverse)) {
        if (Test-Path -LiteralPath $artifact.path -PathType Leaf) {
            Remove-Item -LiteralPath $artifact.path -Force
        }
    }
    $rollbackMoves = @($appliedMoves.ToArray())
    [array]::Reverse($rollbackMoves)
    foreach ($move in $rollbackMoves) {
        if ((Test-Path -LiteralPath $move.destination -PathType Leaf) -and !(Test-Path -LiteralPath $move.source)) {
            Move-Item -LiteralPath $move.destination -Destination $move.source
        }
        if ((Test-Path -LiteralPath $move.destinationMeta -PathType Leaf) -and !(Test-Path -LiteralPath $move.sourceMeta)) {
            Move-Item -LiteralPath $move.destinationMeta -Destination $move.sourceMeta
        }
    }
    foreach ($directory in @($createdDirectories | Sort-Object Length -Descending)) {
        if (Test-Path -LiteralPath $directory -PathType Container -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) {
            Remove-Item -LiteralPath $directory -Force
        }
    }
    throw $failure
}

[pscustomobject]@{
    status='applied'
    sourceRows=$manifest.files.Count
    movedSourceMetaPairs=$moves.Count
    assemblyDefinitions=$manifest.assemblyDefinitions.Count
    assemblyReferences=$manifest.assemblyReferences.Count
    folderMetas=$manifest.newFolderMetas.Count
    newScriptMetas=$manifest.newSourceMetas.Count
    friendAssemblyInfos=$manifest.friendAssemblyInfos.Count
    assemblyDefinitionNames=@($manifest.assemblyDefinitions | ForEach-Object name)
} | ConvertTo-Json -Depth 8
