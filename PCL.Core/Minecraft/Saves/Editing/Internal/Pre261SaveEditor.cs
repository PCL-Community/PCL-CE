using System.IO;
using System.Threading;
using System.Threading.Tasks;
using fNbt;

using PCL.Core.Minecraft.Saves.Parsing.Internal;

namespace PCL.Core.Minecraft.Saves.Editing.Internal;

/// <summary>
/// 26.1 之前的存档编辑器（含整个 1.x 版本体系）。
/// </summary>
internal sealed class Pre261SaveEditor : ISaveEditor
{
    public bool CanHandle(int? dataVersion)
        => dataVersion is null || dataVersion < DataVersionBoundaries.DifficultySettings;

    public async Task<bool> ApplyChangesAsync(string levelDatPath, SaveChanges changes, CancellationToken ct)
    {
        if (changes.IsEmpty)
            return false;

        var nbtFile = new NbtFile();
        await Task.Run(() =>
        {
            using var fs = new FileStream(levelDatPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, true);
            nbtFile.LoadFromStream(fs, NbtCompression.AutoDetect);
        }, ct);

        var data = nbtFile.RootTag.Get<NbtCompound>("Data");
        if (data is null)
            return false;

        var changed = false;
        changed |= WriteAllowCommands(data, changes);
        changed |= WriteDifficulty(data, changes);
        changed |= WriteDifficultyLocked(data, changes);

        if (!changed)
            return false;

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
        data["allowCommands"] = new NbtByte("allowCommands", (byte)(changes.AllowCommands.Value ? 1 : 0));
        return true;
    }

    /// <summary>写入 Data.Difficulty（字节型：0=和平, 1=简单, 2=普通, 3=困难）。</summary>
    internal static bool WriteDifficulty(NbtCompound data, SaveChanges changes)
    {
        if (!changes.Difficulty.HasValue)
            return false;
        data["Difficulty"] = new NbtByte("Difficulty", (byte)changes.Difficulty.Value);
        return true;
    }

    /// <summary>写入 Data.DifficultyLocked（字节型：0/1）。</summary>
    internal static bool WriteDifficultyLocked(NbtCompound data, SaveChanges changes)
    {
        if (!changes.LockDifficulty.HasValue)
            return false;
        data["DifficultyLocked"] = new NbtByte("DifficultyLocked", (byte)(changes.LockDifficulty.Value ? 1 : 0));
        return true;
    }
}
