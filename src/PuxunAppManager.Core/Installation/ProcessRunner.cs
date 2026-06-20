using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PuxunAppManager.Core.Installation;

/// <summary>
/// 真实进程执行：以管理员身份(runas)启动安装子进程。
/// Windows 上 UseShellExecute=true + Verb=runas 触发 UAC；用户拒绝时抛 Win32Exception(1223)。
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private const int ErrorCancelled = 1223; // 用户取消 UAC
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger) => _logger = logger;

    public async Task<ProcessRunResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            try
            {
                if (!process.Start())
                    return new ProcessRunResult { Started = false, ErrorMessage = "无法启动安装进程。" };
            }
            catch (Win32Exception wex) when (wex.NativeErrorCode == ErrorCancelled)
            {
                _logger.LogWarning("用户取消了 UAC 提权。");
                return new ProcessRunResult { Started = false, UserCancelledElevation = true, ErrorMessage = "用户取消提权。" };
            }

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return new ProcessRunResult { Started = true, ExitCode = process.ExitCode };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行安装进程失败：{File} {Args}", fileName, arguments);
            return new ProcessRunResult { Started = false, ErrorMessage = ex.Message };
        }
    }

    public bool HasEnoughDiskSpace(string targetPath, long requiredBytes)
    {
        try
        {
            if (requiredBytes <= 0) return true;
            var root = Path.GetPathRoot(Path.GetFullPath(targetPath));
            if (string.IsNullOrEmpty(root)) return true;
            var drive = new DriveInfo(root);
            // 需求空间 + 2 倍冗余（下载 + 安装展开）
            return drive.AvailableFreeSpace > requiredBytes * 2 + (50L << 20);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "磁盘空间检测失败，按通过处理：{Path}", targetPath);
            return true;
        }
    }
}
