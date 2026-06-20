using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Backend;
using PuxunAppManager.Core.Detection;
using PuxunAppManager.Core.Models;
using PuxunAppManager.Core.Security;

namespace PuxunAppManager.Core.Installation;

/// <summary>
/// 安装流水线实现，严格遵循开发提示词第 6.4 节：
/// 1. 取安装包 → 2. 下载(进度) → 3. 哈希校验 → 4. 签名校验 → 5. UAC 静默安装
/// → 6. 读退出码(0/3010 视为成功) → 7. 安装后复检 → 8. 失败清理临时文件、可重试。
/// </summary>
public sealed class InstallerService : IInstallerService
{
    private const int ExitSuccess = 0;
    private const int ExitRebootRequired = 3010;

    private readonly IBackendClient _backend;
    private readonly ISecurityVerifier _security;
    private readonly IProcessRunner _processRunner;
    private readonly IDetectionService _detection;
    private readonly ILogger<InstallerService> _logger;

    public InstallerService(
        IBackendClient backend,
        ISecurityVerifier security,
        IProcessRunner processRunner,
        IDetectionService detection,
        ILogger<InstallerService> logger)
    {
        _backend = backend;
        _security = security;
        _processRunner = processRunner;
        _detection = detection;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(
        ProductInfo product,
        OperationType operation,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        string? downloadedFile = null;
        try
        {
            progress.Report(new InstallProgress { Phase = InstallPhase.Preparing, Percent = 0, Message = "正在准备…" });

            // 1. 获取安装包信息（优先用清单内已有的；否则向后端查询）
            var pkg = product.Installer
                      ?? await _backend.GetInstallerAsync(product.ProductId, product.LatestVersion, ct).ConfigureAwait(false);

            if (pkg is null || string.IsNullOrWhiteSpace(pkg.DownloadUrl))
            {
                return OperationResult.Fail(ErrorCodes.InstallerMissing, "未获取到有效的安装包信息。");
            }

            // 磁盘空间预检（提示词第 9.11 条）
            if (!_processRunner.HasEnoughDiskSpace(Path.GetTempPath(), pkg.SizeBytes))
            {
                return OperationResult.Fail(ErrorCodes.DiskSpaceInsufficient, "磁盘剩余空间不足，无法继续安装。");
            }

            // 2. 下载
            progress.Report(new InstallProgress { Phase = InstallPhase.Downloading, Percent = 0, Message = "正在下载安装包…" });
            var dlProgress = new Progress<DownloadProgress>(p =>
                progress.Report(new InstallProgress
                {
                    Phase = InstallPhase.Downloading,
                    Percent = p.Percent,
                    Message = p.TotalBytes > 0
                        ? $"正在下载安装包 ({Format(p.BytesReceived)} / {Format(p.TotalBytes)})"
                        : $"正在下载安装包 ({Format(p.BytesReceived)})"
                }));

            try
            {
                downloadedFile = await _backend.DownloadInstallerAsync(pkg, dlProgress, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载安装包失败：{Product}", product.ProductId);
                return OperationResult.Fail(ErrorCodes.DownloadFailed, "安装包下载失败，请检查网络后重试。");
            }

            // 3. 哈希校验（强制：清单提供哈希则必须匹配）
            progress.Report(new InstallProgress { Phase = InstallPhase.Verifying, Percent = 100, Message = "正在校验安全性…" });
            bool hashOk = await _security.VerifyHashAsync(downloadedFile, pkg.Sha256, ct).ConfigureAwait(false);
            if (!hashOk)
            {
                SafeDelete(downloadedFile);
                return OperationResult.Fail(ErrorCodes.HashMismatch, "安全校验未通过（哈希不匹配），已阻止安装。");
            }

            // 4. 签名校验（强制）
            var sig = _security.VerifySignature(downloadedFile);
            if (!sig.IsValid)
            {
                SafeDelete(downloadedFile);
                return OperationResult.Fail(ErrorCodes.SignatureInvalid,
                    $"安全校验未通过（数字签名无效：{sig.FailureReason}），已阻止安装。");
            }

            progress.Report(new InstallProgress
            {
                Phase = InstallPhase.Verifying, Percent = 100,
                Message = "数字签名与哈希校验通过", SignatureVerified = true, HashVerified = true
            });

            // 5. UAC 静默安装
            progress.Report(new InstallProgress { Phase = InstallPhase.Installing, Percent = 100, Message = "正在安装（请在 UAC 提示中确认）…", SignatureVerified = true, HashVerified = true });
            var (fileName, args) = BuildCommand(pkg, downloadedFile, operation);
            var run = await _processRunner.RunElevatedAsync(fileName, args, ct).ConfigureAwait(false);

            // 6. 处理退出码
            if (run.UserCancelledElevation)
            {
                return OperationResult.Fail(ErrorCodes.UacCancelled, "已取消（未授予管理员权限）。");
            }
            if (!run.Started)
            {
                return OperationResult.Fail(ErrorCodes.Unknown, run.ErrorMessage ?? "安装进程启动失败。");
            }
            if (run.ExitCode != ExitSuccess && run.ExitCode != ExitRebootRequired)
            {
                _logger.LogError("安装失败，退出码 {Code}：{Product}", run.ExitCode, product.ProductId);
                return OperationResult.Fail(ErrorCodes.InstallExitNonZero, $"安装失败（退出码 {run.ExitCode}），可重试。");
            }

            // 7. 安装后复检
            progress.Report(new InstallProgress { Phase = InstallPhase.PostVerifying, Percent = 100, Message = "正在校验安装结果…" });
            var recheck = await _detection.DetectAsync(product.DetectionRule, product.LatestVersion, ct).ConfigureAwait(false);
            if (recheck.Status is ProductStatus.Broken or ProductStatus.NotInstalled)
            {
                _logger.LogWarning("安装后复检异常（疑似被安全软件拦截/移除）：{Product} 状态 {Status}", product.ProductId, recheck.Status);
                return OperationResult.Fail(ErrorCodes.PostInstallVerifyFailed,
                    "安装已执行，但复检发现文件被移除，疑似被安全软件拦截。请将安装目录加入杀毒软件信任区后重试。");
            }

            SafeDelete(downloadedFile);
            progress.Report(new InstallProgress { Phase = InstallPhase.Completed, Percent = 100, Message = "完成" });

            var verb = operation switch
            {
                OperationType.Install => "安装",
                OperationType.Repair => "修复",
                OperationType.Update => "更新",
                _ => "操作"
            };
            return OperationResult.Ok($"{verb}成功" + (run.ExitCode == ExitRebootRequired ? "（需要重启生效）" : ""));
        }
        catch (OperationCanceledException)
        {
            if (downloadedFile is not null) SafeDelete(downloadedFile);
            return OperationResult.Fail(ErrorCodes.Cancelled, "操作已取消。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安装流水线异常：{Product}", product.ProductId);
            if (downloadedFile is not null) SafeDelete(downloadedFile);
            return OperationResult.Fail(ErrorCodes.Unknown, $"操作失败：{ex.Message}");
        }
    }

    /// <summary>根据安装包类型与操作类型构建安装命令。</summary>
    internal static (string fileName, string args) BuildCommand(InstallerPackage pkg, string filePath, OperationType operation)
    {
        if (pkg.InstallerType == InstallerType.Msi)
        {
            // 修复使用 msiexec /fa（重装所有文件）；其余使用 /i。统一静默 /qn /norestart。
            string action = operation == OperationType.Repair ? "/fa" : "/i";
            return ("msiexec.exe", $"{action} \"{filePath}\" /qn /norestart");
        }

        // EXE：使用配置的静默参数，支持 {file} 占位符
        var args = pkg.SilentArgs.Contains("{file}", StringComparison.OrdinalIgnoreCase)
            ? pkg.SilentArgs.Replace("{file}", $"\"{filePath}\"", StringComparison.OrdinalIgnoreCase)
            : pkg.SilentArgs;
        return (filePath, args);
    }

    private void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "清理临时安装包失败：{Path}", path); }
    }

    private static string Format(long bytes)
    {
        if (bytes < 0) return "?";
        string[] units = { "B", "KB", "MB", "GB" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {units[i]}";
    }
}
