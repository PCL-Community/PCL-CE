using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     联机设置页：EasyTier 用户名、传输协议偏好与中继策略开关，全部写入 <c>Config.Link</c>。
///     状态由 <see cref="PageSetupGameLinkViewModel" /> 持有；NAT/IPv6 测试卡按 WPF 默认折叠。
/// </summary>
public partial class PageSetupGameLink : MyPageRight, ISetupResetPage
{
    private bool _loaded;

    public PageSetupGameLink()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        BtnNetTest.Click += async (_, _) =>
        {
            try
            {
                await ViewModel.RunNetworkTestAsync();
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    "[Link] 获取网络测试结果失败",
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Setup.GameLink.Error.NetworkTestFailed"));
            }
        };

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
        };
    }

    public PageSetupGameLinkViewModel ViewModel { get; } = new();

    public void Reset()
    {
        ViewModel.Reset();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        if (_loaded) Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (_loaded) ViewModel.RefreshLocalization();
    }

    public void Reload()
    {
        ViewModel.Reload();
    }
}