using System;
using fNbt;
using System.Numerics;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// NBT 读取工具方法，多个版本解析器共用。
/// </summary>
internal static class NbtReadHelper
{
    /// <summary>尝试从 NBT 复合标签中读取 long 值。</summary>
    public static long? TryGetLong(NbtCompound data, string key) =>
        data.TryGet<NbtLong>(key, out var tag) ? tag!.Value : null;

    /// <summary>读取最后游玩时间并转为 UTC DateTime。</summary>
    public static DateTime ReadLastPlayed(NbtCompound data) =>
        EpochMsToUtc(TryGetLong(data, "LastPlayed") ?? 0);

    /// <summary>将 Unix 毫秒时间戳转为 UTC DateTime。</summary>
    public static DateTime EpochMsToUtc(long ms) =>
        DateTime.UnixEpoch.AddMilliseconds(ms);

    /// <summary>读取累计游戏时间。Minecraft 以 tick 为单位（20 tick = 1 秒）。</summary>
    public static TimeSpan ReadPlayTime(NbtCompound data)
    {
        var ticks = TryGetLong(data, "Time");
        return TimeSpan.FromSeconds((ticks ?? 0) / 20.0d);
    }

    /// <summary>读取游戏模式。hardcore 不是独立的 GameType，而是 Survival + hardcore=1。</summary>
    public static GameMode ReadGameMode(NbtCompound data, out bool isHardcore)
    {
        isHardcore = data.TryGet<NbtByte>("hardcore", out var hc) && hc!.Value == 1;
        if (isHardcore) return GameMode.Hardcore;
        var gt = data.TryGet<NbtInt>("GameType", out var gameType) ? gameType!.Value : 0;
        return gt switch
        {
            1 => GameMode.Creative,
            2 => GameMode.Adventure,
            3 => GameMode.Spectator,
            _ => GameMode.Survival,
        };
    }

    /// <summary>读取出生点坐标 —— 旧版格式（SpawnX/Y/Z 三个独立 int 字段）。</summary>
    public static Vector3? TryReadSpawnFromFields(NbtCompound data)
    {
        if (data.TryGet<NbtInt>("SpawnX", out var sx) &&
            data.TryGet<NbtInt>("SpawnY", out var sy) &&
            data.TryGet<NbtInt>("SpawnZ", out var sz))
            return new Vector3(sx!.Value, sy!.Value, sz!.Value);
        return null;
    }

    /// <summary>读取出生点坐标 —— 新版格式（spawn.pos int[] 数组）。</summary>
    public static Vector3? TryReadSpawnFromPos(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("spawn", out var spawn) &&
            spawn!.TryGet<NbtIntArray>("pos", out var pos) && pos!.Value.Length == 3)
            return new Vector3(pos[0], pos[1], pos[2]);
        return null;
    }

    /// <summary>读取旧版字节型难度（0=和平, 1=简单, 2=普通, 3=困难）。</summary>
    public static Difficulty? ReadDifficultyByte(NbtCompound data)
    {
        if (data.TryGet<NbtByte>("Difficulty", out var diff))
            return (Difficulty)diff!.Value;
        return null;
    }

    /// <summary>读取 Data.Version 复合标签中的版本信息。</summary>
    public static (string? name, int? id) ReadVersion(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("Version", out var version))
        {
            var name = version!.TryGet<NbtString>("Name", out var n) ? n!.Value : null;
            var id = version.TryGet<NbtInt>("Id", out var i) ? i!.Value : (int?)null;
            return (name, id);
        }
        return (null, null);
    }
}
