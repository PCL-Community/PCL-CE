using System.Collections.Generic;
using PCL.Core.Minecraft.Modpack.Manifests;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack.Providers;

/// <summary>
/// <c>addons</c> 列表到 <see cref="ModpackComponents"/> 的解析，供 MCBBS 与 Server 两种格式共用。
/// </summary>
internal static class ModpackAddonResolver
{
    /// <summary>
    /// 解析组件列表。
    /// </summary>
    /// <param name="addons">清单中的 <c>addons</c> 列表。</param>
    /// <param name="format">调用方格式，用于构造错误信息。</param>
    /// <param name="manifestPath">清单文件路径，用于构造错误信息。</param>
    /// <param name="warnings">收集非致命问题。</param>
    /// <exception cref="ModpackManifestInvalidException">缺少 Minecraft 版本。</exception>
    public static ModpackComponents Resolve(
        IReadOnlyList<ModpackAddon>? addons, ModpackFormat format, string manifestPath, List<string> warnings)
    {
        string? gameVersion = null;
        var loaders = new List<ModpackLoader>();

        foreach (var addon in addons ?? [])
        {
            if (string.IsNullOrWhiteSpace(addon.Version)) continue;

            if (ModpackAddonCatalog.IsGame(addon.Id))
            {
                gameVersion ??= addon.Version.Trim();
                continue;
            }

            var kind = ModpackAddonCatalog.ResolveLoader(addon.Id);
            if (kind is null)
            {
                warnings.Add($"整合包声明了未知的组件「{addon.Id}」（版本 {addon.Version}），已跳过");
                continue;
            }

            loaders.Add(new ModpackLoader(kind.Value, addon.Version.Trim()));
        }

        if (string.IsNullOrWhiteSpace(gameVersion))
            throw new ModpackManifestInvalidException(
                format, manifestPath, $"addons 中缺少 Minecraft 版本（id 为 {ModpackAddonCatalog.GameId}）");

        var components = new ModpackComponents(gameVersion, loaders);
        components.EnsureInstallable();
        return components;
    }
}
