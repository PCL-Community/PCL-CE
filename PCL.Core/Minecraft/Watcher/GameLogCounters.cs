namespace PCL.Core.Minecraft.Watcher;

/// <summary>游戏实时日志各级别的累计行数。</summary>
public readonly record struct GameLogCounters(
    uint Debug,
    uint Info,
    uint Warn,
    uint Error,
    uint Fatal);