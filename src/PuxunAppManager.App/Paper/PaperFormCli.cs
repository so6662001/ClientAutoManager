using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PuxunAppManager.Core.PaperForms;

namespace PuxunAppManager.App.Paper;

/// <summary>
/// 提权子进程入口：当以 `--paper-op &lt;请求文件&gt; &lt;响应文件&gt;` 启动时，
/// 以管理员身份执行单次 winspool 写操作（增/改/删），不启动 UI。
/// 元数据(类别/等份)不在此进程写，避免落到管理员的用户配置目录。
/// </summary>
public static class PaperFormCli
{
    public const string Flag = "--paper-op";

    public static bool TryHandle(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length < 3 || args[0] != Flag) return false;

        var requestPath = args[1];
        var responsePath = args[2];
        var response = new PaperFormResponse { Success = false, ErrorCode = "UNKNOWN", Message = "未执行。" };

        try
        {
            var json = File.ReadAllText(requestPath);
            var request = JsonSerializer.Deserialize<PaperFormRequest>(json)
                          ?? throw new InvalidOperationException("请求内容为空。");

            // 子进程只做 winspool 写入；元数据用空实现。
            var service = new WindowsPaperFormService(
                new NullPaperTypeMetadataStore(),
                NullLogger<WindowsPaperFormService>.Instance);

            var result = request.Operation switch
            {
                PaperOperation.Add => service.Add(request.Paper),
                PaperOperation.Update => service.Update(request.OriginalName ?? request.Paper.Name, request.Paper),
                PaperOperation.Delete => service.Delete(request.OriginalName ?? request.Paper.Name),
                _ => Core.Models.OperationResult.Fail("UNKNOWN", "未知操作。")
            };

            response = new PaperFormResponse
            {
                Success = result.Success,
                ErrorCode = result.ErrorCode,
                Message = result.Message
            };
            exitCode = result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            response = new PaperFormResponse { Success = false, ErrorCode = "UNKNOWN", Message = ex.Message };
            exitCode = 1;
        }
        finally
        {
            try { File.WriteAllText(responsePath, JsonSerializer.Serialize(response)); }
            catch { /* 响应写入失败时仅靠退出码 */ }
        }

        return true;
    }

    /// <summary>子进程用的空元数据存储（仅 winspool 写入需要，不读写文件）。</summary>
    private sealed class NullPaperTypeMetadataStore : IPaperTypeMetadataStore
    {
        public PaperMetadata? Get(string name) => null;
        public void Upsert(string name, PaperMetadata meta) { }
        public void Remove(string name) { }
        public void Rename(string oldName, string newName) { }
        public System.Collections.Generic.IReadOnlyDictionary<string, PaperMetadata> All()
            => new System.Collections.Generic.Dictionary<string, PaperMetadata>();
    }
}
