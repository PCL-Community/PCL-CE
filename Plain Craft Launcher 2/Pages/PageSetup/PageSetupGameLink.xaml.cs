using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link.Scaffolding.EasyTier;

namespace PCL;

public partial class PageSetupGameLink
{
    private bool IsFirstLoad = true;

    private new bool IsLoaded;

    public PageSetupGameLink()
    {
        InitializeComponent();
        TextUdpNatType.Text = Lang.Text("Setup.GameLink.NetworkTest.UdpNatType", Lang.Text("Setup.GameLink.NetworkTest.NotTested"));
        TextTcpNatType.Text = Lang.Text("Setup.GameLink.NetworkTest.TcpNatType", Lang.Text("Setup.GameLink.NetworkTest.NotTested"));
        TextIpv6Status.Text = Lang.Text("Setup.GameLink.NetworkTest.Ipv6Status", Lang.Text("Setup.GameLink.NetworkTest.NotTested"));
        Loaded += PageSetupLink_Loaded;
        Loaded += (_, _) => Reload();
    }

    private void PageSetupLink_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (IsLoaded)
            return;
        IsLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        TextLinkUsername.Text = Config.Link.Username;
        // TextLinkRelay.Text = Config.Link.RelayServer
        // ComboRelayType.SelectedIndex = Config.Link.RelayType
        // ComboServerType.SelectedIndex = Config.Link.ServerType
        CheckLatencyFirstMode.Checked = Config.Link.UseLatencyFirstMode;
        ComboPreferProtocol.SelectedIndex = (int)Config.Link.ProtocolPreference;
        CheckTryPunchSym.Checked = Config.Link.TryPunchSym;
        CheckEnableIPv6.Checked = Config.Link.EnableIPv6;
        CheckEnableCliOutput.Checked = Config.Link.EnableCliOutput;

        // TextRelays.Text = "正在获取信息..."
        // Do While Not (PageLinkLobby.LobbyAnnouncementLoader.State = LoadState.Finished OrElse PageLinkLobby.LobbyAnnouncementLoader.State = LoadState.Failed)
        // Thread.Sleep(500)
        // Loop
        // If ETRelay.RelayList.Count > 0 Then
        // TextRelays.Text = ""
        // For Each Relay In ETRelay.RelayList
        // Select Case Relay.Type
        // Case ETRelayType.Community
        // TextRelays.Text += "[社区] "
        // Case ETRelayType.Selfhosted
        // TextRelays.Text += "[自有] "
        // Case Else 'ETRelayType.Custom
        // TextRelays.Text += "[自定义] "
        // End Select
        // TextRelays.Text += Relay.Name & "，"
        // Next
        // TextRelays.Text = TextRelays.Text.BeforeLast("，")
        // Else
        // TextRelays.Text = "暂无，你可能需要手动添加中继服务器"
        // End If
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Link.Reset();
            ModBase.Log("[Setup] 已初始化联机页设置");
            ModMain.Hint(Lang.Text("Setup.GameLink.Initialized"), ModMain.HintType.Finish, false);
            Reload();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.GameLink.Error.InitFailed"), ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void TextBoxChange(object senderRaw, TextChangedEventArgs e) // , TextLinkRelay.ValidatedTextChanged
    {
        var sender = (MyTextBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetGameLinkByTag(sender.Tag?.ToString(), sender.Text);
    }

    private static void
        ComboBoxChange(MyComboBox sender,
            object e) // Handles ComboRelayType.SelectionChanged, ComboServerType.SelectionChanged
    {
        if (ModAnimation.AniControlEnabled == 0)
            SetGameLinkByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetGameLinkByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private static void SetGameLinkByTag(string tag, object value)
    {
        switch (tag)
        {
            case "LinkUsername": Config.Link.Username = (string)value; break;
            case "LinkRelayServer": Config.Link.CustomRelayServer = (string)value; break;
            case "LinkRelayType": Config.Link.RelayType = (LinkRelayBehavior)(int)value; break;
            case "LinkServerType": Config.Link.ServerType = (int)value; break;
            case "LinkProtocolPreference": Config.Link.ProtocolPreference = (LinkProtocolPreference)(int)value; break;
            case "LinkLatencyFirstMode": Config.Link.UseLatencyFirstMode = (bool)value; break;
            case "LinkTryPunchSym": Config.Link.TryPunchSym = (bool)value; break;
            case "LinkEnableIPv6": Config.Link.EnableIPv6 = (bool)value; break;
            case "LinkEnableCliOutput": Config.Link.EnableCliOutput = (bool)value; break;
        }
    }

    private void LinkProtocolPerferenceChange(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            try
            {
                var selection = (LinkProtocolPreference)((MyComboBox)sender).SelectedIndex;
                Config.Link.ProtocolPreference = selection;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Setup.GameLink.Error.ConfigChangeFailed"), ModBase.LogLevel.Hint);
            }
    }

    // 网络测试
    private void BtnNetTest_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            BtnNetTest.IsEnabled = false;
            BtnNetTest.Text = Lang.Text("Setup.GameLink.NetworkTest.Testing");
            ModBase.RunInNewThread(() =>
            {
                var status = CliNetTest.GetNetStatusAsync().GetAwaiter().GetResult();
                ModBase.RunInUi(() =>
                {
                    TextUdpNatType.Text =
                        Lang.Text("Setup.GameLink.NetworkTest.UdpNatType", CliNetTest.GetNatTypeString(status.UdpNatType));
                    TextTcpNatType.Text =
                        Lang.Text("Setup.GameLink.NetworkTest.TcpNatType", CliNetTest.GetNatTypeString(status.TcpNatType));
                    TextIpv6Status.Text = Lang.Text("Setup.GameLink.NetworkTest.Ipv6Status",
                        status.SupportIPv6
                            ? Lang.Text("Setup.GameLink.NetworkTest.Supported")
                            : Lang.Text("Setup.GameLink.NetworkTest.Unsupported"));
                    BtnNetTest.IsEnabled = true;
                    BtnNetTest.Text = Lang.Text("Setup.GameLink.NetworkTest.Start");
                });
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Link] 获取网络测试结果失败", ModBase.LogLevel.Hint);
            BtnNetTest.IsEnabled = true;
            BtnNetTest.Text = Lang.Text("Setup.GameLink.NetworkTest.Start");
        }
    }
}