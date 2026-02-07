using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace PCL.Core.App.Tasks;

public partial class TaskModel : ObservableObject
{
    /// <summary>
    /// 任务标题
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 任务是否支持进度
    /// </summary>
    public required bool SupportProgress { get; init; }

    /// <summary>
    /// 任务当前状态
    /// </summary>
    [ObservableProperty] private TaskState _state;

    /// <summary>
    /// 任务当前状态信息
    /// </summary>
    [ObservableProperty] private string _stateMessage = string.Empty;

    /// <summary>
    /// 任务当前进度，<see cref="SupportProgress"/> 为 <see langword="true"/> 时生效
    /// </summary>
    [ObservableProperty] private double _progress;

    /// <summary>
    /// Steps in this task
    /// </summary>
    public ObservableCollection<TaskModel> Steps { get; } = [];

    /// <summary>
    /// Is have steps
    /// </summary>
    public bool HasSteps => Steps.Count > 0;
}
