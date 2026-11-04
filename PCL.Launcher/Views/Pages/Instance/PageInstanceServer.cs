using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using fNbt;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例服务器页：读写实例 <c>servers.dat</c>（NBT）中的服务器列表。
///     每张卡片内嵌服务器查询控件展示图标/MotD/人数/延迟；刷新全部会重新读取列表并重新查询每个服务器。
/// </summary>
public class PageInstanceServer : MyPageRight, IRefreshable
{
    private const int DebounceInterval = 2000;

    private readonly StackPanel _panMain;
    private readonly StackPanel _panServers;
    private readonly MyCard _panEmpty;
    private readonly List<ServerCard> _cards = [];
    private MyScrollViewer _panBack = null!;
    private McInstance? _instance;
    private DateTime _lastRefresh = DateTime.MinValue;

    public PageInstanceServer()
    {
        var root = new Grid();
        _panBack = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden
        };
        _panMain = new StackPanel { Margin = new Thickness(10d, 25d, 10d, 10d) };
        _panServers = new StackPanel { Margin = new Thickness(15d, 0d, 15d, 15d) };
        _panEmpty = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d),
            IsVisible = false
        };
        _panBack.Content = new Grid { Children = { _panMain, _panEmpty } };
        root.Children.Add(_panBack);
        PanScroll = _panBack;
        Child = root;
        BuildUi();
    }

    private string? ServersPath =>
        _instance is null ? null : Path.Combine(_instance.PathIndie, "servers.dat");

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        Reload();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        Reload();
    }

    public void Refresh()
    {
        Reload();
    }

    private void BuildUi()
    {
        _panMain.Children.Clear();

        var quickCard = new MyCard
        {
            Title = Lang.Text("Instance.Server.QuickActions"),
            Margin = new Thickness(15d, 0d, 15d, 15d)
        };
        var quickBody = new Grid
        {
            Height = 35d,
            Margin = new Thickness(25d, 40d, 23d, 15d),
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };
        var refreshAll = new MyButton
        {
            Text = Lang.Text("Instance.Server.RefreshAll"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            ColorType = MyButton.ColorState.Highlight
        };
        refreshAll.Click += (_, _) => RefreshAll();
        quickBody.Children.Add(refreshAll);
        var add = new MyButton
        {
            Text = Lang.Text("Instance.Server.AddServer"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumn(add, 1);
        add.Click += (_, _) => _ = AddServerAsync();
        quickBody.Children.Add(add);
        quickCard.BorderChild = quickBody;
        _panMain.Children.Add(quickCard);
        _panMain.Children.Add(_panServers);

        _panEmpty.BorderChild = CreateEmptyBody();
    }

    private Grid CreateEmptyBody()
    {
        var grid = new Grid
        {
            Margin = new Thickness(20d, 17d),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,*")
        };
        var title = new TextBlock
        {
            Text = Lang.Text("Instance.Server.Empty.Title"),
            FontSize = 19d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 0d, 0d, 9d)
        };
        ContainerTheme.BindDynamic(title, TextBlock.ForegroundProperty, "ColorBrush3");
        Grid.SetColumnSpan(title, 4);
        grid.Children.Add(title);
        var divider = new Avalonia.Controls.Shapes.Rectangle { Height = 2d, HorizontalAlignment = HorizontalAlignment.Stretch };
        ContainerTheme.BindDynamic(divider, Avalonia.Controls.Shapes.Shape.FillProperty, "ColorBrush3");
        Grid.SetRow(divider, 1);
        Grid.SetColumnSpan(divider, 4);
        grid.Children.Add(divider);
        var message = new TextBlock
        {
            Text = Lang.Text("Instance.Server.Empty.Message"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10d, 15d, 10d, 5d)
        };
        Grid.SetRow(message, 2);
        Grid.SetColumnSpan(message, 4);
        grid.Children.Add(message);
        var refresh = new MyButton
        {
            Text = Lang.Text("Instance.Server.RefreshInfo"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorType = MyButton.ColorState.Highlight
        };
        refresh.Click += (_, _) => RefreshAll();
        Grid.SetRow(refresh, 3);
        Grid.SetColumn(refresh, 1);
        grid.Children.Add(refresh);
        var add = new MyButton
        {
            Text = Lang.Text("Instance.Server.AddServer"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        add.Click += (_, _) => _ = AddServerAsync();
        Grid.SetRow(add, 3);
        Grid.SetColumn(add, 2);
        grid.Children.Add(add);
        return grid;
    }

    public void Reload()
    {
        _panServers.Children.Clear();
        _cards.Clear();
        var path = ServersPath;
        if (path is null)
        {
            UpdateEmptyState();
            return;
        }

        _ = LoadAndRenderAsync(path);
    }

    private async Task LoadAndRenderAsync(string path)
    {
        try
        {
            var servers = await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(path, "servers");
            foreach (var tag in servers ?? [])
            {
                if (tag is not NbtCompound compound)
                    continue;
                var name = compound.Get<NbtString>("name")?.Value ?? string.Empty;
                var ip = compound.Get<NbtString>("ip")?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(ip))
                    continue;

                var card = new ServerCard(name, ip);
                card.ConnectRequested += ConnectServer;
                card.CopyRequested += CopyAddress;
                card.EditRequested += item => _ = EditServerAsync(item);
                card.RemoveRequested += item => _ = RemoveServerAsync(item);
                _cards.Add(card);
                _panServers.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.ReadFileFailed", ex);
        }

        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var hasServers = _cards.Count > 0;
        _panMain.IsVisible = true;
        _panEmpty.IsVisible = !hasServers;
        _panServers.IsVisible = hasServers;
    }

    private void RefreshAll()
    {
        if ((DateTime.Now - _lastRefresh).TotalMilliseconds < DebounceInterval)
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Server.NoFrequentRefresh")));
            return;
        }

        _lastRefresh = DateTime.Now;
        var path = ServersPath;
        if (path is null)
            return;
        _panServers.Children.Clear();
        _cards.Clear();
        _ = LoadAndRenderAsync(path);
    }

    private async Task<NbtList?> ReadServersAsync()
    {
        var path = ServersPath;
        return path is null ? null : await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(path, "servers");
    }

    private async Task<bool> WriteServersAsync(NbtList servers)
    {
        var path = ServersPath;
        return path is not null && await NbtFileHandler.WriteTagInNbtFileAsync((NbtList)servers.Clone(), path);
    }

    private void ConnectServer(ServerCard card)
    {
        if (_instance is null)
            return;
        try
        {
            if (GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
                {
                    ServerIp = card.Address,
                    instance = _instance
                }))
            {
                HintService.Show(Hints.Info(Lang.Text("Instance.Server.Card.ConnectingTo", card.Name)));
                PageRouter.Navigate(PageType.Launch);
            }
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.LaunchFailed", ex);
        }
    }

    private void CopyAddress(ServerCard card)
    {
        _ = CopyAddressAsync(card.Address);
    }

    private async Task CopyAddressAsync(string address)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            await clipboard.SetTextAsync(address);
            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Card.AddressCopied")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.CopyAddressFailed", ex);
        }
    }

    private async Task AddServerAsync()
    {
        try
        {
            var nameResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.AddServer"),
                    Content = Lang.Text("Instance.Server.NamePrompt"),
                    DefaultValue = Lang.Text("Instance.Server.DefaultName"),
                    Buttons = DialogButtons.OkCancel
                });
            if (!nameResult.IsConfirmed)
                return;
            var name = nameResult.InputText ?? string.Empty;

            var addressResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.AddServer"),
                    Content = Lang.Text("Instance.Server.AddressPrompt"),
                    Buttons = DialogButtons.OkCancel
                });
            if (!addressResult.IsConfirmed)
                return;
            var address = addressResult.InputText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
            {
                HintService.Show(Hints.Warning(Lang.Text("Instance.Server.AddressPrompt")));
                return;
            }

            var servers = await ReadServersAsync() ?? new NbtList("servers", NbtTagType.Compound);
            servers.Add(
                new NbtCompound
                {
                    new NbtString("name", string.IsNullOrWhiteSpace(name) ? address : name),
                    new NbtString("ip", address)
                });
            if (!await WriteServersAsync(servers))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.WriteDataFailed")));
                return;
            }

            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Updated")));
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.RefreshFailed", ex);
        }
    }

    private async Task EditServerAsync(ServerCard card)
    {
        try
        {
            var servers = await ReadServersAsync();
            var index = _cards.IndexOf(card);
            if (servers is null || index < 0 || index >= servers.Count)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.IndexNotFound")));
                return;
            }

            var nameResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.EditTitle"),
                    Content = Lang.Text("Instance.Server.NamePrompt"),
                    DefaultValue = card.Name,
                    Buttons = DialogButtons.OkCancel
                });
            if (!nameResult.IsConfirmed)
                return;
            var name = nameResult.InputText ?? string.Empty;

            var addressResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.EditTitle"),
                    Content = Lang.Text("Instance.Server.AddressPrompt"),
                    DefaultValue = card.Address,
                    Buttons = DialogButtons.OkCancel
                });
            if (!addressResult.IsConfirmed)
                return;
            var address = addressResult.InputText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                return;

            if (servers[index] is not NbtCompound compound)
                return;
            compound["name"] = new NbtString("name", string.IsNullOrWhiteSpace(name) ? address : name);
            compound["ip"] = new NbtString("ip", address);
            if (!await WriteServersAsync(servers))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.WriteDataFailed")));
                return;
            }

            card.UpdateInfo(string.IsNullOrWhiteSpace(name) ? address : name, address);
            await card.RefreshAsync();
            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Updated")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.EditFailed", ex);
        }
    }

    private async Task RemoveServerAsync(ServerCard card)
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Server.Card.RemoveConfirmTitle"),
                    Content = Lang.Text("Instance.Server.Card.RemoveConfirmMessage"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return;

            var servers = await ReadServersAsync();
            var index = _cards.IndexOf(card);
            if (servers is null || index < 0 || index >= servers.Count)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.IndexNotFound")));
                return;
            }

            servers.RemoveAt(index);
            if (!await WriteServersAsync(servers))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.WriteDataFailed")));
                return;
            }

            _panServers.Children.Remove(card);
            _cards.Remove(card);
            UpdateEmptyState();
            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Removed")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.Remove", ex);
        }
    }
}
