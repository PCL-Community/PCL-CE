namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 游戏模式。
/// </summary>
public enum GameMode
{
    /// <summary>生存</summary>
    Survival = 0,
    /// <summary>创造</summary>
    Creative = 1,
    /// <summary>冒险</summary>
    Adventure = 2,
    /// <summary>旁观</summary>
    Spectator = 3,
    /// <summary>极限模式 —— 在 NBT 中并非独立的 GameType，而是 Survival + hardcore=1。</summary>
    Hardcore = 4,
}
