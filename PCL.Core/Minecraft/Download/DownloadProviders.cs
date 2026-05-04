using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Download;

// ─── URL 转换接口 ────

/// <summary>将官方源 URL 转换为镜像 URL。</summary>
public interface IDownloadProvider
{
    string Name { get; }
    IEnumerable<string> TransformAssetUrls(string original);
    IEnumerable<string> TransformLibraryUrls(string original);
    IEnumerable<string> TransformLauncherMetaUrls(string original);
    string TransformModApiUrl(string original);
    IEnumerable<string> TransformModDownloadUrls(string original);
}

// ─── 辅助方法 ──────────────────────────────────────────────────

internal static class DownloadUrlHelper
{
    public static bool IsThirdPartyLibrary(string url)
    {
        return new[] { "minecraftforge", "fabricmc", "neoforged" }.Any(k => url.Contains(k));
    }
}

// ─── 具体实现：Mojang、BMCLAPI、MCIMirror ──────────────────────

public class MojangDownloadProvider : IDownloadProvider
{
    public string Name => "Mojang 官方源";

    public IEnumerable<string> TransformAssetUrls(string original)
    {
        yield return original.Replace(DownloadRegistry.ResourcesLegacy, DownloadRegistry.Resources);
    }

    public IEnumerable<string> TransformLibraryUrls(string original)
    {
        if (DownloadUrlHelper.IsThirdPartyLibrary(original))
            yield break;
        yield return original;
    }

    public IEnumerable<string> TransformLauncherMetaUrls(string original)
    {
        yield return original;
    }

    public string TransformModApiUrl(string original) => original;
    public IEnumerable<string> TransformModDownloadUrls(string original) { yield return original; }
}

public class BmclapiDownloadProvider : IDownloadProvider
{
    public string Name => "BMCLAPI";

    private const string Base = "https://bmclapi2.bangbang93.com";
    private const string Assets = "https://bmclapi2.bangbang93.com/assets";
    private const string Maven = "https://bmclapi2.bangbang93.com/maven";
    private const string Libraries = "https://bmclapi2.bangbang93.com/libraries";
    private const string McimirrorMod = "https://mod.mcimirror.top";
    private const string UnlistedMirror = "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto";

    public IEnumerable<string> TransformAssetUrls(string original)
    {
        original = original.Replace(DownloadRegistry.ResourcesLegacy, DownloadRegistry.Resources);
        yield return original
            .Replace(DownloadRegistry.PistonData, Assets)
            .Replace(DownloadRegistry.PistonMeta, Assets)
            .Replace(DownloadRegistry.Resources, Assets);
    }

    public IEnumerable<string> TransformLibraryUrls(string original)
    {
        yield return original
            .Replace(DownloadRegistry.PistonData, Maven)
            .Replace(DownloadRegistry.PistonMeta, Maven)
            .Replace(DownloadRegistry.Libraries, Maven)
            .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", UnlistedMirror);

        yield return original
            .Replace(DownloadRegistry.PistonData, Libraries)
            .Replace(DownloadRegistry.PistonMeta, Libraries)
            .Replace(DownloadRegistry.Libraries, Libraries)
            .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", UnlistedMirror);
    }

    public IEnumerable<string> TransformLauncherMetaUrls(string original)
    {
        yield return original
            .Replace(DownloadRegistry.PistonData, Base)
            .Replace(DownloadRegistry.PistonMeta, Base)
            .Replace(DownloadRegistry.Launcher, Base)
            .Replace(DownloadRegistry.LauncherMeta, Base)
            .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", UnlistedMirror);
    }

    public string TransformModApiUrl(string original)
    {
        return original
            .Replace(DownloadRegistry.ModrinthApi, McimirrorMod + "/modrinth")
            .Replace(DownloadRegistry.CurseForgeApi, McimirrorMod + "/curseforge");
    }

    public IEnumerable<string> TransformModDownloadUrls(string original)
    {
        yield return original
            .Replace(DownloadRegistry.ModrinthCdn, McimirrorMod)
            .Replace(DownloadRegistry.CurseForgeCdn, McimirrorMod);
    }
}

public class McimirrorDownloadProvider : IDownloadProvider
{
    public string Name => "MCIMirror";
    private const string McimirrorMod = "https://mod.mcimirror.top";

    public IEnumerable<string> TransformAssetUrls(string original) => throw new NotSupportedException();
    public IEnumerable<string> TransformLibraryUrls(string original) => throw new NotSupportedException();
    public IEnumerable<string> TransformLauncherMetaUrls(string original) => throw new NotSupportedException();
    public string TransformModApiUrl(string original) => DownloadProviderRegistry.Bmclapi.TransformModApiUrl(original);

    public IEnumerable<string> TransformModDownloadUrls(string original)
    {
        yield return original
            .Replace(DownloadRegistry.ModrinthCdn, McimirrorMod)
            .Replace(DownloadRegistry.CurseForgeCdn, McimirrorMod);
    }
}

public static class DownloadProviderRegistry
{
    public static readonly MojangDownloadProvider Mojang = new();
    public static readonly BmclapiDownloadProvider Bmclapi = new();
    public static readonly McimirrorDownloadProvider Mcimirror = new();
}

// ─── 主备链 ────────────────────────────────────────────────────

public class DownloadProviderChain
{
    private readonly IDownloadProvider _primary;
    private readonly IDownloadProvider _fallback;

    public DownloadProviderChain(bool preferOfficial)
    {
        if (preferOfficial) { _primary = DownloadProviderRegistry.Mojang; _fallback = DownloadProviderRegistry.Bmclapi; }
        else { _primary = DownloadProviderRegistry.Bmclapi; _fallback = DownloadProviderRegistry.Mojang; }
    }

    public IEnumerable<string> GetAssetUrls(string original)
    {
        foreach (var url in _primary.TransformAssetUrls(original)) yield return url;
        foreach (var url in _fallback.TransformAssetUrls(original)) yield return url;
    }

    public IEnumerable<string> GetLibraryUrls(string original)
    {
        if (DownloadUrlHelper.IsThirdPartyLibrary(original))
        {
            foreach (var url in DownloadProviderRegistry.Bmclapi.TransformLibraryUrls(original))
                yield return url;
        }
        else
        {
            foreach (var url in _primary.TransformLibraryUrls(original)) yield return url;
            foreach (var url in _fallback.TransformLibraryUrls(original)) yield return url;
            yield return original;
        }
    }

    public IEnumerable<string> GetLauncherMetaUrls(string original)
    {
        foreach (var url in _primary.TransformLauncherMetaUrls(original)) yield return url;
        foreach (var url in _fallback.TransformLauncherMetaUrls(original)) yield return url;
        yield return original;
    }
}

// ════════════════════════════════════════════════════════════════
// 按领域拆分的接口与实现
// ════════════════════════════════════════════════════════════════

// ─── 文件下载（资源、库、启动器元数据）─────────────────────────

/// <summary>将 Mojang 文件下载 URL 转换为镜像地址。</summary>
public interface IFileDownloadUrlProvider
{
    IEnumerable<string> GetAssetUrls(string original, bool preferOfficial);
    IEnumerable<string> GetLibraryUrls(string original, bool preferOfficial);
    IEnumerable<string> GetLauncherMetaUrls(string original, bool preferOfficial);
}

/// <summary>通过主备链转换 Minecraft 文件下载 URL。</summary>
public class FileDownloadUrlProvider : IFileDownloadUrlProvider
{
    public IEnumerable<string> GetAssetUrls(string original, bool preferOfficial) =>
        new DownloadProviderChain(preferOfficial).GetAssetUrls(original);

    public IEnumerable<string> GetLibraryUrls(string original, bool preferOfficial) =>
        new DownloadProviderChain(preferOfficial).GetLibraryUrls(original);

    public IEnumerable<string> GetLauncherMetaUrls(string original, bool preferOfficial) =>
        new DownloadProviderChain(preferOfficial).GetLauncherMetaUrls(original);
}

// ─── Mod（API 与文件下载）─────────────────────────────────────

/// <summary>将 Mod API 和 CDN 下载 URL 转换为镜像地址。</summary>
public interface IModDownloadUrlProvider
{
    string GetModApiUrl(string original);
    List<string> GetModDownloadUrls(string original, int compSourceSolution);
}

/// <summary>通过 Mod 专用镜像转换 Mod 相关 URL。</summary>
public class ModDownloadUrlProvider : IModDownloadUrlProvider
{
    public string GetModApiUrl(string original) =>
        DownloadProviderRegistry.Bmclapi.TransformModApiUrl(original);

    public List<string> GetModDownloadUrls(string original, int compSourceSolution)
    {
        var mirror = original
            .Replace(DownloadRegistry.ModrinthCdn, "https://mod.mcimirror.top")
            .Replace(DownloadRegistry.CurseForgeCdn, "https://mod.mcimirror.top");

        var result = compSourceSolution switch
        {
            0 => new List<string> { mirror, mirror },
            1 => new List<string> { original, mirror },
            2 => new List<string> { original, original },
            _ => new List<string> { original },
        };
        result.Add(original);
        return result;
    }
}

// ─── 版本列表 ─────────────────────────────────────────────────

/// <summary>将版本列表源 URL 映射到 BMCLAPI 镜像。</summary>
public interface IVersionListUrlProvider
{
    string ToBmclapiUrl(string sourceUrl);
    string ForgeVersionListBmclapiUrl(string mcVersion);
    string UnlistedVersionsMirrorUrl();
}

/// <summary>提供版本列表接口的 BMCLAPI 镜像 URL。</summary>
public class VersionListUrlProvider : IVersionListUrlProvider
{
    private const string Bmclapi = "https://bmclapi2.bangbang93.com";
    private const string Unlisted = "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto";

    public string ToBmclapiUrl(string sourceUrl)
    {
        return sourceUrl switch
        {
            _ when sourceUrl == DownloadRegistry.VersionManifest =>
                $"{Bmclapi}/mc/game/version_manifest.json",
            _ when sourceUrl == DownloadRegistry.FabricMeta =>
                $"{Bmclapi}/fabric-meta/v2/versions",
            _ when sourceUrl == DownloadRegistry.ForgeKnownVersions =>
                $"{Bmclapi}/forge/minecraft",
            _ when sourceUrl == DownloadRegistry.NeoForgeVersionsLatest =>
                $"{Bmclapi}/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge",
            _ when sourceUrl == DownloadRegistry.NeoForgeVersionsLegacy =>
                $"{Bmclapi}/neoforge/meta/api/maven/details/releases/net/neoforged/forge",
            _ when sourceUrl == DownloadRegistry.OptiFineList =>
                $"{Bmclapi}/optifine/versionList",
            _ when sourceUrl == DownloadRegistry.LiteLoaderVersions =>
                $"{Bmclapi}/maven/com/mumfrey/liteloader/versions.json",
            _ when sourceUrl == DownloadRegistry.UnlistedVersionsJson =>
                $"{Unlisted}/version_manifest.json",
            _ => null,
        };
    }

    public string ForgeVersionListBmclapiUrl(string mcVersion) =>
        $"{Bmclapi}/forge/minecraft/{mcVersion}";

    public string UnlistedVersionsMirrorUrl() =>
        $"{Unlisted}/version_manifest.json";
}

// ─── 统一入口 ───────────────────────────────────────────────────

/// <summary>
///     下载 URL 的统一入口，按领域暴露子提供者。
///     <br/>• <see cref="File"/> — 资源、库、启动器元数据
///     <br/>• <see cref="Mod"/> — Mod API 与下载
///     <br/>• <see cref="VersionList"/> — 版本列表镜像
/// </summary>
public static class DownloadProvider
{
    public static readonly IFileDownloadUrlProvider File = new FileDownloadUrlProvider();
    public static readonly IModDownloadUrlProvider Mod = new ModDownloadUrlProvider();
    public static readonly IVersionListUrlProvider VersionList = new VersionListUrlProvider();
}