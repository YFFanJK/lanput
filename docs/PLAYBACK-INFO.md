# LANPUT 0.6.4 播放信息

在底部播放控制栏点击“播放信息”，或按 Ctrl+I 打开。支持窗口及全屏；Esc、关闭按钮或点击面板外部可收起。文本可选择后按 Ctrl+C 复制。

打开面板后每秒读取一次当前媒体信息，关闭面板时不读取额外轨道和统计信息。切换影片时从当前 Media 对象重新读取，不沿用上一部影片的数据。

展示播放状态、媒体来源类型、渲染设置、当前所选视频轨道的分辨率/编码/标称帧率/码率，以及所选音轨的编码/采样率/声道数/码率。显示引擎报告的缓冲进度与当前媒体累计解码帧、显示帧和丢帧数。

- 帧率是媒体轨道标称值，不是测量出的实时屏幕刷新率。
- 码率来自轨道元数据，不是网络下载速度。部分 MP4、HLS 等输入不提供轨道码率，这时显示“暂无”。
- 缓冲百分比仅在引擎报告缓冲期间显示，不估算已缓冲秒数。
- 正常默认模式不宣称一定使用硬件解码；界面展示的是配置模式。
- 未测得的帧统计显示“暂无”，已测得的零丢帧显示 0；停止后清除实时帧统计显示。
- 不展示原始媒体链接、签名参数或本地文件路径。

API 依据：[MediaTrack](https://docs.videolan.me/libvlcsharp/api/LibVLCSharp.Shared.MediaTrack.html)、[VideoTrack](https://docs.videolan.me/libvlcsharp/api/LibVLCSharp.Shared.VideoTrack.html)、[MediaStats](https://docs.videolan.me/libvlcsharp/api/LibVLCSharp.Shared.MediaStats.html)。

验证：43 项核心测试通过。通过局域网控制接口向实际应用投放本机测试视频，核对了 640×360 / 25 fps / AAC 48 kHz 双声道、显示帧统计、暂停、停止、切换至 320×240 / 10 fps 和全屏面板。未验证所有网络片源均能提供全部元数据。
