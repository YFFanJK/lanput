# 参与 LANPUT

欢迎报告兼容性问题、改进播放器或提交文档修正。提交前请先查看已有 Issue，说明具体触发过程和期望行为。

## 开发环境

使用 Windows、.NET 10 SDK；构建安装包另外需要 Inno Setup。完整命令见 [README 构建说明](README.md#从源码构建)。

```powershell
dotnet run --project tests/LANPUT.Tests/LANPUT.Tests.csproj -c Release
dotnet build src/LANPUT.Windows/LANPUT.Windows.csproj -c Release
```

测试工程是控制台回归测试程序，应使用 `dotnet run` 执行，而不是只运行 `dotnet test`。

## Pull Request

- 说明解决的问题、实际行为变化和验证方式。
- 播放器或动效变化，请检查暂停、停止、快速换源、全屏、最小窗口、关闭动画及退出。
- 文档与截图应与当前代码一致；演示原型不能代替真机互通验证。
- 不提交 `bin/`、`obj/`、`dist/`、运行日志、个人设置或浏览器缓存。
- 发布 ZIP 和安装程序放入 GitHub Releases，不放进源码提交。
- 不更换现有图标、配色、兼容请求标识或协议行为，除非修改本身需要且已说明影响。

提交的贡献按本项目 MIT 协议提供；如含第三方代码或资源，请明确来源与许可，并保留对应声明。

## 兼容性报告

请提供应用版本、Windows 版本、手机系统、投屏应用及版本、媒体类型、复现步骤。提交前遮盖设备名称、局域网地址、完整媒体 URL、签名参数、账户信息及本地路径。应用的诊断导出仍需人工检查后再公开。
