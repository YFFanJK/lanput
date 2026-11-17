# 发布 LANPUT 0.9.0

> 这是一份历史版本记录，文中的服务器状态与交付路径仅代表当时环境。当前发布流程见 [RELEASING.md](RELEASING.md)。


使用真实的 `dist/LANPUT-0.9.0-Setup-x64.exe` 与本批 `dist/SHA256SUMS.txt`。

检查地址：https://fd.fansweb.top/lanput/updates/latest.json

目标下载地址：https://fd.fansweb.top/lanput/releases/0.9.0/LANPUT-0.9.0-Setup-x64.exe

上传后交给服务器 AI 执行（假设安装包位于 `/root/`）：

```bash
lanput-publish.sh 0.9.0 /root/LANPUT-0.9.0-Setup-x64.exe "原生黑胶星环音乐厅：唱片与中心标签旋转、固定唱臂、分层歌词、融合播放栏、进度时间预览及歌词定位。"
```

外网复验清单 HTTP 200、version=0.9.0、正确的 HTTPS 安装包地址、完整下载与 SHA256，一并保留历史清单。不要复用 0.8.1 的安装包、下载路径或校验值。
