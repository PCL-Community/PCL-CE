namespace PCL;

/// <summary>
///     模块加载状态。
/// </summary>
public enum LoadState
{
    Waiting,
    Loading,
    Finished,
    Failed,
    Aborted
}