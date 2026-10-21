param([Parameter(Mandatory=$true)][string]$RunLabel,[int]$TimeoutSeconds=1200,[switch]$SceneProbe)
$ErrorActionPreference='Stop'
$spikeRoot='C:\Users\artwh\DazPoseWizard-Sandbox'
$spikeUnity='C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe'
$spikeIdentity=Get-Content -LiteralPath (Join-Path $spikeRoot '.dazposewizard-sandbox.json') -Raw | ConvertFrom-Json
if ($spikeIdentity.sandboxProject -ne $spikeRoot -or $spikeRoot -eq $spikeIdentity.sourceProject -or $spikeIdentity.budgetBytes -ne 50GB) { throw 'Unexpected sandbox identity or approved cap.' }
$spikeOutput=Join-Path $spikeRoot 'TestOutput\S01'
$spikeLogs=Join-Path $spikeRoot 'Logs'
New-Item -ItemType Directory -Force -Path $spikeOutput,$spikeLogs | Out-Null
$spikeXml=Join-Path $spikeOutput ($RunLabel+'.xml')
$spikeLog=Join-Path $spikeLogs ('s01-'+$RunLabel+'.log')
if ((Test-Path -LiteralPath $spikeXml) -or (Test-Path -LiteralPath $spikeLog)) { throw 'Run label already exists; do not reuse stale evidence.' }
function Measure-SpikeBytes {
    return [long](Get-ChildItem -LiteralPath $spikeRoot -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
}
$spikeBytes=Measure-SpikeBytes
if ($spikeBytes -gt $spikeIdentity.budgetBytes) { throw 'Sandbox already exceeds approved cap.' }
$spikeExisting=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Select-Object ProcessId,Name)
$spikeArguments=@('-batchmode','-nographics','-projectPath',('"'+$spikeRoot+'"'),'-runTests','-testPlatform','EditMode','-assemblyNames','DazPose.Editor.Tests','-testResults',('"'+$spikeXml+'"'),'-logFile',('"'+$spikeLog+'"'))
if ($SceneProbe) {
    $spikeArguments=@('-batchmode','-nographics','-projectPath',('"'+$spikeRoot+'"'),'-executeMethod','DazPose.Editor.Studio.S01SceneProbe.Run','-quit','-logFile',('"'+$spikeLog+'"'))
}
$spikeWatch=[Diagnostics.Stopwatch]::StartNew();$spikeStart=[DateTime]::UtcNow
$spikeProcess=$null;$spikeStopped=$null;$spikePeak=$spikeBytes;$spikeNextCheck=20
try {
    $spikeProcess=Start-Process -FilePath $spikeUnity -ArgumentList $spikeArguments -WorkingDirectory $spikeRoot -WindowStyle Hidden -PassThru
    [pscustomobject]@{pid=$spikeProcess.Id;executable=$spikeUnity;project=$spikeRoot;startedUtc=$spikeStart.ToString('o');arguments=$spikeArguments} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $spikeOutput ($RunLabel+'.launch.json')) -Encoding utf8NoBOM
    while (!$spikeProcess.HasExited) {
        if ($spikeWatch.Elapsed.TotalSeconds -gt $TimeoutSeconds) { $spikeStopped='Timed out';break }
        if ($spikeWatch.Elapsed.TotalSeconds -ge $spikeNextCheck) {
            $spikeBytes=Measure-SpikeBytes;$spikePeak=[Math]::Max($spikePeak,$spikeBytes);$spikeNextCheck=$spikeWatch.Elapsed.TotalSeconds+20
            if ($spikeBytes -gt $spikeIdentity.budgetBytes) { $spikeStopped='Disk cap exceeded';break }
        }
        Start-Sleep -Milliseconds 250;$spikeProcess.Refresh()
    }
} finally {
    # Only this exact owned process and its children can be stopped. Existing Editor processes are untouched.
    if ($null -ne $spikeProcess) {
        $spikeProcess.Refresh()
        if (!$spikeProcess.HasExited) { $spikeProcess.Kill($true);$spikeProcess.WaitForExit() }
    }
    $spikeWatch.Stop()
}
$spikeProcess.Refresh();$spikeSummary=$null
if (Test-Path -LiteralPath $spikeXml) {
    [xml]$spikeDocument=Get-Content -LiteralPath $spikeXml -Raw
    $spikeNode=$spikeDocument.SelectSingleNode('/test-run')
    if ($null -ne $spikeNode) { $spikeSummary=[ordered]@{result=$spikeNode.result;total=$spikeNode.total;passed=$spikeNode.passed;failed=$spikeNode.failed;skipped=$spikeNode.skipped;testcasecount=$spikeNode.testcasecount} }
}
$spikeEndBytes=Measure-SpikeBytes
$spikeResult=[ordered]@{label=$RunLabel;startedUtc=$spikeStart.ToString('o');wallSeconds=[Math]::Round($spikeWatch.Elapsed.TotalSeconds,3);exitCode=$spikeProcess.ExitCode;ownedPid=$spikeProcess.Id;stillRunning=(!$spikeProcess.HasExited);stoppedReason=$spikeStopped;result=$spikeSummary;xmlPath=$spikeXml;logPath=$spikeLog;sandboxBytes=$spikeEndBytes;observedPeakBytes=[Math]::Max($spikePeak,$spikeEndBytes);existingUnityProcesses=$spikeExisting}
$spikeResult | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $spikeOutput ($RunLabel+'.json')) -Encoding utf8NoBOM
$spikeResult | ConvertTo-Json -Depth 6
