<#
.SYNOPSIS
    对发布产物进行 Authenticode 代码签名（强烈建议使用 EV 证书）。

.DESCRIPTION
    使用 Windows SDK 的 signtool.exe 对 exe/dll/msi 签名并加时间戳。
    代码签名 + 提交杀软白名单是解决"主程序被杀软误删"的根治措施。

.PARAMETER File
    待签名文件路径（可为 exe / dll / msi）。

.PARAMETER PfxPath
    证书文件路径(.pfx)。EV 证书通常存于硬件 USB Key，此时改用 /sha1 指纹方式（见下方注释）。

.PARAMETER Password
    pfx 证书密码（可选）。

.PARAMETER TimestampUrl
    时间戳服务器，默认使用 DigiCert。

.EXAMPLE
    pwsh build\sign.ps1 -File artifacts\win-x64\PuxunAppManager.exe -PfxPath cert.pfx -Password ****
#>
param(
    [Parameter(Mandatory = $true)][string]$File,
    [string]$PfxPath = "",
    [string]$Password = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

# 定位 signtool.exe（Windows SDK）
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "x64" } |
    Sort-Object FullName -Descending | Select-Object -First 1

if (-not $signtool) { throw "未找到 signtool.exe，请安装 Windows SDK。" }

Write-Host "==> 使用 signtool: $($signtool.FullName)" -ForegroundColor Cyan

if ([string]::IsNullOrWhiteSpace($PfxPath)) {
    # EV 证书（硬件 Key）方式：用证书指纹，从计算机/用户证书存储签名
    # & $signtool.FullName sign /sha1 <证书指纹> /fd SHA256 /tr $TimestampUrl /td SHA256 $File
    throw "未提供 -PfxPath。若使用 EV 硬件证书，请改用 /sha1 <指纹> 方式（见脚本注释）。"
}

$args = @("sign", "/fd", "SHA256", "/f", $PfxPath, "/tr", $TimestampUrl, "/td", "SHA256")
if (-not [string]::IsNullOrWhiteSpace($Password)) { $args += @("/p", $Password) }
$args += $File

& $signtool.FullName @args
if ($LASTEXITCODE -ne 0) { throw "签名失败。" }

# 校验签名
& $signtool.FullName verify /pa /v $File
Write-Host "==> 签名完成并校验通过：$File" -ForegroundColor Green
