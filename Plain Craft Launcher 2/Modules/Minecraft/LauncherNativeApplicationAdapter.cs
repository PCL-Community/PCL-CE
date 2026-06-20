extern alias PclApplication;

using NativeArchiveException = PclApplication::PCL.Application.Minecraft.Launch.Natives.MinecraftNativeArchiveException;
using NativeExtractionRequest = PclApplication::PCL.Application.Minecraft.Launch.Natives.MinecraftNativeExtractionRequest;
using NativeExtractionResult = PclApplication::PCL.Application.Minecraft.Launch.Natives.MinecraftNativeExtractionResult;
using NativeExtractionService = PclApplication::PCL.Application.Minecraft.Launch.Natives.MinecraftNativeExtractionService;
using NativeOperatingSystem = PclApplication::PCL.Application.Minecraft.Launch.Natives.MinecraftNativeOperatingSystem;

namespace PCL;

internal static class LauncherNativeApplicationAdapter
{
    public static LauncherNativeExtractionResult ExtractNatives(
        IEnumerable<ModLibrary.McLibToken> libraries,
        string targetDirectory)
    {
        string[] archivePaths = libraries
            .Where(static library => library.IsNatives)
            .Select(static library => library.LocalPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        try
        {
            NativeExtractionResult result = NativeExtractionService.Extract(
                new NativeExtractionRequest
                {
                    ArchivePaths = archivePaths,
                    TargetDirectory = targetDirectory,
                    OperatingSystem = GetCurrentOperatingSystem()
                });

            return new LauncherNativeExtractionResult(
                result.ExtractedFiles,
                result.UpToDateFiles,
                result.DeletedFiles,
                result.LockedFiles);
        }
        catch (NativeArchiveException ex)
        {
            throw new LauncherNativeArchiveException(ex.ArchivePath, ex);
        }
    }

    private static NativeOperatingSystem GetCurrentOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return NativeOperatingSystem.Win32;
        if (OperatingSystem.IsLinux())
            return NativeOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return NativeOperatingSystem.MacOs;
        return NativeOperatingSystem.Unknown;
    }
}

internal sealed record LauncherNativeExtractionResult(
    IReadOnlyList<string> ExtractedFiles,
    IReadOnlyList<string> UpToDateFiles,
    IReadOnlyList<string> DeletedFiles,
    IReadOnlyList<string> LockedFiles);

internal sealed class LauncherNativeArchiveException : Exception
{
    public LauncherNativeArchiveException(string archivePath, Exception innerException)
        : base($"Unable to read native archive '{archivePath}'.", innerException)
    {
        ArchivePath = archivePath;
    }

    public string ArchivePath { get; }
}
