# 构建 LANPUT

## 环境

- Windows x64。原生界面使用 WPF，项目目标为 `net10.0-windows`。
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。首次还原需访问 NuGet。
- 仅生成安装包时需要 [Inno Setup](https://jrsoftware.org/isinfo.php)。脚本支持自动查找 Inno Setup 6/7，或通过 `-Compiler` 指定 ISCC.exe。

普通使用者应优先下载 Releases 中的安装包或便携包，无需为自包含发行版安装 SDK。

## 测试与编译

在仓库根目录执行：

```powershell
dotnet run --project tests/LANPUT.Tests/LANPUT.Tests.csproj --configuration Release
dotnet build src/LANPUT.Windows/LANPUT.Windows.csproj --configuration Release
```

测试工程是控制台测试运行器，退出码非零表示失败。编译 GUI 不等于验证所有手机播放器和媒体来源。

## 发布便携版

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

脚本先运行回归，再发布含 .NET 运行时的 Windows x64 目录，最后压缩便携包。产物位于 `dist/`；必须保留 EXE 同目录的 DLL、运行时和 libvlc 插件。`-SkipArchive` 可略过压缩，`-Run` 可在成功后启动程序。

开发后重建并启动：

```powershell
.\build-and-run.cmd --rebuild
```

已有编译产物时，双击 `run.cmd` 直接启动，不重复构建。

## 安装包

安装 Inno Setup 后执行：

```powershell
.\package.cmd
```

或指定编译器：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Compiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

应用版本从 `src/LANPUT.Windows/LANPUT.Windows.csproj` 读取，不需要手改脚本中的输出文件名。当前版本为 0.9.4。

Windows PowerShell 5.1 下，包含中文的 PowerShell 脚本使用 UTF-8 BOM。项目 XML 按字节加载，不依赖系统默认代码页。

## GitHub Actions

推送到 main 或提交 Pull Request 时运行回归与 Windows 编译。手动运行“Build Windows receiver”可生成便携构建产物。工作流不自动创建公开 Release，也不更新外部服务器。

本地原生 UI 验证工具和大量测试媒体未打包到公共源码中；公开仓库的可复现自动检查为 `tests/LANPUT.Tests` 与编译。原生验证边界见 [验证记录](../VALIDATION.md)。
