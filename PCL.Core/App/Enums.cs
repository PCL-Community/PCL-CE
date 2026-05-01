namespace PCL.Core.App;

public static class Enums
{
    /// <summary>
    /// 模块加载状态枚举。
    /// </summary>
    public enum LoadState
    {
        Waiting,
        Loading,
        Finished,
        Failed,
        Aborted
    }

    /// <summary>
    /// 执行返回值。
    /// </summary>
    public enum ProcessReturnValues
    {
        /// <summary>
        /// 执行成功，或进程被中断。
        /// </summary>
        Aborted = -1,

        /// <summary>
        /// 执行成功。
        /// </summary>
        Success = 0,

        /// <summary>
        /// 执行失败。
        /// </summary>
        Fail = 1,

        /// <summary>
        /// 执行时出现未经处理的异常。
        /// </summary>
        Exception = 2,

        /// <summary>
        /// 执行超时。
        /// </summary>
        Timeout = 3,

        /// <summary>
        /// 取消执行。可能是由于不满足执行的前置条件。
        /// </summary>
        Cancel = 4,

        /// <summary>
        /// 任务成功完成。
        /// </summary>
        TaskDone = 5
    }
}