using PCL.Core.Logging;

namespace PCL.Core.UI.Animation.Core;

/// <summary>观察动画系统的 fire-and-forget 任务，避免异常留到 finalizer 才被报告。</summary>
internal static class AnimationTaskObserver
{
    public static void Observe(Task task, string message)
    {
        _ = ObserveCore(task, message);
    }

    private static async Task ObserveCore(Task task, string message)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Animation", message);
        }
    }
}