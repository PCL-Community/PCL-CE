#if DEBUG
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.Navigation;

namespace PCL.UI.SystemIntegration;

/// <summary>
///     调试构建的页面巡检工具：按 <c>PCL_DEBUG_SMOKE_PAGES</c> 导航并停留，供自动化截图逐页核对。
/// </summary>
internal static class DebugPageSmoke
{
    private const int StartupDelayMilliseconds = 4000;

    public static void TryStart()
    {
        var spec = Environment.GetEnvironmentVariable("PCL_DEBUG_SMOKE_PAGES");
        if (string.IsNullOrWhiteSpace(spec)) return;

        var dwell = int.TryParse(Environment.GetEnvironmentVariable("PCL_DEBUG_SMOKE_DWELL"), out var parsed)
            ? Math.Max(500, parsed)
            : 2500;

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Console.Out.WriteLine($"[SMOKE] UI 未处理异常：{e.Exception}");
            Console.Out.Flush();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Out.WriteLine($"[SMOKE] 域未处理异常：{e.ExceptionObject}");
            Console.Out.Flush();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Out.WriteLine($"[SMOKE] 未观测任务异常：{e.Exception}");
            Console.Out.Flush();
        };

        var pages = spec.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _ = RunAsync(pages, dwell);
    }

    private static async Task RunAsync(string[] pages, int dwellMilliseconds)
    {
        await Task.Delay(StartupDelayMilliseconds);

        foreach (var raw in pages)
        {
            if (!TryParse(raw, out var stack))
            {
                LogWrapper.Warn("UI", $"[SMOKE] 无法解析页面标识：{raw}");
                continue;
            }

            try
            {
                PageRouter.Navigate(stack!);
                Console.Out.WriteLine($"[SMOKE] enter={raw}");
                Console.Out.Flush();
                await Task.Delay(dwellMilliseconds);
                var state = PageRouter.CurrentState;
                var line = $"[SMOKE] page={raw} title={state?.Title} secondary={state?.IsSecondary} tab={state?.Tab} leftWidth={state?.LeftWidth}";
                LogWrapper.Info("UI", line);
                Console.Out.WriteLine(line);
                Console.Out.Flush();
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "UI", $"[SMOKE] 导航失败：{raw}");
                Console.Out.WriteLine($"[SMOKE] 导航失败 {raw}：{ex}");
                Console.Out.Flush();
            }
        }

        LogWrapper.Info("UI", "[SMOKE] 巡检结束");
        Console.Out.WriteLine("[SMOKE] done");
        Console.Out.Flush();
    }

    private static bool TryParse(string raw, out PageStackData? stack)
    {
        stack = null;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;
        if (!Enum.TryParse<PageType>(parts[0], ignoreCase: true, out var page)) return false;
        var subType = PageSubType.Default;
        if (parts.Length > 1 && !Enum.TryParse(parts[1], ignoreCase: true, out subType)) return false;
        stack = new PageStackData(page, subType);
        return true;
    }
}
#endif
