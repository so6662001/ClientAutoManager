namespace PuxunAppManager.Core.PaperForms;

/// <summary>纸型类别。</summary>
public enum PaperCategory
{
    /// <summary>一式二等份。</summary>
    TwoEqual,
    /// <summary>一式三等份。</summary>
    ThreeEqual,
    /// <summary>自定义纸型。</summary>
    Custom
}

/// <summary>可打印边距（mm）。</summary>
public sealed class Margins
{
    public double TopMm { get; set; }
    public double BottomMm { get; set; }
    public double LeftMm { get; set; }
    public double RightMm { get; set; }

    public static Margins Zero => new();
}

/// <summary>纸型（本机打印服务器表单 + 等份元数据）。</summary>
public sealed class PaperType
{
    /// <summary>表单名称（机器内唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    public PaperCategory Category { get; set; }

    /// <summary>整张纸宽(mm)。</summary>
    public double WidthMm { get; set; }

    /// <summary>整张纸高(mm)。</summary>
    public double HeightMm { get; set; }

    /// <summary>等份数：2 / 3 / 1(自定义)。</summary>
    public int Parts { get; set; } = 1;

    public Margins ImageableMargins { get; set; } = Margins.Zero;

    /// <summary>系统内置表单（不可改/删）。</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>每等份高度(mm)，派生。</summary>
    public double PartHeightMm => Parts > 0 ? HeightMm / Parts : HeightMm;

    /// <summary>类别 → 等份数 的统一推导，避免不一致。</summary>
    public static int PartsOf(PaperCategory category) => category switch
    {
        PaperCategory.TwoEqual => 2,
        PaperCategory.ThreeEqual => 3,
        _ => 1
    };
}

/// <summary>判重结果种类。</summary>
public enum DuplicateKind
{
    None,
    /// <summary>名称重复。</summary>
    Name,
    /// <summary>名称不同但尺寸(类别+宽+高+等份)重复。</summary>
    Size
}
