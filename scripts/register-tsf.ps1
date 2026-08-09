[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\bin\Release\net8.0-windows\publish"),
    [string]$TsfCliPath = ""
)

$ErrorActionPreference = "Stop"
$resolvedPublishDir = Resolve-Path -LiteralPath $PublishDir -ErrorAction Stop
$dllPath = Join-Path $resolvedPublishDir "SoftTalkIme.Tsf.comhost.dll"
if ([string]::IsNullOrWhiteSpace($TsfCliPath)) {
    $bundledTsfCliPath = Join-Path $resolvedPublishDir "SoftTalkIme.Tsf.Cli.exe"
    $TsfCliPath = if (Test-Path -LiteralPath $bundledTsfCliPath -PathType Leaf) {
        $bundledTsfCliPath
    }
    else {
        Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf.Cli\bin\x64\Release\net8.0-windows\SoftTalkIme.Tsf.Cli.exe"
    }
}
$resolvedTsfCliPath = Resolve-Path -LiteralPath $TsfCliPath -ErrorAction Stop

if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "找不到 COM Host：$dllPath。请先执行 dotnet publish。"
}
if (-not (Test-Path -LiteralPath $resolvedTsfCliPath -PathType Leaf)) {
    throw "找不到 TSF 注册 CLI：$resolvedTsfCliPath。请先执行 dotnet build SoftTalk-IME.sln --configuration Release。"
}

$regsvr32 = Join-Path $env:windir "System32\regsvr32.exe"
if (-not (Test-Path -LiteralPath $regsvr32 -PathType Leaf)) {
    throw "找不到系统注册工具：$regsvr32"
}

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "注册输入法需要管理员权限，请用‘以管理员身份运行’的 PowerShell 启动安装。"
    }
}

function Invoke-Regsvr32([string[]]$Arguments) {
    $process = Start-Process -FilePath $regsvr32 -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden
    return $process.ExitCode
}

if ($PSCmdlet.ShouldProcess("$dllPath; $resolvedTsfCliPath", "注册 SoftTalk-IME TSF COM Host 与官方语言 Profile")) {
    Assert-Administrator
    $regsvr32ExitCode = Invoke-Regsvr32 @("/s", $dllPath)
    if ($regsvr32ExitCode -ne 0) {
        throw "regsvr32 注册失败，退出码：$regsvr32ExitCode"
    }

    try {
        & $resolvedTsfCliPath register --apply
        if ($LASTEXITCODE -ne 0) {
            throw "TSF 官方注册失败，退出码：$LASTEXITCODE"
        }
    }
    catch {
        [void](Invoke-Regsvr32 @("/u", "/s", $dllPath))
        throw
    }

    Write-Output "SoftTalk-IME TSF 已按官方流程注册。"
}
