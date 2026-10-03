param([string]$DotnetExecutable = $env:DICEBOUND_DOTNET)
$ErrorActionPreference = 'Stop'
$testsDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'native/Tests'
$sdkPolicy = (Get-Content -LiteralPath (Join-Path $testsDirectory 'global.json') -Raw | ConvertFrom-Json).sdk
$minimumVersion = [Version]$sdkPolicy.version

function Test-DotnetSdk([string]$Candidate) {
    try {
        $command = Get-Command -Name $Candidate -CommandType Application, ExternalScript -ErrorAction Stop | Select-Object -First 1
        $executable = $command.Source
        $LASTEXITCODE = 0
        $installed = @(& $executable --list-sdks 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "--list-sdks exited with $LASTEXITCODE" }
        $compatible = @($installed | Where-Object {
            if ("$_" -match '^(\d+\.\d+\.\d+)\s+\[') {
                $version = [Version]$Matches[1]
                $version -ge $minimumVersion -and $version.Major -eq $minimumVersion.Major -and $version.Minor -eq $minimumVersion.Minor
            }
        })
        if ($compatible.Count -eq 0) { throw "No stable .NET $($minimumVersion.Major).$($minimumVersion.Minor) SDK >= $minimumVersion found (a runtime alone is insufficient)" }
        Push-Location $testsDirectory
        try {
            $LASTEXITCODE = 0
            $selected = (@(& $executable --version 2>&1) -join "`n").Trim()
            if ($LASTEXITCODE -ne 0 -or $selected -notmatch '^\d+\.\d+\.\d+$') { throw "Cannot select SDK using native/Tests/global.json: $selected" }
            $version = [Version]$selected
            if ($version -lt $minimumVersion -or $version.Major -ne $minimumVersion.Major -or $version.Minor -ne $minimumVersion.Minor) { throw "Selected incompatible SDK $selected" }
        }
        finally { Pop-Location }
        return @{ Path = $executable; Error = $null }
    }
    catch { return @{ Path = $null; Error = "${Candidate}: $($_.Exception.Message)" } }
}

if ($DotnetExecutable) {
    $result = Test-DotnetSdk $DotnetExecutable
    if ($result.Error) { throw "DICEBOUND_DOTNET / -DotnetExecutable is invalid. $($result.Error)" }
    return $result.Path
}
$candidates = [Collections.Generic.List[string]]::new()
foreach ($command in @(Get-Command dotnet -CommandType Application -All -ErrorAction SilentlyContinue)) { $candidates.Add($command.Source) }
$failures = [Collections.Generic.List[string]]::new()
foreach ($candidate in @($candidates | Select-Object -Unique)) {
    $result = Test-DotnetSdk $candidate
    if ($result.Path) { return $result.Path }
    $failures.Add($result.Error)
}
throw "No compatible .NET SDK found on PATH. Install a stable .NET $($minimumVersion.Major).$($minimumVersion.Minor) SDK >= $minimumVersion, or set DICEBOUND_DOTNET to its dotnet executable.`n$($failures -join "`n")"
