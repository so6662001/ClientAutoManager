using System.Security.Cryptography;

namespace PuxunAppManager.Core.Detection;

/// <summary>基于真实文件系统的 IFileProbe 实现。</summary>
public sealed class FileProbe : IFileProbe
{
    public string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        // 同时支持 %VAR% 形式
        return Environment.ExpandEnvironmentVariables(path);
    }

    public bool FileExists(string expandedPath) =>
        !string.IsNullOrEmpty(expandedPath) && File.Exists(expandedPath);

    public async Task<string?> ComputeSha256Async(string expandedPath, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(expandedPath)) return null;
            await using var stream = new FileStream(
                expandedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 1 << 20, useAsync: true);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
