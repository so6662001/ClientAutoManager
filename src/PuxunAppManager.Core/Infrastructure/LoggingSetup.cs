using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace PuxunAppManager.Core.Infrastructure;

/// <summary>构建写入文件(按天滚动)+控制台的 ILoggerFactory。</summary>
public static class LoggingSetup
{
    public static ILoggerFactory CreateLoggerFactory(string logDirectory, bool verbose = false)
    {
        try { Directory.CreateDirectory(logDirectory); } catch { /* ignore */ }

        var logFile = Path.Combine(logDirectory, "app-.log");

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(verbose ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return new SerilogLoggerFactory(logger, dispose: true);
    }
}
