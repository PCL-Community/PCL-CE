using PCL.Core.App;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>联机设置页：EasyTier 用户名、传输协议偏好与中继策略开关，全部写入 <c>Config.Link</c>。</summary>
public partial class PageSetupGameLink : MyPageRight, ISetupResetPage
{
    private bool _loaded;
    private bool _reloading;

    public PageSetupGameLink()
    {
        InitializeComponent();
        PanScroll = PanBack;

        TextLinkUsername.TextChanged += (_, _) =>
        {
            if (!_reloading) Config.Link.Username = TextLinkUsername.Text ?? string.Empty;
        };
        ComboPreferProtocol.SelectionChanged += (_, _) =>
        {
            if (_reloading || ComboPreferProtocol.SelectedIndex < 0) return;
            Config.Link.ProtocolPreference =
                (LinkProtocolPreference)ComboPreferProtocol.SelectedIndex;
        };
        CheckLatencyFirstMode.Change += (_, _) =>
        {
            if (!_reloading)
                Config.Link.UseLatencyFirstMode = CheckLatencyFirstMode.Checked == true;
        };
        CheckTryPunchSym.Change += (_, _) =>
        {
            if (!_reloading) Config.Link.TryPunchSym = CheckTryPunchSym.Checked == true;
        };
        CheckEnableIPv6.Change += (_, _) =>
        {
            if (!_reloading) Config.Link.EnableIPv6 = CheckEnableIPv6.Checked == true;
        };
        CheckEnableCliOutput.Change += (_, _) =>
        {
            if (!_reloading) Config.Link.EnableCliOutput = CheckEnableCliOutput.Checked == true;
        };

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
        };
    }

    public void Reset()
    {
        Config.Link.Reset();
        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        if (_loaded) Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Reload()
    {
        _reloading = true;
        try
        {
            TextLinkUsername.Text = Config.Link.Username;
            ComboPreferProtocol.SelectedIndex = (int)Config.Link.ProtocolPreference;
            CheckLatencyFirstMode.Checked = Config.Link.UseLatencyFirstMode;
            CheckTryPunchSym.Checked = Config.Link.TryPunchSym;
            CheckEnableIPv6.Checked = Config.Link.EnableIPv6;
            CheckEnableCliOutput.Checked = Config.Link.EnableCliOutput;
        }
        finally
        {
            _reloading = false;
        }
    }
}