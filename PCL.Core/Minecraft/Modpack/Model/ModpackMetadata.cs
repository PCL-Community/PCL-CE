namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 整合包的展示信息。全部字段均为可选 —— 各格式对元数据的要求宽松，缺失属于常态。
/// </summary>
/// <param name="Name">整合包名称，通常用作实例名的默认值。</param>
/// <param name="Version">整合包版本号。</param>
/// <param name="Author">作者。</param>
/// <param name="Description">描述文本。</param>
/// <param name="HomepageUrl">项目主页。</param>
/// <param name="Origin">来源平台标识，用于关联在线整合包项目。</param>
public sealed record ModpackMetadata(
    string? Name = null,
    string? Version = null,
    string? Author = null,
    string? Description = null,
    string? HomepageUrl = null,
    ModpackOrigin? Origin = null)
{
    public static ModpackMetadata Empty { get; } = new();
}

/// <summary>
/// 整合包的来源平台与项目标识。
/// </summary>
/// <param name="Platform">平台名称，如 <c>curseforge</c> / <c>modrinth</c>。</param>
/// <param name="ProjectId">该平台上的项目 ID。</param>
public sealed record ModpackOrigin(string Platform, string ProjectId);
