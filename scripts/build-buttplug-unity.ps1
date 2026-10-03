param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor',
    [string]$SourceDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'validation/DazPoseUnityValidation'
$package = Join-Path $project 'Packages/com.dazpose.buttplug-csharp'
$commit = 'ec8e6ff175e33dd75778cc91c485ec1a24353915'
$work = Join-Path ([IO.Path]::GetTempPath()) ('DazPoseButtplugBuild-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $work | Out-Null
if (!$SourceDirectory) {
    $zip = Join-Path $work 'source.zip'
    Invoke-WebRequest "https://github.com/buttplugio/buttplug-csharp/archive/$commit.zip" -OutFile $zip
    Expand-Archive $zip (Join-Path $work 'source')
    $SourceDirectory = Join-Path $work "source/buttplug-csharp-$commit/Buttplug"
}

# Compile against the JSON assembly that Unity actually loads. Upstream's NuGet
# binary binds JToken.ToString(Formatting), which was added after Unity's version.
$json = Get-ChildItem (Join-Path $project 'Library/PackageCache') -Directory -Filter 'com.unity.nuget.newtonsoft-json@*' |
    ForEach-Object { Join-Path $_.FullName 'Runtime/Newtonsoft.Json.dll' } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$json) { throw 'Import the Unity project first to populate its Newtonsoft package cache.' }
$data = Join-Path $UnityEditor 'Data'
$compiler = Get-ChildItem (Join-Path $data 'DotNetSdk/sdk') -Directory |
    ForEach-Object { Join-Path $_.FullName 'Roslyn/bincore/csc.dll' } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$compiler) { throw 'Unity Roslyn compiler was not found.' }
$versionSource = Join-Path $work 'AssemblyVersion.cs'
'[assembly: System.Reflection.AssemblyVersion("5.0.1.0")]' | Set-Content $versionSource
$output = Join-Path $work 'Buttplug.dll'
$compilerArguments = @('-nologo', '-target:library', '-langversion:latest', '-nostdlib', '-deterministic',
    "-out:$output", "-r:$json", "-pathmap:$SourceDirectory=/Buttplug,$work=/Build", $versionSource)
$compilerArguments += Get-ChildItem (Join-Path $data 'NetStandard/ref/2.1.0') -Filter '*.dll' |
    ForEach-Object { '-r:' + $_.FullName }
$compilerArguments += Get-ChildItem $SourceDirectory -Recurse -Filter '*.cs' | ForEach-Object FullName
& (Join-Path $data 'DotNetSdk/dotnet.exe') $compiler $compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Buttplug Unity build failed.' }
Copy-Item $output (Join-Path $package 'Runtime/Buttplug.dll') -Force
Get-FileHash (Join-Path $package 'Runtime/Buttplug.dll') -Algorithm SHA256
