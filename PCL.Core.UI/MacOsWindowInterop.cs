namespace PCL.Core.UI;

/// <summary>macOS 当前没有游戏窗口查找/改名实现，明确返回不支持。</summary>
public sealed class MacOsWindowInterop : WindowInteropBase
{
    protected override (nint Handle, string Title)? FindGameWindowCore(int processId)
    {
        return null;
    }

    protected override void SetWindowTitleCore(nint handle, string title)
    {
    }

    protected override void MaximizeWindowCore(nint handle)
    {
    }
}