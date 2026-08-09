[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\bin\x64\Release\net8.0-windows"),
    [switch]$CurrentUser
)

$ErrorActionPreference = "Stop"
$clsid = "{D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41}"
$profileId = "{C1E7B9C8-7E3F-45CF-9E2A-3F705C4F0C6B}"
$dllPath = Join-Path (Resolve-Path $PublishDir) "SoftTalkIme.Tsf.comhost.dll"
$root = if ($CurrentUser) { "HKCU:\Software\Classes" } else { "HKLM:\Software\Classes" }
$tipRoot = if ($CurrentUser) { "HKCU:\Software\Microsoft\CTF\TIP" } else { "HKLM:\Software\Microsoft\CTF\TIP" }
$clsidPath = Join-Path $root "CLSID\$clsid"
$tipPath = Join-Path $tipRoot $clsid

if ($PSCmdlet.ShouldProcess($dllPath, "卸载 SoftTalk-IME TSF COM Host")) {
    if (Test-Path -LiteralPath $dllPath) {
        $regsvr32 = Join-Path $env:windir "System32\regsvr32.exe"
        & $regsvr32 /u /s $dllPath
        if ($LASTEXITCODE -ne 0) {
            throw "regsvr32 卸载失败，退出码：$LASTEXITCODE"
        }
    }
    Remove-Item -LiteralPath $tipPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $clsidPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Output "SoftTalk-IME TSF 已卸载。CurrentUser=$CurrentUser"
}
