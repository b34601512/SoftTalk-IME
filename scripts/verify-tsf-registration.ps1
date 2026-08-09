[CmdletBinding()]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\bin\Release\net8.0-windows\publish")
)

$ErrorActionPreference = "Stop"
$clsid = "{D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41}"
$profileId = "{C1E7B9C8-7E3F-45CF-9E2A-3F705C4F0C6B}"
$languageId = "0x00000804"
$resolvedPublishDir = Resolve-Path -LiteralPath $PublishDir -ErrorAction Stop
$dllPath = Join-Path $resolvedPublishDir "SoftTalkIme.Tsf.comhost.dll"

$comKeyPath = "Registry::HKEY_LOCAL_MACHINE\Software\Classes\CLSID\$clsid\InprocServer32"
$profileKeyPath = "Registry::HKEY_LOCAL_MACHINE\Software\Microsoft\CTF\TIP\$clsid\LanguageProfile\$languageId\$profileId"

if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "找不到待验证的 COM Host：$dllPath"
}
if (-not (Test-Path -LiteralPath $comKeyPath)) {
    throw "未找到 COM 注册项：$comKeyPath"
}
if (-not (Test-Path -LiteralPath $profileKeyPath)) {
    throw "未找到 TSF 语言 Profile：$profileKeyPath"
}

$registeredDllPath = [string](Get-ItemProperty -LiteralPath $comKeyPath).'(default)'
if (-not [String]::Equals($registeredDllPath, $dllPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "COM 注册路径不匹配：$registeredDllPath"
}

Write-Output "TSF_REGISTRATION_VERIFIED: CLSID=$clsid LANGID=$languageId PROFILE=$profileId"
