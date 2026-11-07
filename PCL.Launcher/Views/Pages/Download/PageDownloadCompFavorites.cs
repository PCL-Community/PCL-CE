using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.Controls.Common;

namespace PCL.Views.Pages.Download;

public sealed partial class PageDownloadCompFavorites : MyPageRight, IPageView, IRefreshable
{
    private const string SelectBarAnimationKey = "CompFavorites Sidebar";

    private int _bottomBarShownCount;
    private bool _inited;
    private bool _refreshed;
    private bool _updatingGroups;

    public PageDownloadCompFavorites()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;

        PanSearchBox.Search += (_, _) => ApplySearch();
        HintGetFail.Cursor = new Cursor(StandardCursorType.Hand);
        Btn_ManageTargetFav.Click += (_, _) => ShowManageMenu();

        ViewModel.StateChanged += ApplyLoadState;
        ViewModel.FoldersChanged += RefreshGroupCombo;
        ViewModel.SelectionChanged += RefreshSelectionBar;
        ViewModel.SearchReset += () => PanSearchBox.Text = string.Empty;

        KeyDown += OnPageKeyDown;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        AttachedToVisualTree += (_, _) => EnsureInitialized();
    }

    public PageDownloadCompFavoritesViewModel ViewModel { get; } = new();

    private TranslateTransform SelectTransform => (TranslateTransform)CardSelect.RenderTransform!;

    public override void OnNavigatedTo(object? parameter)
    {
        EnsureInitialized();
        ViewModel.RefreshFolders();
        if (_refreshed)
            return;
        _refreshed = true;
        ViewModel.Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        EnsureInitialized();
        ViewModel.RefreshFolders();
        _refreshed = true;
        ViewModel.Reload();
    }

    private void EnsureInitialized()
    {
        if (_inited)
            return;
        _inited = true;
        var loader = ViewModel.EnsureLoader();
        PageLoaderInit(
            Load,
            PanLoad,
            PanContent,
            null,
            loader,
            autoRun: false);
        Load.Click += (_, _) =>
        {
            if (ViewModel.IsLoadFailed)
                ViewModel.Reload();
        };
        RefreshGroupCombo();
    }

    private void SearchRun(object? sender, EventArgs e)
    {
        if (!_inited)
            return;
        ViewModel.SearchText = PanSearchBox.Text;
        ViewModel.RebuildGroups();
    }

    private void ApplySearch()
    {
        SearchRun(PanSearchBox, EventArgs.Empty);
    }

    private void RefreshGroupCombo()
    {
        _updatingGroups = true;
        try
        {
            ComboTargetFav.Items.Clear();
            foreach (var folder in ViewModel.Folders)
                ComboTargetFav.Items.Add(
                    new MyComboBoxItem
                    {
                        Content = folder.Name,
                        Tag = folder.Id
                    });
            if (ComboTargetFav.ItemCount > 0)
                ComboTargetFav.SelectedIndex = Math.Clamp(
                    ViewModel.SelectedFolderIndex,
                    0,
                    ComboTargetFav.ItemCount - 1);
        }
        finally
        {
            _updatingGroups = false;
        }
    }

    private void ComboTargetFav_Selected(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingGroups)
            return;
        var index = ComboTargetFav.SelectedIndex;
        if (index < 0 || index == ViewModel.SelectedFolderIndex)
            return;
        ViewModel.SelectFolder(index);
    }

    private void HintGetFail_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        ViewModel.ShowGetFailedCommand.Execute(null);
        e.Handled = true;
    }

    private void ApplyLoadState()
    {
        var busy = ViewModel.IsLoading || ViewModel.IsLoadFailed;
        PanLoad.IsVisible = busy;
        PanContent.IsVisible = !busy;
    }

    private void OnLanguageChanged()
    {
        if (!_inited)
            return;
        ViewModel.RebuildGroups();
    }

    private void FavoriteItem_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not MyListItem { DataContext: FavoriteItemViewModel item } listItem)
            return;
        if (listItem.Tag is not null)
            return;

        listItem.Tag = item.Project;
        var noteRun = new Run
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148)),
            Text = item.NoteText
        };
        listItem.Inlines.Add(noteRun);
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FavoriteItemViewModel.NoteText))
                noteRun.Text = item.NoteText;
        };

        var editNoteButton = new MyIconButton
        {
            SvgIcon = "lucide/pencil"
        };
        ToolTip.SetTip(editNoteButton, Lang.Text("Download.Comp.Favorites.EditNote"));
        editNoteButton.Click += (_, _) => ViewModel.EditNoteCommand.Execute(item.Project);

        var unfavoriteButton = new MyIconButton
        {
            SvgIcon = "lucide/heart-filled"
        };
        ToolTip.SetTip(
            unfavoriteButton,
            Lang.Text("Download.Comp.Favorites.Action.Unfavorite"));
        unfavoriteButton.Click += (_, _) => ViewModel.RemoveFavorite(item);
        listItem.Buttons = [editNoteButton, unfavoriteButton];

        listItem.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right)
                return;
            OnProjectClicked(item.Project);
            e.Handled = true;
        };
    }

    private void RefreshSelectionBar()
    {
        var count = ViewModel.SelectedItems.Count;
        var selected = count > 0;

        PanContentList.Margin = new Thickness(
            0d,
            0d,
            0d,
            ViewModel.ContentListBottomInset);

        if (Animations.AniExitsAnyControl())
        {
            Animations.AniStop(SelectBarAnimationKey);
            _bottomBarShownCount = count;
            if (selected)
            {
                CardSelect.IsVisible = true;
                CardSelect.Opacity = 1d;
                SelectTransform.Y = -25d;
            }
            else
            {
                CardSelect.IsVisible = false;
                CardSelect.Opacity = 0d;
                SelectTransform.Y = -10d;
            }

            return;
        }

        if (selected)
        {
            if (_bottomBarShownCount >= count)
            {
                _bottomBarShownCount = count;
                return;
            }

            _bottomBarShownCount = count;
            CardSelect.IsVisible = true;
            Animations.AniStart(
                new[]
                {
                    Animations.AaOpacity(CardSelect, 1d - CardSelect.Opacity, 60),
                    Animations.AaTranslateY(
                        CardSelect,
                        -27d - SelectTransform.Y,
                        120,
                        ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)),
                    Animations.AaTranslateY(
                        CardSelect,
                        3d,
                        150,
                        120,
                        new Animations.AniEaseInoutFluent(Animations.AniEasePower.Weak)),
                    Animations.AaTranslateY(
                        CardSelect,
                        -1d,
                        90,
                        270,
                        new Animations.AniEaseInoutFluent(Animations.AniEasePower.Weak))
                },
                SelectBarAnimationKey);
            return;
        }

        if (_bottomBarShownCount == 0)
            return;
        _bottomBarShownCount = 0;
        Animations.AniStart(
            new[]
            {
                Animations.AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                Animations.AaTranslateY(
                    CardSelect,
                    -10d - SelectTransform.Y,
                    90,
                    ease: new Animations.AniEaseInFluent(Animations.AniEasePower.Weak)),
                Animations.AaCode(() => CardSelect.IsVisible = false, after: true)
            },
            SelectBarAnimationKey);
    }

    private void Btn_FavoritesCancel_Clicked(object? sender, RouteEventArgs e)
    {
        ViewModel.CancelSelectedCommand.Execute(null);
    }

    private void Btn_FavoritesShare_Clicked(object? sender, RouteEventArgs e)
    {
        ViewModel.ShareSelectedCommand.Execute(null);
    }

    private void Btn_FavoritesDownload_Clicked(object? sender, RouteEventArgs e)
    {
        ViewModel.BatchDownloadCommand.Execute(null);
    }

    private void Btn_SelectCancel_Clicked(object? sender, RouteEventArgs e)
    {
        ViewModel.ClearSelectedItemsCommand.Execute(null);
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
        menu.Open(Btn_ManageTargetFav);
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
}