[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\bin\Release\net8.0-windows\publish"),
    [string]$TsfCliPath = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf.Cli\bin\x64\Release\net8.0-windows\SoftTalkIme.Tsf.Cli.exe")
)

$ErrorActionPreference = "Stop"
$resolvedPublishDir = Resolve-Path -LiteralPath $PublishDir -ErrorAction Stop
$dllPath = Join-Path $resolvedPublishDir "SoftTalkIme.Tsf.comhost.dll"
$resolvedTsfCliPath = Resolve-Path -LiteralPath $TsfCliPath -ErrorAction Stop

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
        throw "卸载输入法需要管理员权限，请用‘以管理员身份运行’的 PowerShell 启动卸载。"
    }
}

function Invoke-Regsvr32([string[]]$Arguments) {
    $process = Start-Process -FilePath $regsvr32 -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden
    return $process.ExitCode
}

if ($PSCmdlet.ShouldProcess("$dllPath; $resolvedTsfCliPath", "卸载 SoftTalk-IME TSF 官方语言 Profile 与 COM Host")) {
    Assert-Administrator
    & $resolvedTsfCliPath unregister --apply
    if ($LASTEXITCODE -ne 0) {
        throw "TSF 官方卸载失败，退出码：$LASTEXITCODE"
    }

    if (Test-Path -LiteralPath $dllPath -PathType Leaf) {
        $regsvr32ExitCode = Invoke-Regsvr32 @("/u", "/s", $dllPath)
        if ($regsvr32ExitCode -ne 0) {
            throw "regsvr32 卸载失败，退出码：$regsvr32ExitCode"
        }
    }

    Write-Output "SoftTalk-IME TSF 已按官方流程卸载。"
}
