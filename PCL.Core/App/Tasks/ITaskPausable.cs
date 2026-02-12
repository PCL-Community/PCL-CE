namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务暂停事件
/// </summary>
public delegate void TaskPauseEvent();

/// <summary>
/// 用于暂停实现的接口
/// </summary>
public interface ITaskPausable
{
    public event TaskPauseEvent OnPause;
}
