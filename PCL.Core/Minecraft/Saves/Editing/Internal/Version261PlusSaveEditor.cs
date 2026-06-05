using fNbt;

namespace PCL.Core.Minecraft.Saves.Editing.Internal;

/// <summary>
/// 26.1-snapshot-6 及之后的存档编辑器（2026 新版本号体系）。
/// 仅操作内存中的 NbtCompound，文件 IO 由 <see cref="SaveManager"/> 统一处理。
/// </summary>
internal sealed class Version261PlusSaveEditor : ISaveEditor
{
    public bool CanHandle(int? dataVersion)
        => dataVersion >= DataVersionBoundaries._261snapshot6;

    public bool ApplyChanges(NbtCompound data, SaveChanges changes)
    {
        if (changes.IsEmpty)
            return false;

        // 确保 difficulty_settings 复合标签存在
        if (!data.TryGet<NbtCompound>("difficulty_settings", out var ds) || ds is null)
        {
            ds = new NbtCompound("difficulty_settings");
            data.Add(ds);
        }

        var changed = false;
        changed |= Pre261SaveEditor.WriteAllowCommands(data, changes);
        changed |= WriteDifficulty(ds!, changes);
        changed |= WriteLocked(ds!, changes);

        return changed;
    }

    /// <summary>写入 difficulty_settings.difficulty（字符串型）。</summary>
    internal static bool WriteDifficulty(NbtCompound difficultySettings, SaveChanges changes)
    {
        if (!changes.Difficulty.HasValue)
            return false;
        var val = changes.Difficulty.Value switch
        {
            Difficulty.Peaceful => "peaceful",
            Difficulty.Easy => "easy",
            Difficulty.Normal => "normal",
            Difficulty.Hard => "hard",
            _ => "normal",
        };
        difficultySettings["difficulty"] = new NbtString("difficulty", val);
        return true;
    }

    /// <summary>写入 difficulty_settings.locked（字节型：0/1）。</summary>
    internal static bool WriteLocked(NbtCompound difficultySettings, SaveChanges changes)
    {
        if (!changes.LockDifficulty.HasValue)
            return false;
        difficultySettings["locked"] = new NbtByte("locked", (byte)(changes.LockDifficulty.Value ? 1 : 0));
        return true;
    }
}
