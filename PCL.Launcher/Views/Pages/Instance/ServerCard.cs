using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     服务器卡片：与 WPF 版一致的单行 44px 布局，左侧图标、名称、延迟/人数、MotD，
///     右侧提供进入服务器与设置菜单（刷新、复制地址、编辑、移除）。
/// </summary>
public class ServerCard : MyCard
{
    private const string DefaultLogoUri =
        "avares://PCL.Launcher/Assets/Images/Icons/DefaultServer.png";

    private static readonly Bitmap? DefaultLogo = LoadDefaultLogo();
    private readonly MyIconButton _btnSettings;

    private readonly Image _icon;
    private readonly TextBlock _motdText;
    private readonly TextBlock _nameText;
    private readonly TextBlock _playerText;
    private readonly SvgIconControl _signal;
    private string _lastLogoBase64 = string.Empty;

    public ServerCard(
        string name,
        string address,
        int rawIndex) : this(new ServerCardViewModel(name, address, rawIndex))
    {
    }

    public ServerCard(ServerCardViewModel viewModel)
    {
        ViewModel = viewModel;
        Margin = new Thickness(0d, 8d, 0d, 8d);
        UseAnimation = false;

        var root = new Grid
        {
            Height = 44d,
            ColumnDefinitions = new ColumnDefinitions("6,34,7,*,350,60"),
            RowDefinitions = new RowDefinitions("*,17,18,*"),
            Background = Brushes.Transparent
        };
        ContainerTheme.BindDynamic(root, BackgroundProperty, "ColorBrushSemiTransparent");

        _icon = new Image
        {
            Width = 34d,
            Height = 34d,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            ClipToBounds = true
        };
        SetRow(_icon, 1);
        SetRowSpan(_icon, 2);
        SetColumn(_icon, 1);
        root.Children.Add(_icon);

        _nameText = new TextBlock
        {
            Text = viewModel.NameText,
            FontSize = 14d,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            MaxWidth = 180d
        };
        ContainerTheme.BindDynamic(_nameText, TextBlock.ForegroundProperty, "ColorBrush1");
        ToolTip.SetTip(_nameText, viewModel.NameText);
        ToolTip.SetPlacement(_nameText, PlacementMode.Top);
        SetRow(_nameText, 1);
        SetColumn(_nameText, 3);
        root.Children.Add(_nameText);

        var statusPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _signal = new SvgIconControl
        {
            Name = "Signal",
            Width = 20d,
            Height = 20d,
            Icon = viewModel.SignalIcon
        };
        ContainerTheme.BindDynamic(_signal, SvgIconControl.IconBrushProperty, "ColorBrushGray4");
        statusPanel.Children.Add(_signal);
        _playerText = new TextBlock
        {
            Text = viewModel.PlayerText,
            FontSize = 12d,
            Opacity = 0.4d,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ContainerTheme.BindDynamic(_playerText, TextBlock.ForegroundProperty, "ColorBrushGray3");
        statusPanel.Children.Add(_playerText);
        SetRow(statusPanel, 2);
        SetColumn(statusPanel, 3);
        root.Children.Add(statusPanel);

        _motdText = new TextBlock
        {
            Margin = new Thickness(0d, 0d, 3d, 1d),
            FontSize = 12d,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        ContainerTheme.BindDynamic(_motdText, TextBlock.ForegroundProperty, "ColorBrushGray4");
        SetRow(_motdText, 1);
        SetRowSpan(_motdText, 2);
        SetColumn(_motdText, 4);
        root.Children.Add(_motdText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 4d, 0d)
        };
        var connect = new MyIconButton
        {
            Width = 24d,
            SvgIcon = "lucide/play"
        };
        connect.Click += (_, _) => ConnectRequested?.Invoke(this);
        buttons.Children.Add(connect);

        _btnSettings = new MyIconButton
        {
            Width = 24d,
            LogoScale = 1.2d,
            SvgIcon = "lucide/settings",
            ContextMenu = BuildContextMenu()
        };
        _btnSettings.Click += (_, _) => _btnSettings.ContextMenu?.Open(_btnSettings);
        buttons.Children.Add(_btnSettings);
        SetRow(buttons, 1);
        SetRowSpan(buttons, 2);
        SetColumn(buttons, 5);
        root.Children.Add(buttons);

        ContextMenu = BuildContextMenu();
        BorderChild = root;
        ViewModel.PropertyChanged += (_, _) => Dispatcher.UIThread.Post(ApplyState);
        ApplyState();
    }

    public new string Name => ViewModel.Name;

    /// <summary>该卡片对应 servers.dat 中 servers 列表的原始下标（包含空 IP 项）。</summary>
    public int RawIndex => ViewModel.RawIndex;

    public string Address => ViewModel.Address;

    /// <summary>最近一次 ping 成功返回的图标（Base64），用于回写 servers.dat。</summary>
    public string IconBase64 => ViewModel.IconBase64;

    public bool HasPinged => ViewModel.HasPinged;

    public ServerCardViewModel ViewModel { get; }

    public event Action<ServerCard>? ConnectRequested;

    public event Action<ServerCard>? CopyRequested;

    public event Action<ServerCard>? EditRequested;

    public event Action<ServerCard>? RemoveRequested;

    public event Action<ServerCard>? RefreshRequested;

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        var refresh = new MyMenuItem
        {
            Header = Lang.Text("Common.Action.Refresh"),
            SvgIcon = "lucide/refresh-cw"
        };
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this);
        menu.Items.Add(refresh);

        var copy = new MyMenuItem
        {
            Header = Lang.Text("Instance.Server.Card.CopyAddress"),
            SvgIcon = "lucide/copy"
        };
        copy.Click += (_, _) => CopyRequested?.Invoke(this);
        menu.Items.Add(copy);

        var edit = new MyMenuItem
        {
            Header = Lang.Text("Instance.Server.Card.Edit"),
            SvgIcon = "lucide/pencil"
        };
        edit.Click += (_, _) => EditRequested?.Invoke(this);
        menu.Items.Add(edit);

        menu.Items.Add(new Separator());

        var remove = new MyMenuItem
        {
            Header = Lang.Text("Instance.Server.Card.Remove"),
            SvgIcon = "lucide/trash-2"
        };
        remove.Click += (_, _) => RemoveRequested?.Invoke(this);
        menu.Items.Add(remove);
        return menu;
    }

    public void UpdateInfo(string name, string address)
    {
        ViewModel.UpdateInfo(name, address);
        ApplyState();
    }

    public Task RefreshAsync(bool withHint = false, CancellationToken token = default)
    {
        return ViewModel.RefreshAsync(withHint, token);
    }

    private void ApplyState()
    {
        _nameText.Text = ViewModel.NameText;
        ToolTip.SetTip(_nameText, ViewModel.NameText);
        _signal.Icon = ViewModel.SignalIcon;
        if (string.IsNullOrEmpty(ViewModel.SignalTooltip))
        {
            ToolTip.SetTip(_signal, null);
        }
        else
        {
            ToolTip.SetTip(_signal, ViewModel.SignalTooltip);
            ToolTip.SetPlacement(_signal, PlacementMode.Top);
        }

        _playerText.Text = ViewModel.PlayerText;
        if (ViewModel.IsOnline && ViewModel.LastPingResult is { } result)
            MotdTextRenderer.Render(
                _motdText,
                result.Description,
                !FeedbackTheme.IsDarkTheme(this),
                2,
                12d,
                LaunchFonts.ResolveMotdFont());
        else
            _motdText.Text = ViewModel.MotdText;

        if (!string.Equals(_lastLogoBase64, ViewModel.IconBase64, StringComparison.Ordinal))
        {
            _lastLogoBase64 = ViewModel.IconBase64;
            _ = SetLogoAsync(ViewModel.IconBase64);
        }
    }

    private async Task SetLogoAsync(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            _icon.Source = DefaultLogo;
            return;
        }

        try
        {
            var bitmap = await Task.Run(() => DecodeBase64Image(base64));
            Dispatcher.UIThread.Post(() =>
            {
                if (string.Equals(_lastLogoBase64, base64, StringComparison.Ordinal))
                    _icon.Source = bitmap ?? DefaultLogo;
            });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "解析服务器图标失败");
            _icon.Source = DefaultLogo;
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

    private static Bitmap? LoadDefaultLogo()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(DefaultLogoUri, UriKind.Absolute));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}