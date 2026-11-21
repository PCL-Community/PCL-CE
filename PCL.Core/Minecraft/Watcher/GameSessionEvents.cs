using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Watcher;

/// <summary>一条游戏实时日志。</summary>
public sealed class GameLogLineEventArgs(string text, GameLogLevel level) : EventArgs
{
    /// <summary>日志正文。</summary>
    public string Text { get; } = text;

    /// <summary>日志级别。</summary>
    public GameLogLevel Level { get; } = level;
}

/// <summary>会话状态发生变化。</summary>
public sealed class GameSessionStateChangedEventArgs(
    GameSession session,
    GameSessionState oldState,
    GameSessionState newState) : EventArgs
{
    public GameSession Session { get; } = session;

    public GameSessionState OldState { get; } = oldState;

    public GameSessionState NewState { get; } = newState;
}

/// <summary>会话检测到崩溃。</summary>
public sealed class GameSessionCrashedEventArgs(
    GameSession session,
    GameCrashReason reason,
    int? exitCode,
    IReadOnlyList<string> recentLog) : EventArgs
{
    public GameSession Session { get; } = session;

    public GameCrashReason Reason { get; } = reason;

    public int? ExitCode { get; } = exitCode;

    /// <summary>崩溃发生时的最近日志快照。</summary>
    public IReadOnlyList<string> RecentLog { get; } = recentLog;
}

/// <summary>游戏进程已经退出。</summary>
public sealed class GameSessionExitedEventArgs(
    GameSession session,
    int? exitCode,
    GameExitReason reason) : EventArgs
{
    public GameSession Session { get; } = session;

    public int? ExitCode { get; } = exitCode;

    public GameExitReason Reason { get; } = reason;
}