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

        // 懒人包：存在 versions/&lt;版本&gt;/&lt;版本&gt;.json 结构。
        foreach (var entryName in archive.EntryNames)
            if (RegexPatterns.ModpackLazyInstance.Match("/" + entryName).Success)
                return new ModpackDetection(ModpackFormat.LazyPack, "");

        // 嵌套整合包：更深的层级（深度 ≥ 3）存在已知整合包标记，说明压缩包内还打包了其他内容。
        // 仅通过唯一标记（modpack.zip / modrinth.index.json 等）或带 minecraft 字段的 manifest.json 判定，
        // 避免把模组自身的 manifest.json 误判为整合包。
        foreach (var entryName in archive.EntryNames)
        {
            var parts = entryName.Split("/");
            if (parts.Length <= 2)
                continue; // 根目录与一级目录已在上面处理
            switch (parts[^1])
            {
                case "modpack.zip":
                case "modpack.mrpack":
                case "modpack.json":
                case "modrinth.index.json":
                case "mmc-pack.json":
                case "mcbbs.packmeta":
                    return new ModpackDetection(ModpackFormat.LauncherPack, "");
                case "manifest.json":
                    if (_HasMinecraft(archive.ReadEntryText(entryName)))
                        return new ModpackDetection(ModpackFormat.LauncherPack, "");
                    break;
            }
        }

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
            var node = JsonCompat.ParseNode(jsonText);
            return node?["addons"] is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 判断 <c>manifest.json</c> 是否包含 <c>minecraft</c> 字段（CurseForge 整合包清单的必要字段）。
    /// 模组的 <c>manifest.json</c>（如 Fabric 模组）不含该字段，可用于区分嵌套整合包与普通文件。
    /// </summary>
    private static bool _HasMinecraft(string jsonText)
    {
        try
        {
            var node = JsonCompat.ParseNode(jsonText);
            return node?["minecraft"] is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
