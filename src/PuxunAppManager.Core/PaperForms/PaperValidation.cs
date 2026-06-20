using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.PaperForms;

/// <summary>纸型输入校验纯函数（边界条件 1/2/3/11）。</summary>
public static class PaperValidation
{
    public const double MaxDimensionMm = 2000d;
    private static readonly char[] InvalidNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    public static OperationResult Validate(PaperType p)
    {
        if (p is null) return OperationResult.Fail(ErrorCodes.PaperInvalid, "纸型数据为空。");

        // 名称
        if (string.IsNullOrWhiteSpace(p.Name))
            return OperationResult.Fail(ErrorCodes.PaperInvalid, "请输入纸型名称。");
        if (p.Name.IndexOfAny(InvalidNameChars) >= 0)
            return OperationResult.Fail(ErrorCodes.PaperInvalid, "纸型名称不能包含 \\ / : * ? \" < > | 等字符。");

        // 等份数与类别一致性
        var expectedParts = PaperType.PartsOf(p.Category);
        if (p.Parts != expectedParts)
            return OperationResult.Fail(ErrorCodes.PaperInvalid, "等份数与纸型类别不一致。");

        // 尺寸
        if (p.WidthMm <= 0 || p.HeightMm <= 0)
            return OperationResult.Fail(ErrorCodes.PaperInvalid, "纸张宽和高必须大于 0。");
        if (p.WidthMm > MaxDimensionMm || p.HeightMm > MaxDimensionMm)
            return OperationResult.Fail(ErrorCodes.PaperInvalid, $"纸张尺寸不能超过 {MaxDimensionMm:0} mm。");

        // 边距（可打印区域合法性）
        var m = p.ImageableMargins ?? Margins.Zero;
        if (m.TopMm < 0 || m.BottomMm < 0 || m.LeftMm < 0 || m.RightMm < 0)
            return OperationResult.Fail(ErrorCodes.PaperInvalid, "边距不能为负数。");
        if (m.TopMm + m.BottomMm >= p.HeightMm)
            return OperationResult.Fail(ErrorCodes.PaperInvalid, "上下边距之和不能大于等于纸张高度。");
        if (m.LeftMm + m.RightMm >= p.WidthMm)
            return OperationResult.Fail(ErrorCodes.PaperInvalid, "左右边距之和不能大于等于纸张宽度。");

        return OperationResult.Ok();
    }
}
