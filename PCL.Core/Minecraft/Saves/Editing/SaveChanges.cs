namespace PCL.Core.Minecraft.Saves.Editing;

/// <summary>
/// 用户希望对存档应用的修改。使用 <c>default</c> 表示"无任何修改"。
/// 仅当 <see cref="Editable{T}.HasValue"/> 为 true 的字段才会被写入。
/// </summary>
public record struct SaveChanges
{
    /// <summary>是否允许作弊命令的修改。</summary>
    public Editable<bool> AllowCommands { get; set; }

    /// <summary>游戏难度的修改。</summary>
    public Editable<Difficulty> Difficulty { get; set; }

    /// <summary>是否锁定难度的修改。</summary>
    public Editable<bool> LockDifficulty { get; set; }

    /// <summary>此结构体是否不包含任何待写入的修改。</summary>
    public bool IsEmpty => !AllowCommands.HasValue && !Difficulty.HasValue && !LockDifficulty.HasValue;
}
