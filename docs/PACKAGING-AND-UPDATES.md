# LANPUT 0.6.1：Windows 打包与更新

调研日期：2026-11-16。以下为常见技术路线比较，不代表使用率排名。

| 路线 | 交付物与特点 | LANPUT 的选择 |
| --- | --- | --- |
| [Inno Setup](https://jrsoftware.org/inno-setup) | EXE 安装向导、快捷方式、卸载和覆盖安装 | 本次采用；适合直接分发带原生 VLC 依赖的 WPF 桌面软件 |
| [NSIS](https://nsis.sourceforge.io/Features) | EXE 安装器，可编写自定义安装逻辑 | 可替代，暂不需要额外安装脚本体系 |
| [WiX](https://github.com/wixtoolset) / Windows Installer | MSI、EXE bootstrapper、企业部署 | 有企业 MSI 分发要求时再增加 |
| [MSIX / App Installer](https://learn.microsoft.com/windows/msix/overview) | Windows 包身份、安装卸载、系统更新机制 | 需考虑签名与发布渠道，本次不引入 |
| [Velopack](https://docs.velopack.io/packaging/operating-systems/windows) | 集成打包和应用更新的方案 | 未来需要应用内下载和应用更新时可考虑迁移 |
| 自包含目录 + ZIP | 解压运行 LANPUT.exe，无需安装 .NET | 与安装包一起提供，保留整个目录 |

## 当前交付

- `dist/LANPUT-0.6.1-Setup-x64.exe`：单个安装包，安装所有运行依赖，创建开始菜单/可选桌面快捷方式，提供卸载。
- `dist/LANPUT-0.6.1-win-x64.zip`：便携版，解压后双击 `LANPUT.exe`。
- `dist/LANPUT-win-x64/LANPUT.exe`：已编译程序入口。需要同目录的 DLL 和 libvlc/plugins，不能单独拷走这个 EXE。
- `dist/SHA256SUMS.txt`：本次产物 SHA-256 摘要。
- `dist/latest.example.json`：更新清单模板，必须替换示例地址后才能上线。

安装包默认装到 `%LOCALAPPDATA%\Programs\LANPUT`，不要求管理员权限；固定 AppId 支持后续覆盖升级。安装/卸载时应用应退出。设置与日志仍在 `%LOCALAPPDATA%\LANPUT`，卸载保留用户设置，不自动修改防火墙。

此版本未使用代码签名证书。对外分发前可为程序与安装包配置 Authenticode 签名；SHA-256 文件用于核对文件，不能替代发布者签名。

## 在线检查更新

接收设置 → 软件更新：填写 HTTPS JSON 地址，保存后点击“检查更新”。可选择启动后后台检查。地址为空时不发起请求，也不显示“已是最新版”。离线、超时和格式错误不会中断接收。

更新清单格式（示例域名必须替换）：

```json
{
  "version": "0.6.2",
  "downloadUrl": "https://your-domain.example/releases/",
  "notes": "修复说明"
}
```

`version` 使用稳定数字版本；仅比当前版本大时提醒。`downloadUrl` 是下载页或安装包的 HTTPS 地址。发现更新后，用户确认才打开浏览器，下载后退出 LANPUT 并运行安装包覆盖升级。当前实现是在线检测与提示，不自动下载、不静默替换运行文件。

客户端限制清单大小、请求时长与重定向次数，禁止重定向降级至 HTTP，不发送投屏链接或账号信息。清单本身应放在可信 HTTPS 主机上。

用户配置保存在 `%LOCALAPPDATA%\LANPUT\updates.json`，优先于安装目录的 `update-defaults.json`；升级安装不覆盖用户选择。

## 构建与后续发布

Windows 构建机需要 .NET 10 SDK 和 Inno Setup（ISCC.exe）。日常启动仍用 `run.cmd`，制作安装包双击 `package.cmd`。命令行也可：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

拥有服务器或 GitHub Releases 后：

1. 提升 `LANPUT.Windows.csproj` 的版本号、FileVersion 与 app.manifest 的版本号。
2. 发布地址确定后构建：

```powershell
.\scripts\package.ps1 -FeedUrl 'https://your-domain.example/latest.json' -DownloadUrl 'https://your-domain.example/releases/'
```

3. 上传安装包、ZIP、SHA256SUMS.txt 和生成的 `latest.json`，最后更新线上清单，避免先提示尚未上传的安装包。
4. 老版本已配置相同 FeedUrl 的用户下次检查时会收到提示。已安装用户可在设置中填写地址；新分发安装包可自带默认地址。

可将清单托管于 GitHub Releases 的固定附件地址（`releases/latest/download/latest.json`）、静态站点或自己的 HTTPS 服务。不能把 GitHub API 的 latest release JSON 直接当成本项目清单，两者字段不同。

当前没有真实发布渠道，因此本次没有创建仓库、上传文件或声明线上更新已开通。自动构建工作流仅生成可下载产物。
