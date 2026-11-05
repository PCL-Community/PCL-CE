using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例服务器页：读写实例 <c>servers.dat</c>（NBT）中的服务器列表。
///     每张卡片按原始 NBT 下标绑定，空 IP 项不会被丢弃；刷新全部时会并发 Ping 服务器并回写图标。
/// </summary>
public class PageInstanceServer : MyPageRight, IRefreshable
{
    private readonly MyScrollViewer _panBack;
    private readonly MyCard _panEmpty;
    private readonly StackPanel _panMain;
    private readonly StackPanel _panServers;

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
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await ViewModel.LoadAsync();
        BuildCards();
        UpdateEmptyState();
        if (ViewModel.ShowContent)
            _ = ViewModel.PingAllAsync();
    }

    private void BuildCards()
    {
        _panServers.Children.Clear();
        foreach (var item in ViewModel.Servers)
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
            _panServers.Children.Add(card);
        }
    }

    private void UpdateEmptyState()
    {
        var hasServers = ViewModel.ShowContent;
        _panMain.IsVisible = hasServers;
        _panEmpty.IsVisible = !hasServers;
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