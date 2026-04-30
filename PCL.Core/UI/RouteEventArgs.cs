using System;

namespace PCL.Core.UI;


/// <summary>
/// 用于储存 RaiseByMouse 的 EventArgs。
/// </summary>
public sealed class RouteEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool Handled = false;
    public bool RaiseByMouse = raiseByMouse;
}
