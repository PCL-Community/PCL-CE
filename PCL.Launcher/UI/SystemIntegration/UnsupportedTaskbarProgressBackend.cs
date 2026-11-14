using Avalonia;

namespace PCL.UI.SystemIntegration;

/// <summary>当前平台没有可用的任务栏进度实现；不伪造进度。</summary>
internal sealed class UnsupportedTaskbarProgressBackend : ITaskbarProgressBackend
{
    public static readonly UnsupportedTaskbarProgressBackend Instance = new();

    public void Update(TaskbarProgressState state, double progress)
    {
    }

    public void SetThumbnailClip(Thickness margin)
    {
    }
}