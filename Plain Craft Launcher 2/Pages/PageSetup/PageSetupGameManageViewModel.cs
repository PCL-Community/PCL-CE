using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「游戏管理」页的视图模型。</summary>
public partial class PageSetupGameManageViewModel : ViewModelBase
{
    [ObservableProperty] private int _compSourceIndex = Math.Max(0, Config.Download.Comp.CompSourceSolution);

    [ObservableProperty] private int _fileSourceIndex = Math.Max(0, Config.Download.FileSource);

    [ObservableProperty] private int _httpModeIndex = Math.Max(0, (int)Config.Download.HttpMode);

    [ObservableProperty] private int _localNameStyleIndex = Math.Max(0, Config.Download.Comp.UiCompNameSolution);

    [ObservableProperty] private int _quickBehaviorIndex = Math.Max(0, Config.Download.Comp.QuickDownloadBehavior);

    [ObservableProperty] private int _translateFormatIndex = Math.Max(0, Config.Download.Comp.NameFormatV2);

    [ObservableProperty] private int _versionListIndex = Math.Max(0, Config.Download.VersionListSource);

    public void Reload()
    {
        HttpModeIndex = Math.Max(0, (int)Config.Download.HttpMode);
        FileSourceIndex = Math.Max(0, Config.Download.FileSource);
        VersionListIndex = Math.Max(0, Config.Download.VersionListSource);
        TranslateFormatIndex = Math.Max(0, Config.Download.Comp.NameFormatV2);
        CompSourceIndex = Math.Max(0, Config.Download.Comp.CompSourceSolution);
        LocalNameStyleIndex = Math.Max(0, Config.Download.Comp.UiCompNameSolution);
        QuickBehaviorIndex = Math.Max(0, Config.Download.Comp.QuickDownloadBehavior);
    }
}
