namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务取消事件
/// </summary>
public delegate void TaskCancelEvent();

/// <summary>
/// 用于取消实现的接口
/// </summary>
public interface ITaskCancelable
{
    public event TaskCancelEvent Cancel;
}
