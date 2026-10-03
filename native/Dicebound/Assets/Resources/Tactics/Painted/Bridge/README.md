# 回水桥原画场景

本目录为固定朝向、可平移缩放的 2.5D 回水桥场景。原画承载静态环境，人物、敌人、箱罐、技能与范围层继续按真实战棋规则运行。不匹配的地图使用程序场景。

| 文件 | 用途 |
| --- | --- |
| `beauty.png` | 无角色、界面、箱罐及技能的场景原画；sRGB、Clamp、Trilinear、mipmap |
| `depth.png` | 2560×1440 线性眼深度，RGB 按 `dot(rgb,[1,1/255,1/65025])` 解码，alpha 表示有效几何 |
| `foreground-mask.png` | 线性前景分类，划分原画遮挡，不增加规则阻挡 |
| `projection.json` | 相机 basis、画幅、格位、实际高度与阻挡签名 |

深度与前景图使用 Point、Clamp、无压缩、无 mipmap 的线性可读纹理。原画采样遵循投影记录；非 16:9 源图通过 `beautyUvRect` 明确选择区域，不自动拉伸。场景原画不得改变真实缺口、连接、高台及可通行格位。

运行层从同一深度图重建世界位置，Forward、DepthOnly 和 DepthNormalsOnly 写入一致深度。静态网格隐藏后，其碰撞与拾取仍保留，人物和动态物件据此受到正确遮挡。该场景原画已有绘制光照，因此避免再次叠加完整后处理；减少动态效果时停止轻微水纹。

相关 Shader 为 `Dicebound/PaintedGuideDepth`、`Dicebound/PaintedEnvironment` 和 `Dicebound/PaintedEnvironmentWater`。`TacticalEnvironment.ExportGuide` 可导出当前真实几何的制作参考；开发检查应使用独立存档，见根目录 `manual/build.md`。

几何和投影数据只能验证坐标关系，原画、角色脚底和静态前景的视觉对齐仍须通过 Player 检查。本页不将历史检查结果作为本 Beta 最终验收。项目原画许可见根目录 `ASSET_NOTICES.md`。
