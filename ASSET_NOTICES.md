# Asset notices · 素材许可与来源

## License scope / 许可范围

The root [MIT License](LICENSE) applies to Cindersea's original code, tools, and project documentation, except where a file states another license. It does **not** relicense illustrations, character and world material, videos, full-length music, fonts, third-party assets, or Unity components.

根目录 [MIT License](LICENSE) 适用于 Cindersea 原创代码、工具和项目文档，文件另有许可时以其说明为准。它**不将插画、角色与世界设定、视频、完整配乐、字体、第三方素材及 Unity 组件重新授权为 MIT**。

The maintainer has confirmed the right to publicly distribute the supplied worldbuilding, character materials, artwork, and seven Suno tracks with this project. This statement concerns their inclusion in Cindersea; it does not establish a separate open-content license or verify the music's generation-time subscription plan. Project art, story, video, and music are excluded from the code license. For reuse beyond rights already granted by an applicable license, contact the [maintainer](https://github.com/chenzhiyong1994).

维护者已确认有权随本项目公开分发所提供的世界观、人物、美术和七首 Suno 配乐。该声明说明这些内容可随 Cindersea 公开提供，不代表额外授予独立开放内容许可，也不表示已核实音乐生成时的订阅方案。项目美术、故事、视频和完整配乐不包含在代码许可中。超出素材既有许可范围的复用，请联系[维护者](https://github.com/chenzhiyong1994)。

Nothing in this notice restricts permissions already granted by CC0, the SIL Open Font License, or another applicable third-party license. Preserve the relevant license and copyright notices when redistributing those materials.

本声明不限制 CC0、SIL Open Font License 或其他适用第三方许可已授予的权利。再分发相应素材时，请保留有关许可和版权说明。

## Project-created media / 项目制作内容

| Content / 内容 | Source and scope / 来源与范围 |
| --- | --- |
| Character art, environments, UI images, sprites / 角色、场景、界面图像与像素角色 | AI-assisted images created for the project from its supplied character/world references. Existing formal character resources remain the identity reference. / 按提供的世界与角色资料，为本项目制作的 AI 辅助图像，正式人设仍为身份依据。 |
| Portrait and menu videos / 立绘与菜单视频 | Generated with Dreamina/Seedance from project images, then processed locally for playback. Some transparent portraits use a fixed source-image outline. / 使用项目原画经即梦／Seedance 生成，再于本地处理接入；部分透明立绘采用源图固定轮廓。 |
| Seven full-length music tracks / 七首完整配乐 | Supplied Suno-generated tracks: 江湖启程、晴岚行路、回水交锋、强敌临阵、天阙决战、同袍凯旋、余烬再行. / 由维护者提供，随游戏分场景播放。 |
| Game meshes, shaders, effect timing, and procedural sound code / 游戏网格、Shader、特效时序与程序音效代码 | Project implementation; code license applies to original code, while incorporated textures and samples retain their own licenses. / 原创实现适用代码许可，其中使用的贴图与采样保留独立许可。 |

The generated music and video providers do not endorse Cindersea. Provider terms and underlying rights are separate from the project's code license. See [Suno's terms](https://suno.com/terms) for its service conditions; this repository does not certify an individual track's copyright status or grant rights owned by another party.

音乐与视频生成服务不代表对 Cindersea 的认可。服务条款和底层内容权利与项目代码许可相互独立；Suno 服务条件见其[官方条款](https://suno.com/terms)。本仓库不认证单首曲目的著作权状态，也不代替其他权利人授予权利。

## Third-party media / 第三方素材

All paths below are relative to `native/Dicebound/Assets/Resources/`.

下表路径均相对于 `native/Dicebound/Assets/Resources/`。

| Source / 来源 | Included material / 使用内容 | License and local notice / 许可与本地说明 |
| --- | --- | --- |
| [VSCO 2 Community Edition](https://github.com/sgossner/VSCO-2-CE), Sam Gossner / Simon Dalzell; sample editing by Elan Hickler / Soundemote | 13 instrument samples and a derived healing sound / 13 个乐器采样与派生恢复音效 | CC0 1.0; `Audio/LICENSE-VSCO.txt`, `Audio/source-manifest.json` |
| [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) and [Impact Sounds](https://kenney.nl/assets/impact-sounds) | Derived combat sound effects / 派生战斗短音效 | CC0 1.0; `Audio/Combat/LICENSE-Kenney-RPG.txt`, `LICENSE-Kenney-Impact.txt` |
| rubberduck, [80 CC0 RPG SFX](https://opengameart.org/node/86018) and [water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) | Derived combat and water effects / 派生战斗与水声 | CC0 1.0; `Audio/Combat/LICENSE-rubberduck.txt`, `LICENSE-CC0-1.0.txt` |
| Pierre / [Effekseer](https://effekseer.github.io/en/contribute.html), Pierre's Effects vol.2 | Six particle textures / 六张粒子贴图 | CC0 1.0; `Effects/NOTICE.txt`. No Effekseer runtime is included. / 不含 Effekseer 运行时。 |
| [Poly Haven](https://polyhaven.com/license) | Environment models, surface textures, and an HDRI / 场景模型、表面贴图与 HDRI | CC0 1.0; assets under `Environment/` and applicable `Tactics/Environment/` derivatives / 相应目录及其派生素材 |

Poly Haven credits: **James Ray Cock** (`wooden_lantern_01`), **Kless Gyzen** (`rock_moss_set_01`), **Tina** (`large_castle_door`), **Ulan Cabanilla** (`wooden_handle_saber`), **Rob Tuytel** (`large_grey_tiles`, `castle_wall_slates`), **Amal Kumar** (`rusty_metal_04`), **colormass / Rico Cilliers** (`rough_linen`), and **Sergej Majboroda** (`studio_small_09`). Attribution does not imply endorsement.

上述作者名称用于说明来源，不代表其对本项目的认可。CC0 素材的原始许可不会因项目进行裁切、混音、材质转换或场景组装而改变。

Per-file environment sources and hashes: [Poly Haven environment assets](native/vendor-environment-assets.json) and [HDRI and derived surface data](native/vendor-tactical-polish-assets.json). Historical Quaternius character models and their animation libraries are not included in this source snapshot.

场景素材逐文件来源与哈希见 [Poly Haven 场景清单](native/vendor-environment-assets.json)和 [HDRI 与派生表面数据](native/vendor-tactical-polish-assets.json)。历史 Quaternius 人体模型及其动作库未包含在本次源码快照中。

## Fonts / 字体

| Font / 字体 | Author or source / 作者或来源 | Preserved license / 保留许可 |
| --- | --- | --- |
| Ma Shan Zheng | [The Ma Shan Zheng Project Authors](https://github.com/googlefonts/mashanzheng) | `Fonts/MaShanZheng-OFL.txt` |
| LXGW WenKai Regular 1.522 / 霞鹜文楷 | [LXGW](https://github.com/lxgw/LxgwWenKai), based on the Klee Project | `Fonts/LXGWWenKai-OFL.txt` |
| Noto Sans CJK SC / Noto Serif CJK SC | [Noto CJK](https://github.com/notofonts/noto-cjk) | `Fonts/LICENSE-OFL.txt` |

These fonts retain the **SIL Open Font License 1.1**, including applicable reserved-name and redistribution requirements. They are embedded as game resources, not installed on the player's system.

这些字体保留 **SIL Open Font License 1.1**，包括适用的保留名称与再分发条款。游戏仅将其作为资源使用，不安装到玩家系统。

## Engine and packages / 引擎与依赖

Unity, URP, and packages resolved through `native/Dicebound/Packages/` retain their respective licenses. The repository contains dependency manifests, not the Unity Editor. Obtain Unity separately under its own terms. A distributed Windows Player also contains Unity/runtime components; the project MIT license does not relicense them.

Unity、URP 及通过 `native/Dicebound/Packages/` 解析的依赖保留各自许可。仓库包含依赖清单，不包含 Unity Editor；请根据 Unity 自身条款单独获取。Windows Player 包含的 Unity／运行库组件也不适用项目 MIT 再许可。

## Distribution notes / 发行说明

Source redistributions should retain this document and the included asset notices. Binary distributions should include these notices and all font licenses in an accompanying `Credits/` directory. Only content actually included in a distribution is covered by its material inventory; historical sources are not a promise that a resource is active in gameplay.

源码再分发应保留本说明及相关素材许可；二进制发行包应在随附的 `Credits/` 目录中保留这些声明和全部字体许可。素材清单说明实际包含的内容，历史素材存在不等于它仍在当前玩法中使用。
