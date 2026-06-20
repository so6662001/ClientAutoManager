<#
.SYNOPSIS
    打包「普讯科技应用管家」为 Windows 自包含单文件可执行程序。

.DESCRIPTION
    产出 win-x64 自包含单文件 exe（无需目标机安装 .NET 运行时）。
    发布完成后建议立即执行代码签名：build\sign.ps1 -File <发布的 exe>

.PARAMETER Configuration
    构建配置，默认 Release。

.PARAMETER Runtime
    运行时标识，默认 win-x64（可选 win-arm64）。

.PARAMETER Output
    输出目录，默认 artifacts\<runtime>。

.EXAMPLE
    pwsh build\publish.ps1
    pwsh build\publish.ps1 -Runtime win-x64 -Output dist
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src/PuxunAppManager.App/PuxunAppManager.App.csproj"
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $root "artifacts/$Runtime" }

Write-Host "==> 发布 普讯科技应用管家 ($Runtime, $Configuration)" -ForegroundColor Cyan

dotnet publish $proj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $Output

if ($LASTEXITCODE -ne 0) { throw "发布失败。" }

$exe = Join-Path $Output "PuxunAppManager.exe"
Write-Host "==> 完成：$exe" -ForegroundColor Green
Write-Host "下一步（务必执行）：代码签名以避免被杀软误删" -ForegroundColor Yellow
Write-Host "    pwsh build\sign.ps1 -File `"$exe`" -PfxPath <你的证书.pfx>" -ForegroundColor Yellow
