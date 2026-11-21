namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 游戏难度。
/// </summary>
public enum Difficulty
{
    /// <summary>和平</summary>
    Peaceful = 0,
    /// <summary>简单</summary>
    Easy = 1,
    /// <summary>普通</summary>
    Normal = 2,
    /// <summary>困难</summary>
    Hard = 3,
}

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

/// <summary>
/// 存档格式版本，按 Minecraft 大版本的历史演进排列，直接对应解析器类型。
/// 各解析器的匹配优先级等于版本号从高到低的顺序。
/// </summary>
public enum SaveFormatVersion
{
    /// <summary>Alpha ~ 正式 1.2.5</summary>
    Pre113,

    /// <summary>1.3.1 ~ 1.8.9</summary>
    Version131To189,

    /// <summary>15w32a(1.9) ~ 1.12.2</summary>
    Version19To1122,

    /// <summary>17w47a(1.13) ~ 1.15.2</summary>
    Version113To1152,

    /// <summary>20w20a(1.16) ~ 1.21.11</summary>
    Version116To1211,

    /// <summary>26.1-snapshot-6 及之后（2026 新版本号体系）</summary>
    Version261Plus,
}
