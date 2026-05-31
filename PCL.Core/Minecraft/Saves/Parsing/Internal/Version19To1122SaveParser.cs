using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 1.9 ~ 1.12.2 的存档格式。
/// 特征：DataVersion &lt; 1444，新增 Version 复合标签记录版本信息。
/// </summary>
internal sealed class Version19To1122SaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version19To1122;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue && dataVersion.Value < 1444;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        // 字段布局与 1.3.1 ~ 1.8.9 兼容，在此基础上追加 Version 信息
        var baseInfo = new Version131To189SaveParser().Parse(folderPath, data, createdAt, modifiedAt);
        (var versionName, var versionId) = NbtReadHelper.ReadVersion(data);
        return baseInfo with { VersionName = versionName, VersionId = versionId };
    }
}
