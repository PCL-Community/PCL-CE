namespace PCL;

/// <summary>
///     用于储存 RaiseByMouse 的 EventArgs。
/// </summary>
public sealed class RouteEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool handled = false;
    public bool raiseByMouse = raiseByMouse;
}