using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.Backend;

/// <summary>
/// 后端适配层的 REST + JSON 默认实现。
///
/// ============================ 后端对接点 ============================
/// 若公司现有后端为其它形态(gRPC/SOAP)或字段命名不同，仅需在本类内部调整：
///   1. 请求 URL / HTTP 方法（见各 // === 后端对接点 === 处）
///   2. 响应 JSON → BackendProductManifest / InstallerPackage 的映射
/// 对外接口 IBackendClient 保持不变，UI 与检测逻辑无需改动。
/// ===================================================================
/// </summary>
public sealed class RestBackendClient : IBackendClient
{
    private readonly HttpClient _http;
    private readonly BackendConfig _config;
    private readonly string _downloadDir;
    private readonly ILogger<RestBackendClient> _logger;

    public RestBackendClient(
        HttpClient http, BackendConfig config, string downloadDir, ILogger<RestBackendClient> logger)
    {
        _http = http;
        _config = config;
        _downloadDir = downloadDir;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, config.TimeoutSeconds));
        ApplyAuthHeader();
    }

    private void ApplyAuthHeader()
    {
        try
        {
            switch (_config.AuthType?.ToLowerInvariant())
            {
                case "token":
                    if (!string.IsNullOrWhiteSpace(_config.AuthValue))
                        _http.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.AuthValue);
                    break;
                case "apikey":
                    if (!string.IsNullOrWhiteSpace(_config.AuthValue))
                        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", _config.AuthValue);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "设置鉴权头失败。");
        }
    }

    public async Task<IReadOnlyList<BackendProductManifest>> GetManifestAsync(CancellationToken ct)
    {
        return await RetryPolicy.ExecuteAsync(async token =>
        {
            // === 后端对接点：替换为现有后端的"产品清单"接口路径与响应结构 ===
            var url = CombineUrl(_config.BaseUrl, "products/manifest");
            using var resp = await _http.GetAsync(url, token).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var list = await resp.Content
                .ReadFromJsonAsync<List<BackendProductManifest>>(cancellationToken: token)
                .ConfigureAwait(false);

            return (IReadOnlyList<BackendProductManifest>)(list ?? new List<BackendProductManifest>());
        }, _config.MaxRetries, _logger, ct);
    }

    public async Task<InstallerPackage> GetInstallerAsync(string productId, string version, CancellationToken ct)
    {
        return await RetryPolicy.ExecuteAsync(async token =>
        {
            // === 后端对接点：替换为现有后端的"安装包信息"接口 ===
            var url = CombineUrl(_config.BaseUrl, $"products/{Uri.EscapeDataString(productId)}/installer?version={Uri.EscapeDataString(version)}");
            using var resp = await _http.GetAsync(url, token).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var dto = await resp.Content
                .ReadFromJsonAsync<ManifestInstaller>(cancellationToken: token)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("后端返回空的安装包信息。");

            return ManifestMapper.ToInstallerPackage(productId, version, dto);
        }, _config.MaxRetries, _logger, ct);
    }

    public async Task<string> DownloadInstallerAsync(
        InstallerPackage pkg, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(_downloadDir);
        var ext = pkg.InstallerType == InstallerType.Msi ? ".msi" : ".exe";
        var targetPath = Path.Combine(_downloadDir, $"{pkg.ProductId}-{pkg.Version}{ext}");
        var tmpPath = targetPath + ".part";

        await RetryPolicy.ExecuteAsync(async token =>
        {
            // === 后端对接点：若下载需特殊鉴权/重定向，在此调整 ===
            using var resp = await _http
                .GetAsync(pkg.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? pkg.SizeBytes;
            if (total <= 0) total = -1;

            await using var input = await resp.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using (var output = new FileStream(
                tmpPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 1 << 20, useAsync: true))
            {
                var buffer = new byte[1 << 20];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    received += read;
                    progress.Report(new DownloadProgress(received, total));
                }
            }

            // 落地校验：大小（若已知）
            if (total > 0)
            {
                var actualSize = new FileInfo(tmpPath).Length;
                if (actualSize != total)
                    throw new IOException($"下载大小不一致：期望 {total}，实际 {actualSize}。");
            }

            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(tmpPath, targetPath);
            return true;
        }, _config.MaxRetries, _logger, ct);

        return targetPath;
    }

    private static string CombineUrl(string baseUrl, string path) =>
        $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
