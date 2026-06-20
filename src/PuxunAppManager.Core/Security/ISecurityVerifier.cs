namespace PuxunAppManager.Core.Security;

/// <summary>安装包安全校验：哈希 + Authenticode 数字签名。</summary>
public interface ISecurityVerifier
{
    /// <summary>计算文件 SHA256（十六进制小写）。</summary>
    Task<string> ComputeSha256Async(string filePath, CancellationToken ct);

    /// <summary>校验文件 SHA256 是否与期望一致。expected 为空时跳过(返回 true)，由调用方决定是否强制。</summary>
    Task<bool> VerifyHashAsync(string filePath, string? expectedSha256, CancellationToken ct);

    /// <summary>
    /// 校验文件 Authenticode 数字签名：签名存在、证书链可信、未吊销。
    /// 非 Windows 平台返回 NotSupported（生产仅在 Windows 安装）。
    /// </summary>
    SignatureVerificationResult VerifySignature(string filePath);
}

public sealed class SignatureVerificationResult
{
    public bool IsValid { get; init; }
    public string? SignerSubject { get; init; }
    public string? Thumbprint { get; init; }
    public string? FailureReason { get; init; }

    public static SignatureVerificationResult Valid(string subject, string thumbprint) =>
        new() { IsValid = true, SignerSubject = subject, Thumbprint = thumbprint };

    public static SignatureVerificationResult Invalid(string reason) =>
        new() { IsValid = false, FailureReason = reason };
}
