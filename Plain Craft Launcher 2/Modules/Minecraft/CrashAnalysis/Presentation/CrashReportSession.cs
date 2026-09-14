using PCL.Core.Logging;

namespace PCL;

/// <summary>
/// 已生成、但尚未被丢弃的错误报告。
/// 弹窗收起后由它持有报告，右下角入口与独立窗口都从这里取数据，
/// 因此弹窗、入口与窗口之间不需要互相持有引用。
/// </summary>
internal static class CrashReportSession
{
    private static CrashReportSnapshot? _report;
    private static CrashReportWindow? _window;

    /// <summary>
    /// 当前报告或独立窗口状态发生变化。
    /// 始终在 UI 线程触发。
    /// </summary>
    public static event Action? Changed;

    /// <summary>
    /// 是否存在尚未被丢弃的错误报告。
    /// </summary>
    public static bool HasReport => _report is not null;

    /// <summary>
    /// 当前错误报告的标题。
    /// </summary>
    public static string Title => _report?.Title ?? string.Empty;

    /// <summary>
    /// 当前错误报告在收起入口上显示的名称。
    /// </summary>
    public static string EntryLabel => _report?.EntryLabel ?? string.Empty;

    /// <summary>
    /// 收起错误报告：保留报告，并通知界面显示右下角入口。
    /// </summary>
    public static void Collapse(CrashReportSnapshot report)
    {
        // 先记录报告本身，再通知界面。这样即使界面尚未就绪也不会丢失数据，
        // 入口在加载时会主动读取一次当前状态。
        _report = report;
        RunInUi(() =>
        {
            _CloseWindow();
            Changed?.Invoke();
        });
        ModBase.Log("[Crash] 错误报告已收起，可随时在独立窗口中重新打开");
    }

    /// <summary>
    /// 在独立窗口中打开当前报告。若窗口已打开，则将其激活。
    /// </summary>
    public static void OpenWindow()
    {
        var report = _report;
        if (report is null)
            return;

        RunInUi(() => _OpenWindow(report));
    }

    /// <summary>
    /// 关闭已经打开的独立窗口，但保留报告本身。
    /// </summary>
    public static void CloseWindow()
    {
        RunInUi(_CloseWindow);
    }

    /// <summary>
    /// 丢弃当前报告，并关闭已经打开的独立窗口。
    /// </summary>
    public static void Discard()
    {
        _report = null;
        RunInUi(() =>
        {
            _CloseWindow();
            Changed?.Invoke();
        });
    }

    private static void _OpenWindow(CrashReportSnapshot report)
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        try
        {
            var window = new CrashReportWindow(report);
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_window, window))
                    _window = null;
            };
            _window = window;
            window.Show();
        }
        catch (Exception ex)
        {
            _window = null;
            LogWrapper.Error(ex, "Crash", "打开错误报告窗口失败");
        }
    }

    private static void _CloseWindow()
    {
        var window = _window;
        _window = null;
        if (window is null)
            return;

        try
        {
            window.Close();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "关闭错误报告窗口失败");
        }
    }

    /// <summary>
    /// 将界面操作切换到 UI 线程。
    /// 应用尚未启动时不执行任何操作，此时界面就绪后会自行同步状态。
    /// </summary>
    private static void RunInUi(Action action)
    {
        try
        {
            ModBase.RunInUi(action);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "更新错误报告状态失败");
        }
    }
}
