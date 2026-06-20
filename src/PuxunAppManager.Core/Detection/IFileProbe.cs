namespace PuxunAppManager.Core.Detection;

/// <summary>文件探测抽象（存在性 + 哈希 + 环境变量展开），便于测试替换。</summary>
public interface IFileProbe
{
    /// <summary>展开路径中的环境变量（%ProgramFiles% 等）。</summary>
    string ExpandPath(string path);

    bool FileExists(string expandedPath);

    /// <summary>计算文件 SHA256（十六进制小写）。文件不存在或读取失败返回 null。</summary>
    Task<string?> ComputeSha256Async(string expandedPath, CancellationToken ct);
}
