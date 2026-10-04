# Contributing · 参与贡献

Thanks for helping improve Cindersea. The supported product is the Unity tactical roguelike in `native/Dicebound`; start with the [build guide](manual/build.md).

欢迎改进 Cindersea · 烬海天阙。当前维护对象是 `native/Dicebound` 中的 Unity 战棋 roguelike，请先阅读[构建说明](manual/build.md)。

## Issues / 问题反馈

For bugs, include the beta/version, Windows version, steps to reproduce, expected and actual behavior, and a useful screenshot or log excerpt. Use a fresh test save when possible. Remove private paths, tokens, and personal data from attachments. Suggest larger gameplay or architecture changes in an issue before opening a large pull request.

反馈缺陷时请说明 Beta／版本、Windows 版本、复现步骤、预期与实际结果，并附必要截图或日志片段。尽量使用独立测试存档；提交附件前移除私人路径、令牌和个人资料。较大的玩法或架构变化请先通过 issue 讨论范围。

## Pull requests / 提交修改

1. Keep the change focused and explain the player-visible result.
2. Preserve `.meta` files and GUIDs when modifying Unity assets. Do not commit `Library/`, build output, local saves, caches, credentials, or generated test evidence.
3. Keep `Core/Tactical*.cs` independent of Unity rendering. Actions must settle and save before presentation; interrupted animations must not repeat an action or lose committed progress.
4. Run the narrow checks relevant to your change. Record exactly what was checked and what remains unverified; a rule check is not a substitute for a Player visual check.
5. Include only assets you may distribute, with source and license details. Do not assume an image, track, or asset found online is reusable.

1. 保持修改集中，说明它给玩家带来的实际变化。
2. 修改 Unity 素材时保留 `.meta` 和 GUID；不提交 `Library/`、构建产物、本地存档、缓存、凭证或测试证据。
3. `Core/Tactical*.cs` 与 Unity 渲染解耦。行动先结算并保存，再播放演出；中断动画不能重复行动或丢失已提交进度。
4. 执行与改动直接相关的检查，如实说明已验证与未验证范围；规则检查不等于 Player 视觉检查。
5. 素材必须具备分发权，并记录来源和许可；网上可见不等于可以复用。

Original code contributions are accepted under the [MIT License](LICENSE). Asset contributions must state their applicable license separately; see [ASSET_NOTICES.md](ASSET_NOTICES.md). Contributions should be respectful, specific, and reproducible.

原创代码贡献采用 [MIT License](LICENSE)。素材贡献需另行注明适用许可，参见 [ASSET_NOTICES.md](ASSET_NOTICES.md)。讨论请尊重他人，围绕具体问题与可复现证据展开。
