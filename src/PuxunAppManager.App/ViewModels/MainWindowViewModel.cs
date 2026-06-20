using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Configuration;
using PuxunAppManager.Core.Models;
using PuxunAppManager.Core.Services;

namespace PuxunAppManager.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IProductActionHost
{
    private readonly IAppManagerService _service;
    private readonly IConfigService _config;
    private readonly ILogger<MainWindowViewModel> _logger;
    private CancellationTokenSource? _scanCts;

    public MainWindowViewModel(IAppManagerService service, IConfigService config, ILogger<MainWindowViewModel> logger)
    {
        _service = service;
        _config = config;
        _logger = logger;

        AppVersionText = $"版本 v{GetAppVersion()} · 已签名";
        _ = ScanAsync();
    }

    /// <summary>设计器用无参构造。</summary>
    public MainWindowViewModel()
    {
        _service = null!;
        _config = null!;
        _logger = null!;
    }

    public ObservableCollection<ProductItemViewModel> AllProducts { get; } = new();
    public ObservableCollection<ProductItemViewModel> FilteredProducts { get; } = new();

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasScanError;
    [ObservableProperty] private string _scanError = string.Empty;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _activeFilter = "all";

    [ObservableProperty] private string _connectionText = "正在连接…";
    [ObservableProperty] private bool _isOnline;
    [ObservableProperty] private string _lastScanText = string.Empty;
    [ObservableProperty] private string _appVersionText = string.Empty;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _notInstalledCount;
    [ObservableProperty] private int _updatableCount;
    [ObservableProperty] private int _brokenCount;

    public bool HasRunningOperations => AllProducts.Any(p => p.IsBusy);

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnActiveFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SetFilter(string filter) => ActiveFilter = filter ?? "all";

    /// <summary>由 View 注入：打开设置窗口的动作（保持 VM 不直接依赖窗口类型）。</summary>
    public Action? OpenSettingsAction { get; set; }

    /// <summary>由 View 注入：打开纸型管理窗口的动作。</summary>
    public Action? OpenPaperFormsAction { get; set; }

    [RelayCommand]
    private void ToggleTheme() => App.ToggleTheme();

    [RelayCommand]
    private void OpenSettings() => OpenSettingsAction?.Invoke();

    [RelayCommand]
    private void OpenPaperForms() => OpenPaperFormsAction?.Invoke();

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task Refresh() => await ScanAsync();

    private bool CanScan() => !IsScanning;

    private async Task ScanAsync()
    {
        if (IsScanning) return;
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        HasScanError = false;
        RefreshCommand.NotifyCanExecuteChanged();
        ConnectionText = "正在检测…";

        try
        {
            var scan = await _service.ScanAsync(ct).ConfigureAwait(true);

            if (!string.IsNullOrEmpty(scan.Error) && scan.Products.Count == 0)
            {
                HasScanError = true;
                ScanError = scan.Error!;
            }

            MergeProducts(scan.Products);
            UpdateConnection(scan.ConnectionState);
            LastScanText = $"上次检测：{scan.ScannedAt:MM-dd HH:mm}";
            RecountAndFilter();
        }
        catch (OperationCanceledException)
        {
            // 被新的检测取代，忽略
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "检测流程异常。");
            HasScanError = true;
            ScanError = "检测过程中发生错误，请重试。";
        }
        finally
        {
            IsScanning = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    private void MergeProducts(System.Collections.Generic.IReadOnlyList<ProductInfo> products)
    {
        AllProducts.Clear();
        foreach (var p in products)
            AllProducts.Add(new ProductItemViewModel(p, this));
    }

    private void UpdateConnection(BackendConnectionState state)
    {
        (ConnectionText, IsOnline) = state switch
        {
            BackendConnectionState.Online => ("已连接更新服务器", true),
            BackendConnectionState.Cached => ("离线 · 使用本地缓存清单", false),
            _ => ("未连接更新服务器", false)
        };
    }

    private void RecountAndFilter()
    {
        TotalCount = AllProducts.Count;
        NotInstalledCount = AllProducts.Count(p => p.Status == ProductStatus.NotInstalled);
        UpdatableCount = AllProducts.Count(p => p.Status == ProductStatus.UpdateAvailable);
        BrokenCount = AllProducts.Count(p => p.Status == ProductStatus.Broken);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredProducts.Clear();
        foreach (var p in AllProducts.Where(p => p.MatchesFilter(ActiveFilter, SearchText)))
            FilteredProducts.Add(p);
    }

    // ===== IProductActionHost =====

    public async Task ExecuteAsync(ProductItemViewModel item, OperationType operation)
    {
        if (item.IsBusy) return; // 并发去重（提示词第 9.10 条）

        item.OperationCts = new CancellationTokenSource();
        var ct = item.OperationCts.Token;

        var verb = operation switch
        {
            OperationType.Install => "正在安装…",
            OperationType.Repair => "正在修复…",
            OperationType.Update => "正在更新…",
            _ => "处理中…"
        };
        item.BeginBusy(verb);
        OnPropertyChanged(nameof(HasRunningOperations));

        // Progress 在 UI 线程创建，回调自动回到 UI 线程
        var progress = new Progress<InstallProgress>(p => item.ApplyProgress(p));

        try
        {
            var (result, updated) = await _service.ExecuteOperationAsync(item.Model, operation, progress, ct)
                .ConfigureAwait(true);

            item.UpdateModel(updated);
            item.EndBusy(result);
        }
        catch (OperationCanceledException)
        {
            item.EndBusy(OperationResult.Fail(ErrorCodes.Cancelled, "操作已取消。"));
            // 取消后复检以恢复正确状态
            await RedetectSafelyAsync(item);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "执行操作异常：{Product}", item.ProductId);
            item.EndBusy(OperationResult.Fail(ErrorCodes.Unknown, $"操作失败：{ex.Message}"));
        }
        finally
        {
            item.OperationCts?.Dispose();
            item.OperationCts = null;
            OnPropertyChanged(nameof(HasRunningOperations));
            RecountAndFilter();
        }
    }

    public void Cancel(ProductItemViewModel item)
    {
        try { item.OperationCts?.Cancel(); }
        catch { /* ignore */ }
    }

    private async Task RedetectSafelyAsync(ProductItemViewModel item)
    {
        try
        {
            var updated = await _service.RedetectAsync(item.Model, CancellationToken.None).ConfigureAwait(true);
            item.UpdateModel(updated);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "复检失败：{Product}", item.ProductId);
        }
    }

    private static string GetAppVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
