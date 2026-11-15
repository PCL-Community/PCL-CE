using PCL.Core.App;

namespace PCL;

public static partial class GameWatcher
{
    public static List<Watcher> mcWatcherList = [];
    private static bool _isWatcherRunning;
    public static bool hasRunningMinecraft;

    private static void WatcherStateChanged()
    {
        var isRunning = false;
        var triggerLauncherShutdown = true;
        foreach (var watcher in mcWatcherList)
        {
            if (watcher.State
                is Watcher.MinecraftState.Loading
                or Watcher.MinecraftState.Running)
            {
                isRunning = true;
                break;
            }

            if (watcher.State
                is Watcher.MinecraftState.Crashed
                or Watcher.MinecraftState.Canceled)
                triggerLauncherShutdown = false;
        }

        if (_isWatcherRunning == isRunning)
            return;
        _isWatcherRunning = isRunning;
        if (_isWatcherRunning)
            MinecraftStart();
        else
            MinecraftStop(triggerLauncherShutdown);
    }

    private static void MinecraftStart()
    {
        LauncherCommon.Log("[全局] 出现运行中的 Minecraft");
        hasRunningMinecraft = true;
        WatcherUi.Current?.RefreshShutdownButton();
    }

    private static void MinecraftStop(bool triggerLauncherShutdown)
    {
        LauncherCommon.Log("[全局] 已无运行中的 Minecraft");
        hasRunningMinecraft = false;
        WatcherUi.Current?.RefreshShutdownButton();

        if (Config.Preference.Music.StopInGame)
            LauncherCommon.RunInUi(() =>
            {
                MediaService.Current?.ResumeMusic();
                LauncherCommon.Log("[Music] 已根据设置，在结束后开始音乐播放");
            });
        else if (Config.Preference.Music.StartInGame)
            LauncherCommon.RunInUi(() =>
            {
                MediaService.Current?.PauseMusic();
                LauncherCommon.Log("[Music] 已根据设置，在结束后暂停音乐播放");
            });

        MediaService.Current?.SetVideoGaming(false);

        switch (Config.Launch.LauncherVisibility)
        {
            case LauncherVisibility.HideAndExit when triggerLauncherShutdown:
                LauncherCommon.RunInUi(() => WatcherUi.Current?.ExitApplication());
                break;

            case LauncherVisibility.HideAndExit:
            case LauncherVisibility.HideAndReopen:
                LauncherCommon.RunInUi(() => WatcherUi.Current?.ShowApplication());
                break;

            case LauncherVisibility.ExitImmediately:
            case LauncherVisibility.ObsoleteCaseDoNotUse:
            case LauncherVisibility.MinimizeAndReopen:
            case LauncherVisibility.DoNothing:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static GameLogLevel GetLevel(string line, GameLogLevel lastLevel)
    {
        var separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);
        var levelPrefix = separatorIndex >= 0
            ? line[..separatorIndex]
            : line;

        return new object() switch
        {
            _ when levelPrefix.ContainsF("FATAL") => GameLogLevel.Fatal,
            _ when levelPrefix.ContainsF("ERROR") => GameLogLevel.Error,
            _ when levelPrefix.ContainsF("WARN") => GameLogLevel.Warn,
            _ when levelPrefix.ContainsF("INFO") => GameLogLevel.Info,
            _ when levelPrefix.ContainsF("DEBUG") => GameLogLevel.Debug,
            _ when line.StartsWithF("Exception in thread \"") => GameLogLevel.Error,
            _ when lastLevel >= GameLogLevel.Warn
                && (line.ContainsF("Exception")
                    || line.ContainsF("Realms authentication error with message ")
                    || line.StartsWithF("\tat ")) => lastLevel,
            _ => GameLogLevel.Info
        };
    }

    public class LogOutputEventArgs(string logText, GameLogLevel level) : EventArgs
    {
        public readonly GameLogLevel Level = level;
        public readonly string logText = logText;
    }
}