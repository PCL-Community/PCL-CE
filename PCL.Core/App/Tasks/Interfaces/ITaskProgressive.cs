namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务进度改变事件
/// </summary>
/// <param name="progress">0.0 ~ 1.0 之间的浮点数，表示任务进度</param>
public delegate void TaskProgressEvent(double progress);

/// <summary>
/// 可观察进度的任务模型
/// </summary>
public interface ITaskProgressive
{
    /// <summary>
    /// 任务进度改变事件
    /// </summary>
    public event TaskProgressEvent ProgressChanged;
}
