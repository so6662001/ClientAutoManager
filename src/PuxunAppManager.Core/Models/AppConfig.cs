using System.Text.Json.Serialization;

namespace PuxunAppManager.Core.Models;

/// <summary>应用配置（持久化到 %AppData%\PuxunAppManager\config.json）。</summary>
public sealed class AppConfig
{
    [JsonPropertyName("backend")]
    public BackendConfig Backend { get; set; } = new();

    [JsonPropertyName("paths")]
    public PathsConfig Paths { get; set; } = new();

    [JsonPropertyName("ui")]
    public UiConfig Ui { get; set; } = new();

    public static AppConfig CreateDefault() => new();
}

public sealed class BackendConfig
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "https://localhost/api";

    /// <summary>鉴权类型：none | apiKey | token。</summary>
    [JsonPropertyName("authType")]
    public string AuthType { get; set; } = "none";

    [JsonPropertyName("authValue")]
    public string AuthValue { get; set; } = string.Empty;

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 4;
}

public sealed class PathsConfig
{
    [JsonPropertyName("manifestCache")]
    public string ManifestCache { get; set; } = @"%AppData%\PuxunAppManager\manifest.cache.json";

    [JsonPropertyName("downloadDir")]
    public string DownloadDir { get; set; } = @"%AppData%\PuxunAppManager\downloads";

    [JsonPropertyName("logDir")]
    public string LogDir { get; set; } = @"%AppData%\PuxunAppManager\logs";
}

public sealed class UiConfig
{
    /// <summary>light | dark | system。</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "system";
}
