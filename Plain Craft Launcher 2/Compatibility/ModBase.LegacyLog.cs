using PCL.Core.Utils;

namespace PCL;

public static partial class ModBase
{
    #region Debug

    public static bool ModeDebug
    {
        get => LauncherRuntime.ModeDebug;
        set => LauncherRuntime.ModeDebug = value;
    }

    // Log
    public enum LogLevel
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

    /// <summary>
    ///     输出 Log。
    /// </summary>
    /// <param name="title">如果要求弹窗，指定弹窗的标题。</param>
    public static void Log(string text, LogLevel level = LogLevel.Normal, string? title = null,
        string? userSummary = null)
    {
        LauncherLog.Log(text, level, title, userSummary);
    }

    /// <summary>
    ///     输出错误信息。
    /// </summary>
    /// <param name="desc">错误描述，仅用于日志和错误详情。</param>
    /// <param name="userSummary">可选的本地化用户摘要；不会写入日志。</param>
    public static void Log(Exception ex, string desc, LogLevel level = LogLevel.Debug, string? title = null,
        string? userSummary = null)
    {
        LauncherLog.Log(ex, desc, level, title, userSummary);
    }

    public static string Base64Decode(string text)
    {
        return Base64Utils.DecodeToString(text);
    }

    public static string Base64Encode(string text)
    {
        return Base64Utils.EncodeString(text);
    }

    public static string Base64Encode(byte[] bytes)
    {
        return Base64Utils.EncodeBytes(bytes);
    }

    // 反馈
    public static void Feedback(bool showMsgbox = true, bool forceOpenLog = false)
    {
        LauncherFeedbackService.Feedback(showMsgbox, forceOpenLog);
    }

    public static bool CanFeedback(bool showHint)
    {
        return LauncherFeedbackService.CanFeedback(showHint);
    }

    /// <summary>
    ///     在日志中输出系统诊断信息。
    /// </summary>
    public static void FeedbackInfo()
    {
        LauncherFeedbackService.FeedbackInfo();
    }

    // 断言
    public static void DebugAssert(bool exp)
    {
        LauncherLog.DebugAssert(exp);
    }

    // 获取当前的堆栈信息
    public static string GetStackTrace()
    {
        return LauncherLog.GetStackTrace();
    }

    #endregion
}