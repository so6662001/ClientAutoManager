namespace PuxunAppManager.Core.PaperForms;

public enum PaperOperation { Add, Update, Delete }

/// <summary>提权子进程的写操作请求（序列化为临时 JSON 在进程间传递）。</summary>
public sealed class PaperFormRequest
{
    public PaperOperation Operation { get; set; }
    public string? OriginalName { get; set; }
    public PaperType Paper { get; set; } = new();
}

/// <summary>提权子进程的写操作结果。</summary>
public sealed class PaperFormResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}
