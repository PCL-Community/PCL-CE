using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「联机」页的视图模型。</summary>
public partial class PageSetupGameLinkViewModel : ViewModelBase
{
    [ObservableProperty] private bool _enableCliOutput = Config.Link.EnableCliOutput;

    [ObservableProperty] private bool _enableIPv6 = Config.Link.EnableIPv6;

    [ObservableProperty]
    private string _ipv6StatusText = Lang.Text("Setup.GameLink.NetworkTest.Ipv6Status",
        Lang.Text("Setup.GameLink.NetworkTest.NotTested"));

    [ObservableProperty] private bool _latencyFirstMode = Config.Link.UseLatencyFirstMode;

    [ObservableProperty] private string _linkUsername = Config.Link.Username;

    [ObservableProperty] private bool _netTestEnabled = true;

    [ObservableProperty] private string _netTestButtonText = Lang.Text("Setup.GameLink.NetworkTest.Start");

    [ObservableProperty] private int _preferProtocolIndex = (int)Config.Link.ProtocolPreference;

    [ObservableProperty]
    private string _tcpNatTypeText = Lang.Text("Setup.GameLink.NetworkTest.TcpNatType",
        Lang.Text("Setup.GameLink.NetworkTest.NotTested"));

    [ObservableProperty] private bool _tryPunchSym = Config.Link.TryPunchSym;

    [ObservableProperty]
    private string _udpNatTypeText = Lang.Text("Setup.GameLink.NetworkTest.UdpNatType",
        Lang.Text("Setup.GameLink.NetworkTest.NotTested"));

    public void Reload()
    {
        LinkUsername = Config.Link.Username;
        LatencyFirstMode = Config.Link.UseLatencyFirstMode;
        PreferProtocolIndex = (int)Config.Link.ProtocolPreference;
        TryPunchSym = Config.Link.TryPunchSym;
        EnableIPv6 = Config.Link.EnableIPv6;
        EnableCliOutput = Config.Link.EnableCliOutput;
    }

    public void Reset()
    {
        try
        {
            Config.Link.Reset();
            Common.Log("[Setup] 已初始化联机页设置");
            HintService.Hint(Lang.Text("Setup.GameLink.Initialized"), HintType.Success, false);
            Reload();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.GameLink.Error.InitFailed"),
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.GameLink.Error.InitFailed"));
        }

        Reload();
    }

    public void SetConfigByTag(string tag, object value)
    {
        ConfigService.TrySetValue(tag, value);
    }

    public void SetProtocolPreference(int index)
    {
        try
        {
            Config.Link.ProtocolPreference = (LinkProtocolPreference)index;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.GameLink.Error.ConfigChangeFailed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Setup.GameLink.Error.ConfigChangeFailed"));
        }
    }

    public async Task RunNetworkTestAsync()
    {
        NetTestEnabled = false;
        NetTestButtonText = Lang.Text("Setup.GameLink.NetworkTest.Testing");
        try
        {
            var status = await RunAsync(ct => Task.Run(CliNetTest.GetNetStatusAsync, ct));
            if (status is null)
                return;
            UdpNatTypeText = Lang.Text("Setup.GameLink.NetworkTest.UdpNatType",
                CliNetTest.GetNatTypeString(status.UdpNatType));
            TcpNatTypeText = Lang.Text("Setup.GameLink.NetworkTest.TcpNatType",
                CliNetTest.GetNatTypeString(status.TcpNatType));
            Ipv6StatusText = Lang.Text("Setup.GameLink.NetworkTest.Ipv6Status",
                status.SupportIPv6
                    ? Lang.Text("Setup.GameLink.NetworkTest.Supported")
                    : Lang.Text("Setup.GameLink.NetworkTest.Unsupported"));
        }
        catch (Exception ex)
        {
            Common.Log(ex,
                "[Link] 获取网络测试结果失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Setup.GameLink.Error.NetworkTestFailed"));
        }
        finally
        {
            NetTestEnabled = true;
            NetTestButtonText = Lang.Text("Setup.GameLink.NetworkTest.Start");
        }
    }
}
