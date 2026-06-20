extern alias PclApplication;

using PCL.Core.App.Localization;
using ClientDownloadFailureReason = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftClientDownloadFailureReason;
using ClientDownloadPlanner = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftClientDownloadPlanner;
using ClientJarDownloadPlanRequest = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftClientJarDownloadPlanRequest;
using AssetIndexDownloadPlanRequest = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftAssetIndexDownloadPlanRequest;

namespace PCL;

internal static class LauncherClientDownloadApplicationAdapter
{
    public static LauncherClientJarDownloadFile CreateClientJarPlan(McInstance version)
    {
        var plan = ClientDownloadPlanner.CreateClientJarPlan(new ClientJarDownloadPlanRequest
        {
            VersionJson = version.JsonObject,
            InstanceDirectory = version.PathInstance,
            VersionName = version.Name
        });

        if (plan.FailureReason == ClientDownloadFailureReason.NoClientJarDownloadInfo)
            throw new Exception(Lang.Text("Minecraft.Download.Error.NoJarDownloadInfo", version.Name));
        if (plan.File is null)
            throw new InvalidOperationException("Client jar download plan did not produce a file.");

        return new LauncherClientJarDownloadFile(
            plan.File.Url,
            plan.File.LocalPath,
            plan.File.MinimumSize,
            plan.File.ActualSize,
            plan.File.Sha1);
    }

    public static LauncherAssetIndexDownloadPlan CreateAssetIndexPlan(McInstance version)
    {
        var plan = ClientDownloadPlanner.CreateAssetIndexPlan(new AssetIndexDownloadPlanRequest
        {
            VersionJson = version.JsonObject,
            MinecraftRootDirectory = ModFolder.mcFolderSelected,
            UseLegacyFallback = true,
            AllowUrlOnlyAssetIndex = true
        });

        return new LauncherAssetIndexDownloadPlan(
            plan.IndexId ?? string.Empty,
            plan.Url,
            plan.LocalPath,
            plan.UsedLegacyFallback);
    }
}

internal sealed record LauncherClientJarDownloadFile(
    string Url,
    string LocalPath,
    long MinimumSize,
    long ActualSize,
    string? Sha1);

internal sealed record LauncherAssetIndexDownloadPlan(
    string IndexId,
    string? Url,
    string? LocalPath,
    bool UsedLegacyFallback);
