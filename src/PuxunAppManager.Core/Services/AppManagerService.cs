using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Backend;
using PuxunAppManager.Core.Detection;
using PuxunAppManager.Core.Installation;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Services;

public sealed class AppManagerService : IAppManagerService
{
    private readonly IBackendClient _backend;
    private readonly IManifestCache _cache;
    private readonly IDetectionService _detection;
    private readonly IInstallerService _installer;
    private readonly ILogger<AppManagerService> _logger;

    public AppManagerService(
        IBackendClient backend,
        IManifestCache cache,
        IDetectionService detection,
        IInstallerService installer,
        ILogger<AppManagerService> logger)
    {
        _backend = backend;
        _cache = cache;
        _detection = detection;
        _installer = installer;
        _logger = logger;
    }

    public async Task<DetectionScanResult> ScanAsync(CancellationToken ct)
    {
        IReadOnlyList<BackendProductManifest>? manifest = null;
        var state = BackendConnectionState.Online;

        // 1. 拉取清单；失败则降级用缓存
        try
        {
            manifest = await _backend.GetManifestAsync(ct).ConfigureAwait(false);
            await _cache.SaveAsync(manifest, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "后端不可达，尝试使用缓存清单降级。");
            manifest = await _cache.LoadAsync(ct).ConfigureAwait(false);
            state = manifest is null ? BackendConnectionState.Offline : BackendConnectionState.Cached;
        }

        if (manifest is null || manifest.Count == 0)
        {
            return new DetectionScanResult
            {
                Products = Array.Empty<ProductInfo>(),
                ConnectionState = BackendConnectionState.Offline,
                Error = "无法连接更新服务器，且本地无缓存清单。请检查网络后点击重新检测。"
            };
        }

        // 2. 逐产品本机检测（并行）
        var tasks = manifest.Select(async m =>
        {
            var product = ManifestMapper.ToProductInfo(m);
            await PopulateDetectionAsync(product, ct).ConfigureAwait(false);
            return product;
        });

        var products = await Task.WhenAll(tasks).ConfigureAwait(false);

        return new DetectionScanResult
        {
            Products = products,
            ConnectionState = state,
            ScannedAt = DateTimeOffset.Now
        };
    }

    public async Task<ProductInfo> RedetectAsync(ProductInfo product, CancellationToken ct)
    {
        await PopulateDetectionAsync(product, ct).ConfigureAwait(false);
        return product;
    }

    public async Task<(OperationResult result, ProductInfo updated)> ExecuteOperationAsync(
        ProductInfo product, OperationType operation, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var result = await _installer.ExecuteAsync(product, operation, progress, ct).ConfigureAwait(false);

        // 无论成功失败都复检，刷新该产品状态
        try
        {
            await PopulateDetectionAsync(product, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "操作后复检失败：{Product}", product.ProductId);
        }

        return (result, product);
    }

    private async Task PopulateDetectionAsync(ProductInfo product, CancellationToken ct)
    {
        var d = await _detection.DetectAsync(product.DetectionRule, product.LatestVersion, ct).ConfigureAwait(false);
        product.Status = d.Status;
        product.InstalledVersion = d.InstalledVersion;
        product.MissingFiles = d.MissingFiles;
        product.SignatureVerified = d.Status is ProductStatus.UpToDate or ProductStatus.UpdateAvailable;
    }
}
