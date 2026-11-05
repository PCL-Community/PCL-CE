using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

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
    private readonly MyComboBox _comboGroups;
    private readonly StackPanel _contentStack;
    private readonly MyHint _hintGetFail;
    private readonly Dictionary<FavoriteItemViewModel, MyListItem> _itemList = new();
    private readonly TextBlock _labSelected;
    private readonly MyLoading _load;
    private readonly MyCard _loadCard;
    private readonly StackPanel _panCards;
    private readonly MySearchBox _searchBox;
    private bool _inited;
    private bool _syncingSelection;

    private bool _updatingGroups;

    public PageDownloadCompFavorites()
    {
        _searchBox = new MySearchBox
        {
            HintText = Lang.Text("Download.Comp.Favorites.Search.Hint"),
            SearchButtonVisibility = true
        };
        _searchBox.Search += (_, _) => ApplySearch();
        _searchBox.TextChanged += (_, _) => ApplySearch();

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
            ViewModel.SelectFolder(_comboGroups.SelectedIndex);
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
        _hintGetFail.PointerReleased += (_, _) =>
            ViewModel.ShowGetFailedCommand.Execute(null);

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
            () => ViewModel.CancelSelectedCommand.Execute(null));
        _btnShare = CreateActionButton(
            "lucide/share-2",
            Lang.Text("Download.Comp.Favorites.Share"),
            () => ViewModel.ShareSelectedCommand.Execute(null));
        _btnDownload = CreateActionButton(
            "lucide/download",
            Lang.Text("Download.Comp.Favorites.Download"),
            () => ViewModel.BatchDownloadCommand.Execute(null));
        _btnDeselect = CreateActionButton(
            "lucide/download",
            Lang.Text("Download.Comp.Favorites.Deselect"),
            () => ViewModel.ClearSelectedItemsCommand.Execute(null));
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

        ViewModel.ContentChanged += RenderContent;
        ViewModel.StateChanged += ApplyLoadState;
        ViewModel.FoldersChanged += RefreshGroupCombo;
        ViewModel.SelectionChanged += RefreshSelectionBar;
        ViewModel.SearchReset += () => _searchBox.Text = string.Empty;

        KeyDown += OnPageKeyDown;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        AttachedToVisualTree += (_, _) => EnsureInitialized();
    }

    public PageDownloadCompFavoritesViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        EnsureInitialized();
        ViewModel.RefreshFolders();
        ViewModel.Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        EnsureInitialized();
        ViewModel.RefreshFolders();
        ViewModel.Reload();
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

    private void EnsureInitialized()
    {
        if (_inited) return;
        _inited = true;
        var loader = ViewModel.EnsureLoader();
        PageLoaderInit(
            _load,
            _loadCard,
            _contentStack,
            null,
            loader,
            autoRun: false);
        _load.Click += (_, _) =>
        {
            if (ViewModel.IsLoadFailed)
                ViewModel.Reload();
        };
        RefreshGroupCombo();
    }

    private void ApplySearch()
    {
        ViewModel.SearchText = _searchBox.Text;
        ViewModel.RebuildGroups();
    }

    private void RefreshGroupCombo()
    {
        _updatingGroups = true;
        try
        {
            _comboGroups.Items.Clear();
            foreach (var folder in ViewModel.Folders)
                _comboGroups.Items.Add(
                    new MyComboBoxItem
                    {
                        Content = folder.Name,
                        Tag = folder.Id
                    });
            if (_comboGroups.ItemCount > 0)
                _comboGroups.SelectedIndex = Math.Clamp(
                    ViewModel.SelectedFolderIndex,
                    0,
                    _comboGroups.ItemCount - 1);
        }
        finally
        {
            _updatingGroups = false;
        }
    }

    private void ApplyLoadState()
    {
        var busy = ViewModel.IsLoading || ViewModel.IsLoadFailed;
        _loadCard.IsVisible = busy;
        _contentStack.IsVisible = !busy;
    }

    private void OnLanguageChanged()
    {
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
        ViewModel.RebuildGroups();
    }

    private void RenderContent()
    {
        try
        {
            _panCards.Children.Clear();
            _itemList.Clear();
            var cards = new Dictionary<int, CategoryCard>();
            foreach (var group in ViewModel.Groups)
            {
                if (!group.IsVisible)
                    continue;
                if (!cards.TryGetValue(group.CompType, out var categoryCard))
                {
                    var list = new StackPanel
                    {
                        Margin = new Thickness(12d, MyCard.SwapedHeight, 12d, 12d)
                    };
                    var card = new MyCard
                    {
                        CanSwap = true,
                        Margin = new Thickness(0d, 0d, 0d, 15d),
                        Title = group.Title
                    };
                    card.Children.Add(list);
                    categoryCard = new CategoryCard
                    {
                        Card = card,
                        List = list
                    };
                    cards[group.CompType] = categoryCard;
                }

                categoryCard.Card.Title = group.Title;
                foreach (var item in group.Items)
                    categoryCard.List.Children.Add(BuildItem(item));
                _panCards.Children.Add(categoryCard.Card);
            }

            _hintGetFail.IsVisible = ViewModel.ShowHintGetFail;
            _searchBox.IsVisible = ViewModel.ShowSearchBox;
            _cardEmpty.IsVisible = ViewModel.ShowNoContent;
            _cardEmpty.Title = Lang.Text("Download.Comp.Favorites.Empty.Title");
            if (ViewModel.ShowNoResults)
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

    private MyListItem BuildItem(FavoriteItemViewModel item)
    {
        var project = item.Project;
        var listItem = new MyListItem
        {
            Type = MyListItem.CheckType.CheckBox,
            Title = item.Title,
            Info = item.Info,
            Logo = item.Logo,
            LogoCornerRadius = new CornerRadius(6d),
            Tags = item.Tags,
            Checked = item.IsChecked,
            tag = project
        };

        if (!string.IsNullOrEmpty(item.NoteText))
            listItem.Inlines.Add(
                new Run
                {
                    Text = item.NoteText,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148))
                });

        var editNoteButton = new MyIconButton
        {
            SvgIcon = "lucide/pencil"
        };
        ToolTip.SetTip(editNoteButton, Lang.Text("Download.Comp.Favorites.EditNote"));
        editNoteButton.Click += (_, _) => ViewModel.EditNoteCommand.Execute(project);

        var unfavoriteButton = new MyIconButton
        {
            SvgIcon = "lucide/heart-filled"
        };
        ToolTip.SetTip(
            unfavoriteButton,
            Lang.Text("Download.Comp.Favorites.Action.Unfavorite"));
        unfavoriteButton.Click += (_, _) => ViewModel.RemoveFavorite(item);
        listItem.Buttons = [editNoteButton, unfavoriteButton];

        listItem.Changed += (_, _) =>
        {
            if (_syncingSelection) return;
            item.IsChecked = listItem.Checked;
        };
        listItem.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
                OnProjectClicked(project);
        };

        _itemList[item] = listItem;
        return listItem;
    }

    private void RefreshSelectionBar()
    {
        var selected = ViewModel.HasSelection;
        _cardSelect.IsVisible = selected;
        _panCards.Margin = new Thickness(0d, 0d, 0d, ViewModel.ContentListBottomInset);
        if (selected)
            _labSelected.Text = ViewModel.SelectedText;
        _btnDownload.IsEnabled = ViewModel.IsBulkDownloadEnabled && selected;

        _syncingSelection = true;
        try
        {
            foreach (var (item, control) in _itemList)
                if (control.Checked != item.IsChecked)
                    control.Checked = item.IsChecked;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.A)
            return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        ViewModel.SelectAllItemsCommand.Execute(null);
        e.Handled = true;
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

    private void ShowManageMenu()
    {
        var items = new List<MyMenuItem>();

        AddItem(
            "Download.Comp.Favorites.Menu.Share",
            "lucide/share-2",
            () => ViewModel.ShareCurrentFolderCommand.Execute(null));
        AddItem(
            "Download.Comp.Favorites.Menu.Import",
            "lucide/circle-plus",
            () => ViewModel.ImportFavoritesCommand.Execute(null));
        AddItem(
            "Download.Comp.Favorites.Menu.New",
            "lucide/folder-plus",
            () => ViewModel.NewFolderCommand.Execute(null));
        AddItem(
            "Download.Comp.Favorites.Menu.Rename",
            "lucide/pencil",
            () => ViewModel.RenameFolderCommand.Execute(null));
        AddItem(
            "Download.Comp.Favorites.Menu.Delete",
            "lucide/trash-2",
            () => ViewModel.DeleteFolderCommand.Execute(null));

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

    private sealed class CategoryCard
    {
        public required MyCard Card { get; init; }
        public required StackPanel List { get; init; }
    }
}