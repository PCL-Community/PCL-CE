using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;

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
    /// 由于取消此 <see cref="TaskModel"/> 所属的任务
    /// </summary>
    public required CancellationTokenSource Token { get; init; }

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
    public ObservableCollection<TaskModel>? Steps { get; internal set; }

    /// <summary>
    /// Is have steps
    /// </summary>
    public required bool HasSteps { get; init; }
}
