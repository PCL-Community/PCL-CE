using PCL.Core.App.Tasks.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks.Implementations;

/// <summary>
/// 
/// </summary>
/// <param name="title"></param>
public class BaseParallelCompositeTask(string title, List<ITask> steps) : BaseProgressiveTask(title)
{
    private List<ITask> _Steps { get; } = steps;


    /// <inheritdoc />
    public override async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        var stepProgress = new double[_Steps.Count];
        object progressLock = new();

        var tasks = _Steps.Select((step, index) =>
            Task.Run(async () =>
            {
                if (step is IProgressiveTask pTask)
                {
                    pTask.ProgressChanged += (val) =>
                    {
                        stepProgress[index] = val;
                        UpdateTotalProgress();
                    };
                }

                await step.ExecuteAsync(cancelToken).ConfigureAwait(false);
                stepProgress[index] = 1.0; // finished
                UpdateTotalProgress();
            }, cancelToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        ReportStateChange(TaskState.Success, "完成");

        return;

        void UpdateTotalProgress()
        {
            lock (progressLock)
            {
                var total = stepProgress.Sum() / _Steps.Count;
                ReportProgressChange(total);
            }
        }
    }
}