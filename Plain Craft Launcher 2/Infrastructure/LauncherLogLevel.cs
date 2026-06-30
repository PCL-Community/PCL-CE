namespace PCL;

/// <summary>
///     PCL2 用户可见日志等级。LauncherLogLevel 仅保留为旧 API 兼容枚举。
/// </summary>
public enum LauncherLogLevel
{
    /// <summary>
    ///     不提示，只记录日志。
    /// </summary>
    Normal = 0,

    /// <summary>
    ///     只提示开发者。
    /// </summary>
    Developer = 1,

    /// <summary>
    ///     只提示开发者与调试模式用户。
    /// </summary>
    Debug = 2,

    /// <summary>
    ///     弹出提示所有用户。
    /// </summary>
    Hint = 3,

    /// <summary>
    ///     弹窗，不要求反馈。
    /// </summary>
    Msgbox = 4,

    /// <summary>
    ///     弹窗，要求反馈。
    /// </summary>
    Feedback = 5,

    /// <summary>
    ///     弹出 Windows 原生弹窗，要求反馈。在无法保证 WPF 窗口能正常运行时使用此级别。
    ///     在第二次触发后会直接结束程序。
    /// </summary>
    Critical = 6
}