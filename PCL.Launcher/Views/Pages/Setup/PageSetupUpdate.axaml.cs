using Avalonia.Threading;
using PCL.Core.App;
using PCL.UI.Controls;
using PCL.UI.Seams;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     更新设置页：更新通道、自动更新行为、MirrorChyan CDK 与检查更新入口。
///     检查更新、安装进度与更新日志由 <see cref="PageSetupUpdateViewModel" /> 负责。
/// </summary>
public partial class PageSetupUpdate : MyPageRight, ISeamSetupUpdatePage
{
    private bool _loaded;
    private bool _reloading;

    public PageSetupUpdate()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        ComboSystemUpdateChannel.SelectionChanged += async (_, _) =>
            await OnChannelChangedAsync();
        TextMirrorCDK.TextChanged += (_, _) =>
        {
            if (!_reloading) ViewModel.MirrorChyanKey = TextMirrorCDK.Text ?? string.Empty;
        };
        BtnGetMirrorCDK.Click += (_, _) => Basics.OpenPath("https://mirrorchyan.com/");
        BtnCheckAgain.Click += async (_, _) => await ViewModel.CheckUpdateAsync();
        BtnChangelog.Click += (_, _) => PageSetupUpdateViewModel.OpenReleasesPage();
        BtnChangelogDetail.Click += async (_, _) => await ViewModel.ShowChangelogAsync();
        BtnUpdate.Click += async (_, _) => await ViewModel.StartUpdateAsync();
        BtnUpdate.IsVisible = OperatingSystem.IsWindows();

        SeamHosts.SetupUpdatePage = this;

        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
            await ViewModel.CheckUpdateAsync();
        };
    }

    public PageSetupUpdateViewModel ViewModel { get; } = new();

    public void SetUpdateButton(string text, bool enabled)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetUpdateButton(text, enabled));
            return;
        }

        ViewModel.UpdateButtonText = text;
        ViewModel.UpdateButtonEnabled = enabled;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        if (!_loaded) return;
        Reload();
        _ = ViewModel.SyncUpdateButtonStateAsync();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (!_loaded) return;
        ViewModel.Reload();
        ViewModel.RefreshStatusText();
        _ = ViewModel.SyncUpdateButtonStateAsync();
    }

    private void Reload()
    {
        _reloading = true;
        try
        {
            ViewModel.Reload();
            ViewModel.MirrorChyanKey = Config.Update.MirrorChyanKey;
            ComboSystemUpdateChannel.SelectedIndex = Math.Clamp(
                (int)Config.Update.UpdateChannel,
                0,
                2);
            ComboSystemUpdateMode.SelectedIndex = Math.Clamp(
                (int)Config.Update.UpdateMode,
                0,
                3);
        }
        finally
        {
            _reloading = false;
        }
    }

    private async Task OnChannelChangedAsync()
    {
        if (_reloading || ComboSystemUpdateChannel.SelectedIndex < 0) return;
        var index = ComboSystemUpdateChannel.SelectedIndex;
        if (!await ViewModel.ChangeChannelAsync(index))
        {
            _reloading = true;
            try
            {
                ComboSystemUpdateChannel.SelectedIndex = Math.Clamp(
                    (int)Config.Update.UpdateChannel,
                    0,
                    2);
            }
            finally
            {
                _reloading = false;
            }

            return;
        }

        await ViewModel.CheckUpdateAsync();
    }
}