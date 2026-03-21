using System.Threading.Tasks;

namespace PCL.Core.UI.Controls.Dialog;

/// <summary>
/// 负责弹窗回调所需数据的存储
/// </summary>
/// <typeparam name="TResult"></typeparam>
public class DialogHandler<TResult>
{
    public required TaskCompletionSource<TResult> TaskCallback { get; set; }
}
