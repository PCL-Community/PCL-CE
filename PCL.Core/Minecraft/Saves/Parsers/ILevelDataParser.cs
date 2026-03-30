using fNbt;
using PCL.Core.Minecraft.Saves.Models;

namespace PCL.Core.Minecraft.Saves.Parsers;

/// <summary>
/// 存档数据解析器接口
/// </summary>
public interface ILevelDataParser
{
    /// <summary>
    /// 解析 NBT 数据为统一的 LevelDataInfo 对象
    /// </summary>
    /// <param name="dataTag">level.dat 中的 Data 复合标签</param>
    /// <param name="saveFolderPath">存档文件夹路径（用于读取外部文件，如 world_gen_settings.dat）</param>
    /// <returns>解析后的存档信息</returns>
    LevelDataInfo Parse(NbtCompound dataTag, string saveFolderPath);
}