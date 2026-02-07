using PCL.Core.App.Tasks.Interfaces;

namespace PCL.Core.App.Tasks.Implementations;

public abstract class BaseProgressiveTask(string title) : BaseTask(title), IProgressiveTask
{
    /// <inheritdoc />
    public event TaskProgressEvent? ProgressChanged;

    protected void ReportProgressChange(double progress)
    {
        ProgressChanged?.Invoke(progress);
    }
}