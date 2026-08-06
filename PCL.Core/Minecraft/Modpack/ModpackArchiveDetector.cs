using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包格式识别结果。
/// </summary>
/// <param name="Format">识别出的格式。</param>
/// <param name="ArchiveBaseFolder">清单所在的压缩包内基础目录（以 <c>/</c> 结尾；根目录时为空字符串）。</param>
public readonly record struct ModpackDetection(ModpackFormat Format, string ArchiveBaseFolder);

/// <summary>
/// 在 <see cref="IModpackArchiveReader"/> 上识别整合包的格式，与压缩包或文件夹来源无关。
/// </summary>
public static class ModpackArchiveDetector
{
    /// <summary>
    /// 识别整合包格式。优先检查根目录的关键文件，其次检查一层子目录中的关键文件，
    /// 最后尝试识别懒人包（<c>.minecraft</c> 完整实例）结构。
    /// </summary>
    public static ModpackDetection Detect(IModpackArchiveReader archive)
    {
        // 根目录关键文件
        if (archive.EntryExists("mcbbs.packmeta"))
            return new ModpackDetection(ModpackFormat.Mcbbs, "");
        if (archive.EntryExists("mmc-pack.json"))
            return new ModpackDetection(ModpackFormat.MultiMc, "");
        if (archive.EntryExists("modrinth.index.json"))
            return new ModpackDetection(ModpackFormat.Modrinth, "");
        if (archive.EntryExists("manifest.json"))
            return new ModpackDetection(
                _HasAddons(archive.ReadEntryText("manifest.json")) ? ModpackFormat.Mcbbs : ModpackFormat.CurseForge, "");
        if (archive.EntryExists("modpack.json"))
            return new ModpackDetection(ModpackFormat.Hmcl, "");
        if (archive.EntryExists("modpack.zip") || archive.EntryExists("modpack.mrpack"))
            return new ModpackDetection(ModpackFormat.LauncherPack, "");

        // 一层子目录中的关键文件
        foreach (var entryName in archive.EntryNames)
        {
            var parts = entryName.Split("/");
            if (parts.Length != 2)
                continue;
            var baseFolder = parts[0] + "/";
            switch (parts[1])
            {
                case "mcbbs.packmeta":
                    return new ModpackDetection(ModpackFormat.Mcbbs, baseFolder);
                case "mmc-pack.json":
                    return new ModpackDetection(ModpackFormat.MultiMc, baseFolder);
                case "modrinth.index.json":
                    return new ModpackDetection(ModpackFormat.Modrinth, baseFolder);
                case "manifest.json":
                    // 历史行为：一层子目录内带 addons 的 manifest 视为 MCBBS，且基础目录固定为 overrides/。
                    if (_HasAddons(archive.ReadEntryText(entryName)))
                        return new ModpackDetection(ModpackFormat.Mcbbs, "overrides/");
                    return new ModpackDetection(ModpackFormat.CurseForge, baseFolder);
                case "modpack.json":
                    return new ModpackDetection(ModpackFormat.Hmcl, baseFolder);
                case "modpack.zip":
                case "modpack.mrpack":
                    return new ModpackDetection(ModpackFormat.LauncherPack, baseFolder);
            }
        }

        // 懒人包：存在 .minecraft/versions/&lt;版本&gt;/&lt;版本&gt;.json 结构
        foreach (var entryName in archive.EntryNames)
            if (RegexPatterns.ModpackLazyInstance.Match(entryName).Success)
                return new ModpackDetection(ModpackFormat.LazyPack, "");

        return new ModpackDetection(ModpackFormat.Unknown, "");
    }

    /// <summary>
    /// 判断 <c>manifest.json</c> 是否包含 <c>addons</c> 字段（带 addons 视为 MCBBS 格式）。
    /// 清单无法解析时按不含 addons 处理，交由对应格式的安装流程抛出更具体的错误。
    /// </summary>
    private static bool _HasAddons(string jsonText)
    {
        try
        {
            var node = JsonNode.Parse(jsonText);
            return node?["addons"] is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
