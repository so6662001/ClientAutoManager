using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Detection;

/// <summary>
/// 检测引擎实现，严格遵循开发提示词第 6.2 节状态机：
///
/// | 条件                                                        | 结果             |
/// | 卸载项不存在 且 关键文件都不存在                            | NotInstalled    |
/// | 卸载项存在 但 关键文件缺失或哈希不符                        | Broken          |
/// | 关键文件齐全且哈希匹配 且 本机版本 == 最新                  | UpToDate        |
/// | 关键文件齐全且哈希匹配 且 本机版本 &lt; 最新               | UpdateAvailable |
/// | 检测过程抛异常/无法读取                                     | Unknown         |
/// </summary>
public sealed class DetectionService : IDetectionService
{
    private readonly IRegistryReader _registry;
    private readonly IFileProbe _files;
    private readonly ILogger<DetectionService> _logger;

    public DetectionService(IRegistryReader registry, IFileProbe files, ILogger<DetectionService> logger)
    {
        _registry = registry;
        _files = files;
        _logger = logger;
    }

    public async Task<DetectionResult> DetectAsync(DetectionRule rule, string latestVersion, CancellationToken ct)
    {
        try
        {
            bool uninstallKeyExists = !string.IsNullOrWhiteSpace(rule.RegistryUninstallKey)
                                      && _registry.UninstallKeyExists(rule.RegistryUninstallKey!);

            // 评估关键文件：是否全部存在、是否全部哈希匹配
            var missing = new List<string>();
            bool allFilesPresent = true;
            bool allHashMatch = true;

            foreach (var kf in rule.KeyFiles)
            {
                ct.ThrowIfCancellationRequested();
                var expanded = _files.ExpandPath(kf.Path);
                if (!_files.FileExists(expanded))
                {
                    allFilesPresent = false;
                    missing.Add(kf.Path);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(kf.Sha256))
                {
                    var actual = await _files.ComputeSha256Async(expanded, ct).ConfigureAwait(false);
                    if (!string.Equals(actual, kf.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        allHashMatch = false;
                        missing.Add(kf.Path);
                    }
                }
            }

            bool hasKeyFiles = rule.KeyFiles.Count > 0;
            bool keyFilesHealthy = hasKeyFiles ? (allFilesPresent && allHashMatch) : false;
            bool anyKeyFileMissing = hasKeyFiles && (!allFilesPresent || !allHashMatch);

            // 读取已安装版本（注册表）
            string? installedVersion = null;
            if (uninstallKeyExists && !string.IsNullOrWhiteSpace(rule.VersionRegistryValue))
            {
                installedVersion = _registry.ReadUninstallValue(
                    rule.RegistryUninstallKey!, rule.VersionRegistryValue!);
            }

            // ===== 状态机判定 =====

            // 未安装：卸载项不存在 且 (无关键文件配置 或 关键文件全不存在)
            bool noFilesAtAll = !hasKeyFiles || rule.KeyFiles.All(kf => !_files.FileExists(_files.ExpandPath(kf.Path)));
            if (!uninstallKeyExists && noFilesAtAll)
            {
                return new DetectionResult { Status = ProductStatus.NotInstalled };
            }

            // 已损坏：卸载项存在 但 关键文件缺失/哈希不符
            if (uninstallKeyExists && anyKeyFileMissing)
            {
                _logger.LogWarning(
                    "检测到产品已损坏，疑似被安全软件移除。卸载项存在但缺失文件: {Missing}",
                    string.Join(", ", missing));
                return new DetectionResult
                {
                    Status = ProductStatus.Broken,
                    InstalledVersion = installedVersion,
                    MissingFiles = missing
                };
            }

            // 卸载项不存在但部分关键文件在 → 也按损坏处理（残留/不完整安装）
            if (!uninstallKeyExists && hasKeyFiles && !noFilesAtAll && anyKeyFileMissing)
            {
                return new DetectionResult
                {
                    Status = ProductStatus.Broken,
                    InstalledVersion = installedVersion,
                    MissingFiles = missing
                };
            }

            // 健康：关键文件齐全且哈希匹配（或仅有卸载项且无文件规则但卸载项在）
            bool healthy = keyFilesHealthy || (uninstallKeyExists && !hasKeyFiles);
            if (healthy)
            {
                // 版本未知时（注册表读不到）：视为已安装、版本未知，按"已安装(最新)"展示以提供"修复"入口（提示词第 9.9 条）。
                if (string.IsNullOrWhiteSpace(installedVersion))
                {
                    return new DetectionResult { Status = ProductStatus.UpToDate, InstalledVersion = null };
                }

                var status = VersionComparer.IsOlder(installedVersion, latestVersion)
                    ? ProductStatus.UpdateAvailable
                    : ProductStatus.UpToDate;

                return new DetectionResult { Status = status, InstalledVersion = installedVersion };
            }

            // 兜底：无法明确判定
            return new DetectionResult { Status = ProductStatus.Unknown, InstalledVersion = installedVersion };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检测产品状态时发生异常，判定为 Unknown。");
            return new DetectionResult { Status = ProductStatus.Unknown };
        }
    }
}
