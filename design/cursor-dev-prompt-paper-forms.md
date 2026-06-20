# 普讯科技应用管家 · 纸型管理功能 — Cursor AI 开发提示词

> 本提示词在已有「普讯科技应用管家」(.NET 8 + Avalonia) 工程基础上，**新增"本机打印纸型管理"功能**。
> 请与主提示词 `design/cursor-dev-prompt.md` 配合阅读，沿用同样的分层、命名、错误处理与测试规范。
> 配套高保真原型：`design/paper-forms-mockup.html`、`design/paper-forms-light.png`、`design/paper-forms-dark.png`、`design/paper-forms-full.png`，请以其视觉与交互为 UI 实现基准。

---

## 0. 给 AI 的角色与目标

你是一名资深 .NET + Avalonia + Windows 打印子系统工程师。请在现有解决方案中新增"纸型管理"功能模块。

要求：
- **功能完整、逻辑不缺失**：每个分支、每个异常路径都要处理，不留 TODO 逻辑。
- 严格复用现有架构：`Core`(业务，无 UI 依赖) + `App`(Avalonia MVVM) + `Tests`(xUnit)，依赖注入、Serilog 日志、`OperationResult` 错误模型保持一致。
- 与现有检测/安装模块**同级、互不影响**。
- 实现后对照第 11 节验收清单逐条自检并回报。

---

## 1. 功能概述

应用管家新增「纸型管理」，用于管理 **本机打印服务器的纸型（Windows Form / 表单）**。提供三类纸型的新增，并支持编辑、删除、列出：

1. **一式二等份**：用户输入整张纸 宽×高，等份数固定为 2。
2. **一式三等份**：用户输入整张纸 宽×高，等份数固定为 3。
3. **自定义纸型**：用户输入名称 + 宽×高 + 可选可打印边距(上/下/左/右)。

已确认的关键业务规则：
- **等份纸型注册尺寸 = 整张纸尺寸**（注册一个整张尺寸的 Windows 表单，并记录"等份数"，每份高 = 高 / 等份数，供业务程序切分使用）。
- **重复判定 = 先名称、再名称 + 尺寸都查**（详见第 5 节）。
- **增 / 改 / 删 需管理员权限**：通过一次 UAC 提权完成（用户已接受）。查询(列表)不需要提权。

---

## 2. 技术本质（务必理解）

Windows 纸型 = 打印后台(Print Spooler)的**机器级表单(Form)**，通过 `winspool.drv` 管理：
- `OpenPrinter(NULL/服务器名, ...)` 打开本机打印服务器句柄
- `EnumForms(hServer, 1, ...)` 枚举全部表单（系统内置 + 用户自建）
- `AddForm(hServer, 1, &FORM_INFO_1)` 新增表单
- `SetForm(hServer, name, 1, &FORM_INFO_1)` 修改表单
- `DeleteForm(hServer, name)` 删除表单
- `ClosePrinter(hServer)`

`FORM_INFO_1` 关键字段：
- `Flags`：`FORM_USER`(用户) / `FORM_BUILTIN`(系统内置，不可改/删) / `FORM_PRINTER`
- `pName`：表单名（机器内唯一）
- `Size`：`{cx, cy}`，单位 **0.001mm（微米）**，即 1mm = 1000
- `ImageableArea`：`{left, top, right, bottom}`，单位同上（可打印区域，用边距换算）

> 单位换算：界面用 mm，存/调用 winspool 用微米；`微米 = mm × 1000`，反之除以 1000。
> 新增/修改表单写入的是机器级配置，需管理员权限；`AddForm`/`SetForm`/`DeleteForm` 无权限时返回 `ERROR_ACCESS_DENIED(5)`。

---

## 3. 数据模型（新增到 Core/Models 或 Core/PaperForms）

```csharp
public enum PaperCategory { TwoEqual, ThreeEqual, Custom } // 一式二等份/一式三等份/自定义

public sealed class Margins
{
    public double TopMm { get; set; }
    public double BottomMm { get; set; }
    public double LeftMm { get; set; }
    public double RightMm { get; set; }
    public static Margins Zero => new();
}

public sealed class PaperType
{
    public string Name { get; set; } = string.Empty;     // Windows 表单名，机器内唯一
    public PaperCategory Category { get; set; }
    public double WidthMm { get; set; }                   // 整张纸宽
    public double HeightMm { get; set; }                  // 整张纸高
    public int Parts { get; set; } = 1;                   // 等份数：2/3/1(自定义)
    public Margins ImageableMargins { get; set; } = Margins.Zero;
    public bool IsBuiltIn { get; set; }                   // 系统内置(不可改/删)

    public double PartHeightMm => Parts > 0 ? HeightMm / Parts : HeightMm; // 每份高(派生)

    public static int PartsOf(PaperCategory c) => c switch
    {
        PaperCategory.TwoEqual => 2,
        PaperCategory.ThreeEqual => 3,
        _ => 1
    };
}
```

---

## 4. 服务接口与实现

### 4.1 接口（Core/PaperForms）

```csharp
public interface IPaperFormService
{
    /// <summary>EnumForms：列出本机全部纸型(系统内置 + 用户自建)。查询不需要提权。</summary>
    IReadOnlyList<PaperType> ListForms();

    /// <summary>AddForm：新增纸型（需管理员；调用前已通过判重）。</summary>
    OperationResult Add(PaperType paper);

    /// <summary>SetForm：编辑纸型；若改了名称需处理(删旧增新或 SetForm)。内置不可改。</summary>
    OperationResult Update(string originalName, PaperType paper);

    /// <summary>DeleteForm：删除纸型。内置不可删。</summary>
    OperationResult Delete(string name);
}
```

### 4.2 Windows 实现 `WindowsPaperFormService`
- 用 P/Invoke 调 `winspool.drv`（`OpenPrinter/EnumForms/AddForm/SetForm/DeleteForm/ClosePrinter`）。
- mm ↔ 微米换算；`PaperCategory` ↔ `Parts` 的元数据由于 Windows 表单本身不存"等份数"，需**额外持久化等份信息**（见第 4.3）。
- 非 Windows 平台：接口可由空实现/抛 `PlatformNotSupported`，但要保证 Core 可跨平台编译、可单元测试（判重/换算逻辑独立成纯函数，便于测试）。
- 写操作(增/改/删)封装为"需提权"：复用现有 `IProcessRunner.RunElevatedAsync` 思路，以管理员身份执行单次写入（可启动自身的一个隐藏"纸型写入"子命令并 `runas`，或调用提权辅助进程）。查询走普通权限。

### 4.3 等份数(Parts)与类别的持久化
Windows 表单只存名称+尺寸+可打印区域，不存"等份数/类别"。为在列表里正确显示类别与每份高度，需要把 `Name → {Category, Parts}` 的映射**持久化到应用配置目录**（如 `%AppData%\PuxunAppManager\paper-types.json`）：
- 新增/编辑时写入该映射；删除时移除。
- `ListForms()` 合并 EnumForms 结果与该映射：能匹配到映射的标类别/等份；匹配不到的（系统内置或外部创建的）按"自定义/系统内置"展示，等份显示"—"。

---

## 5. 重复检测（已确认：先名称、再名称 + 尺寸都查）

新增纯函数 `PaperUniqueness`（便于单元测试）：

```csharp
public enum DuplicateKind { None, Name, Size }

public static class PaperUniqueness
{
    // existing: 当前已存在纸型列表; editingOriginalName: 编辑时排除自身(新增传 null)
    public static (DuplicateKind kind, PaperType? conflict) Check(
        PaperType candidate, IReadOnlyList<PaperType> existing, string? editingOriginalName)
    {
        bool NameEq(string a, string b) =>
            string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        var pool = editingOriginalName is null
            ? existing
            : existing.Where(p => !NameEq(p.Name, editingOriginalName)).ToList();

        // 1) 先查名称
        var byName = pool.FirstOrDefault(p => NameEq(p.Name, candidate.Name));
        if (byName is not null) return (DuplicateKind.Name, byName);

        // 2) 再查名称+尺寸（类别 + 宽 + 高 + 等份数 完全相同）
        var bySize = pool.FirstOrDefault(p =>
            p.Category == candidate.Category &&
            NearlyEqual(p.WidthMm, candidate.WidthMm) &&
            NearlyEqual(p.HeightMm, candidate.HeightMm) &&
            p.Parts == candidate.Parts);
        if (bySize is not null) return (DuplicateKind.Size, bySize);

        return (DuplicateKind.None, null);
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.05; // 0.05mm 容差
}
```

UI 拦截规则：
- `DuplicateKind.Name` → 红字"纸型『XXX』已存在，不予添加。"
- `DuplicateKind.Size` → 红字"已存在相同尺寸的纸型『YYY』(宽×高，N 等份)，不予添加。"
- `None` → 放行，执行 `Add/Update`（触发 UAC）。

---

## 6. UI 规格（以原型为准）

新增 `App/Views/PaperFormsWindow.axaml` + `PaperFormsViewModel` / `PaperTypeItemViewModel`，遵循 `design/paper-forms-*.png`：

- 头部：打印机图标 + 标题「纸型管理」+ 副标题 + 深浅色切换。
- **新建纸型卡片**：
  - 分段选择器：一式二等份 / 一式三等份 / 自定义。
  - 字段：名称、整张纸 宽(mm)、高(mm)；选「自定义」时显示上/下/左/右边距，等份纸型隐藏边距。
  - 选等份纸型时实时显示蓝色提示："注册为整张纸尺寸 W×H mm，按 N 等份切分，每份 W×(H/N) mm"。
  - 「添加」按钮 + 旁注"增/改/删 需管理员权限（将弹出一次 UAC）"。
  - 重复时就地**红字**提示（按第 5 节文案），不弹窗、不提交。
- **已存在纸型列表**（来自 EnumForms）：列 = 名称 / 类别(彩色标签) / 宽×高(mm) / 等份 / 每份高(mm) / 操作。
  - 用户自建项显示「编辑」「删除」；系统内置项显示"内置不可改/删"且无操作按钮。
  - 编辑时把该行数据回填到上方表单，按钮切换为「保存修改 / 取消」。
- 底部状态栏：打印后台服务状态 + 纸型计数(自建/内置)。
- 浅/深双主题，与主窗口一致；所有耗时/提权操作不阻塞 UI 线程，进行中禁用按钮防重复提交。

### 入口（首页）
在主窗口工具栏「重新检测」左侧新增「纸型管理」按钮（带打印机图标），点击打开 `PaperFormsWindow`（`ShowDialog`），与现有「设置」窗口打开方式一致（通过 View 注入 Action，VM 不直接依赖窗口类型）。DI 注册 `IPaperFormService`(Windows 实现) 与 `PaperFormsViewModel`。

---

## 7. 权限（UAC）

- `ListForms()`：普通权限。
- `Add/Update/Delete`：需管理员。复用现有 `IProcessRunner.RunElevatedAsync` 模式，以管理员身份执行单次写入操作。
- 用户在 UAC 点"否" → 返回 `ErrorCodes.UacCancelled`，UI 提示"需要管理员权限才能修改纸型"，恢复可重试，列表不变。

---

## 8. 边界与异常清单（务必逐条覆盖）

1. 名称为空 / 仅空白 / 含非法字符(`\ / : * ? " < > |`) → 拦截并提示。
2. 宽或高 ≤ 0、或超过合理上限(如 > 2000mm) → 拦截。
3. 自定义边距：上+下 ≥ 高，或 左+右 ≥ 宽 → 拦截（可打印区域非法）。
4. 名称重复 / 尺寸重复 → 按第 5 节拦截。
5. 编辑改名撞到其它已存在纸型 → 拦截（判重排除自身）。
6. 编辑 / 删除系统内置(`FORM_BUILTIN`) → 禁止并提示"系统内置纸型不可修改/删除"。
7. UAC 被拒绝 → `UacCancelled` 提示，可重试。
8. 打印后台服务(Spooler)未运行 / `OpenPrinter` 失败 → 明确错误提示，列表显示空态 + 重试。
9. `AddForm/SetForm/DeleteForm` 返回系统错误码 → 转 `OperationResult` 并带 Win32 错误码记录日志。
10. 删除正被某打印机用作默认纸张的表单 → 捕获错误并提示。
11. 等份数与类别一致性：TwoEqual→2、ThreeEqual→3、Custom→1，由 `PaperType.PartsOf` 统一推导，避免不一致。
12. mm↔微米换算的取整误差：换算后回读比较使用容差(见 NearlyEqual)。
13. 重复点击「添加/保存」→ 操作进行中禁用按钮，任务去重。
14. `paper-types.json` 元数据缺失/损坏 → 忽略并按"自定义/未知等份"降级展示，不影响列表。

---

## 9. 落地位置

- `Core/PaperForms/`：`PaperType`、`PaperCategory`、`Margins`、`IPaperFormService`、`WindowsPaperFormService`(winspool P/Invoke)、`PaperUniqueness`、`PaperTypeMetadataStore`(等份/类别持久化)。
- `App/ViewModels/`：`PaperFormsViewModel`、`PaperTypeItemViewModel`。
- `App/Views/`：`PaperFormsWindow.axaml(.cs)`；主窗口工具栏入口按钮 + `MainWindowViewModel` 增加 `OpenPaperFormsCommand`/`OpenPaperFormsAction`。
- `App/Composition/AppServices.cs`：注册 `IPaperFormService`、`PaperFormsViewModel`。
- `tests/`：`PaperUniquenessTests`(名称/尺寸/编辑排除自身)、`PaperTypeTests`(等份/每份高/换算)、`WindowsPaperFormService` 的逻辑用假实现覆盖。

---

## 10. 与现有代码风格一致性

- 错误统一用 `OperationResult.Ok/Fail` + `ErrorCodes`（如新增 `PAPER_DUPLICATE_NAME`/`PAPER_DUPLICATE_SIZE`/`PAPER_BUILTIN_READONLY`/`PAPER_INVALID` 等常量）。
- 日志用注入的 `ILogger<T>`。
- ViewModel 用 `CommunityToolkit.Mvvm`(`[ObservableProperty]`/`[RelayCommand]`)。
- 平台专有调用包 try/catch 转 `OperationResult`，不让异常冒泡。
- 不引入后台常驻/自动行为，保持管家"用户态、按需操作"的设计边界。

---

## 11. 验收清单（实现后逐条自检并回报）

- [ ] 主页工具栏出现「纸型管理」入口，点击打开纸型管理窗口。
- [ ] 三类纸型均可新增：等份纸型按整张尺寸注册并记录等份数；自定义可设边距。
- [ ] 等份纸型实时显示"每份 = 高/N"提示，自定义显示边距字段。
- [ ] 判重：先名称、再名称+尺寸；命中时红字提示且不提交；编辑判重排除自身。
- [ ] 增/改/删触发一次 UAC；拒绝提权时明确提示且可重试，列表不变。
- [ ] 已存在纸型列表来自 EnumForms，正确区分自建/系统内置；内置不可改/删。
- [ ] 编辑回填、保存生效、删除生效后列表自动刷新；每份高度计算正确。
- [ ] 第 8 节 14 条边界/异常全部有处理。
- [ ] UI 对齐原型（分段选择器/表单/红字提示/列表/双主题/状态栏）。
- [ ] 判重、等份计算、mm↔微米换算有单元测试且通过。
- [ ] 解决方案可编译、可运行，`dotnet test` 全绿。
```
