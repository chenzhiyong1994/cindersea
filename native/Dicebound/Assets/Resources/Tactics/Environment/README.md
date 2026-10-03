# 曜京战棋环境

场景采用哑光石木、厚岸墙、暖光冷影及流动水渠。`TacticalEnvironment` 按战棋状态构建场景，`TacticalSceneryMesh` 生成建筑、石块、木板、植被和水面网格。街巷、管廊、回水桥、旧库与昼轮前庭保留不同结构。

## 地形与交互

新战场为 12×10 格。墙体、缺口、高台、箱罐等遵循 Core 规则；掩体、水雾、阴影、热管、碎石等可通行地形以贴地特征表现。装饰不会额外创建规则阻挡。射线格位带 `TacticalTileHit`，物件带 `TacticalObjectMarker`；寻路、视线和伤害不由美术推断。

## 资源与许可

- 石材、木材底图为项目生成素材；原始 PNG 保留，重复采样与 mipmap 由 Unity 导入设置决定。它们不自动采用第三方素材的 CC0 许可。
- 黄铜、衣幡、灯笼及部分环境表面复用 Poly Haven CC0 素材。作者、源文件地址与 SHA-256 见仓库 `native/vendor-environment-assets.json`。
- `studio_small_09_1k.hdr` 来自 Poly Haven，作者 Sergej Majboroda，采用 CC0。HDRI 与派生表面数据的来源和处理公式见 `native/vendor-tactical-polish-assets.json`。
- `Dicebound/TacticalWater` 为项目原创 URP Shader，使用世界坐标流纹、岸边泡沫与光照。减少动态效果时停止水面流动。

统一许可边界与鸣谢见仓库根目录 `ASSET_NOTICES.md`。运行时读取本地资源，不访问素材网站。

## 开发

正交相机为 HUD 和角色预留显示区域。滚轮缩放，中键平移，Home 恢复视角。静态可读网格按共享材质合批，动态物件依据已提交状态更新。

使用 Unity 6000.3.24f1 与 URP 17.3.0。构建及验证入口见根目录 `manual/build.md`；素材哈希或规则检查不能替代实际 Player 渲染验收。
