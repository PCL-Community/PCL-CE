using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack.CurseForge;

/// <summary>
/// 判定 CurseForge 文件的资源种类，并在 API 未给出下载地址时构造回退地址。
/// </summary>
public static class CurseForgeResourceClassifier
{
    /// <summary>CurseForge 的项目分类 ID。</summary>
    private static class ClassId
    {
        public const int Mod = 6;
        public const int ResourcePack = 12;
        public const int World = 17;
        public const int ShaderPack = 6552;
        public const int DataPack = 6945;
    }

    /// <summary>
    /// 判定文件的资源种类。
    /// <para>
    /// 依次尝试三种依据：项目分类 ID 最可靠；其次是压缩包内的顶层条目名
    /// （<c>pack.mcmeta</c> 之于资源包、<c>level.dat</c> 之于存档）；
    /// 最后回退到扩展名。三者都不确定时按模组处理 —— 模组是整合包中占绝大多数的情况。
    /// </para>
    /// </summary>
    public static ModpackResourceKind Classify(CurseForgeFileDescriptor descriptor)
    {
        if (descriptor.ClassId is { } classId)
        {
            switch (classId)
            {
                case ClassId.Mod: return ModpackResourceKind.Mod;
                case ClassId.ResourcePack: return ModpackResourceKind.ResourcePack;
                case ClassId.DataPack: return ModpackResourceKind.DataPack;
                case ClassId.ShaderPack: return ModpackResourceKind.ShaderPack;
                case ClassId.World: return ModpackResourceKind.World;
            }
        }

        var kindFromModules = _ClassifyByModules(descriptor.ModuleNames);
        if (kindFromModules is not null) return kindFromModules.Value;

        // 扩展名不足以区分资源包与光影包（两者都是 .zip），无从判断时按模组处理
        return ModpackResourceKind.Mod;
    }

    private static ModpackResourceKind? _ClassifyByModules(IReadOnlyList<string> moduleNames)
    {
        if (moduleNames.Count == 0) return null;

        var modules = new HashSet<string>(moduleNames, StringComparer.OrdinalIgnoreCase);

        if (modules.Contains("level.dat")) return ModpackResourceKind.World;
        if (modules.Contains("META-INF") || modules.Contains("mcmod.info") ||
            modules.Contains("fabric.mod.json") || modules.Contains("mods.toml"))
            return ModpackResourceKind.Mod;
        if (modules.Contains("pack.mcmeta")) return ModpackResourceKind.ResourcePack;
        if (modules.Contains("shaders")) return ModpackResourceKind.ShaderPack;

        return null;
    }

    /// <summary>
    /// 决定文件在实例目录下的相对路径。
    /// </summary>
    /// <param name="descriptor">已解析的文件信息。</param>
    /// <param name="declaredPath">清单已指定的路径，优先于按种类推断。</param>
    /// <exception cref="ModpackUnsafePathException">文件名或声明的路径不合法。</exception>
    public static string ResolveTargetPath(CurseForgeFileDescriptor descriptor, string? declaredPath = null)
    {
        if (!string.IsNullOrWhiteSpace(declaredPath) &&
            ModpackPathPolicy.TryNormalizeRelativePath(declaredPath, out var normalized))
            return normalized;

        return ModpackResourcePaths.CombineWithDirectory(Classify(descriptor), descriptor.FileName);
    }

    /// <summary>
    /// 构造 CurseForge CDN 的回退下载地址。
    /// <para>
    /// 地址规则为 <c>https://edge.forgecdn.net/files/{fileId/1000}/{fileId%1000}/{fileName}</c>，
    /// 在 API 未返回 <c>downloadUrl</c>（例如作者禁止第三方下载）时使用。
    /// </para>
    /// </summary>
    /// <returns>文件名为空时返回 <c>null</c>。</returns>
    public static string? BuildCdnUrl(int fileId, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileId <= 0) return null;

        var high = (fileId / 1000).ToString(CultureInfo.InvariantCulture);
        var low = (fileId % 1000).ToString(CultureInfo.InvariantCulture);

        return $"https://edge.forgecdn.net/files/{high}/{low}/{Uri.EscapeDataString(fileName)}";
    }

    /// <summary>
    /// 汇总一个文件的候选下载地址，按优先级去重排列。
    /// </summary>
    public static IReadOnlyList<string> BuildDownloadUrls(
        CurseForgeFileDescriptor descriptor, string? manifestUrl = null)
    {
        var candidates = new[]
        {
            descriptor.DownloadUrl,
            manifestUrl,
            BuildCdnUrl(descriptor.FileId, descriptor.FileName)
        };

        return candidates
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
