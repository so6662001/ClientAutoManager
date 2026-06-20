# 发布产物 · 普讯科技应用管家 (win-x64)

本目录为 `build/publish.sh Release win-x64` 的实测产出。

- `PuxunAppManager.exe` — Windows x64 **自包含单文件** 可执行程序（目标机无需安装 .NET 运行时）。
  - 版本：v1.0.0
  - 已含功能：客户端检测/安装/修复/更新 + **纸型管理**（一式二等份 / 一式三等份 / 自定义）
  - 构建配置：Release，`PublishSingleFile=true` + `IncludeNativeLibrariesForSelfExtract=true` + 压缩

## 测试步骤

详见仓库 `docs/windows-test-checklist.md`。简要：双击运行 → 首页查看客户端检测 → 点工具栏「纸型管理」测试新增/判重/编辑/删除（会弹 UAC）。

## ⚠️ 分发前务必代码签名

未签名的程序极易被杀毒软件误删。请在 Windows 上对该 exe 进行 Authenticode 代码签名（强烈建议 EV 证书）后再分发：

```powershell
pwsh build\sign.ps1 -File dist\win-x64\PuxunAppManager.exe -PfxPath <你的证书.pfx>
```

并向 360 / 火绒 / 腾讯电脑管家 / Microsoft Defender 提交白名单。详见仓库根目录 `README.md` 的「部署前必读」。

> 说明：该 exe 是 Linux 交叉发布产物，仅作为构建验证/测试示例；正式分发请在受控环境重新发布并完成签名。
