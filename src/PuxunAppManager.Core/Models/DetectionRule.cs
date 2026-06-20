namespace PuxunAppManager.Core.Models;

/// <summary>
/// 单个产品的本机检测规则，来自后端清单 / 本地配置。
/// 多重判据：注册表卸载项 + 版本注册表值 + 关键文件(可带哈希)。
/// </summary>
public sealed class DetectionRule
{
    /// <summary>HKLM 卸载项路径，例如 SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{GUID}。</summary>
    public string? RegistryUninstallKey { get; set; }

    /// <summary>读取版本号的注册表值名，例如 DisplayVersion。</summary>
    public string? VersionRegistryValue { get; set; }

    /// <summary>关键文件列表，可选携带期望 SHA256 用于完整性校验。</summary>
    public List<KeyFile> KeyFiles { get; set; } = new();
}

/// <summary>关键文件及其期望哈希。</summary>
public sealed class KeyFile
{
    /// <summary>文件路径，支持环境变量（如 %ProgramFiles%）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>期望 SHA256（十六进制小写）。为空表示只校验存在性。</summary>
    public string? Sha256 { get; set; }
}
