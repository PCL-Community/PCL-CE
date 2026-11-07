using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     服务器卡片：与 WPF 版一致的单行 44px 布局，左侧图标、名称、延迟/人数、MotD，
///     右侧提供进入服务器与设置菜单（刷新、复制地址、编辑、移除）。
/// </summary>
public partial class ServerCard : MyCard
{
    private const string DefaultLogoUri =
        "avares://PCL.Launcher/Assets/Images/Icons/DefaultServer.png";

    private static readonly Bitmap? DefaultLogo = LoadDefaultLogo();
    private string? _lastLogoBase64;

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
        UseAnimation = false;

        BtnConnect.Click += (_, _) => ConnectRequested?.Invoke(this);
        BtnSetting.Click += (_, _) => BtnSetting.ContextMenu?.Open(BtnSetting);
        ContextMenuHelper.Attach(BtnSetting, () => BtnSetting.ContextMenu);
        ToolTip.SetPlacement(ServerName, PlacementMode.Top);

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

        ServerPlayer.Text = ViewModel.PlayerText;
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
}