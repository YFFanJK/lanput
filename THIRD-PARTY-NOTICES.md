# 第三方依赖

本项目通过 NuGet 使用以下依赖，不是华为官方产品，也没有声明获得 DLNA 或华为认证。

| 依赖 | 固定版本 | 许可与上游 |
|---|---|---|
| LibVLCSharp.WPF / LibVLCSharp | 3.10.2 | NuGet 声明 LGPL-2.1-or-later；https://code.videolan.org/videolan/LibVLCSharp |
| VideoLAN.LibVLC.Windows | 3.0.24 | NuGet 声明 LGPL-2.1-or-later；https://code.videolan.org/videolan/libvlc-nuget |
| Avalonia.Desktop / Avalonia.Themes.Fluent（跨平台客户端） | 11.3.23 | MIT；https://github.com/AvaloniaUI/Avalonia |
| LibVLCSharp.Avalonia（跨平台客户端） | 3.10.2 | LGPL-2.1-or-later；https://code.videolan.org/videolan/LibVLCSharp |
| .NET / ASP.NET Core / WPF | .NET 10，补丁版本由构建机 SDK 决定 | .NET 开源项目及其各自第三方声明；https://github.com/dotnet |

LANPUT 自身源码采用根目录 MIT 协议，此授权不会覆盖或替换上表组件与插件的许可。`licenses/LGPL-2.1.txt` 保留 LGPL 2.1 正文与来源说明；`-or-later` 的含义以实际组件声明为准。

LibVLCSharp 和 LibVLC 使用动态链接方式加载。VideoLAN 依赖及其插件包含各自的许可证和版权要求；修改、分发成品前，请核对实际 NuGet 包和插件的完整许可、保留其声明，并按对应条款提供所需源码或获取方式。本源代码包没有预置 VideoLAN 二进制或其源码；首次构建从上游 NuGet 获取。

参考：https://www.videolan.org/legal.html

跨平台客户端还通过 Avalonia 使用 Skia / HarfBuzz 等传递依赖，遵循各自许可与第三方声明。Linux 版从发行版软件包加载 LibVLC 及插件；macOS 打包从构建机的 VLC.app 复制同架构引擎，实际 VLC 版本以构建机安装版本为准，相关二进制不受 LANPUT 的 MIT 许可替代。macOS 包附 VLC 来源说明；构建与分发时应保留实际插件许可，并提供对应版本源码获取方式。

音乐歌词搜索使用 LRCLIB 公共 API（https://lrclib.net/docs）。歌词内容的权利属于对应权利人，LRCLIB 不是本项目的关联方。播放与本地 LRC 导入不依赖该服务。
