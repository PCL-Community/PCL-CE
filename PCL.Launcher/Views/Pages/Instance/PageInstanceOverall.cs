using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Minecraft;
using PCL.Core.Utils.Validate;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例总览页：顶部展示实例版本条目，随后是实例信息、个性化、文件夹快捷方式与管理动作五张卡片。
///     个性化卡直接读写实例配置（<see cref="States.Instance" />）；管理卡接入启动、文件补全、核心修补与回收站删除等既有业务。
/// </summary>
public class PageInstanceOverall : MyPageRight
{
    private const string CustomLogoTag = "__pcl_custom_logo__";

    private readonly StackPanel _panMain;
    private MyLoading _labInfoLoading = null!;
    private Grid _panDisplayItem = null!;
    private StackPanel _panModpack = null!;
    private WrapPanel _panInfo = null!;
    private MyComboBox _comboDisplayLogo = null!;
    private MyComboBoxItem _itemDisplayLogoCustom = null!;
    private MyComboBox _comboDisplayType = null!;
    private MyButton _btnDisplayRename = null!;
    private MyButton _btnDisplayDesc = null!;
    private MyButton _btnDisplayStar = null!;
    private MyButton _btnFolderMods = null!;
    private MyButton _btnManageScript = null!;
    private MyButton _btnManageTest = null!;
    private MyButton _btnManageCheck = null!;
    private MyButton _btnManageRestore = null!;
    private MyButton _btnManageDelete = null!;
    private MyButton _btnManagePatch = null!;
    private MyLoadingStateSimulator? _infoSimulator;
    private McInstance? _instance;
    private bool _isLoad;
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
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        RefreshOnNavigate();
    }

    protected override void RefreshLocalization()
    {
        var offset = PanScroll?.Offset.Y ?? 0d;
        BuildUi();
        RefreshOnNavigate();
        Dispatcher.UIThread.Post(
            () =>
            {
                if (PanScroll is not null)
                    PanScroll.Offset = new Vector(PanScroll.Offset.X, offset);
            });
    }

    private void RefreshOnNavigate()
    {
        if (_instance is null)
            return;
        Reload();
        if (_isLoad)
            return;
        _isLoad = true;
        _panMain.Children[0].InvalidateMeasure();
    }

    public void Reload()
    {
        if (_instance is null)
            return;

        var instance = _instance;
        _syncing = true;
        try
        {
            _panDisplayItem.Children.Clear();
            var item = InstanceVersionItem.Create(instance);
            item.IsHitTestVisible = false;
            _panDisplayItem.Children.Add(item);
            _comboDisplayType.SelectedIndex = Math.Clamp(States.Instance.CardType[instance.PathInstance], 0, 5);
            RefreshStar(instance);
            _btnFolderMods.IsVisible = instance.Modable;
            RefreshIconSelection(instance);
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
        _panDisplayItem = new Grid { Margin = new Thickness(10d, 7d) };
        versionCard.BorderChild = _panDisplayItem;
        _panMain.Children.Add(versionCard);

        var infoCard = NewCard("Instance.Overall.Info.Title");
        var infoBody = new StackPanel { Margin = new Thickness(15d, 40d, 25d, 5d) };
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
        _panInfo = new WrapPanel { Margin = new Thickness(0d, -5d, -20d, 7d) };
        infoBody.Children.Add(_panInfo);
        infoCard.BorderChild = infoBody;
        _panMain.Children.Add(infoCard);

        var displayCard = NewCard("Instance.Overall.Personalization.Title");
        var displayBody = new StackPanel { Margin = new Thickness(25d, 40d, 25d, 15d) };
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
        _itemDisplayLogoCustom = AddLogoItem(_comboDisplayLogo, "Common.Option.Customize", CustomLogoTag);
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.Cobblestone", "Blocks/CobbleStone.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.CommandBlock", "Blocks/CommandBlock.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.GoldBlock", "Blocks/GoldBlock.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.GrassBlock", "Blocks/Grass.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.DirtPath", "Blocks/GrassPath.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.Anvil", "Blocks/Anvil.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.RedstoneBlock", "Blocks/RedstoneBlock.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.RedstoneLampOn", "Blocks/RedstoneLampOn.png");
        AddLogoItem(_comboDisplayLogo, "Instance.Overall.Icon.RedstoneLampOff", "Blocks/RedstoneLampOff.png");
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
        _comboDisplayType.Items.Add(new MyComboBoxItem { Content = Lang.Text("Common.Option.Auto") });
        var hiddenItem = new MyComboBoxItem { Content = Lang.Text("Instance.Overall.Category.Hidden") };
        ToolTip.SetTip(hiddenItem, Lang.Text("Instance.Overall.Category.Hidden.ToolTip"));
        _comboDisplayType.Items.Add(hiddenItem);
        _comboDisplayType.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Overall.Category.Modable") });
        _comboDisplayType.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Overall.Category.Regular") });
        _comboDisplayType.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Overall.Category.LessUsed") });
        _comboDisplayType.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Overall.Category.AprilFools") });
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
        _btnDisplayDesc = NewDisplayButton("Instance.Overall.EditDescription", () => _ = EditDescriptionAsync());
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
        var folderBody = new StackPanel { Margin = new Thickness(25d, 40d, 25d, 15d) };
        var folderButtons = new Grid
        {
            Margin = new Thickness(0d, 2d, 0d, 7d),
            Height = 35d,
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };
        folderButtons.Children.Add(
            NewDisplayButton("Instance.Overall.Shortcuts.InstanceFolder", OpenInstanceFolder));
        var savesButton = NewDisplayButton("Instance.Overall.Shortcuts.SavesFolder", OpenSavesFolder);
        Grid.SetColumn(savesButton, 1);
        folderButtons.Children.Add(savesButton);
        _btnFolderMods = NewDisplayButton("Instance.Overall.Shortcuts.ModsFolder", OpenModsFolder);
        Grid.SetColumn(_btnFolderMods, 2);
        folderButtons.Children.Add(_btnFolderMods);
        folderBody.Children.Add(folderButtons);
        folderCard.BorderChild = folderBody;
        _panMain.Children.Add(folderCard);

        var manageCard = NewCard("Instance.Overall.Manage.Title");
        var manageBody = new StackPanel { Margin = new Thickness(25d, 40d, 25d, 15d) };
        var manageButtons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, -5d, -20d, 7d)
        };
        _btnManageScript = NewManageButton("Instance.Overall.Manage.ExportLaunchScript", ExportLaunchScript);
        manageButtons.Children.Add(_btnManageScript);
        _btnManageTest = NewManageButton("Instance.Overall.Manage.TestGame", TestGame);
        manageButtons.Children.Add(_btnManageTest);
        _btnManageCheck = NewManageButton("Instance.Overall.Manage.RepairFiles", CheckFiles);
        ToolTip.SetTip(_btnManageCheck, Lang.Text("Instance.Overall.Manage.RepairFiles.ToolTip"));
        manageButtons.Children.Add(_btnManageCheck);
        _btnManageRestore = NewManageButton("Common.Action.Reset", () => _ = RestoreAsync());
        ToolTip.SetTip(_btnManageRestore, Lang.Text("Instance.Overall.Manage.Reinstall.ToolTip"));
        manageButtons.Children.Add(_btnManageRestore);
        _btnManageDelete = NewManageButton("Instance.Overall.Manage.Delete", null, MyButton.ColorState.Red);
        _btnManageDelete.Click += (_, e) => _ = DeleteAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        manageButtons.Children.Add(_btnManageDelete);
        _btnManagePatch = NewManageButton("Instance.Overall.Manage.PatchCore", () => _ = PatchCoreAsync());
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
            Margin = first ? new Thickness(0d, 15d, 0d, 15d) : new Thickness(0d, 0d, 0d, 15d)
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
        if (_instance is null)
            return;
        var instance = _instance;

        _panModpack.Children.Clear();
        _panModpack.IsVisible = false;
        _panInfo.Children.Clear();
        _panInfo.IsVisible = false;
        _labInfoLoading.IsVisible = true;
        _infoSimulator = new MyLoadingStateSimulator { LoadingState = LoadingState.Run };
        _labInfoLoading.State = _infoSimulator;

        LauncherCommon.RunInNewThread(
            () =>
            {
                Comp.CompProject? modpackProject = null;
                try
                {
                    var modpackId = States.Instance.ModpackId[instance.PathInstance];
                    if (!string.IsNullOrWhiteSpace(modpackId))
                    {
                        var projects = Comp.CompRequest.GetCompProjectsByIds([modpackId]);
                        if (projects.Count > 0)
                            modpackProject = projects[0];
                    }
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, "加载实例整合包信息失败", LauncherLogLevel.Developer);
                }

                LauncherCommon.RunInUi(
                    () =>
                    {
                        if (!ReferenceEquals(_instance, instance))
                            return;

                        if (modpackProject is not null)
                        {
                            var compItem = new PCL.Views.Pages.Download.MyCompItem(modpackProject, false, false);
                            _panModpack.Children.Add(compItem);
                            _panModpack.Children.Add(new TextBlock());
                            _panModpack.IsVisible = true;
                        }

                        foreach (var row in BuildInfoRows(instance))
                            _panInfo.Children.Add(row);
                        _panInfo.IsVisible = true;
                        _labInfoLoading.IsVisible = false;
                        if (_infoSimulator is not null)
                            _infoSimulator.LoadingState = LoadingState.Stop;
                    });
            },
            "Instance Info Loader",
            ThreadPriority.BelowNormal);
    }

    private static IEnumerable<MyListItem> BuildInfoRows(McInstance instance)
    {
        var instanceInfo = instance.Info;
        var launchCount = States.Instance.LaunchCount[instance.PathInstance];
        yield return CreateInfoRow(
            Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
            launchCount == 0
                ? Lang.Text("Instance.Overall.Info.LaunchCount.Never")
                : Lang.Text("Instance.Overall.Info.LaunchCount.Count", launchCount),
            launchCount == 0 ? "Blocks/RedstoneLampOff.png" : "Blocks/RedstoneLampOn.png");

        if (!string.IsNullOrWhiteSpace(States.Instance.ModpackVersion[instance.PathInstance]))
            yield return CreateInfoRow(
                Lang.Text("Instance.Overall.Info.ModpackVersion"),
                States.Instance.ModpackVersion[instance.PathInstance],
                "Blocks/CommandBlock.png");

        yield return CreateInfoRow(
            "Minecraft",
            McInstanceInfo.GetVanillaNameDisplay(instanceInfo.VanillaName),
            "Blocks/Grass.png");
        if (instanceInfo.HasForge)
            yield return CreateInfoRow("Forge", FormatLoaderVersion(instanceInfo.Forge), "Blocks/Anvil.png");
        if (instanceInfo.HasNeoForge)
            yield return CreateInfoRow("NeoForge", FormatLoaderVersion(instanceInfo.NeoForge), "Blocks/NeoForge.png");
        if (instanceInfo.HasCleanroom)
            yield return CreateInfoRow("Cleanroom", FormatLoaderVersion(instanceInfo.Cleanroom), "Blocks/Cleanroom.png");
        if (instanceInfo.HasFabric)
            yield return CreateInfoRow("Fabric", FormatLoaderVersion(instanceInfo.Fabric), "Blocks/Fabric.png");
        if (instanceInfo.HasQuilt)
            yield return CreateInfoRow("Quilt", FormatLoaderVersion(instanceInfo.Quilt), "Blocks/Quilt.png");
        if (instanceInfo.HasOptiFine)
            yield return CreateInfoRow("OptiFine", FormatLoaderVersion(instanceInfo.OptiFine), "Blocks/GrassPath.png");
        if (instanceInfo.HasLiteLoader)
            yield return CreateInfoRow(
                "LiteLoader",
                Lang.Text("Instance.Overall.Info.Installed"),
                "Blocks/Egg.png");
        if (instanceInfo.HasLegacyFabric)
            yield return CreateInfoRow(
                "Legacy Fabric",
                FormatLoaderVersion(instanceInfo.LegacyFabric),
                "Blocks/Fabric.png");
        if (instanceInfo.HasLabyMod)
            yield return CreateInfoRow("LabyMod", FormatLoaderVersion(instanceInfo.LabyMod), "Blocks/LabyMod.png");
    }

    private static MyListItem CreateInfoRow(string title, string info, string logo)
    {
        return new MyListItem
        {
            Title = title,
            Info = info,
            Logo = LauncherPaths.ImageRoot + logo,
            Margin = new Thickness(0d, 0d, 2d, 0d)
        };
    }

    private static string FormatLoaderVersion(string? version)
    {
        return string.IsNullOrEmpty(version) ? Lang.Text("Minecraft.Version.Unknown") : version;
    }

    #endregion

    #region 个性化

    private void RefreshStar(McInstance instance)
    {
        _btnDisplayStar.Text = instance.IsStar
            ? Lang.Text("Instance.Overall.Unfavorite")
            : Lang.Text("Instance.Overall.Favorite");
    }

    private void RefreshIconSelection(McInstance instance)
    {
        _syncing = true;
        try
        {
            _comboDisplayLogo.SelectedIndex = 0;
            var logo = States.Instance.LogoPath[instance.PathInstance];
            var logoCustom = States.Instance.IsLogoCustom[instance.PathInstance];
            if (!logoCustom)
                return;

            foreach (var raw in _comboDisplayLogo.Items)
            {
                if (raw is not MyComboBoxItem item)
                    continue;
                var tag = item.Tag as string ?? string.Empty;
                var matches = tag == CustomLogoTag
                    ? logo.EndsWith(Path.Combine("PCL", "Logo.png"), StringComparison.OrdinalIgnoreCase)
                    : tag == logo;
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
        if (_syncing || _instance is null)
            return;
        var instance = _instance;
        var path = instance.PathInstance;
        var index = _comboDisplayType.SelectedIndex;
        if (index < 0)
            return;
        var previous = Math.Clamp(States.Instance.CardType[path], 0, 5);

        _syncing = true;
        _comboDisplayType.IsEnabled = false;
        try
        {
            if (index != (int)McInstanceCardType.Hidden)
            {
                States.Instance.CardType[path] = index;
                instance.displayType = (McInstanceCardType)index;
                RefreshInstanceList();
                _syncing = true;
                return;
            }

            if (!States.Hint.HideGameInstance)
            {
                var dialog = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Instance.Overall.Hide.ConfirmTitle"),
                        Content = Lang.Text("Instance.Overall.Hide.ConfirmMessage"),
                        Buttons = DialogButtons.OkCancel,
                        IsWarn = true
                    });
                if (!dialog.IsConfirmed)
                {
                    _syncing = true;
                    _comboDisplayType.SelectedIndex = previous;
                    return;
                }

                States.Hint.HideGameInstance = true;
            }

            States.Instance.CardType[path] = (int)McInstanceCardType.Hidden;
            RefreshInstanceList();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = false;
            _comboDisplayType.IsEnabled = true;
        }
    }

    private void OnLogoChanged()
    {
        if (_syncing || _instance is null)
            return;
        var instance = _instance;
        var path = instance.PathInstance;
        var customLogoFile = Path.Combine(instance.PathInstance, "PCL", "Logo.png");
        string? newLogo;

        try
        {
            if (ReferenceEquals(_comboDisplayLogo.SelectedItem, _itemDisplayLogoCustom))
            {
                var fileName = SystemDialogs.SelectFile(
                    Lang.Text("Instance.Overall.Icon.SelectFile.Filter"),
                    Lang.Text("Instance.Overall.Icon.SelectFile.Title"));
                if (string.IsNullOrEmpty(fileName))
                {
                    RefreshIconSelection(instance);
                    return;
                }

                InstancePageHelper.EnsureDirectory(Path.GetDirectoryName(customLogoFile)!);
                File.Copy(fileName, customLogoFile, true);
                newLogo = Path.Combine("PCL", "Logo.png");
            }
            else
            {
                if (File.Exists(customLogoFile))
                    File.Delete(customLogoFile);
                newLogo = (_comboDisplayLogo.SelectedItem as MyComboBoxItem)?.Tag as string;
                if (newLogo == CustomLogoTag)
                    newLogo = string.Empty;
            }
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
            RefreshIconSelection(instance);
            return;
        }

        try
        {
            States.Instance.LogoPath[path] = newLogo ?? string.Empty;
            States.Instance.IsLogoCustom[path] = !string.IsNullOrEmpty(newLogo);
            var reloaded = new McInstance(instance.Name).Load();
            _instance = reloaded;
            PageInstanceLeft.SetCurrentInstance(reloaded);
            if (InstanceList.McMcInstanceSelected is not null
                && InstanceList.McMcInstanceSelected.PathInstance == reloaded.PathInstance)
                InstanceList.McMcInstanceSelected = reloaded;
            RefreshInstanceList();
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
    }

    private async Task RenameAsync()
    {
        if (_instance is null)
            return;
        _btnDisplayRename.IsEnabled = false;
        try
        {
            var oldName = _instance.Name;
            var oldPath = _instance.PathInstance;
            var versionsFolder = Path.Combine(GameFolders.mcFolderSelected ?? string.Empty, "versions");
            var dialog = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Overall.Name.EditTitle"),
                    DefaultValue = oldName,
                    ValidateRules = [new FolderNameValidator(versionsFolder, ignoreCase: false)],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!dialog.IsConfirmed)
                return;
            var newName = dialog.InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                return;

            var newPath = Path.Combine(versionsFolder, newName);
            var tempName = newName + "_temp";
            var tempPath = Path.Combine(versionsFolder, tempName);
            var isCaseChangedOnly = string.Equals(
                newName.ToLowerInvariant(),
                oldName.ToLowerInvariant(),
                StringComparison.Ordinal);

            JsonObject jsonObject;
            try
            {
                var jsonPath = Path.Combine(oldPath, oldName + ".json");
                jsonObject = JsonNode.Parse(LauncherCommon.ReadFile(jsonPath)) as JsonObject
                             ?? (JsonObject)_instance.JsonObject.DeepClone();
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "重命名读取 Json 时失败", LauncherLogLevel.Developer);
                jsonObject = (JsonObject)_instance.JsonObject.DeepClone();
            }

            Directory.Move(oldPath, tempPath);
            Directory.Move(tempPath, newPath);
            LauncherCommon.IniClearCache(Path.Combine(_instance.PathIndie, "options.txt"));

            var oldNatives = Path.Combine(newPath, oldName + "-natives");
            if (Directory.Exists(oldNatives))
            {
                var newNatives = Path.Combine(newPath, newName + "-natives");
                if (isCaseChangedOnly)
                {
                    var middle = Path.Combine(newPath, oldName + "natives_temp");
                    Directory.Move(oldNatives, middle);
                    Directory.Move(middle, newNatives);
                }
                else
                {
                    if (Directory.Exists(newNatives))
                        Directory.Delete(newNatives, true);
                    Directory.Move(oldNatives, newNatives);
                }
            }

            var oldJar = Path.Combine(newPath, oldName + ".jar");
            if (File.Exists(oldJar))
            {
                var newJar = Path.Combine(newPath, newName + ".jar");
                if (isCaseChangedOnly)
                {
                    var middle = Path.Combine(newPath, oldName + "_temp.jar");
                    File.Move(oldJar, middle);
                    File.Move(middle, newJar);
                }
                else
                {
                    if (File.Exists(newJar))
                        File.Delete(newJar);
                    File.Move(oldJar, newJar);
                }
            }

            var setupIni = Path.Combine(newPath, "PCL", "Setup.ini");
            if (File.Exists(setupIni))
                LauncherCommon.WriteFile(
                    setupIni,
                    LauncherCommon.ReadFile(setupIni).Replace(oldPath, newPath));

            var instanceIni = Path.Combine(GameFolders.mcFolderSelected ?? string.Empty, "PCL.ini");
            if ((LauncherCommon.ReadIni(instanceIni, "Version") ?? string.Empty) == oldName)
                LauncherCommon.WriteIni(instanceIni, "Version", newName);

            try
            {
                jsonObject["id"] = newName;
                LauncherCommon.WriteFile(Path.Combine(newPath, newName + ".json"), jsonObject.ToString());
                if (!isCaseChangedOnly)
                {
                    var oldJson = Path.Combine(newPath, oldName + ".json");
                    if (File.Exists(oldJson))
                        File.Delete(oldJson);
                }
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "重命名实例 Json 失败", LauncherLogLevel.Developer);
            }

            HintService.Show(Hints.Success(Lang.Text("Instance.Overall.Name.RenameSuccess")));
            var reloaded = new McInstance(newName).Load();
            _instance = reloaded;
            PageInstanceLeft.SetCurrentInstance(reloaded);
            if (InstanceList.McMcInstanceSelected is not null
                && InstanceList.McMcInstanceSelected.PathInstance == oldPath)
            {
                InstanceList.McMcInstanceSelected = reloaded;
                States.Game.SelectedInstance = newName;
            }

            RefreshInstanceList();
            PageRouter.Navigate(
                new PageStackData(PageType.InstanceSetup, PageSubType.VersionOverall, reloaded));
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _btnDisplayRename.IsEnabled = true;
        }
    }

    private async Task EditDescriptionAsync()
    {
        if (_instance is null)
            return;
        _btnDisplayDesc.IsEnabled = false;
        try
        {
            var instance = _instance;
            var oldInfo = States.Instance.CustomInfo[instance.PathInstance];
            var dialog = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Instance.Overall.Description.EditTitle"),
                    Content = Lang.Text("Instance.Overall.Description.EditMessage"),
                    DefaultValue = oldInfo ?? string.Empty,
                    HintText = Lang.Text("Instance.Overall.Description.Default"),
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!dialog.IsConfirmed)
                return;
            var newInfo = dialog.InputText ?? string.Empty;
            if ((oldInfo ?? string.Empty) == newInfo)
                return;

            States.Instance.CustomInfo[instance.PathInstance] = newInfo;
            var reloaded = new McInstance(instance.Name).Load();
            _instance = reloaded;
            PageInstanceLeft.SetCurrentInstance(reloaded);
            RefreshInstanceList();
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _btnDisplayDesc.IsEnabled = true;
        }
    }

    private void ToggleStar()
    {
        if (_instance is null)
            return;
        try
        {
            var instance = _instance;
            States.Instance.Starred[instance.PathInstance] = !instance.IsStar;
            var reloaded = new McInstance(instance.Name).Load();
            _instance = reloaded;
            PageInstanceLeft.SetCurrentInstance(reloaded);
            InstanceList.mcInstanceListForceRefresh = true;
            RefreshInstanceList();
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
    }

    #endregion

    #region 快捷方式

    private void OpenInstanceFolder()
    {
        if (_instance is null)
            return;
        InstancePageHelper.OpenFolder(_instance.PathInstance);
    }

    private void OpenSavesFolder()
    {
        if (_instance is null)
            return;
        var path = InstancePageHelper.SavesPath(_instance);
        InstancePageHelper.EnsureDirectory(path);
        InstancePageHelper.OpenFolder(path);
    }

    private void OpenModsFolder()
    {
        if (_instance is null)
            return;
        var path = InstancePageHelper.ModsPath(_instance);
        InstancePageHelper.EnsureDirectory(path);
        InstancePageHelper.OpenFolder(path);
    }

    #endregion

    #region 管理动作

    private void ExportLaunchScript()
    {
        if (_instance is null)
            return;
        try
        {
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Overall.Script.SelectSaveTitle"),
                "启动 " + _instance.Name + ".bat",
                Lang.Text("Instance.Overall.Script.FileFilter"));
            if (string.IsNullOrEmpty(savePath))
                return;
            if (GameLaunch.mcLaunchLoader.State == LoadState.Loading)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Script.WaitForLaunchTask")));
                return;
            }

            if (GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
                {
                    SaveBatch = savePath,
                    instance = _instance
                }))
            {
                HintService.Show(
                    ProfileService.Current?.ProfileType == ProfileType.Offline
                        ? Hints.Info(Lang.Text("Instance.Overall.Script.Exporting"))
                        : Hints.Info(Lang.Text("Instance.Overall.Script.ExportingWarning")));
            }
            else
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Script.ExportFailed")));
            }
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
    }

    private void TestGame()
    {
        if (_instance is null)
            return;
        try
        {
            if (GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
                {
                    instance = _instance,
                    IsTest = true
                }))
                PageRouter.Navigate(PageType.Launch);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
    }

    private void CheckFiles()
    {
        if (_instance is null)
            return;
        try
        {
            var instance = _instance;
            if (Libraries.ShouldIgnoreFileCheck(instance))
            {
                HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Repair.DisableVerificationHint")));
                return;
            }

            var taskName = instance.Name + " " + Lang.Text("Instance.Overall.Repair.TaskName");
            if (Loading.loaderTaskbar.Any(loader => loader.name == taskName))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Repair.Processing")));
                return;
            }

            var loader = new Loading.LoaderCombo<string>(
                taskName,
                GameDownload.DlClientFix(instance, true, GameDownload.AssetsIndexExistsBehaviour.AlwaysDownload))
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                            HintService.Show(
                                Hints.Success(
                                    Lang.Text("Instance.Overall.Repair.Success.WithTaskName", taskName)));
                            break;
                        case LoadState.Failed:
                            HintService.Show(
                                Hints.Error(
                                    Lang.Text(
                                        "Instance.Overall.Repair.Failed.WithDetail",
                                        taskName,
                                        changed.Error?.Message ?? string.Empty)));
                            break;
                        case LoadState.Aborted:
                            HintService.Show(
                                Hints.Info(
                                    Lang.Text("Instance.Overall.Repair.Cancelled.WithTaskName", taskName)));
                            break;
                    }
                }
            };
            loader.Start(instance.Name);
            Loading.LoaderTaskbarAdd(loader);
            LoadingUi.Current?.RefreshDownloadButton();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
    }

    private async Task RestoreAsync()
    {
        if (_instance is null)
            return;
        _btnManageRestore.IsEnabled = false;
        try
        {
            var instance = _instance;
            var currentVersion = instance.Info;
            if (currentVersion.Drop != 99
                && McVersionComparer.CompareVersion(currentVersion.VanillaName, "1.5.2") == -1
                && currentVersion.HasForge)
            {
                HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Reset.NotSupported")));
                return;
            }

            if (currentVersion.HasQuilt)
            {
                HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Reset.QuiltUnsupported")));
                return;
            }

            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Overall.Reset.ConfirmTitle"),
                    Content = Lang.Text("Instance.Overall.Reset.ConfirmMessage", instance.Name),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!dialog.IsConfirmed)
                return;

            // 备份实例核心文件，便于安装失败后还原
            try
            {
                var backupFolder = Path.Combine(instance.PathInstance, "PCLInstallBackups");
                InstancePageHelper.EnsureDirectory(backupFolder);
                var json = Path.Combine(instance.PathInstance, instance.Name + ".json");
                var jar = Path.Combine(instance.PathInstance, instance.Name + ".jar");
                if (File.Exists(json))
                    File.Copy(json, Path.Combine(backupFolder, instance.Name + ".json"), true);
                if (File.Exists(jar))
                    File.Copy(jar, Path.Combine(backupFolder, instance.Name + ".jar"), true);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "备份实例核心文件失败", LauncherLogLevel.Developer);
            }

            var request = new McInstallRequest
            {
                targetInstanceName = instance.Name,
                targetInstanceFolder = instance.PathInstance,
                minecraftName = currentVersion.VanillaName,
                optiFineEntry = currentVersion.HasOptiFine
                    ? new GameDownload.DlOptiFineListEntry
                    {
                        Inherit = currentVersion.VanillaName,
                        DisplayName = currentVersion.VanillaName + " " + currentVersion.OptiFine
                    }
                    : null,
                forgeEntry = currentVersion.HasForge
                    ? new GameDownload.DlForgeVersionEntry(currentVersion.Forge, null, currentVersion.VanillaName)
                    {
                        Category = "installer"
                    }
                    : null,
                forgeVersion = currentVersion.HasForge ? currentVersion.Forge : null,
                neoForgeVersion = currentVersion.HasNeoForge ? currentVersion.NeoForge : null,
                cleanroomVersion = currentVersion.HasCleanroom ? currentVersion.Cleanroom : null,
                fabricVersion = currentVersion.HasFabric ? currentVersion.Fabric : null,
                liteLoaderEntry = currentVersion.HasLiteLoader
                    ? new GameDownload.DlLiteLoaderListEntry { Inherit = currentVersion.VanillaName }
                    : null,
                legacyFabricVersion = currentVersion.HasLegacyFabric ? currentVersion.LegacyFabric : null
            };

            var unsupportedLoader = McInstallPipeline.GetUnsupportedLoaderName(request);
            if (unsupportedLoader is not null)
            {
                HintService.Show(
                    Hints.Warning(Lang.Text("Download.Install.Loader.Unsupported", unsupportedLoader)));
                return;
            }

            var loaders = McInstallPipeline.CreateInstallLoader(request, ignoreDump: true);
            if (loaders.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Install.Error.OperationFailed")));
                return;
            }

            var combo = new Loading.LoaderCombo<object>(Lang.Text("Common.Action.Reset"), loaders)
            {
                OnStateChanged = OnInstallLoaderStateChanged(instance)
            };
            combo.Start();
            Loading.LoaderTaskbarAdd(combo);
            LoadingUi.Current?.RefreshDownloadButton();
            PageRouter.Navigate(PageType.Launch);
        }
        catch (CancelledException)
        {
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _btnManageRestore.IsEnabled = true;
        }
    }

    private async Task DeleteAsync(bool permanent)
    {
        if (_instance is null)
            return;
        _btnManageDelete.IsEnabled = false;
        try
        {
            var instance = _instance;
            var instancePath = instance.PathInstance;
            var instanceName = instance.Name;
            var isIsolatedInstance =
                instance.state != McInstanceState.Error
                && !string.Equals(
                    instance.PathIndie,
                    GameFolders.mcFolderSelected,
                    StringComparison.OrdinalIgnoreCase);
            var recycleSupported = RecycleBin.IsSupported(instancePath);
            var effectivePermanent = permanent || !recycleSupported;

            var confirmMessageKey = (isIsolatedInstance, effectivePermanent) switch
            {
                (true, true) => "Instance.Overall.Delete.ConfirmMessageIsolatedPermanent",
                (true, false) => "Instance.Overall.Delete.ConfirmMessageIsolated",
                (false, true) => "Instance.Overall.Delete.ConfirmMessagePermanent",
                (false, false) => "Instance.Overall.Delete.ConfirmMessage"
            };

            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Overall.Delete.ConfirmTitle"),
                    Content = Lang.Text(confirmMessageKey, instanceName),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = isIsolatedInstance || effectivePermanent
                });
            if (!dialog.IsConfirmed)
                return;

            LauncherCommon.IniClearCache(Path.Combine(instance.PathIndie, "options.txt"));
            if (effectivePermanent)
            {
                LauncherCommon.DeleteDirectory(instancePath);
                HintService.Show(
                    Hints.Success(Lang.Text("Instance.Overall.Delete.PermanentSuccess", instanceName)));
            }
            else
            {
                var outcome = RecycleBin.Move(instancePath, out var error);
                switch (outcome)
                {
                    case RecycleBin.Outcome.Moved:
                        HintService.Show(
                            Hints.Success(Lang.Text("Instance.Overall.Delete.RecycleBinSuccess", instanceName)));
                        break;
                    case RecycleBin.Outcome.Unsupported:
                        LauncherCommon.DeleteDirectory(instancePath);
                        HintService.Show(
                            Hints.Success(Lang.Text("Instance.Overall.Delete.PermanentSuccess", instanceName)));
                        break;
                    default:
                        LauncherCommon.Log(
                            $"将实例移入回收站失败：{error}",
                            LauncherLogLevel.Feedback);
                        HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Error.OperationFailed")));
                        return;
                }
            }

            InstanceList.mcInstanceListForceRefresh = true;
            RefreshInstanceList();
            PageRouter.Back();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _btnManageDelete.IsEnabled = true;
        }
    }

    private async Task PatchCoreAsync()
    {
        if (_instance is null)
            return;
        _btnManagePatch.IsEnabled = false;
        try
        {
            var instance = _instance;
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Overall.Patch.ConfirmTitle"),
                    Content = Lang.Text("Instance.Overall.Patch.ConfirmMessage", instance.Name),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!dialog.IsConfirmed)
                return;

            var jarPath = SystemDialogs.SelectFile(
                Lang.Text("Instance.Overall.Patch.SelectFile.Filter"),
                Lang.Text("Instance.Overall.Patch.SelectFile.Title"));
            if (string.IsNullOrWhiteSpace(jarPath))
                return;

            HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Patch.Patching")));
            var corePath = Path.Combine(instance.PathInstance, instance.Name + ".jar");
            var path = instance.PathInstance;
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        var core = new GameCore(corePath);
                        core.AddToCore(jarPath);
                        Config.Instance.DisableAssetVerifyV2[path] = true;
                        LauncherCommon.RunInUi(
                            () => HintService.Show(Hints.Success(Lang.Text("Instance.Overall.Patch.Success"))));
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(ex, "修补核心失败", LauncherLogLevel.Feedback);
                        LauncherCommon.RunInUi(
                            () => HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Error.OperationFailed"))));
                    }
                },
                "Patch Instance Core",
                ThreadPriority.BelowNormal);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _btnManagePatch.IsEnabled = true;
        }
    }

    #endregion

    #region 刷新

    private static Action<Loading.LoaderBase> OnInstallLoaderStateChanged(McInstance instance)
    {
        return loader =>
        {
            switch (loader.State)
            {
                case LoadState.Finished:
                    RefreshInstanceList();
                    HintService.Show(Hints.Success(Lang.Text("Common.Status.Success") + " " + instance.Name));
                    break;
                case LoadState.Failed:
                    HintService.Show(
                        Hints.Error(
                            Lang.Text("Instance.Install.Error.OperationFailed")
                            + " " + (loader.Error?.Message ?? string.Empty)));
                    break;
                case LoadState.Aborted:
                    HintService.Show(Hints.Info(Lang.Text("Common.Status.Cancelled")));
                    break;
            }
        };
    }

    private static void RefreshInstanceList()
    {
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(folder))
            return;
        InstanceList.mcInstanceListForceRefresh = true;
        LauncherCommon.WriteIni(Path.Combine(folder, "PCL.ini"), "InstanceCache", string.Empty);
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            folder,
            Loading.LoaderFolderRunType.ForceRun,
            1,
            "versions" + Path.DirectorySeparatorChar);
    }

    #endregion
}
