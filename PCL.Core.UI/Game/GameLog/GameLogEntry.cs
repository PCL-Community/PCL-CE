using PCL.Core.Minecraft.Watcher;

namespace PCL.Core.UI.Game.Log;

/// <summary>实时日志中的一行。</summary>
public sealed record GameLogEntry(string Text, GameLogLevel Level);