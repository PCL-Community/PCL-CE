namespace PCL.Core.Platform;

/// <summary>
///     游戏窗口交互。具体实现由 UI 层注册到 <see cref="PlatformServices.WindowInterop" />；
///     未注册或当前桌面环境不支持时，业务层会跳过窗口相关操作。
/// </summary>
public interface IWindowInterop
{
    /// <summary>查找属于指定进程的游戏窗口；未找到时返回 <c>null</c>。</summary>
    (nint Handle, string Title)? TryFindGameWindow(int processId);

    void SetWindowTitle(nint handle, string title);

    void MaximizeWindow(nint handle);
}