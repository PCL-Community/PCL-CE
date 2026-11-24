using PCL.Core.Minecraft.Java.Models;

namespace PCL;

public enum JavaDownloadSource
{
    Foojay,
    Mojang
}

public sealed record MojangJavaVersionOption(
    string ComponentName,
    string VersionName,
    int MajorVersion);

public sealed record JavaDownloadSelection(
    JavaDownloadSource Source,
    string? Distribution = null,
    FoojayVersionModel? FoojayPackage = null,
    MojangJavaVersionOption? MojangVersion = null,
    string? DistributionName = null);
