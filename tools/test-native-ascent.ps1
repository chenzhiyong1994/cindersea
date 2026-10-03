param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnetExe=& (Join-Path $PSScriptRoot 'resolve-native-dotnet.ps1')
& $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.AscentTests.csproj')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.ProgressionTests.csproj')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
