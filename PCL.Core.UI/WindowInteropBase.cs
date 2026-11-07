using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>游戏窗口交互的共享异常兜底；具体平台只实现查找、改名和最大化。</summary>
public abstract class WindowInteropBase : IWindowInterop
{
    protected const string ModuleName = "WindowInterop";

    public (nint Handle, string Title)? TryFindGameWindow(int processId)
    {
        try
        {
            return FindGameWindowCore(processId);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ModuleName, $"查找游戏窗口失败：{ex.Message}");
            return null;
        }
    }

    public void SetWindowTitle(nint handle, string title)
    {
        try
        {
            SetWindowTitleCore(handle, title);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ModuleName, $"设置窗口标题失败：{ex.Message}");
        }
    }

    public void MaximizeWindow(nint handle)
    {
        try
        {
            MaximizeWindowCore(handle);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ModuleName, $"最大化窗口失败：{ex.Message}");
        }
    }

    protected abstract (nint Handle, string Title)? FindGameWindowCore(int processId);
    protected abstract void SetWindowTitleCore(nint handle, string title);
    protected abstract void MaximizeWindowCore(nint handle);
}
