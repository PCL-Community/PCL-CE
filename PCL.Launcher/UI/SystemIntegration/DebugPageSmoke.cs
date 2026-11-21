#if DEBUG
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.Navigation;

namespace PCL.UI.SystemIntegration;

/// <summary>调试构建的页面巡检工具：按 <c>PCL_DEBUG_SMOKE_PAGES</c> 导航并停留，供自动化截图逐页核对。</summary>
internal static class DebugPageSmoke
{
    private const int StartupDelayMilliseconds = 4000;

    public static void TryStart()
    {
        var spec = Environment.GetEnvironmentVariable("PCL_DEBUG_SMOKE_PAGES");
        if (string.IsNullOrWhiteSpace(spec)) return;

        var dwell = int.TryParse(
            Environment.GetEnvironmentVariable("PCL_DEBUG_SMOKE_DWELL"),
            out var parsed)
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

        var pages = spec.Split(
            [','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
                await Console.Out.WriteLineAsync($"[SMOKE] enter={raw}");
                await Console.Out.FlushAsync();
                await Task.Delay(dwellMilliseconds);
                var state = PageRouter.CurrentState;
                var line =
                    $"[SMOKE] page={raw} "
                    + $"title={state?.Title} "
                    + $"secondary={state?.IsSecondary} "
                    + $"tab={state?.Tab} "
                    + $"leftWidth={state?.LeftWidth}";
                LogWrapper.Info("UI", line);
                await Console.Out.WriteLineAsync(line);
                await Console.Out.FlushAsync();
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "UI", $"[SMOKE] 导航失败：{raw}");
                await Console.Out.WriteLineAsync($"[SMOKE] 导航失败 {raw}：{ex}");
                await Console.Out.FlushAsync();
            }
        }

        LogWrapper.Info("UI", "[SMOKE] 巡检结束");
        await Console.Out.WriteLineAsync("[SMOKE] done");
        await Console.Out.FlushAsync();
    }

    private static bool TryParse(string raw, out PageRoute? stack)
    {
        stack = null;
        var parts = raw.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        if (!PageCatalog.TryParsePage(parts[0], out var page))
            return false;

        SubPageId? subPage = null;
        if (parts.Length > 1 && !PageCatalog.TryParseSubPage(page, parts[1], out subPage))
            return false;

        stack = new PageRoute(page, subPage);
        return true;
    }
}
#endif