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
/// HMCL 自有整合包格式解析器。特征文件为根目录下的 <c>modpack.json</c>。
/// <para>
/// 该格式不属于跨启动器的公共规范，但 PCL 长期支持导入，故一并实现。
/// 游戏组件记录在 <c>minecraft/pack.json</c> 中，覆写内容位于 <c>minecraft/</c> 目录。
/// </para>
/// </summary>
public sealed class HmclModpackProvider : IModpackProvider
{
    /// <summary>HMCL 清单文件名。</summary>
    public const string ManifestFileName = "modpack.json";

    /// <summary>内嵌实例定义的路径。</summary>
    public const string PackDefinitionPath = "minecraft/pack.json";

    /// <summary>覆写目录。</summary>
    private const string OverridesDirectory = "minecraft";

    public ModpackFormat Format => ModpackFormat.Hmcl;

    public bool CanRead(ModpackArchive archive) => archive.HasEntry(ManifestFileName);

    public Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default)
    {
        HmclModpackManifest? manifest;
        HmclPackDefinition? definition;

        try
        {
            manifest = archive.ReadJson<HmclModpackManifest>(ManifestFileName);
            definition = archive.ReadJson<HmclPackDefinition>(PackDefinitionPath);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ModpackManifestInvalidException(Format, ManifestFileName, "不是合法的 JSON", ex);
        }

        if (manifest is null)
            throw new ModpackManifestInvalidException(Format, ManifestFileName, "文件缺失");

        var warnings = new List<string>();
        var gameVersion = _ResolveGameVersion(manifest, definition)
                          ?? throw new ModpackManifestInvalidException(
                              Format, ManifestFileName, "未能确定 Minecraft 版本");

        var components = new ModpackComponents(gameVersion, _ParseLoaders(definition, warnings));
        components.EnsureInstallable();

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
            RawManifest = JsonCompat.ParseNode(archive.ReadAllText(ManifestFileName)),
            Warnings = warnings
        };

        return Task.FromResult(descriptor);
    }

    /// <summary>
    /// 确定游戏版本。优先取 <c>pack.json</c> 的 <c>jar</c>，
    /// 其次是其 <c>patches</c> 中的 <c>game</c> 项，最后回退到清单的 <c>gameVersion</c>。
    /// </summary>
    private static string? _ResolveGameVersion(HmclModpackManifest manifest, HmclPackDefinition? definition)
    {
        if (!string.IsNullOrWhiteSpace(definition?.Jar)) return definition.Jar.Trim();

        foreach (var patch in definition?.Patches ?? [])
        {
            if (ModpackAddonCatalog.IsGame(patch.Id) && !string.IsNullOrWhiteSpace(patch.Version))
                return patch.Version.Trim();
        }

        return string.IsNullOrWhiteSpace(manifest.GameVersion) ? null : manifest.GameVersion.Trim();
    }

    private static List<ModpackLoader> _ParseLoaders(HmclPackDefinition? definition, List<string> warnings)
    {
        var loaders = new List<ModpackLoader>();

        foreach (var patch in definition?.Patches ?? [])
        {
            if (ModpackAddonCatalog.IsGame(patch.Id)) continue;
            if (string.IsNullOrWhiteSpace(patch.Version)) continue;

            var kind = ModpackAddonCatalog.ResolveLoader(patch.Id);
            if (kind is null)
            {
                warnings.Add($"整合包声明了未知的组件「{patch.Id}」（版本 {patch.Version}），已跳过");
                continue;
            }

            loaders.Add(new ModpackLoader(kind.Value, patch.Version.Trim()));
        }

        return loaders;
    }
}
