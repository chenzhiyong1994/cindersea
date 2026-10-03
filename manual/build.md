# Build and development · 构建与开发

[English README](../README.md#build) · [中文 README](../README.zh-CN.md#build)

<a id="download"></a>
## Download and play / 下载游玩

For **Windows x64**, choose a package from [Beta 1](https://github.com/chenzhiyong1994/dicebound/releases/tag/v0.22.4-beta.1):

在 **Windows x64** 上，从 [Beta 1 发行页](https://github.com/chenzhiyong1994/dicebound/releases/tag/v0.22.4-beta.1)选择：

| Package / 发行包 | Use / 使用方式 |
| --- | --- |
| [Setup.exe](https://github.com/chenzhiyong1994/dicebound/releases/download/v0.22.4-beta.1/Dicebound-0.22.4-beta.1-Windows-x64-Setup.exe) | Follow the setup wizard / 按向导完成安装 |
| [Portable ZIP / 免安装 ZIP](https://github.com/chenzhiyong1994/dicebound/releases/download/v0.22.4-beta.1/Dicebound-0.22.4-beta.1-Windows-x64.zip) | Extract everything, then run `Dicebound.exe` / 完整解压后运行 `Dicebound.exe` |

You do not need Unity, PowerShell 7, or the .NET SDK to play. Keep all portable-package files together. The game interface is in Simplified Chinese. Both packages contain the same Beta 1 game; the installer and game executable are not code-signed, so Windows may show a publisher or reputation warning.

游玩不需要 Unity、PowerShell 7 或 .NET SDK。免安装版请保留全部同目录文件。游戏界面为简体中文，两种发行包包含相同的 Beta 1 游戏。安装程序与游戏程序暂未代码签名，Windows 可能显示发布者或信誉提示。

The installer runs **for the current user without administrator privileges**, using `%LOCALAPPDATA%\Programs\Dicebound` by default. It adds a Start menu entry and offers an optional desktop shortcut. Uninstall through Windows Settings; uninstalling removes installed program files and shortcuts but keeps your LocalLow saves. The portable version uses the same save location.

安装程序**仅为当前用户安装，无需管理员权限**，默认目录为 `%LOCALAPPDATA%\Programs\Dicebound`。安装后提供开始菜单入口，桌面快捷方式可按需勾选。可在 Windows 设置中卸载；卸载会移除安装的程序文件和快捷方式，并保留 LocalLow 中的存档。免安装版使用相同的存档位置。

Download [SHA256SUMS.txt](https://github.com/chenzhiyong1994/dicebound/releases/download/v0.22.4-beta.1/SHA256SUMS.txt) from the same release. In PowerShell, compute the downloaded file's SHA-256 and compare the full value with its matching filename in that file:

从同一发行页下载 [SHA256SUMS.txt](https://github.com/chenzhiyong1994/dicebound/releases/download/v0.22.4-beta.1/SHA256SUMS.txt)，在 PowerShell 中计算已下载文件的 SHA-256，并与清单中对应文件名的完整值逐位核对：

```powershell
Get-FileHash "./Dicebound-0.22.4-beta.1-Windows-x64-Setup.exe" -Algorithm SHA256
```

A matching hash checks file integrity; it does not replace code signing. Save locations and backup instructions are [below](#saves-and-isolated-testing--存档与隔离检查).

校验值一致表示文件完整性匹配，不能代替代码签名。存档位置与备份方法见[下方](#saves-and-isolated-testing--存档与隔离检查)。

## Requirements / 环境要求

| Tool / 工具 | Version or requirement / 版本或要求 |
| --- | --- |
| Host and Player / 开发与运行平台 | Windows x64 |
| Unity Editor | **6000.3.24f1**, Unity 6.3 LTS, with Windows Build Support / 安装 Windows Build Support |
| Universal Render Pipeline | **17.3.0**, resolved by the project / 由项目依赖解析 |
| PowerShell | **7+**, executable `pwsh` |
| Git | To clone and contribute / 用于克隆与贡献 |
| .NET SDK | **10**, optional for standalone rule checks / 仅独立规则检查需要 |

Install Unity separately and accept its applicable terms. First import requires network access for Unity packages. Node.js, image-generation services, and video-generation accounts are not required to build or play the checked-in game.

请单独安装 Unity 并遵守其适用条款。首次导入需要联网获取 Unity 依赖。构建及运行已提交的游戏不需要 Node.js、图像生成服务或视频生成账号。

The independently exported Beta 1 project has passed a fresh Windows build. A full regression pass and final in-game visual review have not been performed for this publication. To build your own Player, follow the steps below.

独立导出的 Beta 1 工程已通过全新 Windows 构建；本次未执行完整回归或最终实机视觉验收。需要自行构建时，可按下文生成 Player。

## Build the Windows Player / 构建 Windows 游戏

Run from the repository root, replacing the example Editor path with your installation:

在仓库根目录执行，并将示例 Editor 路径替换为本机实际安装位置：

```powershell
pwsh -NoProfile -File tools/build-native.ps1 -Editor "C:/path/to/6000.3.24f1/Editor/Unity.exe"
```

The script invokes Unity in batch mode, checks the build result and shader errors, and writes:

脚本以批处理模式调用 Unity，检查构建结果和 Shader 错误。输出为：

```text
native/Dicebound/Builds/Windows/Dicebound.exe
.qa/native-build.log
```

Keep the complete build directory together when running or packaging the game. Close the same project in Unity before a batch build if the Editor reports that it is already open. For a separate local preview output:

运行或打包时保留完整构建目录。若 Unity 报告项目已被打开，请先关闭该项目再运行批处理构建。需要单独的本地预览目录时：

```powershell
pwsh -NoProfile -File tools/build-native.ps1 -Editor "C:/path/to/6000.3.24f1/Editor/Unity.exe" -Profile JadePreview
```

That output is `native/Dicebound/Builds/JadePreview/`. Alternatively, open `native/Dicebound` through Unity Hub using the pinned Editor version. Do not upgrade packages incidentally while preparing an unrelated change.

其输出为 `native/Dicebound/Builds/JadePreview/`。也可通过 Unity Hub 使用指定 Editor 版本打开 `native/Dicebound`。处理无关修改时，不要顺带升级项目依赖。

## Rule checks / 规则检查

The standalone checks use .NET 10 and do not launch the Unity Player. With the SDK installed, run the checks relevant to your change:

独立规则检查使用 .NET 10，不启动 Unity Player。安装 SDK 后，按改动范围选择：

```powershell
pwsh -NoProfile -File tools/test-native-tactical.ps1 -Focused
pwsh -NoProfile -File tools/test-native-ascent.ps1
pwsh -NoProfile -File tools/test-native-chronicle.ps1
```

These commands are development entry points, not a claim that the current beta has passed every suite. Rule and persistence checks do not establish visual layout, video playback, sound quality, or Player performance. The beta's known validation boundary is recorded in [CHANGELOG.md](../CHANGELOG.md).

这些命令是开发入口，不代表当前 Beta 已通过全部测试。规则和存档检查不能证明界面排版、视频播放、声音或 Player 性能。Beta 的实际验证边界见 [CHANGELOG.md](../CHANGELOG.md)。

## Saves and isolated testing / 存档与隔离检查

Normal Windows saves are stored under:

Windows 默认存档位置：

```text
%USERPROFILE%\AppData\LocalLow\Dicebound Studio\Dicebound\
```

`tactical-journey.json` stores the current journey; `tactical-chronicle.json` stores the chronicle. Settings include import/export. Back up the directory before testing a newer build or modifying saves.

`tactical-journey.json` 保存当前旅程，`tactical-chronicle.json` 保存纪事；设置提供导入导出。在试用新版或修改存档前，请先备份目录。

To test without using normal progress, supply a new, empty save directory:

需要独立检查时，指定一个全新的空目录：

```powershell
& "./native/Dicebound/Builds/Windows/Dicebound.exe" --dicebound-save-dir "C:/path/to/empty-dicebound-test-save"
```

Do not attach a save to a public issue without checking its contents. Never commit player saves or generated logs to the source repository.

在公开 issue 附加存档前，请检查其内容。不要将玩家存档或生成日志提交到源码仓库。

## Packaging / 打包

First [build the Windows Player](#build-the-windows-player--构建-windows-游戏) in this checkout. The packaging script needs its `Library/PackageCache` to collect dependency notices. Install **Inno Setup 6 or later** to compile the installer, then run from the repository root:

先在当前 checkout 中[构建 Windows Player](#build-the-windows-player--构建-windows-游戏)。打包脚本需要该工程的 `Library/PackageCache` 来收集依赖许可。安装 **Inno Setup 6 或更高版本**后，在仓库根目录执行：

```powershell
pwsh -NoProfile -File tools/package-native.ps1 -IsccPath "C:/Program Files (x86)/Inno Setup 6/ISCC.exe"
```

Replace the example `ISCC.exe` path with your installation. Alternatively, set `INNO_SETUP_COMPILER` and omit `-IsccPath`; when neither is set, the script checks `PATH` and common Inno Setup 6 installation directories:

将示例 `ISCC.exe` 路径替换为实际安装位置。也可设置 `INNO_SETUP_COMPILER` 后省略 `-IsccPath`；两者均未设置时，脚本会检查 `PATH` 及常见的 Inno Setup 6 安装目录：

```powershell
$env:INNO_SETUP_COMPILER = "C:/Program Files (x86)/Inno Setup 6/ISCC.exe"
pwsh -NoProfile -File tools/package-native.ps1
```

The default input is `native/Dicebound/Builds/Windows`. The script gathers the allowed runtime files and license notices, creates the portable archive, and invokes `tools/installer/Dicebound.iss` to compile the installer. With the default `-Version 0.22.4`, the output is:

默认输入为 `native/Dicebound/Builds/Windows`。脚本收集允许分发的运行文件和许可声明，生成免安装压缩包，再调用 `tools/installer/Dicebound.iss` 编译安装程序。默认版本参数为 `-Version 0.22.4`，输出内容为：

```text
dist/Dicebound-0.22.4-beta.1/
  Dicebound-0.22.4-beta.1-Windows-x64-Setup.exe
  Dicebound-0.22.4-beta.1-Windows-x64.zip
  SHA256SUMS.txt
  payload/Dicebound/
```

The `payload` folder is the unpacked package content; publish the installer, ZIP, and checksum file as release attachments. Each package contains `Credits/` with [ASSET_NOTICES.md](../ASSET_NOTICES.md), the code [LICENSE](../LICENSE), third-party notices, font licenses, and Unity package notices. Unity runtime components are not MIT-licensed project code.

`payload` 保存未压缩的发行内容；发布时将安装包、ZIP 和校验文件作为发行附件。两种包均附带 `Credits/`，其中包含 [ASSET_NOTICES.md](../ASSET_NOTICES.md)、代码 [LICENSE](../LICENSE)、第三方声明、字体许可和 Unity 包许可。Unity 运行组件不属于项目 MIT 代码。

To produce only the portable ZIP and its checksum, use `-PortableOnly`; Inno Setup is not required for this mode:

仅生成免安装 ZIP 及其校验值时，使用 `-PortableOnly`，此模式不需要 Inno Setup：

```powershell
pwsh -NoProfile -File tools/package-native.ps1 -PortableOnly -OutputDirectory "./dist/portable-only"
```

Optional `-BuildPath` selects another complete Windows build; `-OutputDirectory` selects a new output directory. The script **refuses to overwrite any existing output directory**, including an empty directory or output retained after a failed build. Choose a different output path when rerunning. Output must be outside the input build. `-Version` must match `bundleVersion` in `ProjectSettings.asset`.

可用 `-BuildPath` 指定另一份完整的 Windows 构建，用 `-OutputDirectory` 指定新的输出目录。脚本**拒绝覆盖任何已存在的输出目录**，包括空目录或上次失败后保留的产物；重跑时请选择新的输出路径。输出目录必须位于输入构建之外，`-Version` 必须与 `ProjectSettings.asset` 中的 `bundleVersion` 一致。
