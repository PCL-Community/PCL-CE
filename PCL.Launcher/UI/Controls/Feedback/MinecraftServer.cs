using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Link.McPing;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Logging;
using PCL.Core.Minecraft;

namespace PCL.UI.Controls;

/// <summary>
///     Minecraft 服务器信息卡片：按 <see cref="Address" /> 查询状态，展示服务器图标、
///     MOTD、在线人数与延迟。服务器图标按 Base64 解码，缺省使用内置的
///     <c>DefaultServer.png</c>；MOTD 用内联文本渲染，字体取配置的 MOTD 字体。
/// </summary>
public class MinecraftServer : Grid
{
    private const string DefaultLogoUri =
        "avares://PCL.Launcher/Assets/Images/Icons/DefaultServer.png";

    private static readonly Bitmap? DefaultLogo = LoadDefaultLogo();

    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<MinecraftServer, string>(nameof(Address), string.Empty);

    private readonly Image _imgServerLogo;
    private readonly TextBlock _labServerDesc;
    private readonly TextBlock _labServerLatency;
    private readonly TextBlock _labServerPlayer;
    private readonly TextBlock _motdText;
    private bool _hasLatencyColor;

    private bool _isErrorText;
    private Action? _unwatchTheme;

    public MinecraftServer()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");

        _imgServerLogo = new Image
        {
            Name = "ImgServerLogo",
            Margin = new Thickness(6d),
            Width = 54d,
            Height = 54d,
            Stretch = Stretch.Uniform
        };
        _labServerDesc = new TextBlock
        {
            Name = "LabServerDesc",
            Margin = new Thickness(6d),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15d
        };
        _motdText = new TextBlock
        {
            Name = "MotdRenderer",
            Margin = new Thickness(0d, 20d, 3d, 1d),
            IsHitTestVisible = false,
            Width = 300d,
            VerticalAlignment = VerticalAlignment.Center
        };
        _labServerPlayer = new TextBlock
        {
            Name = "LabServerPlayer",
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            FontSize = 12d
        };
        _labServerLatency = new TextBlock
        {
            Name = "LabServerLatency",
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            FontSize = 12d
        };

        var rightPanel = new StackPanel
        {
            Margin = new Thickness(6d),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        rightPanel.Children.Add(_labServerPlayer);
        rightPanel.Children.Add(_labServerLatency);

        Children.Add(_imgServerLogo);
        SetColumn(_labServerDesc, 1);
        Children.Add(_labServerDesc);
        SetColumn(_motdText, 1);
        Children.Add(_motdText);
        SetColumn(rightPanel, 2);
        Children.Add(rightPanel);

        FeedbackTheme.AttachStyles(this);
        _ApplyTextColors();

        Loaded += (_, _) =>
        {
            _unwatchTheme?.Invoke();
            _unwatchTheme = FeedbackTheme.WatchTheme(this, _ApplyTextColors);
        };
        Unloaded += (_, _) =>
        {
            _unwatchTheme?.Invoke();
            _unwatchTheme = null;
        };
    }

    public string Address
    {
        get => GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != AddressProperty)
            return;

        var address = change.NewValue as string ?? string.Empty;
        Dispatcher.UIThread.Post(() => _ = UpdateServerInfoAsync(address));
    }

    public async Task UpdateServerInfoAsync(string address)
    {
        if (address is null)
            return;

        address = address.Replace("：", ":");
        // 预先重置 UI 状态
        _isErrorText = false;
        _hasLatencyColor = false;
        _ApplyTextColors();
        _labServerDesc.Text = Lang.Text("Tools.ServerQuery.State.Querying");
        _motdText.Inlines?.Clear();
        _labServerPlayer.Text = "-/-";
        ToolTip.SetTip(_labServerPlayer, null);
        _labServerLatency.Text = string.Empty;
        _imgServerLogo.Source = DefaultLogo;

        try
        {
            // 获取可达地址（DNS 解析）
            var addr = await ServerAddressResolver.GetResolvedServerAddressAsync(address);

            // Ping 服务器
            using var query = McPingServiceFactory.CreateService(addr.Host, addr.Ip, addr.Port);
            var result = await query.PingAsync();

            if (result is null)
                throw new Exception(Lang.Text("Tools.ServerQuery.State.NoInfo"));

            // 处理服务器图标
            await SetServerLogoAsync(result.Favicon);

            // 更新 UI
            UpdateServerStatus(result);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "MinecraftServer", "[MinecraftServer] 信息查询失败");
            _labServerDesc.Text = Lang.Text("Tools.ServerQuery.Error.UnableToConnect", ex.Message);
            _isErrorText = true;
            _labServerDesc.Foreground = Brushes.Red;
            _imgServerLogo.Source = DefaultLogo;
        }
    }

    private void UpdateServerStatus(McPingResult result)
    {
        // 延迟颜色判断
        var latencyColor = result.Latency < 150 ? 'a' : result.Latency < 400 ? '6' : 'c';

        // 更新描述
        _labServerDesc.Text = Lang.Text("Tools.ServerQuery.Title.MinecraftServer");
        MotdTextRenderer.Render(
            _motdText,
            result.Description,
            !FeedbackTheme.IsDarkTheme(this),
            2,
            14d,
            LaunchFonts.ResolveMotdFont());

        // 更新玩家信息
        _labServerPlayer.Text = $"{result.Players.Online}/{result.Players.Max}";
        _labServerLatency.Text = $"{result.Latency}ms";
        _hasLatencyColor = MotdTextRenderer.TryGetColor(latencyColor, out var color);
        _labServerLatency.Foreground = _hasLatencyColor
            ? new SolidColorBrush(color)
            : FeedbackTheme.GetBrush(this, "ColorBrush1", Brushes.Black);

        // 玩家列表提示
        if (result.Players.Samples is { Count: > 0 } samples)
        {
            ToolTip.SetTip(
                _labServerPlayer,
                string.Join("\r\n", samples.Select(sample => sample.Name)));
            ToolTip.SetPlacement(_labServerPlayer, PlacementMode.Pointer);
        }
    }

    private void _ApplyTextColors()
    {
        var text = Brushes.White;
        if (!_isErrorText)
            _labServerDesc.Foreground = text;
        _labServerPlayer.Foreground = text;
        if (!_hasLatencyColor)
            _labServerLatency.Foreground = text;
    }

    private async Task SetServerLogoAsync(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            _imgServerLogo.Source = DefaultLogo;
            return;
        }

        try
        {
            var bitmap = await Task.Run(() => DecodeBase64Image(base64));
            if (Dispatcher.UIThread.CheckAccess())
                _imgServerLogo.Source = bitmap;
            else
                Dispatcher.UIThread.Post(() => _imgServerLogo.Source = bitmap);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MinecraftServer", "[MinecraftServer] 服务器图标解析失败");
            _imgServerLogo.Source = DefaultLogo;
        }
    }

    private static Bitmap? LoadDefaultLogo()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(DefaultLogoUri, UriKind.Absolute));
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MinecraftServer", "[MinecraftServer] 默认服务器图标加载失败");
            return null;
        }
    }

    private static Bitmap? DecodeBase64Image(string base64)
    {
        var data = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
        if (string.IsNullOrWhiteSpace(data))
            return null;

        var bytes = Convert.FromBase64String(data);
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }
}