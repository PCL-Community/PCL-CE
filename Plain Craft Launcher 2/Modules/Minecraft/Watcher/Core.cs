using System.Windows.Media;
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
        GameLaunch.McLaunchLog("[全局] 出现运行中的 Minecraft");
        hasRunningMinecraft = true;
        Launcher.frmMain?.BtnExtraShutdown.ShowRefresh();
    }

    private static void MinecraftStop(bool triggerLauncherShutdown)
    {
        GameLaunch.McLaunchLog("[全局] 已无运行中的 Minecraft");
        hasRunningMinecraft = false;
        Launcher.frmMain?.BtnExtraShutdown.ShowRefresh();

        if (Config.Preference.Music.StopInGame)
            Common.RunInUi(() =>
            {
                if (Music.MusicResume())
                    Common.Log("[Music] 已根据设置，在结束后开始音乐播放");
            });
        else if (Config.Preference.Music.StartInGame)
            Common.RunInUi(() =>
            {
                if (Music.MusicPause())
                    Common.Log("[Music] 已根据设置，在结束后暂停音乐播放");
            });

        VideoBackground.IsGaming = false;
        VideoBackground.VideoPlay();

        switch (Config.Launch.LauncherVisibility)
        {
            case LauncherVisibility.HideAndExit when triggerLauncherShutdown:
                Common.RunInUi(() => Launcher.frmMain?.EndProgram(false));
                break;

            case LauncherVisibility.HideAndExit:
            case LauncherVisibility.HideAndReopen:
                Common.RunInUi(() => Launcher.frmMain?.Hidden = false);
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

    private static SolidColorBrush GetColor(GameLogLevel level)
    {
        return level switch
        {
            GameLogLevel.Debug => GetColorBrush("ColorBrushDebug"),
            GameLogLevel.Warn => GetColorBrush("ColorBrushWarn"),
            GameLogLevel.Error => GetColorBrush("ColorBrushError"),
            GameLogLevel.Fatal => GetColorBrush("ColorBrushFatal"),
            _ => GetColorBrush(ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo")
        };

        SolidColorBrush GetColorBrush(string name)
        {
            return System.Windows.Application.Current.Resources[name] as SolidColorBrush
                ?? throw new InvalidOperationException($"缺少颜色资源：{name}");
        }
    }

    public class LogOutputEventArgs(
        string logText,
        SolidColorBrush color) : EventArgs
    {
        public readonly SolidColorBrush color = color;
        public readonly string logText = logText;
    }

    private enum GameLogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }
}