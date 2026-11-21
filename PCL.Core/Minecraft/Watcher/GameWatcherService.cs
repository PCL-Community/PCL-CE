using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Watcher;

/// <summary>全局游戏会话注册表。只负责会话生命周期与活动状态，不直接处理界面、音乐或崩溃分析。</summary>
public static class GameWatcherService
{
    private static readonly Lock _Sync = new();
    private static readonly List<GameSession> _SessionList = [];
    private static bool _hasActiveSessions;

    /// <summary>当前登记过的会话快照，包含尚未被下一次启动清理的已结束会话。</summary>
    public static IReadOnlyList<GameSession> Sessions
    {
        get
        {
            lock (_Sync)
            {
                return [.. _SessionList];
            }
        }
    }

    /// <summary>是否存在处于加载或运行状态的会话。</summary>
    public static bool HasActiveSessions
    {
        get
        {
            lock (_Sync)
            {
                return _hasActiveSessions;
            }
        }
    }

    /// <summary>活动会话数量发生变化。</summary>
    public static event Action? ActiveSessionsChanged;

    /// <summary>最后一个活动会话结束。参数表示是否允许启动器按设置自动退出。</summary>
    public static event Action<bool>? AllSessionsStopped;

    /// <summary>任意会话检测到崩溃。</summary>
    public static event EventHandler<GameSessionCrashedEventArgs>? SessionCrashed;

    /// <summary>开始监视一个已经启动的游戏进程。</summary>
    public static GameSession StartSession(
        Process process,
        GameSessionOptions options,
        Action<GameSessionCrashedEventArgs>? onCrashed = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        return StartSession(new SystemGameProcess(process), options, onCrashed);
    }

    internal static GameSession StartSession(
        IGameProcess process,
        GameSessionOptions options,
        Action<GameSessionCrashedEventArgs>? onCrashed = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(options);

        var session = new GameSession(process, options);
        session.StateChanged += OnSessionStateChanged;
        session.Crashed += OnSessionCrashed;
        if (onCrashed is not null)
            session.Crashed += (_, e) => onCrashed(e);

        lock (_Sync)
        {
            _PruneFinishedSessionsLocked();
            _SessionList.Add(session);
        }

        try
        {
            session.Start();
        }
        catch
        {
            session.StateChanged -= OnSessionStateChanged;
            session.Crashed -= OnSessionCrashed;
            lock (_Sync)
            {
                _SessionList.Remove(session);
            }

            session.Dispose();
            throw;
        }

        _RecalculateActiveState();
        return session;
    }

    /// <summary>请求结束所有仍在运行的会话，包括检测到崩溃但进程尚未退出的会话。</summary>
    public static void KillAll()
    {
        foreach (var session in Sessions)
            if (session.State is GameSessionState.Loading
                or GameSessionState.Running
                or GameSessionState.Crashed)
                session.Kill();
    }

    /// <summary>仅供测试清理静态状态。</summary>
    internal static void ResetForTests()
    {
        lock (_Sync)
        {
            foreach (var session in _SessionList)
            {
                session.StateChanged -= OnSessionStateChanged;
                session.Crashed -= OnSessionCrashed;
                session.Dispose();
            }

            _SessionList.Clear();
            _hasActiveSessions = false;
            ActiveSessionsChanged = null;
            AllSessionsStopped = null;
            SessionCrashed = null;
        }
    }

    private static void _PruneFinishedSessionsLocked()
    {
        _SessionList.RemoveAll(session =>
            session.State is GameSessionState.Crashed
                or GameSessionState.Ended
                or GameSessionState.Canceled);
    }

    private static void OnSessionStateChanged(object? sender, GameSessionStateChangedEventArgs e)
    {
        _RecalculateActiveState();
    }

    private static void OnSessionCrashed(object? sender, GameSessionCrashedEventArgs e)
    {
        _Raise(SessionCrashed, e);
    }

    private static void _RecalculateActiveState()
    {
        bool active;
        bool allowLauncherExit;

        lock (_Sync)
        {
            active = _SessionList.Any(session =>
                session.State is GameSessionState.Loading or GameSessionState.Running);
            allowLauncherExit = !_SessionList.Any(session =>
                session.State is GameSessionState.Crashed or GameSessionState.Canceled);

            if (active == _hasActiveSessions)
                return;

            _hasActiveSessions = active;
        }

        LogWrapper.Info(
            "Watcher",
            active
                ? "[全局] 出现运行中的 Minecraft"
                : "[全局] 已无运行中的 Minecraft");

        _Raise(ActiveSessionsChanged);

        if (!active)
            _Raise(AllSessionsStopped, allowLauncherExit);
    }

    private static void _Raise(Action? handler)
    {
        if (handler is null)
            return;

        foreach (var target in handler.GetInvocationList())
            try
            {
                ((Action)target)();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Watcher", "游戏会话全局事件处理器执行失败");
            }
    }

    private static void _Raise<T>(Action<T>? handler, T argument)
    {
        if (handler is null)
            return;

        foreach (var target in handler.GetInvocationList())
            try
            {
                ((Action<T>)target)(argument);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Watcher", "游戏会话全局事件处理器执行失败");
            }
    }

    private static void _Raise<T>(EventHandler<T>? handler, T argument)
        where T : EventArgs
    {
        if (handler is null)
            return;

        foreach (var target in handler.GetInvocationList())
            try
            {
                ((EventHandler<T>)target)(null, argument);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Watcher", "游戏会话全局事件处理器执行失败");
            }
    }
}