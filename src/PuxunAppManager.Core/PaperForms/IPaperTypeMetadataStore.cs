namespace PuxunAppManager.Core.PaperForms;

/// <summary>
/// 纸型"类别 + 等份数 + 边距"元数据持久化。
/// 因为 Windows 表单本身不存这些信息，需按名称额外保存到用户配置目录。
/// 写入发生在普通(用户)进程，确保落在当前用户的 AppData。
/// </summary>
public interface IPaperTypeMetadataStore
{
    PaperMetadata? Get(string name);
    void Upsert(string name, PaperMetadata meta);
    void Remove(string name);
    void Rename(string oldName, string newName);
    IReadOnlyDictionary<string, PaperMetadata> All();
}

public sealed class PaperMetadata
{
    public PaperCategory Category { get; set; }
    public int Parts { get; set; } = 1;
    public double TopMm { get; set; }
    public double BottomMm { get; set; }
    public double LeftMm { get; set; }
    public double RightMm { get; set; }
}
