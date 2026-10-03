param([int]$Width=1600,[int]$Height=900,[string]$Player,[int]$TimeoutSeconds=240,[ValidateSet('default','d3d11')][string]$GraphicsApi='default',[switch]$ShowWindow)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$playerPath=if($Player){$Player}else{Join-Path $projectRoot 'native\Dicebound\Builds\Windows\Dicebound.exe'}
if(!(Test-Path -LiteralPath $playerPath -PathType Leaf)){throw 'Build the current Unity Player before running this focused layout verifier.'}
$playerPath=(Resolve-Path -LiteralPath $playerPath).Path
$managed=Join-Path (Split-Path $playerPath -Parent) 'Dicebound_Data\Managed'
$runtime=Join-Path $managed 'Dicebound.Runtime.dll'
# An older Player would ignore the short-mode flag and enter the experience journey loop.
# Refuse that binary before starting any process.
$bytes=[IO.File]::ReadAllBytes($runtime)
$hasShortMode=[Text.Encoding]::Unicode.GetString($bytes).Contains('--dicebound-ui022-verify') -or [Text.Encoding]::Unicode.GetString($bytes,1,$bytes.Length-1).Contains('--dicebound-ui022-verify')
if(!$hasShortMode){throw 'This Player does not contain the 0.22 short UI verifier. Rebuild it first; the full experience verifier was not launched.'}
$output=Join-Path $projectRoot ('.qa\experience-player-ui022-'+$Width+'-'+$Height+'-'+(Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
$arguments="--dicebound-experience-verify --dicebound-ui022-verify --dicebound-music 0 --dicebound-save-dir `"$output`" -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$output\Player.log`""
if($GraphicsApi -eq 'd3d11'){$arguments+=' -force-d3d11'}
@($playerPath,$runtime,(Join-Path $managed 'Dicebound.Core.dll'),(Join-Path $managed 'Dicebound.Persistence.dll')) | ForEach-Object {
    [ordered]@{file=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash}
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-identity.json') -Encoding utf8
# Keep the automated layout Player in the background; ScreenCapture runs inside Unity.
$windowStyle=if($ShowWindow){'Normal'}else{'Hidden'}
$process=Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path $playerPath -Parent) -ArgumentList $arguments -WindowStyle $windowStyle -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "UI 0.22 layout verifier timed out: $output"}
$reportPath=Join-Path $output 'ui022-verification.json'
if(!(Test-Path -LiteralPath $reportPath)){Get-Content -LiteralPath (Join-Path $output 'Player.log') -Tail 50;throw "UI 0.22 report missing: $output"}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if($process.ExitCode -ne 0 -or !$report.complete -or $report.mode -ne 'ui022-layout' -or !$report.isolated -or !$report.presentationFixtures -or $report.fullJourneyVerification -ne $false -or $report.actions -ne 0 -or $report.phase -ne 'map' -or $report.fixtureRestorations -ne 3 -or $report.entrypointChecks -lt 12 -or $report.resourceChecks -lt 12 -or $report.noticeChecks -ne 2 -or $report.uiBoundsChecks -lt 20 -or @($report.errors).Count -ne 0 -or $report.width -ne $Width -or $report.height -ne $Height){
    Get-Content -LiteralPath $reportPath
    throw "UI 0.22 layout verification failed: $output"
}
$required=@('01-trial-details.png','02-route.png','03-battle-selected.png','04-current-status.png','05-reward.png','06-rule-popup.png','07-shop.png','08-battle-last-page.png')
foreach($name in $required){
    $capture=Join-Path $output $name
    if($report.captures -notcontains $name -or !(Test-Path -LiteralPath $capture -PathType Leaf) -or (Get-Item -LiteralPath $capture).Length -le 24){throw "Required layout capture missing: $name"}
}
foreach($name in @('battle-selected','reward','shop')){if(!(Test-Path -LiteralPath (Join-Path $output "fixtures\$name.json") -PathType Leaf)){throw "Isolated fixture missing: $name"}}
$runtimeErrors=Select-String -LiteralPath (Join-Path $output 'Player.log') -Pattern 'NullReferenceException|MissingReferenceException|Shader error|Failed to capture screen shot|FALLBACK_GRAPHICS'
if($runtimeErrors){$runtimeErrors | ForEach-Object {$_.Line};throw "Player log contains a runtime/resource error: $output"}
Get-Content -LiteralPath $reportPath
Write-Output 'Presentation fixtures and live UI entries only; no full journey, battle regression or performance verification was run.'
Write-Output "Evidence: $output"
