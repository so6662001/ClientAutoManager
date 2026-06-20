using Microsoft.Extensions.Logging.Abstractions;
using PuxunAppManager.Core.Detection;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Tests;

public class DetectionServiceTests
{
    private const string UninstallKey = @"HKLM\SOFTWARE\...\Uninstall\{X}";
    private const string ExePath = @"C:\Program Files\App\App.exe";

    private static DetectionService Create(FakeRegistryReader reg, FakeFileProbe files)
        => new(reg, files, NullLogger<DetectionService>.Instance);

    private static DetectionRule Rule(string? hash = null) => new()
    {
        RegistryUninstallKey = UninstallKey,
        VersionRegistryValue = "DisplayVersion",
        KeyFiles = { new KeyFile { Path = ExePath, Sha256 = hash } }
    };

    [Fact]
    public async Task NotInstalled_When_NoKeyAndNoFiles()
    {
        var reg = new FakeRegistryReader();
        var files = new FakeFileProbe();
        var result = await Create(reg, files).DetectAsync(Rule(), "1.0.0", CancellationToken.None);
        Assert.Equal(ProductStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task Broken_When_KeyExistsButFileMissing()
    {
        var reg = new FakeRegistryReader { ExistingKeys = { UninstallKey } };
        reg.Values[$"{UninstallKey}::DisplayVersion"] = "1.0.0";
        var files = new FakeFileProbe(); // 文件缺失
        var result = await Create(reg, files).DetectAsync(Rule(), "1.0.0", CancellationToken.None);
        Assert.Equal(ProductStatus.Broken, result.Status);
        Assert.Contains(ExePath, result.MissingFiles);
    }

    [Fact]
    public async Task UpToDate_When_HealthyAndSameVersion()
    {
        var reg = new FakeRegistryReader { ExistingKeys = { UninstallKey } };
        reg.Values[$"{UninstallKey}::DisplayVersion"] = "2.1.0";
        var files = new FakeFileProbe { Files = { [ExePath] = null } };
        var result = await Create(reg, files).DetectAsync(Rule(), "2.1.0", CancellationToken.None);
        Assert.Equal(ProductStatus.UpToDate, result.Status);
        Assert.Equal("2.1.0", result.InstalledVersion);
    }

    [Fact]
    public async Task UpdateAvailable_When_OlderVersion()
    {
        var reg = new FakeRegistryReader { ExistingKeys = { UninstallKey } };
        reg.Values[$"{UninstallKey}::DisplayVersion"] = "1.0.0";
        var files = new FakeFileProbe { Files = { [ExePath] = null } };
        var result = await Create(reg, files).DetectAsync(Rule(), "1.2.0", CancellationToken.None);
        Assert.Equal(ProductStatus.UpdateAvailable, result.Status);
    }

    [Fact]
    public async Task Broken_When_HashMismatch()
    {
        var reg = new FakeRegistryReader { ExistingKeys = { UninstallKey } };
        reg.Values[$"{UninstallKey}::DisplayVersion"] = "1.0.0";
        var files = new FakeFileProbe { Files = { [ExePath] = "actualhash" } };
        var result = await Create(reg, files).DetectAsync(Rule(hash: "expectedhash"), "1.0.0", CancellationToken.None);
        Assert.Equal(ProductStatus.Broken, result.Status);
    }

    [Fact]
    public async Task UpToDate_When_KeyExistsButVersionUnknown()
    {
        var reg = new FakeRegistryReader { ExistingKeys = { UninstallKey } };
        // 不设置 DisplayVersion 值 → 版本读不到
        var files = new FakeFileProbe { Files = { [ExePath] = null } };
        var result = await Create(reg, files).DetectAsync(Rule(), "1.0.0", CancellationToken.None);
        Assert.Equal(ProductStatus.UpToDate, result.Status);
        Assert.Null(result.InstalledVersion);
    }
}
