using Avalonia;

namespace PCL.UI.SystemIntegration;

internal interface ITaskbarProgressBackend
{
    void Update(TaskbarProgressState state, double progress);
    void SetThumbnailClip(Thickness margin);
}
