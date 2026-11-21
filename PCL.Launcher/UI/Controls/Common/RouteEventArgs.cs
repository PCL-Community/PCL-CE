namespace PCL.UI.Controls.Common;

/// <summary>
///     单选/复选控件的事件参数：记录事件是否已被处理，以及是否由鼠标触发。
/// </summary>
public sealed class RouteEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool handled;
    public bool raiseByMouse = raiseByMouse;
}