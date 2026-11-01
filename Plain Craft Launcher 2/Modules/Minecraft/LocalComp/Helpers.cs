using System.IO;
using System.Text.RegularExpressions;
using static PCL.Comp;

namespace PCL;

public static partial class LocalComp
{
    private static readonly Regex RegexIsJarFile = JarFile();

    public static List<CompLoaderType> GetCurrentVersionModLoader()
    {
        var modLoaders = new List<CompLoaderType>();
        var instance = PageInstanceLeft.McInstance;
        if (instance is null)
            return modLoaders;

        if (instance.Info.HasForge)
            modLoaders.Add(CompLoaderType.Forge);

        if (instance.Info.HasNeoForge)
            modLoaders.Add(CompLoaderType.NeoForge);

        if (instance.Info.HasFabric)
            modLoaders.Add(CompLoaderType.Fabric);

        if (instance.Info.HasQuilt)
            modLoaders.AddRange([CompLoaderType.Fabric, CompLoaderType.Quilt]);

        if (instance.Info.HasLiteLoader)
            modLoaders.Add(CompLoaderType.LiteLoader);

        if (modLoaders.Count == 0)
            modLoaders.AddRange(
            [
                CompLoaderType.Forge,
                CompLoaderType.NeoForge,
                CompLoaderType.Fabric,
                CompLoaderType.LiteLoader,
                CompLoaderType.Quilt
            ]);

        return modLoaders;
    }

    public static string GetPathNameByCompType(CompType theType)
    {
        return theType switch
        {
            CompType.Mod => "mods",
            CompType.ResourcePack => "resourcepacks",
            CompType.Shader => "shaderpacks",
            CompType.Schematic => "schematics",
            CompType.World => "saves",
            _ => "Nothing"
        };
    }

    /// <summary>
    ///     通过文件名关键字和 Mod ID 比如 <c>fabric</c> <c>api</c> 和 <c>fabric-api</c> 来获取给定实例 mods 目录中某个 Mod 的
    ///     <see cref="LocalCompFile" /> 对象
    ///     <br />
    ///     <b>为了不浪费性能，关键字统一用小写</b>
    /// </summary>
    /// <returns>
    ///     如果文件名包含主关键字，以及其他关键字中的任意一个，同时 Mod ID 一致，即认为匹配，返回对应的对象，若没有匹配的文件则返回空值。
    /// </returns>
    public static LocalCompFile? GetModLocalCompByKeywords(
        McInstance instance,
        string? modId,
        string mainKeyword,
        params string[] keywords)
    {
        return modId is null
            ? null
            : GetModLocalCompByKeywords(instance, [modId], mainKeyword, keywords);
    }

    public static LocalCompFile? GetModLocalCompByKeywords(
        McInstance instance,
        string[] modIds,
        string mainKeyword,
        params string[] keywords)
    {
        if (!instance.Modable)
            return null;

        var modFolder = $"{instance.PathInstance}mods";
        if (!Directory.Exists(modFolder))
            return null;

        foreach (var file in Directory.EnumerateFiles(modFolder, $"*{mainKeyword}*"))
        {
            var lowerFilePath = file.ToLower();
            if (!RegexIsJarFile.IsMatch(lowerFilePath))
                continue;

            if (keywords.Length > 0 && !keywords.Any(lowerFilePath.Contains))
                continue;

            var localComp = new LocalCompFile(file);
            localComp.Load();
            if (modIds.Any(modId => (localComp.ModId ?? "") == modId))
                return localComp;
        }

        return null;
    }

    [GeneratedRegex(@"\.jar(\.disabled)?$")]
    private static partial Regex JarFile();
}