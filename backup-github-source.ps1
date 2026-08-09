param(
    [string]$RepoDir = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [string]$TargetBranch = "main",
    [string]$CommitMessage = "自动备份",
    [string]$ProxyUrl = "",
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:RepoDir = [System.IO.Path]::GetFullPath($RepoDir)
$script:TargetBranch = $TargetBranch
$script:CommitMessage = $CommitMessage
$script:ProxyUrl = $ProxyUrl

function Write-BackupLog {
    param(
        [string]$Action,
        [string]$Detail
    )
    # 统一输出关键动作，方便一键脚本在没有界面时也能看懂进度。
    $timestamp = Get-Date -Format "yyyy/MM/dd HH:mm:ss"
    Write-Host "[$timestamp][SoftTalk-IME备份][$Action] $Detail"
}

function Invoke-Git {
    param(
        [string[]]$Arguments
    )
    # Git 命令统一检查退出码，避免失败被误报为备份成功。
    Write-BackupLog "Git" ("git " + ($Arguments -join " "))
    & git -C $script:RepoDir @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git 命令失败：git $($Arguments -join ' ')，退出码=$LASTEXITCODE"
    }
}

function Get-GitOutput {
    param(
        [string[]]$Arguments
    )
    # 读取 Git 输出时同样检查退出码，避免错误输出进入后续判断。
    $output = & git -C $script:RepoDir @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git 命令失败：git $($Arguments -join ' ')，退出码=$LASTEXITCODE"
    }
    return @($output)
}

function Convert-ToGitPath {
    param(
        [string]$PathText
    )
    # 排除规则统一使用正斜杠，避免 Windows 路径分隔符影响判断。
    return (($PathText -replace '\\', '/') -replace '^\./', '').Trim()
}

function Test-AllowedPath {
    param(
        [string]$GitPath
    )
    # 示例配置是可公开提交的文档，不应被敏感配置规则误报。
    return $GitPath -match '(^|/)\.env\.example$'
}

function Get-ForbiddenPathMatches {
    param(
        [string[]]$Paths
    )
    # 只拦截用户数据、密钥、缓存和构建产物，避免备份脚本把本机现场上传。
    $patterns = @(
        '(^|/)(__pycache__|\.pytest_cache|\.ruff_cache|\.mypy_cache)(/|$)',
        '(^|/)(venv|\.venv|env|ENV|build|dist|out|release_package|release_packages|logs?|runtime|\.runtime|cache|tmp|temp|downloads)(/|$)',
        '(^|/)(\.env|[^/]+\.env|[^/]+\.local|[^/]+\.secret|[^/]+\.secrets)$',
        '\.(db|sqlite|sqlite3|stdb|backup|bak|key|pem|pfx|log|pyc|pyd|exe|dll|zip|7z|rar|msi)$'
    )
    $matches = New-Object System.Collections.Generic.List[string]
    foreach ($rawPath in $Paths) {
        $gitPath = Convert-ToGitPath $rawPath
        if ($gitPath.Length -eq 0 -or (Test-AllowedPath $gitPath)) {
            continue
        }
        foreach ($pattern in $patterns) {
            if ($gitPath -match $pattern) {
                $matches.Add($gitPath)
                break
            }
        }
    }
    return @($matches | Sort-Object -Unique)
}

function Initialize-Proxy {
    # 代理只注入当前脚本进程，不修改系统代理设置。
    if ($script:ProxyUrl.Trim().Length -eq 0) {
        return
    }
    $uri = [Uri]$script:ProxyUrl
    $env:HTTP_PROXY = $script:ProxyUrl
    $env:HTTPS_PROXY = $script:ProxyUrl
    $env:ALL_PROXY = $script:ProxyUrl
    $env:http_proxy = $script:ProxyUrl
    $env:https_proxy = $script:ProxyUrl
    $env:all_proxy = $script:ProxyUrl
    Write-BackupLog "代理" $uri.AbsoluteUri
}

function Assert-RepoReady {
    # 启动前确认目标目录确实是当前项目的 Git 工作树。
    if (-not (Test-Path -LiteralPath $script:RepoDir -PathType Container)) {
        throw "项目目录不存在：$script:RepoDir"
    }
    $isWorkTree = (Get-GitOutput @("rev-parse", "--is-inside-work-tree") | Select-Object -First 1).Trim()
    if ($isWorkTree -ne "true") {
        throw "目标目录不是 Git 工作树：$script:RepoDir"
    }
    $root = (Get-GitOutput @("rev-parse", "--show-toplevel") | Select-Object -First 1).Trim()
    if ([System.IO.Path]::GetFullPath($root) -ne $script:RepoDir.TrimEnd('\')) {
        throw "Git 根目录与目标目录不一致：$root"
    }
}

function Assert-RemoteReady {
    # 没有 origin 时停止，避免脚本误把源码推到未知远端。
    $remote = (Get-GitOutput @("remote", "get-url", "origin") | Select-Object -First 1).Trim()
    if ($remote.Length -eq 0) {
        throw "尚未配置 origin，请先执行 git remote add origin <GitHub仓库地址>"
    }
    Write-BackupLog "远端" $remote
}

function Assert-BranchReady {
    # 只允许在指定备份分支上运行，避免临时分支被当作正式备份。
    $branch = (Get-GitOutput @("branch", "--show-current") | Select-Object -First 1).Trim()
    if ($branch -ne $script:TargetBranch) {
        throw "当前分支是 '$branch'，目标分支必须是 '$script:TargetBranch'"
    }
}

function Assert-NoPreStagedChanges {
    # 人工暂存内容不与自动备份混合，避免一次提交包含用户未确认的范围。
    $staged = @(Get-GitOutput @("diff", "--cached", "--name-only"))
    if ($staged.Count -gt 0) {
        throw "暂存区已有内容，请先手动处理：$($staged -join ' / ')"
    }
}

function Assert-NoForbiddenFiles {
    # 同时检查已跟踪文件和将被纳入提交的工作区候选文件。
    $tracked = @(Get-GitOutput @("ls-files"))
    $candidates = @(Get-GitOutput @("ls-files", "--others", "--modified", "--exclude-standard"))
    $badTracked = @(Get-ForbiddenPathMatches $tracked)
    $badCandidates = @(Get-ForbiddenPathMatches $candidates)
    $badFiles = @($badTracked + $badCandidates | Sort-Object -Unique)
    if ($badFiles.Count -gt 0) {
        throw "发现禁止进入仓库的文件：$($badFiles -join ' / ')"
    }
}

function Invoke-Backup {
    # 备份主流程固定为：检查 -> 暂存 -> 提交 -> 推送。
    Write-BackupLog "开始" $script:RepoDir
    Assert-RepoReady
    Assert-RemoteReady
    Assert-BranchReady
    Assert-NoPreStagedChanges
    Assert-NoForbiddenFiles

    if ($DryRun) {
        Write-BackupLog "完成" "演练通过，未执行 add、commit、push"
        return
    }

    Invoke-Git @("add", "--all")
    $hasNoChanges = $false
    & git -C $script:RepoDir diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        $hasNoChanges = $true
        Write-BackupLog "变更" "没有需要提交的内容"
    }
    elseif ($LASTEXITCODE -eq 1) {
        Invoke-Git @("commit", "-m", $script:CommitMessage)
    }
    else {
        throw "暂存区差异检查失败，退出码=$LASTEXITCODE"
    }

    Invoke-Git @("push", "origin", $script:TargetBranch, "--progress")
    if ($hasNoChanges) {
        Write-BackupLog "完成" "没有新提交，已确认远端推送状态"
    }
    else {
        Write-BackupLog "完成" "源码备份已提交并推送"
    }
}

try {
    Initialize-Proxy
    Invoke-Backup
    Write-Host "[成功] SoftTalk-IME 备份完成" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "[失败] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
