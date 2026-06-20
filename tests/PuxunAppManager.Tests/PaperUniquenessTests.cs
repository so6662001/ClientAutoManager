using PuxunAppManager.Core.PaperForms;

namespace PuxunAppManager.Tests;

public class PaperUniquenessTests
{
    private static PaperType Make(string name, PaperCategory cat, double w, double h, int parts) =>
        new() { Name = name, Category = cat, WidthMm = w, HeightMm = h, Parts = parts };

    private static List<PaperType> Existing() => new()
    {
        Make("收银小票二联", PaperCategory.TwoEqual, 210, 140, 2),
        Make("增值税三联", PaperCategory.ThreeEqual, 241, 280, 3),
    };

    [Fact]
    public void NoDuplicate_WhenNameAndSizeUnique()
    {
        var candidate = Make("新出库单", PaperCategory.Custom, 190, 120, 1);
        var (kind, _) = PaperUniqueness.Check(candidate, Existing(), null);
        Assert.Equal(DuplicateKind.None, kind);
    }

    [Fact]
    public void NameDuplicate_DetectedFirst_CaseAndSpaceInsensitive()
    {
        var candidate = Make("  收银小票二联 ", PaperCategory.Custom, 99, 99, 1);
        var (kind, conflict) = PaperUniqueness.Check(candidate, Existing(), null);
        Assert.Equal(DuplicateKind.Name, kind);
        Assert.Equal("收银小票二联", conflict!.Name);
    }

    [Fact]
    public void SizeDuplicate_WhenNameDiffersButDimensionsMatch()
    {
        var candidate = Make("另一个二联", PaperCategory.TwoEqual, 210, 140, 2);
        var (kind, conflict) = PaperUniqueness.Check(candidate, Existing(), null);
        Assert.Equal(DuplicateKind.Size, kind);
        Assert.Equal("收银小票二联", conflict!.Name);
    }

    [Fact]
    public void SizeNotDuplicate_WhenPartsDiffer()
    {
        var candidate = Make("三联同尺寸", PaperCategory.ThreeEqual, 210, 140, 3);
        var (kind, _) = PaperUniqueness.Check(candidate, Existing(), null);
        Assert.Equal(DuplicateKind.None, kind);
    }

    [Fact]
    public void Editing_ExcludesItself_AllowsSameNameAndSize()
    {
        var candidate = Make("收银小票二联", PaperCategory.TwoEqual, 210, 140, 2);
        var (kind, _) = PaperUniqueness.Check(candidate, Existing(), editingOriginalName: "收银小票二联");
        Assert.Equal(DuplicateKind.None, kind);
    }

    [Fact]
    public void Editing_StillBlocksCollisionWithOtherItem()
    {
        // 把"增值税三联"改名为已存在的"收银小票二联"
        var candidate = Make("收银小票二联", PaperCategory.ThreeEqual, 241, 280, 3);
        var (kind, _) = PaperUniqueness.Check(candidate, Existing(), editingOriginalName: "增值税三联");
        Assert.Equal(DuplicateKind.Name, kind);
    }

    [Fact]
    public void SizeDuplicate_WithinTolerance()
    {
        var candidate = Make("微差二联", PaperCategory.TwoEqual, 210.02, 139.98, 2);
        var (kind, _) = PaperUniqueness.Check(candidate, Existing(), null);
        Assert.Equal(DuplicateKind.Size, kind);
    }
}
