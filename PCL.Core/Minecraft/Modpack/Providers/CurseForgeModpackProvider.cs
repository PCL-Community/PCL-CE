using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Modpack.Manifests;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack.Providers;

/// <summary>
/// CurseForge 格式解析器。特征文件为根目录下的 <c>manifest.json</c>（不含 <c>addons</c> 字段）。
/// </summary>
public sealed class CurseForgeModpackProvider : IModpackProvider
{
    /// <summary>CurseForge 清单文件名。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>清单未指定时的默认覆写目录。</summary>
    private const string DefaultOverridesDirectory = "overrides";

    public ModpackFormat Format => ModpackFormat.CurseForge;

    public bool CanRead(ModpackArchive archive)
    {
        // MCBBS 复用了同一文件名，以 addons 字段区分：存在即属于 MCBBS
        var manifest = TryReadManifest(archive);
        return manifest is not null && manifest.Addons is null;
    }

    /// <summary>
    /// 尝试读取并反序列化 <c>manifest.json</c>，供本类与 MCBBS 解析器共用判别逻辑。
    /// </summary>
    /// <returns>文件不存在或无法解析时返回 <c>null</c>。</returns>
    internal static CurseForgeManifest? TryReadManifest(ModpackArchive archive)
    {
        try
        {
            return archive.ReadJson<CurseForgeManifest>(ManifestFileName);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ModpackArchiveException)
        {
            return null;
        }
    }

    public Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default)
    {
        var manifest = TryReadManifest(archive)
                       ?? throw new ModpackManifestInvalidException(
                           Format, ManifestFileName, "文件缺失或不是合法的 JSON");

        var gameVersion = manifest.Minecraft?.Version;
        if (string.IsNullOrWhiteSpace(gameVersion))
            throw new ModpackManifestInvalidException(Format, ManifestFileName, "缺少 minecraft.version");

        var warnings = new List<string>();
        var components = new ModpackComponents(gameVersion, _ParseLoaders(manifest, warnings));
        components.EnsureInstallable();

        var descriptor = new ModpackDescriptor
        {
            Format = Format,
            Metadata = new ModpackMetadata(
                Name: manifest.Name,
                Version: manifest.Version,
                Author: manifest.Author),
            Components = components,
            Overrides = [ModpackOverride.ToInstanceRoot(_ResolveOverridesDirectory(manifest.Overrides))],
            Files = _ParseFiles(manifest, warnings),
            RawManifest = JsonCompat.ParseNode(archive.ReadAllText(ManifestFileName)),
            Warnings = warnings
        };

        return Task.FromResult(descriptor);
    }

    /// <summary>
    /// 解析覆写目录名。<c>"."</c> 与 <c>"./"</c> 表示压缩包根目录本身。
    /// </summary>
    private static string _ResolveOverridesDirectory(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared)) return DefaultOverridesDirectory;

        var normalized = declared.Replace('\\', '/').Trim().Trim('/');
        return normalized is "" or "." ? string.Empty : normalized;
    }

    /// <summary>
    /// 解析 <c>modLoaders</c>。条目 <c>id</c> 形如 <c>&lt;type&gt;-&lt;version&gt;</c>。
    /// </summary>
    private static List<ModpackLoader> _ParseLoaders(CurseForgeManifest manifest, List<string> warnings)
    {
        var loaders = new List<ModpackLoader>();
        if (manifest.Minecraft?.ModLoaders is not { } declared) return loaders;

        foreach (var entry in declared)
        {
            var id = entry.Id?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                warnings.Add("整合包中有一项加载器声明缺少 id，已跳过");
                continue;
            }

            var separator = id.IndexOf('-');
            if (separator <= 0 || separator == id.Length - 1)
            {
                warnings.Add($"无法解析加载器声明「{id}」，已跳过");
                continue;
            }

            var typeName = id[..separator];
            var version = id[(separator + 1)..];

            var kind = ModpackAddonCatalog.ResolveLoader(typeName);
            if (kind is null)
            {
                warnings.Add($"未知的模组加载器「{typeName}」，已跳过");
                continue;
            }

            // 极老的整合包用 "forge-recommended" 之类的占位符代替具体版本，无从解析
            if (version.Contains("recommended", StringComparison.OrdinalIgnoreCase) ||
                version.Contains("latest", StringComparison.OrdinalIgnoreCase))
                throw new ModpackUnsupportedContentException(
                    $"整合包声明的加载器版本「{id}」不是具体版本号，无法安装。");

            loaders.Add(new ModpackLoader(kind.Value, version, entry.Primary));
        }

        return loaders;
    }

    private static List<ModpackFile> _ParseFiles(CurseForgeManifest manifest, List<string> warnings)
    {
        var files = new List<ModpackFile>();
        if (manifest.Files is not { } declared) return files;

        var seen = new HashSet<(int Project, int File)>();

        foreach (var entry in declared)
        {
            if (entry.ProjectId <= 0 || entry.FileId <= 0)
            {
                warnings.Add("整合包中有一项文件缺少 projectID 或 fileID，已跳过");
                continue;
            }

            // CurseForge 导出偶尔会重复同一条目
            if (!seen.Add((entry.ProjectId, entry.FileId))) continue;

            files.Add(new ModpackCurseForgeFile
            {
                ProjectId = entry.ProjectId,
                FileId = entry.FileId,
                FileName = entry.FileName,
                Url = entry.Url,
                Requirement = entry.Required ? ModpackFileRequirement.Required : ModpackFileRequirement.Optional
            });
        }

        return files;
    }
}
