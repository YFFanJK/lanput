<p align="center"><img src="src/LANPUT.Windows/Assets/logo.png" width="190" alt="LANPUT"></p>

# LANPUT

手机选好影片，投屏，剩下的交给 LANPUT。

LANPUT 是一个面向 Windows 的局域网媒体接收器，使用 C#、WPF 与 LibVLC 构建。待机时是一间私人影院，播放影片时自动开映；接收音乐时，幕布换场至黑胶音乐厅，展示歌曲信息与同步歌词。

当前版本：**0.9.4** · **Windows x64** · **.NET 10** · **MIT**

[下载发行版](https://github.com/YFFanJK/lanput/releases) · [从源码构建](#从源码构建) · [更新记录](CHANGELOG.md) · [参与贡献](CONTRIBUTING.md)

## 功能

- **局域网投屏接收**：SSDP 发现、UPnP AV 控制与真实媒体播放，支持本地文件播放。
- **私人影院**：观众席待机、视频输出后自动开映、结束后退场；支持窗口、最大化与全屏。
- **黑胶音乐厅**：识别纯音频，旋转唱片与中心标签，显示歌名、歌手和专辑。
- **歌词**：LRCLIB 在线匹配并自动应用可用版本，也支持本地 LRC、手动选版和时间校准。
- **幕间换场**：影院与音乐厅自动合幕、换场、开幕；切歌和暂停不重复播放，可随时跳过。
- **日常使用**：启动自动接收、设备与网卡记忆、托盘、快捷键、音量和进度控制。
- **信息与更新**：播放信息、脱敏诊断导出、可配置 HTTPS 在线更新检查。

## 界面

影院待机，原生界面：

![影院待机](.github/assets/cinema.png)

黑胶音乐厅，使用本地测试音频与示例歌词：

![黑胶音乐厅](.github/assets/music.png)

## 开始使用

1. 在 [Releases](https://github.com/YFFanJK/lanput/releases) 下载 Windows x64 安装包或便携包。若尚未发布附件，可按下方说明自行构建。
2. 手机与电脑连接同一个可信局域网，启动 LANPUT，并开启接收。
3. 首次有多个网卡时，在设置里选择手机所在网络；按需要允许 Windows 专用网络通信。
4. 在手机播放器的投屏 / TV 列表中选择电脑。音乐使用相同入口，识别后自动进入音乐厅。

便携版需要完整解压，保留 EXE 同目录的 DLL、运行时与插件；不要只复制 `LANPUT.exe`。安装版默认安装到当前用户目录，运行不需要管理员权限。

## 手机搜索不到电脑设备？

先确认手机与电脑连接同一个局域网，LANPUT 已开启接收，并选择了手机所在网络的网卡，然后依次检查：

1. **当前网络是否设置为“专用网络”**：在 Windows“设置 → 网络和 Internet → Wi-Fi（或以太网）→ 当前网络属性”中查看。仅在可信的家庭或办公网络下设置为“专用网络”。LANPUT 的专用网络放行规则不会在“公用网络”下生效。
2. **Windows 防火墙是否开启**：打开“Windows 安全中心 → 防火墙和网络保护”，确认“专用网络”的 Microsoft Defender 防火墙已开启。保持防火墙开启，通过允许 LANPUT 通信来配置投屏。
3. **是否允许 LANPUT 通过防火墙**：在“防火墙和网络保护 → 允许应用通过防火墙 → 更改设置”中检查 LANPUT 的“专用”选项；若未列出，可通过“允许其他应用”选择实际运行的 `LANPUT.exe`。如果已通过脚本配置规则，可在“高级设置 → 入站规则”中确认当前程序路径的 LANPUT 允许规则已启用，覆盖 TCP 41000 和 UDP 1900，并适用于专用网络。移动程序或改用其他安装路径后，需要为新路径重新放行。

修改后，在 LANPUT 中停止接收再重新开启，并在手机端重新搜索。若仍找不到，请检查访客 Wi-Fi、路由器 AP 隔离或 VPN，并导出软件诊断供进一步排查。

**没有出现防火墙弹窗，不代表已经放行。** Windows 的通知设置可能关闭了这类提示，应以当前网络类别和有效规则为准。

## 兼容性范围

主要验证环境为 Windows 11 x64 与华为手机视频 / 音乐投屏场景。不同手机、播放器、网络和媒体来源的协议、授权与定位能力存在差异；不保证所有来源可播放。

LANPUT 接收播放器投送的媒体，**不提供通用手机屏幕镜像**。本项目不是华为官方产品，也未声明获得华为或 DLNA 认证。

## 从源码构建

安装 .NET 10 SDK，在仓库根目录运行：

```powershell
dotnet run --project tests/LANPUT.Tests/LANPUT.Tests.csproj -c Release
dotnet build src/LANPUT.Windows/LANPUT.Windows.csproj -c Release
```

生成自包含便携版：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

生成安装包还需要 Inno Setup，然后运行 `package.cmd`。已构建后，双击 `run.cmd` 即可启动；修改源码后使用 `build-and-run.cmd --rebuild`。

测试是控制台回归程序，请使用 `dotnet run`，不能以 `dotnet test` 代替。首次构建需要联网还原 NuGet 包；生成的自包含发行版无需另外安装 .NET。

## 项目结构

```text
src/LANPUT.Core/       发现、协议、网络与诊断逻辑
src/LANPUT.Windows/    WPF 界面、原生播放适配与交互
src/.../Assets/        应用与安装器品牌资源
tests/LANPUT.Tests/    核心协议、更新及歌词回归
scripts/              构建、打包和开发工具
installer/            Inno Setup 与默认更新配置
.github/              CI、协作模板与 README 截图
```

## 设置、隐私与排错

配置和日志位于 `%LOCALAPPDATA%\LANPUT`。在线歌词查询发送歌曲标题与歌手，更新检查请求已配置的清单地址。更新配置位于 `installer/update-defaults.json`，已有用户设置优先；本项目未接入遥测或统计 SDK。

连接失败时先检查同一局域网、网卡、专用网络防火墙、访客网络或 AP 隔离。提交 Issue 前，请人工检查并遮盖日志中的个人信息、设备名称和媒体签名地址。

- [安全问题报告](SECURITY.md)

自动检查覆盖 52 项协议、更新和歌词回归及 Windows 编译；不代表所有手机、片源与网络组合均已验证。

## 发布与在线更新

安装包、便携包和同批 `SHA256SUMS.txt` 放到 GitHub Releases，不提交进源码分支。`package.cmd` 需要 Inno Setup，版本从 Windows 项目文件读取。

客户端更新清单使用 `version`、`downloadUrl`、`notes` 字段，不能直接使用 GitHub API 的 Release 对象代替。先确认真实安装包下载和校验值，再发布清单；`latest.example.json` 只是模板。默认更新地址由 `installer/update-defaults.json` 配置。

## 许可

LANPUT 自身源码采用 [MIT License](LICENSE)，版权署名为 YFFanJK。第三方依赖保留各自许可；LibVLCSharp 与 LibVLC 的 NuGet 包声明 LGPL-2.1-or-later，插件还可能包含其他声明，见 [第三方依赖说明](THIRD-PARTY-NOTICES.md)。

Fork 后发行自己的版本时，请配置自己的更新来源，避免将用户引导至本项目的维护者更新服务。
