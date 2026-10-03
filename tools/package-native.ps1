[CmdletBinding()]
param(
    [string]$BuildPath,
    [string]$OutputDirectory,
    [string]$IsccPath,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.22.4',
    [switch]$PortableOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Join-Path $repoRoot 'native/Dicebound'
if (-not $BuildPath) { $BuildPath = Join-Path $projectRoot 'Builds/Windows' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot "dist/Dicebound-$Version-beta.1" }
$BuildPath = [IO.Path]::GetFullPath($BuildPath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$baseName = "Dicebound-$Version-beta.1-Windows-x64"

function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file is missing: $Path" }
}

function Resolve-Compiler {
    $explicit = $IsccPath
    if (-not $explicit) { $explicit = $env:INNO_SETUP_COMPILER }
    if ($explicit) {
        Require-File $explicit
        return [IO.Path]::GetFullPath($explicit)
    }
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw 'Inno Setup 6 is required. Supply -IsccPath or INNO_SETUP_COMPILER, or use -PortableOnly.'
}

if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Refusing to overwrite an existing output directory: $OutputDirectory"
}
if ($OutputDirectory.StartsWith($BuildPath.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must not be inside the input build.'
}
Require-File (Join-Path $BuildPath 'Dicebound.exe')
Require-File (Join-Path $BuildPath 'UnityPlayer.dll')
Require-File (Join-Path $BuildPath 'Dicebound_Data/globalgamemanagers')
$settings = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectSettings.asset') -Raw
if ($settings -notmatch "(?m)^\s*bundleVersion:\s*$([regex]::Escape($Version))\s*$") {
    throw "The requested package version $Version does not match ProjectSettings."
}
$compiler = $null
if (-not $PortableOnly) {
    $compiler = Resolve-Compiler
    # ISCC's Windows version resource is 0.0.0.0 in some official releases.
    $compilerHelp = (& $compiler '/?' 2>&1 | Out-String)
    if ($compilerHelp -notmatch 'Inno Setup ([6-9]|[1-9][0-9]+) Command-Line Compiler') {
        throw 'Inno Setup 6 or later is required.'
    }
}

# Only known Unity Windows runtime roots enter the package. Logs, debug output,
# saves, and symbols are explicitly excluded even if found under an allowed root.
$runtimeRoots = @('Dicebound.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'D3D12', 'Dicebound_Data', 'MonoBleedingEdge')
$runtimeFiles = @(foreach ($name in $runtimeRoots) {
    $source = Join-Path $BuildPath $name
    if (-not (Test-Path -LiteralPath $source)) { continue }
    $item = Get-Item -LiteralPath $source
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Links are not allowed in build inputs: $source" }
    if ($item.PSIsContainer) {
        $descendants = @(Get-ChildItem -LiteralPath $source -Recurse -Force)
        foreach ($descendant in $descendants) {
            if ($descendant.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Links are not allowed in build inputs: $($descendant.FullName)"
            }
        }
        $descendants | Where-Object { -not $_.PSIsContainer }
    } else { $item }
}) | Where-Object {
    $_.FullName -notmatch '(?i)(DoNotShip|[\\/](Logs?|Saves?|UserData)[\\/]|\.(pdb|mdb|log|dmp|tmp)$|tactical-(journey|chronicle)\.json$)'
} | Sort-Object FullName
if ($runtimeFiles.Count -lt 20) { throw 'The Unity player is incomplete.' }

$payload = Join-Path $OutputDirectory 'payload/Dicebound'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
foreach ($file in $runtimeFiles) {
    $relative = $file.FullName.Substring($BuildPath.Length).TrimStart('\', '/')
    $destination = Join-Path $payload $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}

$credits = Join-Path $payload 'Credits'
New-Item -ItemType Directory -Path $credits | Out-Null
foreach ($name in @('LICENSE', 'ASSET_NOTICES.md')) {
    Require-File (Join-Path $repoRoot $name)
    Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination (Join-Path $credits $name)
}
$resourceRoot = Join-Path $projectRoot 'Assets/Resources'
$notices = @(
    'Fonts/LICENSE-OFL.txt', 'Fonts/MaShanZheng-OFL.txt', 'Fonts/LXGWWenKai-OFL.txt',
    'Audio/LICENSE-VSCO.txt', 'Audio/source-manifest.json',
    'Audio/Combat/LICENSE-CC0-1.0.txt', 'Audio/Combat/LICENSE-Kenney-Impact.txt',
    'Audio/Combat/LICENSE-Kenney-RPG.txt', 'Audio/Combat/LICENSE-rubberduck.txt', 'Audio/Combat/manifest.json',
    'Effects/NOTICE.txt'
)
foreach ($notice in $notices) {
    $source = Join-Path $resourceRoot $notice
    Require-File $source
    $destination = Join-Path $credits $notice
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
foreach ($name in @('vendor-environment-assets.json', 'vendor-tactical-polish-assets.json')) {
    $source = Join-Path $repoRoot "native/$name"
    Require-File $source
    Copy-Item -LiteralPath $source -Destination (Join-Path $credits $name)
}

# Unity resolves these package licenses locally during import/build. Preserve
# their original names under a package-specific folder; never ship PackageCache.
$packageCache = Join-Path $projectRoot 'Library/PackageCache'
if (-not (Test-Path -LiteralPath $packageCache -PathType Container)) {
    throw 'Unity PackageCache is missing. Import and build the project before packaging so dependency notices can be collected.'
}
foreach ($package in Get-ChildItem -LiteralPath $packageCache -Directory) {
    $packageManifest = Join-Path $package.FullName 'package.json'
    if (-not (Test-Path -LiteralPath $packageManifest)) { continue }
    $metadata = Get-Content -LiteralPath $packageManifest -Raw | ConvertFrom-Json
    $packageNotices = @(Get-ChildItem -LiteralPath $package.FullName -Recurse -File | Where-Object {
        $_.Name -match '^(LICENSE|COPYING|NOTICE|Third[ -]?Party[ -]?Notices)(\..*)?$' -and $_.Extension -ne '.meta'
    })
    foreach ($notice in $packageNotices) {
        $relative = $notice.FullName.Substring($package.FullName.Length).TrimStart('\', '/')
        $destination = Join-Path $credits "Packages/$($metadata.name)/$relative"
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $notice.FullName -Destination $destination
    }
}

@'
Dicebound / 烬海天阙 · Beta 1

Windows 10/11 · 64-bit · Simplified Chinese game interface
Windows 10/11 64 位；游戏界面为简体中文。

Run Dicebound.exe. Keep all companion folders beside the executable.
运行 Dicebound.exe；请保留同目录下的全部文件夹。

Saves: %USERPROFILE%\AppData\LocalLow\Dicebound Studio\Dicebound
存档位于上述用户目录。卸载安装版不会删除存档，便携版使用同一存档位置。

Code is MIT licensed. Art, music, fonts, and other materials retain their own
licenses. See Credits/ASSET_NOTICES.md and the accompanying license files.
代码使用 MIT 许可；素材保留各自许可，详见 Credits 目录。

Source / 源码: https://github.com/chenzhiyong1994/dicebound
Feedback / 反馈: https://github.com/chenzhiyong1994/dicebound/issues
'@ | Set-Content -LiteralPath (Join-Path $payload 'START_HERE.txt') -Encoding UTF8

$sourceRevision = $null
if (Get-Command git -ErrorAction SilentlyContinue) {
    $sourceRevision = & git -C $repoRoot rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { $sourceRevision = $null }
}
$manifest = [ordered]@{
    product = 'Dicebound'; version = $Version; channel = 'Beta 1'; platform = 'Windows x64'
    sourceCheckoutRevision = $sourceRevision
    files = @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($payload.Length).TrimStart('\', '/').Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $OutputDirectory "$baseName.zip"
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path -Parent $payload), $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
$artifacts = @($zipPath)
if (-not $PortableOnly) {
    $installerBase = "$baseName-Setup"
    & $compiler "/DPayloadDir=$payload" "/DPackageOutput=$OutputDirectory" "/DPackageVersion=$Version" "/DPackageBaseName=$installerBase" (Join-Path $PSScriptRoot 'installer/Dicebound.iss')
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE. Output was retained for inspection." }
    $installerPath = Join-Path $OutputDirectory "$installerBase.exe"
    Require-File $installerPath
    $artifacts += $installerPath
}
$checksums = foreach ($artifact in $artifacts) {
    "{0}  {1}" -f (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $artifact)
}
$checksums | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ASCII
$artifacts | ForEach-Object {
    $file = Get-Item -LiteralPath $_
    [pscustomobject]@{ File = $file.FullName; Bytes = $file.Length; SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
}
