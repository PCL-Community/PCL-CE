using System;

namespace PCL.Core.Minecraft.Watcher;

/// <summary>Minecraft 日志的纯解析规则。所有方法均不修改外部状态，便于独立测试。</summary>
internal static class GameLogParser
{
    private const string LevelSeparator = ": ";

    /// <summary>判断一行是否属于上一条日志的调用栈或异常延续。</summary>
    public static bool IsContinuationLine(string line)
    {
        return line.StartsWith("\tat ", StringComparison.Ordinal)
            || line.StartsWith("Caused by: ", StringComparison.Ordinal)
            || line.StartsWith("\t... ", StringComparison.Ordinal);
    }

    /// <summary>根据日志前缀判断严重级别；无法判断时返回 <see cref="GameLogLevel.Info" />。</summary>
    public static GameLogLevel ClassifyLevel(string line, GameLogLevel previousLevel)
    {
        var separatorIndex = line.IndexOf(LevelSeparator, StringComparison.Ordinal);
        var prefix = separatorIndex >= 0 ? line[..separatorIndex] : line;

        return prefix switch
        {
            _ when prefix.Contains("FATAL", StringComparison.Ordinal)
                => GameLogLevel.Fatal,
            _ when prefix.Contains("ERROR", StringComparison.Ordinal)
                => GameLogLevel.Error,
            _ when prefix.Contains("WARN", StringComparison.Ordinal)
                => GameLogLevel.Warn,
            _ when prefix.Contains("INFO", StringComparison.Ordinal)
                => GameLogLevel.Info,
            _ when prefix.Contains("DEBUG", StringComparison.Ordinal)
                => GameLogLevel.Debug,
            _ when line.StartsWith("Exception in thread \"", StringComparison.Ordinal)
                => GameLogLevel.Error,
            _ when previousLevel >= GameLogLevel.Warn
                && (line.Contains("Exception", StringComparison.Ordinal)
                    || line.Contains(
                        "Realms authentication error with message ",
                        StringComparison.Ordinal)
                    || line.StartsWith("\tat ", StringComparison.Ordinal))
                => previousLevel,
            _ => GameLogLevel.Info
        };
    }

    /// <summary>识别加载进度标记；没有出现新标记时返回原阶段。</summary>
    public static GameLogStage DetectProgress(string line, GameLogStage current)
    {
        var stage = (int)current;

        return line switch
        {
            _ when stage < (int)GameLogStage.Output
                => GameLogStage.Output,
            _ when stage < (int)GameLogStage.User
                && line.Contains("Setting user:", StringComparison.Ordinal)
                => GameLogStage.User,
            _ when stage < (int)GameLogStage.Lwjgl
                && line.Contains("lwjgl version", StringComparison.OrdinalIgnoreCase)
                => GameLogStage.Lwjgl,
            _ when stage < (int)GameLogStage.Sound
                && (line.Contains("OpenAL initialized", StringComparison.Ordinal)
                    || line.Contains("Starting up SoundSystem", StringComparison.Ordinal))
                => GameLogStage.Sound,
            _ when stage < (int)GameLogStage.Textures
                && ((line.Contains("Created", StringComparison.Ordinal)
                        && line.Contains("textures", StringComparison.Ordinal)
                        && line.Contains("-atlas", StringComparison.Ordinal))
                    || line.Contains("Found animation info", StringComparison.Ordinal))
                => GameLogStage.Textures,
            _ => current
        };
    }

    /// <summary>识别关闭或崩溃标记。聊天消息不参与状态判断。</summary>
    public static GameLogSignal DetectSignal(string line)
    {
        return line switch
        {
            _ when line.Contains("[CHAT]", StringComparison.Ordinal)
                => GameLogSignal.None,

            _ when line.Contains("Someone is closing me!", StringComparison.Ordinal)
                || line.Contains("Restarting Minecraft with command", StringComparison.Ordinal)
                => GameLogSignal.Close,

            _ when line.Contains("Crash report saved to", StringComparison.Ordinal)
                || line.Contains("This crash report has been saved to:", StringComparison.Ordinal)
                || line.Contains("Could not save crash report to", StringComparison.Ordinal)
                || line.Contains("/ERROR]: Unable to launch", StringComparison.Ordinal)
                || line.Contains(
                    "An exception was thrown, the game will display an error screen and halt.",
                    StringComparison.Ordinal)
                => GameLogSignal.Crash,

            _ => GameLogSignal.None
        };
    }
}