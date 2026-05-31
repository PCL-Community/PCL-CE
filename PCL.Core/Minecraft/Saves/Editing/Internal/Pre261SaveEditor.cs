using fNbt;

namespace PCL.Core.Minecraft.Saves.Editing.Internal;

/// <summary>
/// 26.1 之前的存档编辑器（含整个 1.x 版本体系）。
/// 仅操作内存中的 NbtCompound，文件 IO 由 <see cref="SaveManager"/> 统一处理。
/// </summary>
internal sealed class Pre261SaveEditor : ISaveEditor
{
    public bool CanHandle(int? dataVersion)
        => dataVersion is null || dataVersion < DataVersionBoundaries._261snapshot6;

    public bool ApplyChanges(NbtCompound data, SaveChanges changes)
    {
        if (changes.IsEmpty)
            return false;

        var changed = false;
        changed |= WriteAllowCommands(data, changes);
        changed |= WriteDifficulty(data, changes);
        changed |= WriteDifficultyLocked(data, changes);

        return changed;
    }

    /// <summary>写入 Data.allowCommands（字节型：0/1）。仅当该字段原本存在时才写入，避免向 pre-1.3.1 存档添加新字段。</summary>
    internal static bool WriteAllowCommands(NbtCompound data, SaveChanges changes)
    {
        if (!changes.AllowCommands.HasValue || !data.Contains("allowCommands"))
            return false;
        data["allowCommands"] = new NbtByte("allowCommands", (byte)(changes.AllowCommands.Value ? 1 : 0));
        return true;
    }

    /// <summary>写入 Data.Difficulty（字节型：0=和平, 1=简单, 2=普通, 3=困难）。仅当该字段原本存在时才写入。</summary>
    internal static bool WriteDifficulty(NbtCompound data, SaveChanges changes)
    {
        if (!changes.Difficulty.HasValue || !data.Contains("Difficulty"))
            return false;
        data["Difficulty"] = new NbtByte("Difficulty", (byte)changes.Difficulty.Value);
        return true;
    }

    /// <summary>写入 Data.DifficultyLocked（字节型：0/1）。仅当该字段原本存在时才写入。</summary>
    internal static bool WriteDifficultyLocked(NbtCompound data, SaveChanges changes)
    {
        if (!changes.LockDifficulty.HasValue || !data.Contains("DifficultyLocked"))
            return false;
        data["DifficultyLocked"] = new NbtByte("DifficultyLocked", (byte)(changes.LockDifficulty.Value ? 1 : 0));
        return true;
    }
}
