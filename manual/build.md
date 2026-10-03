# Build and development · 构建与开发

[English README](../README.md#build) · [中文 README](../README.zh-CN.md#build)

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

The independently exported Beta 1 project has passed a fresh Windows build. A full regression pass and final in-game visual review have not been performed for this publication. This is a source release; build the Player using the steps below.

独立导出的 Beta 1 工程已通过全新 Windows 构建；本次未执行完整回归或最终实机视觉验收。本次为源码发布，可按下文生成 Player。

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

A Windows archive needs the executable, its data directory, and all Unity runtime files produced beside it. Include [ASSET_NOTICES.md](../ASSET_NOTICES.md), the code [LICENSE](../LICENSE), and a `Credits/` folder with the third-party notices and all font licenses. See the asset notice for the license boundary; the Unity runtime is not MIT-licensed project code.

Windows 压缩包需要保留可执行文件、数据目录及随构建生成的全部 Unity 运行文件，并附上 [ASSET_NOTICES.md](../ASSET_NOTICES.md)、代码 [LICENSE](../LICENSE)，以及包含第三方声明和全部字体许可的 `Credits/`。Unity 运行组件不属于项目 MIT 代码。
