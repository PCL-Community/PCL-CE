using System.Threading;
using System.Threading.Tasks;
using PCL.Core.UI.Dialogs;

namespace PCL.Core.UI;

/// <summary>
/// 弹窗服务：接收数据化的弹窗请求，返回用户的选择结果。<br /> 仅用于致命错误兜底的对话框见
/// <see cref="PCL.Core.Platform.IFatalDialogService" />。
/// </summary>
public interface IDialogService
{
    /// <summary>异步显示弹窗，等待用户关闭后返回结果。</summary>
    Task<DialogResult> ShowAsync(
        DialogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>显示不需要返回结果的通知弹窗。</summary>
    void ShowNotification(DialogRequest request);
}