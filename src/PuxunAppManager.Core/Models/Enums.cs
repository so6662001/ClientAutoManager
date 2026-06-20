namespace PuxunAppManager.Core.Models;

/// <summary>
/// 产品在本机的安装状态。对应开发提示词第 6.2 节状态机。
/// </summary>
public enum ProductStatus
{
    /// <summary>未安装：卸载项不存在且关键文件都不存在。</summary>
    NotInstalled,

    /// <summary>已安装且为最新版本。</summary>
    UpToDate,

    /// <summary>已安装但存在更新的版本。</summary>
    UpdateAvailable,

    /// <summary>已安装但关键文件缺失/损坏（典型：被杀软删除主程序）。</summary>
    Broken,

    /// <summary>检测失败/无法判定。</summary>
    Unknown
}

/// <summary>用户可触发的操作类型。</summary>
public enum OperationType
{
    Install,
    Repair,
    Update
}

/// <summary>后端连接状态，用于底部状态栏展示。</summary>
public enum BackendConnectionState
{
    /// <summary>已连接并使用最新清单。</summary>
    Online,

    /// <summary>后端不可达，使用本地缓存清单（降级）。</summary>
    Cached,

    /// <summary>后端不可达且无缓存。</summary>
    Offline
}

/// <summary>安装包类型。</summary>
public enum InstallerType
{
    Msi,
    Exe
}
