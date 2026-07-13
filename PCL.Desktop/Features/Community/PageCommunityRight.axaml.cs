// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Community;

/// <summary>
/// Community resource browser — control IDs and filter layout mirror WPF <c>PageComp</c>.
/// Searches Modrinth and CurseForge through a shared catalog router.
/// </summary>
public partial class PageCommunityRight : MyPageRight, IDisposable
{
    private const int PageSize = 20;

    private readonly ICommunityResourceCatalog _catalog;
    private readonly bool _ownsCatalog;
    private readonly CommunityFavoritesStore? _favorites;
    private readonly DispatcherTimer _searchTimer;
    private CancellationTokenSource? _loadCancellation;
    private CommunityResourceCategory _category = CommunityResourceCategory.Mod;
    private IReadOnlyList<CommunityResourceEntry> _entries = [];
    private int _page;
    private int _totalPages = 1;
    private bool _hasLoaded;
    private bool _disposed;
    private bool _filtersReady;

    public PageCommunityRight()
        : this(new CompositeCommunityResourceCatalog(), ownsCatalog: true)
    {
    }

    public PageCommunityRight(
        ICommunityResourceCatalog catalog,
        bool ownsCatalog = false,
        CommunityFavoritesStore? favorites = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ownsCatalog = ownsCatalog;
        _favorites = favorites;
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            _page = 0;
            _ = RefreshAsync();
        };

        if (this.FindControl<MySearchBar>("PanSearchBox") is { } search)
            search.TextChanged += (_, _) => RestartSearchTimer();
        if (this.FindControl<MyIconButton>("BtnSearchReset") is { } reset)
            reset.Click += (_, _) => ResetFilter();
        if (this.FindControl<MyIconButton>("BtnPageFirst") is { } first)
            first.Click += (_, _) => GoPage(0);
        if (this.FindControl<MyIconButton>("BtnPageLeft") is { } left)
            left.Click += (_, _) => GoPage(_page - 1);
        if (this.FindControl<MyIconButton>("BtnPageRight") is { } right)
            right.Click += (_, _) => GoPage(_page + 1);
        if (this.FindControl<MyIconButton>("BtnSearchInstallModPack") is { } installPack)
            installPack.Click += (_, _) => InstallModPackRequested?.Invoke(this, EventArgs.Empty);

        InitializeFilters();
        ApplyCategoryFilterVisibility();
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

    public event EventHandler<CommunityResourceDownloadRequest>? DownloadRequested;

    public event EventHandler? InstallModPackRequested;

    public CommunityResourceCategory Category => _category;

    public CommunitySearchOptions CurrentSearchOptions => BuildSearchOptions();

    public async Task SetCategoryAsync(CommunityResourceCategory category)
    {
        if (_category == category && _hasLoaded)
            return;

        _category = category;
        _page = 0;
        ApplyCategoryFilterVisibility();
        PopulateTagItems();
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
        SetError(null);

        try
        {
            string query = this.FindControl<MySearchBar>("PanSearchBox")?.Text?.Trim() ?? string.Empty;
            CommunitySearchOptions options = BuildSearchOptions();
            IReadOnlyList<CommunityResourceEntry> entries =
                await _catalog.SearchAsync(_category, query, options, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _hasLoaded = true;
                _entries = entries;
                _totalPages = Math.Max(1, (int)Math.Ceiling(entries.Count / (double)PageSize));
                if (_page >= _totalPages)
                    _page = _totalPages - 1;
                RenderPage();
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
                SetError(ex.Message);
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
        _searchTimer.Stop();
        if (_ownsCatalog && _catalog is IDisposable disposable)
            disposable.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void GoPage(int page)
    {
        page = Math.Clamp(page, 0, Math.Max(0, _totalPages - 1));
        if (page == _page && _hasLoaded)
            return;
        _page = page;
        RenderPage();
        // WPF PageComp: jump back to top after flipping pages.
        PanScroll?.ScrollToHome();
    }

    private void ResetFilter()
    {
        if (this.FindControl<MySearchBar>("PanSearchBox") is { } search)
            search.Text = string.Empty;
        if (this.FindControl<MyComboBox>("ComboSearchSource") is { } source)
            source.SelectedIndex = 0;
        if (this.FindControl<MyComboBox>("ComboSearchTag") is { } tag)
            tag.SelectedIndex = 0;
        if (this.FindControl<MyComboBox>("ComboSearchSort") is { } sort)
            sort.SelectedIndex = 0;
        if (this.FindControl<MyComboBox>("TextSearchVersion") is { } version)
        {
            version.SelectedIndex = 0;
            version.Text = string.Empty;
        }
        if (this.FindControl<MyComboBox>("ComboSearchLoader") is { } loader)
            loader.SelectedIndex = 0;
        if (this.FindControl<MyComboBox>("ComboSearchShaderLoader") is { } shader)
            shader.SelectedIndex = 0;
        _page = 0;
        _ = RefreshAsync();
    }

    private void InitializeFilters()
    {
        if (_filtersReady)
            return;
        _filtersReady = true;

        // WPF PageComp: All / CurseForge / Modrinth.
        BindCombo("ComboSearchSource",
        [
            new FilterOption("all", "全部"),
            new FilterOption("curseforge", "CurseForge"),
            new FilterOption("modrinth", "Modrinth")
        ], 0);

        // WPF sort tags: Default / Relevance / Downloads / Follows / Latest / Updated
        BindCombo("ComboSearchSort",
        [
            new FilterOption("default", "默认"),
            new FilterOption("relevance", "相关度"),
            new FilterOption("downloads", "下载量"),
            new FilterOption("follows", "关注数"),
            new FilterOption("newest", "最新创建"),
            new FilterOption("updated", "最近更新")
        ], 0);

        BindCombo("TextSearchVersion",
        [
            new FilterOption("", "任意"),
            new FilterOption("1.21.11", "1.21.11"),
            new FilterOption("1.21.1", "1.21.1"),
            new FilterOption("1.21", "1.21"),
            new FilterOption("1.20.6", "1.20.6"),
            new FilterOption("1.20.4", "1.20.4"),
            new FilterOption("1.20.1", "1.20.1"),
            new FilterOption("1.19.4", "1.19.4"),
            new FilterOption("1.19.2", "1.19.2"),
            new FilterOption("1.18.2", "1.18.2"),
            new FilterOption("1.16.5", "1.16.5"),
            new FilterOption("1.12.2", "1.12.2"),
            new FilterOption("1.10.2", "1.10.2"),
            new FilterOption("1.8.9", "1.8.9"),
            new FilterOption("1.7.10", "1.7.10")
        ], 0);

        BindCombo("ComboSearchLoader",
        [
            new FilterOption("any", "任意"),
            new FilterOption("forge", "Forge"),
            new FilterOption("neoforge", "NeoForge"),
            new FilterOption("fabric", "Fabric"),
            new FilterOption("quilt", "Quilt")
        ], 0);

        BindCombo("ComboSearchShaderLoader",
        [
            new FilterOption("any", "任意光影加载器"),
            new FilterOption("vanilla", "原版可用"),
            new FilterOption("iris", "Iris"),
            new FilterOption("optifine", "OptiFine")
        ], 0);

        PopulateTagItems();
    }

    private void PopulateTagItems()
    {
        List<FilterOption> tags = _category switch
        {
            CommunityResourceCategory.Mod =>
            [
                new("", "全部"),
                new("technology", "科技"),
                new("magic", "魔法"),
                new("adventure", "冒险"),
                new("decoration", "装饰"),
                new("utility", "实用"),
                new("library", "支持库"),
                new("optimization", "优化"),
                new("food", "食物"),
                new("storage", "存储"),
                new("equipment", "装备"),
                new("worldgen", "世界生成")
            ],
            CommunityResourceCategory.Modpack =>
            [
                new("", "全部"),
                new("multiplayer", "多人"),
                new("challenging", "挑战"),
                new("quests", "任务"),
                new("lightweight", "轻量")
            ],
            CommunityResourceCategory.ResourcePack =>
            [
                new("", "全部"),
                new("realistic", "写实"),
                new("simplistic", "简洁"),
                new("fantasy", "奇幻"),
                new("combat", "战斗")
            ],
            CommunityResourceCategory.Shader =>
            [
                new("", "全部"),
                new("cartoon", "卡通"),
                new("realistic", "写实"),
                new("semi-realistic", "半写实")
            ],
            _ => [new FilterOption("", "全部")]
        };

        BindCombo("ComboSearchTag", tags, 0);
    }

    private void ApplyCategoryFilterVisibility()
    {
        bool showLoader = _category is CommunityResourceCategory.Mod or CommunityResourceCategory.Modpack;
        bool showShaderLoader = _category == CommunityResourceCategory.Shader;
        if (this.FindControl<Control>("LabLoader") is { } lab)
            lab.IsVisible = showLoader || showShaderLoader;
        if (this.FindControl<Control>("ComboSearchLoader") is { } loader)
            loader.IsVisible = showLoader;
        if (this.FindControl<Control>("ComboSearchShaderLoader") is { } shader)
            shader.IsVisible = showShaderLoader;
        if (this.FindControl<Control>("BtnSearchInstallModPack") is { } installPack)
            installPack.IsVisible = _category == CommunityResourceCategory.Modpack;
    }

    private void BindCombo(string name, IReadOnlyList<FilterOption> items, int selectedIndex)
    {
        if (this.FindControl<MyComboBox>(name) is not { } combo)
            return;

        combo.SelectionChanged -= Combo_SelectionChanged;
        combo.ItemsSource = items;
        combo.SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, items.Count - 1));
        combo.SelectionChanged += Combo_SelectionChanged;
    }

    private void Combo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_filtersReady)
            return;
        RestartSearchTimer();
    }

    private void RestartSearchTimer()
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private CommunitySearchOptions BuildSearchOptions()
    {
        // WPF PageComp sort tags → Modrinth index (follows/newest map to updated client-side).
        CommunityResourceSort sort = CommunityResourceSort.Relevance;
        if (this.FindControl<MyComboBox>("ComboSearchSort")?.SelectedItem is FilterOption sortOpt)
        {
            sort = sortOpt.Id switch
            {
                "downloads" => CommunityResourceSort.Downloads,
                "updated" or "newest" or "follows" => CommunityResourceSort.Updated,
                "relevance" => CommunityResourceSort.Relevance,
                "default" => CommunityResourceSort.Relevance,
                _ => CommunityResourceSort.Relevance
            };
        }

        string? gameVersion = null;
        if (this.FindControl<MyComboBox>("TextSearchVersion") is { } versionCombo)
        {
            if (versionCombo.SelectedItem is FilterOption vo && !string.IsNullOrWhiteSpace(vo.Id))
                gameVersion = vo.Id;
            else if (!string.IsNullOrWhiteSpace(versionCombo.Text) &&
                     !string.Equals(versionCombo.Text.Trim(), "任意", StringComparison.Ordinal))
            {
                gameVersion = versionCombo.Text.Trim();
            }
        }

        string? loader = null;
        if (_category == CommunityResourceCategory.Shader)
        {
            if (this.FindControl<MyComboBox>("ComboSearchShaderLoader")?.SelectedItem is FilterOption so &&
                so.Id is not ("any" or ""))
            {
                loader = so.Id;
            }
        }
        else if (this.FindControl<MyComboBox>("ComboSearchLoader")?.SelectedItem is FilterOption lo &&
                 lo.Id is not ("any" or ""))
        {
            loader = lo.Id;
        }

        string? tag = SelectedTagId();
        CommunityResourceSource source =
            this.FindControl<MyComboBox>("ComboSearchSource")?.SelectedItem is FilterOption sourceOption
                ? sourceOption.Id switch
                {
                    "curseforge" => CommunityResourceSource.CurseForge,
                    "modrinth" => CommunityResourceSource.Modrinth,
                    _ => CommunityResourceSource.All
                }
                : CommunityResourceSource.All;
        return new CommunitySearchOptions(sort, gameVersion, loader, tag, source);
    }

    private string? SelectedTagId()
    {
        if (this.FindControl<MyComboBox>("ComboSearchTag")?.SelectedItem is FilterOption tag &&
            !string.IsNullOrWhiteSpace(tag.Id))
        {
            return tag.Id;
        }

        return null;
    }

    private void RenderPage()
    {
        if (this.FindControl<StackPanel>("PanProjects") is not { } panel)
            return;

        panel.Children.Clear();
        CommunityResourceEntry[] pageItems = _entries
            .Skip(_page * PageSize)
            .Take(PageSize)
            .ToArray();

        if (pageItems.Length == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "没有找到资源。请调整搜索词或筛选条件。",
                Margin = new Thickness(8, 12, 8, 12),
                Opacity = 0.7d,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            CommunitySearchOptions options = BuildSearchOptions();
            foreach (CommunityResourceEntry entry in pageItems)
                panel.Children.Add(CreateCompItem(entry, options));
        }

        // WPF LabPage shows the current page number (1-based).
        if (this.FindControl<TextBlock>("LabPage") is { } labPage)
            labPage.Text = (_page + 1).ToString(CultureInfo.CurrentCulture);

        bool canPrev = _page > 0;
        bool canNext = _page < _totalPages - 1;
        SetPageButton("BtnPageFirst", canPrev);
        SetPageButton("BtnPageLeft", canPrev);
        SetPageButton("BtnPageRight", canNext);

        ControlVisualHelpers.AnimateListEntrance(panel, "Community Project List");
    }

    private void SetPageButton(string name, bool enabled)
    {
        if (this.FindControl<MyIconButton>(name) is not { } button)
            return;
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1d : 0.2d;
    }

    /// <summary>MyCompItem-style row: icon · title · description · downloads/time/source · actions.</summary>
    private MyListItem CreateCompItem(CommunityResourceEntry entry, CommunitySearchOptions options)
    {
        MyIconButton favorite = new()
        {
            SvgIcon = _favorites?.Contains(entry) == true ? "lucide/star-off" : "lucide/star",
            LogoScale = 0.9d,
            ToolTip = _favorites?.Contains(entry) == true ? "取消收藏" : "收藏",
            Width = 25,
            Height = 25,
            Margin = new Thickness(0, 0, 4, 0),
            IsVisible = _favorites is not null
        };
        favorite.Click += (_, _) =>
        {
            if (_favorites is null)
                return;
            bool added = _favorites.Toggle(entry, _category);
            favorite.SvgIcon = added ? "lucide/star-off" : "lucide/star";
            favorite.ToolTip = added ? "取消收藏" : "收藏";
        };

        MyIconButton website = new()
        {
            SvgIcon = "lucide/external-link",
            LogoScale = 0.9d,
            ToolTip = "打开项目页面",
            Width = 25,
            Height = 25,
            Margin = new Thickness(0, 0, 4, 0)
        };
        website.Click += (_, _) => OpenProjectRequested?.Invoke(this, entry);

        MyIconButton download = new()
        {
            SvgIcon = "lucide/download",
            LogoScale = 0.9d,
            ToolTip = "下载到当前实例",
            Width = 25,
            Height = 25
        };
        download.Click += (_, _) => DownloadRequested?.Invoke(
            this,
            new CommunityResourceDownloadRequest(entry, _category, options));

        string downloadsText = entry.Downloads > 0
            ? FormatCount(entry.Downloads)
            : "—";
        string timeText = entry.UpdatedAt is { } updated
            ? updated.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.CurrentCulture)
            : "—";

        string info = entry.Description;
        if (string.IsNullOrWhiteSpace(info))
            info = "暂无简介";

        // Logo = remote icon (async + NoIcon placeholder); SvgIcon is fallback while empty.
        // Height 64 → logo column ~50px (WPF MyCompItem). LogoScale 1 keeps icon filling the cell.
        MyListItem item = new()
        {
            Title = entry.Title,
            Info = info + "  ·  ↓" + downloadsText + "  ·  " + timeText + "  ·  " +
                   (entry.Source == CommunityResourceSource.CurseForge ? "CurseForge" : "Modrinth"),
            Height = 64d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            SvgIcon = CategoryIcon(_category),
            Logo = entry.IconUrl ?? string.Empty,
            LogoScale = 1.05d,
            Buttons = [download, favorite, website]
        };

        item.Click += (_, _) => OpenProjectRequested?.Invoke(this, entry);
        return item;
    }

    private static string FormatCount(long count)
    {
        if (count >= 1_000_000)
            return (count / 1_000_000d).ToString("0.0", CultureInfo.CurrentCulture) + "M";
        if (count >= 1_000)
            return (count / 1_000d).ToString("0.0", CultureInfo.CurrentCulture) + "K";
        return count.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void SetLoadingState(bool loading)
    {
        // WPF-like: hide project list entirely while loading — no empty bars under the spinner,
        // and never cover the search/filter row above.
        if (this.FindControl<Control>("PanLoad") is { } loadPanel)
            loadPanel.IsVisible = loading;
        if (this.FindControl<Control>("PanContent") is { } content)
            content.IsVisible = !loading;
        if (this.FindControl<MyLoading>("Load") is { } load)
            load.State.LoadingState = loading ? MyLoading.MyLoadingState.Run : MyLoading.MyLoadingState.Stop;

        if (loading && this.FindControl<StackPanel>("PanProjects") is { } panel)
            panel.Children.Clear();
    }

    private void SetError(string? message)
    {
        if (this.FindControl<MyHint>("HintError") is not { } hint)
            return;
        if (string.IsNullOrWhiteSpace(message))
        {
            hint.IsVisible = false;
            return;
        }

        hint.IsVisible = true;
        hint.Text = "社区资源加载失败：" + message;
    }

    private static string CategoryIcon(CommunityResourceCategory category) =>
        category switch
        {
            CommunityResourceCategory.Mod => "lucide/puzzle",
            CommunityResourceCategory.Modpack => "lucide/package",
            CommunityResourceCategory.DataPack => "lucide/file-archive",
            CommunityResourceCategory.ResourcePack => "lucide/layers",
            CommunityResourceCategory.Shader => "lucide/sparkles",
            CommunityResourceCategory.World => "lucide/globe",
            _ => "lucide/download"
        };

    private sealed record FilterOption(string Id, string Title)
    {
        public override string ToString() => Title;
    }
}

public sealed record CommunityResourceDownloadRequest(
    CommunityResourceEntry Entry,
    CommunityResourceCategory Category,
    CommunitySearchOptions Options,
    CommunityResourceDownloadFile? PreferredFile = null);
