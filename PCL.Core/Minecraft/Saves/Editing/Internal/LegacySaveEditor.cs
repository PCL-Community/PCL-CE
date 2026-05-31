using System.IO;
using System.Threading;
using System.Threading.Tasks;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Editing.Internal;

/// <summary>
/// Legacy 存档编辑器 —— 适用于 26.1 之前的存档格式。
/// 写入字节型 NBT 标签：allowCommands、Difficulty、DifficultyLocked。
/// </summary>
internal sealed class LegacySaveEditor : ISaveEditor
{
    /// <summary>处理所有 DataVersion 为 null 或 &lt; 4189 的存档。</summary>
    public bool CanHandle(int? dataVersion)
        => dataVersion is null || dataVersion < 4189;

    public async Task<bool> ApplyChangesAsync(string levelDatPath, SaveChanges changes, CancellationToken ct)
    {
        if (changes.IsEmpty)
            return false;

        // 加载 NBT 文件（GZip 压缩）
        var nbtFile = new NbtFile();
        await Task.Run(() =>
        {
            using var fs = new FileStream(levelDatPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, true);
            nbtFile.LoadFromStream(fs, NbtCompression.AutoDetect);
        }, ct);

        var data = nbtFile.RootTag.Get<NbtCompound>("Data");
        if (data is null)
            return false;

        // 依次写入各项修改
        var changed = false;
        changed |= WriteAllowCommands(data, changes);
        changed |= WriteDifficulty(data, changes);
        changed |= WriteDifficultyLocked(data, changes);

        if (!changed)
            return false;

        // 以 GZip 格式写回
        await Task.Run(() =>
        {
            using var fs = new FileStream(levelDatPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            nbtFile.SaveToStream(fs, NbtCompression.GZip);
        }, ct);

        return true;
    }

    /// <summary>写入 Data.allowCommands（字节型：0/1）。</summary>
    internal static bool WriteAllowCommands(NbtCompound data, SaveChanges changes)
    {
        if (!changes.AllowCommands.HasValue)
            return false;

        var val = changes.AllowCommands.Value;
        data["allowCommands"] = new NbtByte("allowCommands", (byte)(val ? 1 : 0));
        return true;
    }

    /// <summary>写入 Data.Difficulty（字节型：0=和平, 1=简单, 2=普通, 3=困难）。</summary>
    internal static bool WriteDifficulty(NbtCompound data, SaveChanges changes)
    {
        if (!changes.Difficulty.HasValue)
            return false;

        var val = (byte)changes.Difficulty.Value;
        data["Difficulty"] = new NbtByte("Difficulty", val);
        return true;
    }

    /// <summary>写入 Data.DifficultyLocked（字节型：0/1）。</summary>
    internal static bool WriteDifficultyLocked(NbtCompound data, SaveChanges changes)
    {
        if (!changes.LockDifficulty.HasValue)
            return false;

        var val = changes.LockDifficulty.Value;
        data["DifficultyLocked"] = new NbtByte("DifficultyLocked", (byte)(val ? 1 : 0));
        return true;
    }
}
