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

        if (prefix.Contains("FATAL", StringComparison.Ordinal))
            return GameLogLevel.Fatal;
        if (prefix.Contains("ERROR", StringComparison.Ordinal))
            return GameLogLevel.Error;
        if (prefix.Contains("WARN", StringComparison.Ordinal))
            return GameLogLevel.Warn;
        if (prefix.Contains("INFO", StringComparison.Ordinal))
            return GameLogLevel.Info;
        if (prefix.Contains("DEBUG", StringComparison.Ordinal))
            return GameLogLevel.Debug;

        if (line.StartsWith("Exception in thread \"", StringComparison.Ordinal))
            return GameLogLevel.Error;

        if (previousLevel >= GameLogLevel.Warn
            && (line.Contains("Exception", StringComparison.Ordinal)
                || line.Contains("Realms authentication error with message ", StringComparison.Ordinal)
                || line.StartsWith("\tat ", StringComparison.Ordinal)))
            return previousLevel;

        return GameLogLevel.Info;
    }

    /// <summary>识别加载进度标记；没有出现新标记时返回原阶段。</summary>
    public static GameLogStage DetectProgress(string line, GameLogStage current)
    {
        var stage = (int)current;
        if (stage < (int)GameLogStage.Output)
            return GameLogStage.Output;

        if (stage < (int)GameLogStage.User
            && line.Contains("Setting user:", StringComparison.Ordinal))
            return GameLogStage.User;

        if (stage < (int)GameLogStage.Lwjgl
            && line.Contains("lwjgl version", StringComparison.OrdinalIgnoreCase))
            return GameLogStage.Lwjgl;

        if (stage < (int)GameLogStage.Sound
            && (line.Contains("OpenAL initialized", StringComparison.Ordinal)
                || line.Contains("Starting up SoundSystem", StringComparison.Ordinal)))
            return GameLogStage.Sound;

        if (stage < (int)GameLogStage.Textures
            && ((line.Contains("Created", StringComparison.Ordinal)
                    && line.Contains("textures", StringComparison.Ordinal)
                    && line.Contains("-atlas", StringComparison.Ordinal))
                || line.Contains("Found animation info", StringComparison.Ordinal)))
            return GameLogStage.Textures;

        return current;
    }

    /// <summary>识别关闭或崩溃标记。聊天消息不参与状态判断。</summary>
    public static GameLogSignal DetectSignal(string line)
    {
        if (line.Contains("[CHAT]", StringComparison.Ordinal))
            return GameLogSignal.None;

        if (line.Contains("Someone is closing me!", StringComparison.Ordinal)
            || line.Contains("Restarting Minecraft with command", StringComparison.Ordinal))
            return GameLogSignal.Close;

        if (line.Contains("Crash report saved to", StringComparison.Ordinal)
            || line.Contains("This crash report has been saved to:", StringComparison.Ordinal)
            || line.Contains("Could not save crash report to", StringComparison.Ordinal)
            || line.Contains("/ERROR]: Unable to launch", StringComparison.Ordinal)
            || line.Contains(
                "An exception was thrown, the game will display an error screen and halt.",
                StringComparison.Ordinal))
            return GameLogSignal.Crash;

        return GameLogSignal.None;
    }

    /// <summary>把日志行统一为 CRLF 行尾。</summary>
    public static string NormalizeLineEndings(string text)
    {
        return text
            .Replace("\r\n", "\r", StringComparison.Ordinal)
            .Replace("\n", "\r", StringComparison.Ordinal)
            .Replace("\r", "\r\n", StringComparison.Ordinal);
    }
}
