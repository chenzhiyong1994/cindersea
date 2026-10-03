param([int]$Width=1600,[int]$Height=900,[int]$TimeoutSeconds=1200,[string]$Player,[switch]$UiMatrix,[switch]$UiSmoke,[string]$GroundViewSource)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$playerPath=if($Player){$Player}else{Join-Path $projectRoot 'native\Dicebound\Builds\Windows\Dicebound.exe'}
if(!(Test-Path -LiteralPath $playerPath -PathType Leaf)){throw 'Build the Windows player with tools/build-native.ps1 first.'}
$playerPath=(Resolve-Path -LiteralPath $playerPath).Path
if($GroundViewSource -and ($UiSmoke -or $UiMatrix)){throw 'GroundViewSource is a focused reproduction and cannot be combined with UI smoke/matrix.'}
$kind=if($GroundViewSource){'tactical-ground-view-'}elseif($UiSmoke){'tactical-ui-smoke-'}else{'tactical-player-'}
$output=Join-Path $projectRoot ('.qa\'+$kind+$Width+'-'+$Height+'-'+(Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
if($GroundViewSource){
    $sourceCheckpoint=Join-Path $GroundViewSource 'tactical-journey.json'
    if(!(Test-Path -LiteralPath $sourceCheckpoint -PathType Leaf)){throw "Missing focused source checkpoint: $sourceCheckpoint"}
    Copy-Item -LiteralPath $sourceCheckpoint -Destination (Join-Path $output 'tactical-journey.json')
    [ordered]@{source=(Resolve-Path -LiteralPath $sourceCheckpoint).Path;sha256=(Get-FileHash -LiteralPath $sourceCheckpoint).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'source-checkpoint.json') -Encoding utf8
}
$arguments="--dicebound-tactical-verify --dicebound-music 0 --dicebound-save-dir `"$output`" -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$output\Player.log`""
if($UiMatrix){$arguments+=' --dicebound-ui-matrix'}
if($UiSmoke){$arguments+=' --dicebound-ui-smoke'}
if($GroundViewSource){$arguments+=' --dicebound-ground-view-verify'}
$managed=Join-Path (Split-Path $playerPath -Parent) 'Dicebound_Data\Managed'
$identity=@($playerPath,(Join-Path $managed 'Dicebound.Runtime.dll'),(Join-Path $managed 'Dicebound.Core.dll'),(Join-Path $managed 'Dicebound.Persistence.dll')) | ForEach-Object {[ordered]@{file=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash}}
$identity | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-identity.json') -Encoding utf8
# This is the actual interactive game preview, not a background build helper.
# Unity's screen capture needs a visible swapchain; hidden launch reports "Failed to capture screen shot".
$process=Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path $playerPath -Parent) -ArgumentList $arguments -WindowStyle Normal -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Tactical Player timed out: $output"}
$reportPath=Join-Path $output $(if($GroundViewSource){'ground-view-verify.json'}elseif($UiSmoke){'ui-smoke.json'}else{'verification.json'})
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportPath)){Get-Content -LiteralPath (Join-Path $output 'Player.log') -Tail 40;throw "Tactical Player failed: $output"}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if($GroundViewSource){
    if($report.mode -ne 'ground-view-focus' -or $report.complete -ne $true -or $report.normalTargetObscured -ne $true -or $report.committedExactlyOnce -ne $true -or $report.savedStateMatches -ne $true -or $report.actions -ne 1 -or $report.errors -ne 0 -or $report.revisionAfter -ne $report.revisionBefore+1 -or $report.groundViewChecks -le 0 -or $report.groundViewReveals -ne 1 -or $report.width -ne $Width -or $report.height -ne $Height){throw "Incomplete focused ground-view report: $reportPath"}
    foreach($name in @('ground-view-before.png','ground-view-revealed.png','ground-view-after.png')){if(!(Test-Path -LiteralPath (Join-Path $output $name) -PathType Leaf)){throw "Focused ground-view screenshot missing: $name"}}
    Write-Output 'Focused occlusion regression only; this does not replace the two-team full tactical verification.'
}elseif($UiSmoke){
    if($report.mode -ne 'ui-smoke' -or $report.complete -ne $true -or $report.fullJourneyVerification -ne $false -or $report.errors -ne 0 -or $report.phase -ne 'battle' -or $report.chapter -ne 2 -or $report.journeyMode -ne 'ascent' -or $report.floor -lt 5 -or $report.actions -le 0 -or $report.portraitChecks -le 0 -or @($report.inspectedHeroes).Count -ne 5 -or $report.width -ne $Width -or $report.height -ne $Height){throw "Incomplete tactical UI preview report: $reportPath"}
    $required=@('00-title.png','00-settings.png','01-party.png','team0-map-0.png','10-terrain-guide.png')
    # RuanZhuo remains out of the selectable roster, so the preview inspects five heroes only.
    $required+=@('sixuan','yanzhuying','lingfeng','cangling','shangshuo') | ForEach-Object { '01-portrait-'+$_+'.png' }
    # Depth routes draw node kinds from seeded pools, so shop/event/battle captures use pattern counts
    # instead of fixed sequence numbers.
    foreach($pattern in @('team0-shop-*.png','team0-event-*.png','team0-reward-*.png','team0-camp-*.png')){
        if(!@($report.captures | Where-Object {$_ -like $pattern}).Count){throw "UI preview is missing a required capture matching $pattern : $reportPath"}
    }
    if(@($report.captures | Where-Object {$_ -like 'team0-battle-*.png'}).Count -lt 2){throw "UI preview is missing at least two battle captures: $reportPath"}
    if(!@($report.captures | Where-Object {$_ -like 'team0-map-*.png'}).Count){throw "UI preview is missing its legal ascent map page: $reportPath"}
    foreach($name in $required){if($report.captures -notcontains $name){throw "UI preview did not report its required capture: $name"}}
    foreach($name in $report.captures){
        $capture=Join-Path $output $name
        if(!(Test-Path -LiteralPath $capture -PathType Leaf) -or (Get-Item -LiteralPath $capture).Length -le 24){throw "UI preview screenshot was not written: $capture"}
    }
    Write-Output 'UI preview only; this does not replace the two-team full tactical verification.'
}else{
    if($report.errors -ne 0 -or $report.journeys -ne 2 -or $report.phase -ne 'victory' -or $report.chapter -ne 4 -or $report.journeyMode -ne 'ascent' -or $report.floor -ne 11 -or $report.floors -ne 12 -or $report.mapEntries -ne 24 -or $report.mapInspectionChecks -ne 24 -or $report.shopPurchases -le 0 -or $report.audioPeak -le 0 -or $report.width -ne $Width -or $report.height -ne $Height){throw "Incomplete tactical Player report: $reportPath"}
    foreach($kind in @('battle','event','shop','elite','boss')){if($report.nodeKinds -notcontains $kind){throw "Tactical Player did not cover the live node kind: $kind"}}
    if($report.groundViewChecks -le 0 -or $report.groundViewReveals -le 0){throw "Tactical Player did not verify an obscured cell through the live ground-view control: $reportPath"}
}
Get-Content -LiteralPath $reportPath
Write-Output "Evidence: $output"
