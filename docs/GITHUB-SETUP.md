# 首次发布到 GitHub

本次准备的仓库名为 `lanput`，所有者按创建页面为 `YFFanJK`，应用版本 0.9.4。

## 创建页面

| 项目 | 填写 |
| --- | --- |
| Repository name | `lanput` |
| Description | `Windows 局域网投屏接收器，提供影院式视频播放、黑胶音乐厅、同步歌词与自动场景切换。` |
| Visibility | Public |
| Add README | Off |
| Add .gitignore | No .gitignore |
| Add license | No license |

README、忽略规则和 MIT `LICENSE` 已经在本地准备好，以上选项避免 GitHub 重复生成文件。“No license”仅表示创建页面不代生成，上传的 `LICENSE` 仍是正式 MIT 协议。

## 使用干净源码目录

不要将原始开发目录整体拖入网页。`scripts/prepare-github.ps1` 将按白名单导出到 `dist/github/lanput-ready/`，并在旁边生成源码 ZIP 和文件清单。导出不含运行日志、个人设置、浏览器测试目录、已编译 DLL、安装包或宣传片工程。

源码 ZIP 用于保存或转交；上传仓库时使用解压后的文件和文件夹，不能只把 ZIP 当作源码提交。

### 方法一：Git（推荐，适合后续维护）

先在网页创建上面的空仓库，然后在干净源码目录执行：

```powershell
git init -b main
git add .
git status --short
git commit -m "Initial open-source release: LANPUT 0.9.4"
git remote add origin https://github.com/YFFanJK/lanput.git
git push -u origin main
```

首次提交需要配置自己的 Git 姓名和邮箱；可在 GitHub 设置中使用提供的 noreply 邮箱保护个人邮箱。推送时由 Git 凭据管理器完成账户验证，不要把令牌写进脚本或仓库。

首次提交请使用自己的 Git 身份；公开项目建议使用 GitHub 设置中提供的 noreply 邮箱。不要通过强制推送覆盖已有远程历史。

### 方法二：网页上传

空仓库选择“uploading an existing file”；已有文件的仓库使用 Add file → Upload files。打开干净源码目录，拖入里面的文件与文件夹，保持目录层级。

GitHub 网页一次最多上传 100 个文件，单文件上限 25 MiB。可以分两批：先上传根目录文件、`src/` 和 `tests/`，再上传其余文件夹。不要直接拖入外层 `lanput` 文件夹导致全部内容多套一层；仓库根目录应直接看到 `README.md`、`LICENSE`、`src/`。

官方说明：https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository

## 源码与安装包分开

源码仓库保存代码与文档。安装包、便携包和校验文件放到 Releases：

- Tag：`v0.9.4`
- Title：`LANPUT 0.9.4`
- 附件：`LANPUT-0.9.4-Setup-x64.exe`、`LANPUT-0.9.4-win-x64.zip`、同批 `SHA256SUMS.txt`

没有真实上传附件之前，README 中的 Releases 入口可能没有可下载版本，不能将更新清单的示例地址当作已上线下载地址。

## 发布后检查

- 首页图片、构建说明与 MIT 标识可正常显示。
- Actions 的 CI 能运行回归和原生编译；本地通过不等于远程已经通过。
- 在 Settings 中按需启用私密漏洞报告。
- 如使用在线更新服务，按 `RELEASING.md` 先核对真实附件再发布清单。
