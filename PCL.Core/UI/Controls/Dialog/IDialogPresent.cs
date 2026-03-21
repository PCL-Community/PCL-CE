using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.UI.Controls.Dialog;


/// <summary>
/// 消息框展示实现的抽象，允许自定义展示方式，方便测试以及……给 VB 用
/// </summary>
public interface IDialogPresent
{
    Task PresentAsync(DialogBase dialog, CancellationToken ct = default);
    Task DismissAsync(CancellationToken ct = default);
    bool IsPresenting { get; }
    DialogBase? CurrentDialog { get; }
}
