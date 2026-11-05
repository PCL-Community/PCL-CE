using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using PCL.Views.Pages.Download;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例总览页：顶部展示实例版本条目，随后是实例信息、个性化、文件夹快捷方式与管理动作五张卡片。
///     个性化卡直接读写实例配置（<see cref="States.Instance" />）；管理卡接入启动、文件补全、核心修补与回收站删除等既有业务。
/// </summary>
public class PageInstanceOverall : MyPageRight
{
    private readonly StackPanel _panMain;
    private MyButton _btnDisplayDesc = null!;
    private MyButton _btnDisplayRename = null!;
    private MyButton _btnDisplayStar = null!;
    private MyButton _btnFolderMods = null!;
    private MyButton _btnManageCheck = null!;
    private MyButton _btnManageDelete = null!;
    private MyButton _btnManagePatch = null!;
    private MyButton _btnManageRestore = null!;
    private MyButton _btnManageScript = null!;
    private MyButton _btnManageTest = null!;
    private MyComboBox _comboDisplayLogo = null!;
    private MyComboBox _comboDisplayType = null!;
    private MyLoadingStateSimulator? _infoSimulator;
    private bool _isLoad;
    private MyComboBoxItem _itemDisplayLogoCustom = null!;
    private MyLoading _labInfoLoading = null!;
    private Grid _panDisplayItem = null!;
    private WrapPanel _panInfo = null!;
    private StackPanel _panModpack = null!;
    private bool _syncing;

    public PageInstanceOverall()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 10d, 25d, 0d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        Child = scroll;
        BuildUi();
        ViewModel.InstanceChanged += Reload;
    }

    public PageInstanceOverallViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        ViewModel.SetInstance(InstancePageHelper.ResolveInstance(parameter));
        RefreshOnNavigate();
    }

    protected override void RefreshLocalization()
    {
        var offset = PanScroll?.Offset.Y ?? 0d;
        BuildUi();
        RefreshOnNavigate();
        Dispatcher.UIThread.Post(() =>
        {
            PanScroll?.Offset = new Vector(PanScroll.Offset.X, offset);
        });
    }

    private void RefreshOnNavigate()
    {
        if (ViewModel.Instance is null)
            return;
        Reload();
        if (_isLoad)
            return;
        _isLoad = true;
        _panMain.Children[0].InvalidateMeasure();
    }

    public void Reload()
    {
        var instance = ViewModel.Instance;
        if (instance is null)
            return;

        _syncing = true;
        try
        {
            ViewModel.RefreshDisplay();
            _panDisplayItem.Children.Clear();
            var item = InstanceVersionItem.Create(instance);
            item.IsHitTestVisible = false;
            _panDisplayItem.Children.Add(item);
            _comboDisplayType.SelectedIndex = ViewModel.CardTypeIndex;
            _btnDisplayStar.Text = ViewModel.StarText;
            _btnFolderMods.IsVisible = ViewModel.ModsFolderVisible;
            RefreshIconSelection();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = false;
        }

        GetInstanceInfo();
    }

    #region 界面构建

    private void BuildUi()
    {
        _panMain.Children.Clear();

        var versionCard = NewCard(string.Empty, true);
        _panDisplayItem = new Grid
        {
            Margin = new Thickness(10d, 7d)
        };
        versionCard.BorderChild = _panDisplayItem;
        _panMain.Children.Add(versionCard);

        var infoCard = NewCard("Instance.Overall.Info.Title");
        var infoBody = new StackPanel
        {
            Margin = new Thickness(15d, 40d, 25d, 5d)
        };
        _labInfoLoading = new MyLoading
        {
            Text = Lang.Text("Instance.Overall.Info.Loading"),
            Margin = new Thickness(0d, 0d, 0d, 10d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        infoBody.Children.Add(_labInfoLoading);
        _panModpack = new StackPanel
        {
            IsVisible = false,
            Margin = new Thickness(0d, 0d, 0d, 5d)
        };
        infoBody.Children.Add(_panModpack);
        _panInfo = new WrapPanel
        {
            Margin = new Thickness(0d, -5d, -20d, 7d)
        };
        infoBody.Children.Add(_panInfo);
        infoCard.BorderChild = infoBody;
        _panMain.Children.Add(infoCard);

        var displayCard = NewCard("Instance.Overall.Personalization.Title");
        var displayBody = new StackPanel
        {
            Margin = new Thickness(25d, 40d, 25d, 15d)
        };
        var displayGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("28,9,28")
        };
        var iconLabel = new TextBlock
        {
            Text = Lang.Text("Instance.Overall.Icon.Title"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 0d, 25d, 0d)
        };
        displayGrid.Children.Add(iconLabel);
        _comboDisplayLogo = new MyComboBox();
        Grid.SetColumn(_comboDisplayLogo, 1);
        Grid.SetRow(_comboDisplayLogo, 0);
        displayGrid.Children.Add(_comboDisplayLogo);
        AddLogoItem(_comboDisplayLogo, "Common.Option.Auto", string.Empty);
        _itemDisplayLogoCustom = AddLogoItem(
            _comboDisplayLogo,
            "Common.Option.Customize",
            Path.Combine("PCL", "Logo.png"));
        AddLogoItem(
            _comboDisplayLogo,
            "Instance.Overall.Icon.Cobblestone",
            "Blocks/CobbleStone.png");
        AddLogoItem(
            _comboDisplayLogo,
            "Instance.Overall.Icon.CommandBlock",
            "Blocks/CommandBlock.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.GoldBlock", "Blocks/GoldBlock.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.GrassBlock", "Blocks/Grass.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.DirtPath", "Blocks/GrassPath.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.Anvil", "Blocks/Anvil.png");
        AddLogoItem(
            _comboDisplayLogo,
            "Instance.Overall.Icon.RedstoneBlock",
            "Blocks/RedstoneBlock.png");
        AddLogoItem(
            _comboDisplayLogo,
            "Instance.Overall.Icon.RedstoneLampOn",
            "Blocks/RedstoneLampOn.png");
        AddLogoItem(
            _comboDisplayLogo,
            "Instance.Overall.Icon.RedstoneLampOff",
            "Blocks/RedstoneLampOff.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.Egg", "Blocks/Egg.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.Fabric", "Blocks/Fabric.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.Quilt", "Blocks/Quilt.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.NeoForgeFox", "Blocks/NeoForge.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.Cleanroom", "Blocks/Cleanroom.png");
        _comboDisplayLogo.SelectionChanged += (_, _) => OnLogoChanged();

        var typeLabel = new TextBlock
        {
            Text = Lang.Text("Instance.Overall.Category.Title"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 0d, 25d, 0d)
        };
        Grid.SetRow(typeLabel, 2);
        displayGrid.Children.Add(typeLabel);
        _comboDisplayType = new MyComboBox();
        Grid.SetColumn(_comboDisplayType, 1);
        Grid.SetRow(_comboDisplayType, 2);
        _comboDisplayType.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Common.Option.Auto")
            });
        var hiddenItem = new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Overall.Category.Hidden")
        };
        ToolTip.SetTip(hiddenItem, Lang.Text("Instance.Overall.Category.Hidden.ToolTip"));
        _comboDisplayType.Items.Add(hiddenItem);
        _comboDisplayType.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Overall.Category.Modable")
            });
        _comboDisplayType.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Overall.Category.Regular")
            });
        _comboDisplayType.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Overall.Category.LessUsed")
            });
        _comboDisplayType.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Overall.Category.AprilFools")
            });
        _comboDisplayType.SelectionChanged += (_, _) => _ = OnCardTypeChangedAsync();
        displayGrid.Children.Add(_comboDisplayType);

        var displayButtons = new Grid
        {
            Margin = new Thickness(0d, 15d, 0d, 7d),
            Height = 35d,
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };
        _btnDisplayRename = NewDisplayButton("Instance.Overall.Rename", () => _ = RenameAsync());
        displayButtons.Children.Add(_btnDisplayRename);
        _btnDisplayDesc = NewDisplayButton(
            "Instance.Overall.EditDescription",
            () => _ = EditDescriptionAsync());
        Grid.SetColumn(_btnDisplayDesc, 1);
        displayButtons.Children.Add(_btnDisplayDesc);
        _btnDisplayStar = NewDisplayButton("Instance.Overall.Favorite", () => ToggleStar());
        Grid.SetColumn(_btnDisplayStar, 2);
        displayButtons.Children.Add(_btnDisplayStar);

        displayBody.Children.Add(displayGrid);
        displayBody.Children.Add(displayButtons);
        displayCard.BorderChild = displayBody;
        _panMain.Children.Add(displayCard);

        var folderCard = NewCard("Instance.Overall.Shortcuts.Title");
        var folderBody = new StackPanel
        {
            Margin = new Thickness(25d, 40d, 25d, 15d)
        };
        var folderButtons = new Grid
        {
            Margin = new Thickness(0d, 2d, 0d, 7d),
            Height = 35d,
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };
        folderButtons.Children.Add(
            NewDisplayButton("Instance.Overall.Shortcuts.InstanceFolder", OpenInstanceFolder));
        var savesButton = NewDisplayButton(
            "Instance.Overall.Shortcuts.SavesFolder",
            OpenSavesFolder);
        Grid.SetColumn(savesButton, 1);
        folderButtons.Children.Add(savesButton);
        _btnFolderMods = NewDisplayButton("Instance.Overall.Shortcuts.ModsFolder", OpenModsFolder);
        Grid.SetColumn(_btnFolderMods, 2);
        folderButtons.Children.Add(_btnFolderMods);
        folderBody.Children.Add(folderButtons);
        folderCard.BorderChild = folderBody;
        _panMain.Children.Add(folderCard);

        var manageCard = NewCard("Instance.Overall.Manage.Title");
        var manageBody = new StackPanel
        {
            Margin = new Thickness(25d, 40d, 25d, 15d)
        };
        var manageButtons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, -5d, -20d, 7d)
        };
        _btnManageScript = NewManageButton(
            "Instance.Overall.Manage.ExportLaunchScript",
            ExportLaunchScript);
        manageButtons.Children.Add(_btnManageScript);
        _btnManageTest = NewManageButton("Instance.Overall.Manage.TestGame", TestGame);
        manageButtons.Children.Add(_btnManageTest);
        _btnManageCheck = NewManageButton("Instance.Overall.Manage.RepairFiles", CheckFiles);
        ToolTip.SetTip(_btnManageCheck, Lang.Text("Instance.Overall.Manage.RepairFiles.ToolTip"));
        manageButtons.Children.Add(_btnManageCheck);
        _btnManageRestore = NewManageButton("Common.Action.Reset", () => _ = RestoreAsync());
        ToolTip.SetTip(_btnManageRestore, Lang.Text("Instance.Overall.Manage.Reinstall.ToolTip"));
        manageButtons.Children.Add(_btnManageRestore);
        _btnManageDelete = NewManageButton(
            "Instance.Overall.Manage.Delete",
            null,
            MyButton.ColorState.Red);
        _btnManageDelete.Click +=
            (_, e) => _ = DeleteAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        manageButtons.Children.Add(_btnManageDelete);
        _btnManagePatch = NewManageButton(
            "Instance.Overall.Manage.PatchCore",
            () => _ = PatchCoreAsync());
        manageButtons.Children.Add(_btnManagePatch);
        manageBody.Children.Add(manageButtons);
        manageCard.BorderChild = manageBody;
        _panMain.Children.Add(manageCard);
    }

    private static MyCard NewCard(string titleKey, bool first = false)
    {
        return new MyCard
        {
            Title = titleKey.Length == 0 ? string.Empty : Lang.Text(titleKey),
            Margin = first
                ? new Thickness(0d, 15d, 0d, 15d)
                : new Thickness(0d, 0d, 0d, 15d)
        };
    }

    private static MyComboBoxItem AddLogoItem(MyComboBox combo, string textKey, string tag)
    {
        var item = new MyComboBoxItem
        {
            Content = Lang.Text(textKey),
            Tag = tag
        };
        combo.Items.Add(item);
        return item;
    }

    private static MyButton NewDisplayButton(string textKey, Action onClick)
    {
        var button = new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static MyButton NewManageButton(
        string textKey,
        Action? onClick,
        MyButton.ColorState color = MyButton.ColorState.Normal)
    {
        var button = new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 7d, 20d, 0d),
            Height = 35d,
            ColorType = color
        };
        if (onClick is not null)
            button.Click += (_, _) => onClick();
        return button;
    }

    #endregion

    #region 实例信息

    private void GetInstanceInfo()
    {
        if (ViewModel.Instance is null)
            return;

        _panModpack.Children.Clear();
        _panModpack.IsVisible = false;
        _panInfo.Children.Clear();
        _panInfo.IsVisible = false;
        _labInfoLoading.IsVisible = true;
        _infoSimulator = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
        _labInfoLoading.State = _infoSimulator;
        _ = LoadInstanceInfoAsync();
    }

    private async Task LoadInstanceInfoAsync()
    {
        try
        {
            await ViewModel.LoadInfoAsync();
            if (ViewModel.Instance is null)
                return;

            _panModpack.Children.Clear();
            _panInfo.Children.Clear();
            if (ViewModel.ModpackProject is not null)
            {
                var compItem = new MyCompItem(ViewModel.ModpackProject, false, false);
                _panModpack.Children.Add(compItem);
                _panModpack.Children.Add(new TextBlock());
                _panModpack.IsVisible = true;
            }

            foreach (var row in ViewModel.InfoRows)
                _panInfo.Children.Add(
                    new MyListItem
                    {
                        Title = row.Title,
                        Info = row.Info,
                        Logo = row.Logo,
                        Margin = new Thickness(0d, 0d, 2d, 0d)
                    });
            _panInfo.IsVisible = true;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _labInfoLoading.IsVisible = false;
            _infoSimulator?.LoadingState = LoadingState.Stop;
        }
    }

    #endregion

    #region 个性化

    private void RefreshStar(McInstance instance)
    {
        _btnDisplayStar.Text = ViewModel.StarText;
    }

    private void RefreshIconSelection()
    {
        _syncing = true;
        try
        {
            _comboDisplayLogo.SelectedIndex = 0;
            var logo = ViewModel.LogoTag;
            var customTag = Path.Combine("PCL", "Logo.png");
            foreach (var raw in _comboDisplayLogo.Items)
            {
                if (raw is not MyComboBoxItem item)
                    continue;
                var tag = item.Tag as string ?? string.Empty;
                var matches = ReferenceEquals(item, _itemDisplayLogoCustom)
                    ? ViewModel.IsCustomLogo
                    : !ViewModel.IsCustomLogo && tag == logo;
                if (!matches)
                    continue;
                _comboDisplayLogo.SelectedItem = item;
                break;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private async Task OnCardTypeChangedAsync()
    {
        if (_syncing)
            return;
        var index = _comboDisplayType.SelectedIndex;
        if (index < 0)
            return;

        _comboDisplayType.IsEnabled = false;
        try
        {
            if (!await ViewModel.ApplyCardTypeAsync(index))
            {
                _syncing = true;
                _comboDisplayType.SelectedIndex = ViewModel.CardTypeIndex;
                _syncing = false;
            }
        }
        finally
        {
            _comboDisplayType.IsEnabled = true;
        }
    }

    private void OnLogoChanged()
    {
        if (_syncing)
            return;
        var isCustom = ReferenceEquals(_comboDisplayLogo.SelectedItem, _itemDisplayLogoCustom);
        var tag = (_comboDisplayLogo.SelectedItem as MyComboBoxItem)?.Tag as string ?? string.Empty;
        _ = ApplyLogoAsync(tag, isCustom);
    }

    private async Task ApplyLogoAsync(string tag, bool isCustom)
    {
        if (!await ViewModel.SelectLogoAsync(tag, isCustom))
            RefreshIconSelection();
    }

    private async Task RenameAsync()
    {
        await ViewModel.RenameAsync();
    }

    private async Task EditDescriptionAsync()
    {
        await ViewModel.EditDescriptionAsync();
    }

    private void ToggleStar()
    {
        ViewModel.ToggleStarCommand.Execute(null);
    }

    #endregion

    #region 快捷方式

    private void OpenInstanceFolder()
    {
        ViewModel.OpenInstanceFolderCommand.Execute(null);
    }

    private void OpenSavesFolder()
    {
        ViewModel.OpenSavesFolderCommand.Execute(null);
    }

    private void OpenModsFolder()
    {
        ViewModel.OpenModsFolderCommand.Execute(null);
    }

    #endregion

    #region 管理动作

    private void ExportLaunchScript()
    {
        ViewModel.ExportLaunchScriptCommand.Execute(null);
    }

    private void TestGame()
    {
        ViewModel.TestGameCommand.Execute(null);
    }

    private void CheckFiles()
    {
        ViewModel.CheckFilesCommand.Execute(null);
    }

    private async Task RestoreAsync()
    {
        await ViewModel.RestoreAsync();
    }

    private async Task DeleteAsync(bool permanent)
    {
        await ViewModel.DeleteAsync(permanent);
    }

    private async Task PatchCoreAsync()
    {
        await ViewModel.PatchCoreAsync();
    }

    #endregion
}