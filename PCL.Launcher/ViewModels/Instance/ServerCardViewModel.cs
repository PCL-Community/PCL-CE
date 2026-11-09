using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.Link.McPing;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Minecraft;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>服务器卡片的视图模型：维护名称、地址、状态文本与最近一次 Ping 结果。</summary>
public partial class ServerCardViewModel : ViewModelBase
{
    public ServerCardViewModel(string name, string address, int rawIndex)
    {
        RawIndex = rawIndex;
        UpdateInfo(name, address);
    }

    [ObservableProperty] public partial bool IsOnline { get; set; }

    [ObservableProperty] public partial bool IsPinging { get; set; }

    [ObservableProperty] public partial McPingResult? LastPingResult { get; set; }

    [ObservableProperty] public partial long Latency { get; set; }

    [ObservableProperty] public partial string Name { get; set; } = string.Empty;

    [ObservableProperty] public partial string Address { get; set; } = string.Empty;

    [ObservableProperty] public partial int RawIndex { get; set; }

    [ObservableProperty] public partial string NameText { get; set; } = "Unknown";

    [ObservableProperty] public partial string PlayerText { get; set; } = string.Empty;

    [ObservableProperty] public partial string MotdText { get; set; } = string.Empty;

    [ObservableProperty] public partial string SignalIcon { get; set; } = "lucide/signal";

    [ObservableProperty] public partial string SignalTooltip { get; set; } = string.Empty;

    [ObservableProperty] public partial bool HasPinged { get; set; }

    [ObservableProperty] public partial string IconBase64 { get; set; } = string.Empty;

    public void UpdateInfo(string name, string address)
    {
        Name = name;
        Address = address;
        NameText = !string.IsNullOrEmpty(name)
            ? name
            : !string.IsNullOrEmpty(address)
                ? address
                : "Unknown";
        IconBase64 = string.Empty;
        HasPinged = false;
        IsOnline = false;
        IsPinging = false;
        LastPingResult = null;
        Latency = 0;
        PlayerText = string.Empty;
        MotdText = string.Empty;
        SignalIcon = "lucide/signal";
        SignalTooltip = string.Empty;
    }

    public async Task RefreshAsync(bool withHint = false, CancellationToken token = default)
    {
        if (withHint)
            HintService.Show(
                Hints.Info(Lang.Text("Instance.Server.Card.RefreshingStatus", Name)));
        if (string.IsNullOrWhiteSpace(Address))
        {
            ShowOffline();
            return;
        }

        ShowPinging();
        try
        {
            var addr = await ServerAddressResolver.GetResolvedServerAddressAsync(Address, token);
            using var query = McPingServiceFactory.CreateService(addr.Host, addr.Ip, addr.Port);
            var result = await query.PingAsync(token);
            token.ThrowIfCancellationRequested();
            if (result is null)
            {
                ShowOffline();
                return;
            }

            HasPinged = true;
            IsOnline = true;
            IsPinging = false;
            IconBase64 = result.Favicon ?? string.Empty;
            LastPingResult = result;
            Latency = result.Latency;
            SignalIcon = GetSignalIcon((int)Math.Min(result.Latency, int.MaxValue));
            SignalTooltip = $"{result.Latency}ms";
            PlayerText = result.Players.Online != 0 || result.Players.Max != 0
                ? $"{result.Players.Online} / {result.Players.Max}"
                : "???";
            MotdText = result.Description;
        }
        catch (OperationCanceledException)
        {
            ShowOffline();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"Ping 服务器失败：{Address}");
            ShowOffline();
        }
    }

    private void ShowPinging()
    {
        IsPinging = true;
        IsOnline = false;
        LastPingResult = null;
        SignalIcon = "lucide/loader";
        SignalTooltip = string.Empty;
        PlayerText = string.Empty;
        MotdText = Lang.Text("Instance.Server.Card.ConnectingDots");
    }

    private void ShowOffline()
    {
        IsPinging = false;
        IsOnline = false;
        HasPinged = false;
        LastPingResult = null;
        Latency = 0;
        SignalIcon = "lucide/signal-zero";
        SignalTooltip = string.Empty;
        PlayerText = string.Empty;
        MotdText = Lang.Text("Instance.Server.Card.ServerOffline");
    }

    private static string GetSignalIcon(int ping)
    {
        return ping switch
        {
            <= 99 => "lucide/signal",
            <= 299 => "lucide/signal-high",
            <= 599 => "lucide/signal-medium",
            <= 999 => "lucide/signal-low",
            _ => "lucide/signal-zero"
        };
    }
}