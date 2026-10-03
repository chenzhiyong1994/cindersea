param([string]$Player,[int]$Width=1600,[int]$Height=900,[int]$TimeoutSeconds=300)
$ErrorActionPreference='Stop'
if($Width -le 0 -or $Height -le 0 -or $TimeoutSeconds -le 0){throw 'Dimensions and timeout must be positive.'}
$projectRoot=Split-Path $PSScriptRoot -Parent
$playerPath=if($Player){$Player}else{Join-Path $projectRoot 'native\Dicebound\Builds\Windows\Dicebound.exe'}
if(!(Test-Path -LiteralPath $playerPath -PathType Leaf)){throw 'Build the Windows Player with tools/build-native.ps1 first.'}
$playerPath=(Resolve-Path -LiteralPath $playerPath).Path
$output=Join-Path $projectRoot ('.qa\bgm-player-'+$Width+'-'+$Height+'-'+(Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
$managed=Join-Path (Split-Path $playerPath -Parent) 'Dicebound_Data\Managed'
@($playerPath,(Join-Path $managed 'Dicebound.Runtime.dll'),(Join-Path $managed 'Dicebound.Core.dll'),(Join-Path $managed 'Dicebound.Persistence.dll')) |
    ForEach-Object {[ordered]@{file=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant()}} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-identity.json') -Encoding utf8
$arguments="--dicebound-bgm-verify --dicebound-save-dir `"$output`" -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$output\Player.log`""
# No captures are needed. Keep the real audio-capable Player hidden; do not pass -nographics.
$process=Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path $playerPath -Parent) -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while(!$process.WaitForExit(1000)){
    if([DateTime]::UtcNow -ge $deadline){Stop-Process -Id $process.Id;throw "Focused BGM Player timed out: $output"}
}
$reportPath=Join-Path $output 'bgm-verification.json'
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportPath) -or (Test-Path -LiteralPath (Join-Path $output 'bgm-failed.txt'))){
    if(Test-Path -LiteralPath (Join-Path $output 'Player.log')){Get-Content -LiteralPath (Join-Path $output 'Player.log') -Tail 50}
    throw "Focused BGM Player failed: $output"
}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
foreach($property in @('complete','isolated','legalCheckpoints','preparationOnlyInMemory','musicZero','paused','resumed','ducked','duckRecovered','unknownFallback','recordedRestored')){
    if($report.$property -isnot [bool] -or $report.$property -ne $true){throw "Missing or failed BGM evidence '$property': $reportPath"}
}
if($report.mode -ne 'bgm-focus' -or $report.fullJourneyVerification -ne $false -or $report.errors -ne 0 -or $report.recordedClips -ne 7 -or $report.width -ne $Width -or $report.height -ne $Height -or @($report.cases).Count -ne 12){throw "Incomplete focused BGM report: $reportPath"}
$expected=@{title='title';party='title';map='travel';shop='travel';event='travel';battle='battle';elite='elite';'summit-ordinary'='battle';boss='boss';reward='victory';victory='victory';defeat='defeat'}
$titles=@{title='江湖启程';travel='晴岚行路';battle='回水交锋';elite='强敌临阵';boss='天阙决战';victory='同袍凯旋';defeat='余烬再行'}
$traces=@{}
foreach($traceName in @('winning-legal-actions.json','losing-legal-actions.json')){
    $tracePath=Join-Path $output $traceName
    if(!(Test-Path -LiteralPath $tracePath -PathType Leaf)){throw "Missing legal preparation trace: $tracePath"}
    $traces[$traceName]=Get-Content -LiteralPath $tracePath -Raw | ConvertFrom-Json
}
if(@($traces['winning-legal-actions.json'].actions).Count -ne $report.winningPreparationActions -or @($traces['losing-legal-actions.json'].actions).Count -ne $report.losingPreparationActions -or $report.winningPreparationActions -le 0 -or $report.losingPreparationActions -le 0){throw 'Legal preparation action counts differ from their traces.'}
foreach($name in $expected.Keys){
    $cases=@($report.cases | Where-Object name -eq $name)
    if($cases.Count -ne 1){throw "Missing or duplicated BGM scene '$name'."}
    $case=$cases[0];$key=$expected[$name]
    if($case.key -ne $key -or $case.clip -ne $key -or $case.title -ne $titles[$key] -or $case.channels -ne 2 -or $case.frequency -le 0 -or $case.clipSeconds -le 2 -or $case.audioSamples -le 0 -or $case.audioPeak -lt .0001){throw "Wrong recorded resource or silent signal in scene '$name'."}
    foreach($property in @('recorded','streaming','stereo','looping','nonSpatial','sameSource','positionContinues')){
        if($case.$property -isnot [bool] -or $case.$property -ne $true){throw "BGM scene '$name' failed '$property'."}
    }
    if($name -ne 'title' -and $name -ne 'party'){
        if($case.decoded -ne $true -or $case.savedStateMatches -ne $true -or !(Test-Path -LiteralPath (Join-Path $output $case.checkpointFile) -PathType Leaf) -or !$traces.ContainsKey($case.traceFile) -or $case.preparationActions -lt 0 -or $case.preparationActions -gt @($traces[$case.traceFile].actions).Count){throw "Missing legal saved checkpoint evidence in scene '$name'."}
    }
}
foreach($key in $titles.Keys){if(!@($report.cases | Where-Object {$_.key -eq $key -and $_.loopWrapped -eq $true}).Count){throw "Recorded track '$key' did not cross its loop boundary."}}
Get-Content -LiteralPath $reportPath
Write-Output 'Focused recorded music routing and playback only. Journey preparation ran in memory; this does not replace full Player journey, combat, visual or seamless-loop listening checks.'
Write-Output "Evidence: $output"
