[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\bin\Release\net8.0-windows\publish"),
    [switch]$CurrentUser
)

$ErrorActionPreference = "Stop"
$clsid = "{D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41}"
$profileId = "{C1E7B9C8-7E3F-45CF-9E2A-3F705C4F0C6B}"
$languageId = "0x00000804"
$dllPath = Join-Path (Resolve-Path $PublishDir) "SoftTalkIme.Tsf.comhost.dll"

if (-not (Test-Path -LiteralPath $dllPath)) {
    throw "找不到 COM Host：$dllPath。请先执行 dotnet publish。"
}

$root = if ($CurrentUser) { "HKCU:\Software\Classes" } else { "HKLM:\Software\Classes" }
$tipRoot = if ($CurrentUser) { "HKCU:\Software\Microsoft\CTF\TIP" } else { "HKLM:\Software\Microsoft\CTF\TIP" }
$clsidPath = Join-Path $root "CLSID\$clsid\InprocServer32"
$tipPath = Join-Path $tipRoot $clsid
$languageProfilePath = Join-Path $tipPath "LanguageProfile\$languageId\$profileId"

if ($PSCmdlet.ShouldProcess($dllPath, "注册 SoftTalk-IME TSF COM Host")) {
    New-Item -Path $clsidPath -Force | Out-Null
    New-ItemProperty -Path $clsidPath -Name "(default)" -Value $dllPath -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $clsidPath -Name "ThreadingModel" -Value "Both" -PropertyType String -Force | Out-Null

    New-Item -Path $tipPath -Force | Out-Null
    New-ItemProperty -Path $tipPath -Name "(default)" -Value "SoftTalk-IME" -PropertyType String -Force | Out-Null
    New-Item -Path $languageProfilePath -Force | Out-Null
    New-ItemProperty -Path $languageProfilePath -Name "(default)" -Value "SoftTalk-IME" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $languageProfilePath -Name "Description" -Value "话术精灵输入法" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $languageProfilePath -Name "Enable" -Value 1 -PropertyType DWord -Force | Out-Null

    $regsvr32 = Join-Path $env:windir "System32\regsvr32.exe"
    & $regsvr32 /s $dllPath
    if ($LASTEXITCODE -ne 0) {
        throw "regsvr32 注册失败，退出码：$LASTEXITCODE"
    }
    Write-Output "SoftTalk-IME TSF 已注册。CurrentUser=$CurrentUser"
}
