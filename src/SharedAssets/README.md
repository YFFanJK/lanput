# 共用界面素材

## 全屏图标

- `icons/expand-screen.svg`、`icons/collapse-screen.svg`：用户于 2026-11-23 提供，原文件名为 `放大屏幕.svg` 和 `缩小屏幕.svg`。
- Windows 的 `Themes/Cinema.xaml` 保留两份 SVG 的路径数据，以 WPF Geometry 渲染，大小沿用 18px，填充绑定按钮前景色，不沿用 SVG 的固定黑色。

## cinema-hall.jpg

- 用户于 2026-11-19 提供并明确授权接入 LANPUT 的 Gemini 生成影院图片。
- 原始文件名：`Gemini_Generated_Image_k9ysytk9ysytk9ys.jpg`。
- 尺寸：2752 × 1536；保留原图，不裁剪、不拉伸，不在图片中烧录文案。
- SHA256：`60a9c542958d95cff69f5afd5d5a9b4b5af7744247e75b17c3625fc071348d37`。
- 银幕内部像素区域：左上 `(669, 262)`，宽 `1416`，高 `694`。文字和欢迎画面单独叠加。
- 两个客户端嵌入同一个本地资源；Windows 的布局和开映推进通过 Core 的 `CinemaArtworkLayout` 统一计算。
- 替换图片时需重新确认尺寸与银幕区域，并检查小窗口、宽屏、最大化、开映和停止回到待机的布局。

## 黑胶音乐厅

- `music-room-background.jpg`（2752 × 1536）、`vinyl-record.png` 和 `tonearm.png`（2048 × 2048）为用户提供的 Gemini 原图，两张 PNG 保留透明通道。
- Windows 音乐厅以原图像素为坐标合成三个图层。唱片围绕背景转轴旋转，唱臂固定，歌词与按键独立排版。
