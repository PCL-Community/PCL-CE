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
/// MCBBS 格式解析器。
/// <para>
/// 特征文件优先取 <c>mcbbs.packmeta</c>；该文件不存在时，含 <c>addons</c> 字段的
/// <c>manifest.json</c> 同样属于本格式 —— 这是与 CurseForge 的唯一区分点。
/// </para>
/// </summary>
public sealed class McbbsModpackProvider : IModpackProvider
{
    /// <summary>MCBBS 专用清单文件名。</summary>
    public const string PackMetaFileName = "mcbbs.packmeta";

    /// <summary>覆写目录。</summary>
    private const string OverridesDirectory = "overrides";

    public ModpackFormat Format => ModpackFormat.Mcbbs;

    public bool CanRead(ModpackArchive archive)
    {
        if (archive.HasEntry(PackMetaFileName)) return true;

        // manifest.json 中存在 addons 字段即属于 MCBBS
        return CurseForgeModpackProvider.TryReadManifest(archive)?.Addons is not null;
    }

    /// <summary>解析实际使用的清单文件名。</summary>
    private static string _ResolveManifestPath(ModpackArchive archive)
        => archive.HasEntry(PackMetaFileName)
            ? PackMetaFileName
            : CurseForgeModpackProvider.ManifestFileName;

    public Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default)
    {
        var manifestPath = _ResolveManifestPath(archive);

        McbbsPackMeta? manifest;
        try
        {
            manifest = archive.ReadJson<McbbsPackMeta>(manifestPath);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ModpackManifestInvalidException(Format, manifestPath, "不是合法的 JSON", ex);
        }

        if (manifest is null)
            throw new ModpackManifestInvalidException(Format, manifestPath, "文件缺失");

        var warnings = new List<string>();
        var components = ModpackAddonResolver.Resolve(manifest.Addons, Format, manifestPath, warnings);

        var descriptor = new ModpackDescriptor
        {
            Format = Format,
            Metadata = new ModpackMetadata(
                Name: manifest.Name,
                Version: manifest.Version,
                Author: manifest.Author,
                Description: manifest.Description,
                HomepageUrl: manifest.Url,
                Origin: _ParseOrigin(manifest)),
            Components = components,
            Overrides = archive.HasDirectory(OverridesDirectory)
                ? [ModpackOverride.ToInstanceRoot(OverridesDirectory)]
                : [],
            Files = _ParseFiles(manifest, warnings),
            LaunchOptions = _ParseLaunchOptions(manifest),
            RawManifest = JsonCompat.ParseNode(archive.ReadAllText(manifestPath)),
            Warnings = warnings
        };

        return Task.FromResult(descriptor);
    }

    private static ModpackOrigin? _ParseOrigin(McbbsPackMeta manifest)
    {
        foreach (var origin in manifest.Origin ?? [])
        {
            if (!string.IsNullOrWhiteSpace(origin.Type) && !string.IsNullOrWhiteSpace(origin.Id))
                return new ModpackOrigin(origin.Type.Trim().ToLowerInvariant(), origin.Id.Trim());
        }

        return null;
    }

    private static List<ModpackFile> _ParseFiles(McbbsPackMeta manifest, List<string> warnings)
    {
        var files = new List<ModpackFile>();
        if (manifest.Files is not { } declared) return files;

        var installMods = manifest.Settings?.InstallMods ?? true;
        var installResourcePacks = manifest.Settings?.InstallResourcePack ?? true;

        foreach (var entry in declared)
        {
            var type = entry.Type?.Trim().ToLowerInvariant();

            // "curse" 条目只给出项目与文件 ID，需在安装阶段经 API 解析
            if (type == "curse")
            {
                if (entry.ProjectId is not { } projectId || entry.FileId is not { } fileId)
                {
                    warnings.Add("整合包中有一项 curse 文件缺少 projectID 或 fileID，已跳过");
                    continue;
                }

                string? targetPath = null;
                if (entry.Path is not null && !ModpackPathPolicy.TryNormalizeRelativePath(entry.Path, out targetPath))
                {
                    warnings.Add($"整合包中有一项文件的路径不合法，已跳过：{entry.Path}");
                    continue;
                }

                files.Add(new ModpackCurseForgeFile
                {
                    ProjectId = projectId,
                    FileId = fileId,
                    FileName = entry.FileName,
                    Url = entry.Url,
                    TargetPath = targetPath
                });
                continue;
            }

            // 其余（含 "addon" 与缺省）按直接文件处理
            if (!ModpackPathPolicy.TryNormalizeRelativePath(entry.Path, out var path))
            {
                warnings.Add($"整合包中有一项文件的路径不合法，已跳过：{entry.Path}");
                continue;
            }

            var kind = ModpackResourcePaths.InferKind(path);
            if (kind == ModpackResourceKind.Mod && !installMods) continue;
            if (kind == ModpackResourceKind.ResourcePack && !installResourcePacks) continue;

            var url = _BuildFileApiUrl(manifest.FileApi, path);
            if (url is null)
            {
                warnings.Add($"整合包未提供 fileApi，无法下载文件，已跳过：{path}");
                continue;
            }

            files.Add(new ModpackDirectFile
            {
                TargetPath = path,
                Urls = [url],
                Sha1 = entry.Hash?.Trim().ToLowerInvariant(),
                Kind = kind
            });
        }

        return files;
    }

    /// <summary>
    /// 由 <c>fileApi</c> 与文件路径拼接下载地址。
    /// </summary>
    private static string? _BuildFileApiUrl(string? fileApi, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(fileApi)) return null;

        var baseUrl = fileApi.TrimEnd('/');
        var encoded = string.Join('/', relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

        return $"{baseUrl}/{encoded}";
    }

    private static ModpackLaunchOptions _ParseLaunchOptions(McbbsPackMeta manifest)
    {
        if (manifest.LaunchInfo is not { } info) return ModpackLaunchOptions.None;

        return new ModpackLaunchOptions
        {
            JvmArguments = info.JavaArgument ?? [],
            GameArguments = info.LaunchArgument ?? [],
            MinMemoryMegabytes = info.MinMemory > 0 ? info.MinMemory : null,
            SupportedJavaMajors = info.SupportJava ?? [],
            Notes = manifest.Description
        };
    }
}
