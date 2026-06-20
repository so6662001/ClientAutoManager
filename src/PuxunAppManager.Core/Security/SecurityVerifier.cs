using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace PuxunAppManager.Core.Security;

/// <summary>
/// 安全校验实现。
/// - 哈希：SHA256。
/// - 签名：通过 Authenticode 证书 + 证书链构建校验信任与吊销状态。
///   说明：生产环境建议进一步使用 WinVerifyTrust 进行完整的 Authenticode 策略校验；
///   此处使用 X509 链构建作为可移植实现，已覆盖"签名存在/链可信/吊销"核心判定。
/// </summary>
public sealed class SecurityVerifier : ISecurityVerifier
{
    private readonly ILogger<SecurityVerifier> _logger;

    public SecurityVerifier(ILogger<SecurityVerifier> logger) => _logger = logger;

    public async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 20, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<bool> VerifyHashAsync(string filePath, string? expectedSha256, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            // 清单未提供哈希：不阻断，由调用方根据策略决定（默认仍要求签名校验）。
            _logger.LogWarning("清单未提供 SHA256，跳过哈希校验：{File}", filePath);
            return true;
        }

        var actual = await ComputeSha256Async(filePath, ct).ConfigureAwait(false);
        bool match = string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
        if (!match)
            _logger.LogError("哈希校验失败。期望 {Expected}，实际 {Actual}", expectedSha256, actual);
        return match;
    }

    public SignatureVerificationResult VerifySignature(string filePath)
    {
        if (!OperatingSystem.IsWindows())
            return SignatureVerificationResult.Invalid("当前平台不支持 Authenticode 校验（仅 Windows）。");

        try
        {
            // 从已签名文件提取签名者证书；未签名将抛异常。
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));

            using var chain = new X509Chain
            {
                ChainPolicy =
                {
                    RevocationMode = X509RevocationMode.Online,
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    VerificationFlags = X509VerificationFlags.NoFlag,
                    UrlRetrievalTimeout = TimeSpan.FromSeconds(15)
                }
            };

            bool built = chain.Build(cert);
            if (!built)
            {
                var reasons = chain.ChainStatus.Length > 0
                    ? string.Join("; ", chain.ChainStatus.Select(s => s.StatusInformation.Trim()))
                    : "证书链构建失败";
                _logger.LogError("签名证书链校验失败：{Reasons}", reasons);
                return SignatureVerificationResult.Invalid($"证书链不可信：{reasons}");
            }

            return SignatureVerificationResult.Valid(cert.Subject, cert.Thumbprint);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "文件未签名或签名无法读取：{File}", filePath);
            return SignatureVerificationResult.Invalid("文件未签名或签名无效。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "签名校验异常：{File}", filePath);
            return SignatureVerificationResult.Invalid($"签名校验异常：{ex.Message}");
        }
    }
}
