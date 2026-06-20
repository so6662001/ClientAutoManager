using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Services;

/// <summary>管家业务编排：检测流程 + 操作执行。供 ViewModel 调用。</summary>
public interface IAppManagerService
{
    /// <summary>执行完整检测流程：拉清单(或缓存降级) → 逐产品本机检测 → 合并产品列表。</summary>
    Task<DetectionScanResult> ScanAsync(CancellationToken ct);

    /// <summary>对单个产品执行操作，并在成功后返回复检后的最新 ProductInfo。</summary>
    Task<(OperationResult result, ProductInfo updated)> ExecuteOperationAsync(
        ProductInfo product, OperationType operation, IProgress<InstallProgress> progress, CancellationToken ct);

    /// <summary>仅重新检测单个产品（不安装）。</summary>
    Task<ProductInfo> RedetectAsync(ProductInfo product, CancellationToken ct);
}

/// <summary>检测流程结果。</summary>
public sealed class DetectionScanResult
{
    public IReadOnlyList<ProductInfo> Products { get; init; } = Array.Empty<ProductInfo>();
    public BackendConnectionState ConnectionState { get; init; }
    public DateTimeOffset ScannedAt { get; init; } = DateTimeOffset.Now;
    public string? Error { get; init; }
}
