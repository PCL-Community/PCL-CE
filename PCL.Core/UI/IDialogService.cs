using System.Threading.Tasks;
using PCL.Core.UI.Dialogs;

namespace PCL.Core.UI;

/// <summary>弹窗服务：接收数据化的弹窗请求，返回用户的选择结果。</summary>
public interface IDialogService
{
    /// <summary>同步显示弹窗；仅当请求要求等待时才阻塞调用线程。</summary>
    DialogViewModel Show(DialogRequest request);

    /// <summary>异步显示弹窗，等待用户关闭后返回结果。</summary>
    Task<DialogResult> ShowAsync(DialogRequest request);
}
