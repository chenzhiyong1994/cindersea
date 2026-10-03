param([string]$Editor = $env:UNITY_EDITOR,
      [ValidateSet('Windows','JadePreview')][string]$Profile='Windows')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$unityProject=Join-Path $projectRoot 'native\Dicebound'
$versionFile=Join-Path $unityProject 'ProjectSettings/ProjectVersion.txt'
$versionLine=Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
if(!$versionLine){throw "The required Unity version is missing from $versionFile"}
$requiredVersion=($versionLine -replace '^m_EditorVersion:\s*','').Trim()

# Explicit configuration wins. Otherwise resolve the project's exact Hub version,
# then an editor installed on PATH. No machine-specific tool directories are used.
if($Editor){
    $editorCommand=Get-Command -Name $Editor -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if(!$editorCommand){throw "Unity Editor not found: $Editor. Pass -Editor <Unity.exe> or set UNITY_EDITOR."}
    $Editor=$editorCommand.Source
}
else{
    $candidates=[Collections.Generic.List[string]]::new()
    foreach($programDirectory in @($env:ProgramFiles,${env:ProgramFiles(x86)})){
        if($programDirectory){$candidates.Add((Join-Path $programDirectory "Unity/Hub/Editor/$requiredVersion/Editor/Unity.exe"))}
    }
    foreach($command in @(Get-Command Unity.exe,Unity -CommandType Application -All -ErrorAction SilentlyContinue)){
        $candidates.Add($command.Source)
    }
    $Editor=$candidates | Select-Object -Unique | Where-Object {Test-Path -LiteralPath $_ -PathType Leaf} | Select-Object -First 1
    if(!$Editor){throw "Unity $requiredVersion was not found in the standard Unity Hub folders or PATH. Install its Windows Build Support (Mono) module and pass -Editor <Unity.exe> or set UNITY_EDITOR."}
}
$reportedVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($Editor).ProductVersion
if($reportedVersion -and $reportedVersion -notlike "$requiredVersion*"){
    throw "Expected Unity $requiredVersion from ProjectVersion.txt, but $Editor reports $reportedVersion. Select the matching editor with -Editor or UNITY_EDITOR."
}
$logDirectory=Join-Path $projectRoot '.qa'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$buildLog=Join-Path $logDirectory 'native-build.log'
$arguments="-batchmode -nographics -quit -projectPath `"$unityProject`" -executeMethod Dicebound.Editor.NativeBuild.$Profile -logFile `"$buildLog`""
$buildProcess=Start-Process -FilePath $Editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
if($buildProcess.ExitCode -ne 0){Get-Content -LiteralPath $buildLog -Tail 50;throw "Unity build failed ($($buildProcess.ExitCode)). See $buildLog"}
if(!(Select-String -LiteralPath $buildLog -Pattern 'DICEBOUND_BUILD Succeeded' -Quiet)){throw "Unity did not report a successful player build. See $buildLog"}
# Unity can report a successful build when only a shader's colour pass failed.
# isSupported can still be true because its depth pass compiled; inspect compilation evidence.
if(Select-String -LiteralPath $buildLog -Pattern 'Shader error in' -Quiet){
    Select-String -LiteralPath $buildLog -Pattern 'Shader error in' | ForEach-Object {Write-Output $_.Line}
    throw "Player contains shader compilation errors. See $buildLog"
}
Write-Output (Join-Path $unityProject ('Builds\'+$Profile+'\Dicebound.exe'))
