using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.ViewModel;

namespace PCL.Core.UI.Game.Log;

/// <summary>日志页左栏中的一个游戏会话条目。</summary>
public sealed class GameLogSessionEntry(int uuid, GameSession session) : LocalizedObservableObject
{
    public int Uuid { get; } = uuid;

    public GameSession Session { get; } = session;

    public string Title => Session.Metadata.InstanceName;

    public string Info =>
        $"{Session.Metadata.InstanceDescription ?? string.Empty} - {Lang.Date(Session.StartedAt, "T")}";
}