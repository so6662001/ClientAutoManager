using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Backend;

/// <summary>本地清单缓存，用于后端不可达时的降级（提示词第 6.1.4 条）。</summary>
public interface IManifestCache
{
    Task SaveAsync(IReadOnlyList<BackendProductManifest> manifest, CancellationToken ct);

    /// <summary>读取上次缓存的清单；无缓存返回 null。</summary>
    Task<IReadOnlyList<BackendProductManifest>?> LoadAsync(CancellationToken ct);

    DateTimeOffset? GetLastUpdated();

    void Clear();
}
