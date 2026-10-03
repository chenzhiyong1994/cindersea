param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnetExe=& (Join-Path $PSScriptRoot 'resolve-native-dotnet.ps1')
Push-Location (Join-Path $projectRoot 'native/Tests')
try {
    & $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.SkillBuildTests.csproj')
    if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
}
finally { Pop-Location }
