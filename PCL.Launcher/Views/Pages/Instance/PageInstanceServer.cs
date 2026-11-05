using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using fNbt;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Navigation;
using PCL.UI.Controls;
using Path = System.IO.Path;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例服务器页：读写实例 <c>servers.dat</c>（NBT）中的服务器列表。
///     每张卡片按原始 NBT 下标绑定，空 IP 项不会被丢弃；刷新全部时会并发 Ping 服务器并回写图标。
/// </summary>
public class PageInstanceServer : MyPageRight, IRefreshable
{
    private const int DebounceInterval = 2000;
    private const int PingConcurrency = 5;
    private readonly List<ServerCard> _cards = [];
    private readonly MyScrollViewer _panBack = null!;
    private readonly MyCard _panEmpty;

    private readonly StackPanel _panMain;
    private readonly StackPanel _panServers;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private McInstance? _instance;
    private DateTime _lastRefresh = DateTime.MinValue;
    private string _loadedServersPath = string.Empty;

    public PageInstanceServer()
    {
        var root = new Grid();
        _panBack = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden
        };
        _panMain = new StackPanel
        {
            Margin = new Thickness(10d, 25d, 10d, 10d)
        };
        _panServers = new StackPanel
        {
            Margin = new Thickness(15d, 0d, 15d, 15d)
        };
        _panEmpty = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d),
            IsVisible = false
        };
        _panBack.Content = new Grid
        {
            Children =
            {
                _panMain,
                _panEmpty
            }
        };
        root.Children.Add(_panBack);
        PanScroll = _panBack;
        Child = root;
        BuildUi();
    }

    private string? ServersPath =>
        _instance is null ? null : Path.Combine(_instance.PathIndie, "servers.dat");

    public void Refresh()
    {
        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        Reload();
    }

    public override void OnNavigatedFrom()
    {
        CancelPing();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
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
        var divider = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Shape.FillProperty, "ColorBrush3");
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
        CancelPing();
        _panServers.Children.Clear();
        _cards.Clear();
        var path = ServersPath;
        if (path is null)
        {
            UpdateEmptyState();
            return;
        }

        _loadedServersPath = path;
        _cts = new CancellationTokenSource();
        _ = LoadAndRenderAsync(path, _cts.Token);
    }

    private async Task LoadAndRenderAsync(string path, CancellationToken token)
    {
        try
        {
            var servers =
                await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(path, "servers", token);
            if (token.IsCancellationRequested
                || !string.Equals(path, ServersPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (servers is not null)
                for (var i = 0; i < servers.Count; i++)
                {
                    if (servers[i] is not NbtCompound compound)
                        continue;
                    var name = compound.Get<NbtString>("name")?.Value ?? string.Empty;
                    var ip = compound.Get<NbtString>("ip")?.Value ?? string.Empty;
                    var card = new ServerCard(name, ip, i);
                    card.ConnectRequested += ConnectServer;
                    card.CopyRequested += CopyAddress;
                    card.RefreshRequested += item => _ = RefreshAndPersistAsync(item);
                    card.EditRequested += item => _ = EditServerAsync(item);
                    card.RemoveRequested += item => _ = RemoveServerAsync(item);
                    _cards.Add(card);
                    _panServers.Children.Add(card);
                }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.ReadFileFailed", ex);
        }

        UpdateEmptyState();
        if (_cards.Count > 0)
            _ = PingAllServersAsync(_cts?.Token ?? CancellationToken.None);
    }

    private void UpdateEmptyState()
    {
        var hasServers = _cards.Count > 0;
        _panMain.IsVisible = hasServers;
        _panEmpty.IsVisible = !hasServers;
    }

    private void RefreshAll()
    {
        if ((DateTime.Now - _lastRefresh).TotalMilliseconds < DebounceInterval)
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Server.NoFrequentRefresh")));
            return;
        }

        _lastRefresh = DateTime.Now;
        HintService.Show(Hints.Info(Lang.Text("Instance.Server.RefreshingList")));
        Reload();
    }

    private async Task<NbtList?> ReadServersAsync()
    {
        var path = ServersPath;
        return path is null
            ? null
            : await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(path, "servers");
    }

    private async Task<bool> WriteServersAsync(NbtList servers)
    {
        var path = ServersPath;
        return path is not null
            && await NbtFileHandler.WriteTagInNbtFileAsync((NbtList)servers.Clone(), path);
    }

    private void CancelPing()
    {
        var cts = _cts;
        _cts = null;
        if (cts is null)
            return;
        try
        {
            cts.Cancel();
        }
        catch
        {
            // ignored
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task PingAllServersAsync(CancellationToken token)
    {
        var semaphore = new SemaphoreSlim(PingConcurrency);
        var tasks = _cards
            .ToList()
            .Select(async card =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    await card.RefreshAsync(false, token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, $"Ping 服务器失败：{card.Address}");
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            semaphore.Dispose();
        }

        if (!token.IsCancellationRequested)
            await PersistServerIconsAsync(_loadedServersPath, [.. _cards]);
    }

    private async Task RefreshAndPersistAsync(ServerCard card)
    {
        await card.RefreshAsync(true);
        await PersistServerIconsAsync(_loadedServersPath, [card]);
    }

    private async Task PersistServerIconsAsync(string path, List<ServerCard> cards)
    {
        var withIcon = cards
            .Where(card => card.HasPinged && !string.IsNullOrWhiteSpace(card.IconBase64))
            .ToList();
        if (withIcon.Count == 0 || string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        await _saveLock.WaitAsync();
        try
        {
            var servers = await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(path, "servers");
            if (servers is null)
                return;

            var changed = false;
            foreach (var card in withIcon.Where(card =>
                         card.RawIndex >= 0 && card.RawIndex < servers.Count))
            {
                if (servers[card.RawIndex] is not NbtCompound compound)
                    continue;
                if (string.Equals(
                        compound.Get<NbtString>("icon")?.Value,
                        card.IconBase64,
                        StringComparison.Ordinal))
                    continue;
                compound["icon"] = new NbtString("icon", card.IconBase64);
                changed = true;
            }

            if (changed)
                await NbtFileHandler.WriteTagInNbtFileAsync((NbtList)servers.Clone(), path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "回写服务器图标失败", LauncherLogLevel.Developer);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private void ConnectServer(ServerCard card)
    {
        if (_instance is null)
            return;
        try
        {
            if (GameLaunch.McLaunchStart(
                    new GameLaunch.McLaunchOptions
                    {
                        ServerIp = card.Address,
                        instance = _instance
                    }))
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Instance.Server.Card.ConnectingTo", card.Name)));
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
            HintService.Show(
                Hints.Success(Lang.Text("Instance.Server.Card.AddressCopied", address)));
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
                    Title = Lang.Text("Instance.Server.EditTitle"),
                    Content = Lang.Text("Instance.Server.NamePrompt"),
                    DefaultValue = Lang.Text("Instance.Server.DefaultName"),
                    ValidateRules = [new NullOrWhiteSpaceValidator()],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!nameResult.IsConfirmed)
                return;
            var name = nameResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return;

            var addressResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.EditTitle"),
                    Content = Lang.Text("Instance.Server.AddressPrompt"),
                    ValidateRules = [new NullOrWhiteSpaceValidator()],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!addressResult.IsConfirmed)
                return;
            var address = addressResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                return;

            var servers = await ReadServersAsync() ?? new NbtList("servers", NbtTagType.Compound);
            if (servers.Count == 0)
                servers.ListType = NbtTagType.Compound;
            servers.Add(
                new NbtCompound
                {
                    new NbtString("name", name),
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
            var nameResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.EditTitle"),
                    Content = Lang.Text("Instance.Server.NamePrompt"),
                    DefaultValue = string.IsNullOrEmpty(card.Name) ? card.Address : card.Name,
                    ValidateRules = [new NullOrWhiteSpaceValidator()],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!nameResult.IsConfirmed)
                return;
            var name = nameResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return;

            var addressResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.EditTitle"),
                    Content = Lang.Text("Instance.Server.AddressPrompt"),
                    DefaultValue = card.Address,
                    ValidateRules = [new NullOrWhiteSpaceValidator()],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!addressResult.IsConfirmed)
                return;
            var address = addressResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                return;

            var servers = await ReadServersAsync();
            if (servers is null
                || card.RawIndex < 0
                || card.RawIndex >= servers.Count
                || servers[card.RawIndex] is not NbtCompound compound)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.IndexNotFound")));
                return;
            }

            compound["name"] = new NbtString("name", name);
            compound["ip"] = new NbtString("ip", address);
            if (!await WriteServersAsync(servers))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.WriteDataFailed")));
                return;
            }

            card.UpdateInfo(name, address);
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
                    Content = Lang.Text(
                        "Instance.Server.Card.RemoveConfirmMessage",
                        card.Name,
                        card.Address),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return;

            var servers = await ReadServersAsync();
            if (servers is null || card.RawIndex < 0 || card.RawIndex >= servers.Count)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.IndexNotFound")));
                return;
            }

            servers.RemoveAt(card.RawIndex);
            if (!await WriteServersAsync(servers))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.WriteDataFailed")));
                return;
            }

            _panServers.Children.Remove(card);
            _cards.Remove(card);
            UpdateEmptyState();
            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Removed")));

            // 删除后下标整体前移，重新读取以保持卡片与 NBT 下标一致。
            if (_cards.Count > 0)
                Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.Remove", ex);
        }
    }
}