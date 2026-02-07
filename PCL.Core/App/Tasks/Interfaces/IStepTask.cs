using System;
using System.Collections.Generic;

namespace PCL.Core.App.Tasks.Interfaces;

public interface IStepTask : ITask
{
    public event Action<ITask>? StepChanged;

    public List<ITask> Steps { get; init; }
}