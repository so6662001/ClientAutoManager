namespace PuxunAppManager.Core.Installation;

/// <summary>提权进程执行抽象，便于测试替换真实安装行为。</summary>
public interface IProcessRunner
{
    /// <summary>
    /// 以管理员身份(UAC 提权)启动安装进程并等待退出。
    /// </summary>
    /// <returns>进程执行结果（含退出码 / 是否被用户取消提权）。</returns>
    Task<ProcessRunResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct);

    /// <summary>检测目标盘剩余空间是否足够（用于安装前预检）。</summary>
    bool HasEnoughDiskSpace(string targetPath, long requiredBytes);
}

public sealed class ProcessRunResult
{
    public bool Started { get; init; }

    /// <summary>用户在 UAC 弹窗点击"否"。</summary>
    public bool UserCancelledElevation { get; init; }

    public int ExitCode { get; init; }

    public string? ErrorMessage { get; init; }
}
