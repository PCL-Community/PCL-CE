using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例总览页的视图模型。</summary>
public partial class PageInstanceOverallViewModel : ViewModelBase
{
    [ObservableProperty] private string _infoLoadingText = Lang.Text("Instance.Overall.Info.Loading");

    [ObservableProperty] private bool _modsFolderVisible = true;

    [ObservableProperty] private string _starText = Lang.Text("Instance.Overall.Favorite");

    public static IReadOnlyList<InstanceInfoRow> BuildInfoRows(McInstance instance)
    {
        var instanceInfo = instance.Info;
        List<InstanceInfoRow> rows = [];
        var launchCount = States.Instance.LaunchCount[instance.PathInstance];
        rows.Add(launchCount == 0
            ? new InstanceInfoRow(Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
                Lang.Text("Instance.Overall.Info.LaunchCount.Never"),
                "pack://application:,,,/images/Blocks/RedstoneLampOff.png")
            : new InstanceInfoRow(Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
                Lang.Text("Instance.Overall.Info.LaunchCount.Count", launchCount),
                "pack://application:,,,/images/Blocks/RedstoneLampOn.png"));
        if (!string.IsNullOrWhiteSpace(States.Instance.ModpackVersion[instance.PathInstance]))
            rows.Add(new InstanceInfoRow(Lang.Text("Instance.Overall.Info.ModpackVersion"),
                States.Instance.ModpackVersion[instance.PathInstance],
                "pack://application:,,,/images/Blocks/CommandBlock.png"));
        rows.Add(new InstanceInfoRow("Minecraft", McInstanceInfo.GetVanillaNameDisplay(instanceInfo.VanillaName),
            "pack://application:,,,/images/Blocks/Grass.png"));
        if (instanceInfo.HasForge)
            rows.Add(new InstanceInfoRow("Forge", FormatLoaderVersion(instanceInfo.Forge),
                "pack://application:,,,/images/Blocks/Anvil.png"));
        if (instanceInfo.HasNeoForge)
            rows.Add(new InstanceInfoRow("NeoForge", FormatLoaderVersion(instanceInfo.NeoForge),
                "pack://application:,,,/images/Blocks/NeoForge.png"));
        if (instanceInfo.HasCleanroom)
            rows.Add(new InstanceInfoRow("Cleanroom", FormatLoaderVersion(instanceInfo.Cleanroom),
                "pack://application:,,,/images/Blocks/Cleanroom.png"));
        if (instanceInfo.HasFabric)
            rows.Add(new InstanceInfoRow("Fabric", FormatLoaderVersion(instanceInfo.Fabric),
                "pack://application:,,,/images/Blocks/Fabric.png"));
        if (instanceInfo.HasQuilt)
            rows.Add(new InstanceInfoRow("Quilt", FormatLoaderVersion(instanceInfo.Quilt),
                "pack://application:,,,/images/Blocks/Quilt.png"));
        if (instanceInfo.HasOptiFine)
            rows.Add(new InstanceInfoRow("OptiFine", FormatLoaderVersion(instanceInfo.OptiFine),
                "pack://application:,,,/images/Blocks/GrassPath.png"));
        if (instanceInfo.HasLiteLoader)
            rows.Add(new InstanceInfoRow("LiteLoader", Lang.Text("Instance.Overall.Info.Installed"),
                "pack://application:,,,/images/Blocks/Egg.png"));
        if (instanceInfo.HasLegacyFabric)
            rows.Add(new InstanceInfoRow("Legacy Fabric", FormatLoaderVersion(instanceInfo.LegacyFabric),
                "pack://application:,,,/images/Blocks/Fabric.png"));
        if (instanceInfo.HasLabyMod)
            rows.Add(new InstanceInfoRow("LabyMod", FormatLoaderVersion(instanceInfo.LabyMod),
                "pack://application:,,,/images/Blocks/LabyMod.png"));
        return rows;
    }

    private static string FormatLoaderVersion(string? version)
    {
        return string.IsNullOrEmpty(version)
            ? Lang.Text("Minecraft.Version.Unknown")
            : version;
    }

    public static void OpenVersionFolder(McInstance version)
    {
        Common.OpenExplorer(version.PathInstance);
    }

    public void RefreshDisplay(McInstance instance)
    {
        StarText = instance.IsStar
            ? Lang.Text("Instance.Overall.Unfavorite")
            : Lang.Text("Instance.Overall.Favorite");
        ModsFolderVisible = instance.Modable;
    }
}

/// <summary>实例信息表中的一行。</summary>
public class InstanceInfoRow(string title, string info, string logo)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Logo { get; } = logo;
}
