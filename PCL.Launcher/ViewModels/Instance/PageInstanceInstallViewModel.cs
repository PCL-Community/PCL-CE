using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Download;

namespace PCL;

/// <summary>实例安装/修改页的视图模型。</summary>
public partial class PageInstanceInstallViewModel : ViewModelBase
{
    private string _actionKey = "Instance.Install.Action.StartModify";

    private Func<string>? _selectedInfoProvider;

    public string ActionText => Lang.Text(_actionKey);

    [ObservableProperty] public partial bool CanStartInstall { get; set; }

    [ObservableProperty] public partial string CurrentInfo { get; set; } = "";

    public string SelectedInfo => _selectedInfoProvider?.Invoke() ?? string.Empty;

    [ObservableProperty] public partial string InstanceName { get; set; } = "";

    [ObservableProperty] public partial bool IsInstalling { get; set; }

    [ObservableProperty] public partial bool IsSelecting { get; set; }

    [ObservableProperty]
    public partial string VersionIcon { get; set; } =
        $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";

    /// <summary>Minecraft 卡片上的原版图标（与 WPF 的 _vanillaIcon 一致，不随加载器变化）。</summary>
    [ObservableProperty]
    public partial string MinecraftIcon { get; set; } =
        $"{LauncherPaths.ImageRoot}Blocks/Grass.png";

    [ObservableProperty] public partial string VersionName { get; set; } = "";

    [ObservableProperty] public partial string? VersionUrl { get; set; }

    internal InstallLoaderSelection Selection { get; } = new();

    public void SetAction(string key)
    {
        if (_actionKey == key)
            return;
        _actionKey = key;
        OnPropertyChanged(nameof(ActionText));
    }

    public void SetSelectedInfoProvider(Func<string>? provider)
    {
        _selectedInfoProvider = provider;
        OnPropertyChanged(nameof(SelectedInfo));
    }

    public void RefreshSelectedInfo()
    {
        OnPropertyChanged(nameof(SelectedInfo));
    }

    public void ApplySelection(McInstallRequest request)
    {
        Selection.ApplyTo(request);
    }

    public string GetLoaderSuffix()
    {
        return Selection.GetSuffix();
    }

    public string GetSelectionInfo()
    {
        return Selection.GetInfo(VersionName);
    }
}