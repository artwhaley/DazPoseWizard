$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\DazPose.App\DazPose.App.csproj'
$outputPath = Join-Path $repositoryRoot 'artifacts\win-x64'

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $outputPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published DazPoseTool to $outputPath"
