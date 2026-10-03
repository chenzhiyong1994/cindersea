# 战棋档案兼容与实机回归夹具

这些文件不含用户身份或本机路径，用于验证扩充内容池后，旧地图、货架、价格与售罄状态仍可恢复。测试不得用现行生成器重建它们。

- `tactical-rev1-published-shop.json`：使用已经通过实机检查的 P0 Player 内 `Dicebound.Core.dll` 与 `Dicebound.Persistence.dll`，调用 `NewAscent(71, [sixuan, lingfeng, cangling], 1)`，沿合法节点与规则建议到达第一家商店后保存。
- `tactical-rev2-published-shop.json`：相同方法，`ascentRevision=2`。
- `tactical-rev2-published-victory.json`：直接保留 `.qa/tactical-player-1600-900-20261003-011155648/tactical-journey.json` 的原始内容。该次报告完成两支队伍、24 个节点和 417 次行动。

生成两份商店夹具时所用 DLL 与该次 Player 报告中的构建身份完全一致：

| 文件 | SHA-256 |
| --- | --- |
| Dicebound.Core.dll | E3B83182165B5B655ACE288112CBFFD4CD13044D3901387AB42EBA9DE1D9ED5D |
| Dicebound.Persistence.dll | A342126670A3098E8F820EFEBE9F831660EE276803F5891405172A5FEF931948 |
| tactical-rev1-published-shop.json | CAD5622A78227B13D92E1452EF355E11EF4B95C48AA94C5AEC595562F09A9E7C |
| tactical-rev2-published-shop.json | B2607CA7B0CBA279DEBA24A7AD681CBA32BE0D1CB0394E468B634EA3AF777F53 |
| tactical-rev2-published-victory.json | 9DBDCCE41749AA88E7F422167896501BF5238A83C4B20843DF391A740AFB7291 |

`ProgressionChecks.PublishedContentCompatibility` 先读取这些档案，再验证无损往返、旧商店重随和篡改价格拒绝。首次加入时，未修复版本在第一份商店夹具上拒绝读档；修复通过内容版本固定旧池，保留严格校验。

## 实机结束行动存档回归

`tactical-rev3-player-endturn-plated.json` 直接保留 `.qa/tactical-player-1600-900-20261003-151435984/tactical-journey.json` 的原始内容，不含用户身份或本机路径；SHA-256 为 `80250423727D2C88ED6FA7AE2C85B18BFF9D5755D2339C2255386E777C46FC3B`。

该档为第 9 层精英战、revision 128，晏烛影是最后一位未结束者。提交结束行动后，剩余 4 生命的镀甲巨卫因守势反击死亡，原回合初始化仍为其补回 6 护盾，导致合法结束行动无法保存。`PlayerEndTurnRetaliation` 保留原档复现，检查死亡敌不再回盾、存活镀甲敌仍回 6 盾、提交一次及保存往返；校验器继续拒绝死亡带盾。

定点入口：`dotnet run --project native/Tests/Dicebound.ProgressionTests.csproj -- --player-end-turn`。默认 progression 检查同样包含此回归。

## 商店展示文案兼容

`tactical-rev3-historical-shop-text.json` 保留数值文案修正前的「定谳一击」货架说明与已售出状态。它从上述实机结束行动档继续合法战斗与路线到达 `floor-9-0` 商店，夹具金币设为 1000 后重随一次并购买该技能；用于固定货架兼容，不作为真实玩家经济记录。SHA-256 为 `23CE371414D1CBA50C430EE38E40D018DB45FAAE0CBDDDA60E43514025EAE28A`。

revision 3 的货架先校验固定种子生成的 ID、类别、来源、技能或旧物 ID、价格和数量，`sold` 必须为布尔值；仅接受有界历史 `name/text`，成功解码后这两个展示字段统一取当前内容。历史路线、价格、售罄、金币与技能等级均不重建。revision 0–2 的原档兼容规则保持不变。

定点入口：`dotnet run --project native/Tests/Dicebound.ProgressionTests.csproj -- --shop-text`。它检查实际旧文案恢复、仅展示字段更新，以及价格、身份和字段类型篡改拒绝。
