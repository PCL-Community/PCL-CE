// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Community;

/// <summary>
/// Independent resource detail page — structure mirrors WPF <c>PageDownloadCompDetail</c>:
/// intro card (project row + action buttons), chip filters, version-group cards, load overlay.
/// </summary>
public partial class PageCommunityDetail : MyPageRight, IDisposable
{
    private readonly ICommunityResourceCatalog _catalog;
    private readonly bool _ownsCatalog;
    private readonly CommunityFavoritesStore? _favorites;
    private CommunityResourceEntry? _entry;
    private CommunitySearchOptions _baseOptions = new();
    private CommunityResourceCategory _category = CommunityResourceCategory.Mod;
    private IReadOnlyList<CommunityResourceVersion> _allVersions = [];
    private string? _instanceFilter; // null = 全部
    private string? _loaderFilter;
    private CancellationTokenSource? _loadCancellation;
    private bool _disposed;
    private bool _filtersReady;

    public PageCommunityDetail()
        : this(new CompositeCommunityResourceCatalog(), ownsCatalog: true)
    {
    }

    public PageCommunityDetail(
        ICommunityResourceCatalog catalog,
        bool ownsCatalog = false,
        CommunityFavoritesStore? favorites = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ownsCatalog = ownsCatalog;
        _favorites = favorites;
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");

        if (this.FindControl<MyIconTextButton>("BtnIntroWeb") is { } web)
            web.Click += (_, _) =>
            {
                if (_entry is not null)
                    OpenWebRequested?.Invoke(this, _entry);
            };
        if (this.FindControl<MyIconTextButton>("BtnIntroCopy") is { } copyName)
            copyName.Click += (_, _) => _ = CopyTextAsync(_entry?.Title ?? string.Empty);
        if (this.FindControl<MyIconTextButton>("BtnIntroLinkCopy") is { } copyLink)
            copyLink.Click += (_, _) => _ = CopyTextAsync(_entry?.WebsiteUrl ?? string.Empty);
        if (this.FindControl<MyIconTextButton>("BtnIntroFavorite") is { } favorite)
            favorite.Click += (_, _) => ToggleFavorite();

        SetLoading(false);
    }

    // Host may subscribe for title-bar / gesture back; currently title bar owns navigation.
#pragma warning disable CS0067
    public event EventHandler? BackRequested;
#pragma warning restore CS0067

    public event EventHandler<CommunityResourceEntry>? OpenWebRequested;

    public event EventHandler<CommunityResourceDownloadRequest>? DownloadRequested;

    public CommunityResourceEntry? Entry => _entry;

    public async Task ShowAsync(
        CommunityResourceEntry entry,
        CommunityResourceCategory category,
        CommunitySearchOptions? options = null)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _category = category;
        _baseOptions = options ?? new CommunitySearchOptions();
        _instanceFilter = null;
        _loaderFilter = null;
        _filtersReady = false;
        BindIntro(entry);
        UpdateFavoriteButton();
        await ReloadVersionsAsync().ConfigureAwait(true);
    }

    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        if (_ownsCatalog && _catalog is IDisposable d)
            d.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void BindIntro(CommunityResourceEntry entry)
    {
        if (this.FindControl<MyListItem>("ItemProject") is { } item)
        {
            item.Title = entry.Title;
            string downloads = entry.Downloads > 0
                ? entry.Downloads.ToString("N0", CultureInfo.CurrentCulture) + " 次下载"
                : string.Empty;
            item.Info = string.IsNullOrWhiteSpace(entry.Description)
                ? downloads
                : entry.Description + (string.IsNullOrEmpty(downloads) ? string.Empty : " · " + downloads);
            item.Logo = entry.IconUrl ?? string.Empty;
            item.SvgIcon = string.IsNullOrWhiteSpace(entry.IconUrl) ? "lucide/package" : string.Empty;
            item.LogoScale = 1.08d;
            item.Type = MyListItem.CheckType.None;
        }

        if (this.FindControl<MyIconTextButton>("BtnIntroWeb") is { } web)
            web.Text = entry.Source == CommunityResourceSource.CurseForge ? "CurseForge" : "Modrinth";
    }

    private void ToggleFavorite()
    {
        if (_favorites is null || _entry is null)
            return;
        _favorites.Toggle(_entry, _category);
        UpdateFavoriteButton();
    }

    private void UpdateFavoriteButton()
    {
        if (this.FindControl<MyIconTextButton>("BtnIntroFavorite") is not { } button)
            return;
        button.IsVisible = _favorites is not null;
        bool favorite = _favorites is not null && _entry is not null && _favorites.Contains(_entry);
        button.Text = favorite ? "取消收藏" : "收藏";
        button.SvgIcon = favorite ? "lucide/star-off" : "lucide/star";
    }

    private async Task ReloadVersionsAsync()
    {
        if (_entry is null || _disposed)
            return;

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        CancellationToken token = _loadCancellation.Token;
        SetLoading(true);
        SetError(null);

        try
        {
            // Fetch without client-side filters first so chips can list all options (WPF CompFilesGet).
            CommunitySearchOptions fetchOptions = new(
                _baseOptions.Sort,
                GameVersion: null,
                Loader: null,
                Tag: _baseOptions.Tag,
                Source: _entry.Source);
            IReadOnlyList<CommunityResourceVersion> versions =
                await _catalog.GetVersionsAsync(_entry, fetchOptions, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested)
                    return;
                _allVersions = versions;
                BuildFilterChips(versions);
                ApplyFiltersAndRender();
                SetLoading(false);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetError(ex.Message);
                SetLoading(false);
            });
        }
    }

    private void BuildFilterChips(IReadOnlyList<CommunityResourceVersion> versions)
    {
        Panel? panVersion = this.FindControl<Panel>("PanInstanceFilter");
        Panel? panLoader = this.FindControl<Panel>("PanModLoaderFilter");
        Control? cardFilter = this.FindControl<Control>("CardFilter");
        if (panVersion is null || panLoader is null)
            return;

        panVersion.Children.Clear();
        panLoader.Children.Clear();

        HashSet<string> gameVersions = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> loaders = new(StringComparer.OrdinalIgnoreCase);
        foreach (CommunityResourceVersion v in versions)
        {
            foreach (string g in v.GameVersions)
                if (!string.IsNullOrWhiteSpace(g))
                    gameVersions.Add(g);
            foreach (string l in v.Loaders)
                if (!string.IsNullOrWhiteSpace(l))
                    loaders.Add(l);
        }

        List<string> orderedVersions = gameVersions
            .OrderByDescending(static s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<string> orderedLoaders = loaders
            .OrderBy(static s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool showFilters = orderedVersions.Count >= 2 || orderedLoaders.Count >= 1;
        if (cardFilter is not null)
            cardFilter.IsVisible = showFilters;

        if (!showFilters)
        {
            _filtersReady = true;
            return;
        }

        // Prefer list-page filters when present; otherwise default to “全部”.
        string? preferredVersion = _baseOptions.GameVersion;
        if (!string.IsNullOrWhiteSpace(preferredVersion) &&
            orderedVersions.Any(v => string.Equals(v, preferredVersion, StringComparison.OrdinalIgnoreCase)))
        {
            _instanceFilter = preferredVersion;
        }
        else
        {
            _instanceFilter = null;
        }

        panVersion.Children.Add(CreateFilterLabel("版本"));
        AddChip(panVersion, "全部", isVersion: true, selected: _instanceFilter is null);
        foreach (string v in orderedVersions.Take(24))
        {
            bool selected = _instanceFilter is not null &&
                            string.Equals(v, _instanceFilter, StringComparison.OrdinalIgnoreCase);
            AddChip(panVersion, v, isVersion: true, selected: selected);
        }

        // Loader chips (mods / modpacks)
        bool showLoaderRow = _category is CommunityResourceCategory.Mod or CommunityResourceCategory.Modpack
                             && orderedLoaders.Count > 0;
        panLoader.IsVisible = showLoaderRow;
        if (showLoaderRow)
        {
            string? preferredLoader = _baseOptions.Loader;
            if (!string.IsNullOrWhiteSpace(preferredLoader) &&
                orderedLoaders.Any(l => string.Equals(l, preferredLoader, StringComparison.OrdinalIgnoreCase)))
            {
                _loaderFilter = preferredLoader;
            }
            else
            {
                _loaderFilter = null;
            }

            panLoader.Children.Add(CreateFilterLabel("加载器"));
            AddChip(panLoader, "全部", isVersion: false, selected: _loaderFilter is null);
            foreach (string l in orderedLoaders)
            {
                bool selected = _loaderFilter is not null &&
                                string.Equals(l, _loaderFilter, StringComparison.OrdinalIgnoreCase);
                AddChip(panLoader, l, isVersion: false, selected: selected);
            }
        }
        else
        {
            _loaderFilter = null;
        }

        _filtersReady = true;
    }

    private static TextBlock CreateFilterLabel(string text) =>
        new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0),
            Opacity = 0.75,
            FontSize = 12
        };

    private void AddChip(Panel panel, string text, bool isVersion, bool selected)
    {
        MyRadioButton chip = new()
        {
            Text = text,
            ColorType = MyRadioButton.ColorState.Highlight,
            Margin = new Thickness(2, 2, 2, 2),
            Checked = selected,
            // Keep chips compact so they wrap inside CardFilter (WPF-style).
            MaxWidth = 118,
            MinHeight = 26
        };
        // Hide logo host for text-only chips (WPF LabText-only radio).
        chip.SvgIcon = string.Empty;
        chip.Logo = string.Empty;
        chip.Check += (sender, _) =>
        {
            if (!_filtersReady || sender is not MyRadioButton rb)
                return;
            if (isVersion)
                _instanceFilter = string.Equals(rb.Text, "全部", StringComparison.Ordinal) ? null : rb.Text;
            else
                _loaderFilter = string.Equals(rb.Text, "全部", StringComparison.Ordinal) ? null : rb.Text;
            ApplyFiltersAndRender();
        };
        panel.Children.Add(chip);
    }

    private void ApplyFiltersAndRender()
    {
        if (this.FindControl<StackPanel>("PanResults") is not { } panResults)
            return;

        panResults.Children.Clear();
        List<CommunityResourceVersion> filtered = _allVersions.ToList();
        if (!string.IsNullOrWhiteSpace(_instanceFilter))
        {
            filtered = filtered
                .Where(v =>
                    v.GameVersions.Any(g => string.Equals(g, _instanceFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(_loaderFilter))
        {
            filtered = filtered
                .Where(v =>
                    v.Loaders.Any(l => string.Equals(l, _loaderFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // WPF PageDownloadCompDetail / CompFilesGet: one card per Minecraft version.
        // With a chip filter, collapse into that single card; otherwise assign each
        // file to its newest supported game version (avoids multi-card fan-out spam).
        Dictionary<string, List<CommunityResourceVersion>> groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (CommunityResourceVersion version in filtered)
        {
            if (!string.IsNullOrWhiteSpace(_instanceFilter))
            {
                AddToGroup(groups, _instanceFilter!, version);
                continue;
            }

            string key = "其他";
            if (version.GameVersions.Count > 0)
            {
                key = version.GameVersions
                    .Where(static g => !string.IsNullOrWhiteSpace(g))
                    .OrderByDescending(static g => g, MinecraftVersionNameComparer.Instance)
                    .FirstOrDefault() ?? "其他";
            }

            AddToGroup(groups, key, version);
        }

        if (groups.Count == 0)
        {
            panResults.Children.Add(new TextBlock
            {
                Text = "没有匹配的版本文件。请调整筛选条件。",
                Margin = new Thickness(8, 12),
                Opacity = 0.7
            });
            return;
        }

        // Newest game versions first (WPF uses version-aware sort).
        List<KeyValuePair<string, List<CommunityResourceVersion>>> ordered = groups
            .OrderByDescending(static g => g.Key, MinecraftVersionNameComparer.Instance)
            .ToList();

        int cardIndex = 0;
        foreach ((string title, List<CommunityResourceVersion> list) in ordered)
        {
            // Within a card: newest publish date first.
            List<CommunityResourceVersion> sorted = list
                .OrderByDescending(static v => v.PublishedAt ?? DateTimeOffset.MinValue)
                .ToList();

            MyCard card = new()
            {
                Title = title + "（" + sorted.Count.ToString(CultureInfo.CurrentCulture) + "）",
                Margin = new Thickness(0, 0, 0, 15),
                CanSwap = true,
                // First (newest) group expanded; rest collapsed — matches WPF default.
                IsSwapped = cardIndex > 0
            };

            StackPanel stack = new()
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 8d),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = sorted
            };
            card.SwapControl = stack;
            card.InstallMethod = InstallVersionStack;
            // MyCard only sizes to SwapControl when it is a visual child (same as install/select pages).
            card.Children.Add(stack);
            panResults.Children.Add(card);

            if (!card.IsSwapped)
                card.StackInstall();

            cardIndex++;
        }

        if (panResults.Children.Count == 1 && panResults.Children[0] is MyCard only)
            only.IsSwapped = false;
    }

    private static void AddToGroup(
        Dictionary<string, List<CommunityResourceVersion>> groups,
        string key,
        CommunityResourceVersion version)
    {
        if (!groups.TryGetValue(key, out List<CommunityResourceVersion>? list))
        {
            list = [];
            groups[key] = list;
        }

        // Avoid duplicating the same version id inside one card.
        if (list.Any(v => string.Equals(v.VersionId, version.VersionId, StringComparison.OrdinalIgnoreCase)))
            return;
        list.Add(version);
    }

    private void InstallVersionStack(StackPanel stack)
    {
        if (stack.Tag is not List<CommunityResourceVersion> list || _entry is null)
            return;

        stack.Children.Clear();
        CommunitySearchOptions options = new(
            _baseOptions.Sort,
            _instanceFilter,
            _loaderFilter,
            _baseOptions.Tag);

        foreach (CommunityResourceVersion version in list.OrderByDescending(v => v.PublishedAt ?? DateTimeOffset.MinValue))
        {
            CommunityResourceDownloadFile? primary = version.Files.Count > 0 ? version.Files[0] : null;
            if (primary is null)
                continue;

            string loaders = version.Loaders.Count > 0 ? string.Join(", ", version.Loaders) : "—";
            string published = version.PublishedAt is { } p
                ? p.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.CurrentCulture)
                : "—";
            string size = primary.Size > 0 ? FormatSize(primary.Size) : "—";

            MyIconButton download = new()
            {
                SvgIcon = "lucide/download",
                LogoScale = 0.85d,
                ToolTip = "下载 " + primary.FileName,
                Width = 25,
                Height = 25
            };
            CommunityResourceEntry entry = _entry;
            CommunityResourceDownloadFile file = primary;
            download.Click += (_, _) => DownloadRequested?.Invoke(
                this,
                new CommunityResourceDownloadRequest(entry, _category, options, file));

            MyListItem item = new()
            {
                Title = string.IsNullOrWhiteSpace(version.Name)
                    ? version.VersionNumber
                    : version.Name,
                Info = loaders + " · " + size + " · " + published + " · " + primary.FileName,
                Height = 48d,
                Type = MyListItem.CheckType.Clickable,
                SvgIcon = "lucide/file-archive",
                Buttons = [download],
                Tag = version
            };
            item.Click += (_, _) => DownloadRequested?.Invoke(
                this,
                new CommunityResourceDownloadRequest(entry, _category, options, file));
            stack.Children.Add(item);
        }
    }

    private void SetLoading(bool loading)
    {
        if (this.FindControl<Control>("PanLoad") is { } load)
            load.IsVisible = loading;
        if (this.FindControl<Control>("PanMain") is { } main)
            main.IsVisible = !loading;
        if (this.FindControl<MyLoading>("Load") is { } spinner)
            spinner.State.LoadingState = loading ? MyLoading.MyLoadingState.Run : MyLoading.MyLoadingState.Stop;
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
        hint.Text = "获取版本失败：" + message;
    }

    private async Task CopyTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        try
        {
            TopLevel? top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard is null)
                return;
            await top.Clipboard.SetTextAsync(text).ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";
        if (bytes < 1024 * 1024)
            return (bytes / 1024d).ToString("0.0", CultureInfo.CurrentCulture) + " KB";
        return (bytes / (1024d * 1024d)).ToString("0.00", CultureInfo.CurrentCulture) + " MB";
    }

    /// <summary>Orders Minecraft version labels newest-first (1.21 &gt; 1.20.1 &gt; 1.12.2).</summary>
    private sealed class MinecraftVersionNameComparer : IComparer<string>
    {
        public static MinecraftVersionNameComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;
            if (string.Equals(x, "其他", StringComparison.Ordinal) &&
                !string.Equals(y, "其他", StringComparison.Ordinal))
            {
                return -1;
            }

            if (string.Equals(y, "其他", StringComparison.Ordinal) &&
                !string.Equals(x, "其他", StringComparison.Ordinal))
            {
                return 1;
            }

            Version? vx = TryParseMc(x);
            Version? vy = TryParseMc(y);
            if (vx is not null && vy is not null)
                return vx.CompareTo(vy);
            if (vx is not null)
                return 1;
            if (vy is not null)
                return -1;
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static Version? TryParseMc(string text)
        {
            string core = text.Split('-', '+')[0].Trim();
            return Version.TryParse(core, out Version? v) ? v : null;
        }
    }
}
