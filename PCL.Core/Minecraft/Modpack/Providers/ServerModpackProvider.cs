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
/// 服务端整合包格式解析器。特征文件为根目录下的 <c>server-manifest.json</c>。
/// </summary>
public sealed class ServerModpackProvider : IModpackProvider
{
    /// <summary>服务端整合包清单文件名。</summary>
    public const string ManifestFileName = "server-manifest.json";

    /// <summary>覆写目录。</summary>
    private const string OverridesDirectory = "overrides";

    public ModpackFormat Format => ModpackFormat.Server;

    public bool CanRead(ModpackArchive archive) => archive.HasEntry(ManifestFileName);

    public Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default)
    {
        ServerManifest? manifest;
        try
        {
            manifest = archive.ReadJson<ServerManifest>(ManifestFileName);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ModpackManifestInvalidException(Format, ManifestFileName, "不是合法的 JSON", ex);
        }

        if (manifest is null)
            throw new ModpackManifestInvalidException(Format, ManifestFileName, "文件缺失");

        if (string.IsNullOrWhiteSpace(manifest.FileApi))
            throw new ModpackManifestInvalidException(Format, ManifestFileName, "缺少 fileApi");

        var warnings = new List<string>();
        var components = ModpackAddonResolver.Resolve(manifest.Addons, Format, ManifestFileName, warnings);

        var descriptor = new ModpackDescriptor
        {
            Format = Format,
            Metadata = new ModpackMetadata(
                Name: manifest.Name,
                Version: manifest.Version,
                Author: manifest.Author,
                Description: manifest.Description),
            Components = components,
            Overrides = archive.HasDirectory(OverridesDirectory)
                ? [ModpackOverride.ToInstanceRoot(OverridesDirectory)]
                : [],
            Files = _ParseFiles(manifest, warnings),
            RawManifest = JsonCompat.ParseNode(archive.ReadAllText(ManifestFileName)),
            Warnings = warnings
        };

        return Task.FromResult(descriptor);
    }

    private static List<ModpackFile> _ParseFiles(ServerManifest manifest, List<string> warnings)
    {
        var files = new List<ModpackFile>();
        var baseUrl = manifest.FileApi!.TrimEnd('/');

        foreach (var entry in manifest.Files ?? [])
        {
            if (!ModpackPathPolicy.TryNormalizeRelativePath(entry.Path, out var targetPath))
            {
                warnings.Add($"整合包中有一项文件的路径不合法，已跳过：{entry.Path}");
                continue;
            }

            // 优先使用条目自带的地址，否则由 fileApi 拼接
            var url = !string.IsNullOrWhiteSpace(entry.DownloadUrl)
                ? entry.DownloadUrl.Trim()
                : $"{baseUrl}/{_EncodePath(targetPath)}";

            files.Add(new ModpackDirectFile
            {
                TargetPath = targetPath,
                Urls = [url],
                Sha1 = entry.Hash?.Trim().ToLowerInvariant(),
                Kind = ModpackResourcePaths.InferKind(targetPath)
            });
        }

        return files;
    }

    private static string _EncodePath(string relativePath)
        => string.Join('/', relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
}
