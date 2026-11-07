using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.Logging;
using PCL.UI.Controls;

namespace PCL.UI.SystemIntegration;

/// <summary>
///     Debug-only 布局探针：页面进入后检查横向溢出，便于在 810×470 等尺寸下收集证据。
///     仅当 PCL_DEBUG_LAYOUT=1 时输出，不参与正常功能。
/// </summary>
public static class LayoutProbe
{
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("PCL_DEBUG_LAYOUT") == "1";

    public static void CheckAfterLayout(Control page, string pageName)
    {
        if (!Enabled) return;
        Dispatcher.UIThread.Post(
            () => Check(page, pageName),
            DispatcherPriority.Loaded);
    }

    private static void Check(Control page, string pageName)
    {
        try
        {
            var scrolls = page.GetVisualDescendants().OfType<MyScrollViewer>().ToList();
            if (scrolls.Count == 0) return;

            for (var index = 0; index < scrolls.Count; index++)
            {
                var scroll = scrolls[index];
                var viewport = scroll.Viewport;
                var extent = scroll.Extent;
                var overflow = extent.Width > viewport.Width + 0.5d;
                LogWrapper.Debug(
                    "Layout",
                    $"[LAYOUT] page={pageName} index={index} "
                    + $"viewport={viewport.Width:0.#}x{viewport.Height:0.#} "
                    + $"extent={extent.Width:0.#}x{extent.Height:0.#} "
                    + $"horizontalOverflow={overflow}");
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Layout", $"布局探针失败：{ex.Message}");
        }
    }
}