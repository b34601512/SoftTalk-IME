[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\bin\Release\net8.0-windows\publish"),
    [string]$TsfCliPath = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf.Cli\bin\x64\Release\net8.0-windows\SoftTalkIme.Tsf.Cli.exe")
)

$ErrorActionPreference = "Stop"
$resolvedPublishDir = Resolve-Path -LiteralPath $PublishDir -ErrorAction Stop
$dllPath = Join-Path $resolvedPublishDir "SoftTalkIme.Tsf.comhost.dll"
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

if ($PSCmdlet.ShouldProcess("$dllPath; $resolvedTsfCliPath", "注册 SoftTalk-IME TSF COM Host 与官方语言 Profile")) {
    & $regsvr32 /s $dllPath
    if ($LASTEXITCODE -ne 0) {
        throw "regsvr32 注册失败，退出码：$LASTEXITCODE"
    }

    try {
        & $resolvedTsfCliPath register --apply
        if ($LASTEXITCODE -ne 0) {
            throw "TSF 官方注册失败，退出码：$LASTEXITCODE"
        }
    }
    catch {
        & $regsvr32 /u /s $dllPath
        throw
    }

    Write-Output "SoftTalk-IME TSF 已按官方流程注册。"
}
