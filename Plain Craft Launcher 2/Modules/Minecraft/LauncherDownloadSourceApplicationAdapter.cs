extern alias PclApplication;

using DownloadSourcePlanner = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftDownloadSourcePlanner;

namespace PCL;

internal static class LauncherDownloadSourceApplicationAdapter
{
    public static IEnumerable<string> OrderSources(
        IEnumerable<string> officialUrls,
        IEnumerable<string> mirrorUrls,
        bool preferOfficialSource) =>
        DownloadSourcePlanner.OrderSources(officialUrls, mirrorUrls, preferOfficialSource);

    public static IEnumerable<string> GetAssetSources(string original, bool preferOfficialSource) =>
        DownloadSourcePlanner.GetAssetSources(original, preferOfficialSource);

    public static IEnumerable<string> GetLibrarySources(string original, bool preferOfficialSource) =>
        DownloadSourcePlanner.GetLibrarySources(original, preferOfficialSource);

    public static IEnumerable<string> GetLauncherOrMetaSources(string original, bool preferOfficialSource) =>
        DownloadSourcePlanner.GetLauncherOrMetaSources(original, preferOfficialSource);
}
