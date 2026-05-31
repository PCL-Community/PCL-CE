using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// Modern 解析器 —— 对应 1.9 ~ 1.12.2 之间的存档格式。
/// 特征：DataVersion &lt; 1444，新增 Version 复合标签记录版本信息。
/// </summary>
internal sealed class ModernSaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Modern;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue && dataVersion.Value < 1444;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        // 字段布局与 Legacy 兼容，在此基础上追加 Version 信息
        var baseInfo = new LegacySaveParser().Parse(folderPath, data, createdAt, modifiedAt);
        (var versionName, var versionId) = ReadVersion(data);

        return baseInfo with
        {
            VersionName = versionName,
            VersionId = versionId,
        };
    }

    /// <summary>读取 Data.Version 复合标签中的版本信息。</summary>
    internal static (string? name, int? id) ReadVersion(NbtCompound data)
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
