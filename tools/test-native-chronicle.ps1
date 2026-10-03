$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnetExe=& (Join-Path $PSScriptRoot 'resolve-native-dotnet.ps1')
$outputDirectory=Join-Path $projectRoot ('.qa/native-chronicle-tests/'+[Guid]::NewGuid().ToString('N'))
Push-Location (Join-Path $projectRoot 'native/Tests')
try {
    & $dotnetExe run --project (Join-Path $projectRoot 'native/Tests/Dicebound.ChronicleTests.csproj') -- $outputDirectory
    if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
}
finally { Pop-Location }
