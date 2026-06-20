#!/usr/bin/env bash
# 打包「普讯科技应用管家」为 Windows 自包含单文件可执行程序（可在 Linux/macOS 上交叉发布）。
# 用法：build/publish.sh [Configuration] [Runtime] [OutputDir]
# 例如：build/publish.sh Release win-x64 artifacts/win-x64
set -euo pipefail

CONFIG="${1:-Release}"
RUNTIME="${2:-win-x64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT="${3:-$ROOT/artifacts/$RUNTIME}"
PROJ="$ROOT/src/PuxunAppManager.App/PuxunAppManager.App.csproj"

echo "==> 发布 普讯科技应用管家 ($RUNTIME, $CONFIG)"

dotnet publish "$PROJ" \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=None \
    -o "$OUTPUT"

echo "==> 完成：$OUTPUT/PuxunAppManager.exe"
echo "下一步（务必在 Windows 上执行）：代码签名以避免被杀软误删"
echo "    pwsh build/sign.ps1 -File \"$OUTPUT/PuxunAppManager.exe\" -PfxPath <你的证书.pfx>"
