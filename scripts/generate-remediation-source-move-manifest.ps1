[CmdletBinding()]
param(
    [string]$Repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$UnityProject = 'validation/DazPoseUnityValidation',
    [string]$Output = 'docs/production-consolidation/remediation/SOURCE_MOVE_MANIFEST.json'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $Repository).Path
$project = Join-Path $repo $UnityProject
$assets = Join-Path $project 'Assets'
$outputPath = Join-Path $repo $Output
$prior = if (Test-Path -LiteralPath $outputPath) { Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json } else { $null }

function Get-RepoPath([string]$Path) {
    return $Path.Substring($repo.Length + 1).Replace('\','/')
}
function Get-Sha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function New-Guid([string]$Key) {
    if ($prior) {
        $allocation = $prior.guidAllocations.PSObject.Properties[$Key]
        if ($allocation -and $allocation.Value) { return [string]$allocation.Value }
    }
    return [Guid]::NewGuid().ToString('N')
}
function Get-Namespace([string]$Code, [int]$Index) {
    $matches = [regex]::Matches($Code, '(?m)\bnamespace\s+([A-Za-z_][\w.]*)\s*[;{]')
    $found = $matches | Where-Object { $_.Index -lt $Index } | Select-Object -Last 1
    if ($found) { return $found.Groups[1].Value }
    return ''
}
function Get-TypeBraceEnd([string]$Code, [int]$OpenIndex) {
    if ($OpenIndex -lt 0) { return -1 }
    $depth = 0
    for ($i = $OpenIndex; $i -lt $Code.Length; $i++) {
        if ($Code[$i] -eq '{') { $depth++ }
        elseif ($Code[$i] -eq '}') { $depth--; if ($depth -eq 0) { return $i } }
    }
    return -1
}
function Get-AssemblyAndDestination([string]$RepoRelative) {
    $assetRelative = 'Assets/' + $RepoRelative.Substring($repoAssetsPrefix.Length)
    $destination = $assetRelative
    $assembly = 'DazPose.Runtime'
    $classification = 'runtime-default'
    $isEditorPath = $assetRelative -match '(^|/)Editor/'
    $isDazPoseEditor = $assetRelative.StartsWith('Assets/DazPose/Editor/', [StringComparison]::Ordinal)
    $isDazPoseValidation = $assetRelative.StartsWith('Assets/DazPose/Validation/', [StringComparison]::Ordinal)
    $isPlayerEditor = $assetRelative.StartsWith('Assets/Player/Editor/', [StringComparison]::Ordinal)
    $isFirstVoidEditor = $assetRelative.StartsWith('Assets/FirstPerformanceVoid/Editor/', [StringComparison]::Ordinal)
    $isRuntime = $assetRelative.StartsWith('Assets/DazPose/Runtime/', [StringComparison]::Ordinal) -or $assetRelative.StartsWith('Assets/Player/Runtime/', [StringComparison]::Ordinal)
    $fileName = [IO.Path]::GetFileName($assetRelative)
    $relativeRuntime = if ($assetRelative.StartsWith('Assets/DazPose/Runtime/', [StringComparison]::Ordinal)) { $assetRelative.Substring('Assets/DazPose/Runtime/'.Length) } else { '' }

    if ($assetRelative -eq 'Assets/FirstPerformanceVoid/FirstPerformanceVoidControls.cs') {
        $destination = 'Assets/DazPose/Validation/Legacy/FirstPerformanceVoidControls.cs'; $assembly = 'DazPose.Validation'; $classification = 'legacy-scene-controller'
    } elseif ($assetRelative -eq 'Assets/FirstPerformanceVoid/FirstContactPerformance.cs') {
        $destination = 'Assets/FirstPerformanceVoid/Runtime/FirstContactPerformance.cs'; $assembly = 'DazPose.Examples'; $classification = 'production-example'
    } elseif ($assetRelative -eq 'Assets/DazPose/Validation/RemediationValidation.cs') {
        $destination = 'Assets/DazPose/Validation/Editor/Legacy/RemediationValidation.cs'; $assembly = 'DazPose.Validation.Editor'; $classification = 'validation-editor-execute-method'
    } elseif ($assetRelative -eq 'Assets/DazPose/Validation/RemediationPreflightRunner.cs') {
        # This MonoBehaviour is attached to a saved validation scene and must be
        # present in a player build. Keep it in the validation runtime assembly.
        $destination = 'Assets/DazPose/Validation/RemediationPreflightRunner.cs'; $assembly = 'DazPose.Validation'; $classification = 'validation-runtime-scene-witness'
    } elseif ($isDazPoseValidation) {
        if ($isEditorPath) { $assembly = 'DazPose.Validation.Editor'; $classification = 'validation-editor' }
        else { $assembly = 'DazPose.Validation'; $classification = 'validation-runtime' }
    } elseif ($isRuntime -and ($fileName -match 'RuntimeSelfTests\.cs$|AcceptanceSelfTests\.cs$|TransportSelfTests\.cs$|AcceptanceHarness\.cs$|SmokeHarness\.cs$|^HandGripSpatialProof\.cs$|^WardrobeRuntimeExecutionHarness\.cs$|^WardrobePlayerExecutionFixture\.cs$|^PartnerAnatomyTestController\.cs$' -or $relativeRuntime.StartsWith('AnimationAudit/', [StringComparison]::Ordinal))) {
        $destination = 'Assets/DazPose/Validation/Legacy/' + $relativeRuntime; $assembly = 'DazPose.Validation'; $classification = 'validation-runtime-explicit-rule'
    } elseif ($isDazPoseEditor -and $assetRelative.Substring('Assets/DazPose/Editor/'.Length).StartsWith('AnimationAudit/', [StringComparison]::Ordinal)) {
        $destination = 'Assets/DazPose/Validation/Editor/Legacy/' + $assetRelative.Substring('Assets/DazPose/Editor/'.Length); $assembly = 'DazPose.Validation.Editor'; $classification = 'validation-editor-animation-audit'
    } elseif ($isDazPoseEditor -and $validationEditorSources.Contains($assetRelative.Substring('Assets/DazPose/Editor/'.Length))) {
        $destination = 'Assets/DazPose/Validation/Editor/Legacy/' + $assetRelative.Substring('Assets/DazPose/Editor/'.Length); $assembly = 'DazPose.Validation.Editor'; $classification = 'validation-editor-assertion-or-fixture'
    } elseif ($isDazPoseEditor -or $isPlayerEditor -or $isFirstVoidEditor) {
        $assembly = 'DazPose.Editor'; $classification = 'product-editor'
    } elseif ($assetRelative.StartsWith('Assets/FirstPerformanceVoid/Runtime/', [StringComparison]::Ordinal)) {
        $assembly = 'DazPose.Examples'; $classification = 'production-example'
    } elseif ($assetRelative.StartsWith('Assets/DazPose/Runtime/', [StringComparison]::Ordinal)) {
        $assembly = 'DazPose.Runtime'; $classification = 'production-runtime'
    } elseif ($assetRelative.StartsWith('Assets/Player/Runtime/', [StringComparison]::Ordinal)) {
        $assembly = 'DazPose.Runtime'; $classification = 'player-runtime'
    }
    $current = if ($isEditorPath) { 'Assembly-CSharp-Editor' } else { 'Assembly-CSharp' }
    return [pscustomobject]@{ source = $RepoRelative; destination = $destination; sourceAssembly = $current; destinationAssembly = $assembly; classification = $classification }
}

$repoAssetsPrefix = (Get-RepoPath $assets).TrimEnd('/') + '/'
$validationEditorSources = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'DazFbxEvidenceInspector.cs','DazGraftPairProbe.cs','DazPoseClipBindingComparison.cs',
    'LaraCandidateLiveDiagnostics.cs','LaraFirstOutfitValidation.cs','LaraWardrobeMotionDiagnostics.cs',
    'HandGrip/HandGripAcceptanceSetup.cs','HandGrip/HandGripLiveProof.cs','HandGrip/HandGripValidationRunner.cs',
    'HandGrip/HandGripVisualProof.cs','Motion/FunscriptAcceptanceRunner.cs','Partner/PartnerRigAcceptanceBuilder.cs',
    'Toys/ToyControlBt1TestMenu.cs','Wardrobe/Day5WardrobeValidation.cs','Wardrobe/WardrobeExecutionValidation.cs',
    'Wardrobe/WardrobeRuntimeFixtureBuilder.cs','Wardrobe/WardrobeImportRenderValidation.cs','Wardrobe/WardrobeImportValidation.cs'
) | ForEach-Object { [void]$validationEditorSources.Add($_) }

$scope = @('DazPose','Player','FirstPerformanceVoid') | ForEach-Object { Join-Path $assets $_ }
$sourceFiles = @(Get-ChildItem -LiteralPath $scope -Recurse -File -Force -Filter '*.cs' | Sort-Object FullName)
$typeRows = [System.Collections.Generic.List[object]]::new()
$codeByPath = @{}
foreach ($file in $sourceFiles) {
    $rel = Get-RepoPath $file.FullName
    $text = [IO.File]::ReadAllText($file.FullName)
    $code = [regex]::Replace($text, '(?s)/\*.*?\*/|//[^\r\n]*|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|''(?:\\.|[^''\\])*''', ' ')
    $codeByPath[$rel] = $code
    $declarations = @([regex]::Matches($code, '\b(?:class|struct|interface|enum|record)\s+(?:class\s+|struct\s+)?(@?[A-Za-z_]\w*)'))
    $scopes = [System.Collections.Generic.List[object]]::new()
    foreach ($match in $declarations) {
        $name = $match.Groups[1].Value.TrimStart('@')
        $namespace = Get-Namespace $code $match.Index
        $open = $code.IndexOf('{', $match.Index + $match.Length)
        $semicolon = $code.IndexOf(';', $match.Index + $match.Length)
        if ($semicolon -ge 0 -and ($open -lt 0 -or $semicolon -lt $open)) { $open = -1 }
        $close = Get-TypeBraceEnd $code $open
        $parent = $scopes | Where-Object { $_.open -ge 0 -and $_.open -lt $match.Index -and $_.close -gt $match.Index } | Sort-Object open -Descending | Select-Object -First 1
        $typePath = if ($parent) { $parent.typePath + '.' + $name } else { $name }
        $fullName = if ($namespace) { $namespace + '.' + $typePath } else { $typePath }
        $row = [pscustomobject]@{ name=$name; namespace=$namespace; typePath=$typePath; containerTypePath=if($parent){$parent.typePath}else{''}; fullName=$fullName; source=(Get-RepoPath $file.FullName); open=$open; close=$close }
        $typeRows.Add($row); $scopes.Add($row)
    }
}
$typeMap = @{}
foreach ($type in $typeRows) { if (!$typeMap.ContainsKey($type.name)) { $typeMap[$type.name] = [System.Collections.Generic.List[object]]::new() }; $typeMap[$type.name].Add($type) }

$manifestFiles = [System.Collections.Generic.List[object]]::new()
foreach ($file in $sourceFiles) {
    $rel = Get-RepoPath $file.FullName
    $metaPath = $file.FullName + '.meta'
    if (!(Test-Path -LiteralPath $metaPath)) { throw "C# source has no companion meta: $rel" }
    $guidMatch = Select-String -LiteralPath $metaPath -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' | Select-Object -First 1
    if (!$guidMatch) { throw "C# meta has no valid GUID: $(Get-RepoPath $metaPath)" }
    $plan = Get-AssemblyAndDestination $rel
    $code = $codeByPath[$rel]
    $declared = @($typeRows | Where-Object source -eq $rel | ForEach-Object fullName)
    $sourceNamespaces = @([regex]::Matches($code, '\bnamespace\s+([A-Za-z_][\w.]*)\s*[;{]') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $usingRows = @([regex]::Matches($code, '(?m)^\s*using\s+(static\s+)?(?:(?<alias>[A-Za-z_][\w]*)\s*=\s*)?(?<target>[A-Za-z_][\w.]*)\s*;'))
    $usings = @($usingRows | ForEach-Object {
        $target = $_.Groups['target'].Value
        if ($_.Groups['static'].Success) { ($target -replace '\.[^.]+$','') }
        elseif ($_.Groups['alias'].Success -and $target.Contains('.')) { ($target -replace '\.[^.]+$','') }
        else { $target }
    } | Sort-Object -Unique)
    $aliasUsings = @{}
    foreach ($using in $usingRows) { if ($using.Groups['alias'].Success) { $aliasUsings[$using.Groups['alias'].Value] = $using.Groups['target'].Value } }
    $referenced = [System.Collections.Generic.List[object]]::new()
    foreach ($typeName in $typeMap.Keys) {
        $identifierMatches = [regex]::Matches($code, '(?<![A-Za-z0-9_])' + [regex]::Escape($typeName) + '(?![A-Za-z0-9_])')
        if ($identifierMatches.Count -eq 0) { continue }
        foreach ($target in $typeMap[$typeName]) {
            if ($target.source -eq $rel) { continue }
            $targetPlan = Get-AssemblyAndDestination $target.source
            $qualifiedType = if ($target.namespace) { $target.namespace + '.' + $target.typePath } else { $target.typePath }
            $qualifiedPresent = [regex]::IsMatch($code, '(?<![A-Za-z0-9_])(?:global::)?' + [regex]::Escape($qualifiedType) + '(?![A-Za-z0-9_])')
            $nestedPresent = $target.typePath -ne $target.name -and [regex]::IsMatch($code, '(?<![A-Za-z0-9_])' + [regex]::Escape($target.typePath) + '(?![A-Za-z0-9_])')
            $aliasPresent = @($aliasUsings.Keys | Where-Object {
                $aliasUsings[$_] -eq $qualifiedType -and [regex]::IsMatch($code, '(?<![A-Za-z0-9_])' + [regex]::Escape($_) + '(?![A-Za-z0-9_])')
            }).Count -gt 0
            $resolution = if ($aliasPresent) { 'type-alias' } elseif ($qualifiedPresent) { 'fully-qualified' } elseif ($nestedPresent) { 'nested-qualified' } else { '' }
            if (!$resolution -and $target.typePath -ne $target.name) {
                $container = $target.containerTypePath
                foreach ($identifierMatch in $identifierMatches) {
                    $containingType = $typeRows | Where-Object {
                        $_.source -eq $rel -and $_.open -ge 0 -and $_.open -lt $identifierMatch.Index -and $_.close -gt $identifierMatch.Index
                    } | Sort-Object open -Descending | Select-Object -First 1
                    if ($container -and $containingType -and ($containingType.typePath -eq $container -or $containingType.typePath.StartsWith($container + '.', [StringComparison]::Ordinal))) {
                        $resolution = 'containing-type-scope'
                        break
                    }
                }
            }
            if (!$resolution -and $target.typePath -eq $target.name) {
                $localNameCollision = @($typeRows | Where-Object { $_.source -eq $rel -and $_.name -eq $typeName -and $_.typePath -eq $_.name }).Count -gt 0
                $namespaceAvailable = !$target.namespace -or $sourceNamespaces -contains $target.namespace -or $usings -contains $target.namespace
                if (!$localNameCollision -and $namespaceAvailable) {
                    $resolution = if ($target.namespace -and $sourceNamespaces -contains $target.namespace) { 'same-namespace' } else { 'using-namespace' }
                }
            }
            if ($resolution) {
                $referenced.Add([pscustomobject]@{ type = $target.fullName; source = $target.source; destinationAssembly = $targetPlan.destinationAssembly; resolution = $resolution })
            }
        }
    }
    $sourceAssembly = if ($plan.sourceAssembly -eq 'Assembly-CSharp-Editor') { 'Assembly-CSharp-Editor' } else { 'Assembly-CSharp' }
    $manifestFiles.Add([pscustomobject]@{
        source = $plan.source; meta = $plan.source + '.meta'; guid = $guidMatch.Matches[0].Groups[1].Value.ToLowerInvariant()
        sourceSha256 = Get-Sha256 $file.FullName; metaSha256 = Get-Sha256 $metaPath
        declaredTypes = $declared; currentAssembly = $sourceAssembly; destination = $plan.destination
        destinationMeta = $plan.destination + '.meta'; destinationAssembly = $plan.destinationAssembly
        classification = $plan.classification; usingNamespaces = $usings
        directTypeImports = @($referenced | Sort-Object source,type -Unique)
    })
}

# Assembly-qualified reflection strings are compile-time dependencies that a using/type-token scan cannot see.
$reflectionAssemblyReferences = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $manifestFiles) {
    $rawSource = [IO.File]::ReadAllText((Join-Path $repo $entry.source))
    foreach ($match in [regex]::Matches($rawSource, '(?i)Type\.GetType\s*\(\s*["''](?<type>[A-Za-z_][\w.]*)\s*,\s*(?<assembly>[A-Za-z_][\w.\-]*)["'']')) {
        $typeName = $match.Groups['type'].Value
        $assemblyName = $match.Groups['assembly'].Value
        $targetRows = @($typeRows | Where-Object fullName -eq $typeName)
        if ($targetRows.Count -eq 0) {
            $reflectionAssemblyReferences.Add([pscustomobject]@{ source=$entry.source; type=$typeName; currentAssembly=$assemblyName; destinationAssembly=$null; destinationSource=$null; status='unresolved' })
            continue
        }
        $target = $targetRows | Select-Object -First 1
        $targetPlan = Get-AssemblyAndDestination $target.source
        $reflectionAssemblyReferences.Add([pscustomobject]@{
            source=$entry.source; type=$typeName; currentAssembly=$assemblyName; destinationAssembly=$targetPlan.destinationAssembly
            destinationSource=$target.source; destinationPath=$targetPlan.destination; updatedAssemblyQualifiedName=($typeName + ', ' + $targetPlan.destinationAssembly); status='classified'
        })
        $entry.directTypeImports = @($entry.directTypeImports) + @([pscustomobject]@{
            type=$target.fullName; source=$target.source; destinationAssembly=$targetPlan.destinationAssembly; resolution='reflection-assembly-qualified'
        }) | Sort-Object source,type,resolution -Unique
    }
}

$assemblyDefinitions = @(
    [pscustomobject]@{ name='DazPose.Runtime'; path='Assets/DazPose/Runtime/DazPose.Runtime.asmdef'; guid=(New-Guid 'asmdef.DazPose.Runtime'); references=@('GUID:e0cd26848372d4e5c891c569017e11f1','GUID:7f7d1af65c2641843945d409d28f2e20'); includePlatforms=@(); excludePlatforms=@(); allowUnsafeCode=$false; overrideReferences=$false; precompiledReferences=@(); autoReferenced=$true; defineConstraints=@(); versionDefines=@(); noEngineReferences=$false; friends=@('DazPose.Editor','DazPose.Validation','DazPose.Validation.Editor') },
    [pscustomobject]@{ name='DazPose.Editor'; path='Assets/DazPose/Editor/DazPose.Editor.asmdef'; guid=(New-Guid 'asmdef.DazPose.Editor'); references=@('GUID:runtime','GUID:validation','GUID:examples','GUID:hdrp.runtime','GUID:hdrp.editor'); includePlatforms=@('Editor'); excludePlatforms=@(); allowUnsafeCode=$false; overrideReferences=$false; precompiledReferences=@(); autoReferenced=$true; defineConstraints=@(); versionDefines=@(); noEngineReferences=$false; friends=@('DazPose.Validation.Editor','Assembly-CSharp-Editor') },
    [pscustomobject]@{ name='DazPose.Validation'; path='Assets/DazPose/Validation/DazPose.Validation.asmdef'; guid=(New-Guid 'asmdef.DazPose.Validation'); references=@('GUID:runtime','GUID:e0cd26848372d4e5c891c569017e11f1'); includePlatforms=@(); excludePlatforms=@(); allowUnsafeCode=$false; overrideReferences=$false; precompiledReferences=@(); autoReferenced=$true; defineConstraints=@('UNITY_EDITOR || DAZPOSE_VALIDATION'); versionDefines=@(); noEngineReferences=$false; friends=@('DazPose.Validation.Editor') },
    [pscustomobject]@{ name='DazPose.Validation.Editor'; path='Assets/DazPose/Validation/Editor/DazPose.Validation.Editor.asmdef'; guid=(New-Guid 'asmdef.DazPose.Validation.Editor'); references=@('GUID:runtime','GUID:validation','GUID:editor','GUID:hdrp.runtime','GUID:hdrp.editor','GUID:rigging'); includePlatforms=@('Editor'); excludePlatforms=@(); allowUnsafeCode=$false; overrideReferences=$false; precompiledReferences=@(); autoReferenced=$true; defineConstraints=@(); versionDefines=@(); noEngineReferences=$false; friends=@() },
    [pscustomobject]@{ name='DazPose.Examples'; path='Assets/FirstPerformanceVoid/Runtime/DazPose.Examples.asmdef'; guid=(New-Guid 'asmdef.DazPose.Examples'); references=@('GUID:runtime'); includePlatforms=@(); excludePlatforms=@(); allowUnsafeCode=$false; overrideReferences=$false; precompiledReferences=@(); autoReferenced=$true; defineConstraints=@(); versionDefines=@(); noEngineReferences=$false; friends=@() }
)
$guidAllocations = [ordered]@{}
foreach ($definition in $assemblyDefinitions) { $key = 'asmdef.' + $definition.name; $guidAllocations[$key] = $definition.guid }

$asmrefs = @(
    [pscustomobject]@{ path='Assets/Player/Runtime/DazPose.Runtime.asmref'; target='DazPose.Runtime'; reference='GUID:runtime'; guid=(New-Guid 'asmref.player.runtime') },
    [pscustomobject]@{ path='Assets/Player/Editor/DazPose.Editor.asmref'; target='DazPose.Editor'; reference='GUID:editor'; guid=(New-Guid 'asmref.player.editor') },
    [pscustomobject]@{ path='Assets/FirstPerformanceVoid/Editor/DazPose.Editor.asmref'; target='DazPose.Editor'; reference='GUID:editor'; guid=(New-Guid 'asmref.examples.editor') }
)
foreach ($row in $asmrefs) { $guidAllocations['asmref.' + $row.path] = $row.guid }

$destDirs = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
foreach ($row in $manifestFiles) {
    $directory = [IO.Path]::GetDirectoryName($row.destination).Replace('\','/')
    $parts = $directory.Split('/')
    $path = ''
    foreach ($part in $parts) { $path = if ($path) { $path + '/' + $part } else { $part }; if ($path.StartsWith('Assets/DazPose/Validation/', [StringComparison]::Ordinal) -or $path -eq 'Assets/FirstPerformanceVoid/Runtime') { [void]$destDirs.Add($path) } }
}
$existingGuids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$existingMetaFiles = @(Get-ChildItem -LiteralPath $assets -Recurse -File -Force -Filter '*.meta')
foreach ($meta in $existingMetaFiles) { $m=Select-String -LiteralPath $meta.FullName -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' | Select-Object -First 1; if($m){[void]$existingGuids.Add($m.Matches[0].Groups[1].Value)} }
$folderMetas = [System.Collections.Generic.List[object]]::new()
foreach ($folder in $destDirs) {
    $physical = Join-Path $assets $folder.Substring('Assets/'.Length).Replace('/','\')
    $folderMetaPath = $physical + '.meta'
    if (!(Test-Path -LiteralPath $physical) -or !(Test-Path -LiteralPath $folderMetaPath)) {
        $key = 'folder.' + $folder
        $guid = New-Guid $key
        if ($existingGuids.Contains($guid)) { throw "Allocated folder GUID already exists: $guid" }
        $guidAllocations[$key] = $guid
        $folderMetas.Add([pscustomobject]@{ path=$folder; meta=$folder+'.meta'; guid=$guid; payload="fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n" })
    }
}

$newDependencyBridgeGuid = New-Guid 'newSource.DazPoseDtuConversionAdapter.cs.meta'
$guidAllocations['newSource.DazPoseDtuConversionAdapter.cs.meta'] = $newDependencyBridgeGuid
$scriptMetaTemplate = {
    param([string]$Guid)
    "fileFormatVersion: 2`nguid: $Guid`nMonoImporter:`n  externalObjects: {}`n  serializedVersion: 2`n  defaultReferences: []`n  executionOrder: 0`n  icon: {instanceID: 0}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n"
}
$newSourceMetaSpecs = @(
    [pscustomobject]@{ path='Assets/Daz3D/Scripts/Editor/DazPoseDtuConversionAdapter.cs'; key='newSource.DazPoseDtuConversionAdapter.cs.meta'; guid=$newDependencyBridgeGuid },
    [pscustomobject]@{ path='Assets/DazPose/Editor/DazPoseDtuConversionBridge.cs'; key='newSource.DazPoseDtuConversionBridge.cs.meta'; guid=(New-Guid 'newSource.DazPoseDtuConversionBridge.cs.meta') },
    [pscustomobject]@{ path='Assets/DazPose/Runtime/AssemblyInfo.cs'; key='newSource.DazPose.Runtime.AssemblyInfo.cs.meta'; guid=(New-Guid 'newSource.DazPose.Runtime.AssemblyInfo.cs.meta') },
    [pscustomobject]@{ path='Assets/DazPose/Editor/AssemblyInfo.cs'; key='newSource.DazPose.Editor.AssemblyInfo.cs.meta'; guid=(New-Guid 'newSource.DazPose.Editor.AssemblyInfo.cs.meta') }
)
$newSourceMetas = foreach ($spec in $newSourceMetaSpecs) {
    $guidAllocations[$spec.key] = $spec.guid
    [pscustomobject]@{ source=$spec.path; meta=$spec.path+'.meta'; guid=$spec.guid; payload=& $scriptMetaTemplate $spec.guid }
}
$friendAssemblyInfos = @(
    [pscustomobject]@{
        source='Assets/DazPose/Runtime/AssemblyInfo.cs'; guid=($newSourceMetas | Where-Object source -eq 'Assets/DazPose/Runtime/AssemblyInfo.cs').guid
        friends=@('DazPose.Editor','DazPose.Validation','DazPose.Validation.Editor')
        contents=@('using System.Runtime.CompilerServices;','','[assembly: InternalsVisibleTo("DazPose.Editor")]','[assembly: InternalsVisibleTo("DazPose.Validation")]','[assembly: InternalsVisibleTo("DazPose.Validation.Editor")]','') -join "`n"
    },
    [pscustomobject]@{
        source='Assets/DazPose/Editor/AssemblyInfo.cs'; guid=($newSourceMetas | Where-Object source -eq 'Assets/DazPose/Editor/AssemblyInfo.cs').guid
        friends=@('DazPose.Validation.Editor','Assembly-CSharp-Editor')
        contents=@('using System.Runtime.CompilerServices;','','[assembly: InternalsVisibleTo("DazPose.Validation.Editor")]','[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]','') -join "`n"
    }
)
$packageAssemblies = @(
    [pscustomobject]@{ assembly='Unity.Collections'; guid='e0cd26848372d4e5c891c569017e11f1'; package='com.unity.collections@ec2c217e4cb2'; assetPath='validation/DazPoseUnityValidation/Library/PackageCache/com.unity.collections@ec2c217e4cb2/Unity.Collections/Unity.Collections.asmdef' },
    [pscustomobject]@{ assembly='Unity.Animation.Rigging'; guid='7f7d1af65c2641843945d409d28f2e20'; package='com.unity.animation.rigging@ae5f251e1ed4'; assetPath='validation/DazPoseUnityValidation/Library/PackageCache/com.unity.animation.rigging@ae5f251e1ed4/Runtime/Unity.Animation.Rigging.asmdef' },
    [pscustomobject]@{ assembly='Unity.RenderPipelines.Core.Runtime'; guid='df380645f10b7bc4b97d4f5eb6303d95'; package='com.unity.render-pipelines.core@a4c9a9793789'; assetPath='validation/DazPoseUnityValidation/Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/Runtime/Unity.RenderPipelines.Core.Runtime.asmdef' },
    [pscustomobject]@{ assembly='Unity.RenderPipelines.HighDefinition.Runtime'; guid='457756d89b35d2941b3e7b37b4ece6f1'; package='com.unity.render-pipelines.high-definition@6461a40020a9'; assetPath='validation/DazPoseUnityValidation/Library/PackageCache/com.unity.render-pipelines.high-definition@6461a40020a9/Runtime/Unity.RenderPipelines.HighDefinition.Runtime.asmdef' },
    [pscustomobject]@{ assembly='Unity.RenderPipelines.HighDefinition.Editor'; guid='78bd2ddd6e276394a9615c203e574844'; package='com.unity.render-pipelines.high-definition@6461a40020a9'; assetPath='validation/DazPoseUnityValidation/Library/PackageCache/com.unity.render-pipelines.high-definition@6461a40020a9/Editor/Unity.RenderPipelines.HighDefinition.Editor.asmdef' },
    [pscustomobject]@{ assembly='Unity.VisualEffectGraph.Runtime'; guid='d04eb9c554ad44ceab303cecf0c0cf82'; package='com.unity.visualeffectgraph@b1f03b147b92'; assetPath='validation/DazPoseUnityValidation/Library/PackageCache/com.unity.visualeffectgraph@b1f03b147b92/Unity.VisualEffectGraph.Runtime.asmdef' },
    [pscustomobject]@{ assembly='SimpleJSON'; guid='25daac269e13e4bb0966a465099a7753'; package='Assets/Daz3D/Vendors'; assetPath='validation/DazPoseUnityValidation/Assets/Daz3D/Vendors/SimpleJSON.asmdef' }
)

$sourceCanonical = @($manifestFiles | Sort-Object source | ForEach-Object { $_.source.ToLowerInvariant() + ':' + $_.sourceSha256 + ':' + $_.metaSha256 }) -join "`n"
$sourceCanonicalBytes = [Text.Encoding]::UTF8.GetBytes($sourceCanonical)
$sourceCanonicalHasher = [Security.Cryptography.SHA256]::Create()
try { $sourceTreeHash = [BitConverter]::ToString($sourceCanonicalHasher.ComputeHash($sourceCanonicalBytes)).Replace('-','').ToLowerInvariant() }
finally { $sourceCanonicalHasher.Dispose() }
$manifest = [ordered]@{
    schemaVersion=1; generatedUtc=[DateTime]::UtcNow.ToString('O'); repositoryRoot=$repo
    sourceRoots=@('Assets/DazPose','Assets/Player','Assets/FirstPerformanceVoid')
    coverage=[ordered]@{ sourceCount=$manifestFiles.Count; metaCount=$manifestFiles.Count; filesWithMeta=@($manifestFiles | Where-Object guid).Count; sourceTreeSha256=$sourceTreeHash }
    files=@($manifestFiles)
    reflectionAssemblyReferences=@($reflectionAssemblyReferences)
    assemblyDefinitions=@($assemblyDefinitions)
    assemblyReferences=@($asmrefs)
    newFolderMetas=@($folderMetas)
    newSourceMetas=@($newSourceMetas)
    friendAssemblyInfos=@($friendAssemblyInfos)
    packageAssemblies=$packageAssemblies
    builtinEngineModules=@('UnityEngine.VFXModule')
    autoReferencedPrecompiledAssemblies=@('SALSA-LipSync','Buttplug')
    editorEntrypoints=@()
    sourcePathReferences=@()
    typeClassificationEvidence=@()
    dependencyRepairs=@(
        [pscustomobject]@{
            id='vendor-daz3d-editor-boundary'; status='required'; observedTypes=@('Daz3D.DTU','Daz3D.Daz3DDTUImporter','Daz3D.DTUMaterial','Daz3D.DTUMaterialProperty','Daz3D.DTUValue')
            referencingSources=@('Assets/DazPose/Editor/LaraCandidateBuilder.cs','Assets/DazPose/Editor/Partner/PartnerCharacterPreparation.cs')
            introducedSource='Assets/Daz3D/Scripts/Editor/DazPoseDtuConversionAdapter.cs'; introducedMeta='Assets/Daz3D/Scripts/Editor/DazPoseDtuConversionAdapter.cs.meta'
            introducedMetaGuid=$newDependencyBridgeGuid; introducedMetaPayload=(& $scriptMetaTemplate $newDependencyBridgeGuid)
            exactSteps=@(
                'Add Assets/DazPose/Editor/DazPoseDtuConversionBridge.cs to DazPose.Editor. Define internal immutable request records containing DTU path, destination texture folder, material node/name/type/value, product/component names, and ordered property records (name, kind, number, color, text and already-staged texture path). Define an internal Func<Request, Material> registration point; throw a clear InvalidOperationException if the adapter has not registered.'
                'Add an InternalsVisibleTo("Assembly-CSharp-Editor") declaration to DazPose.Editor so only the predefined editor adapter can register the converter. Keep the bridge free of Daz3D types and keep Lara/Partner file copying, texture staging, material saving, shader selection, artistic property merging and result checks in their current callers.'
                'Add the adapter at Assets/Daz3D/Scripts/Editor/DazPoseDtuConversionAdapter.cs in the existing Assembly-CSharp-Editor; do not add or alter vendor asmdefs. On editor load, register a delegate. The delegate creates Daz3D.DTU from the request path; constructs each DTUValue with the current numeric default and current Color/String override semantics; constructs DTUMaterialProperty with Name, Value, staged Texture and Exists=true; fills DTUMaterial with the existing caller-specific material metadata; calls ConvertToUnityIrayUber or ConvertToUnityDazStudioDefault based on the request; and returns the Material.'
                'Inside that delegate, save Daz3DDTUImporter.UseLegacyShaders, set it false for the conversion, and restore it in finally. The callers no longer use using Daz3D, DTU, DTUMaterial, DTUMaterialProperty, DTUValue or Daz3DDTUImporter. Preserve Lara conversion behavior for both Iray Uber and Daz Studio Default; Partner uses Iray Uber.'
                'Preserve the existing per-surface ProductName (Lara or Partner), ProductComponentName (surface.node), AssetName, MaterialName, MaterialType, Value, destination /Textures folder and property order in both callers. The adapter must not choose dissolve shaders or change the existing later shader/material logic.'
            )
        },
        [pscustomobject]@{
            id='remediation-compiler-reflection-assembly'; status='required'; source='validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/RemediationValidation.cs'
            current='Type.GetType("DazPose.UnityValidation.WardrobeFitCompiler, Assembly-CSharp-Editor")'
            exactReplacement='Type.GetType("DazPose.UnityValidation.WardrobeFitCompiler, DazPose.Editor")'
            destinationAssembly='DazPose.Editor'; reason='WardrobeFitCompiler remains a product Editor utility; RemediationValidation moves to Validation.Editor. Keep the unchanged namespace, type and Compile signature; update only the assembly-qualified name.'
        },
        [pscustomobject]@{
            id='vfx-type-attribute-runtime-package-reference'; status='required'
            observedTypes=@('UnityEngine.VFX.VFXTypeAttribute')
            referencingSources=@('Assets/DazPose/Runtime/Performer/PerformerSurfaceBindingAsset.cs')
            destinationAssembly='DazPose.Runtime'; destinationPackageAssembly='Unity.VisualEffectGraph.Runtime'
            packageGuid='d04eb9c554ad44ceab303cecf0c0cf82'
            reason='VFXTypeAttribute shares UnityEngine.VFX namespace with UnityEngine.VFXModule types but is defined by Unity.VisualEffectGraph.Runtime. Keep both dependencies distinct and reference the package asmdef explicitly.'
        },
        [pscustomobject]@{
            id='render-pipelines-volume-types-core-runtime-reference'; status='required'
            observedTypes=@('UnityEngine.Rendering.VolumeProfile','UnityEngine.Rendering.VolumeComponent')
            referencingSources=@('Assets/FirstPerformanceVoid/Editor/FirstPerformanceVoidBuilder.cs')
            destinationAssembly='DazPose.Editor'; destinationPackageAssembly='Unity.RenderPipelines.Core.Runtime'
            packageGuid='df380645f10b7bc4b97d4f5eb6303d95'
            reason='VolumeProfile and VolumeComponent are defined by Unity.RenderPipelines.Core.Runtime. The predefined editor assembly had this package dependency implicitly; the named editor assembly requires an explicit reference.'
        }
    )
    typeClassificationRules=@(
        'Production-source references keep the referenced type in DazPose.Runtime regardless of namespace.',
        'Types declared only for an explicitly selected validation source move alongside it; no duplicate or namespace rename.',
        'DazPoseCharacterState and DazPoseTransitionExtrapolationJob stay in DazPose.Runtime because production motion sources reference them.',
        'All Runtime/AnimationAudit sources move to DazPose.Validation/Legacy/AnimationAudit with unchanged metas; their source assets under Assets/DazPose/AnimationAudit remain unmoved.'
    )
    guidAllocations=$guidAllocations
    gate=@{ unresolvedFirstPartyAssemblyEdges=@(); duplicateDestinationPaths=@(); duplicateGuids=@(); missingMetas=@(); runtimeToValidationOrEditorEdges=@() }
}

# Resolve generated first-party GUID references in exact asmdef payloads.
$guidByAssembly = @{}; foreach ($definition in $assemblyDefinitions) { $guidByAssembly[$definition.name] = $definition.guid }
$guidByAssembly['Unity.RenderPipelines.HighDefinition.Runtime'] = ($packageAssemblies | Where-Object assembly -eq 'Unity.RenderPipelines.HighDefinition.Runtime' | Select-Object -First 1).guid
$guidByAssembly['Unity.RenderPipelines.HighDefinition.Editor'] = ($packageAssemblies | Where-Object assembly -eq 'Unity.RenderPipelines.HighDefinition.Editor' | Select-Object -First 1).guid
foreach ($definition in $assemblyDefinitions) {
    $definition.references = @($definition.references | ForEach-Object {
        switch -Regex ($_) {
            '^GUID:runtime$' { 'GUID:' + $guidByAssembly['DazPose.Runtime']; break }
            '^GUID:editor$' { 'GUID:' + $guidByAssembly['DazPose.Editor']; break }
            '^GUID:validation$' { 'GUID:' + $guidByAssembly['DazPose.Validation']; break }
            '^GUID:examples$' { 'GUID:' + $guidByAssembly['DazPose.Examples']; break }
            '^GUID:hdrp\.runtime$' { 'GUID:' + $guidByAssembly['Unity.RenderPipelines.HighDefinition.Runtime']; break }
            '^GUID:hdrp\.editor$' { 'GUID:' + $guidByAssembly['Unity.RenderPipelines.HighDefinition.Editor']; break }
            '^GUID:rigging$' { 'GUID:7f7d1af65c2641843945d409d28f2e20'; break }
            default { $_ }
        }
    })
}
foreach ($asmref in $asmrefs) {
    $asmref.reference = 'GUID:' + $guidByAssembly[$asmref.target]
    Add-Member -InputObject $asmref -NotePropertyName payload -NotePropertyValue ([ordered]@{ reference=$asmref.reference }) -Force
}

# Record exact direct namespace imports by planned assembly; validation is performed again in Unity.
$firstPartyNamespaces = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($type in $typeRows) { if ($type.namespace) { [void]$firstPartyNamespaces.Add($type.namespace) } }
$packageNamespaceMap = @{
    'Unity.Collections'='Unity.Collections'; 'UnityEngine.Animations.Rigging'='Unity.Animation.Rigging'
    'UnityEngine.Rendering'='Unity.RenderPipelines.Core.Runtime'
    'UnityEngine.Rendering.HighDefinition'='Unity.RenderPipelines.HighDefinition.Runtime'
    'UnityEditor.Rendering.HighDefinition'='Unity.RenderPipelines.HighDefinition.Editor'
    'CrazyMinnow.SALSA'='precompiled:SALSA-LipSync'; 'Buttplug.Client'='precompiled:Buttplug'
    'Buttplug.Core'='precompiled:Buttplug'; 'Buttplug.Core.Messages'='precompiled:Buttplug'
    'UnityEngine.VFX'='builtin:UnityEngine.VFXModule'; 'SimpleJSON'='SimpleJSON'
}
$usageByAssembly = @{}
foreach ($entry in $manifestFiles) {
    $key=$entry.destinationAssembly
    if (!$usageByAssembly.ContainsKey($key)) { $usageByAssembly[$key]=[System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal) }
    $sourceCode = $codeByPath[$entry.source]
    foreach ($ns in $packageNamespaceMap.Keys) {
        if ($entry.usingNamespaces -contains $ns -or [regex]::IsMatch($sourceCode, '(?<![A-Za-z0-9_])' + [regex]::Escape($ns) + '\.')) {
            [void]$usageByAssembly[$key].Add($packageNamespaceMap[$ns])
        }
    }
    if ([regex]::IsMatch($sourceCode, '(?<![A-Za-z0-9_])VFXType(?:Attribute)?\b')) {
        [void]$usageByAssembly[$key].Add('Unity.VisualEffectGraph.Runtime')
    }
}
$manifest.assemblyImportsByAssembly = @($usageByAssembly.Keys | Sort-Object | ForEach-Object { [pscustomobject]@{ assembly=$_; directImports=@($usageByAssembly[$_] | Sort-Object) } })
$packageGuidByAssembly = @{
    'Unity.Collections'='e0cd26848372d4e5c891c569017e11f1'
    'Unity.Animation.Rigging'='7f7d1af65c2641843945d409d28f2e20'
    'Unity.RenderPipelines.Core.Runtime'='df380645f10b7bc4b97d4f5eb6303d95'
    'Unity.RenderPipelines.HighDefinition.Runtime'='457756d89b35d2941b3e7b37b4ece6f1'
    'Unity.RenderPipelines.HighDefinition.Editor'='78bd2ddd6e276394a9615c203e574844'
    'Unity.VisualEffectGraph.Runtime'='d04eb9c554ad44ceab303cecf0c0cf82'
    'SimpleJSON'='25daac269e13e4bb0966a465099a7753'
}
$fixedDependencies = @{
    'DazPose.Runtime'=@()
    'DazPose.Editor'=@('DazPose.Runtime','DazPose.Validation','DazPose.Examples')
    'DazPose.Validation'=@('DazPose.Runtime','DazPose.Examples')
    'DazPose.Validation.Editor'=@('DazPose.Runtime','DazPose.Validation','DazPose.Editor','DazPose.Examples')
    'DazPose.Examples'=@('DazPose.Runtime')
}
foreach ($definition in $assemblyDefinitions) {
    $references = [System.Collections.Generic.List[string]]::new()
    foreach ($dependency in $fixedDependencies[$definition.name]) { $references.Add('GUID:' + $guidByAssembly[$dependency]) }
    if ($usageByAssembly.ContainsKey($definition.name)) {
        foreach ($import in $usageByAssembly[$definition.name]) {
            if ($packageGuidByAssembly.ContainsKey($import)) { $references.Add('GUID:' + $packageGuidByAssembly[$import]) }
        }
    }
    $definition.references = @($references | Sort-Object -Unique)
    $payload = [ordered]@{
        name=$definition.name; references=@($definition.references); includePlatforms=@($definition.includePlatforms)
        excludePlatforms=@($definition.excludePlatforms); allowUnsafeCode=$definition.allowUnsafeCode
        overrideReferences=$definition.overrideReferences; precompiledReferences=@($definition.precompiledReferences)
        autoReferenced=$definition.autoReferenced; defineConstraints=@($definition.defineConstraints)
        versionDefines=@($definition.versionDefines); noEngineReferences=$definition.noEngineReferences
    }
    Add-Member -InputObject $definition -NotePropertyName unityPayload -NotePropertyValue $payload -Force
}
$typeEdges = [System.Collections.Generic.List[object]]::new()
foreach ($file in $manifestFiles) {
    foreach ($target in $file.directTypeImports) {
        if ($target.destinationAssembly -ne $file.destinationAssembly) {
            $typeEdges.Add([pscustomobject]@{ from=$file.destinationAssembly; to=$target.destinationAssembly; source=$file.source; targetType=$target.type; targetSource=$target.source })
        }
    }
}
$manifest.firstPartyAssemblyEdges = @($typeEdges | Sort-Object from,to,source,targetSource,targetType -Unique)
$manifest.externalUsingNamespaces = @($manifestFiles | ForEach-Object { foreach ($ns in $_.usingNamespaces) { if ($ns -match '^(Unity\.Collections|UnityEngine\.Animations\.Rigging|UnityEngine\.Rendering\.HighDefinition|UnityEditor\.Rendering\.HighDefinition|UnityEngine\.VFX|CrazyMinnow\.SALSA|Buttplug\.|Daz3D$)') { [pscustomobject]@{ source=$_.source; destinationAssembly=$_.destinationAssembly; namespace=$ns } } } } | Sort-Object destinationAssembly,namespace,source -Unique)
$forbiddenEdges = @($manifest.firstPartyAssemblyEdges | Where-Object {
    ($_.from -eq 'DazPose.Runtime' -and $_.to -ne 'DazPose.Runtime') -or
    ($_.from -eq 'DazPose.Examples' -and $_.to -ne 'DazPose.Runtime') -or
    ($_.from -eq 'DazPose.Editor' -and $_.to -eq 'DazPose.Validation.Editor') -or
    ($_.from -eq 'DazPose.Validation' -and $_.to -in @('DazPose.Editor','DazPose.Validation.Editor')) -or
    ($_.from -eq 'DazPose.Validation.Editor' -and $_.to -notin @('DazPose.Runtime','DazPose.Validation','DazPose.Editor','DazPose.Examples')) -or
    ($_.from -eq 'DazPose.Editor' -and $_.to -notin @('DazPose.Runtime','DazPose.Validation','DazPose.Examples')) -or
    ($_.from -eq 'DazPose.Validation' -and $_.to -notin @('DazPose.Runtime','DazPose.Examples'))
})
$predefinedDazEdges = @($manifest.externalUsingNamespaces | Where-Object { $_.namespace -eq 'Daz3D' })
$manifest.gate.unresolvedFirstPartyAssemblyEdges = $forbiddenEdges
$manifest.gate.runtimeToValidationOrEditorEdges = @($forbiddenEdges | Where-Object from -eq 'DazPose.Runtime')
$manifest.gate.requiredPredefinedAssemblyEdges = $predefinedDazEdges

# Preserve tool entrypoints and literal source-path references before R11 moves files.
$assetEntryByPath = @{}
foreach ($entry in $manifestFiles) {
    $assetPath = 'Assets/' + $entry.source.Substring($repoAssetsPrefix.Length)
    $assetEntryByPath[$assetPath] = $entry
}
$entrypoints = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $manifestFiles | Where-Object destinationAssembly -in @('DazPose.Editor','DazPose.Validation.Editor')) {
    $raw = [IO.File]::ReadAllText((Join-Path $repo $entry.source))
    foreach ($attribute in [regex]::Matches($raw, '\[\s*MenuItem\s*\((?<args>[^)]*)\)\s*\]')) {
        $afterStart = $attribute.Index + $attribute.Length
        $afterLength = [Math]::Min(1600, $raw.Length - $afterStart)
        $after = $raw.Substring($afterStart, $afterLength)
        $methodMatch = [regex]::Match($after, '(?s)^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|new|async|virtual|override)\s+)*(?:[\w<>,.\?\[\]]+\s+)+(?<method>[A-Za-z_]\w*)\s*\(')
        $args = $attribute.Groups['args'].Value.Trim()
        $menuPath = $null
        $menuLiteral = [regex]::Match($args, '^\s*"((?:\\.|[^"\\])*)"')
        if ($menuLiteral.Success) { $menuPath = [regex]::Unescape($menuLiteral.Groups[1].Value) }
        $entrypoints.Add([pscustomobject]@{
            kind='MenuItem'; source=$entry.source; destination=$entry.destination; destinationAssembly=$entry.destinationAssembly
            declaredTypes=@($entry.declaredTypes); method=if($methodMatch.Success){$methodMatch.Groups['method'].Value}else{$null}
            menuArguments=$args; menuPath=$menuPath
        })
    }
    foreach ($attribute in [regex]::Matches($raw, '\[\s*InitializeOnLoad\s*\]')) {
        $after = $raw.Substring($attribute.Index + $attribute.Length, [Math]::Min(1000, $raw.Length - $attribute.Index - $attribute.Length))
        $typeMatch = [regex]::Match($after, '(?s)^\s*(?:(?:public|private|protected|internal|static|partial|sealed)\s+)*(?:class|struct)\s+(?<type>[A-Za-z_]\w*)')
        if ($typeMatch.Success) {
            $entrypoints.Add([pscustomobject]@{ kind='InitializeOnLoad'; source=$entry.source; destination=$entry.destination; destinationAssembly=$entry.destinationAssembly; type=$typeMatch.Groups['type'].Value })
        }
    }
}
$scriptRoots = @((Join-Path $repo 'scripts'), $assets)
$scriptFiles = foreach ($root in $scriptRoots) {
    if (Test-Path -LiteralPath $root) { Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object { $_.Extension -in @('.ps1','.cs','.md','.json','.txt') } }
}
$executePattern = [regex]'(?i)["'']-executeMethod["'']\s*,\s*["''](?<method>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+)["'']'
foreach ($scriptFile in $scriptFiles) {
    $scriptText = [IO.File]::ReadAllText($scriptFile.FullName)
    foreach ($match in $executePattern.Matches($scriptText)) {
        $methodName = $match.Groups['method'].Value
        $typeName = $methodName.Substring(0, $methodName.LastIndexOf('.'))
        $type = $typeRows | Where-Object fullName -eq $typeName | Select-Object -First 1
        if ($type) {
            $targetPlan = Get-AssemblyAndDestination $type.source
            $entrypoints.Add([pscustomobject]@{
                kind='executeMethod'; source=(Get-RepoPath $scriptFile.FullName); method=$methodName
                targetSource=$type.source; destination=$targetPlan.destination; destinationAssembly=$targetPlan.destinationAssembly
            })
        }
    }
}
$manifest.editorEntrypoints = @($entrypoints | Sort-Object kind,source,method,menuPath -Unique)

$textExtensions = @('.cs','.asmdef','.asmref','.json','.asset','.prefab','.unity','.yaml','.yml','.ps1')
$pathPattern = [regex]'(?i)Assets[/\\](?:DazPose|Player|FirstPerformanceVoid)(?:[/\\][^"''\s,;]+)*\.cs'
$pathReferences = [System.Collections.Generic.List[object]]::new()
$pathScanRoots = @($assets,(Join-Path $repo 'scripts'),(Join-Path $project 'ProjectSettings'))
$textFiles = foreach ($scanRoot in $pathScanRoots) {
    if (Test-Path -LiteralPath $scanRoot) {
        Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Force | Where-Object {
            ($_.Extension.ToLowerInvariant() -in $textExtensions) -and
                ($_.FullName -notmatch '[\\/](\.git|\.dazposewizard|Library|Temp|TestOutput|artifacts|obj|bin)[\\/]') -and
                ($_.FullName -ne $PSCommandPath)
        }
}
}
foreach ($textFile in $textFiles) {
    try { $lines = [IO.File]::ReadAllLines($textFile.FullName) } catch { continue }
    for ($lineIndex = 0; $lineIndex -lt $lines.Length; $lineIndex++) {
        foreach ($match in $pathPattern.Matches($lines[$lineIndex])) {
            $referencedPath = $match.Value.Replace('\','/')
            if (!$assetEntryByPath.ContainsKey($referencedPath)) { continue }
            $targetEntry = $assetEntryByPath[$referencedPath]
            if ($referencedPath -eq $targetEntry.destination) { continue }
            $pathReferences.Add([pscustomobject]@{
                owner=(Get-RepoPath $textFile.FullName); line=$lineIndex + 1; literal=$match.Value
                currentAssetPath=$referencedPath; plannedAssetPath=$targetEntry.destination
                targetSource=$targetEntry.source; targetAssembly=$targetEntry.destinationAssembly
            })
        }
    }
}
$manifest.sourcePathReferences = @($pathReferences | Sort-Object owner,line,currentAssetPath -Unique)

$typeEvidence = [System.Collections.Generic.List[object]]::new()
foreach ($type in $typeRows) {
    $ownerEntry = $manifestFiles | Where-Object source -eq $type.source | Select-Object -First 1
    $incoming = foreach ($candidate in $manifestFiles) {
        foreach ($import in $candidate.directTypeImports) {
            if ($import.source -eq $type.source -and $import.type -eq $type.fullName) {
                [pscustomobject]@{ source=$candidate.source; assembly=$candidate.destinationAssembly; referencingTypes=@($candidate.declaredTypes) }
            }
        }
    }
    $typeEvidence.Add([pscustomobject]@{
        type=$type.fullName; source=$type.source; destination=$ownerEntry.destination; destinationAssembly=$ownerEntry.destinationAssembly
        classification=$ownerEntry.classification; referencedBy=@($incoming | Sort-Object source -Unique)
    })
}
$manifest.typeClassificationEvidence = @($typeEvidence | Sort-Object type,source)

$destinations = @($manifestFiles | ForEach-Object destination)
$duplicateDestinations = @($destinations | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
$sourceGuids = @($manifestFiles | ForEach-Object guid)
$plannedGuids = @($guidAllocations.Values)
$duplicateSourceGuids = @($sourceGuids | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
$plannedGuidCollisions = @($plannedGuids | Where-Object { $existingGuids.Contains($_) } | Sort-Object -Unique)
$duplicatePlannedGuids = @($plannedGuids | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
$duplicateGuids = @($duplicateSourceGuids + $plannedGuidCollisions + $duplicatePlannedGuids | Sort-Object -Unique)
$missingMetas = @($manifestFiles | Where-Object { !$_.guid } | ForEach-Object source)
$manifest.gate.duplicateDestinationPaths = $duplicateDestinations
$manifest.gate.duplicateGuids = $duplicateGuids
$manifest.gate.missingMetas = $missingMetas

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
$json = $manifest | ConvertTo-Json -Depth 16
[IO.File]::WriteAllText($outputPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output "Wrote $outputPath; sources=$($manifestFiles.Count); declaredTypes=$($typeRows.Count); newFolderMetas=$($folderMetas.Count); duplicateDestinations=$($duplicateDestinations.Count); duplicateGuids=$($duplicateGuids.Count); missingMetas=$($missingMetas.Count)"
