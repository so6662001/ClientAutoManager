using System.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.App.ViewModels;

/// <summary>宿主接口：产品卡片把操作回调给主 ViewModel。</summary>
public interface IProductActionHost
{
    System.Threading.Tasks.Task ExecuteAsync(ProductItemViewModel item, OperationType operation);
    void Cancel(ProductItemViewModel item);
}

/// <summary>单个产品卡片的 ViewModel。</summary>
public sealed partial class ProductItemViewModel : ViewModelBase
{
    private readonly IProductActionHost _host;

    public ProductItemViewModel(ProductInfo model, IProductActionHost host)
    {
        _host = host;
        Model = model;
        Refresh();
    }

    public ProductInfo Model { get; private set; }

    public CancellationTokenSource? OperationCts { get; set; }

    public string ProductId => Model.ProductId;
    public string DisplayName => Model.DisplayName;
    public string Description => Model.Description;
    public string IconKey => string.IsNullOrEmpty(Model.IconKey) ? "?" : Model.IconKey[..1];

    [ObservableProperty] private ProductStatus _status;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _versionText = string.Empty;
    [ObservableProperty] private string _signatureText = string.Empty;

    [ObservableProperty] private bool _showPrimaryAction;
    [ObservableProperty] private string _primaryActionText = string.Empty;
    [ObservableProperty] private bool _showRepairSecondary;
    [ObservableProperty] private bool _showRetry;

    // 进度态
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private bool _securityChecked;

    // 操作结果提示
    [ObservableProperty] private bool _hasMessage;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _messageIsError;

    /// <summary>图标背景色（按产品 Id 稳定映射调色板）。</summary>
    public IBrush IconBrush => Palette.For(Model.ProductId);

    public OperationType PrimaryOperation { get; private set; }

    /// <summary>用于筛选/搜索的匹配。</summary>
    public bool MatchesFilter(string filter, string search)
    {
        bool filterOk = filter switch
        {
            "notinstalled" => Status == ProductStatus.NotInstalled,
            "updatable" => Status == ProductStatus.UpdateAvailable,
            "broken" => Status == ProductStatus.Broken,
            _ => true
        };
        bool searchOk = string.IsNullOrWhiteSpace(search)
                        || DisplayName.Contains(search, System.StringComparison.OrdinalIgnoreCase)
                        || Description.Contains(search, System.StringComparison.OrdinalIgnoreCase);
        return filterOk && searchOk;
    }

    public void UpdateModel(ProductInfo model)
    {
        Model = model;
        Refresh();
    }

    /// <summary>根据模型状态刷新所有展示属性与可用操作（状态→操作映射，提示词第 6.3 节）。</summary>
    public void Refresh()
    {
        Status = Model.Status;
        OnPropertyChanged(nameof(IconBrush));

        ShowPrimaryAction = false;
        ShowRepairSecondary = false;
        ShowRetry = false;

        switch (Model.Status)
        {
            case ProductStatus.NotInstalled:
                StatusText = "未安装";
                PrimaryOperation = OperationType.Install;
                PrimaryActionText = "安装";
                ShowPrimaryAction = true;
                VersionText = $"本机版本 —    最新版本 {Model.LatestVersion}";
                break;

            case ProductStatus.UpToDate:
                StatusText = "已安装（最新）";
                PrimaryOperation = OperationType.Repair;
                PrimaryActionText = "修复";
                ShowPrimaryAction = true;
                VersionText = $"本机版本 {Model.InstalledVersion ?? "未知"}    最新版本 {Model.LatestVersion}";
                break;

            case ProductStatus.UpdateAvailable:
                StatusText = "可更新";
                PrimaryOperation = OperationType.Update;
                PrimaryActionText = "更新";
                ShowPrimaryAction = true;
                ShowRepairSecondary = true;
                VersionText = $"本机版本 {Model.InstalledVersion ?? "未知"}    最新版本 {Model.LatestVersion} ↑";
                break;

            case ProductStatus.Broken:
                StatusText = "已损坏（主程序丢失）";
                PrimaryOperation = OperationType.Repair;
                PrimaryActionText = "修复";
                ShowPrimaryAction = true;
                var miss = Model.MissingFiles.Count > 0 ? $"    缺失文件 {System.IO.Path.GetFileName(Model.MissingFiles[0])}" : string.Empty;
                VersionText = $"本机版本 {Model.InstalledVersion ?? "未知"}    最新版本 {Model.LatestVersion}{miss}";
                break;

            default: // Unknown
                StatusText = "状态未知";
                ShowRetry = true;
                VersionText = "无法判定安装状态，请重试检测";
                break;
        }

        SignatureText = Model.SignatureVerified ? "签名 已验证 ✓" : string.Empty;
    }

    [RelayCommand]
    private System.Threading.Tasks.Task PrimaryAction() => _host.ExecuteAsync(this, PrimaryOperation);

    [RelayCommand]
    private System.Threading.Tasks.Task Repair() => _host.ExecuteAsync(this, OperationType.Repair);

    [RelayCommand]
    private System.Threading.Tasks.Task Retry() => _host.ExecuteAsync(this, OperationType.Repair);

    [RelayCommand]
    private void Cancel() => _host.Cancel(this);

    public void ApplyProgress(InstallProgress p)
    {
        IsBusy = p.Phase is not (InstallPhase.Completed or InstallPhase.Failed);
        Progress = p.Percent;
        ProgressText = p.Message;
        if (p.SignatureVerified && p.HashVerified) SecurityChecked = true;
    }

    public void BeginBusy(string text)
    {
        HasMessage = false;
        IsBusy = true;
        SecurityChecked = false;
        Progress = 0;
        ProgressText = text;
    }

    public void EndBusy(OperationResult result)
    {
        IsBusy = false;
        Progress = 0;
        HasMessage = true;
        MessageIsError = !result.Success;
        Message = result.Message ?? (result.Success ? "完成" : "操作失败");
    }
}

/// <summary>卡片图标调色板，按字符串稳定取色。</summary>
internal static class Palette
{
    private static readonly (Color from, Color to)[] Colors =
    {
        (Color.Parse("#2563eb"), Color.Parse("#1e40af")),
        (Color.Parse("#0891b2"), Color.Parse("#0e7490")),
        (Color.Parse("#7c3aed"), Color.Parse("#5b21b6")),
        (Color.Parse("#db2777"), Color.Parse("#9d174d")),
        (Color.Parse("#ea580c"), Color.Parse("#c2410c")),
        (Color.Parse("#0f8a4f"), Color.Parse("#0b6b3d")),
    };

    public static IBrush For(string key)
    {
        int h = 0;
        foreach (var c in key ?? string.Empty) h = (h * 31 + c) & 0x7fffffff;
        var (from, to) = Colors[h % Colors.Length];
        return new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(from, 0),
                new GradientStop(to, 1)
            }
        };
    }
}
