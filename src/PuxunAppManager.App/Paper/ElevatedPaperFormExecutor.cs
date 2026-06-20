using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Installation;
using PuxunAppManager.Core.Models;
using PuxunAppManager.Core.PaperForms;

namespace PuxunAppManager.App.Paper;

/// <summary>
/// 在普通(用户)进程中，通过 UAC 提权子进程执行纸型写操作。
/// 经临时 JSON 文件与子进程交换请求/响应，避免命令行转义与权限/配置目录问题。
/// </summary>
public sealed class ElevatedPaperFormExecutor
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<ElevatedPaperFormExecutor> _logger;

    public ElevatedPaperFormExecutor(IProcessRunner processRunner, ILogger<ElevatedPaperFormExecutor> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(PaperFormRequest request, CancellationToken ct)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return OperationResult.Fail(ErrorCodes.PaperSpoolerError, "无法定位程序路径以提权执行。");

        var requestPath = Path.Combine(Path.GetTempPath(), $"pxpaper-req-{Guid.NewGuid():N}.json");
        var responsePath = Path.Combine(Path.GetTempPath(), $"pxpaper-resp-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request), ct).ConfigureAwait(false);

            var args = $"{PaperFormCli.Flag} \"{requestPath}\" \"{responsePath}\"";
            var run = await _processRunner.RunElevatedAsync(exePath, args, ct).ConfigureAwait(false);

            if (run.UserCancelledElevation)
                return OperationResult.Fail(ErrorCodes.UacCancelled, "需要管理员权限才能修改纸型（已取消）。");
            if (!run.Started)
                return OperationResult.Fail(ErrorCodes.PaperSpoolerError, run.ErrorMessage ?? "提权进程启动失败。");

            // 读取子进程响应
            if (File.Exists(responsePath))
            {
                var json = await File.ReadAllTextAsync(responsePath, ct).ConfigureAwait(false);
                var resp = JsonSerializer.Deserialize<PaperFormResponse>(json);
                if (resp is not null)
                    return resp.Success
                        ? OperationResult.Ok(resp.Message)
                        : OperationResult.Fail(resp.ErrorCode ?? ErrorCodes.PaperSpoolerError, resp.Message ?? "操作失败。");
            }

            // 无响应文件则以退出码判定
            return run.ExitCode == 0
                ? OperationResult.Ok()
                : OperationResult.Fail(ErrorCodes.PaperSpoolerError, $"操作失败（退出码 {run.ExitCode}）。");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail(ErrorCodes.Cancelled, "操作已取消。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提权执行纸型写操作失败。");
            return OperationResult.Fail(ErrorCodes.PaperSpoolerError, $"操作失败：{ex.Message}");
        }
        finally
        {
            TryDelete(requestPath);
            TryDelete(responsePath);
        }
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "清理临时文件失败：{Path}", path); }
    }
}
