# Compile current main-project sources with Unity's own cached compiler inputs,
# without closing the interactive editor or writing its Library assemblies.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'validation/DazPoseUnityValidation'
$proof = Join-Path $project 'TestOutput/TargetDrivenGripMigration'
$runtimeInput = Get-ChildItem (Join-Path $project 'Library/Bee/artifacts') -Filter 'Assembly-CSharp.rsp' -Recurse |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$runtimeInput) { throw 'Import the main project in Unity first to generate compiler inputs.' }
$editorInput = Join-Path $runtimeInput.Directory.FullName 'Assembly-CSharp-Editor.rsp'
New-Item -ItemType Directory -Path $proof -Force | Out-Null
$unityData = 'C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data'
$compiler = Join-Path $unityData 'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
Push-Location $project
try {
    foreach ($assembly in @('Assembly-CSharp', 'Assembly-CSharp-Editor')) {
        $inputPath = if ($assembly -eq 'Assembly-CSharp') { $runtimeInput.FullName } else { $editorInput }
        $rsp = Get-Content -LiteralPath $inputPath -Raw
        $rsp = [regex]::Replace($rsp, '(?m)^-out:.*$', ('-out:"' + $proof + '/' + $assembly + '.dll"'))
        $rsp = [regex]::Replace($rsp, '(?m)^-refout:.*$', ('-refout:"' + $proof + '/' + $assembly + '.ref.dll"'))
        if ($assembly -eq 'Assembly-CSharp-Editor') {
            $rsp = [regex]::Replace($rsp, '(?m)^-r:"[^"\r\n]*/Assembly-CSharp.ref.dll"\r?$',
                ('-r:"' + $proof + '/Assembly-CSharp.ref.dll"'))
        }
        $responsePath = Join-Path $proof ($assembly + '.rsp')
        Set-Content -LiteralPath $responsePath -Value $rsp
        $output = & (Join-Path $unityData 'NetCoreRuntime/dotnet.exe') $compiler ('@' + $responsePath) 2>&1
        $compileExit = $LASTEXITCODE
        $output | Set-Content -LiteralPath (Join-Path $proof ($assembly + '.compile.log'))
        if ($compileExit -ne 0) { $output | Write-Output; throw "$assembly compilation failed ($compileExit)." }
        Write-Output "PASS $assembly compiled from main-project sources."
    }
} finally { Pop-Location }
