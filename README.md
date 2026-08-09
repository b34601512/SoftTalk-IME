# SoftTalk-IME

话术精灵的 Windows 输入法项目。

## 当前边界

- 知识库客户端和服务器是业务数据的唯一写入方。
- IME 只检查版本、下载增量、检索话术并把结果插入当前应用。
- IME 可以写自己的本地缓存和搜索索引，但不回写知识库。
- 知识库同步采用“检查版本，有变化才增量同步”，不复制正在使用的主数据库。
- 本仓库只保存源码、脚本和文档，不保存账号配置、用户数据库、备份包或运行产物。

## 目录约定

项目按“业务核心”和“Windows 接入层”分开：

- `src/SoftTalkIme.Core`：只读同步、本地快照、候选检索；不包含任何知识库写入接口。
- `KnowledgeSyncWorker`：每分钟检查版本，有变化才拉取并原子保存本地快照；同步失败保留旧快照。
- `src/SoftTalkIme.Cli`：CLI 自测和快照检索入口，供自动化测试使用。
- `src/SoftTalkIme.Tsf`：Windows TSF COM Host；只负责接收键盘事件和向当前文本框插入结果。
- `scripts/register-tsf.ps1`：注册 TSF（会修改系统注册表，测试阶段不要直接运行）。
- `scripts/unregister-tsf.ps1`：卸载 TSF。

- `backup-github-source.ps1`：检查、提交并推送源码的备份脚本。
- `备份SoftTalk-IME.bat`：Windows 一键备份入口。
- `.gitignore`：排除本地数据、密钥、缓存和构建产物。

## 当前可运行的 MVP

1. `dotnet build SoftTalk-IME.sln --configuration Release`
2. `dotnet run --project src/SoftTalkIme.Cli/SoftTalkIme.Cli.csproj --configuration Release -- self-test`

TSF 层当前采用最小可验证交互：切换到 SoftTalk-IME 后按 `Ctrl+Shift+Space` 进入话术模式，输入英文检索词，按 `Enter`/`Space` 输出第一条结果，按 `F1-F9` 选择候选，按 `Esc` 取消。候选窗口和同步后台调度会在下一阶段补齐；业务核心已经可以独立测试。

发布 TSF COM Host：

```powershell
dotnet publish src/SoftTalkIme.Tsf/SoftTalkIme.Tsf.csproj --configuration Release --self-contained false
scripts\validate-tsf-build.ps1
```

注册脚本默认使用管理员权限所需的 HKLM；开发机可传 `-CurrentUser` 做当前用户注册。注册动作会修改系统状态，CLI 自测不会执行注册。

TSF 激活后只有在进程环境中同时存在以下两个变量时，才会启动每分钟只读同步；缺少任意一个变量时只使用本地快照：

```powershell
$env:SOFTTALK_IME_SYNC_BASE_URL = "https://你的同步服务地址"
$env:SOFTTALK_IME_SYNC_TOKEN = "只读同步令牌"
```

## 一键备份

首次使用前，先让本地仓库连接到 GitHub 的 `origin`：

```powershell
git remote add origin https://github.com/b34601512/SoftTalk-IME.git
```

之后双击 `备份SoftTalk-IME.bat`，或在 PowerShell 中运行：

```powershell
.\backup-github-source.ps1
```

脚本会依次检查仓库、分支、暂存区和敏感/运行产物，再执行 `add -> commit -> push`。发现异常时会停止，不会强制覆盖远端。

如果网络需要代理，可在 PowerShell 中传入：

```powershell
.\backup-github-source.ps1 -ProxyUrl "http://127.0.0.1:7890"
```

## 开发原则

1. IME 与知识库客户端解耦，优先使用公开、稳定的只读数据协议。
2. 候选话术优先走本地索引，联网生成只作为后备能力。
3. AI 通过经过权限、校验、日志和事务保护的业务接口操作，不直接修改数据库表。
4. 任何用户数据都不能进入 GitHub 源码仓库。
