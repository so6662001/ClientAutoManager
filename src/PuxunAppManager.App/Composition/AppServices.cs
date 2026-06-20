using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PuxunAppManager.App.Paper;
using PuxunAppManager.App.ViewModels;
using PuxunAppManager.Core.Backend;
using PuxunAppManager.Core.PaperForms;
using PuxunAppManager.Core.Configuration;
using PuxunAppManager.Core.Detection;
using PuxunAppManager.Core.Infrastructure;
using PuxunAppManager.Core.Installation;
using PuxunAppManager.Core.Models;
using PuxunAppManager.Core.Security;
using PuxunAppManager.Core.Services;

namespace PuxunAppManager.App.Composition;

/// <summary>DI 组合根：装配 Core 全部服务与 ViewModel。</summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        // 配置（先用临时 logger 加载，再据此建立正式 logger）
        using var bootstrapFactory = LoggerFactory.Create(b => b.AddConsole());
        var configService = new ConfigService(bootstrapFactory.CreateLogger<ConfigService>());
        var config = configService.Load();

        var logDir = configService.ResolveDirectory(config.Paths.LogDir);
        var downloadDir = configService.ResolveDirectory(config.Paths.DownloadDir);
        var manifestCachePath = configService.ResolveFilePath(config.Paths.ManifestCache);
        var paperTypesPath = configService.ResolveFilePath(@"%AppData%\PuxunAppManager\paper-types.json");

        var loggerFactory = LoggingSetup.CreateLoggerFactory(logDir);

        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        services.AddSingleton<IConfigService>(configService);
        services.AddSingleton(config);
        services.AddSingleton(config.Backend);

        // 检测
        services.AddSingleton<IRegistryReader, WindowsRegistryReader>();
        services.AddSingleton<IFileProbe, FileProbe>();
        services.AddSingleton<IDetectionService, DetectionService>();

        // 安全
        services.AddSingleton<ISecurityVerifier, SecurityVerifier>();

        // 安装
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IInstallerService, InstallerService>();

        // 后端
        services.AddSingleton<IManifestCache>(sp =>
            new ManifestCache(manifestCachePath, sp.GetRequiredService<ILogger<ManifestCache>>()));

        services.AddSingleton<HttpClient>(_ => new HttpClient());
        services.AddSingleton<IBackendClient>(sp => new RestBackendClient(
            sp.GetRequiredService<HttpClient>(),
            config.Backend,
            downloadDir,
            sp.GetRequiredService<ILogger<RestBackendClient>>()));

        services.AddSingleton<IAppManagerService, AppManagerService>();

        // 纸型管理
        services.AddSingleton<IPaperTypeMetadataStore>(sp =>
            new PaperTypeMetadataStore(paperTypesPath, sp.GetRequiredService<ILogger<PaperTypeMetadataStore>>()));
        services.AddSingleton<IPaperFormService, WindowsPaperFormService>();
        services.AddSingleton<ElevatedPaperFormExecutor>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PaperFormsViewModel>();

        return services.BuildServiceProvider();
    }
}
