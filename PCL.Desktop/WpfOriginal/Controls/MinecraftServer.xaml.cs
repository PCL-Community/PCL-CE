using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.Core.Link.McPing;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Minecraft;
using PCL.Core.UI;

namespace PCL;

public partial class MinecraftServer : Grid
{
    private const string fallbackImageUri =
        "pack://application:,,,/Plain Craft Launcher 2;component/Images/Icons/DefaultServer.png";

    private static readonly DependencyProperty AddressProperty = DependencyProperty.Register(nameof(Address),
        typeof(string), typeof(MinecraftServer), new PropertyMetadata(string.Empty, OnAddressChanged));

    public MinecraftServer()
    {
        InitializeComponent();
    }

    public string Address
    {
        get => (string)(GetValue(AddressProperty));
        set => SetValue(AddressProperty, value);
    }

    private static void OnAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var server = (MinecraftServer)d;
        d.Dispatcher.BeginInvoke(new Func<Task>(() => server.UpdateServerInfoAsync(e.NewValue?.ToString())));
    }

    public async Task UpdateServerInfoAsync(string address)
    {
        if (address is null)
            return;
        address = address.Replace("：", ":");
        // 预先重置UI状态
        LabServerDesc.Foreground = Brushes.White;
        LabServerDesc.Text = Lang.Text("Tools.ServerQuery.State.Querying");
        LabServerPlayer.Text = "-/-";
        LabServerPlayer.ToolTip = null;
        ImageLoaderHelper.SetFallbackImage(ImgServerLogo, fallbackImageUri);

        try
        {
            // 获取可达地址（DNS解析）
            var addr = await ServerAddressResolver.GetResolvedServerAddressAsync(address);

            // Ping服务器
            using (var query = McPingServiceFactory.CreateService(addr.Host, addr.Ip, addr.Port))
            {
                var ret = await query.PingAsync();

                if (ret is null) throw new Exception(Lang.Text("Tools.ServerQuery.State.NoInfo"));

                // 处理服务器图标
                await ImageLoaderHelper.SetServerLogoAsync(ret.Favicon, ImgServerLogo);

                // 更新UI
                UpdateServerStatus(ret);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[MinecraftServer] 信息查询失败");
            LabServerDesc.Text = Lang.Text("Tools.ServerQuery.Error.UnableToConnect", ex.Message);
            LabServerDesc.Foreground = Brushes.Red;
            ImageLoaderHelper.SetFallbackImage(ImgServerLogo, fallbackImageUri);
        }
    }

    private void UpdateServerStatus(McPingResult ret)
    {
        // 延迟颜色判断
        var latencyColor = ret.Latency < 150 ? "a" : ret.Latency < 400 ? "6" : "c";

        // 更新描述
        LabServerDesc.Text = Lang.Text("Tools.ServerQuery.Title.MinecraftServer");
        MotdRenderer.RenderMotd(ret.Description, false, 2, 14);
        MotdRenderer.RenderCanvas();

        // 更新玩家信息
        var playerText = $"{ret.Players.Online}/{ret.Players.Max}{"\r\n"}§{latencyColor}{ret.Latency}ms";
        ModStyle.MinecraftFormatter.SetColorfulTextLab(playerText, LabServerPlayer, false);

        // 玩家列表提示
        if (ret.Players.Samples.Any())
        {
            LabServerPlayer.ToolTip = string.Join("\r\n", ret.Players.Samples.Select(x => x.Name));
            ToolTipService.SetPlacement(LabServerPlayer, PlacementMode.Mouse);
        }
    }
}