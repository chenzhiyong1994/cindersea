param([switch]$Focused)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnetExe=& (Join-Path $PSScriptRoot 'resolve-native-dotnet.ps1')
Push-Location (Join-Path $projectRoot 'native/Tests')
try {
    $checkArguments=@((Join-Path $projectRoot '.qa/native-tactical-tests'))
    if($Focused){$checkArguments+='--focused'}
    & $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.TacticalTests.csproj') -- @checkArguments
    if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
}
finally { Pop-Location }
