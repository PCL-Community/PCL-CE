using System;
using System.Threading.Tasks;

namespace PCL.Core.UI.Animation.Clock;

public interface IClock
{
    /// <summary>
    /// 当前是否在运行。
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 时钟频率 (FPS)，如果为 <see cref="int.MaxValue"/> 则表示每次循环都触发 Tick 事件。
    /// </summary>
    int Fps { get; set; }

    /// <summary>
    /// 启动时钟。
    /// </summary>
    void Start();

    /// <summary>
    /// 停止时钟。
    /// </summary>
    void Stop();

    /// <summary>
    /// 每一帧触发的事件。
    /// </summary>
    event EventHandler<long>? Tick;
}