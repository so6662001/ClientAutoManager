using PuxunAppManager.Core.Detection;

namespace PuxunAppManager.Tests;

public class VersionComparerTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0", "1.0.0", 0)]          // 位数不一致补零相等
    [InlineData("1.0.0.0", "1.0", 0)]
    [InlineData("1.0.0", "1.2.0", -1)]       // 较旧
    [InlineData("2.1.0", "2.0.9", 1)]        // 较新
    [InlineData("1.10.0", "1.9.0", 1)]       // 数值比较而非字符串
    [InlineData("v2.0", "2.0", 0)]           // 去前缀 v
    [InlineData("1.0.0-beta", "1.0.0", 0)]   // 忽略预发布元数据
    public void Compare_Works(string a, string b, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(VersionComparer.Compare(a, b)));
    }

    [Fact]
    public void IsOlder_DetectsUpdate()
    {
        Assert.True(VersionComparer.IsOlder("1.0.0", "1.2.0"));
        Assert.False(VersionComparer.IsOlder("1.2.0", "1.2.0"));
        Assert.False(VersionComparer.IsOlder("1.3.0", "1.2.0"));
    }

    [Fact]
    public void EmptyOrNull_TreatedAsZero()
    {
        Assert.Equal(0, VersionComparer.Compare(null, ""));
        Assert.True(VersionComparer.IsOlder("", "1.0.0"));
    }
}
