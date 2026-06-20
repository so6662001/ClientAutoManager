namespace PuxunAppManager.Core.Models;

/// <summary>安装包元数据（来自后端）。</summary>
public sealed class InstallerPackage
{
    public string ProductId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    /// <summary>下载地址（可带 token）。</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>用于下载后完整性校验的 SHA256（十六进制小写）。可空。</summary>
    public string? Sha256 { get; set; }

    /// <summary>安装包类型。</summary>
    public InstallerType InstallerType { get; set; } = InstallerType.Msi;

    /// <summary>
    /// 静默安装参数模板。可包含 {file} 占位符，运行时替换为本地包路径。
    /// MSI 示例：/i "{file}" /qn /norestart
    /// EXE 示例：/S
    /// </summary>
    public string SilentArgs { get; set; } = string.Empty;

    /// <summary>安装包字节大小（用于展示与磁盘空间预检）。</summary>
    public long SizeBytes { get; set; }
}
