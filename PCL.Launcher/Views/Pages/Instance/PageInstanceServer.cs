using Avalonia.Controls;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例服务器页：读写实例 <c>servers.dat</c>（NBT）中的服务器列表。
///     每张卡片按原始 NBT 下标绑定，空 IP 项不会被丢弃；刷新全部时会并发 Ping 服务器并回写图标。
/// </summary>
public partial class PageInstanceServer : MyPageRight, IRefreshable
{
    public PageInstanceServer()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;

        BtnRefreshAll.Click += (_, _) => RefreshAll();
        BtnRefreshEmpty.Click += (_, _) => RefreshAll();
        BtnAddServer.Click += (_, _) => _ = AddServerAsync();
        BtnAddServerEmpty.Click += (_, _) => _ = AddServerAsync();
    }

    public PageInstanceServerViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        ViewModel.SetInstance(InstancePageHelper.ResolveInstance(parameter));
        Reload();
    }

    public override void OnNavigatedFrom()
    {
        ViewModel.Cancel();
    }

    protected override void RefreshLocalization()
    {
        Reload();
    }

    public void Reload()
    {
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await ViewModel.LoadAsync();
        BuildCards();
        if (ViewModel.ShowContent)
            _ = ViewModel.PingAllAsync();
    }

    private void BuildCards()
    {
        PanServers.Children.Clear();
        foreach (var item in ViewModel.Servers)
        {
            var placeholder = new MyVirtualizingElement<ServerCard>(() => CreateCard(item))
            {
                Height = 44d,
                RecycleOffscreen = true
            };
            PanServers.Children.Add(placeholder);
        }
    }

    private ServerCard CreateCard(ServerCardViewModel item)
    {
        var card = new ServerCard(item);
        card.ConnectRequested += server => ViewModel.ConnectServer(server.ViewModel);
        card.CopyRequested += server =>
            _ = ViewModel.CopyAddressAsync(
                server.ViewModel,
                TopLevel.GetTopLevel(this)?.Clipboard);
        card.RefreshRequested += server =>
            _ = ViewModel.RefreshServerAndPersistAsync(server.ViewModel);
        card.EditRequested += server => _ = EditServerAsync(server.ViewModel);
        card.RemoveRequested += server => _ = RemoveServerAsync(server.ViewModel);
        return card;
    }

    private void RefreshAll()
    {
        if (!ViewModel.TryBeginRefreshAll())
            return;
        Reload();
    }

    private async Task AddServerAsync()
    {
        if (await ViewModel.AddServerAsync())
            await ReloadAsync();
    }

    private async Task EditServerAsync(ServerCardViewModel card)
    {
        if (await ViewModel.EditServerAsync(card))
            await ReloadAsync();
    }

    private async Task RemoveServerAsync(ServerCardViewModel card)
    {
        if (await ViewModel.RemoveServerAsync(card))
            await ReloadAsync();
    }
}