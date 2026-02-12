using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务状态改变事件
/// <param name="state">当前状态，将影响日志和 UI 显示效果</param>
/// <param name="message">状态消息</param>
/// </summary>
public delegate void TaskStateEvent(TaskState state, string message);

/// <summary>
/// 响应式任务接口<br/>
/// <b>NOTE</b>: 为确保运行时响应式模型的 hash 映射正常工作，若无特殊需求，请勿重写对象相等性实现如
/// <see cref="object.GetHashCode"/> 与 <see cref="object.Equals(object)"/>！
/// </summary>
public interface ITask
{
    /// <summary>
    /// 任务标题
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 运行任务
    /// </summary>
    /// <param name="cancelToken">取消令牌</param>
    public Task ExecuteAsync(CancellationToken cancelToken = default);

    /// <summary>
    /// 任务状态改变事件
    /// </summary>
    public event TaskStateEvent StateChanged;
}
