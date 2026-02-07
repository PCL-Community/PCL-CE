using PCL.Core.App.Tasks.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks.Implementations;

/// <summary>
/// Composite task base class, which contains multiple steps
/// </summary>
/// <remarks>
/// This class will execute each step sequentially
/// </remarks>
public class BaseCompositeTask(string title, List<ITask> steps) : BaseProgressiveTask(title), IStepTask
{
    /// <inheritdoc />
    public override async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        ReportStateChange(TaskState.Running, "Running...");
        var totalSteps = Steps.Count;
        var completedSteps = 0;

        foreach (var step in Steps)
        {
            cancelToken.ThrowIfCancellationRequested();

            if (step is IProgressiveTask pStep)
            {
                pStep.ProgressChanged += SubTaskProgress;
            }

            StepChanged?.Invoke(step);

            try
            {
                await step.ExecuteAsync(cancelToken).ConfigureAwait(false);
            }
            finally
            {
                if (step is IProgressiveTask pStepClean)
                {
                    pStepClean.ProgressChanged -= SubTaskProgress;
                }
            }

            completedSteps++;
            ReportProgressChange((double)completedSteps / totalSteps);
        }

        ReportStateChange(TaskState.Success, "完成");

        return;


        void SubTaskProgress(double progress)
        {
            var totalProgress = (completedSteps + progress) / totalSteps;
            ReportProgressChange(totalProgress);
        }
    }

    /// <inheritdoc />
    public event Action<ITask>? StepChanged;

    /// <inheritdoc />
    public List<ITask> Steps { get; init; } = steps;
}