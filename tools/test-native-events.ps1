param([switch]$ContentEdges,[switch]$BattleFeedback)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnetExe=& (Join-Path $PSScriptRoot 'resolve-native-dotnet.ps1')
if($BattleFeedback){& $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.EventTests.csproj') -- --battle-feedback}
elseif($ContentEdges){& $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.EventTests.csproj') -- --content-edge}
else{& $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.EventTests.csproj')}
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
