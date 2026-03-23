using System.Windows.Controls;

namespace PCL.Core.UI.Controls.Dialog;

/// <summary>
/// 弹窗实现的基类，所有实现需要基于 <see cref="DialogBase"/>
/// </summary>
public class DialogBase : Grid
{
    protected DialogManager? DManager;
    protected void Close(object result)
    {
        DManager.SetResult(result);
    }
}
