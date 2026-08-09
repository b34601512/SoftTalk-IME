# SoftTalk-IME

话术精灵的 Windows 输入法职责说明，运行时复用官方 Weasel/Rime。

## 推荐路线：Rime

不再自研普通中文输入、候选窗口、Windows TSF 兼容层或额外部署包装器。官方 Weasel 负责输入法运行时、候选窗口和部署；SoftTalk 客户端是账号级话术词典的唯一生成方，并直接调用官方部署程序。

客户端生成独立 `softtalk.schema.yaml` 和账号哈希词典，使用问题全拼和首字母简拼作为编码，选择候选后插入完整答案；不会占用或覆盖用户的 `custom_phrase.txt`。

## 迁移边界

- 知识库客户端和服务器是业务数据的唯一写入方。
- SoftTalk 客户端只读查询本地知识库并原子生成账号隔离的 Rime 词典。
- Weasel/Rime 不读取 SoftTalk 登录态、数据库或 access token，也不访问 SoftTalk 服务端。
- 本地使用排序由账号级 Rime userdb 保存，不上传、不回写知识库。
- 本仓库只保存职责文档和源码备份工具，不保存账号配置、用户数据库、备份包或运行产物。

## 仓库边界

- SoftTalk 客户端负责查询、过滤、排序、原子生成 Rime 话术文件并触发官方 Weasel 部署。
- 本仓库不提供编译产物、安装程序、探测 CLI 或部署 CLI；没有生产调用方的包装层已经退役。
- 旧 TSF、同步、快照、索引及 .NET CLI/Core 源码已移至 `D:\备份文件夹\SoftTalk-IME`，仅供审计，不作为兼容层继续维护。
- 如果未来需要改变职责边界，应先建立新的架构决策并明确废止当前方案，不能直接恢复旧实现。

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

1. IME 不接触知识库、登录态、令牌或网络；账号数据边界只存在于 SoftTalk 客户端。
2. 普通中文和本地学习排序复用 Rime，不在本仓库复制实现。
3. 任何用户数据都不能进入 GitHub 源码仓库。
