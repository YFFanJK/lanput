# 发布版本

源码、二进制与更新清单分开维护。

1. 修改 `src/LANPUT.Windows/LANPUT.Windows.csproj` 的版本与文件版本，检查界面中的版本文本及更新记录。
2. 执行测试、Windows 构建及 `scripts/package.ps1`。
3. 从本次 `dist/` 取得安装包、便携包和 `SHA256SUMS.txt`，验证安装、启动、升级和卸载所需场景。
4. 在 GitHub Releases 创建对应版本，如 `v0.9.4`，将二进制作为 Release 附件上传。不要提交进源码分支。

在发布二进制前，核对 [第三方依赖说明](../THIRD-PARTY-NOTICES.md)、实际依赖包和插件的许可、声明及所需源码获取方式。源码 MIT 不会将第三方组件改成 MIT。

## 在线检查更新

客户端读取的 JSON 至少使用以下字段，不能直接使用 GitHub API 的 Release 对象代替：

```json
{
  "version": "0.9.4",
  "downloadUrl": "https://github.com/YFFanJK/lanput/releases/download/v0.9.4/LANPUT-0.9.4-Setup-x64.exe",
  "notes": "影院与音乐厅自动幕间换场"
}
```

这只是结构示例，只有实际上传安装包后下载地址才有效。先验证真实安装包的下载与 SHA256，再发布更新清单。

`installer/update-defaults.json` 是新安装包的默认更新配置。官方源码保留维护者的公开 HTTPS 更新地址；Fork 后发行自己的软件时，应修改为自己的服务地址，或留空并关闭默认检查。

如自行托管，在生成安装包时可使用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1 -FeedUrl 'https://updates.example.com/lanput/latest.json' -DownloadUrl 'https://downloads.example.com/LANPUT-0.9.4-Setup-x64.exe'
```

提供 `DownloadUrl` 才会生成可发布的 `latest.json`。缺少时生成 `latest.example.json`，它是模板，不能直接上线。

源码上传、GitHub Release 与服务器更新清单是三个独立步骤。请分别确认实际发布状态，不要将示例下载地址当成已上线附件。
