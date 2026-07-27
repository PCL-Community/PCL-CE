using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Modpack.Manifests;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack.Providers;

/// <summary>
/// Modrinth 格式（<c>.mrpack</c>）解析器。特征文件为根目录下的 <c>modrinth.index.json</c>。
/// </summary>
public sealed class ModrinthModpackProvider : IModpackProvider
{
    /// <summary>Modrinth 索引文件名。</summary>
    public const string IndexFileName = "modrinth.index.json";

    /// <summary>
    /// 覆写目录，按应用顺序排列 —— <c>client-overrides</c> 在后，可覆盖通用覆写。
    /// </summary>
    private static readonly string[] _OverrideDirectories = ["overrides", "client-overrides"];

    public ModpackFormat Format => ModpackFormat.Modrinth;

    public bool CanRead(ModpackArchive archive) => archive.HasEntry(IndexFileName);

    public Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default)
    {
        ModrinthIndex? index;
        try
        {
            index = archive.ReadJson<ModrinthIndex>(IndexFileName);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ModpackManifestInvalidException(Format, IndexFileName, "不是合法的 JSON", ex);
        }

        if (index is null)
            throw new ModpackManifestInvalidException(Format, IndexFileName, "文件缺失");

        if (index.Dependencies is null ||
            !index.Dependencies.TryGetValue("minecraft", out var gameVersion) ||
            string.IsNullOrWhiteSpace(gameVersion))
            throw new ModpackManifestInvalidException(Format, IndexFileName, "dependencies 中缺少 minecraft");

        var warnings = new List<string>();
        var components = new ModpackComponents(gameVersion, _ParseLoaders(index.Dependencies, warnings));
        components.EnsureInstallable();

        var descriptor = new ModpackDescriptor
        {
            Format = Format,
            Metadata = new ModpackMetadata(
                Name: index.Name,
                Version: index.VersionId,
                Description: index.Summary),
            Components = components,
            Overrides = _OverrideDirectories
                .Where(archive.HasDirectory)
                .Select(ModpackOverride.ToInstanceRoot)
                .ToArray(),
            Files = _ParseFiles(index, warnings),
            RawManifest = JsonCompat.ParseNode(archive.ReadAllText(IndexFileName)),
            Warnings = warnings
        };

        return Task.FromResult(descriptor);
    }

    /// <summary>
    /// 解析 <c>dependencies</c> 中除 <c>minecraft</c> 外的加载器项。
    /// </summary>
    private static List<ModpackLoader> _ParseLoaders(
        Dictionary<string, string> dependencies, List<string> warnings)
    {
        var loaders = new List<ModpackLoader>();

        foreach (var (rawKey, version) in dependencies)
        {
            var key = rawKey.Trim().ToLowerInvariant();
            if (key is "minecraft") continue;
            if (string.IsNullOrWhiteSpace(version)) continue;

            var kind = key switch
            {
                "forge" => ModLoaderKind.Forge,
                // "neo-forge" 是部分导出工具产生的兼容写法
                "neoforge" or "neo-forge" => ModLoaderKind.NeoForge,
                "fabric-loader" => ModLoaderKind.Fabric,
                "quilt-loader" => ModLoaderKind.Quilt,
                _ => (ModLoaderKind?)null
            };

            if (kind is null)
            {
                warnings.Add($"整合包声明了未知的依赖项「{rawKey}」（版本 {version}），已跳过");
                continue;
            }

            loaders.Add(new ModpackLoader(kind.Value, version.Trim()));
        }

        return loaders;
    }

    private static List<ModpackFile> _ParseFiles(ModrinthIndex index, List<string> warnings)
    {
        var files = new List<ModpackFile>();
        if (index.Files is not { } declared) return files;

        foreach (var entry in declared)
        {
            if (!ModpackPathPolicy.TryNormalizeRelativePath(entry.Path, out var targetPath))
            {
                warnings.Add($"整合包中有一项文件的路径不合法，已跳过：{entry.Path}");
                continue;
            }

            var requirement = _ParseRequirement(entry.Env?.Client);
            if (requirement == ModpackFileRequirement.Unsupported) continue;

            var urls = new List<string>();
            foreach (var url in entry.Downloads ?? [])
            {
                if (ModpackDownloadPolicy.IsAcceptable(url)) urls.Add(url);
                else warnings.Add($"已忽略 {targetPath} 的非 HTTPS 下载地址：{url}");
            }

            if (urls.Count == 0)
            {
                warnings.Add($"整合包中有一项文件没有可用的下载地址，已跳过：{targetPath}");
                continue;
            }

            files.Add(new ModpackDirectFile
            {
                TargetPath = targetPath,
                Urls = urls,
                Sha1 = _GetHash(entry, "sha1"),
                Sha512 = _GetHash(entry, "sha512"),
                FileSize = entry.FileSize > 0 ? entry.FileSize : null,
                Requirement = requirement
            });
        }

        return files;
    }

    private static string? _GetHash(ModrinthIndexFile entry, string algorithm)
        => entry.Hashes is not null && entry.Hashes.TryGetValue(algorithm, out var value)
           && !string.IsNullOrWhiteSpace(value)
            ? value.Trim().ToLowerInvariant()
            : null;

    /// <summary>
    /// 解析 <c>env.client</c>。字段缺失按「必需」处理，与 Modrinth 规范一致。
    /// </summary>
    private static ModpackFileRequirement _ParseRequirement(string? client) => client?.Trim().ToLowerInvariant() switch
    {
        "optional" => ModpackFileRequirement.Optional,
        "unsupported" => ModpackFileRequirement.Unsupported,
        _ => ModpackFileRequirement.Required
    };
}
