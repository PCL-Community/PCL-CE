using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 可观察的任务模型<br/>
/// <b>NOTE</b>: 请勿自行修改任何 observable 属性
/// </summary>
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
    [ObservableProperty] private TaskState _state = TaskState.Waiting;

    /// <summary>
    /// 任务当前状态信息
    /// </summary>
    [ObservableProperty] private string _stateMessage = string.Empty;

    /// <summary>
    /// 任务当前进度，<see cref="SupportProgress"/> 为 <see langword="true"/> 时有效
    /// </summary>
    [ObservableProperty] private double _progress = 0.0;

    private static readonly Action _EmptyAction = (static () => {});

    /// <summary>
    /// 取消任务时触发的事件，值为 <see langword="null"/> 表示不支持取消
    /// </summary>
    public required Action? OnCancel { private get; init; }

    /// <summary>
    /// 取消任务命令
    /// </summary>
    public RelayCommand Cancel
    {
        get => field ??= new RelayCommand(OnCancel ?? _EmptyAction, () => OnCancel != null);
    } = null!;

    /// <summary>
    /// 暂停任务时触发的事件，值为 <see langword="null"/> 表示不支持暂停
    /// </summary>
    public required Action? OnPause { private get; init; }

    /// <summary>
    /// 暂停任务命令
    /// </summary>
    public RelayCommand Pause
    {
        get => field ??= new RelayCommand(OnPause ?? _EmptyAction, () => OnPause != null);
    } = null!;

    /// <summary>
    /// 任务是否为任务组，即是否存在子任务
    /// </summary>
    [ObservableProperty] private bool _isGroup;

    /// <summary>
    /// 子任务模型
    /// </summary>
    public ObservableCollection<TaskModel> Children { get; } = [];

    public TaskModel()
    {
        Children.CollectionChanged += (sender, _) =>
        {
            if (sender is ObservableCollection<TaskModel> c) IsGroup = c.Count > 0;
        };
    }
}
