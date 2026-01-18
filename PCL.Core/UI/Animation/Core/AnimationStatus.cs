namespace PCL.Core.UI.Animation.Core;

public enum AnimationStatus
{
    /// <summary>
    /// 动画未开始。
    /// </summary>
    NotStarted,
    /// <summary>
    /// 动画正在运行。
    /// </summary>
    Running,
    /// <summary>
    /// 动画已完成。
    /// </summary>
    Completed,
    /// <summary>
    /// 动画已取消。
    /// </summary>
    Canceled,
}