namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 存档格式版本，按 Minecraft 大版本划分。
/// 各解析器的优先级由注册顺序决定（从高版本到低版本）。
/// </summary>
public enum SaveFormatVersion
{
    /// <summary>1.3.1 之前：没有 DataVersion、没有 allowCommands、没有 Difficulty。</summary>
    PreLegacy,

    /// <summary>1.3.1 ~ 1.8-pre：没有 DataVersion，有 allowCommands。</summary>
    Legacy,

    /// <summary>1.9 ~ 1.12.2：DataVersion &lt; 1444。</summary>
    Modern,

    /// <summary>1.13 ~ 1.16(20w20a)：DataVersion 在 [1444, 2567) 之间。</summary>
    Post113,

    /// <summary>20w20a(1.16) ~ 26.1-snapshot-5：DataVersion 在 [2567, 4189) 之间，种子在 WorldGenSettings.seed。</summary>
    WorldGen,

    /// <summary>26.1-snapshot-6 及以后：DataVersion >= 4189 或存在 difficulty_settings 复合标签。</summary>
    NextGen,
}
