using fNbt;

namespace PCL.Core.Minecraft.Saves.Services;

/// <summary>
/// 存档版本检测器
/// </summary>
public static class VersionDetector
{
    /// <summary>
    /// 判断是否为 26.1+ 新格式（Mojang 自 26.1-snapshot-6 起大改了存档基础数据存储格式）
    /// </summary>
    public static bool IsModernFormat(NbtCompound dataTag)
    {
        var dataVersion = dataTag.Get<NbtInt>("DataVersion")?.Value;
        return dataVersion.HasValue && dataVersion.Value >= 4774;
    }
}