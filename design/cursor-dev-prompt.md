# 普讯科技应用管家 — Cursor AI 开发提示词

> 用途：把本文件整体（或分节）粘贴给 Cursor，用于驱动 AI 完成「普讯科技应用管家」的开发。
> 已配套高保真原型：`design/app-manager-mockup.html`、`design/mockup-light.png`、`design/mockup-dark.png`，请以其视觉与交互为 UI 实现基准。

---

## 0. 给 AI 的角色与总目标（System / 首条指令）

你是一名资深 .NET + Avalonia 桌面应用工程师。请基于以下完整规格，开发一个名为「**普讯科技应用管家**」的 Windows 桌面应用。

要求：
- **功能完整、逻辑不缺失**：每个状态、每个分支、每个异常路径都要有明确处理，不允许出现 TODO 占位逻辑（除非是明确标注的"后端接口对接点"）。
- 代码结构清晰、分层合理、可测试、可维护。
- 严格遵循本文件的数据模型、状态机、验收标准。
- 实现完成后，对照第 12 节「验收清单」逐条自检并说明每条是否满足。
- 遇到规格中未定义的细节，选择行业通用的合理默认实现并在代码注释/说明中标注（不要反复追问）。

---

## 1. 项目概述

「普讯科技应用管家」是一个**用户态、按需启动**的桌面工具，用于检测并管理本机已部署的多个客户端程序（均为公司自研的 .NET 桌面客户端）。

核心场景：客户电脑上的杀毒软件可能误删我司客户端的主程序。应用管家在用户**主动打开时**检测各客户端的安装状态，并允许用户**手动**完成：安装（未安装）、修复（已损坏/重装同版本）、更新（有新版本）。

明确的设计边界（务必遵守，不要自行加回）：
- ❌ **不做**后台常驻服务 / Windows Service。
- ❌ **不做**自动自愈、自动重装、定时轮询、后台更新推送。
- ❌ **不做**进程互保、文件隐藏、注入等任何类病毒对抗行为。
- ✅ 只在**用户打开时检测** + 提供**手动「刷新（重新检测）」**。
- ✅ 所有安装/修复/更新动作均由**用户点击**触发。
- ✅ 版本信息与安装包下载，**对接公司现有后端管理系统**（不新建服务端）。

---

## 2. 技术栈与运行环境

- 语言/框架：**C# + .NET 8**
- UI：**Avalonia UI（最新稳定版）+ Fluent 主题**，MVVM 架构（推荐 CommunityToolkit.Mvvm）。
- 目标平台：**Windows 10/11 x64**（架构上用平台抽象隔离 Windows 专有逻辑，便于将来扩展，但当前只交付 Windows）。
- 依赖管理：使用 NuGet，禁止编造不存在的包/版本。
- 安装包格式：被管理的客户端安装包为 **MSI 或 EXE**，支持静默参数（MSI 用 `msiexec /i pkg.msi /qn`，EXE 用各自的静默开关，配置可指定）。
- 提权：管家本身以普通用户启动；执行安装/修复/更新时**按需 UAC 提权**（以管理员身份启动安装子进程）。

---

## 3. 总体架构（分层）

```
PuxunAppManager.sln
├─ PuxunAppManager.App            // Avalonia 表现层 (Views + ViewModels)
├─ PuxunAppManager.Core           // 领域模型 + 业务服务 (无 UI 依赖)
│   ├─ Models                     // ProductInfo / DetectionRule / OperationResult ...
│   ├─ Detection                  // 检测引擎 IDetectionService
│   ├─ Backend                    // 后端适配层 IBackendClient (对接现有后端)
│   ├─ Installation               // 下载/校验/安装执行器 IInstallerService
│   ├─ Security                   // 签名 & 哈希校验 ISignatureVerifier
│   └─ Configuration              // 配置/清单解析
└─ PuxunAppManager.Tests          // 单元测试
```

关键设计：
- **后端适配层 `IBackendClient`** 是唯一与公司现有后端交互的出口。管家内部只认统一模型；适配层把现有后端的真实接口/字段映射成统一模型。后端接口变化时只改这一层。
- 表现层（ViewModel）只依赖 `Core` 的接口，不直接碰 HTTP / 文件系统 / 进程，便于测试与替换。

---

## 4. 核心数据模型（统一内部模型）

```csharp
// 产品状态枚举
public enum ProductStatus
{
    NotInstalled,     // 未安装
    UpToDate,         // 已安装且为最新
    UpdateAvailable,  // 已安装但有新版本
    Broken,           // 已安装但关键文件缺失/损坏（典型：被杀软删除）
    Unknown           // 检测失败/无法判定
}

// 操作类型
public enum OperationType { Install, Repair, Update }

// 产品信息（合并了后端清单 + 本机检测结果）
public class ProductInfo
{
    public string ProductId { get; set; }        // 唯一标识，关联后端与检测规则
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string IconKey { get; set; }
    public string? InstalledVersion { get; set; } // 本机版本，null 表示未安装
    public string LatestVersion { get; set; }     // 后端返回的最新版本
    public long InstallerSizeBytes { get; set; }
    public ProductStatus Status { get; set; }
    public List<string> MissingFiles { get; set; } // Broken 时列出缺失文件
    public bool SignatureVerified { get; set; }
}

// 检测规则（来自配置/后端清单）
public class DetectionRule
{
    public string? RegistryUninstallKey { get; set; } // HKLM 卸载项路径
    public string? VersionRegistryValue { get; set; } // 读取版本号的注册表值名
    public List<KeyFile> KeyFiles { get; set; }       // 关键文件 + 期望哈希(可选)
}
public class KeyFile { public string Path { get; set; } public string? Sha256 { get; set; } }

// 安装包元数据（来自后端）
public class InstallerPackage
{
    public string ProductId { get; set; }
    public string Version { get; set; }
    public string DownloadUrl { get; set; }
    public string? Sha256 { get; set; }              // 用于下载后校验
    public string InstallerType { get; set; }        // "msi" | "exe"
    public string SilentArgs { get; set; }           // 静默安装参数
    public long SizeBytes { get; set; }
}

// 统一操作结果
public class OperationResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}
```

---

## 5. 后端适配层（对接现有后端）— 必须保留为清晰对接点

> 公司已有后端管理系统提供版本管理与下载。以下接口为管家内部契约；**适配层实现需对接现有后端真实 API**。
> 在实现处用 `// === 后端对接点 ===` 注释清晰标出需要替换为真实后端调用的位置，并把后端地址、鉴权方式抽到配置文件，便于联调。

```csharp
public interface IBackendClient
{
    // 拉取所有产品的最新版本清单（含下载地址、哈希、检测规则）
    Task<IReadOnlyList<BackendProductManifest>> GetManifestAsync(CancellationToken ct);

    // 获取指定产品指定版本的安装包下载信息
    Task<InstallerPackage> GetInstallerAsync(string productId, string version, CancellationToken ct);

    // 下载安装包到本地（支持进度回调、断点/重试）
    Task<string> DownloadInstallerAsync(InstallerPackage pkg, IProgress<DownloadProgress> progress, CancellationToken ct);
}
```

适配层实现要求：
- 后端形态默认按 **REST + JSON** 实现；若公司后端为其它形态（gRPC/SOAP），在该层内部封装，对外保持接口不变。
- 支持配置 BaseUrl、鉴权头（API Key / Token / 客户授权码，三选一可配）。
- 网络请求需：超时、失败重试（指数退避，最多 4 次：4s/8s/16s/32s）、明确错误码。
- 下载需校验返回内容长度与 `Content-Type`，下载到临时目录后再做哈希/签名校验。

---

## 6. 功能需求（逐条实现，确保逻辑闭环）

### 6.1 启动与检测
1. 应用启动后立即执行一次"检测流程"，加载期间 UI 显示骨架/加载态。
2. 检测流程：① 调用 `IBackendClient.GetManifestAsync` 拉取最新清单（含各产品检测规则与最新版本）；② 对每个产品执行本机检测；③ 合并为 `ProductInfo` 列表并渲染。
3. 顶部「重新检测」按钮可手动重跑检测流程（带旋转动画，期间禁用按钮防重复点击）。
4. 后端不可达时：仍能基于**本地缓存的上次清单**做检测（降级），并在状态栏提示"离线/使用缓存清单"；无任何缓存时给出可重试的错误态。

### 6.2 本机检测与状态判定（状态机，务必完整）
对每个产品按下列规则判定 `ProductStatus`：

| 条件 | 结果 |
|------|------|
| 卸载项不存在 且 关键文件都不存在 | `NotInstalled` |
| 卸载项存在 但 关键文件缺失或哈希不符 | `Broken`（记录 `MissingFiles`） |
| 关键文件齐全且哈希匹配 且 本机版本 == 最新版本 | `UpToDate` |
| 关键文件齐全且哈希匹配 且 本机版本 < 最新版本 | `UpdateAvailable` |
| 检测过程抛异常/无法读取 | `Unknown`（UI 显示并允许重试） |

- 版本比较使用语义化版本比较（处理 `1.0`、`1.0.0`、`1.0.0.0` 等位数不一致）。
- 注册表读取需兼容 32/64 位视图（WOW6432Node）。
- 路径中的环境变量（`%ProgramFiles%` 等）需展开。

### 6.3 状态 → 可用操作映射
| 状态 | 主操作按钮 | 次操作 |
|------|-----------|--------|
| NotInstalled | 安装 (Install) | — |
| UpToDate | 修复 (Repair) | — |
| UpdateAvailable | 更新 (Update) | 修复 |
| Broken | 修复 (Repair) | — |
| Unknown | 重试检测 | — |

### 6.4 安装 / 修复 / 更新执行（统一流水线）
点击任一操作后执行统一流水线，并把每一步反馈到该产品卡片的进度区：
1. 向后端获取该产品目标版本的 `InstallerPackage`。
2. 下载安装包到临时目录（显示下载进度：已下载/总大小、百分比、可取消）。
3. **安全校验（强制）**：先校验文件 SHA256（若清单提供）→ 再校验 Windows Authenticode 数字签名（签名者、是否受信任、是否吊销）。任一校验失败：中止、删除临时文件、提示"安全校验未通过，已阻止安装"。
4. UAC 提权执行安装：
   - MSI：`msiexec /i "pkg.msi" /qn /norestart`（修复用 `/fa` 或重装）。
   - EXE：使用配置的 `SilentArgs`。
   - 以管理员身份启动子进程（`UseShellExecute=true, Verb="runas"`）；用户拒绝 UAC 时返回明确的"用户取消提权"。
5. 安装子进程结束后读取退出码（0 / 3010=需重启 视为成功；其余为失败并记录退出码）。
6. **安装后复检**：重新对该产品执行检测，确认状态变为 `UpToDate`；据此刷新该卡片。
7. 全程结果写日志，并在 UI 给出成功/失败（含失败原因与重试入口）。
8. 失败时清理临时文件；可安全重试。

### 6.5 配置与设置
- 配置文件（JSON，放用户目录如 `%AppData%\PuxunAppManager\config.json`）：后端 BaseUrl、鉴权信息、清单缓存路径、下载目录、日志级别。
- 设置界面：查看/修改后端地址（可选）、清空缓存、打开日志目录、查看应用管家版本与签名状态、深/浅色主题切换。

### 6.6 日志与诊断
- 使用结构化日志（Serilog 或 Microsoft.Extensions.Logging），写入 `%AppData%\PuxunAppManager\logs\`，按天滚动。
- 记录：检测结果、每次操作的完整流水线步骤、网络错误、校验失败、安装退出码。
- "已损坏"判定时额外记录"疑似被安全软件移除"事件（便于公司侧统计误杀）。

---

## 7. UI 规格（以原型为准）

整体遵循 `design/mockup-light.png` / `mockup-dark.png` 的布局与视觉：

- 顶部品牌栏：盾牌图标 + 标题「**普讯科技应用管家**」+ 副标题「检测并管理本机已部署的客户端程序 · 安装 / 修复 / 更新」+ 右上角主题切换 & 设置。
- 工具栏：搜索框（按名称过滤）、状态筛选标签（全部 / 未安装 / 可更新 / 已损坏，带数量）、右侧「重新检测」主按钮。
- 产品列表：卡片式，每张含 图标、名称、描述、本机版本、最新版本、签名状态、状态徽章、操作按钮（按 6.3 映射）。
- 安装/下载进行中：卡片切换为进度态（进度条 + 字节进度 + "数字签名与哈希校验通过"提示 + 取消按钮）。
- 底部状态栏：后端连接状态（在线/离线/缓存）、上次检测时间、应用管家版本与签名状态。
- 主题：**浅色 / 深色双主题**，遵循 Avalonia Fluent 配色（见原型 CSS 变量），可切换并记忆用户选择。
- 状态徽章配色：已安装最新=绿，可更新=蓝，已损坏=橙，未安装=红。
- 所有耗时操作不得阻塞 UI 线程（async/await + 进度回调）。
- 操作按钮在进行中需禁用并显示加载态，防止重复触发。

---

## 8. 非功能需求

- **安全（重点）**：安装前强制签名 + 哈希双校验；只从配置的可信后端地址下载；全程 HTTPS；不执行任何未通过校验的文件。
- **代码签名**：构建产物（应用管家自身）预留代码签名步骤；README 中说明需用 EV 证书签名并提交各杀软白名单（360/火绒/腾讯管家/Defender）。
- **健壮性**：所有外部调用（网络、文件、注册表、进程）包 try/catch，转为 `OperationResult` 或可重试错误态，不得让异常冒泡到崩溃。
- **性能**：检测多个产品并行执行；UI 流畅，启动检测有加载态。
- **可测试性**：`Core` 层全部面向接口；为状态判定、版本比较、校验逻辑写单元测试（用假数据/临时目录）。
- **国际化**：界面文案集中管理（资源文件），默认简体中文。

---

## 9. 边界与异常处理清单（务必逐条覆盖，防止逻辑缺失）

1. 后端完全不可达且无缓存清单 → 错误态 + 重试按钮。
2. 后端可达但某产品缺少下载地址/版本字段 → 该产品标 `Unknown`，不影响其它产品。
3. 下载中断/超时 → 重试（指数退避）；多次失败后报错并清理临时文件。
4. 下载文件哈希不匹配 → 阻止安装、删除文件、明确提示。
5. 数字签名缺失/不受信任/已吊销 → 阻止安装、明确提示。
6. 用户在 UAC 弹窗点"否" → 返回"用户取消提权"，恢复卡片为可重试状态。
7. 安装子进程返回非 0（非 3010）→ 记录退出码、报失败、可重试。
8. 安装成功但复检仍不完整（可能立刻又被杀软删）→ 提示"安装后文件被移除，疑似被安全软件拦截"，引导用户将目录加入信任区。
9. 注册表卸载项存在但版本号读不到 → 视为已安装、版本未知，允许修复/更新。
10. 同一产品重复点击操作 → 按钮禁用 + 任务去重，避免并发安装。
11. 磁盘空间不足 / 临时目录不可写 → 提前检测或捕获并提示。
12. 关闭窗口时有正在进行的安装 → 提示确认，可取消或后台等待完成。
13. 配置文件缺失/损坏 → 使用内置默认配置并重建。
14. 32/64 位注册表视图差异、环境变量路径展开。
15. 版本号位数不一致的比较正确性。

---

## 10. 配置 / 清单示例

`config.json`：
```jsonc
{
  "backend": {
    "baseUrl": "https://你们现有后端域名/api",
    "authType": "token",          // none | apiKey | token
    "authValue": "",              // 运行时填充/登录获取
    "timeoutSeconds": 30
  },
  "paths": {
    "manifestCache": "%AppData%/PuxunAppManager/manifest.cache.json",
    "downloadDir": "%AppData%/PuxunAppManager/downloads",
    "logDir": "%AppData%/PuxunAppManager/logs"
  },
  "ui": { "theme": "system" }      // light | dark | system
}
```

后端产品清单条目（适配层需把现有后端响应映射成此结构）：
```jsonc
{
  "productId": "clientA",
  "displayName": "客户端A · 业务主程序",
  "description": ".NET 桌面客户端 · 主收银与业务处理",
  "iconKey": "A",
  "latestVersion": "2.1.0",
  "installer": {
    "downloadUrl": "https://.../clientA-2.1.0.msi",
    "sha256": "....",
    "installerType": "msi",
    "silentArgs": "/i \"{file}\" /qn /norestart",
    "sizeBytes": 14260736
  },
  "detect": {
    "registryUninstallKey": "HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{GUID}",
    "versionRegistryValue": "DisplayVersion",
    "keyFiles": [
      { "path": "%ProgramFiles%\\ClientA\\ClientA.exe", "sha256": "..." }
    ]
  }
}
```

---

## 11. 交付物与里程碑（建议分阶段，逐阶段可运行）

- **M1 项目骨架**：解决方案分层、Avalonia 主窗口、MVVM、DI、配置加载、日志。
- **M2 检测引擎**：注册表/文件/版本检测 + 状态机 + 单元测试（可用假清单跑通并显示状态）。
- **M3 后端适配层**：`IBackendClient` REST 实现 + 缓存/重试 + 清单映射（带可替换的对接点注释）。
- **M4 安装流水线**：下载 + 签名/哈希校验 + UAC 静默安装 + 安装后复检 + 错误处理。
- **M5 UI 完整化**：对齐原型（卡片、进度态、筛选、搜索、主题、状态栏、设置页）。
- **M6 收尾**：异常清单全覆盖、日志完善、打包（含代码签名说明）、README。

每个里程碑结束时：保证可编译、可运行，并附简短自测说明。

---

## 12. 验收清单（实现完成后逐条自检并回报）

- [ ] 启动即检测，可手动重新检测，加载态正常。
- [ ] 五种状态（未安装/最新/可更新/已损坏/未知）判定准确，符合第 6.2 状态机。
- [ ] 状态→操作映射正确（安装/修复/更新/重试）。
- [ ] 安装流水线完整：下载→哈希校验→签名校验→UAC 静默安装→读退出码→安装后复检→刷新。
- [ ] 任一安全校验失败都会阻止安装并清理文件。
- [ ] 后端离线可降级用缓存；无缓存有错误态与重试。
- [ ] 第 9 节 15 条边界/异常全部有处理。
- [ ] UI 对齐原型（含浅/深主题、进度态、筛选、搜索、状态栏、设置）。
- [ ] 后端对接点清晰标注、配置化，便于替换为公司现有后端真实接口。
- [ ] 无任何后台常驻/自动自愈/进程互保等被设计排除的行为。
- [ ] 关键逻辑（版本比较、状态判定、校验）有单元测试且通过。
- [ ] 可编译、可运行、可打包，附 README（含代码签名与杀软白名单说明）。

---

## 13. 重要提醒（写入 README）

1. 应用管家自身与各客户端安装包**必须代码签名（建议 EV 证书）**，并向主流杀软（360 / 火绒 / 腾讯电脑管家 / 微软 Defender）提交白名单，否则下载的安装包仍可能在落地瞬间被误删。
2. 安装包请使用标准 MSI/正规安装流程，**不要加壳、不要释放临时 exe 执行、不要 HTTP 明文下载执行**，以免触发误杀。
3. 鸿蒙（HarmonyOS）客户端不在本管家管理范围，走应用市场 / 企业 MDM 分发。
