param([int]$Width=1600,[int]$Height=900,[string]$Player,[int]$TimeoutSeconds=1200)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$playerPath=if($Player){$Player}else{Join-Path $projectRoot 'native\Dicebound\Builds\Windows\Dicebound.exe'}
if(!(Test-Path -LiteralPath $playerPath -PathType Leaf)){throw 'Build the Unity Player first.'}
$playerPath=(Resolve-Path -LiteralPath $playerPath).Path
$output=Join-Path $projectRoot ('.qa\experience-player-'+$Width+'-'+$Height+'-'+(Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
$arguments="--dicebound-experience-verify --dicebound-save-dir `"$output`" -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$output\Player.log`""
$managed=Join-Path (Split-Path $playerPath -Parent) 'Dicebound_Data\Managed'
@($playerPath,(Join-Path $managed 'Dicebound.Runtime.dll'),(Join-Path $managed 'Dicebound.Core.dll'),(Join-Path $managed 'Dicebound.Persistence.dll')) | ForEach-Object {[ordered]@{file=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-identity.json') -Encoding utf8
# This visible Player is the actual visual verification preview and needs a swapchain for capture.
$process=Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path $playerPath -Parent) -ArgumentList $arguments -WindowStyle Normal -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Focused Player timed out: $output"}
$reportPath=Join-Path $output 'experience-verification.json'
if(!(Test-Path -LiteralPath $reportPath)){Get-Content -LiteralPath (Join-Path $output 'Player.log') -Tail 50;throw "No focused Player report: $output"}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if($process.ExitCode -ne 0 -or !$report.complete -or $report.phase -ne 'victory' -or $report.checks -le 0 -or $report.actions -le 0 -or $report.width -ne $Width -or $report.height -ne $Height){Get-Content -LiteralPath $reportPath;throw "Focused Player failed: $output"}
if($report.uiBoundsChecks -le 0 -or $report.receiptChecks -le 0 -or $report.inspectorSelections -le 0 -or $report.loadedRelicIcons -ne 34 -or $report.helpPages -ne 3 -or $report.pathChecks -le 0 -or !$report.eventChecked -or !$report.rewardReceiptChecked -or !$report.steamValveChecked){throw "Focused Player missed interaction coverage: $output"}
if($report.trialGuidePages -ne 1 -or !$report.goldRewardChecked -or !$report.wrenchSelectionChecked -or $report.smallSkillPageChecks -ne 2 -or $report.fixtureRestorations -ne 5){throw "Focused Player missed fixture coverage: $output"}
foreach($name in @('00-trial-details.png','00-fixture-gold-reward.png','00-fixture-wrench-throw-source.png','00-fixture-skill-last-page-1.png','00-fixture-skill-last-page-2.png')){if($report.captures -notcontains $name -or !(Test-Path -LiteralPath (Join-Path $output $name))){throw "Fixture screenshot missing: $name"}}
foreach($kind in @('battle','shop','elite','boss')){if($report.visited -notcontains $kind){throw "Focused route missed $kind"}}
foreach($name in @('00-route.png','00-route-party.png','00-route-inventory.png','00-guide-1.png','00-guide-2.png','00-guide-3.png','01-selected-move-path.png','01-selected-skill.png','01-steam-valve.png','01-battle-terrain.png','02-party-details.png','03-inventory.png','04-enemy-details.png','05-terrain-details.png','06-insufficient-resource.png','07-next-round.png','08-elite.png','09-event-choices.png','09-event-receipt.png','09-reward-receipt.png','09-reward.png','09-reward-party.png','09-reward-inventory.png','10-shop.png','10-shop-rerolled.png','11-boss.png','12-victory.png')){
    if($report.captures -notcontains $name -or !(Test-Path -LiteralPath (Join-Path $output $name))){throw "Focused Player screenshot missing: $name"}
}
Get-Content -LiteralPath $reportPath
Write-Output "Evidence: $output"
