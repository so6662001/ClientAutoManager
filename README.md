# 普讯科技应用管家 (ClientAutoManager)

一个 **用户态、按需启动** 的 Windows 桌面工具，用于检测并管理本机已部署的多个客户端程序（公司自研 .NET 桌面客户端）。当客户电脑上的杀毒软件误删客户端主程序时，用户打开应用管家即可看到各客户端状态，并 **手动** 完成安装 / 修复 / 更新。

> 技术栈：**.NET 8 + Avalonia (Fluent 主题) + MVVM**。版本管理与安装包下载对接公司 **现有后端管理系统**（通过后端适配层 `IBackendClient`）。

---

## 设计边界（明确不做）

- ❌ 无后台常驻服务 / Windows Service
- ❌ 无自动自愈、自动重装、定时轮询、后台更新推送
- ❌ 无进程互保、文件隐藏、注入等任何类病毒对抗行为
- ✅ 仅在用户打开时检测 + 手动「重新检测」
- ✅ 所有安装/修复/更新均由用户点击触发
- ✅ 安装前强制 **SHA256 + Authenticode 数字签名** 双校验

---

## 解决方案结构

```
PuxunAppManager.sln
├─ src/PuxunAppManager.App     # Avalonia 表现层 (Views + ViewModels + DI 组合根)
├─ src/PuxunAppManager.Core    # 领域模型 + 业务服务（无 UI 依赖，可测试）
│   ├─ Models                  # ProductInfo / DetectionRule / OperationResult ...
│   ├─ Detection               # 检测引擎(注册表/文件/版本) + 五态状态机
│   ├─ Backend                 # 后端适配层 IBackendClient + 缓存 + 重试 + 映射
│   ├─ Installation            # 下载/校验/UAC 静默安装流水线
│   ├─ Security                # SHA256 + Authenticode 签名校验
│   ├─ Configuration           # 配置读写
│   └─ Infrastructure          # 日志
└─ tests/PuxunAppManager.Tests # 单元测试 (xUnit)
```

## 五态状态机（检测引擎）

| 条件 | 状态 | 可用操作 |
|------|------|----------|
| 卸载项不存在 且 关键文件都不存在 | `NotInstalled` 未安装 | 安装 |
| 关键文件齐全且哈希匹配 且 本机版本 == 最新 | `UpToDate` 已安装(最新) | 修复 |
| 关键文件齐全且哈希匹配 且 本机版本 < 最新 | `UpdateAvailable` 可更新 | 更新 / 修复 |
| 卸载项存在 但 关键文件缺失/哈希不符 | `Broken` 已损坏(疑似被杀软删除) | 修复 |
| 检测异常/无法判定 | `Unknown` 未知 | 重试检测 |

## 安装流水线（统一）

```
取安装包 → 下载(进度) → SHA256 校验 → Authenticode 签名校验
→ UAC 静默安装 → 读退出码(0/3010 视为成功) → 安装后复检 → 刷新/失败清理
```

任一安全校验失败将中止并删除临时文件；用户拒绝 UAC、退出码异常、安装后复检失败均有明确提示并可重试。

---

## 构建与运行

需要 **.NET 8 SDK**。

```bash
# 还原 + 编译
dotnet build PuxunAppManager.sln -c Release

# 运行（Windows）
dotnet run --project src/PuxunAppManager.App

# 运行单元测试
dotnet test
```

> 注：检测涉及的注册表读取、UAC 提权、msiexec 静默安装为 Windows 专有能力，需在 Windows 上运行管家本体；Core 业务逻辑可在任意平台编译与单元测试。

## 配置

首次运行会在 `%AppData%\PuxunAppManager\config.json` 生成默认配置。可在「设置」中修改后端地址、鉴权、主题等。样例见 [`samples/config.sample.json`](samples/config.sample.json)。

后端清单结构样例见 [`samples/manifest.sample.json`](samples/manifest.sample.json)。

## 对接现有后端

后端交互全部封装在 `src/PuxunAppManager.Core/Backend/RestBackendClient.cs`，其中以 `// === 后端对接点 ===` 标注了需要替换为公司现有后端真实接口的位置：

1. 产品清单接口 URL 与响应结构
2. 安装包信息接口
3. 下载地址/鉴权

若现有后端为 gRPC/SOAP 等非 REST 形态，只需在该类内部封装，对外 `IBackendClient` 接口保持不变，UI 与检测逻辑无需改动。

---

## ⚠️ 部署前必读（解决杀软误删的根治措施）

应用管家与各客户端安装包 **必须代码签名 + 提交杀软白名单**，否则下载的安装包仍可能在落地瞬间被误删：

1. **代码签名（建议 EV 证书）**：对应用管家自身、所有客户端 `.exe`/`.dll`/安装包进行 Authenticode 签名。EV 证书能快速积累 SmartScreen/Defender 信誉度。
2. **提交白名单/误报申诉**：向 360、火绒、腾讯电脑管家、金山毒霸、Microsoft Defender 等提交签名指纹与程序。
3. **避免误杀诱因**：不要加壳(UPX/VMProtect)、不要释放临时 exe 执行、不要 HTTP 明文下载执行、不要写系统目录/注入进程。
4. **引导客户加信任区**：安装时引导客户将安装目录加入杀软信任区（对企业客户尤其有效）。

> 鸿蒙（HarmonyOS）客户端不在本管家管理范围，走华为应用市场 / 企业 MDM 分发。

---

## 相关设计文档

- 高保真原型：[`design/app-manager-mockup.html`](design/app-manager-mockup.html)（含 [浅色](design/mockup-light.png) / [深色](design/mockup-dark.png) 截图）
- 开发提示词：[`design/cursor-dev-prompt.md`](design/cursor-dev-prompt.md)
