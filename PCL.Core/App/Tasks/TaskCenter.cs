using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务中心，用于管理任务
/// </summary>
public static class TaskCenter
{
    private static readonly ConditionalWeakTable<ITask, TaskModel> _ModelMap = [];

    private static readonly ObservableCollection<TaskModel> _ModelCollection = [];

    /// <summary>
    /// 可观察的任务模型集合
    /// </summary>
    public static INotifyCollectionChanged Tasks => _ModelCollection;

    /// <summary>
    /// 注册响应式任务实例
    /// </summary>
    /// <param name="instance">任务实例</param>
    public static void Register(ITask instance)
    {
        if (Exists(instance)) throw new InvalidOperationException("Existent ITask instance");
        // TODO
    }

    /// <summary>
    /// 检查指定任务实例是否已存在于任务列表中
    /// </summary>
    /// <param name="instance">任务实例</param>
    public static bool Exists(ITask instance)
    {
        return _ModelMap.TryGetValue(instance, out _);
    }
}
