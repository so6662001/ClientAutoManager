using System.Text.Json.Serialization;

namespace PuxunAppManager.Core.Models;

/// <summary>
/// 后端产品清单条目（管家内部统一形态）。
/// 适配层负责把公司现有后端的真实响应映射成该结构。
/// </summary>
public sealed class BackendProductManifest
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconKey")]
    public string IconKey { get; set; } = string.Empty;

    [JsonPropertyName("latestVersion")]
    public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("installer")]
    public ManifestInstaller Installer { get; set; } = new();

    [JsonPropertyName("detect")]
    public ManifestDetect Detect { get; set; } = new();
}

public sealed class ManifestInstaller
{
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("installerType")]
    public string InstallerTypeRaw { get; set; } = "msi";

    [JsonPropertyName("silentArgs")]
    public string SilentArgs { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

public sealed class ManifestDetect
{
    [JsonPropertyName("registryUninstallKey")]
    public string? RegistryUninstallKey { get; set; }

    [JsonPropertyName("versionRegistryValue")]
    public string? VersionRegistryValue { get; set; }

    [JsonPropertyName("keyFiles")]
    public List<ManifestKeyFile> KeyFiles { get; set; } = new();
}

public sealed class ManifestKeyFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}
