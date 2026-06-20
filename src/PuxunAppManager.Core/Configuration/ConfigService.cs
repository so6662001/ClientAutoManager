using System.Text.Json;
using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Configuration;

public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configPath;
    private readonly ILogger<ConfigService> _logger;
    private AppConfig _current = AppConfig.CreateDefault();

    public ConfigService(ILogger<ConfigService> logger, string? configPathOverride = null)
    {
        _logger = logger;
        _configPath = configPathOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PuxunAppManager", "config.json");
    }

    public AppConfig Current => _current;

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg is not null)
                {
                    _current = cfg;
                    return _current;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "配置文件缺失或损坏，使用默认配置并重建：{Path}", _configPath);
        }

        // 缺失/损坏：使用默认并写回
        _current = AppConfig.CreateDefault();
        TryWrite(_current);
        return _current;
    }

    public async Task SaveAsync(AppConfig config, CancellationToken ct = default)
    {
        _current = config;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(_configPath, json, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置失败：{Path}", _configPath);
        }
    }

    public string ResolveDirectory(string configuredPath)
    {
        var dir = Environment.ExpandEnvironmentVariables(configuredPath);
        try { Directory.CreateDirectory(dir); }
        catch (Exception ex) { _logger.LogWarning(ex, "创建目录失败：{Dir}", dir); }
        return dir;
    }

    public string ResolveFilePath(string configuredPath)
    {
        var full = Environment.ExpandEnvironmentVariables(configuredPath);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex) { _logger.LogWarning(ex, "创建目录失败：{Dir}", dir); }
        }
        return full;
    }

    private void TryWrite(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            File.WriteAllText(_configPath, JsonSerializer.Serialize(config, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入默认配置失败：{Path}", _configPath);
        }
    }
}
