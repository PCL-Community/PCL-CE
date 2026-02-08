using System.Collections.ObjectModel;

namespace PCL.Core.App.Tasks.Interfaces;

/// <summary>
/// 拥有步骤的任务
/// </summary>
public interface IStepTask
{
    /// <summary>
    /// 步骤集合
    /// </summary>
    public ObservableCollection<ITask> Steps { get; }

    /// <summary>
    /// 向步骤集合中添加一个步骤
    /// </summary>
    public void AddStep(ITask step);

    /// <summary>
    /// 向步骤集合中移除一个步骤
    /// </summary>
    public void RemoveStep(ITask step);
}