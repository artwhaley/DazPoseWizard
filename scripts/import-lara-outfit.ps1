[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Recipe,
    [ValidateSet('Preflight','Inspect','Prepare','Status','Import','Validate','Assets','All','Publish','ReimportTest')]
    [string]$Action='Preflight',
    [string]$Python='C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin\python.exe',
    [switch]$ForcePreparation
)
$ErrorActionPreference='Stop'
$importRepository = Split-Path $PSScriptRoot -Parent
$importProject = Join-Path $importRepository 'validation\DazPoseUnityValidation'
$importRecipe = (Resolve-Path -LiteralPath $Recipe).Path
$importConfig = Get-Content -LiteralPath $importRecipe -Raw | ConvertFrom-Json
if ($importConfig.schemaVersion -ne 1 -or $importConfig.id -cnotmatch '^[a-z0-9_-]+$') {
    throw 'Unsupported recipe version or invalid outfit id (lowercase ASCII, digits, hyphens or underscores).'
}
if ($importConfig.destination -cne ('Assets/TestData/Wardrobe/' + $importConfig.id)) {
    throw 'Destination must be exactly Assets/TestData/Wardrobe/<id> matching the recipe id.'
}
if ($Action -in @('Import','Validate','Assets','All','Publish','ReimportTest')) {
    throw 'Outfit conversion/validation/publication integration in the main editor is DEFERRED. No Unity process was started and no assets were converted. Use -Action Preflight, Inspect or Prepare for source preparation; use the existing Clothing Setup for already imported outfits. The proven legacy procedure/source is preserved in docs/archive/p02-import-evidence/; see docs/plan/P02_MAIN_PROJECT_IMPORT_PLAN.md.'
}
if (!(Test-Path -LiteralPath (Join-Path $importProject 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The authoritative main Unity project is missing at validation/DazPoseUnityValidation.'
}
if ($Action -eq 'Status') {
    [pscustomobject]@{
        Project=$importProject
        Outfit=$importConfig.id
        ManifestPrepared=(Test-Path -LiteralPath (Join-Path $importProject ($importConfig.destination+'/wardrobe-manifest.json')))
        OutfitAssetExists=(Test-Path -LiteralPath (Join-Path $importProject ($importConfig.destination+'/Outfit.asset')))
        Conversion='DEFERRED: main-editor import hookup is not implemented'
        Procedure='docs/archive/p02-import-evidence/README.md'
    }
    return
}
$importStage = switch ($Action) { 'Preflight' { 'preflight' } 'Inspect' { 'inspect' } 'Prepare' { 'prepare' } }
$importArguments = @((Join-Path $PSScriptRoot 'wardrobe-recipe.py'),$importStage,'--recipe',$importRecipe,'--project',$importProject)
if ($ForcePreparation) { $importArguments += '--force' }
& $Python @importArguments
if ($LASTEXITCODE -ne 0) { throw 'Source preparation failed; inspect the report and correct the recipe.' }
Write-Output ('PREPARATION_ONLY: '+$importConfig.id+'; main project '+$importProject+'; no Unity conversion or publication performed')
