using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Backend;
using PuxunAppManager.Core.Configuration;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    private readonly IManifestCache _cache;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(IConfigService config, IManifestCache cache, ILogger<SettingsViewModel> logger)
    {
        _config = config;
        _cache = cache;
        _logger = logger;

        var c = _config.Current;
        _baseUrl = c.Backend.BaseUrl;
        _authType = c.Backend.AuthType;
        _authValue = c.Backend.AuthValue;
        _theme = c.Ui.Theme;
    }

    public SettingsViewModel()
    {
        _config = null!;
        _cache = null!;
        _logger = null!;
    }

    public string[] AuthTypes { get; } = { "none", "apiKey", "token" };
    public string[] Themes { get; } = { "system", "light", "dark" };

    [ObservableProperty] private string _baseUrl = string.Empty;
    [ObservableProperty] private string _authType = "none";
    [ObservableProperty] private string _authValue = string.Empty;
    [ObservableProperty] private string _theme = "system";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public event Action<string>? ThemeChangeRequested;

    [RelayCommand]
    private async Task Save()
    {
        var c = _config.Current;
        c.Backend.BaseUrl = BaseUrl?.Trim() ?? string.Empty;
        c.Backend.AuthType = AuthType;
        c.Backend.AuthValue = AuthValue ?? string.Empty;
        c.Ui.Theme = Theme;
        await _config.SaveAsync(c).ConfigureAwait(true);
        ThemeChangeRequested?.Invoke(Theme);
        StatusMessage = "设置已保存。后端地址变更将在下次重新检测时生效。";
    }

    [RelayCommand]
    private void ClearCache()
    {
        _cache.Clear();
        StatusMessage = "已清空本地清单缓存。";
    }

    [RelayCommand]
    private void OpenLogDir()
    {
        try
        {
            var dir = _config.ResolveDirectory(_config.Current.Paths.LogDir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "打开日志目录失败。");
            StatusMessage = "无法打开日志目录。";
        }
    }
}
