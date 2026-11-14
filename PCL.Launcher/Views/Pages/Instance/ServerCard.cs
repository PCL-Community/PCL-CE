using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     服务器列表行：与资源页保持一致的 44px 紧凑布局。
///     左侧 34px 图标，中间名称/地址/MotD，右侧固定操作按钮。
/// </summary>
public partial class ServerCard : Grid
{
    private const string DefaultLogoUri =
        "avares://PCL.Launcher/Assets/Images/Icons/DefaultServer.png";

    private static readonly Bitmap? DefaultLogo = LoadDefaultLogo();
    private readonly int _uuid = LauncherCommon.GetUuid();
    private bool _isMouseDown;
    private string? _lastLogoBase64;
    private string? _stateLast;

    public ServerCard()
    {
        ViewModel = null!;
        InitializeComponent();
    }

    public ServerCard(
        string name,
        string address,
        int rawIndex) : this(new ServerCardViewModel(name, address, rawIndex))
    {
    }

    public ServerCard(ServerCardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;

        BtnConnect.Click += (_, _) => ConnectRequested?.Invoke(this);
        BtnSetting.Click += (_, _) => BtnSetting.ContextMenu?.Open(BtnSetting);
        ContextMenuHelper.Attach(BtnSetting, () => BtnSetting.ContextMenu);
        ToolTip.SetPlacement(ServerName, PlacementMode.Top);

        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) =>
        {
            _isMouseDown = false;
            RefreshColor();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) =>
        {
            _isMouseDown = false;
            RefreshColor();
        };
        Loaded += (_, _) =>
        {
            _stateLast = null;
            RefreshColor();
        };

        var weakCard = new WeakReference<ServerCard>(this);
        ViewModel.PropertyChanged += (_, _) =>
        {
            if (weakCard.TryGetTarget(out var card))
                Dispatcher.UIThread.Post(card.ApplyState);
        };
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

    public void UpdateInfo(string name, string address)
    {
        ViewModel.UpdateInfo(name, address);
        ApplyState();
    }

    public Task RefreshAsync(bool withHint = false, CancellationToken token = default)
    {
        return ViewModel.RefreshAsync(withHint, token);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || IsPointerOnActionButton(e.Source as Visual))
            return;
        _isMouseDown = true;
        RefreshColor();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isMouseDown = false;
        RefreshColor();
    }

    private void MenuRefresh_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this);
    }

    private void MenuCopy_Click(object? sender, RoutedEventArgs e)
    {
        CopyRequested?.Invoke(this);
    }

    private void MenuEdit_Click(object? sender, RoutedEventArgs e)
    {
        EditRequested?.Invoke(this);
    }

    private void MenuRemove_Click(object? sender, RoutedEventArgs e)
    {
        RemoveRequested?.Invoke(this);
    }

    private void ApplyState()
    {
        ServerName.Text = ViewModel.NameText;
        ToolTip.SetTip(ServerName, ViewModel.NameText);
        ServerAddress.Text = ViewModel.Address;
        ServerPlayer.Text = ViewModel.PlayerText;
        ServerPlayer.IsVisible = !string.IsNullOrWhiteSpace(ViewModel.PlayerText);
        Signal.Icon = ViewModel.SignalIcon;
        if (string.IsNullOrEmpty(ViewModel.SignalTooltip))
        {
            ToolTip.SetTip(Signal, null);
        }
        else
        {
            ToolTip.SetTip(Signal, ViewModel.SignalTooltip);
            ToolTip.SetPlacement(Signal, PlacementMode.Top);
        }

        if (ViewModel is { IsOnline: true, LastPingResult: { } result })
            MotdTextRenderer.Render(
                ServerMotD,
                result.Description,
                !FeedbackTheme.IsDarkTheme(this),
                2,
                12d,
                LaunchFonts.ResolveMotdFont());
        else
            ServerMotD.Text = ViewModel.MotdText;

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
            ServerIcon.Source = DefaultLogo;
            return;
        }

        try
        {
            var bitmap = await Task.Run(() => DecodeBase64Image(base64));
            Dispatcher.UIThread.Post(() =>
            {
                if (string.Equals(_lastLogoBase64, base64, StringComparison.Ordinal))
                    ServerIcon.Source = bitmap ?? DefaultLogo;
            });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "解析服务器图标失败");
            ServerIcon.Source = DefaultLogo;
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

    private void RefreshColor()
    {
        var stateNew = _isMouseDown
            ? "MouseDown"
            : IsPointerOver
                ? "MouseOver"
                : "Idle";
        if (_stateLast == stateNew)
            return;
        _stateLast = stateNew;

        var show = stateNew is "MouseOver" or "MouseDown";
        var time = show ? 120 : 160;
        if (!ContainerAnimation.CanAnimate(this))
        {
            ApplyColorStateInstant(show, stateNew);
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(
                    RectBack,
                    (show ? 1d : 0d) - RectBack.Opacity,
                    time,
                    ease: new FluentEaseOut()),
                new ColorAnimation(
                    RectBack,
                    Border.BackgroundProperty,
                    stateNew == "MouseDown"
                        ? "ColorBrush6"
                        : show
                            ? "ColorBrushBg1"
                            : "ColorBrush7",
                    time)
            },
            $"ServerCard Color {_uuid}");
    }

    private void ApplyColorStateInstant(bool show, string stateNew)
    {
        AnimationService.CancelAnimationByName($"ServerCard Color {_uuid}");
        RectBack.Opacity = show ? 1d : 0d;
        ContainerTheme.BindDynamic(
            RectBack,
            Border.BackgroundProperty,
            stateNew == "MouseDown"
                ? "ColorBrush6"
                : show
                    ? "ColorBrushBg1"
                    : "ColorBrush7");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AnimationService.CancelAnimationByName($"ServerCard Color {_uuid}");
        _stateLast = null;
        base.OnDetachedFromVisualTree(e);
    }

    private static bool IsPointerOnActionButton(Visual? source)
    {
        for (var current = source;
             current is not null;
             current = current.GetVisualParent())
            if (current is MyIconButton)
                return true;
        return false;
    }
}