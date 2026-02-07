using PCL.Core.App.Tasks.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks.Implementations;

public abstract class BaseTask(string title) : ITask
{
    /// <inheritdoc />
    public string Title { get; } = title;

    /// <inheritdoc />
    public event TaskStateEvent? StateChanged;

    /// <inheritdoc />
    public abstract Task ExecuteAsync(CancellationToken cancelToken = default);

    protected void ReportStateChange(TaskState state, string message)
    {
        StateChanged?.Invoke(state, message);
    }
}