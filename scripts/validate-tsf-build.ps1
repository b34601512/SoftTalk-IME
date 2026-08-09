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
    "SoftTalkIme.Tsf.dll"
)

foreach ($fileName in $requiredFiles) {
    $filePath = Join-Path $resolvedPublishDir $fileName
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "TSF 发布产物缺少：$fileName"
    }
}

$serviceSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\SoftTalkImeTextService.cs")
$interopSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "..\src\SoftTalkIme.Tsf\TsfInterop.cs")
if ($serviceSource -notmatch "D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41") {
    throw "TSF 类 GUID 未出现在源码中。"
}
if ($interopSource -notmatch "AA80E7F7-2021-11D2-93E0-0060B067B86E") {
    throw "ITfTextInputProcessor GUID 未出现在源码中。"
}
if ($interopSource -notmatch "AA80E7F5-2021-11D2-93E0-0060B067B86E") {
    throw "ITfKeyEventSink GUID 未出现在源码中。"
}

Write-Output "TSF_BUILD_VALIDATED: $resolvedPublishDir"
