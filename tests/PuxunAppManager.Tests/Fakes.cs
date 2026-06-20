using PuxunAppManager.Core.Backend;
using PuxunAppManager.Core.Detection;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Tests;

/// <summary>可配置的假注册表读取器。</summary>
public sealed class FakeRegistryReader : IRegistryReader
{
    public HashSet<string> ExistingKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool UninstallKeyExists(string uninstallKeyPath) => ExistingKeys.Contains(uninstallKeyPath);

    public string? ReadUninstallValue(string uninstallKeyPath, string valueName)
        => Values.TryGetValue($"{uninstallKeyPath}::{valueName}", out var v) ? v : null;
}

/// <summary>可配置的假文件探测器（按虚拟路径）。</summary>
public sealed class FakeFileProbe : IFileProbe
{
    // path -> sha256(null 表示存在但无哈希要求)
    public Dictionary<string, string?> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string ExpandPath(string path) => path;
    public bool FileExists(string expandedPath) => Files.ContainsKey(expandedPath);

    public Task<string?> ComputeSha256Async(string expandedPath, CancellationToken ct)
        => Task.FromResult(Files.TryGetValue(expandedPath, out var h) ? h : null);
}

/// <summary>可配置的假后端客户端。</summary>
public sealed class FakeBackendClient : IBackendClient
{
    public List<BackendProductManifest>? Manifest { get; set; }
    public bool ThrowOnManifest { get; set; }
    public Func<InstallerPackage>? InstallerFactory { get; set; }
    public string DownloadResultPath { get; set; } = "C:/tmp/pkg.msi";

    public Task<IReadOnlyList<BackendProductManifest>> GetManifestAsync(CancellationToken ct)
    {
        if (ThrowOnManifest) throw new HttpRequestException("backend down");
        return Task.FromResult<IReadOnlyList<BackendProductManifest>>(Manifest ?? new());
    }

    public Task<InstallerPackage> GetInstallerAsync(string productId, string version, CancellationToken ct)
        => Task.FromResult(InstallerFactory?.Invoke() ?? new InstallerPackage { ProductId = productId, Version = version });

    public Task<string> DownloadInstallerAsync(InstallerPackage pkg, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        progress.Report(new DownloadProgress(100, 100));
        return Task.FromResult(DownloadResultPath);
    }
}

/// <summary>内存清单缓存。</summary>
public sealed class FakeManifestCache : IManifestCache
{
    private IReadOnlyList<BackendProductManifest>? _data;
    public void Seed(IReadOnlyList<BackendProductManifest> data) => _data = data;

    public Task SaveAsync(IReadOnlyList<BackendProductManifest> manifest, CancellationToken ct)
    {
        _data = manifest;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BackendProductManifest>?> LoadAsync(CancellationToken ct) => Task.FromResult(_data);
    public DateTimeOffset? GetLastUpdated() => _data is null ? null : DateTimeOffset.Now;
    public void Clear() => _data = null;
}
