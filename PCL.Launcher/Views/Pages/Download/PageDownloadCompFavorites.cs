using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.UI.Controls;
using PCL.UI.Controls.Common;

namespace PCL.Views.Pages.Download;

internal sealed class PageDownloadCompFavorites : MyPageRight, IPageView, IRefreshable
{
    private readonly MyIconTextButton _btnCancelFavorite;
    private readonly MyIconTextButton _btnDeselect;
    private readonly MyIconTextButton _btnDownload;
    private readonly MyIconButton _btnManage;
    private readonly MyIconTextButton _btnShare;
    private readonly MyCard _cardEmpty;
    private readonly MyCard _cardSelect;
    private readonly Dictionary<int, CategoryCard> _cards = new();
    private readonly MyComboBox _comboGroups;
    private readonly StackPanel _contentStack;
    private readonly MyHint _hintGetFail;
    private readonly List<(MyListItem Item, Comp.CompProject Project)> _itemList = [];
    private readonly TextBlock _labSelected;
    private readonly MyLoading _load;
    private readonly MyCard _loadCard;
    private readonly StackPanel _panCards;
    private readonly MySearchBox _searchBox;
    private readonly List<MyListItem> _selectedItems = [];
    private bool _autoRetried;
    private bool _inited;
    private Loading.LoaderTask<int, List<Comp.CompProject>>? _loader;
    private bool _updatingGroups;

    public PageDownloadCompFavorites()
    {
        _searchBox = new MySearchBox
        {
            HintText = Lang.Text("Download.Comp.Favorites.Search.Hint"),
            SearchButtonVisibility = true
        };
        _searchBox.Search += (_, _) => RenderProjects();
        _searchBox.TextChanged += (_, _) => RenderProjects();

        _comboGroups = new MyComboBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 80d,
            MaxWidth = 160d,
            Margin = new Thickness(8d, 0d, 4d, 0d)
        };
        _comboGroups.SelectionChanged += (_, _) =>
        {
            if (_updatingGroups) return;
            SetAllSelected(false);
            Reload();
        };
        _btnManage = new MyIconButton
        {
            SvgIcon = "lucide/settings-2",
            LogoScale = 1.1d,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(_btnManage, Lang.Text("Download.Comp.Favorites.Manage"));
        _btnManage.Click += (_, _) => ShowManageMenu();

        var manageCard = new MyCard
        {
            Margin = new Thickness(15d, 0d, 0d, 0d),
            Height = 40d,
            CanSwap = false
        };
        var manageStack = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        manageStack.Children.Add(_comboGroups);
        manageStack.Children.Add(_btnManage);
        manageCard.Children.Add(manageStack);

        var searchRow = new Grid
        {
            Margin = new Thickness(0d, 0d, 0d, 15d),
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        Grid.SetColumn(_searchBox, 0);
        Grid.SetColumn(manageCard, 1);
        searchRow.Children.Add(_searchBox);
        searchRow.Children.Add(manageCard);

        _hintGetFail = new MyHint
        {
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 0d, 0d, 15d),
            IsVisible = false,
            Text = Lang.Text("Download.Comp.Favorites.GetInfoFailed"),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        _hintGetFail.PointerReleased += (_, _) => ShowGetFailedDialog();

        _panCards = new StackPanel();
        _cardEmpty = new MyCard
        {
            Title = Lang.Text("Download.Comp.Favorites.Empty.Title"),
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(40d)
        };
        _cardEmpty.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Download.Comp.Favorites.Empty.Description"),
                TextWrapping = TextWrapping.Wrap,
                Width = 260d,
                Margin = new Thickness(20d, MyCard.SwapedHeight, 20d, 20d),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });

        var contentStack = new StackPanel
        {
            Margin = new Thickness(25d)
        };
        _contentStack = contentStack;
        contentStack.Children.Add(searchRow);
        contentStack.Children.Add(_hintGetFail);
        contentStack.Children.Add(_panCards);
        contentStack.Children.Add(_cardEmpty);

        var scroll = new MyScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = contentStack
        };

        _cardSelect = new MyCard
        {
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(25d, 25d, 25d, 0d)
        };
        _labSelected = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(9d)
        };
        _cardSelect.Children.Add(_labSelected);
        var selectButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(5d, MyCard.SwapedHeight, 5d, 5d)
        };
        _btnCancelFavorite = CreateActionButton(
            "lucide/heart-filled",
            Lang.Text("Download.Comp.Favorites.Cancel"),
            OnCancelSelectedClicked);
        _btnShare = CreateActionButton(
            "lucide/share-2",
            Lang.Text("Download.Comp.Favorites.Share"),
            OnShareSelectedClicked);
        _btnDownload = CreateActionButton(
            "lucide/download",
            Lang.Text("Download.Comp.Favorites.Download"),
            () => _ = BatchDownloadAsync());
        _btnDeselect = CreateActionButton(
            "lucide/download",
            Lang.Text("Download.Comp.Favorites.Deselect"),
            () => SetAllSelected(false));
        selectButtons.Children.Add(_btnCancelFavorite);
        selectButtons.Children.Add(_btnShare);
        selectButtons.Children.Add(_btnDownload);
        selectButtons.Children.Add(_btnDeselect);
        _cardSelect.Children.Add(selectButtons);

        _load = new MyLoading
        {
            Margin = new Thickness(20d, 20d, 20d, 17d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = Lang.Text("Download.Comp.Favorites.Loading")
        };
        _loadCard = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d, 50d, 40d, 50d),
            UseAnimation = false,
            IsVisible = false
        };
        _loadCard.Children.Add(_load);

        var root = new Grid();
        root.Children.Add(scroll);
        root.Children.Add(_loadCard);
        root.Children.Add(_cardSelect);
        Child = root;
        PanScroll = scroll;

        KeyDown += OnPageKeyDown;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        AttachedToVisualTree += (_, _) => InitOnce();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshGroupCombo();
        Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        RefreshGroupCombo();
        Reload();
    }

    private static MyIconTextButton CreateActionButton(string icon, string text, Action onClick)
    {
        var button = new MyIconTextButton
        {
            SvgIcon = icon,
            Text = text,
            LogoScale = 1d
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void InitOnce()
    {
        if (_inited) return;
        _inited = true;

        _loader = new Loading.LoaderTask<int, List<Comp.CompProject>>(
            Lang.Text("Download.Comp.Favorites.Loading"),
            task =>
            {
                var group = GetSelectedGroup();
                if (group is null || group.Favs.Count == 0)
                {
                    task.output = [];
                    return;
                }

                task.output = Comp.CompRequest.GetCompProjectsByIds([.. group.Favs]);
            })
        {
            reloadTimeout = 60 * 1000
        };
        RefreshGroupCombo();
        PageLoaderInit(
            _load,
            _loadCard,
            _contentStack,
            null,
            _loader,
            _ => RenderProjects(),
            () => 0);
        _loader.OnStateChangedUi += (_, newState, _) =>
        {
            if (newState != LoadState.Failed || _autoRetried)
                return;
            if (_loader.Error?.Message.Contains(
                    Lang.Text("Common.Error.InvalidJson"),
                    StringComparison.OrdinalIgnoreCase)
                != true)
                return;
            _autoRetried = true;
            PageLoaderRestart(0);
        };
    }

    private void OnLanguageChanged()
    {
        var selectedIds = _selectedItems
            .Select(item => item.tag)
            .OfType<Comp.CompProject>()
            .Select(project => project.Id)
            .ToHashSet();
        _searchBox.HintText = Lang.Text("Download.Comp.Favorites.Search.Hint");
        _cardEmpty.Title = Lang.Text("Download.Comp.Favorites.Empty.Title");
        if (_cardEmpty.Children.Count > 0
            && _cardEmpty.Children[0] is TextBlock emptyDescription)
            emptyDescription.Text = Lang.Text("Download.Comp.Favorites.Empty.Description");
        _hintGetFail.Text = Lang.Text("Download.Comp.Favorites.GetInfoFailed");
        _load.Text = Lang.Text("Download.Comp.Favorites.Loading");
        _btnCancelFavorite.Text = Lang.Text("Download.Comp.Favorites.Cancel");
        _btnShare.Text = Lang.Text("Download.Comp.Favorites.Share");
        _btnDownload.Text = Lang.Text("Download.Comp.Favorites.Download");
        _btnDeselect.Text = Lang.Text("Download.Comp.Favorites.Deselect");
        ToolTip.SetTip(_btnManage, Lang.Text("Download.Comp.Favorites.Manage"));
        _cards.Clear();
        RenderProjects();
        if (selectedIds.Count <= 0) return;
        foreach (var entry in _itemList.Where(entry => selectedIds.Contains(entry.Project.Id)))
            entry.Item.Checked = true;
    }

    private sealed class CategoryCard
    {
        public required MyCard Card { get; init; }
        public required StackPanel List { get; init; }
        public required Func<int, string> TitleFormat { get; init; }
    }

    #region 收藏夹

    private void RefreshGroupCombo()
    {
        _updatingGroups = true;
        try
        {
            var groups = Comp.CompFavorites.FavoritesList;
            var selectedId = GetSelectedGroup()?.Id;
            _comboGroups.Items.Clear();
            if (groups is null || groups.Count == 0)
                return;
            foreach (var group in groups)
                _comboGroups.Items.Add(
                    new MyComboBoxItem
                    {
                        Content = group.Name,
                        Tag = group.Id
                    });
            var index = groups.FindIndex(group => group.Id == selectedId);
            _comboGroups.SelectedIndex = index >= 0 ? index : 0;
        }
        finally
        {
            _updatingGroups = false;
        }
    }

    private Comp.CompFavorites.FavData? GetSelectedGroup()
    {
        var groups = Comp.CompFavorites.FavoritesList;
        if (groups is null || groups.Count == 0) return null;
        if (_comboGroups.SelectedIndex >= 0 && _comboGroups.SelectedIndex < groups.Count)
            return groups[_comboGroups.SelectedIndex];
        return groups[0];
    }

    private void Reload()
    {
        if (_loader is null) return;
        _panCards.Children.Clear();
        _cards.Clear();
        _itemList.Clear();
        _selectedItems.Clear();
        SetAllSelected(false);
        _cardEmpty.IsVisible = false;
        _hintGetFail.IsVisible = false;
        _searchBox.Text = "";
        _autoRetried = false;
        PageLoaderRestart(0);
    }

    private void ShowManageMenu()
    {
        var items = new List<MyMenuItem>();

        AddItem("Download.Comp.Favorites.Menu.Share", "lucide/share-2", ShareCurrentFolder);
        AddItem(
            "Download.Comp.Favorites.Menu.Import",
            "lucide/circle-plus",
            () => _ = ImportFavoritesAsync());
        AddItem(
            "Download.Comp.Favorites.Menu.New",
            "lucide/folder-plus",
            () => _ = NewFolderAsync());
        AddItem(
            "Download.Comp.Favorites.Menu.Rename",
            "lucide/pencil",
            () => _ = RenameFolderAsync());
        AddItem(
            "Download.Comp.Favorites.Menu.Delete",
            "lucide/trash-2",
            () => _ = DeleteFolderAsync());

        var menu = new ContextMenu
        {
            ItemsSource = items
        };
        menu.Open(_btnManage);
        return;

        void AddItem(string headerKey, string icon, Action action)
        {
            var item = new MyMenuItem
            {
                Header = Lang.Text(headerKey),
                SvgIcon = icon
            };
            item.Click += (_, _) => action();
            items.Add(item);
        }
    }

    private void ShareCurrentFolder()
    {
        try
        {
            var group = GetSelectedGroup();
            if (group is null || group.Favs.Count == 0)
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.NothingShared")));
                return;
            }

            PlatformServices.Clipboard?.SetText(Comp.CompFavorites.GetShareCode(group.Favs));
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompFavorites] 分享收藏时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async Task NewFolderAsync()
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Menu.New"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"),
                    HighLight = false
                });
            var name = result.IsConfirmed ? result.InputText?.Trim() : null;
            if (string.IsNullOrEmpty(name)) return;
            Comp.CompFavorites.FavoritesList?.Add(Comp.CompFavorites.GetNewFav(name, null));
            Comp.CompFavorites.Save();
            RefreshGroupCombo();
            _comboGroups.SelectedIndex = _comboGroups.ItemCount - 1;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "新建收藏夹时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async Task RenameFolderAsync()
    {
        var group = GetSelectedGroup();
        if (group is null) return;
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Rename.Title"),
                    DefaultValue = group.Name,
                    HighLight = false
                });
            var newName = result.IsConfirmed ? result.InputText?.Trim() : null;
            if (string.IsNullOrEmpty(newName) || newName == group.Name) return;
            group.Name = newName;
            Comp.CompFavorites.Save();
            RefreshGroupCombo();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "重命名收藏夹时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async Task DeleteFolderAsync()
    {
        var group = GetSelectedGroup();
        var groups = Comp.CompFavorites.FavoritesList;
        if (group is null || groups is null) return;
        try
        {
            if (groups.Count == 1)
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.LastCollection")));
                return;
            }

            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Delete.Title"),
                    Content = Lang.Text(
                        "Download.Comp.Favorites.Dialog.Delete.Confirm",
                        group.Name,
                        group.Favs.Count,
                        group.Id),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Common.Option.No")),
                        DialogButton.Create(Lang.Text("Common.Option.Yes")),
                        DialogButton.Create(Lang.Text("Common.Option.No"))),
                    IsWarn = true
                });
            if (result.Button != 2) return;
            groups.Remove(group);
            Comp.CompFavorites.Save();
            HintService.Show(Hints.Success(Lang.Text("Download.Comp.Favorites.Hint.Deleted")));
            RefreshGroupCombo();
            _comboGroups.SelectedIndex = 0;
            Reload();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "删除收藏夹时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async Task ImportFavoritesAsync()
    {
        try
        {
            var codeResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Import.Input"),
                    HintText = Lang.Text("Download.Comp.Favorites.Dialog.Import.Hint"),
                    HighLight = false
                });
            var code = codeResult.IsConfirmed ? codeResult.InputText : null;
            if (string.IsNullOrWhiteSpace(code)) return;
            var ids = Comp.CompFavorites.GetIdsByShareCode(code);
            if (ids.Count == 0)
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.NothingShared")));
                return;
            }

            var typeResult = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.Import.Type"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(
                            Lang.Text("Download.Comp.Favorites.Dialog.Import.NewFolder")),
                        DialogButton.Create(
                            Lang.Text("Download.Comp.Favorites.Dialog.Import.CurrentFolder")))
                });
            switch (typeResult.Button)
            {
                case 1:
                {
                    var nameResult = await DialogService.Instance.ShowAsync(
                        new InputDialogRequest
                        {
                            Title = Lang.Text("Download.Comp.Favorites.Dialog.Import.New"),
                            Content = Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"),
                            HighLight = false
                        });
                    var name = nameResult.IsConfirmed ? nameResult.InputText?.Trim() : null;
                    if (string.IsNullOrEmpty(name)) return;
                    Comp.CompFavorites.FavoritesList?.Add(Comp.CompFavorites.GetNewFav(name, ids));
                    Comp.CompFavorites.Save();
                    RefreshGroupCombo();
                    _comboGroups.SelectedIndex = _comboGroupItemCountMinusOne();
                    break;
                }
                case 2:
                {
                    var group = GetSelectedGroup();
                    if (group is null) return;
                    foreach (var id in ids)
                        group.Favs.Add(id);
                    Comp.CompFavorites.Save();
                    Reload();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "解析分享数据失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private int _comboGroupItemCountMinusOne()
    {
        return Math.Max(0, _comboGroups.ItemCount - 1);
    }

    #endregion

    #region 列表

    private void RenderProjects()
    {
        try
        {
            _itemList.Clear();
            _selectedItems.Clear();
            _panCards.Children.Clear();
            _cards.Clear();
            var projects = _loader?.output ?? [];
            var group = GetSelectedGroup();
            _hintGetFail.IsVisible =
                group is not null && projects.Count < group.Favs.Count;
            _panCards.Margin = new Thickness(0d);

            var searchText = _searchBox.Text.Trim();
            var isSearching = searchText.Length > 0;
            if (isSearching)
            {
                var entries = new List<SearchService.SearchEntry<Comp.CompProject>>();
                foreach (var project in projects)
                {
                    var sources = new List<SearchService.SearchSource>
                    {
                        new(project.RawName)
                    };
                    if (!string.IsNullOrEmpty(project.Description))
                        sources.Add(new SearchService.SearchSource(project.Description, 0.4d));
                    if ((project.TranslatedName ?? "") != (project.RawName ?? ""))
                        sources.Add(new SearchService.SearchSource(project.TranslatedName));
                    sources.Add(
                        new SearchService.SearchSource(
                            string.Join("", project.Tags ?? []),
                            0.2d));
                    entries.Add(
                        new SearchService.SearchEntry<Comp.CompProject>
                        {
                            item = project,
                            searchSource = sources
                        });
                }

                projects =
                [
                    .. SearchService
                        .Search(entries, searchText, SearchService.MaxLocalSearchDepth, 0.35d)
                        .Select(result => result.item)
                        .OfType<Comp.CompProject>()
                ];
            }

            var hasAnyFavorites = (_loader?.output?.Count ?? 0) > 0;
            _searchBox.IsVisible = hasAnyFavorites || isSearching;
            _cardEmpty.IsVisible = projects.Count == 0 && !isSearching;
            _cardEmpty.Title = Lang.Text("Download.Comp.Favorites.Empty.Title");

            foreach (var project in projects)
                GetCategoryCard(isSearching ? -1 : (int)project.Type)
                    .List.Children.Add(BuildItem(project));

            foreach (var card in _cards.Values.Where(card => card.List.Children.Count != 0))
            {
                card.Card.Title = card.TitleFormat(card.List.Children.Count);
                _panCards.Children.Add(card.Card);
            }

            if (isSearching && projects.Count == 0)
                _panCards.Children.Add(
                    new TextBlock
                    {
                        Text = Lang.Text("Download.Comp.List.NoResults"),
                        Margin = new Thickness(4d, 6d, 4d, 6d)
                    });

            RefreshSelectionBar();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "可视化收藏夹列表失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private CategoryCard GetCategoryCard(int type)
    {
        if (_cards.TryGetValue(type, out var cached))
            return cached;

        var list = new StackPanel
        {
            Margin = new Thickness(12d, MyCard.SwapedHeight, 12d, 12d)
        };
        var card = new MyCard
        {
            CanSwap = true,
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        card.Children.Add(list);
        var result = new CategoryCard
        {
            Card = card,
            List = list,
            TitleFormat = GetCategoryTitleFormat(type)
        };
        _cards[type] = result;
        return result;
    }

    private MyListItem BuildItem(Comp.CompProject project)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.CheckBox,
            Title = project.TranslatedName,
            Info = (project.Description ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty),
            Logo = string.IsNullOrEmpty(project.LogoUrl)
                ? $"{LauncherPaths.ImageRoot}Icons/NoIcon.png"
                : project.LogoUrl,
            LogoCornerRadius = new CornerRadius(6d),
            Tags = project.Tags,
            tag = project
        };

        var group = GetSelectedGroup();
        if (group is not null
            && group.Notes.TryGetValue(project.Id, out var note)
            && !string.IsNullOrWhiteSpace(note))
            item.Inlines.Add(
                new Run
                {
                    Text = $" ({note})",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148))
                });

        var editNoteButton = new MyIconButton
        {
            SvgIcon = "lucide/pencil"
        };
        ToolTip.SetTip(editNoteButton, Lang.Text("Download.Comp.Favorites.EditNote"));
        editNoteButton.Click += (_, _) => _ = EditNoteAsync(project);

        var unfavoriteButton = new MyIconButton
        {
            SvgIcon = "lucide/heart-filled"
        };
        ToolTip.SetTip(unfavoriteButton, Lang.Text("Download.Comp.Favorites.Action.Unfavorite"));
        unfavoriteButton.Click += (_, _) =>
        {
            Unfavorite(project);
            RefreshAfterRemoval();
        };
        item.Buttons = [editNoteButton, unfavoriteButton];

        item.Changed += OnItemChanged;
        item.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
                OnProjectClicked(project);
        };

        _itemList.Add((item, project));
        return item;
    }

    private void OnItemChanged(object sender, RouteEventArgs e)
    {
        if (sender is not MyListItem item) return;
        _selectedItems.Remove(item);
        if (item.Checked)
            _selectedItems.Add(item);
        RefreshSelectionBar();
    }

    private void RefreshSelectionBar()
    {
        var count = _selectedItems.Count;
        _cardSelect.IsVisible = count > 0;
        _panCards.Margin = new Thickness(0d, 0d, 0d, count > 0 ? 80d : 0d);
        if (count > 0)
            _labSelected.Text = Lang.Text("Download.Comp.Favorites.Hint.SelectedCount", count);
        _btnDownload.IsEnabled = count > 0;
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.A)
            return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        SetAllSelected(true);
        e.Handled = true;
    }

    private void ShowGetFailedDialog()
    {
        var group = GetSelectedGroup();
        if (group is null) return;
        var loadedIds = (_loader?.output ?? [])
            .Select(project => project.Id)
            .ToHashSet();
        var failedIds = group.Favs.Where(id => !loadedIds.Contains(id)).Distinct().ToList();
        if (failedIds.Count == 0) return;

        var content = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Content")
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, failedIds.Select(id => $" - {id}"));
        _ = DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Title"),
                Content = content,
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Common.Action.Confirm")),
                    DialogButton.Create(
                        Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.CopyIds"),
                        () => PlatformServices.Clipboard?.SetText(
                            string.Join(Environment.NewLine, failedIds))),
                    DialogButton.Create(
                        Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Remove"),
                        () =>
                        {
                            foreach (var id in failedIds)
                                group.Favs.Remove(id);
                            Comp.CompFavorites.Save();
                            HintService.Show(
                                Hints.Success(
                                    Lang.Text("Download.Comp.Favorites.Hint.Removed")));
                            Reload();
                        })),
                ForceWait = true
            });
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var entry in _itemList)
            entry.Item.Checked = selected;
        _selectedItems.Clear();
        if (selected)
            foreach (var entry in _itemList)
                _selectedItems.Add(entry.Item);
        RefreshSelectionBar();
    }

    private async Task EditNoteAsync(Comp.CompProject project)
    {
        var group = GetSelectedGroup();
        if (group is null) return;
        try
        {
            group.Notes.TryGetValue(project.Id, out var note);
            var result = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.EditNote"),
                    DefaultValue = note ?? ""
                });
            if (!result.IsConfirmed) return;
            group.Notes[project.Id] = result.InputText ?? "";
            Comp.CompFavorites.Save();
            RenderProjects();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompFavorites] 修改备注时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private void Unfavorite(Comp.CompProject project)
    {
        var group = GetSelectedGroup();
        if (group is null) return;
        group.Favs.Remove(project.Id);
        Comp.CompFavorites.Save();
    }

    private void RefreshAfterRemoval()
    {
        var group = GetSelectedGroup();
        if (group is null || group.Favs.Count == 0)
            Reload();
        else
            _loader?.Start(0, true);
    }

    private static void OnProjectClicked(Comp.CompProject project)
    {
        try
        {
            PageRouter.Navigate(
                PageType.CompDetail,
                PageSubType.Default,
                new CompDetailParameter(
                    project,
                    project.Type,
                    null,
                    Comp.CompLoaderType.Any));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开收藏资源详情失败");
        }
    }

    private static Func<int, string> GetCategoryTitleFormat(int type)
    {
        if (type == -1)
            return count => Lang.Text("Download.Comp.Favorites.SearchResults.Title", count);
        var typeName = type switch
        {
            (int)Comp.CompType.Mod => Lang.Text("Download.Comp.Type.Mod"),
            (int)Comp.CompType.ModPack => Lang.Text("Download.Comp.Type.Modpack"),
            (int)Comp.CompType.ResourcePack => Lang.Text("Download.Comp.Type.ResourcePack"),
            (int)Comp.CompType.Shader => Lang.Text("Download.Comp.Type.Shader"),
            (int)Comp.CompType.DataPack => Lang.Text("Download.Comp.Type.DataPack"),
            (int)Comp.CompType.Plugin => Lang.Text("Download.Comp.Type.Plugin"),
            (int)Comp.CompType.World => Lang.Text("Download.Comp.Type.World"),
            _ => Lang.Text("Download.Comp.Favorites.UnknownType")
        };
        return count => $"{typeName} ({count})";
    }

    #endregion

    #region 选择操作

    private void OnCancelSelectedClicked()
    {
        foreach (var item in _selectedItems.ToList())
            if (item.tag is Comp.CompProject project)
                Unfavorite(project);
        _selectedItems.Clear();
        RefreshAfterRemoval();
    }

    private void OnShareSelectedClicked()
    {
        try
        {
            var ids = _selectedItems
                .Select(item => item.tag)
                .OfType<Comp.CompProject>()
                .Select(project => project.Id)
                .ToHashSet();
            if (ids.Count == 0) return;
            PlatformServices.Clipboard?.SetText(Comp.CompFavorites.GetShareCode(ids));
            SetAllSelected(false);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompFavorites] 分享收藏时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async Task BatchDownloadAsync()
    {
        var projects = _selectedItems
            .Select(item => item.tag)
            .OfType<Comp.CompProject>()
            .ToList();
        if (projects.Count == 0) return;

        _btnDownload.IsEnabled = false;
        try
        {
            var confirm = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Title"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Content"),
                    Buttons = DialogButtons.ContinueCancel,
                    IsWarn = true
                });
            if (!confirm.IsConfirmed) return;

            var hasMod = projects.Any(project => project.Type == Comp.CompType.Mod);
            var supportedLoaders = new List<Comp.CompLoaderType>();
            var first = true;
            foreach (var project in projects.Where(project => project.Type == Comp.CompType.Mod))
                if (first)
                {
                    supportedLoaders = [.. project.ModLoaders];
                    first = false;
                }
                else
                {
                    supportedLoaders = [.. supportedLoaders.Intersect(project.ModLoaders)];
                }

            if (hasMod && supportedLoaders.Count == 0)
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.SelectLoader")));
                return;
            }

            var desiredLoader = Comp.CompLoaderType.Any;
            if (hasMod && supportedLoaders.Count > 0)
            {
                var loaderChoice = await DialogService.Instance.ShowAsync(
                    new SelectDialogRequest
                    {
                        Title = Lang.Text("Download.Comp.Favorites.Dialog.SelectLoader.Title"),
                        Options =
                        [
                            .. supportedLoaders
                                .Select(loader => new DialogOption
                                {
                                    Text = loader.ToString()
                                })
                        ],
                        Buttons = DialogButtons.OkCancel,
                        HighLight = false
                    });
                if (!loaderChoice.IsConfirmed || loaderChoice.SelectedIndex is null) return;
                desiredLoader = supportedLoaders[loaderChoice.SelectedIndex.Value];
            }

            HintService.Show(Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.LoadingVersions")));
            var loader = new Loading.LoaderCombo<List<string>>(
                Lang.Text(
                    "Download.Comp.Favorites.LoaderName.BatchDownload",
                    LauncherCommon.GetUuid()),
                [
                    new Loading.LoaderTask<List<string>, List<DownloadFile>>(
                        Lang.Text("Download.Comp.Favorites.LoaderName.QueryInfo"),
                        task => task.output = BuildBatchDownloadFiles(
                            task,
                            projects,
                            desiredLoader))
                    {
                        ProgressWeight = 2d
                    },
                    new LoaderDownload(
                        Lang.Text("Download.Comp.Favorites.LoaderName.BatchDownloadSuitable"),
                        [])
                    {
                        ProgressWeight = 8d
                    }
                ]);
            loader.OnStateChangedUi += (sender, newState, _) =>
            {
                switch (newState)
                {
                    case LoadState.Finished:
                        HintService.Show(Hints.Success(Lang.Text("Common.Status.Success")));
                        break;
                    case LoadState.Failed:
                        HintService.Show(
                            Hints.Error(
                                sender.Error?.Message
                                ?? Lang.Text("Download.Comp.Favorites.Error.OperationFailed")));
                        break;
                    case LoadState.Aborted:
                        HintService.Show(Hints.Info(Lang.Text("Common.Status.Cancelled")));
                        break;
                }
            };
            loader.Start(new List<string>(projects.Select(project => project.Id)));
            Loading.LoaderTaskbarAdd(loader);
            SetAllSelected(false);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "批量下载收藏时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
        finally
        {
            _btnDownload.IsEnabled = _selectedItems.Count > 0;
        }
    }

    /// <summary>在后台为所选收藏计算共同可下载版本并生成下载项；需要选择版本/目录时回到 UI 线程弹窗。</summary>
    private static List<DownloadFile> BuildBatchDownloadFiles(
        Loading.LoaderTask<List<string>, List<DownloadFile>> task,
        List<Comp.CompProject> projects,
        Comp.CompLoaderType desiredLoader)
    {
        var projectFiles = new Dictionary<Comp.CompProject, List<Comp.CompFile>>();
        List<string>? commonVersions = null;
        foreach (var project in projects)
        {
            List<Comp.CompFile> files;
            try
            {
                files = Comp.CompFilesGet(project.Id, project.FromCurseForge);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    $"获取 {project.TranslatedName} 的下载信息失败",
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
                continue;
            }

            files = project.Type == Comp.CompType.Mod
                ?
                [
                    .. files.Where(file => desiredLoader == Comp.CompLoaderType.Any
                        || file.ModLoaders.Contains(desiredLoader))
                ]
                : files;
            projectFiles[project] = files;

            var versions = files.SelectMany(file => file.GameVersions).Distinct().ToList();
            commonVersions = commonVersions is null
                ? versions
                : [.. commonVersions.Intersect(versions)];
            if (commonVersions.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.NoResource")));
                task.Abort();
                return [];
            }
        }

        if (commonVersions is null || commonVersions.Count == 0 || projectFiles.Count == 0)
        {
            HintService.Show(Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.NoResource")));
            task.Abort();
            return [];
        }

        var versionResult = LauncherCommon.RunInUiWait(() =>
            DialogService
                .Instance.ShowAsync(
                    new SelectDialogRequest
                    {
                        Title = Lang.Text("Download.Comp.Favorites.Dialog.SelectVersion.Title"),
                        Options =
                        [
                            .. commonVersions.Select(version => new DialogOption
                            {
                                Text = version
                            })
                        ],
                        Buttons = DialogButtons.OkCancel,
                        HighLight = false
                    })
                .GetAwaiter()
                .GetResult());
        if (versionResult is null
            || !versionResult.IsConfirmed
            || versionResult.SelectedIndex is null)
        {
            task.Abort();
            return [];
        }

        var selectedVersion = commonVersions[versionResult.SelectedIndex.Value];
        var saveFolder = LauncherCommon.RunInUiWait(() => SystemDialogs.SelectFolder());
        if (string.IsNullOrWhiteSpace(saveFolder))
        {
            task.Abort();
            return [];
        }

        var result = new List<DownloadFile>();
        foreach (var (project, files) in projectFiles)
        {
            var file = files
                .Where(candidate => candidate.GameVersions.Contains(selectedVersion))
                .OrderByDescending(candidate => candidate.ReleaseDate)
                .FirstOrDefault();
            if (file is null) continue;
            var fileName = Comp.CompFileNameGet(project, file);
            result.Add(file.ToNetFile(Path.Combine(saveFolder, fileName)));
        }

        return result;
    }

    #endregion
}