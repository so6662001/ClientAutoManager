using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Configuration;

/// <summary>应用配置读写。配置缺失/损坏时使用内置默认并重建（提示词第 9.13 条）。</summary>
public interface IConfigService
{
    AppConfig Current { get; }

    /// <summary>加载配置（损坏自动重建为默认）。</summary>
    AppConfig Load();

    /// <summary>持久化当前配置。</summary>
    Task SaveAsync(AppConfig config, CancellationToken ct = default);

    /// <summary>展开路径中的环境变量并确保目录存在。</summary>
    string ResolveDirectory(string configuredPath);

    string ResolveFilePath(string configuredPath);
}
