param([string]$Player,[int]$Width=1600,[int]$Height=900,[switch]$ArtGuide,[int]$TimeoutSeconds=300)
$ErrorActionPreference='Stop'
function Assert-BridgeEvidence {
    param([string]$ReportPath,[string]$EvidenceDirectory,[int]$ExpectedWidth,[int]$ExpectedHeight)
    $report=Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    foreach($name in @('complete','isolated','legalCheckpoint','moveCommittedExactlyOnce','savedStateMatches','hoverDoesNotCommit','clickLocksTarget','supportPreview','damagePreview','invalidPreview','paintedSceneActive','liveDepthCopy','cameraBounds','cameraReset','cameraDoesNotCommit')){
        $property=$report.PSObject.Properties[$name]
        if($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne $true){throw "Missing or failed bridge evidence '$name': $ReportPath"}
    }
    foreach($name in @('errors','width','height','legalCellHitChecks','liveActions','calibratedLandingCells','battleIcons','panCases','zoomCases')){
        $property=$report.PSObject.Properties[$name]
        if($null -eq $property -or ($property.Value -isnot [int] -and $property.Value -isnot [long])){throw "Invalid bridge counter '$name': $ReportPath"}
    }
    if($report.errors -ne 0 -or $report.width -ne $ExpectedWidth -or $report.height -ne $ExpectedHeight -or $report.legalCellHitChecks -le 0 -or $report.liveActions -lt 2 -or $report.calibratedLandingCells -ne 106 -or $report.battleIcons -ne 20 -or $report.panCases -ne 4 -or $report.zoomCases -ne 2){throw "Incomplete bridge counters or dimensions: $ReportPath"}
    foreach($name in @('skill','sceneResource')){
        if($report.$name -isnot [string] -or [string]::IsNullOrWhiteSpace($report.$name)){throw "Missing bridge resource evidence '$name': $ReportPath"}
    }
    foreach($name in @('medianFrameMs','p95FrameMs')){
        $value=$report.$name
        if(($value -isnot [double] -and $value -isnot [float] -and $value -isnot [int] -and $value -isnot [long]) -or [double]::IsNaN([double]$value) -or [double]::IsInfinity([double]$value) -or $value -le 0){throw "Invalid bridge idle timing '$name': $ReportPath"}
    }
    if($report.p95FrameMs -lt $report.medianFrameMs){throw "Bridge idle percentile is below median: $ReportPath"}
    foreach($name in @('bridge-playable','bridge-zoom-in','bridge-pan-limit','bridge-camera-reset','bridge-move-hover','bridge-move-locked','bridge-invalid-target','bridge-support-hover','bridge-after-move','bridge-damage-hover','bridge-live-skill','bridge-after-skill')){
        $capture=Join-Path $EvidenceDirectory ($name+'.png')
        if(!(Test-Path -LiteralPath $capture -PathType Leaf) -or (Get-Item -LiteralPath $capture).Length -le 24){throw "Bridge screenshot was not written: $capture"}
        $stream=[IO.File]::OpenRead($capture)
        try{$header=New-Object byte[] 24;$read=$stream.Read($header,0,24)}finally{$stream.Dispose()}
        $signature=@(137,80,78,71,13,10,26,10)
        for($i=0;$i -lt 8;$i++){if($read -ne 24 -or $header[$i] -ne $signature[$i]){throw "Invalid bridge PNG header: $capture"}}
        $imageWidth=([int]$header[16] -shl 24) -bor ([int]$header[17] -shl 16) -bor ([int]$header[18] -shl 8) -bor [int]$header[19]
        $imageHeight=([int]$header[20] -shl 24) -bor ([int]$header[21] -shl 16) -bor ([int]$header[22] -shl 8) -bor [int]$header[23]
        if($imageWidth -ne $ExpectedWidth -or $imageHeight -ne $ExpectedHeight){throw "Bridge PNG dimensions differ from the real Player: $capture"}
    }
    return $report
}
$projectRoot=Split-Path $PSScriptRoot -Parent
$playerPath=if($Player){$Player}else{Join-Path $projectRoot 'native\Dicebound\Builds\Windows\Dicebound.exe'}
if(!(Test-Path -LiteralPath $playerPath -PathType Leaf)){throw 'Build the Windows player first.'}
$playerPath=(Resolve-Path -LiteralPath $playerPath).Path
$mode=if($ArtGuide){'bridge-art-guide'}else{'bridge-study'}
$output=Join-Path $projectRoot ('.qa\'+$mode+'-'+(Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
$extra=if($ArtGuide){'--dicebound-art-guide'}else{'--dicebound-bridge-verify'}
$arguments="--dicebound-bridge-study $extra --dicebound-save-dir `"$output`" -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$output\Player.log`""
$managed=Join-Path (Split-Path $playerPath -Parent) 'Dicebound_Data\Managed'
@($playerPath,(Join-Path $managed 'Dicebound.Runtime.dll'),(Join-Path $managed 'Dicebound.Core.dll'),(Join-Path $managed 'Dicebound.Persistence.dll')) |
    ForEach-Object {[ordered]@{file=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant()}} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-identity.json') -Encoding utf8
# This is an interactive Player; its visible swapchain is required for real screenshots.
$process=Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path $playerPath -Parent) -ArgumentList $arguments -WindowStyle Normal -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Bridge study timed out: $output"}
if($process.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $output 'bridge-failed.txt'))){
    Get-Content -LiteralPath (Join-Path $output 'Player.log') -Tail 45
    throw "Bridge study failed: $output"
}
if(!$ArtGuide){
    $reportPath=Join-Path $output 'bridge-verification.json'
    if(!(Test-Path -LiteralPath $reportPath)){throw "Bridge study did not produce verification: $output"}
    $report=Assert-BridgeEvidence $reportPath $output $Width $Height
    Get-Content -LiteralPath $reportPath
}
Write-Output "Evidence: $output"
