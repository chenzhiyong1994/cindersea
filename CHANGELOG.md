# Changelog · 更新记录

## Beta 1 — 2026-10-03

First public beta, based on gameplay version **0.22.4**. The **`v0.22.4-beta.1`** distribution includes the Unity source project, a Windows x64 installer, a portable ZIP, and SHA-256 checksums. See [downloads](README.md#play) or [build from source](README.md#build).

首个公开 Beta，基于玩法版本 **0.22.4**。**`v0.22.4-beta.1`** 发行内容包括 Unity 源码、Windows x64 安装包、免安装 ZIP 和 SHA-256 校验值。可[直接下载](README.zh-CN.md#play)或[从源码构建](README.zh-CN.md#build)。

### Included / 本版内容

- Five selectable companions and a three-character party; character-specific skills, skill levels, and ultimates.
- Twelve-floor branching runs with battles, elite gates, shops, campfires, events, relics, and a final boss encounter.
- Per-character turn completion, inspectable enemy intent, terrain rules, current statuses, and inventory access.
- Party-owned skill rewards, shop rerolls, immediate reward feedback, and four difficulty settings.
- Painted battle environments, pixel characters, animated portrait close-ups, full-frame portrait viewing, and ultimate cut-ins with speed lines and elemental effects.
- Local saves, import/export, volume controls, and reduced motion.
- Open-source code, bilingual project documentation, and a static project website.
- Windows installer and portable package, with license notices and file checksums. The executables are not code-signed.

- 五位可选同行者、三人小队，以及各自的技能、等级与奥义。
- 十二层分支旅程，包含战斗、精英幕门、商店、营火、事件、旧物和顶层决战。
- 逐角色结束行动，可查看敌人动向、地形规则、当前状态与行囊。
- 技能奖励自动归属对应队员、商店重随、即时奖励反馈及四档难度。
- 手绘战场、像素人物、动态特写立绘、全画幅欣赏，以及带速度线和元素特效的奥义演出。
- 本地存档、导入导出、音量调节与减少动态效果。
- 开放源码、中英双语项目文档与静态项目主页。
- Windows 安装包与免安装包，附许可声明及文件校验值；可执行程序暂未进行代码签名。

### Validation and limitations / 验证与限制

- The independently exported public project passed a fresh Unity Windows build with no C# or shader compilation errors. Final portrait and video changes have not completed a fresh Player visual review or full regression pass.
- The installer and portable archive passed SHA-256 checks for all 215 manifest entries. Per-user installation, the Start menu shortcut, isolated startup, and uninstall were verified; an independently created user file remained intact after uninstall.
- Balance and UI polish are still evolving. There is no performance or compatibility certification across hardware configurations.
- The game is in Simplified Chinese. Windows x64 is the supported target; no macOS, Linux, mobile, or English gameplay release is claimed.
- Some animated portraits use a fixed transparency outline. Music consists of full tracks and is not guaranteed to loop seamlessly at musical phrase boundaries.
- Project media and third-party dependencies have separate license scopes; the entire game is not covered by MIT. See [asset notices](ASSET_NOTICES.md).

- 独立导出的公开项目已通过全新 Unity Windows 构建，没有 C# 或 Shader 编译错误；最终立绘与视频改动尚未完成新一轮 Player 视觉验收或完整回归。
- 安装包与免安装包均完成 215 项清单文件的 SHA-256 核对；按用户安装、开始菜单、隔离启动及卸载检查通过，卸载保留独立创建的用户文件。
- 平衡与界面仍在完善，未对不同硬件做全面性能或兼容认证。
- 游戏为简体中文，目前支持 Windows x64；未提供 macOS、Linux、移动端或英文游戏版本。
- 部分动态立绘采用固定透明轮廓，完整配乐尚不保证按乐句无缝循环。
- 项目媒体和第三方依赖分别适用各自许可，不应将整款游戏统称为 MIT，详见[素材说明](ASSET_NOTICES.md)。
