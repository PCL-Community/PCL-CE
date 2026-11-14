using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「游戏管理」页的视图模型。</summary>
public partial class PageSetupGameManageViewModel : ViewModelBase
{
    [ObservableProperty] private int _compSourceIndex = Math.Max(
        0,
        Config.Download.Comp.CompSourceSolution);

    [ObservableProperty] private bool _fileFormatSettingsVisible = Lang.IsChineseMainland;

    [ObservableProperty] private int _fileSourceIndex = Math.Max(0, Config.Download.FileSource);

    [ObservableProperty] private int _httpModeIndex = Math.Max(0, (int)Config.Download.HttpMode);

    [ObservableProperty] private int _localNameStyleIndex = Math.Max(
        0,
        Config.Download.Comp.UiCompNameSolution);

    [ObservableProperty] private int _quickBehaviorIndex = Math.Max(
        0,
        Config.Download.Comp.QuickDownloadBehavior);

    [ObservableProperty]
    private int _translateFormatIndex = Math.Max(0, Config.Download.Comp.NameFormatV2);

    [ObservableProperty] private int _versionListIndex = Math.Max(
        0,
        Config.Download.VersionListSource);

    partial void OnHttpModeIndexChanged(int value)
    {
        if (value >= 0) Config.Download.HttpMode = (DownloadHttpMode)value;
    }

    partial void OnFileSourceIndexChanged(int value)
    {
        if (value >= 0) Config.Download.FileSource = value;
    }

    partial void OnVersionListIndexChanged(int value)
    {
        if (value >= 0) Config.Download.VersionListSource = value;
    }

    partial void OnTranslateFormatIndexChanged(int value)
    {
        if (value >= 0) Config.Download.Comp.NameFormatV2 = value;
    }

    partial void OnCompSourceIndexChanged(int value)
    {
        if (value >= 0) Config.Download.Comp.CompSourceSolution = value;
    }

    partial void OnLocalNameStyleIndexChanged(int value)
    {
        if (value >= 0) Config.Download.Comp.UiCompNameSolution = value;
    }

    partial void OnQuickBehaviorIndexChanged(int value)
    {
        if (value >= 0) Config.Download.Comp.QuickDownloadBehavior = value;
    }

    public void Reload()
    {
        HttpModeIndex = Math.Clamp((int)Config.Download.HttpMode, 0, 2);
        FileSourceIndex = Math.Clamp(Config.Download.FileSource, 0, 2);
        VersionListIndex = Math.Clamp(Config.Download.VersionListSource, 0, 2);
        TranslateFormatIndex = Math.Clamp(Config.Download.Comp.NameFormatV2, 0, 4);
        CompSourceIndex = Math.Clamp(Config.Download.Comp.CompSourceSolution, 0, 2);
        LocalNameStyleIndex = Math.Clamp(Config.Download.Comp.UiCompNameSolution, 0, 1);
        QuickBehaviorIndex = Math.Clamp(Config.Download.Comp.QuickDownloadBehavior, 0, 3);
        FileFormatSettingsVisible = Lang.IsChineseMainland;
    }

    /// <summary>首次把下载线程调到 64 及以上时给出警告。</summary>
    public async Task WarnLargeThreadOnceAsync()
    {
        if (States.Hint.LargeDownloadThread) return;
        States.Hint.LargeDownloadThread = true;
        await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Warning"),
                Content = Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Message"),
                Buttons = DialogButtons.From(
                    DialogButton.Create(
                        Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Confirm"))),
                IsWarn = true,
                ForceWait = true
            });
    }
}