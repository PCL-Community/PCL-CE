using System.Collections.ObjectModel;

namespace PCL.Core.UI.Game.Log;

/// <summary>单个游戏会话的显示缓冲区。</summary>
public sealed class GameLogBuffer
{
    public ObservableCollection<GameLogEntry> Lines { get; } = [];
}