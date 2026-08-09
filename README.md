# SoftTalk-IME

话术精灵的 Windows 输入法项目。

## 当前边界

- 知识库客户端和服务器是业务数据的唯一写入方。
- IME 只检查版本、下载增量、检索话术并把结果插入当前应用。
- IME 可以写自己的本地缓存和搜索索引，但不回写知识库。
- 知识库同步采用“检查版本，有变化才增量同步”，不复制正在使用的主数据库。
- 本仓库只保存源码、脚本和文档，不保存账号配置、用户数据库、备份包或运行产物。

## 目录约定

当前只保留项目基础设施，输入法运行代码将在技术方案确定后加入：

- `backup-github-source.ps1`：检查、提交并推送源码的备份脚本。
- `备份SoftTalk-IME.bat`：Windows 一键备份入口。
- `.gitignore`：排除本地数据、密钥、缓存和构建产物。

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
