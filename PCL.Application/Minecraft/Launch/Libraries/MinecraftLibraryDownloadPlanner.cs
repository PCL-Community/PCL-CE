// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Minecraft.Downloads;

namespace PCL.Application.Minecraft.Launch.Libraries;

public static class MinecraftLibraryDownloadPlanner
{
    private const string DefaultLibraryBaseUrl = "https://libraries.minecraft.net";

    public static MinecraftLibraryDownloadPlan CreatePlan(MinecraftLibraryDownloadPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftRootDirectory);

        List<MinecraftLibraryDownloadFile> downloadFiles = [];
        List<MinecraftBundledLibraryFile> bundledFiles = [];
        List<string> skippedLocalLibraries = [];
        HashSet<string> seenLocalPaths = new(GetPathComparer());

        foreach (MinecraftLibraryToken token in request.Libraries)
        {
            if (token.IsLocal)
            {
                skippedLocalLibraries.Add(token.OriginalName ?? token.LocalPath);
                continue;
            }

            if (token.LocalPath.Contains("transformer-discovery-service", StringComparison.OrdinalIgnoreCase))
            {
                bundledFiles.Add(new MinecraftBundledLibraryFile
                {
                    ResourceName = "Resources/transformer.jar",
                    LocalPath = token.LocalPath
                });
                continue;
            }

            MinecraftLibraryDownloadFile? downloadFile = CreateDownloadFile(token, request);
            if (downloadFile is null || !seenLocalPaths.Add(downloadFile.LocalPath))
                continue;

            downloadFiles.Add(downloadFile);
        }

        return new MinecraftLibraryDownloadPlan(downloadFiles, bundledFiles, skippedLocalLibraries);
    }

    private static MinecraftLibraryDownloadFile? CreateDownloadFile(
        MinecraftLibraryToken token,
        MinecraftLibraryDownloadPlanRequest request)
    {
        List<string> urls = [];
        string? tokenUrl = token.Url;
        if (tokenUrl is null &&
            string.Equals(token.NameWithoutVersion, "net.minecraftforge:forge:universal", StringComparison.Ordinal))
        {
            tokenUrl = "https://maven.minecraftforge.net" +
                       GetLibraryRelativeUrl(request.MinecraftRootDirectory, token.LocalPath);
        }

        if (tokenUrl is not null)
        {
            urls.Add(tokenUrl);
            if (IsLauncherOrMappingUrl(tokenUrl))
                urls.AddRange(MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(tokenUrl, request.PreferOfficialSource));

            if (tokenUrl.Contains("maven", StringComparison.Ordinal))
            {
                string mirrorUrl = GetMavenMirrorUrl(tokenUrl);
                if (request.PreferOfficialSource)
                    urls.Add(mirrorUrl);
                else
                    urls.Insert(0, mirrorUrl);
            }
        }

        if (IsOptiFineLibraryPath(request.MinecraftRootDirectory, token.LocalPath))
        {
            urls.Add(GetOptiFineMirrorUrl(request.MinecraftRootDirectory, token.LocalPath));
        }
        else if (token.NameWithoutVersion?.Contains("LabyMod", StringComparison.Ordinal) == true)
        {
            if (token.Url is not null)
                urls.Add(token.Url);

            return new MinecraftLibraryDownloadFile
            {
                Urls = urls.Distinct(StringComparer.Ordinal).ToArray(),
                LocalPath = token.LocalPath,
                ActualSize = -1,
                ReportedSize = token.Size,
                Sha1 = token.Sha1,
                IgnoreSize = true,
                Note = MinecraftLibraryDownloadNote.LabyModSizeIgnored
            };
        }
        else if (urls.Count <= 2)
        {
            string officialUrl = DefaultLibraryBaseUrl +
                                 GetLibraryRelativeUrl(request.MinecraftRootDirectory, token.LocalPath);
            urls.AddRange(MinecraftDownloadSourcePlanner.GetLibrarySources(officialUrl, request.PreferOfficialSource));
        }

        string[] distinctUrls = urls.Where(static url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return distinctUrls.Length == 0
            ? null
            : new MinecraftLibraryDownloadFile
            {
                Urls = distinctUrls,
                LocalPath = token.LocalPath,
                ActualSize = token.Size == 0L ? -1 : token.Size,
                ReportedSize = token.Size,
                Sha1 = token.Sha1
            };
    }

    private static string GetMavenMirrorUrl(string original)
    {
        int mavenIndex = original.IndexOf("maven", StringComparison.Ordinal);
        if (mavenIndex < 0)
            return original;

        return original.Replace(original[..mavenIndex], "https://bmclapi2.bangbang93.com/", StringComparison.Ordinal)
            .Replace("maven.fabricmc.net", "maven", StringComparison.Ordinal)
            .Replace("maven.minecraftforge.net", "maven", StringComparison.Ordinal)
            .Replace("maven.neoforged.net/releases", "maven", StringComparison.Ordinal);
    }

    private static bool IsLauncherOrMappingUrl(string url) =>
        url.Contains("launcher.mojang.com/v1/objects", StringComparison.Ordinal) ||
        url.Contains("client.txt", StringComparison.Ordinal) ||
        url.Contains(".tsrg", StringComparison.Ordinal);

    private static string GetLibraryRelativeUrl(string minecraftRootDirectory, string localPath)
    {
        string librariesRoot = Path.Combine(Path.GetFullPath(minecraftRootDirectory), "libraries");
        string relativePath = Path.GetRelativePath(librariesRoot, Path.GetFullPath(localPath));
        return "/" + relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool IsOptiFineLibraryPath(string minecraftRootDirectory, string localPath)
    {
        string optiFineRoot = Path.Combine(
            Path.GetFullPath(minecraftRootDirectory),
            "libraries",
            "optifine",
            "OptiFine");
        return Path.GetFullPath(localPath).StartsWith(EnsureTrailingSeparator(optiFineRoot), GetPathComparison());
    }

    private static string GetOptiFineMirrorUrl(string minecraftRootDirectory, string localPath)
    {
        string optiFineRoot = Path.Combine(
            Path.GetFullPath(minecraftRootDirectory),
            "libraries",
            "optifine",
            "OptiFine");
        string relative = Path.GetRelativePath(optiFineRoot, Path.GetFullPath(localPath))
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        string[] parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string fileName = Path.GetFileName(localPath).Replace("-", "_", StringComparison.Ordinal);
        string basePath = parts.Length == 0 ? fileName : parts[0].Split('_')[0] + "/" + fileName;
        basePath = "/maven/com/optifine/" + basePath;
        if (basePath.Contains("_pre", StringComparison.Ordinal))
            basePath = basePath.Replace("com/optifine/", "com/optifine/preview_", StringComparison.Ordinal);

        return "https://bmclapi2.bangbang93.com" + basePath;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
