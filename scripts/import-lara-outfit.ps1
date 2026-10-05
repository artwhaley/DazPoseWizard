[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Recipe,
    [ValidateSet('Preflight','Inspect','Import','Validate','Assets','All','Publish','ReimportTest')][string]$Action='All',
    [string]$Python='C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin\python.exe',
    [string]$Unity='C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe',
    [switch]$ForcePreparation
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskMain=Join-Path $taskRoot 'validation\DazPoseUnityValidation'
$taskProject=Join-Path $taskRoot '.dazposewizard\p0c-native-generation'
$taskRecipe=(Resolve-Path -LiteralPath $Recipe).Path
$taskConfig=Get-Content -LiteralPath $taskRecipe -Raw | ConvertFrom-Json
if($taskConfig.schemaVersion -ne 1 -or $taskConfig.id -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Unsupported recipe version or invalid id.'}
if($taskConfig.destination -notmatch '^Assets/TestData/Wardrobe/[a-zA-Z0-9_-]+$'){throw 'Use one owned Assets/TestData/Wardrobe/<id> destination.'}
if(!(Test-Path -LiteralPath (Join-Path $taskProject 'ProjectSettings\ProjectVersion.txt'))){throw 'Isolated validation project is missing; stage the validation project under .dazposewizard/p0c-native-generation first.'}
$taskOutput=Join-Path $taskProject ('TestOutput\wardrobe-import\'+$taskConfig.id)
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
# One importer owns this staging project at a time. Never kill another Unity process.
$taskLockPath=Join-Path $taskProject 'wardrobe-import.lock'
$taskLock=[IO.File]::Open($taskLockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
function Copy-Changed([string]$source,[string]$target) {
    if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $source).Hash -eq (Get-FileHash -LiteralPath $target).Hash){return}
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}
function Run-Unity([string]$method,[string]$stage,[bool]$quit=$true,[bool]$gpu=$false) {
    $taskLog=Join-Path $taskOutput ($stage+'.log')
    $taskArguments=@('-batchmode','-projectPath',('"'+$taskProject+'"'),'-executeMethod',$method,'-wardrobeRecipe',('"'+$taskRecipe+'"'),'-logFile',('"'+$taskLog+'"'))
    if($quit){$taskArguments+='-quit'}
    if(!$gpu){$taskArguments+='-nographics'}
    Write-Output ('UNITY_STAGE: '+$stage)
    $taskProcess=Start-Process -FilePath $Unity -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    if($taskProcess.ExitCode -ne 0){throw "Unity stage $stage failed (exit $($taskProcess.ExitCode)); see $taskLog"}
}
try {
    $taskActive=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($taskProject) }
    if($taskActive){throw 'Another Unity process owns the isolated project. Wait for it to finish.'}
    if($Action -ne 'Publish'){
        # Sync code/graphs, not the user's scene or generated wardrobe assets.
        $taskCodeRoot=Join-Path $taskMain 'Assets\DazPose'
        Get-ChildItem -LiteralPath $taskCodeRoot -Recurse -File | Where-Object { $_.Extension -eq '.cs' -or $_.Name -like '*.cs.meta' -or $_.FullName.Contains('\Effects\Dissolve\Shaders\') } | ForEach-Object {
            Copy-Changed $_.FullName (Join-Path $taskProject $_.FullName.Substring($taskMain.Length+1))
        }
        foreach($taskAsset in @($taskConfig.fbx,$taskConfig.referenceFbx)){
            if($taskAsset -notmatch '^Assets/TestCharacter/[^/\\]+\.fbx$'){throw 'FBX inputs must be under Assets/TestCharacter.'}
            if($Action -eq 'Preflight'){continue}
            $taskSource=Join-Path $taskMain $taskAsset;Copy-Changed $taskSource (Join-Path $taskProject $taskAsset)
            if(Test-Path -LiteralPath ($taskSource+'.meta')){Copy-Changed ($taskSource+'.meta') (Join-Path $taskProject ($taskAsset+'.meta'))}
        }
        # Main-project artist edits are authoritative on later reimports.
        $taskOwned=Join-Path $taskMain $taskConfig.destination
        if($Action -in @('Import','All')){
          $taskOverrides=@{}
          if(Test-Path -LiteralPath $taskOwned){
            Get-ChildItem -LiteralPath $taskOwned -Recurse -File | Where-Object { $_.FullName.Contains('\RuntimeMaterials\') -or $_.FullName.Contains('\SourceMaterials\') -or $_.Name -in @('Footwear.asset','Footwear.asset.meta') } | ForEach-Object {
                $taskOverrides[$_.FullName.Substring($taskMain.Length+1)]=(Get-FileHash -LiteralPath $_.FullName).Hash
                Copy-Changed $_.FullName (Join-Path $taskProject $_.FullName.Substring($taskMain.Length+1))
            }
          }
          $taskOverrides | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'overrides-input.json') -Encoding utf8
        }
    }
    if($Action -in @('Preflight','Inspect','Import','All')){
        $taskPrepare=if($Action -eq 'Preflight'){'preflight'}elseif($Action -eq 'Inspect'){'inspect'}else{'prepare'}
        $taskArgs=@((Join-Path $PSScriptRoot 'wardrobe-recipe.py'),$taskPrepare,'--recipe',$taskRecipe,'--project',$taskProject)
        if($ForcePreparation){$taskArgs+='--force'}
        & $Python @taskArgs
        if($LASTEXITCODE -ne 0){throw 'Source preparation failed; inspect evidence and correct the recipe.'}
    }
    if($Action -in @('Import','Validate','Assets','All','ReimportTest')){
        foreach($taskSummary in @('report.json','report.md')){
            $taskStale=Join-Path $taskOutput $taskSummary
            if(Test-Path -LiteralPath $taskStale){Remove-Item -LiteralPath $taskStale}
        }
    }
    if($Action -in @('Import','All')){Run-Unity 'DazPose.UnityValidation.WardrobeImporter.Build' 'build'}
    if($Action -eq 'ReimportTest'){
        Run-Unity 'DazPose.UnityValidation.WardrobeImportValidation.SeedReimport' 'reimport-seed'
        try {
            Run-Unity 'DazPose.UnityValidation.WardrobeImporter.Build' 'reimport-build'
            Run-Unity 'DazPose.UnityValidation.WardrobeImportValidation.CheckReimport' 'reimport-check'
        } finally {Run-Unity 'DazPose.UnityValidation.WardrobeImportValidation.RestoreReimport' 'reimport-restore'}
    }
    if($Action -in @('Validate','Assets','All')){
        $taskChecks=if($Action -eq 'Assets'){@('assets')}else{@('assets','render','motion')}
        foreach($taskStage in $taskChecks){
            $taskEvidence=Join-Path $taskOutput ($taskStage+'\validation.json')
            if(Test-Path -LiteralPath $taskEvidence){Remove-Item -LiteralPath $taskEvidence}
        }
        Run-Unity 'DazPose.UnityValidation.WardrobeImportValidation.Run' 'assets'
        if($Action -ne 'Assets'){
        Run-Unity 'DazPose.UnityValidation.WardrobeImportRenderValidation.Run' 'render' $true $true
        Run-Unity 'DazPose.UnityValidation.LaraWardrobeMotionDiagnostics.RunRecipe' 'motion' $false $true
        }
        & $Python (Join-Path $PSScriptRoot 'wardrobe-recipe.py') summary --recipe $taskRecipe --project $taskProject
        if($LASTEXITCODE -ne 0){throw 'Validation report is incomplete.'}
    }
    if($Action -eq 'Publish'){
        $taskReport=Get-Content -LiteralPath (Join-Path $taskOutput 'report.json') -Raw | ConvertFrom-Json
        if($taskReport.recipeHash -ne (Get-FileHash -LiteralPath $taskRecipe).Hash.ToLowerInvariant()){throw 'Recipe changed since validation.'}
        foreach($taskPair in @(@($taskConfig.fbx,$taskReport.fbxHash),@($taskConfig.duf,$taskReport.dufHash))){
            $taskSource=if([IO.Path]::IsPathRooted($taskPair[0])){$taskPair[0]}else{Join-Path $taskMain $taskPair[0]}
            if((Get-FileHash -LiteralPath $taskSource).Hash.ToLowerInvariant() -ne $taskPair[1]){throw 'Source changed since validation.'}
        }
        $taskOwned=Join-Path $taskMain $taskConfig.destination
        $taskInputs=Get-Content -LiteralPath (Join-Path $taskOutput 'overrides-input.json') -Raw | ConvertFrom-Json
        if($taskConfig.shoes){
            $taskCalibrationRelative=if($taskConfig.poseCalibration){$taskConfig.poseCalibration}else{'configs/wardrobe/paired-tiptoe-calibration.json'}
            $taskCalibration=Join-Path $taskRoot $taskCalibrationRelative
            $taskHeel=Get-Content -LiteralPath (Join-Path $taskProject ($taskConfig.destination+'/heel-reference.json')) -Raw | ConvertFrom-Json
            if((Get-FileHash -LiteralPath $taskCalibration).Hash.ToLowerInvariant() -ne $taskHeel.poseCalibration.calibrationSHA256){throw 'Pose calibration changed since import; rerun All before publishing.'}
        }
        foreach($taskProperty in $taskInputs.PSObject.Properties){
            $taskTarget=Join-Path $taskMain $taskProperty.Name
            if(!(Test-Path -LiteralPath $taskTarget) -or (Get-FileHash -LiteralPath $taskTarget).Hash -ne $taskProperty.Value){throw 'Main-project overrides changed since import. Save edits and rerun All before publishing.'}
        }
        $taskGenerated=Join-Path $taskProject $taskConfig.destination
        $taskIncoming=Get-ChildItem -LiteralPath $taskGenerated -Recurse -File
        foreach($taskItem in $taskIncoming){
            $taskRelative=$taskItem.FullName.Substring($taskGenerated.Length+1);$taskTarget=Join-Path $taskOwned $taskRelative
            if(($taskRelative.Contains('RuntimeMaterials\') -or $taskRelative -in @('Footwear.asset','Footwear.asset.meta')) -and (Test-Path -LiteralPath $taskTarget)){
                $taskKey=$taskTarget.Substring($taskMain.Length+1)
                # A repeated publication may see the identical assets it just published.
                # Allow that exact match; still reject any new/edited artist bytes.
                if(!$taskInputs.PSObject.Properties[$taskKey] -and (Get-FileHash -LiteralPath $taskTarget).Hash -ne (Get-FileHash -LiteralPath $taskItem.FullName).Hash){throw 'An artist asset appeared after import; rerun All before publishing.'}
            }
        }
        $taskBackup=Join-Path $taskRoot ('.dazposewizard\backups\wardrobe-'+$taskConfig.id+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
        if(Test-Path -LiteralPath $taskOwned){Copy-Item -LiteralPath $taskOwned -Destination $taskBackup -Recurse}
        $taskReview='Review.unity'
        if(Test-Path -LiteralPath (Join-Path $taskOwned $taskReview)){$taskReview='Review-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.unity'}
        $taskIncoming | ForEach-Object {
            $taskRelative=$_.FullName.Substring($taskGenerated.Length+1);$taskTarget=Join-Path $taskOwned $taskRelative
            if($taskRelative -eq 'Review.unity'){$taskTarget=Join-Path $taskOwned $taskReview}
            if($taskRelative -eq 'Review.unity.meta' -and $taskReview -ne 'Review.unity'){return}
            Copy-Changed $_.FullName $taskTarget
        }
        if($taskReview -ne 'Review.unity'){
            $taskGuid=[guid]::NewGuid().ToString('N')
            "fileFormatVersion: 2`nguid: $taskGuid`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: " | Set-Content -LiteralPath ((Join-Path $taskOwned $taskReview)+'.meta') -Encoding utf8
        }
        $taskEvidenceMain=Join-Path $taskMain ('TestOutput\wardrobe-import\'+$taskConfig.id)
        New-Item -ItemType Directory -Force -Path $taskEvidenceMain | Out-Null
        Copy-Item -LiteralPath (Join-Path $taskOutput 'report.json'),(Join-Path $taskOutput 'report.md') -Destination $taskEvidenceMain -Force
        foreach($taskEvidenceFolder in @('assets','render','motion')){
            $taskEvidenceTarget=Join-Path $taskEvidenceMain $taskEvidenceFolder
            New-Item -ItemType Directory -Force -Path $taskEvidenceTarget | Out-Null
            Get-ChildItem -LiteralPath (Join-Path $taskOutput $taskEvidenceFolder) -File | ForEach-Object {Copy-Changed $_.FullName (Join-Path $taskEvidenceTarget $_.Name)}
        }
        if(Test-Path -LiteralPath (Join-Path $taskOutput 'reimport-validation.json')){Copy-Changed (Join-Path $taskOutput 'reimport-validation.json') (Join-Path $taskEvidenceMain 'reimport-validation.json')}
        $taskReport | Add-Member -NotePropertyName publishedReviewScene -NotePropertyValue ($taskConfig.destination+'/'+$taskReview) -Force
        $taskReport | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $taskEvidenceMain 'report.json') -Encoding utf8
        Add-Content -LiteralPath (Join-Path $taskEvidenceMain 'report.md') -Value ('Published review: '+$taskConfig.destination+'/'+$taskReview)
        Write-Output ('PUBLISHED_OWNED_ASSETS: '+$taskOwned+'; existing review scene/artist overrides preserved')
    }
    Write-Output ('IMPORT_OUTPUT: '+$taskOutput)
} finally {$taskLock.Dispose()}
