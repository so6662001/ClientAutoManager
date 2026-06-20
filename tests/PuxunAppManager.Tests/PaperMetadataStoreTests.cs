using Microsoft.Extensions.Logging.Abstractions;
using PuxunAppManager.Core.PaperForms;

namespace PuxunAppManager.Tests;

public class PaperMetadataStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"px-paper-meta-{Guid.NewGuid():N}.json");

    private PaperTypeMetadataStore Create() =>
        new(_path, NullLogger<PaperTypeMetadataStore>.Instance);

    [Fact]
    public void Upsert_Get_RoundTrip_Persists()
    {
        var store = Create();
        store.Upsert("收银小票二联", new PaperMetadata { Category = PaperCategory.TwoEqual, Parts = 2 });

        // 新实例从磁盘加载
        var reloaded = Create();
        var meta = reloaded.Get("收银小票二联");
        Assert.NotNull(meta);
        Assert.Equal(PaperCategory.TwoEqual, meta!.Category);
        Assert.Equal(2, meta.Parts);
    }

    [Fact]
    public void Get_IsCaseAndSpaceInsensitive()
    {
        var store = Create();
        store.Upsert("Form A", new PaperMetadata { Category = PaperCategory.Custom, Parts = 1 });
        Assert.NotNull(store.Get("  form a "));
    }

    [Fact]
    public void Remove_Works()
    {
        var store = Create();
        store.Upsert("X", new PaperMetadata { Parts = 1 });
        store.Remove("X");
        Assert.Null(store.Get("X"));
    }

    [Fact]
    public void Rename_MovesMetadata()
    {
        var store = Create();
        store.Upsert("Old", new PaperMetadata { Category = PaperCategory.ThreeEqual, Parts = 3 });
        store.Rename("Old", "New");
        Assert.Null(store.Get("Old"));
        Assert.Equal(3, store.Get("New")!.Parts);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
