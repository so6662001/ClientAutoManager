using PuxunAppManager.Core.Models;
using PuxunAppManager.Core.PaperForms;

namespace PuxunAppManager.Tests;

public class PaperTypeTests
{
    [Theory]
    [InlineData(PaperCategory.TwoEqual, 2)]
    [InlineData(PaperCategory.ThreeEqual, 3)]
    [InlineData(PaperCategory.Custom, 1)]
    public void PartsOf_MapsCategory(PaperCategory cat, int expected)
        => Assert.Equal(expected, PaperType.PartsOf(cat));

    [Fact]
    public void PartHeight_TwoEqual()
    {
        var p = new PaperType { Category = PaperCategory.TwoEqual, HeightMm = 140, Parts = 2 };
        Assert.Equal(70, p.PartHeightMm, 3);
    }

    [Fact]
    public void PartHeight_ThreeEqual()
    {
        var p = new PaperType { Category = PaperCategory.ThreeEqual, HeightMm = 280, Parts = 3 };
        Assert.Equal(93.333, p.PartHeightMm, 2);
    }
}

public class PaperValidationTests
{
    private static PaperType Valid() => new()
    {
        Name = "测试纸型", Category = PaperCategory.Custom, WidthMm = 190, HeightMm = 120, Parts = 1
    };

    [Fact]
    public void Valid_Passes()
        => Assert.True(PaperValidation.Validate(Valid()).Success);

    [Fact]
    public void EmptyName_Fails()
    {
        var p = Valid(); p.Name = "  ";
        Assert.False(PaperValidation.Validate(p).Success);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("x:y")]
    [InlineData("q*?")]
    public void InvalidChars_Fail(string name)
    {
        var p = Valid(); p.Name = name;
        var r = PaperValidation.Validate(p);
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.PaperInvalid, r.ErrorCode);
    }

    [Fact]
    public void NonPositiveSize_Fails()
    {
        var p = Valid(); p.WidthMm = 0;
        Assert.False(PaperValidation.Validate(p).Success);
    }

    [Fact]
    public void OversizeDimension_Fails()
    {
        var p = Valid(); p.HeightMm = 5000;
        Assert.False(PaperValidation.Validate(p).Success);
    }

    [Fact]
    public void PartsMismatch_Fails()
    {
        var p = Valid(); p.Category = PaperCategory.TwoEqual; p.Parts = 1; // 应为 2
        Assert.False(PaperValidation.Validate(p).Success);
    }

    [Fact]
    public void MarginsExceed_Fails()
    {
        var p = Valid();
        p.ImageableMargins = new Margins { TopMm = 70, BottomMm = 70 }; // 和 = 高
        Assert.False(PaperValidation.Validate(p).Success);
    }
}
