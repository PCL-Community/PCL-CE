using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing;

/// <summary>
/// 存档解析器接口 —— 负责将 level.dat 中的 NBT 数据转换为 <see cref="SaveInfo"/>。
/// 每种格式版本对应一个实现类。
/// </summary>
public interface ISaveParser
{
    /// <summary>此解析器对应的存档格式版本。</summary>
    SaveFormatVersion FormatVersion { get; }

    /// <summary>返回此解析器能否处理给定的 NBT 数据。</summary>
    /// <param name="data">level.dat 中的 Data 复合标签。</param>
    /// <param name="dataVersion">Data 中的 DataVersion 字段值，如果不存在则为 null。</param>
    bool CanHandle(NbtCompound data, int? dataVersion);

    /// <summary>
    /// 解析 NBT 数据并返回 <see cref="SaveInfo"/>。
    /// 文件系统元数据（创建时间、修改时间）由调用方传入。
    /// </summary>
    /// <param name="folderPath">存档文件夹的绝对路径。</param>
    /// <param name="data">level.dat 中的 Data 复合标签。</param>
    /// <param name="createdAt">文件夹创建时间（UTC）。</param>
    /// <param name="modifiedAt">level.dat 最后修改时间（UTC）。</param>
    SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt);
}
