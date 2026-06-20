using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Core.PaperForms;

/// <summary>
/// 本机纸型(打印服务器表单)读写服务。
/// - ListForms 普通权限即可。
/// - Add/Update/Delete 直接调用 winspool，<b>要求当前进程已具备管理员权限</b>
///   （由 App 层通过 UAC 提权子进程调用，本接口不负责提权）。
/// </summary>
public interface IPaperFormService
{
    /// <summary>列出本机全部纸型（系统内置 + 用户自建），合并等份/类别元数据。</summary>
    IReadOnlyList<PaperType> ListForms();

    /// <summary>AddForm（需管理员）。调用方应已完成校验与判重。</summary>
    OperationResult Add(PaperType paper);

    /// <summary>SetForm / 改名(删旧增新)（需管理员）。内置不可改。</summary>
    OperationResult Update(string originalName, PaperType paper);

    /// <summary>DeleteForm（需管理员）。内置不可删。</summary>
    OperationResult Delete(string name);
}
