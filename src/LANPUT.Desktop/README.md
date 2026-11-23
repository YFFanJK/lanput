# LANPUT Linux / macOS 预览版

与 Windows 0.9.5 共用 `LANPUT.Core`，使用 Avalonia 11.3.23 和 LibVLCSharp 3.10.2。这是独立跨平台客户端，尚未达到 Windows 版的完整功能与实机验证范围。Windows 的安装包和启动脚本继续使用原来的前端。

## 已接入

- 启动自动接收、网卡选择、设备名与身份持久化。
- SSDP / UPnP AV 发现、HTTP 媒体控制、视频与音乐播放。
- 影院观众席待机、开映欢迎语、结束回到观众席；视频只在换场完成后显示。
- 纯音频识别、唱片及标签旋转、暂停时停转、歌词搜索后自动应用、LRC 导入与时间校准。
- 播放 / 暂停 / 停止、进度、音量、静音、全屏、播放信息与诊断。
- HTTPS 更新检查；每个系统和架构使用独立清单，拒绝不匹配的安装包。
- 可关闭动画、键盘 Space / F11 / Esc、原生系统标题栏。

## 系统与依赖

首轮目标：Ubuntu 24.04 x64（X11 或 XWayland）和 macOS 14+（Apple Silicon / Intel 分开打包）。不能把 WSL 测试视为所有 Linux 桌面或实际手机投屏的兼容保证。暂不承诺原生 Wayland 视频嵌入、HDR、多声道直通、DRM 片源或 AirPlay。

打包脚本生成的成品包含 .NET 运行时，用户不需要安装 .NET。源码运行需要 .NET 10 SDK。

### Ubuntu / Debian

安装 `.deb` 时使用 `apt`，它会安装声明的播放和界面依赖：

```bash
sudo apt install ./LANPUT-0.9.5-preview-linux-x64.deb
```

便携 `.tar.gz` 仍依赖系统的 LibVLC 及图形库，请先安装：

```bash
sudo apt update
sudo apt install libvlc5 vlc-plugin-base vlc-plugin-video-output libx11-6 libice6 libsm6 libfontconfig1 libxcb1 libxkbcommon0 libgl1 fonts-noto-cjk
tar -xzf LANPUT-0.9.5-preview-linux-x64.tar.gz
./LANPUT/run.sh
```

### macOS

打包脚本把匹配架构的 VLC 引擎复制进 `.app`，生成 `.dmg`；将 LANPUT 拖到 Applications。需要允许系统的“本地网络”访问和防火墙入站。首次手机投屏还需真实设备验证。

从源码启动前，安装对应架构的 VLC 到 `/Applications/VLC.app`。也可通过 `LANPUT_VLC_PATH` 指向原生 LibVLC 库目录。

本地 / 默认 CI 包使用临时签名，**没有 Apple 公证**。面向普通用户正式分发前，必须配置 Developer ID 签名、公证并在干净的 Mac 上验收；不要将当前预览包描述为已公证正式版。

## 源码运行与测试

在项目根目录运行：

```bash
dotnet run --project src/LANPUT.Desktop/LANPUT.Desktop.csproj -c Release
dotnet run --project tests/LANPUT.Desktop.Tests/LANPUT.Desktop.Tests.csproj -c Release
dotnet run --project tests/LANPUT.Desktop.Tests/LANPUT.Desktop.Tests.csproj -c Release -- --native
```

最后一条测试需要原生 LibVLC，会使用临时生成的音频和 dummy 输出验证播放器，并在回环网络验证真实 HTTP / SOAP 服务；不需要播放声音。

Windows 上仅调试新前端时，可设置 `LANPUT_VLC_PATH` 到现有发行包中的 `libvlc/win-x64`。这不替代原有 Windows 正式版。

## 打包

必须在对应系统及对应架构上执行，Python 3 无额外依赖：

```bash
# Ubuntu x64，生成 tar.gz 和 deb
python3 scripts/package-desktop.py --rid linux-x64
# Apple Silicon Mac（先安装 VLC）
python3 scripts/package-desktop.py --rid osx-arm64
# Intel Mac（先安装 Intel VLC）
python3 scripts/package-desktop.py --rid osx-x64
```

输出在 `dist/desktop-preview/<rid>/`，附 `SHA256SUMS.txt`。中间文件使用系统临时目录，结束后清理。重新打包会替换同名产物；需要保留旧包时请提供新的 `--output` 路径。macOS 构建支持 `LANPUT_SIGN_IDENTITY` 和 `LANPUT_NOTARY_PROFILE` 环境变量；证书和 notarytool keychain profile 需事先配置在构建机上，不写入仓库。

GitHub Actions 的 `Build Linux and macOS previews` 提供三个原生 runner 的构建、回归和打包；仅上传构建产物，不自动发布 Release。本地新增工作流需要提交至 GitHub 后才能运行。

## 在线更新

默认更新地址留空，避免现有 Windows 更新服务向其他平台推送 EXE。在设置中分别填写本平台清单，结构与旧版本一致：

```json
{
  "version": "0.9.5",
  "downloadUrl": "https://your-domain.example/lanput/LANPUT-0.9.5-preview-osx-arm64.dmg",
  "notes": "macOS Apple Silicon 预览版"
}
```

文件名必须包含准确的 `-<rid>.`，例如 `-osx-arm64.dmg`、`-linux-x64.deb`。清单和下载链接都需 HTTPS。当前版本只检查并打开下载页面，不会自动安装。

## 网络与本地数据

手机与电脑必须在同一个可互通的局域网；允许 UDP 1900 / 239.255.255.250 设备发现，以及 TCP 41000 入站。不要关闭整个防火墙。VPN、多网卡、访客 Wi-Fi 和 AP 隔离可能导致发现失败。

设置与轮转日志保存在系统的 LocalApplicationData 下的 `LANPUT.Desktop` 目录。软件的“诊断”面板显示实际路径。日志不保存完整媒体 URL、签名参数或本地媒体路径。

## 下一轮实机验收

两种 Mac 架构和 Ubuntu 桌面分别验证：手机发现、影片、音乐、连续换曲、暂停 / 拖动、结束退场、全屏、缩放、文件选择、网络权限、重复启动及退出后端口释放。仍需补齐 Windows 版的托盘、防休眠、完整歌词选版及更精细的影院 / 音乐厅合幕动画。

当前播放器适配代码由 Windows 0.9.5 版本移植到独立目录，以免改变正式版；修复协议行为时先放进 Core，后续再提取共享播放器服务，避免两个实现长期分叉。
