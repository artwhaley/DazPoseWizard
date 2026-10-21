[CmdletBinding()]
param(
    [string]$SandboxRoot = 'C:\Users\artwh\DazPoseWizard-Sandbox',
    [ValidateRange(1,1024)][double]$BudgetGiB = 50,
    [switch]$Initialize
)
$ErrorActionPreference = 'Stop'
$sandboxRepo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$sandboxSource = Join-Path $sandboxRepo 'validation\DazPoseUnityValidation'
$sandboxTarget = [IO.Path]::GetFullPath($SandboxRoot).TrimEnd('\','/')
$sandboxCap = [long]($BudgetGiB * 1GB)
if ($sandboxTarget -eq $sandboxRepo -or $sandboxTarget.StartsWith($sandboxRepo + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $sandboxRepo.StartsWith($sandboxTarget + '\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Sandbox must be separate from the repository, never inside it or its parent.'
}
$sandboxMarkerPath = Join-Path $sandboxTarget '.dazposewizard-sandbox.json'
function ConvertTo-PlainHashtable($Value) {
    # Windows PowerShell 5.1 has no ConvertFrom-Json -AsHashtable; PowerShell 7 works either way.
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $table = @{}
        foreach ($property in $Value.PSObject.Properties) { $table[$property.Name] = ConvertTo-PlainHashtable $property.Value }
        return $table
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = @(); foreach ($item in $Value) { $items += ,(ConvertTo-PlainHashtable $item) }
        return ,$items
    }
    return $Value
}
function Write-Utf8NoBomJson($Value, [string]$Path) {
    [IO.File]::WriteAllText($Path, (($Value | ConvertTo-Json -Depth 6) + [Environment]::NewLine), (New-Object Text.UTF8Encoding($false)))
}
function Get-SandboxFiles([string]$Folder) {
    if (!(Test-Path -LiteralPath $Folder)) { return }
    Get-ChildItem -LiteralPath $Folder -Force -Recurse -File | ForEach-Object {
        if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked input refused: $($_.FullName)" }
        $_
    }
}
function Get-SandboxBytes {
    $sandboxMeasure = Get-SandboxFiles $sandboxTarget | Measure-Object Length -Sum
    return [long]$sandboxMeasure.Sum
}
function Get-CodeInputs {
    $sandboxPrefix = 'validation/DazPoseUnityValidation/'
    $sandboxTracked = & git -C $sandboxRepo -c core.quotepath=false ls-files -- 'validation/DazPoseUnityValidation/Assets'
    if ($LASTEXITCODE -ne 0) { throw 'Could not list tracked source inputs.' }
    foreach ($sandboxFile in $sandboxTracked) {
        $sandboxRelative = $sandboxFile.Substring($sandboxPrefix.Length)
        if ($sandboxRelative -match '\.(cs|asmdef|asmref)(\.meta)?$' -or
            ($sandboxRelative -match '\.meta$' -and (Test-Path -LiteralPath (Join-Path $sandboxSource $sandboxRelative.Substring(0,$sandboxRelative.Length-5)) -PathType Container))) {
            if (!(Test-Path -LiteralPath (Join-Path $sandboxSource $sandboxRelative) -PathType Leaf)) { throw "Tracked source missing: $sandboxRelative" }
            $sandboxRelative
        }
    }
}
if ($Initialize) {
    if (Test-Path -LiteralPath $sandboxTarget) { throw 'Sandbox already exists; initialize never overwrites it. Use the normal code sync.' }
    $sandboxInputs = @()
    foreach ($sandboxFolder in @('Assets','Packages','ProjectSettings')) {
        $sandboxInputRoot = Join-Path $sandboxSource $sandboxFolder
        if (!(Test-Path -LiteralPath $sandboxInputRoot)) { throw "Source folder missing: $sandboxInputRoot" }
        $sandboxInputs += @(Get-SandboxFiles $sandboxInputRoot)
    }
    $sandboxSidecar = Join-Path $sandboxSource 'DazPoseWizard.project.json'
    if (Test-Path -LiteralPath $sandboxSidecar) { $sandboxInputs += Get-Item -LiteralPath $sandboxSidecar }
    $sandboxInputBytes = [long]($sandboxInputs | Measure-Object Length -Sum).Sum
    if ($sandboxInputBytes -gt $sandboxCap) { throw "Initial inputs require $sandboxInputBytes bytes, exceeding approved cap $sandboxCap." }
    New-Item -ItemType Directory -Path $sandboxTarget | Out-Null
    $sandboxMarker = [ordered]@{ schemaVersion=1; sourceProject=$sandboxSource; sandboxProject=$sandboxTarget; budgetBytes=$sandboxCap; createdUtc=[DateTime]::UtcNow.ToString('o'); initialInputBytes=$sandboxInputBytes; initialFiles=$sandboxInputs.Count; managedCode=@(); packageHashes=@{} }
    # Identity is written before copying, allowing inspection/repair of this one path after failure.
    Write-Utf8NoBomJson $sandboxMarker $sandboxMarkerPath
    foreach ($sandboxInput in $sandboxInputs) {
        $sandboxRelative = $sandboxInput.FullName.Substring($sandboxSource.Length+1)
        $sandboxDestination = Join-Path $sandboxTarget $sandboxRelative
        New-Item -ItemType Directory -Force -Path (Split-Path $sandboxDestination -Parent) | Out-Null
        [IO.File]::Copy($sandboxInput.FullName,$sandboxDestination,$false)
    }
    Write-Output "INITIALIZED $($sandboxInputs.Count) input files / $sandboxInputBytes bytes; Library, Temp, Logs, UserSettings, Build* and TestOutput were not copied."
} else {
    if (!(Test-Path -LiteralPath $sandboxMarkerPath)) { throw 'No sandbox identity marker; do not use an unrelated project. Initialize the agreed path first.' }
    $sandboxMarker = ConvertTo-PlainHashtable (Get-Content -LiteralPath $sandboxMarkerPath -Raw | ConvertFrom-Json)
    if ($sandboxMarker.sourceProject -ne $sandboxSource -or $sandboxMarker.sandboxProject -ne $sandboxTarget -or $sandboxMarker.budgetBytes -ne $sandboxCap) { throw 'Sandbox identity/budget differs from the approved configuration.' }
}
$sandboxBeforeBytes = Get-SandboxBytes
if ($sandboxBeforeBytes -gt $sandboxCap) { throw "Sandbox already exceeds approved disk cap: $sandboxBeforeBytes > $sandboxCap" }
$sandboxCode = @(Get-CodeInputs)
$sandboxPrevious = @{}
foreach ($sandboxRow in $sandboxMarker.managedCode) { $sandboxPrevious[$sandboxRow.path] = $sandboxRow.sha256 }
$sandboxPlanned = @()
foreach ($sandboxRelative in $sandboxCode) {
    $sandboxOrigin = Join-Path $sandboxSource $sandboxRelative
    $sandboxDestination = Join-Path $sandboxTarget $sandboxRelative
    $sandboxSourceHash = (Get-FileHash -LiteralPath $sandboxOrigin -Algorithm SHA256).Hash
    $sandboxDestinationHash = if (Test-Path -LiteralPath $sandboxDestination) { (Get-FileHash -LiteralPath $sandboxDestination -Algorithm SHA256).Hash } else { '' }
    if ($sandboxPrevious.ContainsKey($sandboxRelative) -and $sandboxDestinationHash -ne $sandboxPrevious[$sandboxRelative]) { throw "Sandbox code was changed independently: $sandboxRelative" }
    $sandboxPlanned += [pscustomobject]@{path=$sandboxRelative;sha256=$sandboxSourceHash;changed=($sandboxSourceHash -ne $sandboxDestinationHash)}
}
# Prevalidate owned removals before any synchronization writes.
$sandboxRemoved = @($sandboxPrevious.Keys | Where-Object { $_ -notin $sandboxCode })
foreach ($sandboxRelative in $sandboxRemoved) {
    $sandboxDestination = [IO.Path]::GetFullPath((Join-Path $sandboxTarget $sandboxRelative))
    if (!$sandboxDestination.StartsWith($sandboxTarget+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Removal escapes sandbox.' }
    if (!(Test-Path -LiteralPath $sandboxDestination) -or (Get-FileHash -LiteralPath $sandboxDestination).Hash -ne $sandboxPrevious[$sandboxRelative]) { throw "Owned sandbox code differs: $sandboxRelative" }
}
foreach ($sandboxRow in $sandboxPlanned) {
    if (!$sandboxRow.changed) { continue }
    $sandboxOrigin = Join-Path $sandboxSource $sandboxRow.path
    $sandboxDestination = Join-Path $sandboxTarget $sandboxRow.path
    New-Item -ItemType Directory -Force -Path (Split-Path $sandboxDestination -Parent) | Out-Null
    Copy-Item -LiteralPath $sandboxOrigin -Destination $sandboxDestination -Force
}
foreach ($sandboxRelative in $sandboxRemoved) { Remove-Item -LiteralPath (Join-Path $sandboxTarget $sandboxRelative) -Force }
# The package graph is synced explicitly; sandbox-authored package changes (S02) are protected.
foreach ($sandboxPackage in @('Packages/manifest.json','Packages/packages-lock.json')) {
    $sandboxDestination = Join-Path $sandboxTarget $sandboxPackage
    $sandboxOrigin = Join-Path $sandboxSource $sandboxPackage
    $sandboxHash = (Get-FileHash -LiteralPath $sandboxDestination).Hash
    $sandboxOriginHash = (Get-FileHash -LiteralPath $sandboxOrigin).Hash
    if ($sandboxMarker.packageHashes.ContainsKey($sandboxPackage)) {
        # Unity may resolve its own lock file, and S02 may add sandbox packages. Leave both alone
        # unless the main project's source package graph has deliberately changed since sync.
        if ($sandboxOriginHash -eq $sandboxMarker.packageHashes[$sandboxPackage]) { continue }
        if ($sandboxHash -ne $sandboxMarker.packageHashes[$sandboxPackage]) { throw "Both source and sandbox package graph changed: $sandboxPackage" }
    }
    Copy-Item -LiteralPath $sandboxOrigin -Destination $sandboxDestination -Force
    $sandboxMarker.packageHashes[$sandboxPackage] = $sandboxOriginHash
}
$sandboxMarker.managedCode = @($sandboxPlanned | Select-Object path,sha256)
$sandboxMarker.syncedUtc = [DateTime]::UtcNow.ToString('o')
$sandboxMarker.sourceRevision = (& git -C $sandboxRepo rev-parse HEAD)
Write-Utf8NoBomJson $sandboxMarker $sandboxMarkerPath
$sandboxAfterBytes = Get-SandboxBytes
if ($sandboxAfterBytes -gt $sandboxCap) { throw "Sandbox exceeds approved cap after sync: $sandboxAfterBytes > $sandboxCap" }
Write-Output "SYNCED $($sandboxCode.Count) tracked code/meta inputs; $(@($sandboxPlanned | Where-Object changed).Count) changed; $($sandboxRemoved.Count) removed; total $sandboxAfterBytes / $sandboxCap bytes. No Library write."
