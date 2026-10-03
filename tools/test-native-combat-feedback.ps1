param([string]$Player,[int]$Width=1600,[int]$Height=900,[int]$TimeoutSeconds=300)
$ErrorActionPreference='Stop'

function Assert-CombatFeedbackEvidence {
    param([string]$ReportPath,[string]$EvidenceDirectory,[int]$ExpectedWidth,[int]$ExpectedHeight)
    $report=Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    foreach($name in @('complete','isolated','legalCheckpoints')){
        if($report.$name -isnot [bool] -or $report.$name -ne $true){throw "Missing or failed combat feedback evidence '$name': $ReportPath"}
    }
    if($report.mode -ne 'combat-feedback-focus' -or $report.fullJourneyVerification -ne $false -or $report.errors -ne 0 -or $report.width -ne $ExpectedWidth -or $report.height -ne $ExpectedHeight -or $report.tacticalAudioClips -ne 23 -or $report.legalPreparationActions -le 0 -or @($report.actions).Count -ne 15){throw "Incomplete focused combat feedback report: $ReportPath"}
    $requiredActions=@('sixuan-strike','sixuan-balance','lingfeng-strike','lingfeng-cleave','cangling-strike','cangling-tide','yanzhuying-strike','yanzhuying-moon','shangshuo-strike','shangshuo-anchor','brace-shield','enemy-blocked-turn','battle-ending-shield','reduced-heavy','paused-heavy')
    foreach($name in $requiredActions){
        $cases=@($report.actions | Where-Object name -eq $name)
        if($cases.Count -ne 1){throw "Missing or duplicated combat feedback case '$name': $ReportPath"}
        $case=$cases[0]
        foreach($property in @('savedBeforeAnimation','exactlyOnce','savedStateMatches','cameraRestored','naturalNumberLifetime','cleanup')){
            if($case.$property -isnot [bool] -or $case.$property -ne $true){throw "Failed combat feedback case '$name' invariant '$property': $ReportPath"}
        }
        if($case.revisionAfter -ne $case.revisionBefore+1 -or $case.preparationActions -le 0){throw "Case '$name' was not prepared and committed through legal rules actions: $ReportPath"}
        $trace=Join-Path $EvidenceDirectory ($name+'-legal-preparation.txt')
        if(!(Test-Path -LiteralPath $trace -PathType Leaf) -or @(Get-Content -LiteralPath $trace).Count -ne $case.preparationActions){throw "Missing legal action trace for '$name': $trace"}
        if($name -ne 'enemy-blocked-turn' -and ($case.contactSynchronized -ne $true -or $case.impactCallbacks -ne 1)){throw "Skill contact was not synchronized exactly once for '$name': $ReportPath"}
        if($case.damageNumbers -gt 0 -and ($case.hitFlashAtContact -ne $true -or $case.contacts -le 0)){throw "Missing synchronized damage feedback for '$name': $ReportPath"}
    }
    $cleave=$report.actions | Where-Object name -eq 'lingfeng-cleave'
    if($cleave.localHoldObserved -ne $true -or $cleave.impactHoldSeconds -lt .02 -or $cleave.numberLifetimeSeconds -lt .70){throw "Heavy contact hold or natural damage-number lifetime was not observed: $ReportPath"}
    $brace=$report.actions | Where-Object name -eq 'brace-shield'
    $enemy=$report.actions | Where-Object name -eq 'enemy-blocked-turn'
    if($brace.shieldNumbers -le 0 -or $enemy.shieldNumbers -le 0 -or $enemy.contacts -le 0){throw "Shield and blocked enemy-turn feedback were not exercised: $ReportPath"}
    $ending=$report.actions | Where-Object name -eq 'battle-ending-shield'
    if($ending.damageNumbers -le 0 -or $ending.healNumbers -ne 0 -or $ending.shieldNumbers -ne 0 -or $ending.contacts -ne $ending.damageNumbers){throw "Postbattle restoration created a false combat receipt: $ReportPath"}
    $reduced=$report.actions | Where-Object name -eq 'reduced-heavy'
    if($reduced.reduced -ne $true -or $reduced.impulses -ne 0 -or $reduced.maxImpulsePixels -ne 0 -or $reduced.numberLifetimeSeconds -lt .32){throw "Reduced-motion feedback produced camera motion or cut off receipts: $ReportPath"}
    $paused=$report.actions | Where-Object name -eq 'paused-heavy'
    if($paused.interrupted -ne $true -or $paused.damageNumbers -le 0 -or $paused.impactCallbacks -ne 1){throw "Pause interruption did not exercise already committed live impact feedback: $ReportPath"}
    $requiredCaptures=@('sixuan-balance-contact.png','lingfeng-cleave-before.png','lingfeng-cleave-windup.png','lingfeng-cleave-contact.png','lingfeng-cleave-numbers.png','cangling-tide-contact.png','yanzhuying-moon-contact.png','shangshuo-anchor-contact.png','brace-shield-contact.png','enemy-blocked-turn-contact.png','enemy-blocked-turn-numbers.png','battle-ending-shield-contact.png','battle-ending-shield-after.png','reduced-heavy-contact.png','reduced-heavy-after.png','paused-heavy-contact.png','paused-heavy-after.png')
    foreach($name in $requiredCaptures){if($report.captures -notcontains $name){throw "Missing representative live action screenshot '$name': $ReportPath"}}
    foreach($name in $report.captures){
        $capture=Join-Path $EvidenceDirectory $name
        if(!(Test-Path -LiteralPath $capture -PathType Leaf) -or (Get-Item -LiteralPath $capture).Length -le 24){throw "Combat feedback screenshot was not written: $capture"}
        $stream=[IO.File]::OpenRead($capture)
        try{$header=New-Object byte[] 24;$read=$stream.Read($header,0,24)}finally{$stream.Dispose()}
        $signature=@(137,80,78,71,13,10,26,10)
        for($i=0;$i -lt 8;$i++){if($read -ne 24 -or $header[$i] -ne $signature[$i]){throw "Invalid combat feedback PNG header: $capture"}}
        $imageWidth=([int]$header[16] -shl 24) -bor ([int]$header[17] -shl 16) -bor ([int]$header[18] -shl 8) -bor [int]$header[19]
        $imageHeight=([int]$header[20] -shl 24) -bor ([int]$header[21] -shl 16) -bor ([int]$header[22] -shl 8) -bor [int]$header[23]
        if($imageWidth -ne $ExpectedWidth -or $imageHeight -ne $ExpectedHeight){throw "Combat feedback PNG dimensions differ from the actual Player: $capture"}
    }
    return $report
}

$projectRoot=Split-Path $PSScriptRoot -Parent
$playerPath=if($Player){$Player}else{Join-Path $projectRoot 'native\Dicebound\Builds\Windows\Dicebound.exe'}
if(!(Test-Path -LiteralPath $playerPath -PathType Leaf)){throw 'Build the Windows player with tools/build-native.ps1 first.'}
$playerPath=(Resolve-Path -LiteralPath $playerPath).Path
$output=Join-Path $projectRoot ('.qa\combat-feedback-'+$Width+'-'+$Height+'-'+(Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
$arguments="--dicebound-combat-feedback-verify --dicebound-save-dir `"$output`" -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$output\Player.log`""
$managed=Join-Path (Split-Path $playerPath -Parent) 'Dicebound_Data\Managed'
@($playerPath,(Join-Path $managed 'Dicebound.Runtime.dll'),(Join-Path $managed 'Dicebound.Core.dll'),(Join-Path $managed 'Dicebound.Persistence.dll')) |
    ForEach-Object {[ordered]@{file=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant()}} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-identity.json') -Encoding utf8
# Actual interactive Player verification needs a visible swapchain for real PNG captures.
$process=Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path $playerPath -Parent) -ArgumentList $arguments -WindowStyle Normal -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Combat feedback Player verification timed out: $output"}
$reportPath=Join-Path $output 'combat-feedback-verification.json'
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportPath) -or (Test-Path -LiteralPath (Join-Path $output 'combat-feedback-failed.txt'))){
    if(Test-Path -LiteralPath (Join-Path $output 'Player.log')){Get-Content -LiteralPath (Join-Path $output 'Player.log') -Tail 55}
    throw "Combat feedback Player verification failed: $output"
}
$report=Assert-CombatFeedbackEvidence $reportPath $output $Width $Height
Get-Content -LiteralPath $reportPath
Write-Output 'Focused combat feedback verification; this does not replace full ascent or tactical regressions.'
Write-Output "Evidence: $output"
