using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Download;

/// <summary>
///     A download provider transforms URLs between official sources and mirrors.
///     Based on HMCL's DownloadProvider architecture.
/// </summary>
public interface IDownloadProvider
{
    string Name { get; }

    /// <summary>Transform asset download URLs (resources.download.minecraft.net).</summary>
    IEnumerable<string> TransformAssetUrls(string original);

    /// <summary>Transform library download URLs (libraries.minecraft.net, maven repos).</summary>
    IEnumerable<string> TransformLibraryUrls(string original);

    /// <summary>Transform launcher/meta download URLs (launchermeta.mojang.com, piston-data.mojang.com).</summary>
    IEnumerable<string> TransformLauncherMetaUrls(string original);

    /// <summary>Transform mod API URLs (api.modrinth.com, api.curseforge.com). Returns a single URL.</summary>
    string TransformModApiUrl(string original);

    /// <summary>Transform mod file download URLs (cdn.modrinth.com, edge.forgecdn.net).</summary>
    IEnumerable<string> TransformModDownloadUrls(string original);
}

/// <summary>
///     URL categorization helpers for download providers.
/// </summary>
internal static class DownloadUrlHelper
{
    public static bool IsThirdPartyLibrary(string url)
    {
        return new[] { "minecraftforge", "fabricmc", "neoforged" }.Any(k => url.Contains(k));
    }
}

/// <summary>
///     Official Mojang download provider. Passes URLs through unchanged for Mojang-hosted resources,
///     but yields nothing for third-party libraries (Forge, Fabric, NeoForge) that Mojang doesn't host.
/// </summary>
public class MojangDownloadProvider : IDownloadProvider
{
    public string Name => "Mojang 官方源";

    public IEnumerable<string> TransformAssetUrls(string original)
    {
        yield return original.Replace("http://resources.download.minecraft.net",
            "https://resources.download.minecraft.net");
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

    public IEnumerable<string> TransformModDownloadUrls(string original)
    {
        yield return original;
    }
}

/// <summary>
///     BMCLAPI mirror download provider. Transforms Mojang URLs to BMCLAPI equivalents.
/// </summary>
public class BmclapiDownloadProvider : IDownloadProvider
{
    public string Name => "BMCLAPI";

    public IEnumerable<string> TransformAssetUrls(string original)
    {
        original = original.Replace("http://resources.download.minecraft.net",
            "https://resources.download.minecraft.net");
        yield return original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/assets")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/assets")
            .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets");
    }

    public IEnumerable<string> TransformLibraryUrls(string original)
    {
        yield return original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven")
            .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
            .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto");

        yield return original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
            .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries")
            .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto");
    }

    public IEnumerable<string> TransformLauncherMetaUrls(string original)
    {
        yield return original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto");
    }

    public string TransformModApiUrl(string original)
    {
        return original
            .Replace("https://api.modrinth.com", "https://mod.mcimirror.top/modrinth")
            .Replace("https://api.curseforge.com", "https://mod.mcimirror.top/curseforge");
    }

    public IEnumerable<string> TransformModDownloadUrls(string original)
    {
        yield return original
            .Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top")
            .Replace("https://edge.forgecdn.net", "https://mod.mcimirror.top");
    }
}

/// <summary>
///     A Mcimirror download provider for mod file downloads. Transforms mod CDN URLs to mcimirror.top equivalents.
/// </summary>
public class McimirrorDownloadProvider : IDownloadProvider
{
    public string Name => "MCIMirror";

    public IEnumerable<string> TransformAssetUrls(string original)
    {
        throw new NotSupportedException();
    }

    public IEnumerable<string> TransformLibraryUrls(string original)
    {
        throw new NotSupportedException();
    }

    public IEnumerable<string> TransformLauncherMetaUrls(string original)
    {
        throw new NotSupportedException();
    }

    public string TransformModApiUrl(string original) => DownloadProviderRegistry.Bmclapi.TransformModApiUrl(original);

    public IEnumerable<string> TransformModDownloadUrls(string original)
    {
        yield return original
            .Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top")
            .Replace("https://edge.forgecdn.net", "https://mod.mcimirror.top");
    }
}

/// <summary>
///     Registry for download provider singletons.
/// </summary>
public static class DownloadProviderRegistry
{
    public static readonly MojangDownloadProvider Mojang = new();
    public static readonly BmclapiDownloadProvider Bmclapi = new();
    public static readonly McimirrorDownloadProvider Mcimirror = new();
}

/// <summary>
///     Manages the download provider chain based on user source preferences.
///     Handles source ordering (official-first vs mirror-first) and URL transformation.
/// </summary>
public class DownloadProviderChain
{
    private readonly IDownloadProvider _primary;
    private readonly IDownloadProvider _fallback;

    public DownloadProviderChain(bool preferOfficial)
    {
        if (preferOfficial)
        {
            _primary = DownloadProviderRegistry.Mojang;
            _fallback = DownloadProviderRegistry.Bmclapi;
        }
        else
        {
            _primary = DownloadProviderRegistry.Bmclapi;
            _fallback = DownloadProviderRegistry.Mojang;
        }
    }

    /// <summary>
    ///     Get asset download URLs in the preferred order.
    /// </summary>
    public IEnumerable<string> GetAssetUrls(string original)
    {
        foreach (var url in _primary.TransformAssetUrls(original))
            yield return url;
        foreach (var url in _fallback.TransformAssetUrls(original))
            yield return url;
    }

    /// <summary>
    ///     Get library download URLs in the preferred order.
    ///     For third-party libraries (Forge, Fabric, NeoForge), only the mirror is returned.
    /// </summary>
    public IEnumerable<string> GetLibraryUrls(string original)
    {
        if (DownloadUrlHelper.IsThirdPartyLibrary(original))
        {
            foreach (var url in DownloadProviderRegistry.Bmclapi.TransformLibraryUrls(original))
                yield return url;
        }
        else
        {
            foreach (var url in _primary.TransformLibraryUrls(original))
                yield return url;
            foreach (var url in _fallback.TransformLibraryUrls(original))
                yield return url;
            yield return original; // Always include the original as final fallback
        }
    }

    /// <summary>
    ///     Get launcher/meta download URLs in the preferred order.
    /// </summary>
    public IEnumerable<string> GetLauncherMetaUrls(string original)
    {
        foreach (var url in _primary.TransformLauncherMetaUrls(original))
            yield return url;
        foreach (var url in _fallback.TransformLauncherMetaUrls(original))
            yield return url;
        yield return original; // Always include original as fallback
    }
}

/// <summary>
///     Helps resolve mod download sources based on user preferences.
///     This is separate from DownloadProviderChain because mod downloads have their own
///     source preference setting (CompSourceSolution).
/// </summary>
public static class ModDownloadSourceResolver
{
    /// <summary>
    ///     Resolve mod API URL based on CompSourceSolution config.
    /// </summary>
    public static string GetModApiUrl(string original)
    {
        return DownloadProviderRegistry.Bmclapi.TransformModApiUrl(original);
    }

    /// <summary>
    ///     Resolve mod file download URLs based on CompSourceSolution config.
    ///     Returns URLs in the order that download attempts should be made.
    /// </summary>
    public static List<string> GetModDownloadUrls(string original, int compSourceSolution)
    {
        var mirror = original
            .Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top")
            .Replace("https://edge.forgecdn.net", "https://mod.mcimirror.top");

        var result = compSourceSolution switch
        {
            0 => new List<string> { mirror, mirror }, // Mirror
            1 => new List<string> { original, mirror }, // Balanced
            2 => new List<string> { original, original }, // Official
            _ => new List<string> { original } // Fallback
        };

        result.Add(original); // Always include original as final fallback
        return result;
    }
}
