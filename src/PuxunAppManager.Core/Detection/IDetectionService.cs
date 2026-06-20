using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Detection;

/// <summary>本机检测引擎：依据检测规则与最新版本判定产品状态。</summary>
public interface IDetectionService
{
    /// <summary>
    /// 检测单个产品的本机状态。
    /// </summary>
    /// <param name="rule">检测规则。</param>
    /// <param name="latestVersion">后端最新版本，用于判定可更新/最新。</param>
    Task<DetectionResult> DetectAsync(DetectionRule rule, string latestVersion, CancellationToken ct);
}

/// <summary>检测结果。</summary>
public sealed class DetectionResult
{
    public ProductStatus Status { get; init; }
    public string? InstalledVersion { get; init; }
    public List<string> MissingFiles { get; init; } = new();
}
