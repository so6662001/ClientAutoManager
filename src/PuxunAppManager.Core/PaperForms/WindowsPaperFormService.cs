using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.PaperForms;

/// <summary>
/// 基于 winspool.drv 的纸型(表单)读写实现。mm ↔ 微米(×1000) 换算。
/// 写操作需管理员权限（由 App 层提权子进程调用）。
/// </summary>
public sealed class WindowsPaperFormService : IPaperFormService
{
    private const uint FORM_USER = 0x00000000;
    private const uint FORM_BUILTIN = 0x00000001;
    private const uint LEVEL_1 = 1;
    private const uint SERVER_ACCESS_ADMINISTER = 0x00000001;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    private readonly IPaperTypeMetadataStore _metadata;
    private readonly ILogger<WindowsPaperFormService> _logger;

    public WindowsPaperFormService(IPaperTypeMetadataStore metadata, ILogger<WindowsPaperFormService> logger)
    {
        _metadata = metadata;
        _logger = logger;
    }

    private static int MmToMicron(double mm) => (int)Math.Round(mm * 1000d);
    private static double MicronToMm(int micron) => micron / 1000d;

    public IReadOnlyList<PaperType> ListForms()
    {
        if (!OperatingSystem.IsWindows())
            return Array.Empty<PaperType>();

        try
        {
            return EnumFormsInternal();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "枚举纸型失败。");
            return Array.Empty<PaperType>();
        }
    }

    public OperationResult Add(PaperType paper)
    {
        var validate = PaperValidation.Validate(paper);
        if (!validate.Success) return validate;
        if (!OperatingSystem.IsWindows())
            return OperationResult.Fail(ErrorCodes.PaperSpoolerError, "仅支持 Windows。");
        return WriteOperation(server => AddFormImpl(server, paper));
    }

    public OperationResult Update(string originalName, PaperType paper)
    {
        var validate = PaperValidation.Validate(paper);
        if (!validate.Success) return validate;
        if (!OperatingSystem.IsWindows())
            return OperationResult.Fail(ErrorCodes.PaperSpoolerError, "仅支持 Windows。");

        return WriteOperation(server =>
        {
            // 内置不可改
            var existing = EnumFormsInternal().FirstOrDefault(f => PaperUniqueness.NameEquals(f.Name, originalName));
            if (existing is { IsBuiltIn: true })
                return OperationResult.Fail(ErrorCodes.PaperBuiltinReadonly, "系统内置纸型不可修改。");

            bool renamed = !PaperUniqueness.NameEquals(originalName, paper.Name);
            if (renamed)
            {
                // 改名：删旧 + 增新
                DeleteFormNative(server, originalName); // 旧名不存在也无妨
                return AddFormImpl(server, paper);
            }

            return SetFormImpl(server, paper);
        });
    }

    public OperationResult Delete(string name)
    {
        if (!OperatingSystem.IsWindows())
            return OperationResult.Fail(ErrorCodes.PaperSpoolerError, "仅支持 Windows。");

        return WriteOperation(server =>
        {
            var existing = EnumFormsInternal().FirstOrDefault(f => PaperUniqueness.NameEquals(f.Name, name));
            if (existing is null)
                return OperationResult.Fail(ErrorCodes.PaperNotFound, $"未找到纸型「{name}」。");
            if (existing.IsBuiltIn)
                return OperationResult.Fail(ErrorCodes.PaperBuiltinReadonly, "系统内置纸型不可删除。");

            if (!DeleteForm(server, name))
            {
                int err = Marshal.GetLastWin32Error();
                _logger.LogError("DeleteForm 失败，Win32 错误码 {Err}：{Name}", err, name);
                return OperationResult.Fail(ErrorCodes.PaperSpoolerError,
                    $"删除纸型失败（错误码 {err}）。该纸型可能正被某打印机使用。");
            }
            return OperationResult.Ok("已删除纸型。");
        });
    }

    // ===== 内部实现 =====

    private OperationResult WriteOperation(Func<IntPtr, OperationResult> action)
    {
        IntPtr server = IntPtr.Zero;
        var defaults = new PRINTER_DEFAULTS { pDatatype = IntPtr.Zero, pDevMode = IntPtr.Zero, DesiredAccess = SERVER_ACCESS_ADMINISTER };
        try
        {
            if (!OpenPrinter(null, out server, ref defaults) || server == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                _logger.LogError("OpenPrinter(管理员) 失败，Win32 错误码 {Err}", err);
                return OperationResult.Fail(ErrorCodes.PaperSpoolerError,
                    $"无法打开打印服务器（错误码 {err}）。请确认以管理员身份运行且打印后台服务正常。");
            }
            return action(server);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "纸型写操作异常。");
            return OperationResult.Fail(ErrorCodes.PaperSpoolerError, $"操作失败：{ex.Message}");
        }
        finally
        {
            if (server != IntPtr.Zero) ClosePrinter(server);
        }
    }

    private OperationResult AddFormImpl(IntPtr server, PaperType paper)
    {
        var info = BuildFormInfo(paper);
        if (!AddForm(server, LEVEL_1, ref info))
        {
            int err = Marshal.GetLastWin32Error();
            _logger.LogError("AddForm 失败，Win32 错误码 {Err}：{Name}", err, paper.Name);
            return OperationResult.Fail(ErrorCodes.PaperSpoolerError, $"新增纸型失败（错误码 {err}）。");
        }
        return OperationResult.Ok("已添加纸型。");
    }

    private OperationResult SetFormImpl(IntPtr server, PaperType paper)
    {
        var info = BuildFormInfo(paper);
        if (!SetForm(server, paper.Name, LEVEL_1, ref info))
        {
            int err = Marshal.GetLastWin32Error();
            // 表单不存在时回退为新增
            if (!AddForm(server, LEVEL_1, ref info))
            {
                int err2 = Marshal.GetLastWin32Error();
                _logger.LogError("SetForm/AddForm 失败，Win32 错误码 {Err}/{Err2}：{Name}", err, err2, paper.Name);
                return OperationResult.Fail(ErrorCodes.PaperSpoolerError, $"保存纸型失败（错误码 {err2}）。");
            }
        }
        return OperationResult.Ok("已保存纸型。");
    }

    private static void DeleteFormNative(IntPtr server, string name)
    {
        try { DeleteForm(server, name); } catch { /* 旧名可能不存在，忽略 */ }
    }

    private static FORM_INFO_1 BuildFormInfo(PaperType paper)
    {
        int w = MmToMicron(paper.WidthMm);
        int h = MmToMicron(paper.HeightMm);
        var m = paper.ImageableMargins ?? Margins.Zero;
        return new FORM_INFO_1
        {
            Flags = FORM_USER,
            pName = paper.Name.Trim(),
            Size = new SIZEL { cx = w, cy = h },
            ImageableArea = new RECTL
            {
                left = MmToMicron(m.LeftMm),
                top = MmToMicron(m.TopMm),
                right = w - MmToMicron(m.RightMm),
                bottom = h - MmToMicron(m.BottomMm)
            }
        };
    }

    private List<PaperType> EnumFormsInternal()
    {
        IntPtr server = IntPtr.Zero;
        var result = new List<PaperType>();
        try
        {
            if (!OpenPrinter(null, out server, IntPtr.Zero) || server == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                _logger.LogWarning("OpenPrinter(只读) 失败，Win32 错误码 {Err}", err);
                return result;
            }

            // 第一次取所需字节数
            EnumForms(server, LEVEL_1, IntPtr.Zero, 0, out uint needed, out _);
            if (needed == 0) return result;

            IntPtr buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!EnumForms(server, LEVEL_1, buffer, needed, out _, out uint returned))
                    return result;

                int structSize = Marshal.SizeOf<FORM_INFO_1>();
                for (int i = 0; i < returned; i++)
                {
                    var ptr = IntPtr.Add(buffer, i * structSize);
                    var form = Marshal.PtrToStructure<FORM_INFO_1>(ptr);
                    result.Add(ToPaperType(form));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            if (server != IntPtr.Zero) ClosePrinter(server);
        }
        return result;
    }

    private PaperType ToPaperType(FORM_INFO_1 form)
    {
        bool builtin = (form.Flags & FORM_BUILTIN) != 0;
        double widthMm = MicronToMm(form.Size.cx);
        double heightMm = MicronToMm(form.Size.cy);

        // 类别/等份以元数据为准；元数据缺失则降级为自定义、等份 1。
        var meta = _metadata.Get(form.pName ?? string.Empty);
        return new PaperType
        {
            Name = form.pName ?? string.Empty,
            Category = meta?.Category ?? PaperCategory.Custom,
            WidthMm = widthMm,
            HeightMm = heightMm,
            Parts = meta?.Parts ?? 1,
            IsBuiltIn = builtin,
            ImageableMargins = meta is null ? Margins.Zero : new Margins
            {
                TopMm = meta.TopMm, BottomMm = meta.BottomMm, LeftMm = meta.LeftMm, RightMm = meta.RightMm
            }
        };
    }

    // ===== P/Invoke =====

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZEL { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECTL { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FORM_INFO_1
    {
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)] public string pName;
        public SIZEL Size;
        public RECTL ImageableArea;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PRINTER_DEFAULTS
    {
        public IntPtr pDatatype;
        public IntPtr pDevMode;
        public uint DesiredAccess;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenPrinterW")]
    private static extern bool OpenPrinter(string? pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenPrinterW")]
    private static extern bool OpenPrinter(string? pPrinterName, out IntPtr phPrinter, ref PRINTER_DEFAULTS pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "AddFormW")]
    private static extern bool AddForm(IntPtr hPrinter, uint Level, ref FORM_INFO_1 pForm);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetFormW")]
    private static extern bool SetForm(IntPtr hPrinter, string pFormName, uint Level, ref FORM_INFO_1 pForm);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "DeleteFormW")]
    private static extern bool DeleteForm(IntPtr hPrinter, string pFormName);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "EnumFormsW")]
    private static extern bool EnumForms(IntPtr hPrinter, uint Level, IntPtr pForm, uint cbBuf, out uint pcbNeeded, out uint pcReturned);
}
