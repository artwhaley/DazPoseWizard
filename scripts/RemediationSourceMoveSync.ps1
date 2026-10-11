function Remove-StaleRemediationSourceCopies {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$RepositoryRoot,
        [Parameter(Mandatory=$true)][string]$StagingProjectRoot
    )

    $repo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $staging = (Resolve-Path -LiteralPath $StagingProjectRoot).Path
    $stagingAssets = (Resolve-Path -LiteralPath (Join-Path $staging 'Assets')).Path.TrimEnd('\')
    $manifestPath = Join-Path $repo 'docs\production-consolidation\remediation\SOURCE_MOVE_MANIFEST.json'
    if (!(Test-Path -LiteralPath $manifestPath)) { throw "R10 source-move manifest is missing: $manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $removed = [System.Collections.Generic.List[string]]::new()

    foreach ($row in $manifest.files) {
        $assetMarker = '/Assets/'
        $markerIndex = $row.source.IndexOf($assetMarker, [StringComparison]::OrdinalIgnoreCase)
        if ($markerIndex -lt 0) { throw "Manifest source is outside the Unity Assets tree: $($row.source)" }
        $sourceAsset = $row.source.Substring($markerIndex + 1).Replace('\','/')
        if ($sourceAsset.Equals($row.destination, [StringComparison]::OrdinalIgnoreCase)) { continue }

        $relativeSource = $sourceAsset.Replace('/','\')
        $stagedSource = Join-Path $staging $relativeSource
        $stagedMeta = $stagedSource + '.meta'
        $hasSource = Test-Path -LiteralPath $stagedSource -PathType Leaf
        $hasMeta = Test-Path -LiteralPath $stagedMeta -PathType Leaf
        if ($hasSource -ne $hasMeta) { throw "Staging contains only half of a moved source/meta pair: $relativeSource" }
        if (!$hasSource) { continue }

        foreach ($candidate in @(@($stagedSource,[string]$row.sourceSha256),@($stagedMeta,[string]$row.metaSha256))) {
            $full = [IO.Path]::GetFullPath($candidate[0])
            if (!$full.StartsWith($stagingAssets + '\', [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove a stale path outside staging Assets: $full"
            }
            $actual = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -ne $candidate[1].ToLowerInvariant()) {
                throw "Stale staging source does not match its R10 pre-move hash; preserved it: $full"
            }
        }

        Remove-Item -LiteralPath $stagedSource -Force
        Remove-Item -LiteralPath $stagedMeta -Force
        $removed.Add($relativeSource)
    }

    Write-Output "Removed $($removed.Count) hash-verified stale source/meta pairs from staging only."
    return ,@($removed)
}

function Remove-MisclassifiedPreflightRunnerCopy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$RepositoryRoot,
        [Parameter(Mandatory=$true)][string]$StagingProjectRoot
    )

    $repo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $staging = (Resolve-Path -LiteralPath $StagingProjectRoot).Path
    $mainAssets = Join-Path $repo 'validation\DazPoseUnityValidation\Assets'
    $runtimeSource = Join-Path $mainAssets 'DazPose\Validation\RemediationPreflightRunner.cs'
    $runtimeMeta = $runtimeSource + '.meta'
    $oldEditorSource = Join-Path $staging 'Assets\DazPose\Validation\Editor\RemediationPreflightRunner.cs'
    $oldEditorMeta = $oldEditorSource + '.meta'
    $stagingAssets = (Resolve-Path -LiteralPath (Join-Path $staging 'Assets')).Path.TrimEnd('\')

    if (!(Test-Path -LiteralPath $runtimeSource -PathType Leaf) -or !(Test-Path -LiteralPath $runtimeMeta -PathType Leaf)) {
        throw "The authoritative validation-runtime preflight runner or its meta is missing: $runtimeSource"
    }
    if (Test-Path -LiteralPath (Join-Path $mainAssets 'DazPose\Validation\Editor\RemediationPreflightRunner.cs')) {
        throw 'The misclassified editor-path preflight runner still exists in the authoritative project; inspect the source move before syncing.'
    }

    $hasSource = Test-Path -LiteralPath $oldEditorSource -PathType Leaf
    $hasMeta = Test-Path -LiteralPath $oldEditorMeta -PathType Leaf
    if ($hasSource -ne $hasMeta) { throw "Staging contains only half of the misclassified preflight source/meta pair: $oldEditorSource" }
    if (!$hasSource) { return $false }

    foreach ($candidate in @(@($oldEditorSource,$runtimeSource),@($oldEditorMeta,$runtimeMeta))) {
        $stagedPath = [IO.Path]::GetFullPath($candidate[0])
        if (!$stagedPath.StartsWith($stagingAssets + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a stale preflight source outside staging Assets: $stagedPath"
        }
        $stagedHash = (Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $authoritativeHash = (Get-FileHash -LiteralPath $candidate[1] -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($stagedHash -ne $authoritativeHash) {
            throw "Stale editor-path preflight source differs from the authoritative runtime source; preserved it: $stagedPath"
        }
    }

    Remove-Item -LiteralPath $oldEditorSource -Force
    Remove-Item -LiteralPath $oldEditorMeta -Force
    Write-Output 'Removed the hash-verified R04 editor-only copy from isolated staging.'
    return $true
}
