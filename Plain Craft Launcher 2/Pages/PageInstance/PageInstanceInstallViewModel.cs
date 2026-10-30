using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例安装向导页的视图模型。</summary>
public partial class PageInstanceInstallViewModel : ViewModelBase
{
    /// <summary>已选择安装项的摘要。</summary>
    public sealed record LoaderSelection(
        string? VanillaName,
        string? Fabric,
        string? LegacyFabric,
        string? Forge,
        string? NeoForge,
        string? Cleanroom,
        string? LabyMod,
        string? OptiFine,
        bool HasLiteLoader);

    [ObservableProperty] private string? _selectedTitle = "";

    [ObservableProperty] private string? _selectedLogo = "";

    [ObservableProperty] private string? _selectedInfo = "";

    [ObservableProperty] private string _startActionText = Lang.Text("Instance.Install.Action.StartModify");

    [ObservableProperty] private bool _canStartInstall;

    [ObservableProperty] private string? _minecraftText = "";

    [ObservableProperty] private string _loadingText = "";

    [ObservableProperty] private bool _showOptiFineCard = true;

    [ObservableProperty] private bool _showOptiFineClear = true;

    [ObservableProperty] private bool _showOptiFineImage = true;

    [ObservableProperty] private string? _optiFineText = "";

    [ObservableProperty] private bool _showLiteLoaderCard = true;

    [ObservableProperty] private bool _showLiteLoaderClear = true;

    [ObservableProperty] private bool _showLiteLoaderImage = true;

    [ObservableProperty] private string? _liteLoaderText = "";

    [ObservableProperty] private bool _showForgeCard = true;

    [ObservableProperty] private bool _showForgeClear = true;

    [ObservableProperty] private bool _showForgeImage = true;

    [ObservableProperty] private string? _forgeText = "";

    [ObservableProperty] private bool _showCleanroomCard = true;

    [ObservableProperty] private bool _showCleanroomClear = true;

    [ObservableProperty] private bool _showCleanroomImage = true;

    [ObservableProperty] private string? _cleanroomText = "";

    [ObservableProperty] private bool _showNeoForgeCard = true;

    [ObservableProperty] private bool _showNeoForgeClear = true;

    [ObservableProperty] private bool _showNeoForgeImage = true;

    [ObservableProperty] private string? _neoForgeText = "";

    [ObservableProperty] private bool _showFabricCard = true;

    [ObservableProperty] private bool _showFabricClear = true;

    [ObservableProperty] private bool _showFabricImage = true;

    [ObservableProperty] private string? _fabricText = "";

    [ObservableProperty] private bool _showFabricApiCard = true;

    [ObservableProperty] private bool _showFabricApiClear = true;

    [ObservableProperty] private bool _showFabricApiImage = true;

    [ObservableProperty] private string? _fabricApiText = "";

    [ObservableProperty] private bool _showLegacyFabricCard = true;

    [ObservableProperty] private bool _showLegacyFabricClear = true;

    [ObservableProperty] private bool _showLegacyFabricImage = true;

    [ObservableProperty] private string? _legacyFabricText = "";

    [ObservableProperty] private bool _showLegacyFabricApiCard = true;

    [ObservableProperty] private bool _showLegacyFabricApiClear = true;

    [ObservableProperty] private bool _showLegacyFabricApiImage = true;

    [ObservableProperty] private string? _legacyFabricApiText = "";

    [ObservableProperty] private bool _showLabyModCard = true;

    [ObservableProperty] private bool _showLabyModClear = true;

    [ObservableProperty] private bool _showLabyModImage = true;

    [ObservableProperty] private string? _labyModText = "";

    [ObservableProperty] private bool _showOptiFabricCard = true;

    [ObservableProperty] private bool _showOptiFabricClear = true;

    [ObservableProperty] private bool _showOptiFabricImage = true;

    [ObservableProperty] private string? _optiFabricText = "";

    [ObservableProperty] private bool _showHintFabricAPI = true;

    [ObservableProperty] private bool _showHintLegacyFabricAPI = true;

    [ObservableProperty] private bool _showHintOptiFabric = true;

    [ObservableProperty] private bool _showHintLegacyOptiFabric = true;

    [ObservableProperty] private bool _showHintOptiFabricOld = true;

    [ObservableProperty] private bool _showHintModOptiFine = true;

    [ObservableProperty] private Brush _minecraftForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _optiFineForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _liteLoaderForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _forgeForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _cleanroomForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _neoForgeForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _fabricForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _fabricApiForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _legacyFabricForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _legacyFabricApiForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _labyModForeground = ThemeManager.colorGray1;

    [ObservableProperty] private Brush _optiFabricForeground = ThemeManager.colorGray1;

    public static string GetVersionTypeTitle(string key) => key switch
    {
        "正式版" => Lang.Text("Download.Version.Type.Release"),
        "预览版" => Lang.Text("Download.Version.Type.Development"),
        "远古版" => Lang.Text("Download.Version.Type.BeforeRelease"),
        "愚人节版" => Lang.Text("Download.Version.Type.AprilFools"),
        _ => key
    };

    public static string? GetSelectLogo(LoaderSelection selection, string? vanillaIcon)
    {
        if (selection.Fabric is not null) return "pack://application:,,,/images/Blocks/Fabric.png";

        if (selection.LegacyFabric is not null) return "pack://application:,,,/images/Blocks/Fabric.png";

        if (selection.Forge is not null) return "pack://application:,,,/images/Blocks/Anvil.png";

        if (selection.NeoForge is not null) return "pack://application:,,,/images/Blocks/NeoForge.png";

        if (selection.HasLiteLoader) return "pack://application:,,,/images/Blocks/Egg.png";

        if (selection.OptiFine is not null) return "pack://application:,,,/images/Blocks/GrassPath.png";

        if (selection.Cleanroom is not null) return "pack://application:,,,/images/Blocks/Cleanroom.png";

        if (selection.LabyMod is not null) return "pack://application:,,,/images/Blocks/LabyMod.png";

        return vanillaIcon;
    }

    public static string GetSelectInfo(LoaderSelection selection)
    {
        var parts = new List<string>
        {
            selection.VanillaName ?? ""
        };

        var loaderInfos = new (string NameKey, string? Version)[]
        {
            ("Common.Installation.Fabric", selection.Fabric),
            ("Common.Installation.LegacyFabric", selection.LegacyFabric),
            ("Common.Installation.Forge", selection.Forge),
            ("Common.Installation.NeoForge", selection.NeoForge),
            ("Common.Installation.Cleanroom", selection.Cleanroom),
            ("Common.Installation.LabyMod", selection.LabyMod),
            ("Common.Installation.OptiFine", selection.OptiFine)
        };

        parts.AddRange(
            loaderInfos
                .Where(info => !string.IsNullOrWhiteSpace(info.Version))
                .Select(info => $"{Lang.Text(info.NameKey)} {info.Version}")
        );

        if (selection.HasLiteLoader) parts.Add(Lang.Text("Common.Installation.LiteLoader"));

        if (parts.Count == 1) parts.Add(Lang.Text("Instance.Install.NoExtraInstall"));

        return string.Join("  |  ", parts);
    }
}
