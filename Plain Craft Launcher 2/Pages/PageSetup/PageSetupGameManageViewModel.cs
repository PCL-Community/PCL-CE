using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「游戏管理」页的视图模型。</summary>
public partial class PageSetupGameManageViewModel : ViewModelBase
{
    [ObservableProperty] private int _compSourceIndex = -1;

    [ObservableProperty] private int _fileSourceIndex = -1;

    [ObservableProperty] private int _httpModeIndex = -1;

    [ObservableProperty] private int _localNameStyleIndex = -1;

    [ObservableProperty] private int _quickBehaviorIndex = -1;

    [ObservableProperty] private int _translateFormatIndex = -1;

    [ObservableProperty] private int _versionListIndex = -1;

    public void Reload()
    {
        HttpModeIndex = (int)Config.Download.HttpMode;
        FileSourceIndex = Config.Download.FileSource;
        VersionListIndex = Config.Download.VersionListSource;
        TranslateFormatIndex = Config.Download.Comp.NameFormatV2;
        CompSourceIndex = Config.Download.Comp.CompSourceSolution;
        LocalNameStyleIndex = Config.Download.Comp.UiCompNameSolution;
        QuickBehaviorIndex = Config.Download.Comp.QuickDownloadBehavior;
    }
}
