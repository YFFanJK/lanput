# 正式更新地址与首次发布 0.8.1

> 这是一份历史版本记录，文中的服务器状态与交付路径仅代表当时环境。当前发布流程见 [RELEASING.md](RELEASING.md)。


检查更新：https://fd.fansweb.top/lanput/updates/latest.json

本次真实安装包：`dist/LANPUT-0.8.1-Setup-x64.exe`。本次安装包已内置上述地址，默认启动后检查；此机器的个人更新配置同步设为上述地址。旧版用户如果已经保存了空地址或自定义地址，仍需在设置中填写并保存正式地址。

当前服务器尚未发布清单，HTTPS 返回 404。这说明首次发布还未完成，不表示已有有效更新。服务器先上传真实安装包，再由发布脚本生成、切换清单；不要上传旧版 0.6.4，也不要用占位文件。

目标下载地址：

https://fd.fansweb.top/lanput/releases/0.8.1/LANPUT-0.8.1-Setup-x64.exe

## 交给服务器 AI 的发布任务

请发布 LANPUT **0.8.1**，不是先前报告中的 0.6.4。拿到真实 `LANPUT-0.8.1-Setup-x64.exe` 后，与同批 `SHA256SUMS.txt` 比较 SHA256；不能沿用旧安装包或旧校验值。

假设上传至 `/root/`，执行：

```bash
lanput-publish.sh 0.8.1 /root/LANPUT-0.8.1-Setup-x64.exe "新增音乐投屏、氛围音乐厅、同步歌词及本地 LRC；歌词搜索结果自动应用，支持手动更换版本。"
```

发布完成后，从外网核验：

1. 检查地址返回 HTTP 200、JSON Content-Type 与禁止陈旧缓存的响应头。
2. `version` 为 `0.8.1`，`downloadUrl` 指向上面的真实安装包，`notes` 是本次说明。
3. 完整下载并核对 SHA256 与本批安装包一致，全程 HTTPS。
4. 确认历史清单备份成功，提供发布结果，不用测试文件充当安装包。

客户端使用语义版本比较：当前 0.8.1 读取到 0.8.1 不提示升级；0.6.4/0.8.0 等旧版读到 0.8.1 才提示。将来 0.8.2 的真实安装包发布后，0.8.1 才会提示更新。客户端当前是打开浏览器下载安装包，由用户运行升级；不是静默下载或自动安装。

## 本地重新打包

`scripts/package.ps1` 默认读取 `installer/update-defaults.json`，不会在未传参数时清空正式地址。可显式传入 `-FeedUrl` 使用其他地址；`-FeedUrl ''` 可打包无默认地址版本。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -DownloadUrl "https://fd.fansweb.top/lanput/releases/0.8.1/LANPUT-0.8.1-Setup-x64.exe" -Notes "新增音乐投屏、氛围音乐厅、同步歌词及本地 LRC；歌词搜索结果自动应用，支持手动更换版本。"
```

生成本地 `dist/latest.json` 只是准备发布文件，不会自动修改服务器。`SHA256SUMS.txt` 必须与本次重新打包的文件一起使用。
