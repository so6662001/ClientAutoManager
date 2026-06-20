using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Backend;

/// <summary>
/// 后端适配层：管家与公司现有后端管理系统交互的唯一出口。
/// 管家内部只认本接口与统一模型；具体实现负责把现有后端的真实接口/字段映射过来。
/// 后端接口变化时，仅需修改该接口的实现。
/// </summary>
public interface IBackendClient
{
    /// <summary>拉取所有产品的最新版本清单（含下载地址、哈希、检测规则）。</summary>
    Task<IReadOnlyList<BackendProductManifest>> GetManifestAsync(CancellationToken ct);

    /// <summary>获取指定产品指定版本的安装包下载信息。</summary>
    Task<InstallerPackage> GetInstallerAsync(string productId, string version, CancellationToken ct);

    /// <summary>下载安装包到本地，支持进度回调与取消。返回本地文件完整路径。</summary>
    Task<string> DownloadInstallerAsync(
        InstallerPackage pkg, IProgress<DownloadProgress> progress, CancellationToken ct);
}
