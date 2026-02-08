using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PCL.Core.App.Tasks;

public partial class TaskStepModel : ObservableObject, IDisposable
{
    /// <summary>
    /// 任务是否支持进度
    /// </summary>
    public required bool SupportProgress { get; init; }

    /// <summary>
    /// 任务当前状态信息
    /// </summary>
    [ObservableProperty] private string _message = string.Empty;

    /// <summary>
    /// 任务当前进度，<see cref="SupportProgress"/> 为 <see langword="true"/> 时生效
    /// </summary>
    [ObservableProperty] private double _progress;

    private event Action? Cleanup;

    internal void RegisterCleanup(Action cleanupAction)
    {
        Cleanup = cleanupAction;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Cleanup?.Invoke();
        Cleanup = null;
    }
}