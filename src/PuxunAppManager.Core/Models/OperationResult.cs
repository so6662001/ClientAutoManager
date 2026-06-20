namespace PuxunAppManager.Core.Models;

/// <summary>统一操作结果。所有外部调用（网络/文件/进程）失败都转为该结果，不让异常冒泡。</summary>
public sealed class OperationResult
{
    public bool Success { get; init; }

    /// <summary>稳定错误码，便于 UI 分支与日志统计。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>面向用户的可读消息。</summary>
    public string? Message { get; init; }

    public static OperationResult Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static OperationResult Fail(string errorCode, string message) =>
        new() { Success = false, ErrorCode = errorCode, Message = message };
}

/// <summary>常用错误码常量。</summary>
public static class ErrorCodes
{
    public const string BackendUnreachable = "BACKEND_UNREACHABLE";
    public const string NoManifest = "NO_MANIFEST";
    public const string DownloadFailed = "DOWNLOAD_FAILED";
    public const string HashMismatch = "HASH_MISMATCH";
    public const string SignatureInvalid = "SIGNATURE_INVALID";
    public const string UacCancelled = "UAC_CANCELLED";
    public const string InstallExitNonZero = "INSTALL_EXIT_NONZERO";
    public const string PostInstallVerifyFailed = "POST_INSTALL_VERIFY_FAILED";
    public const string DiskSpaceInsufficient = "DISK_SPACE_INSUFFICIENT";
    public const string InstallerMissing = "INSTALLER_MISSING";
    public const string AlreadyRunning = "ALREADY_RUNNING";
    public const string Cancelled = "CANCELLED";
    public const string Unknown = "UNKNOWN";
}
