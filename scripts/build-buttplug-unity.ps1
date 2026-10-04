param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor',
    [string]$SourceDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'validation/DazPoseUnityValidation'
$package = Join-Path $project 'Packages/com.dazpose.buttplug-csharp'
$commit = 'ec8e6ff175e33dd75778cc91c485ec1a24353915'
$expectedUnityVersion = '6000.5.9f1'
$expectedJsonVersion = '3.2.2'
$expectedJsonSha256 = '7292D3EB508652D14726749DD27094F2D481AECCF2DB6427B62F68A71460897E'
$expectedButtplugSha256 = '3D528966315940E1E944AB038FC2EFBE26E3BD103E9918CCBFD8D67AE6FCCC6A'
$expectedRoslynSdk = '8.0.318'
if ((Split-Path $UnityEditor -Leaf) -ne $expectedUnityVersion) {
    throw "This rebuild is pinned to Unity $expectedUnityVersion; supplied editor was '$UnityEditor'."
}
$work = Join-Path ([IO.Path]::GetTempPath()) ('DazPoseButtplugBuild-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $work | Out-Null
if (!$SourceDirectory) {
    $zip = Join-Path $work 'source.zip'
    Invoke-WebRequest "https://github.com/buttplugio/buttplug-csharp/archive/$commit.zip" -OutFile $zip
    Expand-Archive $zip (Join-Path $work 'source')
    $SourceDirectory = Join-Path $work "source/buttplug-csharp-$commit/Buttplug"
}

# Compile against the exact cached JSON package resolved by this project. Never
# select an arbitrary cache entry when several package versions are installed.
$cache = Join-Path $project 'Library/PackageCache'
$jsonPackages = Get-ChildItem $cache -Directory -Filter 'com.unity.nuget.newtonsoft-json@*' |
    Where-Object {
        $manifest = Join-Path $_.FullName 'package.json'
        (Test-Path $manifest) -and ((Get-Content $manifest -Raw | ConvertFrom-Json).version -eq $expectedJsonVersion)
    }
if ($jsonPackages.Count -ne 1) {
    throw "Expected exactly one cached Newtonsoft.Json package at version $expectedJsonVersion; found $($jsonPackages.Count)."
}
$json = Join-Path $jsonPackages[0].FullName 'Runtime/Newtonsoft.Json.dll'
if (!(Test-Path $json)) { throw 'The resolved Newtonsoft.Json runtime assembly was not found.' }
$actualJsonSha256 = (Get-FileHash $json -Algorithm SHA256).Hash
if ($actualJsonSha256 -ne $expectedJsonSha256) {
    throw "Newtonsoft.Json input checksum mismatch: expected $expectedJsonSha256, got $actualJsonSha256."
}
$data = Join-Path $UnityEditor 'Data'
$compiler = Join-Path $data "DotNetSdk/sdk/$expectedRoslynSdk/Roslyn/bincore/csc.dll"
if (!(Test-Path $compiler)) { throw "Pinned Unity Roslyn compiler $expectedRoslynSdk was not found at $compiler." }
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
$actualButtplugSha256 = (Get-FileHash $output -Algorithm SHA256).Hash
if ($actualButtplugSha256 -ne $expectedButtplugSha256) {
    throw "Buttplug output checksum mismatch: expected $expectedButtplugSha256, got $actualButtplugSha256."
}
Copy-Item $output (Join-Path $package 'Runtime/Buttplug.dll') -Force
Get-FileHash (Join-Path $package 'Runtime/Buttplug.dll') -Algorithm SHA256
