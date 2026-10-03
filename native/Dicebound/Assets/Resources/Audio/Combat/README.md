# 战斗短音效

本目录包含 23 个公共素材派生的短音效，覆盖起手、轻重命中、护盾、破盾、倒下、足音和角色元素。运行时直接加载 WAV，无需网络或生成服务。

| 来源 | 许可文件 |
| --- | --- |
| [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) | `LICENSE-Kenney-RPG.txt` |
| [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) | `LICENSE-Kenney-Impact.txt` |
| rubberduck [80 CC0 RPG SFX](https://opengameart.org/node/86018) 与 [water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) | `LICENSE-rubberduck.txt` |
| VSCO 2 Community Edition 竖琴采样 | 上级 `LICENSE-VSCO.txt` 与 `source-manifest.json` |

这些来源均采用 CC0 1.0，完整正文见 `LICENSE-CC0-1.0.txt`。`manifest.json` 记录作者、官方来源、整包与源文件／产物 SHA-256、处理参数、时长和音量测量；rubberduck 的许可证据说明为项目编写，不冒充原包文件。

产物为 44.1 kHz、单声道、16 位 PCM。制作过程使用 FFmpeg 8.1.1 与 Python 标准库，处理包括裁切、滤波、固定增益及短淡入淡出；没有调用生成模型。原包与中间缓存不属于运行依赖。

`Soundscape.PlayTactical` 按已保存行动的接触时机播放音效，失败不改变规则结算。火焰、潮水、玉钟、阴影和石屑用于区分角色，部分恢复声音采用真实竖琴采样。声音来源及解码检查不能替代游戏内的时序和听感验收。统一鸣谢及许可边界见根目录 `ASSET_NOTICES.md`。
