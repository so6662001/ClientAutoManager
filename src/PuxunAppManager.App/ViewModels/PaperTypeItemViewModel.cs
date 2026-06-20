using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using PuxunAppManager.Core.PaperForms;

namespace PuxunAppManager.App.ViewModels;

/// <summary>宿主接口：纸型列表行把编辑/删除回调给纸型管理 ViewModel。</summary>
public interface IPaperActionHost
{
    Task EditAsync(PaperTypeItemViewModel item);
    Task DeleteAsync(PaperTypeItemViewModel item);
}

public sealed partial class PaperTypeItemViewModel : ViewModelBase
{
    private readonly IPaperActionHost _host;

    public PaperTypeItemViewModel(PaperType model, IPaperActionHost host)
    {
        _host = host;
        Model = model;
    }

    public PaperType Model { get; }

    public string Name => Model.Name;
    public bool IsBuiltIn => Model.IsBuiltIn;
    public bool CanModify => !Model.IsBuiltIn;

    public string CategoryText => Model.IsBuiltIn
        ? "系统内置"
        : Model.Category switch
        {
            PaperCategory.TwoEqual => "一式二等份",
            PaperCategory.ThreeEqual => "一式三等份",
            _ => "自定义"
        };

    public string SizeText => $"{Model.WidthMm:0.#} × {Model.HeightMm:0.#}";

    public string PartsText => Model.IsBuiltIn || Model.Parts <= 1 ? "—" : Model.Parts.ToString();

    public string PartHeightText =>
        (Model.IsBuiltIn || Model.Parts <= 1) ? "—" : $"{Model.PartHeightMm:0.#}";

    public IBrush TagBackground => new SolidColorBrush(Color.Parse(
        Model.IsBuiltIn ? "#eceef0" :
        Model.Category switch
        {
            PaperCategory.TwoEqual => "#e5f0fb",
            PaperCategory.ThreeEqual => "#f0e8fb",
            _ => "#e6f4ec"
        }));

    public IBrush TagForeground => new SolidColorBrush(Color.Parse(
        Model.IsBuiltIn ? "#5f6368" :
        Model.Category switch
        {
            PaperCategory.TwoEqual => "#0067c0",
            PaperCategory.ThreeEqual => "#7c3aed",
            _ => "#0f8a4f"
        }));

    [RelayCommand]
    private Task Edit() => _host.EditAsync(this);

    [RelayCommand]
    private Task Delete() => _host.DeleteAsync(this);
}
