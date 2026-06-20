extern alias PclApplication;

using PCL.Core.Utils.OS;
using ClasspathPlanRequest = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftClasspathPlanRequest;
using ClasspathPlanner = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftClasspathPlanner;
using LibraryOperatingSystem = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftLibraryOperatingSystem;
using LibraryDownloadPlanner = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftLibraryDownloadPlanner;
using LibraryDownloadNote = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftLibraryDownloadNote;
using LibraryDownloadPlanRequest = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftLibraryDownloadPlanRequest;
using LibraryResolutionRequest = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftLibraryResolutionRequest;
using LibraryResolver = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftLibraryResolver;
using LibraryToken = PclApplication::PCL.Application.Minecraft.Launch.Libraries.MinecraftLibraryToken;

namespace PCL;

internal static class LauncherLibraryApplicationAdapter
{
    public static List<ModLibrary.McLibToken> ResolveLibraries(
        System.Text.Json.Nodes.JsonObject jsonObject,
        string minecraftRootDirectory,
        string? targetInstanceDirectory)
    {
        IReadOnlyList<LibraryToken> tokens = LibraryResolver.Resolve(
            new LibraryResolutionRequest
            {
                VersionJson = jsonObject,
                MinecraftRootDirectory = minecraftRootDirectory,
                TargetInstanceDirectory = targetInstanceDirectory,
                OperatingSystem = GetCurrentOperatingSystem(),
                Is64BitArchitecture = !SystemInfo.Is32BitSystem,
                OperatingSystemVersion = Environment.OSVersion.Version.ToString()
            });

        return tokens.Select(static token => new ModLibrary.McLibToken
        {
            OriginalName = token.OriginalName,
            Url = token.Url,
            LocalPath = token.LocalPath,
            size = token.Size,
            IsNatives = token.IsNatives,
            Sha1 = token.Sha1,
            IsLocal = token.IsLocal
        }).ToList();
    }

    public static string GetLibraryPath(string coordinate, string minecraftRootDirectory, bool includeMinecraftRoot) =>
        LibraryResolver.GetCoordinatePath(coordinate, minecraftRootDirectory, includeMinecraftRoot);

    public static LauncherLibraryDownloadPlan CreateDownloadPlan(
        IEnumerable<ModLibrary.McLibToken> libraries,
        string minecraftRootDirectory,
        bool preferOfficialSource)
    {
        var tokens = libraries.Select(ToApplicationToken).ToArray();

        var plan = LibraryDownloadPlanner.CreatePlan(new LibraryDownloadPlanRequest
        {
            Libraries = tokens,
            MinecraftRootDirectory = minecraftRootDirectory,
            PreferOfficialSource = preferOfficialSource
        });

        return new LauncherLibraryDownloadPlan(
            plan.DownloadFiles.Select(static file => new LauncherLibraryDownloadFile(
                file.Urls,
                file.LocalPath,
                file.ActualSize,
                file.ReportedSize,
                file.Sha1,
                file.IgnoreSize,
                file.Note switch
                {
                    LibraryDownloadNote.LabyModSizeIgnored => LauncherLibraryDownloadNote.LabyModSizeIgnored,
                    _ => LauncherLibraryDownloadNote.None
                })).ToArray(),
            plan.BundledFiles.Select(static file => new LauncherBundledLibraryFile(
                file.ResourceName,
                file.LocalPath)).ToArray(),
            plan.SkippedLocalLibraries);
    }

    public static LauncherClasspathPlan CreateClasspathPlan(
        IEnumerable<ModLibrary.McLibToken> libraries,
        IEnumerable<string> classpathHeadEntries,
        IEnumerable<string> bundledEntries,
        bool hasCleanroom)
    {
        var plan = ClasspathPlanner.CreatePlan(new ClasspathPlanRequest
        {
            Libraries = libraries.Select(ToApplicationToken).ToArray(),
            ClasspathHeadEntries = classpathHeadEntries.ToArray(),
            BundledClasspathEntries = bundledEntries.ToArray(),
            HasCleanroom = hasCleanroom
        });

        return new LauncherClasspathPlan(plan.Entries);
    }

    private static LibraryToken ToApplicationToken(ModLibrary.McLibToken library) => new()
    {
        OriginalName = library.OriginalName,
        NameWithoutVersion = library.Name,
        Url = library.Url,
        LocalPath = library.LocalPath,
        Sha1 = library.Sha1,
        Size = library.size,
        IsNatives = library.IsNatives,
        IsLocal = library.IsLocal
    };

    private static LibraryOperatingSystem GetCurrentOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return LibraryOperatingSystem.Win32;
        if (OperatingSystem.IsLinux())
            return LibraryOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return LibraryOperatingSystem.MacOs;
        return LibraryOperatingSystem.Unknown;
    }
}

internal sealed record LauncherLibraryDownloadPlan(
    IReadOnlyList<LauncherLibraryDownloadFile> DownloadFiles,
    IReadOnlyList<LauncherBundledLibraryFile> BundledFiles,
    IReadOnlyList<string> SkippedLocalLibraries);

internal sealed record LauncherLibraryDownloadFile(
    IReadOnlyList<string> Urls,
    string LocalPath,
    long ActualSize,
    long ReportedSize,
    string? Sha1,
    bool IgnoreSize,
    LauncherLibraryDownloadNote Note);

internal enum LauncherLibraryDownloadNote
{
    None,
    LabyModSizeIgnored
}

internal sealed record LauncherBundledLibraryFile(
    string ResourceName,
    string LocalPath);

internal sealed record LauncherClasspathPlan(IReadOnlyList<string> Entries);
