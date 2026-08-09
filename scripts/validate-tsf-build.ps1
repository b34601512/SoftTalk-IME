[CmdletBinding()]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\bin\Release\net8.0-windows\publish")
)

$ErrorActionPreference = "Stop"
$resolvedPublishDir = Resolve-Path -LiteralPath $PublishDir -ErrorAction Stop
$requiredFiles = @(
    "SoftTalkIme.Tsf.comhost.dll",
    "SoftTalkIme.Tsf.X.manifest",
    "SoftTalkIme.Tsf.runtimeconfig.json",
    "SoftTalkIme.Tsf.dll",
    "PinyinNet.dll"
)

foreach ($fileName in $requiredFiles) {
    $filePath = Join-Path $resolvedPublishDir $fileName
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "TSF 发布产物缺少：$fileName"
    }
}

$serviceSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\SoftTalkImeTextService.cs")
$interopSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\TsfInterop.cs")
$registrationSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\TsfRegistration.cs")
$registerSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "register-tsf.ps1")
$unregisterSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "unregister-tsf.ps1")
$verifySource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "verify-tsf-registration.ps1")
if ($serviceSource -notmatch "D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41") {
    throw "TSF 类 GUID 未出现在源码中。"
}
if ($interopSource -notmatch "AA80E7F7-2021-11D2-93E0-0060B067B86E") {
    throw "ITfTextInputProcessor GUID 未出现在源码中。"
}
if ($interopSource -notmatch "AA80E7F5-2021-11D2-93E0-0060B067B86E") {
    throw "ITfKeyEventSink GUID 未出现在源码中。"
}
if ($interopSource -notmatch "EA1EA138-19DF-11D7-A6D2-00065B84435C") {
    throw "ITfCandidateListUIElement GUID 未出现在源码中。"
}
if ($interopSource -notmatch "EA1EA135-19DF-11D7-A6D2-00065B84435C") {
    throw "ITfUIElementMgr GUID 未出现在源码中。"
}
foreach ($guid in @(
        "1F02B6C5-7842-4EE6-8A0B-9A24183A95CA",
        "C3ACEFB5-F69D-4905-938F-FCADCF4BE830",
        "33C53A50-F456-4884-B049-85FD643ECFED",
        "A4B544A1-438D-4B41-9325-869523E2D6C7",
        "34745C63-B2F0-4784-8B67-5E12C8701A31")) {
    if ($registrationSource -notmatch $guid) {
        throw "TSF 官方注册契约缺少 GUID：$guid"
    }
}
if ($registrationSource -notmatch "SimplifiedChineseLanguageId = 0x0804") {
    throw "TSF 官方注册契约没有使用简体中文 LANGID 0x0804。"
}
$expectedPublishSegment = [regex]::Escape("bin\Release\net8.0-windows\publish")
if ($registerSource -notmatch $expectedPublishSegment -or $unregisterSource -notmatch $expectedPublishSegment) {
    throw "TSF 注册/卸载脚本默认目录没有指向 publish 产物。"
}
if ($registerSource -notmatch "TsfCliPath" -or $unregisterSource -notmatch "TsfCliPath") {
    throw "TSF 注册/卸载脚本没有接入官方注册 CLI。"
}
if ($registerSource -notmatch "SoftTalkIme.Tsf.Cli.exe" -or $unregisterSource -notmatch "SoftTalkIme.Tsf.Cli.exe") {
    throw "TSF 注册/卸载脚本没有支持安装包内置 CLI。"
}
if ($registerSource -notmatch "register --apply" -or $unregisterSource -notmatch "unregister --apply") {
    throw "TSF 注册/卸载脚本没有调用官方注册/卸载命令。"
}
if ($verifySource -notmatch "0x00000804" -or $verifySource -match "New-Item|Remove-Item|Set-Item|Set-ItemProperty") {
    throw "TSF 只读验证脚本的语言 ID 或写入边界不正确。"
}
if ($registerSource -notmatch "SupportsShouldProcess" -or $unregisterSource -notmatch "SupportsShouldProcess") {
    throw "TSF 注册/卸载脚本缺少 WhatIf 安全开关。"
}
if ($registerSource -notmatch "IsInRole.*Administrator" -or $unregisterSource -notmatch "IsInRole.*Administrator") {
    throw "TSF 注册/卸载脚本缺少管理员权限检查。"
}
if ($registerSource -notmatch "Start-Process" -or $unregisterSource -notmatch "Start-Process") {
    throw "TSF 注册/卸载脚本没有使用可检查退出码的 regsvr32 调用。"
}
if ($registerSource -notmatch "Verb RunAs" -or $unregisterSource -notmatch "Verb RunAs") {
    throw "TSF 注册/卸载脚本没有自动请求 UAC 提权。"
}

Write-Output "TSF_BUILD_VALIDATED: $resolvedPublishDir"
