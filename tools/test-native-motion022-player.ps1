param([string]$Player,[int]$Width=1600,[int]$Height=900,[int]$TimeoutSeconds=180,[switch]$ShowWindow)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$playerPath=if($Player){$Player}else{Join-Path $projectRoot 'native\Dicebound\Builds\Windows\Dicebound.exe'}
if(!(Test-Path -LiteralPath $playerPath -PathType Leaf)){throw 'Build the current Windows Player with tools/build-native.ps1 first.'}
$playerPath=(Resolve-Path -LiteralPath $playerPath).Path
$output=Join-Path $projectRoot ('.qa\motion022-'+$Width+'-'+$Height+'-'+(Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
$managed=Join-Path (Split-Path $playerPath -Parent) 'Dicebound_Data\Managed'
@($playerPath,(Join-Path $managed 'Dicebound.Runtime.dll'),(Join-Path $managed 'Dicebound.Core.dll'),(Join-Path $managed 'Dicebound.Persistence.dll')) |
    ForEach-Object {[ordered]@{file=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant()}} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-identity.json') -Encoding utf8
$arguments="--dicebound-motion022-verify --dicebound-save-dir `"$output`" -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$output\Player.log`""
# ScreenCapture reads the Player render target; the background QA window stays hidden.
$windowStyle=if($ShowWindow){'Normal'}else{'Hidden'}
$process=Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path $playerPath -Parent) -ArgumentList $arguments -WindowStyle $windowStyle -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Focused motion check timed out: $output"}
$reportPath=Join-Path $output 'motion022-verification.json'
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportPath) -or (Test-Path -LiteralPath (Join-Path $output 'motion022-failed.txt'))){
    if(Test-Path -LiteralPath (Join-Path $output 'Player.log')){Get-Content -LiteralPath (Join-Path $output 'Player.log') -Tail 45}
    throw "Focused motion check failed: $output"
}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if($report.mode -ne 'motion022-focus' -or $report.complete -ne $true -or $report.isolated -ne $true -or $report.presentationOnly -ne $true -or $report.fullJourneyVerification -ne $false -or $report.errors -ne 0 -or $report.width -ne $Width -or $report.height -ne $Height -or @($report.cases).Count -ne 26){throw "Incomplete focused motion evidence: $reportPath"}
$heroes=@('sixuan','lingfeng','cangling','yanzhuying','shangshuo')
$expected=@(
    foreach($hero in $heroes){"portrait-$hero"}
    foreach($hero in $heroes){"preview-$hero"}
    foreach($hero in $heroes){foreach($mode in @('complete','cancel','reduced')){"ultimate-$hero-$mode"}}
    'missing-menu-fallback'
)
foreach($name in $expected){
    $cases=@($report.cases | Where-Object name -eq $name)
    if($cases.Count -ne 1 -or $cases[0].savedUnchanged -ne $true){throw "Missing or changed checkpoint case: $name"}
    $case=$cases[0]
    if($name -like 'portrait-*' -or $name -like 'preview-*'){
        foreach($key in @('firstFrame','advanced','looped','reducedStill','cancelled','currentPortraitSource','correctVideoFormat','resourcesReleased')){if($case.$key -ne $true){throw "$name failed $key"}}
        if($name -like 'portrait-*'){
            if($case.uniformPortraitFrame -ne $true -or $case.portraitGapClosed -ne $true -or [Math]::Abs([double]$case.portraitPanelOverlap-29) -ge .5){throw "$name did not retain uniform framing and close the panel gap"}
        }elseif($case.completePreviewFrame -ne $true){throw "$name did not show the complete artwork at its original aspect ratio"}
    }elseif($name -eq 'missing-menu-fallback'){
        if($case.fallback -ne $true){throw "$name did not retain its fallback"}
    }elseif($name -like '*-reduced'){
        if($case.reducedStill -ne $true -or $case.firstFrame -ne $false -or $case.cancelled -ne $true){throw "$name did not respect reduced motion"}
    }else{
        if($case.firstFrame -ne $true -or $case.cancelled -ne $true){throw "$name did not display and clean up its decoder"}
        if($case.correctVideoFormat -ne $true){throw "$name did not use its source coverage format"}
        if($case.movieEntry -ne $true -or $case.noStillFlash -ne $true -or ($name -like '*-complete' -and $case.movieExit -ne $true)){throw "$name did not keep the movie visible through its transition"}
    }
    if($name -like 'ultimate-*'){
        foreach($key in @('currentPortraitSource','impactEffects','resourcesReleased')){if($case.$key -ne $true){throw "$name failed $key"}}
        if($null -eq $case.impactVertexCount -or ($name -like '*-reduced' -and $case.impactVertexCount -ne 0) -or ($name -notlike '*-reduced' -and $case.impactVertexCount -le 0)){throw "$name has incorrect impact geometry for its motion mode"}
    }
}
$expectedCaptures=@(foreach($name in $expected){if($name -like 'portrait-*' -or $name -like 'preview-*'){"$name-movie.png";"$name-still.png"}elseif($name -like 'ultimate-*'){"$name.png"}})
if(@($report.captures).Count -ne 35 -or @($report.captures | Select-Object -Unique).Count -ne 35 -or @($expectedCaptures | Where-Object {$_ -notin $report.captures}).Count -ne 0){throw "Missing or duplicate movie/static frame captures: $reportPath"}
foreach($file in $report.captures){$capture=Join-Path $output $file;if(!(Test-Path -LiteralPath $capture -PathType Leaf) -or (Get-Item -LiteralPath $capture).Length -le 24){throw "Missing frame capture: $capture"}}
Get-Content -LiteralPath $reportPath
Write-Output 'Focused presentation checks only; no battle actions, full journey, or user archive changes.'
Write-Output "Evidence: $output"
