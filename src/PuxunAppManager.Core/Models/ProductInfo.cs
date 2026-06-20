namespace PuxunAppManager.Core.Models;

/// <summary>
/// 产品信息：合并了后端清单（最新版本/描述/安装包）与本机检测结果（已装版本/状态）。
/// 这是管家内部统一模型，UI 直接绑定。
/// </summary>
public sealed class ProductInfo
{
    /// <summary>唯一标识，关联后端与检测规则。</summary>
    public string ProductId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>UI 图标键（如 A/B/C，用于卡片色块）。</summary>
    public string IconKey { get; set; } = string.Empty;

    /// <summary>本机已安装版本；null 表示未安装。</summary>
    public string? InstalledVersion { get; set; }

    /// <summary>后端返回的最新版本。</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>安装包字节大小。</summary>
    public long InstallerSizeBytes { get; set; }

    /// <summary>计算得到的状态。</summary>
    public ProductStatus Status { get; set; } = ProductStatus.Unknown;

    /// <summary>当 Status==Broken 时，列出缺失/损坏的文件。</summary>
    public List<string> MissingFiles { get; set; } = new();

    /// <summary>已安装产物的数字签名是否已验证（展示用）。</summary>
    public bool SignatureVerified { get; set; }

    /// <summary>检测规则（执行检测与安装后复检时使用）。</summary>
    public DetectionRule DetectionRule { get; set; } = new();

    /// <summary>该产品的安装包信息（最新版本对应）。</summary>
    public InstallerPackage? Installer { get; set; }
}
