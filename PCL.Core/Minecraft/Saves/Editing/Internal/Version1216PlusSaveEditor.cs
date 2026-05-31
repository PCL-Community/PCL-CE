using System.IO;
using System.Threading;
using System.Threading.Tasks;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Editing.Internal;

/// <summary>
/// 26w04a(1.21.6) 及之后的存档编辑器。
/// 写入 difficulty_settings 复合标签（字符串型难度 + 字节型锁定）。
/// allowCommands 路径与旧版一致。
/// </summary>
internal sealed class Version1216PlusSaveEditor : ISaveEditor
{
    /// <summary>处理 DataVersion >= 4189 的存档。</summary>
    public bool CanHandle(int? dataVersion)
        => dataVersion >= 4189;

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

        // 确保 difficulty_settings 复合标签存在
        if (!data.TryGet<NbtCompound>("difficulty_settings", out var ds) || ds is null)
        {
            ds = new NbtCompound("difficulty_settings");
            data.Add(ds);
        }

        var changed = false;
        changed |= Pre1216SaveEditor.WriteAllowCommands(data, changes);
        changed |= WriteDifficulty(ds!, changes);
        changed |= WriteLocked(ds!, changes);

        if (!changed)
            return false;

        await Task.Run(() =>
        {
            using var fs = new FileStream(levelDatPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            nbtFile.SaveToStream(fs, NbtCompression.GZip);
        }, ct);

        return true;
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
