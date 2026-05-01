using System.IO;
using System.Runtime.InteropServices;
using PCL.CE.Core.App;
using PCL.CE.Core.IO;

namespace PCL.CE.Core.Link.Scaffolding.EasyTier;

public static class EasyTierMetadata
{
    public const string CurrentEasyTierVer = "2.5.0";

    public static string EasyTierFilePath => Path.Combine(Paths.SharedLocalData, "EasyTier",
        CurrentEasyTierVer,
        $"easytier-windows-{(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x86_64")}");
}