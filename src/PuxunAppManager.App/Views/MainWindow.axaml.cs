using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using PuxunAppManager.App.ViewModels;

namespace PuxunAppManager.App.Views;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.OpenSettingsAction = OpenSettings;
    }

    private void OpenSettings()
    {
        try
        {
            var settingsVm = App.Services.GetRequiredService<SettingsViewModel>();
            settingsVm.ThemeChangeRequested += App.ApplyTheme;
            var win = new SettingsWindow { DataContext = settingsVm };
            win.ShowDialog(this);
        }
        catch
        {
            // 设置窗口打开失败不应导致主程序异常
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose) return;
        if (DataContext is not MainWindowViewModel vm || !vm.HasRunningOperations) return;

        // 有安装进行中：弹确认（提示词第 9.12 条）
        e.Cancel = true;
        var confirmed = await ConfirmCloseAsync();
        if (confirmed)
        {
            _forceClose = true;
            Close();
        }
    }

    private async Task<bool> ConfirmCloseAsync()
    {
        var dialog = new Window
        {
            Title = "确认退出",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var ok = new Button { Content = "仍然退出", IsDefault = false, Classes = { "warn" } };
        var cancel = new Button { Content = "继续等待", IsDefault = true, Classes = { "primary" } };
        bool result = false;
        ok.Click += (_, _) => { result = true; dialog.Close(); };
        cancel.Click += (_, _) => { result = false; dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = "有安装/修复任务正在进行，退出可能导致安装中断。确定退出吗？",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { ok, cancel }
                }
            }
        };

        await dialog.ShowDialog(this);
        return result;
    }
}
