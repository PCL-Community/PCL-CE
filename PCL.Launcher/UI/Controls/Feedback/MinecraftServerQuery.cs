using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.Validate;

namespace PCL.UI.Controls;

/// <summary>
///     服务器信息查询面板：地址输入框（禁止输入 <c>/</c>）、查询按钮，以及查询后显示的
///     <see cref="MinecraftServer" /> 卡片；卡片背景为平铺的 <c>server_bg.png</c>。
/// </summary>
public class MinecraftServerQuery : Grid
{
    private const string BackgroundUri =
        "avares://PCL.Launcher/Assets/Images/Backgrounds/server_bg.png";

    private static readonly Bitmap? ServerBackground = LoadServerBackground();
    private readonly MyButton _btnServerQuery;

    private readonly MyTextBox _labServerIp;
    private readonly Border _serverInfo;

    public MinecraftServerQuery()
    {
        RowDefinitions = new RowDefinitions("Auto,Auto");
        ColumnDefinitions = new ColumnDefinitions("*,Auto");

        _labServerIp = new MyTextBox
        {
            Name = "LabServerIp",
            HintText = Lang.Text("Tools.ServerQuery.Address.Hint"),
            Margin = new Thickness(0d, 0d, 15d, 0d),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _labServerIp.ValidateRules.Add(new BlacklistValidator(["/"]));

        _btnServerQuery = new MyButton
        {
            Name = "BtnServerQuery",
            Text = Lang.Text("Tools.ServerQuery.Action.Query"),
            Width = 60d,
            Height = double.NaN,
            MinHeight = 28d,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _btnServerQuery.Click += (_, _) => _ = ServerQueryAsync();

        Server = new MinecraftServer
        {
            Name = "PanMcServer"
        };
        _serverInfo = new Border
        {
            Name = "ServerInfo",
            Margin = new Thickness(0d, 7d, 0d, 0d),
            IsVisible = false,
            CornerRadius = new CornerRadius(5d),
            BorderThickness = new Thickness(2d),
            Child = Server
        };
        if (ServerBackground is not null)
            _serverInfo.Background = new ImageBrush(ServerBackground)
            {
                Stretch = Stretch.Uniform,
                TileMode = TileMode.Tile,
                DestinationRect = new RelativeRect(0d, 0d, 124d, 124d, RelativeUnit.Absolute)
            };
        else
            FeedbackTheme.BindDynamic(_serverInfo, Border.BackgroundProperty, "ColorBrushWhite");

        FeedbackTheme.BindDynamic(_serverInfo, Border.BorderBrushProperty, "ColorBrush5");
        SetRow(_serverInfo, 1);
        SetColumnSpan(_serverInfo, 2);

        Children.Add(_labServerIp);
        SetColumn(_btnServerQuery, 1);
        Children.Add(_btnServerQuery);
        Children.Add(_serverInfo);

        FeedbackTheme.AttachStyles(this);
    }

    /// <summary>供页面直接访问内部服务器卡片。</summary>
    public MinecraftServer Server { get; }

    public string Address
    {
        get => _labServerIp.Text ?? string.Empty;
        set => _labServerIp.Text = value;
    }

    private async Task ServerQueryAsync()
    {
        _labServerIp.Validate();
        await Server.UpdateServerInfoAsync(_labServerIp.Text ?? string.Empty);
        _serverInfo.IsVisible = true;
    }

    private static Bitmap? LoadServerBackground()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(BackgroundUri, UriKind.Absolute));
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MinecraftServerQuery", "[MinecraftServerQuery] 服务器卡片背景加载失败");
            return null;
        }
    }
}