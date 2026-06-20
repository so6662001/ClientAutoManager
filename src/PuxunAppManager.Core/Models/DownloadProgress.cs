namespace PuxunAppManager.Core.Models;

/// <summary>下载进度回调载体。</summary>
public readonly struct DownloadProgress
{
    public DownloadProgress(long bytesReceived, long totalBytes)
    {
        BytesReceived = bytesReceived;
        TotalBytes = totalBytes;
    }

    public long BytesReceived { get; }

    /// <summary>总大小；-1 表示未知（服务器未返回 Content-Length）。</summary>
    public long TotalBytes { get; }

    public double Percent => TotalBytes > 0
        ? Math.Clamp((double)BytesReceived / TotalBytes * 100d, 0d, 100d)
        : 0d;
}

/// <summary>安装流水线阶段，用于 UI 进度态文案。</summary>
public enum InstallPhase
{
    Preparing,
    Downloading,
    Verifying,
    Installing,
    PostVerifying,
    Completed,
    Failed
}

/// <summary>安装流水线进度回调载体。</summary>
public sealed class InstallProgress
{
    public InstallPhase Phase { get; init; }
    public double Percent { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool SignatureVerified { get; init; }
    public bool HashVerified { get; init; }
}
