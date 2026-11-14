using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using fNbt;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Pages.Instance;
using Path = System.IO.Path;

namespace PCL;

/// <summary>实例服务器列表页的视图模型：读写 servers.dat，维护卡片集合与并发 Ping。</summary>
public partial class PageInstanceServerViewModel : ViewModelBase
{
    private const int PingConcurrency = 5;

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private McInstance? _instance;
    private DateTime _lastRefresh = DateTime.MinValue;

    private CancellationTokenSource? _loadCts;
    private string _loadedServersPath = string.Empty;
    private CancellationTokenSource? _pingCts;

    [ObservableProperty] public partial bool ShowContent { get; set; }

    public ObservableCollection<ServerCardViewModel> Servers { get; } = [];

    public bool ShowEmpty => !ShowContent;

    public string? ServersPath =>
        _instance is null ? null : Path.Combine(_instance.PathIndie, "servers.dat");

    public void SetInstance(McInstance? instance)
    {
        _instance = instance;
    }

    /// <summary>取消当前加载与 Ping；页面离开时调用。</summary>
    public void Cancel()
    {
        CancelLoad();
        CancelPing();
    }

    /// <summary>重新读取 servers.dat；结果写入 <see cref="Servers" />，由页面重建卡片。</summary>
    public async Task<bool> LoadAsync()
    {
        CancelPing();
        CancelLoad();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        var path = ServersPath;

        Servers.Clear();
        ShowContent = false;
        _loadedServersPath = path ?? string.Empty;
        if (path is null)
            return false;

        State = AsyncState.Loading;
        Error = null;
        try
        {
            var servers = await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(
                path,
                "servers",
                token);
            if (token.IsCancellationRequested
                || !string.Equals(path, ServersPath, StringComparison.OrdinalIgnoreCase))
                return false;

            if (servers is not null)
                for (var i = 0; i < servers.Count; i++)
                {
                    if (servers[i] is not NbtCompound compound)
                        continue;
                    var name = compound.Get<NbtString>("name")?.Value ?? string.Empty;
                    var ip = compound.Get<NbtString>("ip")?.Value ?? string.Empty;
                    var icon = compound.Get<NbtString>("icon")?.Value ?? string.Empty;
                    var card = new ServerCardViewModel(name, ip, i);
                    if (!string.IsNullOrWhiteSpace(icon))
                        card.IconBase64 = icon;
                    Servers.Add(card);
                }

            ShowContent = Servers.Count > 0;
            State = AsyncState.Ready;
            return true;
        }
        catch (OperationCanceledException)
        {
            State = AsyncState.Idle;
            return false;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            State = AsyncState.Failed;
            ShowContent = false;
            InstancePageHelper.ReportError("Instance.Server.ReadFileFailed", ex);
            return false;
        }
    }

    /// <summary>并发 Ping 所有卡片（最多 5 个），完成后回写图标。</summary>
    public async Task PingAllAsync()
    {
        if (Servers.Count == 0)
            return;

        CancelPing();
        _pingCts = new CancellationTokenSource();
        var token = _pingCts.Token;
        var semaphore = new SemaphoreSlim(PingConcurrency);
        var cards = Servers.ToList();
        var tasks = cards
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
            await PersistServerIconsAsync(_loadedServersPath, cards);
    }

    public async Task RefreshServerAndPersistAsync(ServerCardViewModel card)
    {
        await card.RefreshAsync(true);
        await PersistServerIconsAsync(_loadedServersPath, [card]);
    }

    /// <summary>刷新全部的前置条件：2 秒内重复触发会被拒绝并提示。</summary>
    public bool TryBeginRefreshAll()
    {
        if ((DateTime.Now - _lastRefresh).TotalMilliseconds < 2000)
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Server.NoFrequentRefresh")));
            return false;
        }

        _lastRefresh = DateTime.Now;
        HintService.Show(Hints.Info(Lang.Text("Instance.Server.RefreshingList")));
        return true;
    }

    public async Task<bool> AddServerAsync()
    {
        try
        {
            var nameResult = await Dialog.ShowAsync(
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
                return false;
            var name = nameResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var addressResult = await Dialog.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Server.EditTitle"),
                    Content = Lang.Text("Instance.Server.AddressPrompt"),
                    ValidateRules = [new NullOrWhiteSpaceValidator()],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!addressResult.IsConfirmed)
                return false;
            var address = addressResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            var written = await UpdateServersAsync(
                servers =>
                {
                    if (servers.Count == 0)
                        servers.ListType = NbtTagType.Compound;
                    servers.Add(
                        new NbtCompound
                        {
                            new NbtString("name", name),
                            new NbtString("ip", address)
                        });
                    return true;
                },
                true);
            if (!written)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Server.WriteDataFailed")));
                return false;
            }

            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Updated")));
            return true;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.RefreshFailed", ex);
            return false;
        }
    }

    public async Task<bool> EditServerAsync(ServerCardViewModel card)
    {
        try
        {
            var nameResult = await Dialog.ShowAsync(
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
                return false;
            var name = nameResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var addressResult = await Dialog.ShowAsync(
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
                return false;
            var address = addressResult.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            var indexFound = true;
            var written = await UpdateServersAsync(servers =>
            {
                if (card.RawIndex < 0
                    || card.RawIndex >= servers.Count
                    || servers[card.RawIndex] is not NbtCompound compound)
                {
                    indexFound = false;
                    return false;
                }

                compound["name"] = new NbtString("name", name);
                compound["ip"] = new NbtString("ip", address);
                return true;
            });
            if (!written)
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            indexFound
                                ? "Instance.Server.WriteDataFailed"
                                : "Instance.Server.IndexNotFound")));
                return false;
            }

            card.UpdateInfo(name, address);
            await card.RefreshAsync();
            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Updated")));
            return true;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.EditFailed", ex);
            return false;
        }
    }

    public async Task<bool> RemoveServerAsync(ServerCardViewModel card)
    {
        try
        {
            var result = await Dialog.ShowAsync(
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
                return false;

            var indexFound = true;
            var written = await UpdateServersAsync(servers =>
            {
                if (card.RawIndex < 0 || card.RawIndex >= servers.Count)
                {
                    indexFound = false;
                    return false;
                }

                servers.RemoveAt(card.RawIndex);
                return true;
            });
            if (!written)
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            indexFound
                                ? "Instance.Server.WriteDataFailed"
                                : "Instance.Server.IndexNotFound")));
                return false;
            }

            Servers.Remove(card);
            ShowContent = Servers.Count > 0;
            HintService.Show(Hints.Success(Lang.Text("Instance.Server.Removed")));
            return true;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.Remove", ex);
            return false;
        }
    }

    public void ConnectServer(ServerCardViewModel card)
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

    public async Task CopyAddressAsync(ServerCardViewModel card, IClipboard? clipboard)
    {
        try
        {
            if (clipboard is null)
                return;
            await clipboard.SetTextAsync(card.Address);
            HintService.Show(
                Hints.Success(Lang.Text("Instance.Server.Card.AddressCopied", card.Address)));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Server.Card.CopyAddressFailed", ex);
        }
    }

    private async Task<NbtList?> ReadServersAsync()
    {
        var path = ServersPath;
        return path is null
            ? null
            : await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(path, "servers");
    }

    private async Task<bool> UpdateServersAsync(
        Func<NbtList, bool> update,
        bool createIfMissing = false)
    {
        var path = ServersPath;
        if (path is null)
            return false;

        await _saveLock.WaitAsync();
        try
        {
            var servers = await ReadServersAsync();
            if (servers is null)
            {
                if (!createIfMissing)
                    return false;
                servers = new NbtList("servers", NbtTagType.Compound);
            }

            return update(servers) && await WriteServersAsync(servers);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task<bool> WriteServersAsync(NbtList servers)
    {
        var path = ServersPath;
        return path is not null
            && await NbtFileHandler.WriteTagInNbtFileAsync((NbtList)servers.Clone(), path);
    }

    private void CancelLoad()
    {
        var cts = _loadCts;
        _loadCts = null;
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

    private void CancelPing()
    {
        var cts = _pingCts;
        _pingCts = null;
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

    private async Task PersistServerIconsAsync(
        string path,
        IReadOnlyList<ServerCardViewModel> cards)
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
}