using System.IO;
using System.Runtime.InteropServices;
using PCL.Core.IO;

namespace PCL.Core.Link.Scaffolding.EasyTier;

public static class EasyTierMetadata
{
    public const string CurrentEasyTierVer = "2.5.0";

    public static string EasyTierFilePath => Path.Combine(FileService.LocalDataPath, "EasyTier",
        CurrentEasyTierVer,
        $"easytier-windows-{(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x86_64")}");
}