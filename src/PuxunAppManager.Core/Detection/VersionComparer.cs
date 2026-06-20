namespace PuxunAppManager.Core.Detection;

/// <summary>
/// 版本比较：兼容 1.0 / 1.0.0 / 1.0.0.0 等位数不一致的情况。
/// 缺失的段按 0 补齐；非数字段安全降级（按字符串比较该段）。
/// </summary>
public static class VersionComparer
{
    /// <summary>比较两个版本号。返回 &lt;0 表示 a 较旧，0 相等，&gt;0 表示 a 较新。</summary>
    public static int Compare(string? a, string? b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        int len = Math.Max(pa.Length, pb.Length);
        for (int i = 0; i < len; i++)
        {
            long va = i < pa.Length ? pa[i] : 0;
            long vb = i < pb.Length ? pb[i] : 0;
            if (va != vb) return va < vb ? -1 : 1;
        }
        return 0;
    }

    /// <summary>a 是否比 b 旧（存在可更新版本）。</summary>
    public static bool IsOlder(string? a, string? b) => Compare(a, b) < 0;

    public static bool IsEqual(string? a, string? b) => Compare(a, b) == 0;

    private static long[] Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return Array.Empty<long>();

        // 去除前缀 v / V，截掉预发布/构建元数据（- 或 + 后内容）
        var v = version.Trim().TrimStart('v', 'V');
        int cut = v.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0) v = v[..cut];

        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new long[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            // 容错：非纯数字段取其前导数字，否则按 0
            result[i] = ExtractLeadingNumber(parts[i]);
        }
        return result;
    }

    private static long ExtractLeadingNumber(string segment)
    {
        int i = 0;
        while (i < segment.Length && char.IsDigit(segment[i])) i++;
        if (i == 0) return 0;
        return long.TryParse(segment[..i], out var n) ? n : 0;
    }
}
