namespace PCL.Core.Minecraft.Watcher;

/// <summary>单个游戏会话的生命周期状态。</summary>
public enum GameSessionState
{
    /// <summary>进程已启动，但游戏尚未完成基础加载。</summary>
    Loading,

    /// <summary>游戏窗口或关键日志阶段已经出现，视为正在运行。</summary>
    Running,

    /// <summary>检测到启动失败、异常退出或崩溃日志。</summary>
    Crashed,

    /// <summary>游戏正常退出，或日志中明确出现了关闭标记。</summary>
    Ended,

    /// <summary>用户主动结束了游戏进程，或启动流程被取消。</summary>
    Canceled
}

/// <summary>崩溃原因的粗分类。</summary>
public enum GameCrashReason
{
    /// <summary>游戏在完成加载前退出。</summary>
    StartupFailure,

    /// <summary>新版本游戏以非零返回值退出。</summary>
    AbnormalExit,

    /// <summary>日志中出现了明确的崩溃标记。</summary>
    CrashLogDetected
}

/// <summary>游戏进程退出时记录的最终原因。</summary>
public enum GameExitReason
{
    /// <summary>正常退出。</summary>
    Normal,

    /// <summary>加载阶段退出，按启动失败处理。</summary>
    StartupFailure,

    /// <summary>运行阶段以非零返回值退出。</summary>
    AbnormalExit,

    /// <summary>日志检测到崩溃标记。</summary>
    CrashLogDetected,

    /// <summary>用户主动结束或启动流程取消。</summary>
    Canceled
}

/// <summary>日志解析器识别出的状态信号。</summary>
internal enum GameLogSignal
{
    None,
    Close,
    Crash
}

/// <summary>日志中用于估算加载阶段的进度标记。</summary>
internal enum GameLogStage
{
    None = 0,
    Output = 1,
    User = 2,
    Lwjgl = 3,
    Sound = 4,
    Textures = 5
}