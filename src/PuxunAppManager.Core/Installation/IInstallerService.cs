using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Installation;

/// <summary>安装/修复/更新统一流水线。</summary>
public interface IInstallerService
{
    /// <summary>
    /// 对指定产品执行操作（安装/修复/更新）。统一流水线：
    /// 获取安装包 → 下载 → 哈希校验 → 签名校验 → UAC 静默安装 → 读退出码 → 安装后复检。
    /// 全程通过 progress 回调，异常转为 OperationResult，不冒泡。
    /// </summary>
    Task<OperationResult> ExecuteAsync(
        ProductInfo product,
        OperationType operation,
        IProgress<InstallProgress> progress,
        CancellationToken ct);
}
