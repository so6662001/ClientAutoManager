# 发布产物 · 普讯科技应用管家 (win-x64)

本目录为 `build/publish.sh Release win-x64` 的实测产出。

- `PuxunAppManager.exe` — Windows x64 **自包含单文件** 可执行程序（目标机无需安装 .NET 运行时）。
  - 版本：v1.0.0
  - 构建配置：Release，`PublishSingleFile=true` + `IncludeNativeLibrariesForSelfExtract=true` + 压缩

## ⚠️ 分发前务必代码签名

未签名的程序极易被杀毒软件误删。请在 Windows 上对该 exe 进行 Authenticode 代码签名（强烈建议 EV 证书）后再分发：

```powershell
pwsh build\sign.ps1 -File dist\win-x64\PuxunAppManager.exe -PfxPath <你的证书.pfx>
```

并向 360 / 火绒 / 腾讯电脑管家 / Microsoft Defender 提交白名单。详见仓库根目录 `README.md` 的「部署前必读」。

> 说明：该 exe 是 Linux 交叉发布产物，仅作为构建验证示例；正式分发请在受控环境重新发布并完成签名。
