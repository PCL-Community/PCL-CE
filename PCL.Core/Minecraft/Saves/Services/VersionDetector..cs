using fNbt;

namespace PCL.Core.Minecraft.Saves.Services;

/// <summary>
/// 存档版本检测器
/// </summary>
public static class VersionDetector
{
    public const int DataVersion26_1 = 4774;

    /// <summary>
    /// 判断是否为 26.1+ 新格式
    /// </summary>
    public static bool IsModernFormat(NbtCompound dataTag)
    {
        var dataVersion = dataTag.Get<NbtInt>("DataVersion")?.Value;
        return dataVersion.HasValue && dataVersion.Value >= DataVersion26_1;
    }
}