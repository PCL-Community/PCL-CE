using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.App.Media;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.UI.Background;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Threading;
using HintPresets = PCL.Core.UI.Hints.Hints;

namespace PCL.Core.UI.Game;

/// <summary>把游戏会话的全局事件翻译成启动器界面行为：关闭按钮刷新、启动器显隐、音乐控制、视频背景暂停与崩溃提示。</summary>
public sealed class GameWatcherUiService : GeneralService
{
    private static GameWatcherUiService? _instance;

    private GameWatcherUiService() : base("game-watcher-ui", "游戏监控界面", false)
    {
        _instance = this;
    }

    /// <summary>请求外壳刷新“关闭游戏”快捷按钮。</summary>
    public static event Action? ShutdownButtonRefreshRequested;

    /// <summary>请求打开崩溃反馈页面。</summary>
    public static event Action? FeedbackRequested;

    public override void Start()
    {
        GameWatcherService.ActiveSessionsChanged += OnActiveSessionsChanged;
        GameWatcherService.AllSessionsStopped += OnAllSessionsStopped;
        GameWatcherService.SessionCrashed += OnSessionCrashed;
    }

    public override void Stop()
    {
        GameWatcherService.ActiveSessionsChanged -= OnActiveSessionsChanged;
        GameWatcherService.AllSessionsStopped -= OnAllSessionsStopped;
        GameWatcherService.SessionCrashed -= OnSessionCrashed;
        _instance = null;
    }

    private static void OnActiveSessionsChanged()
    {
        if (_instance is null)
            return;

        UiDispatcher.Post(() =>
        {
            BackgroundHost.Current?.SetGaming(GameWatcherService.HasActiveSessions);
            Raise(ShutdownButtonRefreshRequested);
        });
    }

    private static void OnAllSessionsStopped(bool allowLauncherExit)
    {
        if (_instance is null)
            return;

        UiDispatcher.Post(() =>
        {
            if (GameWatcherService.HasActiveSessions)
                return;

            ApplyMusicPolicy();
            ApplyLauncherVisibility(allowLauncherExit);
        });
    }

    private static void OnSessionCrashed(object? sender, GameSessionCrashedEventArgs e)
    {
        if (_instance is null)
            return;

        if (Config.Launch.DisableCrashAnalysis)
        {
            LogWrapper.Info(
                "Watcher",
                $"[{e.Session.ProcessId}] {Lang.Text("Watcher.Crash.DetectedDisabled")}");
            return;
        }

        UiDispatcher.Post(() =>
        {
            HintService.Show(HintPresets.Info(Lang.Text("Watcher.Crash.Hint")));
            Raise(FeedbackRequested);
        });
    }

    private static void ApplyMusicPolicy()
    {
        try
        {
            var media = MediaService.Current;
            if (media is null)
                return;

            if (Config.Preference.Music.StopInGame)
            {
                media.ResumeMusic();
                LogWrapper.Info("Watcher", "[Music] 已根据设置，在结束后开始音乐播放");
            }
            else if (Config.Preference.Music.StartInGame)
            {
                media.PauseMusic();
                LogWrapper.Info("Watcher", "[Music] 已根据设置，在结束后暂停音乐播放");
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Watcher", "应用游戏结束音乐策略失败");
        }
    }

    private static void ApplyLauncherVisibility(bool allowLauncherExit)
    {
        try
        {
            var visibility = Config.Launch.LauncherVisibility;
            var application = Lifecycle.CurrentApplication;

            switch (visibility)
            {
                case LauncherVisibility.HideAndExit when allowLauncherExit:
                    application?.Shutdown();
                    break;

                case LauncherVisibility.HideAndExit:
                case LauncherVisibility.HideAndReopen:
                {
                    var window = application?.MainWindow;
                    if (window is null)
                        return;

                    window.Show();
                    window.Restore();
                    window.Activate();
                    break;
                }

                case LauncherVisibility.ExitImmediately:
                case LauncherVisibility.ObsoleteCaseDoNotUse:
                case LauncherVisibility.MinimizeAndReopen:
                case LauncherVisibility.DoNothing:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Watcher", "应用启动器可见性设置失败");
        }
    }

    private static void Raise(Action? handler)
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
                LogWrapper.Warn(ex, "Watcher", "游戏监控界面事件处理器执行失败");
            }
    }
}