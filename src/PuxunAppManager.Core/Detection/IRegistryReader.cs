namespace PuxunAppManager.Core.Detection;

/// <summary>
/// 注册表读取抽象（平台无关）。Windows 实现使用 Microsoft.Win32 并兼容 32/64 位视图；
/// 抽象出接口便于单元测试以假数据替换。
/// </summary>
public interface IRegistryReader
{
    /// <summary>HKLM 下指定卸载项是否存在（自动尝试 64 位与 WOW6432Node 视图）。</summary>
    bool UninstallKeyExists(string uninstallKeyPath);

    /// <summary>读取 HKLM 卸载项下某个字符串值（如 DisplayVersion）。不存在返回 null。</summary>
    string? ReadUninstallValue(string uninstallKeyPath, string valueName);
}
