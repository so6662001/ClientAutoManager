using Microsoft.Extensions.Logging;

namespace PuxunAppManager.Core.Backend;

/// <summary>指数退避重试：4s/8s/16s/32s（提示词第 5 节）。</summary>
public static class RetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        int maxRetries,
        ILogger logger,
        CancellationToken ct,
        Func<TimeSpan, CancellationToken, Task>? delayFn = null)
    {
        delayFn ??= (d, c) => Task.Delay(d, c);
        Exception? last = null;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
                if (attempt == maxRetries) break;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 2)); // 4,8,16,32
                logger.LogWarning(ex,
                    "操作失败，第 {Attempt}/{Max} 次重试前等待 {Delay}s",
                    attempt + 1, maxRetries, delay.TotalSeconds);
                await delayFn(delay, ct).ConfigureAwait(false);
            }
        }
        throw last ?? new InvalidOperationException("重试失败且无异常信息。");
    }
}
