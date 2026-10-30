namespace PCL.Core.ViewModel;

/// <summary>
/// 表示异步操作的执行状态。
/// </summary>
public enum AsyncState
{
    Idle = 0,
    Loading = 1,
    Ready = 2,
    Failed = 3
}
