using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>下载安装向导页的视图模型。</summary>
public partial class PageDownloadInstallViewModel : ViewModelBase
{
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

    [ObservableProperty] private bool _showHintOptiFabricOld = true;

    [ObservableProperty] private bool _showHintLegacyOptiFabric = true;

    [ObservableProperty] private bool _showHintModOptiFine = true;

    [ObservableProperty] private bool _canStartInstall;
}
