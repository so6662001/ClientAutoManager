namespace PuxunAppManager.Core.PaperForms;

/// <summary>
/// 纸型判重纯函数（无副作用，便于单元测试）。
/// 规则（已确认）：先查名称，再查名称 + 尺寸（类别 + 宽 + 高 + 等份数）。
/// 编辑时通过 editingOriginalName 排除自身。
/// </summary>
public static class PaperUniqueness
{
    private const double Tolerance = 0.05; // mm 容差，吸收换算取整误差

    public static (DuplicateKind kind, PaperType? conflict) Check(
        PaperType candidate, IReadOnlyList<PaperType> existing, string? editingOriginalName)
    {
        var pool = editingOriginalName is null
            ? existing
            : existing.Where(p => !NameEquals(p.Name, editingOriginalName)).ToList();

        // 1) 先查名称
        var byName = pool.FirstOrDefault(p => NameEquals(p.Name, candidate.Name));
        if (byName is not null) return (DuplicateKind.Name, byName);

        // 2) 再查尺寸（类别 + 宽 + 高 + 等份数）
        var bySize = pool.FirstOrDefault(p =>
            p.Category == candidate.Category &&
            NearlyEqual(p.WidthMm, candidate.WidthMm) &&
            NearlyEqual(p.HeightMm, candidate.HeightMm) &&
            p.Parts == candidate.Parts);
        if (bySize is not null) return (DuplicateKind.Size, bySize);

        return (DuplicateKind.None, null);
    }

    public static bool NameEquals(string a, string b) =>
        string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < Tolerance;
}
