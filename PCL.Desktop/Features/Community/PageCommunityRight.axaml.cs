// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Community;

public partial class PageCommunityRight : MyPageRight, IDisposable
{
    private readonly ICommunityResourceCatalog _catalog;
    private readonly bool _ownsCatalog;
    private readonly DispatcherTimer _searchTimer;
    private CancellationTokenSource? _loadCancellation;
    private CommunityResourceCategory _category = CommunityResourceCategory.Mod;
    private bool _hasLoaded;
    private bool _disposed;

    public PageCommunityRight()
        : this(new ModrinthCommunityResourceCatalog(), ownsCatalog: true)
    {
    }

    public PageCommunityRight(ICommunityResourceCatalog catalog, bool ownsCatalog = false)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ownsCatalog = ownsCatalog;
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchTimer.Tick += SearchTimer_Tick;
        if (this.FindControl<MySearchBar>("TextSearch") is { } search)
            search.TextChanged += (_, _) => RestartSearchTimer();
        AttachedToVisualTree += (_, _) =>
        {
            if (!_hasLoaded)
                _ = RefreshAsync();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _loadCancellation?.Cancel();
            _searchTimer.Stop();
        };
        SetLoadingState(false);
    }

    public event EventHandler<CommunityResourceEntry>? OpenProjectRequested;

    public CommunityResourceCategory Category => _category;

    public async Task SetCategoryAsync(CommunityResourceCategory category)
    {
        if (_category == category && _hasLoaded)
            return;

        _category = category;
        await RefreshAsync().ConfigureAwait(true);
    }

    public async Task RefreshAsync()
    {
        if (_disposed)
            return;

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _loadCancellation.Token;
        SetLoadingState(true);
        try
        {
            string query = this.FindControl<MySearchBar>("TextSearch")?.Text?.Trim() ?? string.Empty;
            IReadOnlyList<CommunityResourceEntry> entries =
                await _catalog.SearchAsync(_category, query, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _hasLoaded = true;
                RenderEntries(entries, query);
                SetLoadingState(false);
            }, DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RenderMessage("社区资源加载失败", ex.Message);
                SetLoadingState(false);
            });
        }
    }

    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _searchTimer.Stop();
        _searchTimer.Tick -= SearchTimer_Tick;
        if (_ownsCatalog && _catalog is IDisposable disposable)
            disposable.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void RestartSearchTimer()
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        _ = RefreshAsync();
    }

    private void RenderEntries(IReadOnlyList<CommunityResourceEntry> entries, string query)
    {
        if (this.FindControl<StackPanel>("PanMain") is not { } panel)
            return;

        panel.Children.Clear();
        if (entries.Count == 0)
        {
            RenderMessage(
                "没有找到资源",
                string.IsNullOrWhiteSpace(query) ? "当前分类暂时没有可显示的资源。" : $"没有找到包含“{query}”的资源。");
            return;
        }

        panel.Children.Add(new TextBlock
        {
            Text = CategoryTitle(_category) + $" ({entries.Count.ToString(CultureInfo.CurrentCulture)})",
            Margin = new Thickness(13d, 12d, 5d, 8d),
            Opacity = 0.6d,
            FontSize = 12d
        });
        foreach (CommunityResourceEntry entry in entries)
            panel.Children.Add(CreateResourceItem(entry));
        panel.InvalidateMeasure();
        panel.InvalidateArrange();
        PanScroll?.InvalidateMeasure();
        PanScroll?.InvalidateArrange();
    }

    private MyListItem CreateResourceItem(CommunityResourceEntry entry)
    {
        MyIconButton website = new()
        {
            SvgIcon = "lucide/external-link",
            LogoScale = 0.9d,
            ToolTip = "打开项目页面"
        };
        website.Click += (_, _) => OpenProjectRequested?.Invoke(this, entry);

        string info = entry.Description;
        if (entry.Downloads > 0)
        {
            string downloads = entry.Downloads.ToString("N0", CultureInfo.CurrentCulture) + " 次下载";
            info = string.IsNullOrWhiteSpace(info) ? downloads : info + " · " + downloads;
        }

        MyListItem item = new()
        {
            Title = entry.Title,
            Info = info,
            Height = 52d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            SvgIcon = CategoryIcon(_category),
            LogoScale = 0.9d,
            Buttons = [website]
        };
        item.Click += (_, _) => OpenProjectRequested?.Invoke(this, entry);
        return item;
    }

    private void RenderMessage(string title, string message)
    {
        if (this.FindControl<StackPanel>("PanMain") is not { } panel)
            return;

        panel.Children.Clear();
        MyCard card = new()
        {
            Title = title,
            Margin = new Thickness(0d, 0d, 0d, 15d),
            UseAnimation = false
        };
        card.Children.Add(new TextBlock
        {
            Text = message,
            Margin = new Thickness(25d, 38d, 23d, 16d),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        panel.Children.Add(card);
    }

    private void SetLoadingState(bool loading)
    {
        if (this.FindControl<Control>("PanLoad") is { } loadPanel)
            loadPanel.IsVisible = loading;
        if (this.FindControl<Control>("PanAllBack") is { } content)
        {
            // Keep the results tree mounted while a request is running. Toggling
            // IsVisible here can leave a freshly populated ScrollViewer content
            // detached until a later layout pass in the headless renderer.
            content.IsVisible = true;
            content.IsHitTestVisible = !loading;
            content.Opacity = loading ? 0d : 1d;
        }
        if (this.FindControl<MyLoading>("Load") is { } load)
            load.State.LoadingState = loading ? MyLoading.MyLoadingState.Run : MyLoading.MyLoadingState.Stop;
    }

    private static string CategoryTitle(CommunityResourceCategory category) =>
        category switch
        {
            CommunityResourceCategory.Mod => "Mod",
            CommunityResourceCategory.Modpack => "整合包",
            CommunityResourceCategory.DataPack => "数据包",
            CommunityResourceCategory.ResourcePack => "资源包",
            CommunityResourceCategory.Shader => "光影包",
            CommunityResourceCategory.World => "世界",
            _ => "社区资源"
        };

    private static string CategoryIcon(CommunityResourceCategory category) =>
        category switch
        {
            CommunityResourceCategory.Mod => "lucide/puzzle",
            CommunityResourceCategory.Modpack => "lucide/package",
            CommunityResourceCategory.DataPack => "lucide/boxes",
            CommunityResourceCategory.ResourcePack => "lucide/layers",
            CommunityResourceCategory.Shader => "lucide/sparkles",
            CommunityResourceCategory.World => "lucide/globe",
            _ => "lucide/download"
        };
}
