using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PuxunAppManager.App.Paper;
using PuxunAppManager.Core.Models;
using PuxunAppManager.Core.PaperForms;

namespace PuxunAppManager.App.ViewModels;

public sealed partial class PaperFormsViewModel : ViewModelBase, IPaperActionHost
{
    private readonly IPaperFormService _service;
    private readonly IPaperTypeMetadataStore _metadata;
    private readonly ElevatedPaperFormExecutor _executor;
    private readonly ILogger<PaperFormsViewModel> _logger;

    public PaperFormsViewModel(
        IPaperFormService service,
        IPaperTypeMetadataStore metadata,
        ElevatedPaperFormExecutor executor,
        ILogger<PaperFormsViewModel> logger)
    {
        _service = service;
        _metadata = metadata;
        _executor = executor;
        _logger = logger;

        SelectCategory("2");
        Refresh();
    }

    /// <summary>设计器无参构造。</summary>
    public PaperFormsViewModel()
    {
        _service = null!;
        _metadata = null!;
        _executor = null!;
        _logger = null!;
    }

    public ObservableCollection<PaperTypeItemViewModel> Forms { get; } = new();

    // 表单字段
    [ObservableProperty] private PaperCategory _selectedCategory = PaperCategory.TwoEqual;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private double _widthMm = 210;
    [ObservableProperty] private double _heightMm = 140;
    [ObservableProperty] private double _marginTop;
    [ObservableProperty] private double _marginBottom;
    [ObservableProperty] private double _marginLeft;
    [ObservableProperty] private double _marginRight;

    [ObservableProperty] private bool _isCustom;
    [ObservableProperty] private string _partHint = string.Empty;

    // 编辑/提交状态
    [ObservableProperty] private string? _editingOriginalName;
    [ObservableProperty] private string _submitText = "添加";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isBusy;

    // 重复/结果提示
    [ObservableProperty] private bool _hasDuplicate;
    [ObservableProperty] private string _duplicateMessage = string.Empty;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _resultMessage = string.Empty;
    [ObservableProperty] private bool _resultIsError;

    [ObservableProperty] private string _statusText = string.Empty;

    partial void OnWidthMmChanged(double value) => UpdateHint();
    partial void OnHeightMmChanged(double value) => UpdateHint();

    [RelayCommand]
    private void SelectCategory(string code)
    {
        SelectedCategory = code switch
        {
            "3" => PaperCategory.ThreeEqual,
            "c" => PaperCategory.Custom,
            _ => PaperCategory.TwoEqual
        };
        IsCustom = SelectedCategory == PaperCategory.Custom;
        UpdateHint();
    }

    private void UpdateHint()
    {
        if (IsCustom) { PartHint = string.Empty; return; }
        int parts = PaperType.PartsOf(SelectedCategory);
        PartHint = $"注册为整张纸尺寸 {WidthMm:0.#} × {HeightMm:0.#} mm，按 {parts} 等份切分，每份 {WidthMm:0.#} × {(HeightMm / parts):0.#} mm";
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_service is null) return;
        Forms.Clear();
        var list = _service.ListForms()
            .OrderByDescending(p => !p.IsBuiltIn) // 自建在前
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var p in list)
            Forms.Add(new PaperTypeItemViewModel(p, this));

        int user = Forms.Count(f => f.CanModify);
        int sys = Forms.Count - user;
        StatusText = $"打印后台服务正常 · 共 {Forms.Count} 个纸型（{user} 个自建 / {sys} 个系统内置）";
    }

    private PaperType BuildFromForm()
    {
        int parts = PaperType.PartsOf(SelectedCategory);
        return new PaperType
        {
            Name = Name?.Trim() ?? string.Empty,
            Category = SelectedCategory,
            WidthMm = WidthMm,
            HeightMm = HeightMm,
            Parts = parts,
            ImageableMargins = IsCustom
                ? new Margins { TopMm = MarginTop, BottomMm = MarginBottom, LeftMm = MarginLeft, RightMm = MarginRight }
                : Margins.Zero
        };
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy) return;
        HasDuplicate = false;
        HasResult = false;

        var candidate = BuildFromForm();

        // 1) 校验
        var validate = PaperValidation.Validate(candidate);
        if (!validate.Success)
        {
            ShowDuplicate(validate.Message ?? "输入有误。");
            return;
        }

        // 2) 判重（先名称、再尺寸；编辑排除自身）
        var existing = _service.ListForms();
        var (kind, conflict) = PaperUniqueness.Check(candidate, existing, EditingOriginalName);
        if (kind == DuplicateKind.Name)
        {
            ShowDuplicate($"纸型「{candidate.Name}」已存在，不予添加。");
            return;
        }
        if (kind == DuplicateKind.Size)
        {
            ShowDuplicate($"已存在相同尺寸的纸型「{conflict!.Name}」({conflict.WidthMm:0.#}×{conflict.HeightMm:0.#}，{conflict.Parts} 等份)，不予添加。");
            return;
        }

        // 3) 提权执行写操作
        IsBusy = true;
        try
        {
            var op = EditingOriginalName is null ? PaperOperation.Add : PaperOperation.Update;
            var request = new PaperFormRequest { Operation = op, OriginalName = EditingOriginalName, Paper = candidate };
            var result = await _executor.ExecuteAsync(request, CancellationToken.None).ConfigureAwait(true);

            if (result.Success)
            {
                // 4) 写入元数据（普通进程，落当前用户配置）
                PersistMetadata(candidate, EditingOriginalName);
                ShowResult(EditingOriginalName is null ? "纸型已添加。" : "纸型已保存。", isError: false);
                ResetForm();
                Refresh();
            }
            else
            {
                ShowResult(result.Message ?? "操作失败。", isError: true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PersistMetadata(PaperType paper, string? originalName)
    {
        var meta = new PaperMetadata
        {
            Category = paper.Category,
            Parts = paper.Parts,
            TopMm = paper.ImageableMargins.TopMm,
            BottomMm = paper.ImageableMargins.BottomMm,
            LeftMm = paper.ImageableMargins.LeftMm,
            RightMm = paper.ImageableMargins.RightMm
        };
        if (originalName is not null && !PaperUniqueness.NameEquals(originalName, paper.Name))
            _metadata.Remove(originalName); // 改名：移除旧元数据
        _metadata.Upsert(paper.Name, meta);
    }

    [RelayCommand]
    private void CancelEdit() => ResetForm();

    private void ResetForm()
    {
        EditingOriginalName = null;
        IsEditing = false;
        SubmitText = "添加";
        Name = string.Empty;
        MarginTop = MarginBottom = MarginLeft = MarginRight = 0;
        HasDuplicate = false;
    }

    // ===== IPaperActionHost =====

    public Task EditAsync(PaperTypeItemViewModel item)
    {
        var m = item.Model;
        if (m.IsBuiltIn)
        {
            ShowResult("系统内置纸型不可修改。", isError: true);
            return Task.CompletedTask;
        }

        EditingOriginalName = m.Name;
        IsEditing = true;
        SubmitText = "保存修改";
        Name = m.Name;
        WidthMm = m.WidthMm;
        HeightMm = m.HeightMm;
        MarginTop = m.ImageableMargins.TopMm;
        MarginBottom = m.ImageableMargins.BottomMm;
        MarginLeft = m.ImageableMargins.LeftMm;
        MarginRight = m.ImageableMargins.RightMm;
        SelectCategory(m.Category switch
        {
            PaperCategory.ThreeEqual => "3",
            PaperCategory.Custom => "c",
            _ => "2"
        });
        HasDuplicate = false;
        HasResult = false;
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(PaperTypeItemViewModel item)
    {
        if (IsBusy) return;
        var m = item.Model;
        if (m.IsBuiltIn)
        {
            ShowResult("系统内置纸型不可删除。", isError: true);
            return;
        }

        IsBusy = true;
        try
        {
            var request = new PaperFormRequest { Operation = PaperOperation.Delete, OriginalName = m.Name, Paper = m };
            var result = await _executor.ExecuteAsync(request, CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                _metadata.Remove(m.Name);
                ShowResult($"已删除纸型「{m.Name}」。", isError: false);
                if (PaperUniqueness.NameEquals(EditingOriginalName ?? string.Empty, m.Name)) ResetForm();
                Refresh();
            }
            else
            {
                ShowResult(result.Message ?? "删除失败。", isError: true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowDuplicate(string message)
    {
        HasDuplicate = true;
        DuplicateMessage = message;
    }

    private void ShowResult(string message, bool isError)
    {
        HasResult = true;
        ResultMessage = message;
        ResultIsError = isError;
    }
}
