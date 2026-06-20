using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Backend;

/// <summary>把后端清单 DTO 映射为管家内部统一模型。</summary>
public static class ManifestMapper
{
    public static InstallerType ParseInstallerType(string? raw) =>
        string.Equals(raw, "exe", StringComparison.OrdinalIgnoreCase)
            ? InstallerType.Exe
            : InstallerType.Msi;

    public static InstallerPackage ToInstallerPackage(string productId, string version, ManifestInstaller dto) => new()
    {
        ProductId = productId,
        Version = version,
        DownloadUrl = dto.DownloadUrl,
        Sha256 = string.IsNullOrWhiteSpace(dto.Sha256) ? null : dto.Sha256,
        InstallerType = ParseInstallerType(dto.InstallerTypeRaw),
        SilentArgs = dto.SilentArgs,
        SizeBytes = dto.SizeBytes
    };

    public static DetectionRule ToDetectionRule(ManifestDetect dto) => new()
    {
        RegistryUninstallKey = dto.RegistryUninstallKey,
        VersionRegistryValue = dto.VersionRegistryValue,
        KeyFiles = dto.KeyFiles.Select(k => new KeyFile { Path = k.Path, Sha256 = k.Sha256 }).ToList()
    };

    /// <summary>由清单条目构建初始 ProductInfo（状态待检测填充）。</summary>
    public static ProductInfo ToProductInfo(BackendProductManifest m) => new()
    {
        ProductId = m.ProductId,
        DisplayName = m.DisplayName,
        Description = m.Description,
        IconKey = string.IsNullOrWhiteSpace(m.IconKey)
            ? (m.DisplayName.Length > 0 ? m.DisplayName[..1].ToUpperInvariant() : "?")
            : m.IconKey,
        LatestVersion = m.LatestVersion,
        InstallerSizeBytes = m.Installer.SizeBytes,
        Status = ProductStatus.Unknown,
        DetectionRule = ToDetectionRule(m.Detect),
        Installer = ToInstallerPackage(m.ProductId, m.LatestVersion, m.Installer)
    };
}
