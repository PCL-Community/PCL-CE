using System.Windows;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL;

public partial class PageSetupGameLink
{
    private bool IsFirstLoad = true;

    private new bool IsLoaded;

    public PageSetupGameLink()
    {
        Loaded += PageSetupLink_Loaded;
        Loaded += (_, __) => Reload();
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
            ModMain.Hint("已初始化联机页设置！", ModMain.HintType.Finish, false);
            Reload();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化联机页设置失败", ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private static void TextBoxChange(MyTextBox sender, object e) // , TextLinkRelay.ValidatedTextChanged
    {
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Text);
    }

    private static void
        ComboBoxChange(MyComboBox sender,
            object e) // Handles ComboRelayType.SelectionChanged, ComboServerType.SelectionChanged
    {
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.SelectedIndex);
    }

    private static void CheckBoxChange(MyCheckBox sender, object e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Checked);
    }

    private static void LinkProtocolPerferenceChange(MyComboBox sender, object e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            try
            {
                LinkProtocolPreference selection = (LinkProtocolPreference)sender.SelectedIndex;
                Config.Link.ProtocolPreference = selection;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "改变配置项失败", ModBase.LogLevel.Hint);
            }
    }

    // 网络测试
    private void BtnNetTest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnNetTest.IsEnabled = false;
            BtnNetTest.Text = "正在测试";
            ModBase.RunInNewThread(() =>
            {
                ;
                ModBase.RunInUi(() =>
                {
                    TextUdpNatType.Text =
                        "UDP NAT 类型: " + Scaffolding.EasyTier.CliNetTest.GetNatTypeString(status.UdpNatType);
                    TextTcpNatType.Text =
                        "TCP NAT 类型: " + Scaffolding.EasyTier.CliNetTest.GetNatTypeString(status.TcpNatType);
                    TextIpv6Status.Text = "IPv6: " + (status.SupportIPv6 ? "支持" : "不支持");
                    BtnNetTest.IsEnabled = true;
                    BtnNetTest.Text = "开始测试";
                });
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Link] 获取网络测试结果失败", ModBase.LogLevel.Hint);
            BtnNetTest.IsEnabled = true;
            BtnNetTest.Text = "开始测试";
        }
    }
}