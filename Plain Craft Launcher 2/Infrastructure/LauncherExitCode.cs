namespace PCL;

/// <summary>
///     PCL2 进程退出码。LauncherExitCode 保留为旧 API 兼容层。
/// </summary>
public enum LauncherExitCode
{
    Aborted = -1,
    Success = 0,
    Fail = 1,
    Exception = 2,
    Timeout = 3,
    Cancel = 4,
    TaskDone = 5
}