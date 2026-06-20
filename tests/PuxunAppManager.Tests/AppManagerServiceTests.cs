using Microsoft.Extensions.Logging.Abstractions;
using PuxunAppManager.Core.Detection;
using PuxunAppManager.Core.Installation;
using PuxunAppManager.Core.Models;
using PuxunAppManager.Core.Services;

namespace PuxunAppManager.Tests;

public class AppManagerServiceTests
{
    private sealed class NoopInstaller : IInstallerService
    {
        public Task<OperationResult> ExecuteAsync(ProductInfo product, OperationType operation,
            IProgress<InstallProgress> progress, CancellationToken ct)
            => Task.FromResult(OperationResult.Ok());
    }

    private static BackendProductManifest SampleManifest() => new()
    {
        ProductId = "clientA",
        DisplayName = "客户端A",
        LatestVersion = "1.0.0",
        Detect = new ManifestDetect { RegistryUninstallKey = @"HKLM\X", VersionRegistryValue = "DisplayVersion" }
    };

    private static AppManagerService Build(FakeBackendClient backend, FakeManifestCache cache,
        FakeRegistryReader reg, FakeFileProbe files)
    {
        var detection = new DetectionService(reg, files, NullLogger<DetectionService>.Instance);
        return new AppManagerService(backend, cache, detection, new NoopInstaller(),
            NullLogger<AppManagerService>.Instance);
    }

    [Fact]
    public async Task Scan_Online_ReturnsProductsAndCachesManifest()
    {
        var backend = new FakeBackendClient { Manifest = new() { SampleManifest() } };
        var cache = new FakeManifestCache();
        var svc = Build(backend, cache, new FakeRegistryReader(), new FakeFileProbe());

        var result = await svc.ScanAsync(CancellationToken.None);

        Assert.Equal(BackendConnectionState.Online, result.ConnectionState);
        Assert.Single(result.Products);
        Assert.Equal(ProductStatus.NotInstalled, result.Products[0].Status);
        // 已写入缓存
        Assert.NotNull(await cache.LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Scan_BackendDown_FallsBackToCache()
    {
        var backend = new FakeBackendClient { ThrowOnManifest = true };
        var cache = new FakeManifestCache();
        cache.Seed(new List<BackendProductManifest> { SampleManifest() });

        var svc = Build(backend, cache, new FakeRegistryReader(), new FakeFileProbe());
        var result = await svc.ScanAsync(CancellationToken.None);

        Assert.Equal(BackendConnectionState.Cached, result.ConnectionState);
        Assert.Single(result.Products);
    }

    [Fact]
    public async Task Scan_BackendDownNoCache_ReturnsOfflineWithError()
    {
        var backend = new FakeBackendClient { ThrowOnManifest = true };
        var cache = new FakeManifestCache(); // 无缓存

        var svc = Build(backend, cache, new FakeRegistryReader(), new FakeFileProbe());
        var result = await svc.ScanAsync(CancellationToken.None);

        Assert.Equal(BackendConnectionState.Offline, result.ConnectionState);
        Assert.Empty(result.Products);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }
}
