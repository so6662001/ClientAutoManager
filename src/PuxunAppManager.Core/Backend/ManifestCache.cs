using System.Text.Json;
using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Backend;

public sealed class ManifestCache : IManifestCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _cacheFilePath;
    private readonly ILogger<ManifestCache> _logger;

    public ManifestCache(string cacheFilePath, ILogger<ManifestCache> logger)
    {
        _cacheFilePath = cacheFilePath;
        _logger = logger;
    }

    public async Task SaveAsync(IReadOnlyList<BackendProductManifest> manifest, CancellationToken ct)
    {
        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(_cacheFilePath, json, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入清单缓存失败：{Path}", _cacheFilePath);
        }
    }

    public async Task<IReadOnlyList<BackendProductManifest>?> LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_cacheFilePath)) return null;
            var json = await File.ReadAllTextAsync(_cacheFilePath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<BackendProductManifest>>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取清单缓存失败：{Path}", _cacheFilePath);
            return null;
        }
    }

    public DateTimeOffset? GetLastUpdated()
    {
        try
        {
            return File.Exists(_cacheFilePath)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(_cacheFilePath), TimeSpan.Zero)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_cacheFilePath)) File.Delete(_cacheFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清空清单缓存失败：{Path}", _cacheFilePath);
        }
    }
}
