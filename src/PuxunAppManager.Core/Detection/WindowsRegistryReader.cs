using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PuxunAppManager.Core.Detection;

/// <summary>
/// 基于 Microsoft.Win32 的注册表读取实现，兼容 64 位与 WOW6432Node 视图。
/// 仅在 Windows 上调用；非 Windows 平台调用会安全返回空（不会崩溃）。
/// </summary>
public sealed class WindowsRegistryReader : IRegistryReader
{
    public bool UninstallKeyExists(string uninstallKeyPath)
    {
        if (string.IsNullOrWhiteSpace(uninstallKeyPath)) return false;
        if (!OperatingSystem.IsWindows()) return false;
        return TryOpen(uninstallKeyPath) is not null;
    }

    public string? ReadUninstallValue(string uninstallKeyPath, string valueName)
    {
        if (string.IsNullOrWhiteSpace(uninstallKeyPath) || string.IsNullOrWhiteSpace(valueName))
            return null;
        if (!OperatingSystem.IsWindows()) return null;

        using var key = TryOpen(uninstallKeyPath);
        var value = key?.GetValue(valueName);
        return value?.ToString();
    }

    [SupportedOSPlatform("windows")]
    private static RegistryKey? TryOpen(string subKeyPath)
    {
        var normalized = Normalize(subKeyPath);

        // 依次尝试 64 位视图与 32 位(WOW6432Node)视图
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                var key = baseKey.OpenSubKey(normalized);
                if (key is not null) return key;
            }
            catch
            {
                // 忽略单个视图的访问异常，继续尝试下一个
            }
        }
        return null;
    }

    /// <summary>去掉可能存在的 HKLM\ / HKEY_LOCAL_MACHINE\ 前缀，得到相对子键路径。</summary>
    private static string Normalize(string path)
    {
        var p = path.Replace('/', '\\').Trim();
        string[] prefixes = { @"HKEY_LOCAL_MACHINE\", @"HKLM\" };
        foreach (var prefix in prefixes)
        {
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return p[prefix.Length..];
        }
        return p;
    }
}
