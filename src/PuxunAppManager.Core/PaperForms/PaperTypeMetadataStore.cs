using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PuxunAppManager.Core.PaperForms;

/// <summary>基于 JSON 文件(%AppData%\PuxunAppManager\paper-types.json)的元数据存储。</summary>
public sealed class PaperTypeMetadataStore : IPaperTypeMetadataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly ILogger<PaperTypeMetadataStore> _logger;
    private readonly object _lock = new();
    private Dictionary<string, PaperMetadata> _map;

    public PaperTypeMetadataStore(string filePath, ILogger<PaperTypeMetadataStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
        _map = Load();
    }

    public PaperMetadata? Get(string name)
    {
        lock (_lock)
            return _map.TryGetValue(Normalize(name), out var m) ? m : null;
    }

    public void Upsert(string name, PaperMetadata meta)
    {
        lock (_lock)
        {
            _map[Normalize(name)] = meta;
            Save();
        }
    }

    public void Remove(string name)
    {
        lock (_lock)
        {
            if (_map.Remove(Normalize(name))) Save();
        }
    }

    public void Rename(string oldName, string newName)
    {
        lock (_lock)
        {
            var key = Normalize(oldName);
            if (_map.TryGetValue(key, out var meta))
            {
                _map.Remove(key);
                _map[Normalize(newName)] = meta;
                Save();
            }
        }
    }

    public IReadOnlyDictionary<string, PaperMetadata> All()
    {
        lock (_lock)
            return new Dictionary<string, PaperMetadata>(_map);
    }

    private static string Normalize(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();

    private Dictionary<string, PaperMetadata> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, PaperMetadata>>(json, JsonOptions);
                if (data is not null)
                    return new Dictionary<string, PaperMetadata>(data, StringComparer.Ordinal);
            }
        }
        catch (Exception ex)
        {
            // 元数据损坏 → 忽略并降级（边界条件 14）
            _logger.LogWarning(ex, "纸型元数据损坏，忽略并重建：{Path}", _filePath);
        }
        return new Dictionary<string, PaperMetadata>(StringComparer.Ordinal);
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_map, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存纸型元数据失败：{Path}", _filePath);
        }
    }
}
