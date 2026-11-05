using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>导出选项：标题、说明、匹配规则与显示/勾选条件。</summary>
internal sealed class ExportOption
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Rules { get; init; } = string.Empty;

    public string ShowRules { get; init; } = string.Empty;

    public bool DefaultChecked { get; init; }

    public bool RequireModLoader { get; init; }

    public bool RequireOptiFine { get; init; }

    public bool RequireModLoaderOrOptiFine { get; init; }
}

/// <summary>
///     实例导出页：包名/版本、按实例内容动态显隐的导出内容选项、高级选项与底部导出按钮。
///     导出内容按选项规则匹配实例文件，生成带 <c>modrinth.index.json</c> 元数据的整合包；
///     勾选打包资源文件或联网获取失败时，资源文件直接放入 overrides。
/// </summary>
public class PageInstanceExport : MyPageRight, IRefreshable
{
    private static readonly string[] SubOptionBlackList =
        ["Quark Programmer Art.zip", "+ EuphoriaPatches_"];

    private readonly MyExtraTextButton _btnStartExport = null!;

    private readonly List<(MyCheckBox Box, ExportOption Option)> _options = [];

    private readonly StackPanel _panMain;
    private MyIconTextButton _btnResetOverrides = null!;
    private MyCard _cardOptions = null!;
    private MyCheckBox _checkBundleFiles = null!;
    private MyCheckBox _checkMod = null!;
    private MyCheckBox _checkModrinth = null!;
    private MyCheckBox _checkOtherFolders = null!;
    private MyCheckBox _checkPcl = null!;
    private MyCheckBox _checkPclCustom = null!;
    private MyCheckBox _checkResourcePacks = null!;
    private MyCheckBox _checkSaves = null!;
    private MyCheckBox _checkShaderPacks = null!;
    private string _configPackPath = string.Empty;
    private bool _exporting;
    private List<string> _extraFiles = [];
    private MyHint _hintBundle = null!;
    private MyHint _hintOptiFine = null!;
    private McInstance? _instance;
    private string _lastConfigPath = string.Empty;
    private string _lastInstancePath = string.Empty;
    private StackPanel _panModSub = null!;
    private StackPanel _panOptions = null!;
    private StackPanel _panOtherFoldersSub = null!;
    private StackPanel _panPclSub = null!;
    private StackPanel _panResourcePackSub = null!;
    private StackPanel _panSavesSub = null!;
    private StackPanel _panShaderPackSub = null!;

    private List<string> _rulesOverrides = [];

    private MyTextBox _textName = null!;
    private MyTextBox _textVersion = null!;

    public PageInstanceExport()
    {
        var root = new Grid();
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 10d, 25d, 65d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        root.Children.Add(scroll);
        _btnStartExport = new MyExtraTextButton
        {
            Text = Lang.Text("Instance.Export.StartExport"),
            SvgIcon = "lucide/package",
            LogoScale = 1.1d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _btnStartExport.Click += (_, _) => _ = StartExportAsync();
        root.Children.Add(_btnStartExport);
        Child = root;

        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
        BuildUi();
    }

    public void Refresh()
    {
        RefreshAll();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        if (_instance is not null && _instance.PathInstance != _lastInstancePath)
            RefreshAll();
        else
            RefreshSubOptions();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (_instance is null)
            return;
        _lastInstancePath = _instance.PathInstance;
        _lastConfigPath = States.System.ExportConfigPath ?? string.Empty;
        _rulesOverrides = [];
        _extraFiles = [];
        _configPackPath = string.Empty;
        _textName.Text = string.Empty;
        _textVersion.Text = string.Empty;
        if (_panMain.Children.Count > 0 && _panMain.Children[0] is MyHint hint)
            hint.IsVisible = _instance.Info.HasOptiFine;
        ResetConfigOverrides();
        RefreshSubOptions();
        RefreshAllOptionsUi();
        PanScroll?.Offset = new Vector(0d, 0d);
    }

    #region 界面构建

    private void BuildUi()
    {
        _panMain.Children.Clear();
        _options.Clear();

        _hintOptiFine = new MyHint
        {
            Text = Lang.Text("Instance.Export.ModrinthWarning"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 15d, 0d, 0d),
            IsVisible = _instance?.Info.HasOptiFine ?? false
        };
        _panMain.Children.Add(_hintOptiFine);

        var headerCard = new MyCard
        {
            Margin = new Thickness(0d, 15d)
        };
        var headerGrid = new Grid
        {
            Margin = new Thickness(22d, 15d, 25d, 15d),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,0.3*"),
            RowDefinitions = new RowDefinitions("28")
        };
        var nameLabel = new TextBlock
        {
            Text = Lang.Text("Instance.Export.PackName"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 20d, 0d)
        };
        headerGrid.Children.Add(nameLabel);
        _textName = new MyTextBox
        {
            MaxLength = 100
        };
        Grid.SetColumn(_textName, 1);
        headerGrid.Children.Add(_textName);
        var versionLabel = new TextBlock
        {
            Text = Lang.Text("Instance.Export.PackVersion"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(30d, 0d, 20d, 0d)
        };
        Grid.SetColumn(versionLabel, 2);
        headerGrid.Children.Add(versionLabel);
        _textVersion = new MyTextBox
        {
            MaxLength = 100
        };
        Grid.SetColumn(_textVersion, 3);
        headerGrid.Children.Add(_textVersion);
        headerCard.BorderChild = headerGrid;
        _panMain.Children.Add(headerCard);

        _cardOptions = new MyCard
        {
            Margin = new Thickness(0d, 0d, 0d, 15d),
            MinHeight = 39d
        };
        _cardOptions.PointerPressed += (_, _) =>
        {
            if (_rulesOverrides.Count > 0)
                ResetConfigOverrides();
        };
        _btnResetOverrides = new MyIconTextButton
        {
            Text = Lang.Text("Common.Action.Reset"),
            SvgIcon = "lucide/rotate-ccw",
            LogoScale = 0.9d,
            Height = 24d,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 5d, 0d),
            IsHitTestVisible = true,
            IsVisible = false
        };
        _btnResetOverrides.Click += (_, _) => ResetConfigOverrides();
        _cardOptions.Children.Add(_btnResetOverrides);
        _panOptions = new StackPanel
        {
            Margin = new Thickness(25d, 38d, 15d, 15d)
        };
        _cardOptions.Children.Add(_panOptions);
        _panMain.Children.Add(_cardOptions);

        BuildOptionRows();

        var advancedCard = new MyCard
        {
            Title = Lang.Text("Instance.Export.Advanced.Title"),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var advancedBody = new StackPanel
        {
            Margin = new Thickness(25d, 37d, 23d, 20d)
        };
        _hintBundle = new MyHint
        {
            Text = Lang.Text("Instance.Export.Advanced.BundleHint"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 2d, 0d, 8d),
            IsVisible = false
        };
        advancedBody.Children.Add(_hintBundle);

        _checkBundleFiles = new MyCheckBox
        {
            Text = Lang.Text("Instance.Export.Advanced.BundleFiles")
        };
        ToolTip.SetTip(
            _checkBundleFiles,
            Lang.Text("Instance.Export.Advanced.BundleFiles.ToolTip"));
        _checkBundleFiles.Change += (_, _) =>
        {
            var bundle = _checkBundleFiles.Checked == true;
            _hintBundle.IsVisible = bundle;
            if (bundle)
                _checkModrinth.Checked = false;
            _checkModrinth.IsEnabled = !bundle;
        };
        advancedBody.Children.Add(_checkBundleFiles);

        _checkModrinth = new MyCheckBox
        {
            Text = Lang.Text("Instance.Export.Advanced.Modrinth")
        };
        ToolTip.SetTip(_checkModrinth, Lang.Text("Instance.Export.Advanced.Modrinth.ToolTip"));
        _checkModrinth.Change += (_, _) =>
        {
            var modrinth = _checkModrinth.Checked == true;
            if (modrinth)
            {
                _checkBundleFiles.Checked = false;
                _checkPcl.Checked = false;
            }

            _checkBundleFiles.IsEnabled = !modrinth;
            _checkPcl.IsEnabled = !modrinth;
            if (modrinth && _checkPcl.Checked == true)
                _checkPcl.Checked = false;
        };
        advancedBody.Children.Add(_checkModrinth);

        advancedBody.Children.Add(
            new MyHint
            {
                Text = Lang.Text("Instance.Export.Advanced.ConfigHint"),
                Theme = MyHint.Themes.Blue,
                RelativeSetup = "HintExportConfig",
                CanClose = true,
                Margin = new Thickness(0d, 20d, 0d, 2d)
            });

        var configButtons = new Grid
        {
            Height = 35d,
            Margin = new Thickness(0d, 12d, 0d, 0d),
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };
        var importButton = new MyButton
        {
            Text = Lang.Text("Instance.Export.Advanced.ImportConfig"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            ColorType = MyButton.ColorState.Highlight
        };
        importButton.Click += (_, _) => ImportConfig();
        configButtons.Children.Add(importButton);
        var exportButton = new MyButton
        {
            Text = Lang.Text("Instance.Export.Advanced.ExportConfig"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d)
        };
        Grid.SetColumn(exportButton, 1);
        exportButton.Click += (_, _) => ExportConfig();
        configButtons.Children.Add(exportButton);
        advancedBody.Children.Add(configButtons);

        advancedCard.Children.Add(advancedBody);
        advancedCard.SwapControl = advancedBody;
        advancedCard.IsSwapped = true;
        _panMain.Children.Add(advancedCard);
    }

    private void BuildOptionRows()
    {
        var basic = AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Game"),
                DefaultChecked = true
            },
            false);
        basic.IsEnabled = false;

        AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.GameSettings"),
                Description = Lang.Text("Instance.Export.Option.GameSettings.Desc"),
                Rules = "options.txt|configureddefaults/",
                DefaultChecked = true
            },
            true);

        AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.GameProfile"),
                Description = Lang.Text("Instance.Export.Option.GameProfile.Desc"),
                Rules = "hotbar.nbt|command_history.txt"
            },
            true);

        AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.OptiFine"),
                Rules = "optionsof.txt|optionsshaders.txt",
                DefaultChecked = true,
                RequireOptiFine = true
            },
            true);

        _checkMod = AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Mod"),
                Description = Lang.Text("Instance.Export.Option.Mod.Desc"),
                Rules =
                    "mods/|!mods/*.disabled|!mods/*.old|!mods/.connector/|coremods/|lib/|!mods/mcef-libraries/|!mods/mcef-cache/",
                DefaultChecked = true,
                RequireModLoader = true
            },
            false);
        _panModSub = new StackPanel
        {
            Margin = new Thickness(30d, 0d, 0d, 0d)
        };
        _panOptions.Children.Add(_panModSub);
        _checkMod.Change += (_, _) => _panModSub.IsVisible = _checkMod.Checked == true;

        AddOption(
            _panModSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.DisabledMod"),
                Rules = "mods/*.disabled|mods/*.old"
            },
            false);
        AddOption(
            _panModSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ImportantData"),
                Description = Lang.Text("Instance.Export.Option.ImportantData.Desc"),
                Rules =
                    "addons/|multiblocked/|modpack-update-checker/|global_packs/|global_resource_packs/|global_data_packs/|optional_data_packs/|maps/|icon.png|mods-resourcepacks/|matmos/|resource_assorts/|resource_assorts.json|patchouli_books/|datapacks/|kubejs*/|!kubejs*/probe/|!kubejs*/exported/|!kubejs*/jsconfig.json|!kubejs*/README.txt|openloader/|worldshape/|resources/|scripts/|structures/|fontfiles/|oresources/|packmenu/|craftpresence/|pointblanks/|template*/|!template*/playerdata/|!template*/stats/",
                DefaultChecked = true
            },
            false);
        AddOption(
            _panModSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ModSettings"),
                Rules =
                    "config/|!config/accountsx/|!config/jei/world/|!config/worldedit/|config/worldedit/worldedit.properties|!config/spark/|config/spark/config.json|defaultconfigs/|journeymap/config/|journeymap/server/|TrashSlotSaveState.json|customfov.txt|gg.essential.mod/|essential/|!essential/*/|!essential/*.jar*|!essential/screenshot-checksum-caches.json|!essential/microsoft_accounts.json|paragliderSettings.nbt|local/client_config.json|local/ftbl.json|local/client/sidebar_buttons.json|local/client/ftbutilities.cfg|local/client/ftblib.cfg|local/client/xencraft.cfg|liteloader.properties|default_reference.xml|CustomSkinLoader/CustomSkinLoader.json",
                DefaultChecked = true
            },
            false);
        AddOption(
            _panModSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Maps"),
                Description = Lang.Text("Instance.Export.Option.Maps.Desc"),
                Rules = "journeymap/data/|xaero/|XaeroWaypoints/|XaeroWorldMap/"
            },
            false);
        AddOption(
            _panModSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Jei"),
                Description = Lang.Text("Instance.Export.Option.Jei.Desc"),
                Rules = "config/jei/world/"
            },
            false);
        AddOption(
            _panModSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Emi"),
                Description = Lang.Text("Instance.Export.Option.Emi.Desc"),
                Rules = "emi.json"
            },
            false);
        AddOption(
            _panModSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Patchouli"),
                Description = Lang.Text("Instance.Export.Option.Patchouli.Desc"),
                Rules = "patchouli_data.json"
            },
            false);

        _checkResourcePacks = AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ResourcePacks"),
                Description = Lang.Text("Instance.Export.Option.ResourcePacks.Desc"),
                ShowRules = "resourcepacks/|texturepacks/",
                DefaultChecked = true
            },
            false);
        _panResourcePackSub = new StackPanel
        {
            Margin = new Thickness(30d, 0d, 0d, 0d)
        };
        _panOptions.Children.Add(_panResourcePackSub);
        _checkResourcePacks.Change += (_, _) =>
            _panResourcePackSub.IsVisible = _checkResourcePacks.Checked == true;

        _checkShaderPacks = AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ShaderPacks"),
                ShowRules = "shaderpacks/",
                DefaultChecked = true,
                RequireModLoaderOrOptiFine = true
            },
            false);
        _panShaderPackSub = new StackPanel
        {
            Margin = new Thickness(30d, 0d, 0d, 0d)
        };
        _panOptions.Children.Add(_panShaderPackSub);
        _checkShaderPacks.Change += (_, _) =>
            _panShaderPackSub.IsVisible = _checkShaderPacks.Checked == true;

        AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Screenshots"),
                Rules = "screenshots/"
            },
            false);
        AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Structures"),
                Description = Lang.Text("Instance.Export.Option.Structures.Desc"),
                Rules = "schematics/"
            },
            false);
        AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Replay"),
                Description = Lang.Text("Instance.Export.Option.Replay.Desc"),
                Rules = "replay_recordings/|replay_videos/",
                RequireModLoader = true
            },
            false);

        _checkSaves = AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Saves"),
                Description = Lang.Text("Instance.Export.Option.Saves.Desc"),
                ShowRules = "saves/"
            },
            false);
        _panSavesSub = new StackPanel
        {
            Margin = new Thickness(30d, 0d, 0d, 0d)
        };
        _panOptions.Children.Add(_panSavesSub);
        _checkSaves.Change += (_, _) => _panSavesSub.IsVisible = _checkSaves.Checked == true;

        AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Servers"),
                Rules = "servers.dat"
            },
            false);

        _checkOtherFolders = AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.OtherFolders"),
                Description = Lang.Text("Instance.Export.Option.OtherFolders.Desc")
            },
            false);
        _panOtherFoldersSub = new StackPanel
        {
            Margin = new Thickness(30d, 0d, 0d, 0d)
        };
        _panOptions.Children.Add(_panOtherFoldersSub);
        _checkOtherFolders.Change += (_, _) =>
        {
            var check = _checkOtherFolders.Checked == true;
            _panOtherFoldersSub.IsVisible = check;
            foreach (var child in _panOtherFoldersSub.Children)
                if (child is MyCheckBox childBox)
                    childBox.Checked = check;
        };

        _checkPcl = AddOption(
            _panOptions,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Launcher"),
                Description = Lang.Text("Instance.Export.Option.Launcher.Desc")
            },
            false);
        _panPclSub = new StackPanel
        {
            Margin = new Thickness(30d, 0d, 0d, 0d)
        };
        _panOptions.Children.Add(_panPclSub);
        _checkPcl.Change += (_, _) => _panPclSub.IsVisible = _checkPcl.Checked == true;
        _checkPclCustom = AddOption(
            _panPclSub,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.LauncherCustom"),
                Description = Lang.Text("Instance.Export.Option.LauncherCustom.Desc"),
                DefaultChecked = true
            },
            false);
    }

    private MyCheckBox AddOption(StackPanel parent, ExportOption option, bool indent)
    {
        var box = new MyCheckBox
        {
            Text = option.Title,
            Height = 26d,
            Tag = option,
            Margin = indent ? new Thickness(30d, 0d, 0d, 0d) : new Thickness(0d)
        };
        if (!string.IsNullOrEmpty(option.Description))
            ToolTip.SetTip(box, option.Description);
        parent.Children.Add(box);
        _options.Add((box, option));
        return box;
    }

    #endregion

    #region 子选项

    private void RefreshSubOptions()
    {
        if (_instance is null || _panResourcePackSub is null)
            return;

        ReloadSubOptions(_panResourcePackSub, true, true, "resourcepacks", "texturepacks");
        ReloadSubOptions(_panSavesSub, false, true, "saves");
        ReloadSubOptions(_panShaderPackSub, true, true, "shaderpacks");
        ReloadOtherFolders();
        RefreshAllOptionsUi();
    }

    private void ReloadSubOptions(
        StackPanel panel,
        bool acceptCompressedFile,
        bool acceptFolder,
        params string[] folders)
    {
        if (_instance is null)
            return;
        panel.Children.Clear();
        foreach (var folder in folders)
        {
            var targetFolder = new DirectoryInfo(Path.Combine(_instance.PathIndie, folder));
            if (!targetFolder.Exists)
                continue;
            if (acceptCompressedFile)
                foreach (var file in targetFolder
                             .EnumerateFiles("*.zip")
                             .Concat(targetFolder.EnumerateFiles("*.rar")))
                {
                    if (SubOptionBlackList.Any(b => file.Name.Contains(
                            b,
                            StringComparison.OrdinalIgnoreCase)))
                        continue;
                    panel.Children.Add(
                        CreateSubOption(file.Name, EscapePattern($"{folder}/{file.Name}"), true));
                    if (folder != "shaderpacks")
                        continue;
                    var shaderConfig = new FileInfo(
                        Path.Combine(
                            file.Directory!.FullName,
                            $"{file.Name}.txt"));
                    if (shaderConfig.Exists)
                        panel.Children.Add(
                            CreateSubOption(
                                shaderConfig.Name,
                                EscapePattern($"{folder}/{shaderConfig.Name}"),
                                true,
                                Lang.Text("Instance.Export.Config.ShaderConfigSuffix"),
                                true));
                }

            if (!acceptFolder)
                continue;
            foreach (var subFolder in targetFolder
                         .EnumerateDirectories()
                         .OrderByDescending(f => f.LastWriteTime))
            {
                if (SubOptionBlackList.Any(b => subFolder.Name.Contains(
                        b,
                        StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (!subFolder.EnumerateFileSystemInfos().Any())
                    continue;
                var description = ReferenceEquals(panel, _panSavesSub)
                    ? Lang.Date(subFolder.LastWriteTime, "g")
                    : string.Empty;
                panel.Children.Add(
                    CreateSubOption(
                        subFolder.Name,
                        EscapePattern($"{folder}/{subFolder.Name}/"),
                        true,
                        description));
                if (folder != "shaderpacks")
                    continue;
                var shaderConfig = new FileInfo(
                    Path.Combine(
                        targetFolder.FullName,
                        $"{subFolder.Name}.txt"));
                if (shaderConfig.Exists)
                    panel.Children.Add(
                        CreateSubOption(
                            shaderConfig.Name,
                            EscapePattern($"{folder}/{shaderConfig.Name}"),
                            true,
                            Lang.Text("Instance.Export.Config.ShaderConfigSuffix"),
                            true));
            }
        }
    }

    private MyCheckBox CreateSubOption(
        string title,
        string rules,
        bool defaultChecked,
        string description = "",
        bool indent = false)
    {
        var option = new ExportOption
        {
            Title = title,
            Rules = rules,
            DefaultChecked = defaultChecked,
            Description = description
        };
        var box = new MyCheckBox
        {
            Text = title,
            Height = 26d,
            Tag = option,
            Margin = indent ? new Thickness(30d, 0d, 0d, 0d) : new Thickness(0d),
            Checked = defaultChecked
        };
        if (!string.IsNullOrEmpty(description))
            ToolTip.SetTip(box, description);
        _options.Add((box, option));
        return box;
    }

    private void ReloadOtherFolders()
    {
        if (_instance is null)
            return;
        _panOtherFoldersSub.Children.Clear();

        var coveredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mods",
            "coremods",
            "lib",
            "addons",
            "multiblocked",
            "modpack-update-checker",
            "global_packs",
            "global_resource_packs",
            "global_data_packs",
            "optional_data_packs",
            "maps",
            "mods-resourcepacks",
            "matmos",
            "resource_assorts",
            "patchouli_books",
            "datapacks",
            "openloader",
            "worldshape",
            "resources",
            "scripts",
            "structures",
            "fontfiles",
            "oresources",
            "packmenu",
            "craftpresence",
            "pointblanks",
            "config",
            "defaultconfigs",
            "journeymap",
            "local",
            "essential",
            "gg.essential.mod",
            "CustomSkinLoader",
            "xaero",
            "XaeroWaypoints",
            "XaeroWorldMap",
            "resourcepacks",
            "texturepacks",
            "shaderpacks",
            "screenshots",
            "schematics",
            "replay_recordings",
            "replay_videos",
            "saves",
            "configureddefaults",
            "assets",
            "versions",
            "libraries",
            "structureCacheV1",
            ".fabric",
            ".git",
            "avatar-cache",
            "cosmetic-cache",
            "PCL"
        };
        var coveredPrefixes = new[]
        {
            "kubejs",
            "template"
        };
        var coveredSuffixes = new[]
        {
            "-natives"
        };

        var rootDir = new DirectoryInfo(_instance.PathIndie);
        if (!rootDir.Exists)
        {
            _checkOtherFolders.IsVisible = false;
            return;
        }

        foreach (var subDir in rootDir.EnumerateDirectories())
        {
            if (coveredFolders.Contains(subDir.Name))
                continue;
            if (coveredPrefixes.Any(p => subDir.Name.StartsWith(
                    p,
                    StringComparison.OrdinalIgnoreCase)))
                continue;
            if (coveredSuffixes.Any(s => subDir.Name.EndsWith(
                    s,
                    StringComparison.OrdinalIgnoreCase)))
                continue;

            var box = new MyCheckBox
            {
                Text = subDir.Name,
                Height = 26d
            };
            var option = new ExportOption
            {
                Title = subDir.Name,
                Rules = EscapePattern($"{subDir.Name}/")
            };
            box.Tag = option;
            _panOtherFoldersSub.Children.Add(box);
            _options.Add((box, option));
        }

        _checkOtherFolders.IsVisible = _panOtherFoldersSub.Children.Count > 0;
        if (!_checkOtherFolders.IsVisible)
            _checkOtherFolders.Checked = false;
    }

    #endregion

    #region 选项显隐与规则

    private void RefreshAllOptionsUi()
    {
        if (_instance is null)
            return;

        var allEntries = new List<string>();
        var pathIndie = _instance.PathIndie;
        try
        {
            var pathInfo = new DirectoryInfo(pathIndie);
            if (pathInfo.Exists)
            {
                allEntries.AddRange(pathInfo.EnumerateFiles().Select(f => f.Name));
                foreach (var subFolder in pathInfo.EnumerateDirectories().Where(IsValidDirectory))
                {
                    allEntries.Add($"{subFolder.Name}\\");
                    allEntries.AddRange(
                        subFolder
                            .EnumerateFiles()
                            .Select(f =>
                                $"{subFolder.Name}\\{f.Name}"));
                    allEntries.AddRange(
                        subFolder
                            .EnumerateDirectories()
                            .Where(IsValidDirectory)
                            .Select(d => $"{subFolder.Name}\\{d.Name}\\"));
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "扫描实例目录以判断导出选项失败", LauncherLogLevel.Developer);
        }

        foreach (var (box, option) in _options)
        {
            if (string.IsNullOrEmpty(option.Rules) && string.IsNullOrEmpty(option.ShowRules))
            {
                box.IsVisible = true;
                if (box.IsEnabled)
                    box.Checked = option.DefaultChecked;
                continue;
            }

            var pass = IsVisible(option);
            box.IsVisible = pass;
            if (box.IsEnabled)
                box.Checked = option.DefaultChecked && pass;
        }

        if (_checkMod is not null)
            _panModSub.IsVisible = _checkMod.Checked == true;
        if (_checkResourcePacks is not null)
            _panResourcePackSub.IsVisible = _checkResourcePacks.Checked == true;
        if (_checkShaderPacks is not null)
            _panShaderPackSub.IsVisible = _checkShaderPacks.Checked == true;
        if (_checkSaves is not null)
            _panSavesSub.IsVisible = _checkSaves.Checked == true;
        if (_checkOtherFolders is not null)
            _panOtherFoldersSub.IsVisible = _checkOtherFolders.Checked == true;
        if (_checkPcl is not null)
            _panPclSub.IsVisible = _checkPcl.Checked == true;
        return;

        bool IsVisible(ExportOption option)
        {
            if (option.RequireOptiFine && !_instance.Info.HasOptiFine)
                return false;
            if (option.RequireModLoader && !_instance.Modable)
                return false;
            if (option.RequireModLoaderOrOptiFine
                && !_instance.Info.HasOptiFine
                && !_instance.Modable)
                return false;
            return StandardizeRules(option.Rules.Split('|'), true)
                .Any(rule =>
                {
                    if (rule.StartsWith('!'))
                        return false;
                    try
                    {
                        if (allEntries.Any(entry => LikeString(entry, rule)))
                            return true;
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(ex, $"错误的导出规则：{rule}", LauncherLogLevel.Hint);
                        return false;
                    }

                    var trimmed = rule.Trim('*', '?');
                    if (trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length < 3)
                        return false;
                    return trimmed.EndsWith('\\')
                        ? IsValidDirectory(new DirectoryInfo(pathIndie + trimmed))
                        : File.Exists(pathIndie + trimmed);
                });
        }
    }

    private static bool IsValidDirectory(DirectoryInfo folder)
    {
        try
        {
            return folder.Exists
                && folder
                    .EnumerateFileSystemInfos()
                    .Any(i => !SubOptionBlackList.Any(b => i.Name.Contains(
                        b,
                        StringComparison.OrdinalIgnoreCase)));
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<string> GetAllRules()
    {
        if (_rulesOverrides.Count > 0)
        {
            foreach (var rule in _rulesOverrides)
                yield return rule;
            yield break;
        }

        yield return string.Empty;
        yield return $"# {Lang.Text("Instance.Export.Config.Comment.ModifyRules")}";
        yield return $"# {Lang.Text("Instance.Export.Config.Comment.ReverseMatch")}";
        yield return string.Empty;
        foreach (var (box, option) in _options)
        {
            if (box.Checked != true || string.IsNullOrEmpty(option.Rules))
                continue;
            yield return $"# {option.Title}";
            foreach (var rule in option.Rules.Split('|'))
                yield return rule;
            yield return string.Empty;
        }

        yield return $"# {Lang.Text("Instance.Export.Config.Comment.ExcludedFiles")}";
        yield return "!*.log";
        yield return "!*.dat_old";
        yield return "!*.BakaCoreInfo";
        yield return "!hmclversion.cfg";
        yield return "!log4j2.xml";
        yield return string.Empty;
    }

    private IEnumerable<string> GetExtraFileLines()
    {
        if (_extraFiles.Count == 0)
        {
            yield return string.Empty;
            yield return $"# {Lang.Text("Instance.Export.Config.Comment.ExtraFiles")}";
            yield return $"# {Lang.Text("Instance.Export.Config.Comment.ExtraFiles2")}";
            yield return string.Empty;
            yield break;
        }

        foreach (var file in _extraFiles)
            yield return file;
    }

    private static IEnumerable<string> StandardizeRules(
        IEnumerable<string> raw,
        bool addSuffixStarToFolderPath)
    {
        return raw
            .Select(rawLine => rawLine.Trim())
            .Where(line =>
                !string.IsNullOrEmpty(line) && !line.StartsWith('#') && !line.StartsWith('='))
            .Select(line => line.Replace('/', '\\'))
            .Select(line => line.EndsWith('\\') && addSuffixStarToFolderPath ? $"{line}**" : line);
    }

    private static List<Regex> CompileRules(IEnumerable<string> rules)
    {
        var result = new List<Regex>();
        foreach (var rule in StandardizeRules(rules, true))
        {
            var pattern = new StringBuilder("^");
            for (var i = 0; i < rule.Length; i++)
            {
                var ch = rule[i];
                if (ch == '*' && i + 1 < rule.Length && rule[i + 1] == '*')
                {
                    pattern.Append(".*");
                    i++;
                }
                else if (ch == '*')
                {
                    pattern.Append("[^\\\\]*");
                }
                else if (ch == '?')
                {
                    pattern.Append("[^\\\\]");
                }
                else if (ch == '#')
                {
                    pattern.Append("[0-9]");
                }
                else
                {
                    pattern.Append(Regex.Escape(ch.ToString()));
                }
            }

            pattern.Append('$');
            result.Add(
                new Regex(
                    pattern.ToString(),
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        }

        return result;
    }

    private static string EscapePattern(string path)
    {
        var builder = new StringBuilder();
        foreach (var ch in path)
        {
            if (ch is '*' or '?' or '#' or '[' or ']' or '\\')
                builder.Append('\\');
            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool LikeString(string input, string rule)
    {
        var regex = new Regex(
            $"^{Regex.Escape(rule.Replace('/', '\\'))
                .Replace("\\*\\*", ".*")
                .Replace("\\*", "[^\\\\]*")
                .Replace("\\?", "[^\\\\]")
                .Replace("#", "[0-9]")}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return regex.IsMatch(input.Replace('/', '\\'));
    }

    #endregion

    #region 配置文件

    private const string Separator =
        "==============================================================";

    private void ResetConfigOverrides()
    {
        _rulesOverrides = [];
        _extraFiles = [];
        _configPackPath = string.Empty;
        _btnResetOverrides.IsVisible = false;
        _panOptions.IsVisible = true;
        _cardOptions.Title = Lang.Text("Instance.Export.OptionListTitle");
    }

    private void ExportConfig()
    {
        try
        {
            var configPath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Export.SelectFileLocation"),
                "export_config.txt",
                Lang.Text("Instance.Export.Config.FileFilter"),
                _lastConfigPath);
            if (string.IsNullOrEmpty(configPath))
                return;
            _lastConfigPath = configPath;
            States.System.ExportConfigPath = configPath;

            var lines = new List<string>
            {
                $"Name:{_textName.Text ?? string.Empty}",
                $"Version:{_textVersion.Text ?? string.Empty}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.IncludeLauncher")}",
                $"IncludeLauncher:{_checkPcl.Checked == true}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.IncludeLauncherCustom")}",
                $"IncludeLauncherCustom:{_checkPclCustom.Checked == true}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.BundleFiles")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.BundleFiles2")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.BundleFiles3")}",
                $"DontCheckHostedAssets:{_checkBundleFiles.Checked == true}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.Modrinth")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.Modrinth2")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.Modrinth3")}",
                $"ModrinthUploadMode:{_checkModrinth.Checked == true}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.PackPath")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.PackPath2")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.PackPath3")}",
                $"PackPath:{_configPackPath}",
                string.Empty,
                Separator
            };
            lines.AddRange(GetAllRules());
            lines.Add(Separator);
            lines.AddRange(GetExtraFileLines());
            LauncherCommon.WriteFile(configPath, string.Join("\r\n", lines));
            HintService.Show(Hints.Success(Lang.Text("Instance.Export.SaveSuccess", configPath)));
            InstancePageHelper.OpenFolder(configPath);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Export.Error.OperationFailed", ex);
        }
    }

    private void ImportConfig()
    {
        try
        {
            var configPath = SystemDialogs.SelectFile(
                Lang.Text("Instance.Export.Config.FileFilter"),
                Lang.Text("Instance.Export.SelectConfigFile"),
                _lastConfigPath);
            if (string.IsNullOrEmpty(configPath))
                return;
            ReadConfigFile(configPath);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Export.Error.OperationFailed", ex);
        }
    }

    private void ReadConfigFile(string configPath)
    {
        try
        {
            _lastConfigPath = configPath;
            States.System.ExportConfigPath = configPath;
            var content = LauncherCommon.ReadFile(configPath);
            var segments = content.Split(Separator);
            if (segments.Length == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Export.Config.Invalid")));
                return;
            }

            var ini = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lineRaw in segments[0].Split('\n'))
            {
                var line = lineRaw.Replace("\r", string.Empty).Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('='))
                    continue;
                var index = line.IndexOf(':');
                if (index > 0)
                    ini[line[..index]] = line[(index + 1)..];
            }

            _textName.Text = ini.GetValueOrDefault("Name", string.Empty);
            _textVersion.Text = ini.GetValueOrDefault("Version", string.Empty);
            _checkPcl.Checked = ReadBool(ini, "IncludeLauncher", false);
            _checkPclCustom.Checked = ReadBool(ini, "IncludeLauncherCustom", true);
            _checkModrinth.Checked = ReadBool(ini, "ModrinthUploadMode", false);
            _checkBundleFiles.Checked = ReadBool(ini, "DontCheckHostedAssets", false);
            _configPackPath = ini.GetValueOrDefault("PackPath", string.Empty);

            _rulesOverrides = [.. segments[1].Replace("\r", string.Empty).Split('\n')];
            _extraFiles = segments.Length > 2
                ? [.. segments[2].Replace("\r", string.Empty).Split('\n')]
                : [];
            _panOptions.IsVisible = false;
            _btnResetOverrides.IsVisible = true;
            _cardOptions.Title =
                $"{Lang.Text("Instance.Export.OptionListTitle")}: {Lang.Text("Instance.Export.OptionList.FromConfig")}";
            HintService.Show(Hints.Success(Lang.Text("Instance.Export.ReadSuccess", configPath)));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"读取导出配置失败：{configPath}", LauncherLogLevel.Feedback);
            InstancePageHelper.ReportError("Instance.Export.Error.OperationFailed", ex);
        }
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> ini, string key, bool fallback)
    {
        return ini.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static string? GetSingleTxtFile(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: 1 })
            return null;
        var path = files[0].TryGetLocalPath();
        return !string.IsNullOrEmpty(path)
            && path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = GetSingleTxtFile(e) is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var path = GetSingleTxtFile(e);
        if (path is not null)
            ReadConfigFile(path);
        e.Handled = true;
    }

    #endregion

    #region 导出

    private async Task StartExportAsync()
    {
        if (_exporting || _instance is null)
            return;
        _exporting = true;
        _btnStartExport.IsEnabled = false;
        try
        {
            var instance = _instance;
            var packName = string.IsNullOrWhiteSpace(_textName.Text)
                ? instance.Name
                : _textName.Text!.Trim();
            var packVersion = string.IsNullOrWhiteSpace(_textVersion.Text)
                ? "1.0.0"
                : _textVersion.Text!.Trim();
            var loaderName = Lang.Text("Instance.Export.ExportTask.Prefix") + packName;
            if (Loading.loaderTaskbar.Any(loader => loader.name == loaderName))
            {
                PageRouter.Navigate(PageType.TaskManager);
                return;
            }

            string? packPath = null;
            if (!string.IsNullOrWhiteSpace(_configPackPath)
                && !_configPackPath.EndsWith('\\')
                && !_configPackPath.EndsWith('/'))
                try
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(_configPackPath) ?? string.Empty);
                    packPath = _configPackPath;
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, "无法使用配置文件指定的导出路径", LauncherLogLevel.Hint);
                }

            if (packPath is null)
            {
                var extensions = new List<string>();
                if (_checkModrinth.Checked != true)
                    extensions.Add(Lang.Text("Instance.Export.ZipFilter"));
                if (_checkPcl.Checked != true)
                    extensions.Add(Lang.Text("Instance.Export.MrpackFilter"));
                packPath = SystemDialogs.SelectSaveFile(
                    Lang.Text("Instance.Export.SelectSaveLocation"),
                    packName
                    + (string.IsNullOrWhiteSpace(_textVersion.Text)
                        ? string.Empty
                        : $" {packVersion}"),
                    string.Join('|', extensions));
            }

            if (string.IsNullOrEmpty(packPath))
                return;

            var includePcl = _checkPcl.Checked == true;
            var includePclCustom = includePcl && _checkPclCustom.Checked == true;
            var bundleFiles = _checkBundleFiles.Checked == true;
            var modrinthMode = _checkModrinth.Checked == true;
            var rules = (
                from rule in StandardizeRules(GetAllRules(), true)
                let exclude = rule.StartsWith('!')
                let compiled = CompileRules([exclude ? rule[1..] : rule]).First()
                select (compiled, !exclude)).ToList();

            var extraFiles = StandardizeRules(GetExtraFileLines(), false).ToList();
            var cacheFolder = LauncherCommon.GetTempFolder("Instance Export");
            var targetPath = packPath;
            var loaders = new List<Loading.LoaderBase>
            {
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Instance.Export.Task.CopyContent"),
                    loader => CopyContent(
                        instance,
                        cacheFolder,
                        rules,
                        extraFiles,
                        includePcl,
                        includePclCustom,
                        loader))
                {
                    ProgressWeight = 5d
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Instance.Export.Task.CreateArchive"),
                    loader => CreateArchive(
                        instance,
                        cacheFolder,
                        targetPath,
                        packName,
                        packVersion,
                        rules,
                        includePcl,
                        bundleFiles,
                        modrinthMode,
                        loader))
                {
                    ProgressWeight = 6d
                }
            };

            var mainLoader = new Loading.LoaderCombo<string>(loaderName, loaders)
            {
                OnStateChanged = loader =>
                {
                    switch (loader.State)
                    {
                        case LoadState.Finished:
                            HintService.Show(
                                Hints.Success(loaderName + Lang.Text("Common.Status.Success")));
                            InstancePageHelper.OpenFolder(
                                Path.GetDirectoryName(targetPath) ?? string.Empty);
                            break;
                        case LoadState.Failed:
                            HintService.Show(
                                Hints.Error(Lang.Text("Instance.Export.Error.OperationFailed")));
                            break;
                        case LoadState.Aborted:
                            HintService.Show(Hints.Info(Lang.Text("Common.Status.Cancelled")));
                            break;
                        case LoadState.Waiting:
                        case LoadState.Loading:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            };
            mainLoader.Start();
            Loading.LoaderTaskbarAdd(mainLoader);
            LoadingUi.Current?.RefreshDownloadButton();
            PageRouter.Navigate(PageType.TaskManager);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Export.Error.OperationFailed", ex);
        }
        finally
        {
            _exporting = false;
            _btnStartExport.IsEnabled = true;
        }
    }

    private static void CopyContent(
        McInstance instance,
        string cacheFolder,
        List<(Regex Regex, bool Include)> rules,
        List<string> extraFiles,
        bool includePcl,
        bool includePclCustom,
        Loading.LoaderTask<int, int> loader)
    {
        var overridesFolder = Path.Combine(cacheFolder, "modpack", "overrides");
        Directory.CreateDirectory(overridesFolder);
        var pathIndie = instance.PathIndie;

        void SearchFolder(DirectoryInfo folder, bool isRoot)
        {
            foreach (var subFolder in folder.EnumerateDirectories())
            {
                if (isRoot && subFolder.Name is "assets" or "versions" or "libraries")
                    continue;
                if (subFolder.Name is "structureCacheV1"
                    or ".fabric"
                    or ".git"
                    or "avatar-cache"
                    or "cosmetic-cache")
                    continue;
                SearchFolder(subFolder, false);
            }

            foreach (var entry in folder.EnumerateFiles())
            {
                var relativePath = entry.FullName[pathIndie.Length..];
                var matchPath = relativePath
                    .Replace(Path.DirectorySeparatorChar, '\\')
                    .Replace('/', '\\');
                var shouldKeep = false;
                foreach (var (regex, include) in rules)
                    if (regex.IsMatch(matchPath))
                        shouldKeep = include;
                if (!shouldKeep)
                    continue;

                var targetPath = Path.Combine(overridesFolder, relativePath);
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);
                File.Copy(entry.FullName, targetPath, true);
                loader.Progress = Math.Min(0.94d, loader.Progress + 0.002d);
            }
        }

        SearchFolder(new DirectoryInfo(pathIndie), true);

        var baseFolder = includePcl ? cacheFolder : Path.Combine(cacheFolder, "modpack");
        foreach (var line in extraFiles)
        {
            var normalized = line
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (normalized.EndsWith(Path.DirectorySeparatorChar))
            {
                if (Directory.Exists(normalized))
                {
                    var target = Path.Combine(
                        baseFolder,
                        Path.GetFileName(normalized.TrimEnd(Path.DirectorySeparatorChar)));
                    CopyDirectory(normalized, target);
                }
                else
                {
                    HintService.Show(
                        Hints.Error(Lang.Text("Instance.Export.Config.FolderNotFound", line)));
                }
            }
            else if (File.Exists(normalized))
            {
                File.Copy(normalized, Path.Combine(baseFolder, Path.GetFileName(normalized)), true);
            }
            else
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Instance.Export.Config.FileNotFound", line)));
            }
        }

        var pclFolder = Path.Combine(instance.PathInstance, "PCL");
        if (Directory.Exists(pclFolder))
            CopyDirectory(pclFolder, Path.Combine(overridesFolder, "PCL"));

        if (includePcl)
            try
            {
                var launcherPath = Path.Combine(cacheFolder, "Plain Craft Launcher.exe");
                if (File.Exists(Basics.ExecutablePath))
                    File.Copy(Basics.ExecutablePath, launcherPath, true);
                if (includePclCustom)
                {
                    var exeFolder = Basics.ExecutableDirectory;
                    var pclSource = Path.Combine(exeFolder, "PCL");
                    var pclTarget = Path.Combine(cacheFolder, "PCL");
                    foreach (var name in new[]
                             {
                                 "Pictures",
                                 "Musics"
                             })
                    {
                        var source = Path.Combine(pclSource, name);
                        if (Directory.Exists(source))
                            CopyDirectory(source, Path.Combine(pclTarget, name));
                    }

                    foreach (var name in new[]
                             {
                                 "Custom.xaml",
                                 "Setup.ini",
                                 "hints.txt",
                                 "Logo.png"
                             })
                    {
                        var source = Path.Combine(pclSource, name);
                        if (File.Exists(source))
                        {
                            Directory.CreateDirectory(pclTarget);
                            File.Copy(source, Path.Combine(pclTarget, name), true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "复制启动器文件到整合包失败", LauncherLogLevel.Developer);
            }

        loader.Progress = 0.97d;
    }

    private static void CreateArchive(
        McInstance instance,
        string cacheFolder,
        string packPath,
        string packName,
        string packVersion,
        List<(Regex Regex, bool Include)> rules,
        bool includePcl,
        bool bundleFiles,
        bool modrinthMode,
        Loading.LoaderTask<int, int> loader)
    {
        var modpackFolder = Path.Combine(cacheFolder, "modpack");
        var overridesFolder = Path.Combine(modpackFolder, "overrides");

        var files = new JsonArray();
        if (!bundleFiles)
            try
            {
                files = FetchHostedFiles(overridesFolder, modrinthMode);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "联网获取资源下载信息失败，改为将资源文件直接打包", LauncherLogLevel.Hint);
                files = [];
            }

        loader.Progress = 0.2d;

        var dependencies = new JsonObject
        {
            {
                "minecraft", instance.Info.VanillaName
            }
        };
        if (instance.Info.HasForge)
            dependencies.Add("forge", instance.Info.Forge);
        if (instance.Info.HasFabric)
            dependencies.Add("fabric-loader", instance.Info.Fabric);
        if (instance.Info.HasNeoForge)
            dependencies.Add("neoforge", instance.Info.NeoForge);

        var resultJson = new JsonObject
        {
            {
                "game", "minecraft"
            },
            {
                "formatVersion", 1
            },
            {
                "versionId", packVersion
            },
            {
                "name", packName
            },
            {
                "summary", instance.Desc
            },
            {
                "files", files
            },
            {
                "dependencies", dependencies
            }
        };
        Directory.CreateDirectory(modpackFolder);
        File.WriteAllText(
            Path.Combine(modpackFolder, "modrinth.index.json"),
            resultJson.ToJsonString(
                new JsonSerializerOptions(JsonCompat.SerializerOptions)
                {
                    WriteIndented = true
                }));

        Directory.CreateDirectory(Path.GetDirectoryName(packPath) ?? string.Empty);
        if (File.Exists(packPath))
            File.Delete(packPath);

        if (includePcl)
        {
            var mrpackPath = Path.Combine(cacheFolder, "modpack.mrpack");
            if (File.Exists(mrpackPath))
                File.Delete(mrpackPath);
            ZipFile.CreateFromDirectory(modpackFolder, mrpackPath);
            loader.Progress = 0.6d;
            Directory.Delete(modpackFolder, true);
            ZipFile.CreateFromDirectory(cacheFolder, packPath);
        }
        else
        {
            ZipFile.CreateFromDirectory(modpackFolder, packPath);
        }

        loader.Progress = 0.9d;
        try
        {
            Directory.Delete(cacheFolder, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除导出临时目录失败", LauncherLogLevel.Developer);
        }
    }

    private static JsonArray FetchHostedFiles(string overridesFolder, bool modrinthMode)
    {
        var files = new JsonArray();
        if (!Directory.Exists(overridesFolder))
            return files;

        var archiveExtensions = new[]
        {
            ".zip",
            ".rar",
            ".jar",
            ".disabled",
            ".old"
        };
        var candidates = Directory
            .EnumerateFiles(overridesFolder, "*", SearchOption.AllDirectories)
            .Select(file => (File: file,
                Relative: Path.GetRelativePath(overridesFolder, file).Replace('\\', '/')))
            .Where(pair =>
                archiveExtensions.Contains(Path.GetExtension(pair.File).ToLowerInvariant())
                && new[]
                {
                    "mods/",
                    "packs/",
                    "openloader/",
                    "resource"
                }.Any(prefix =>
                    pair.Relative.Contains(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 0)
            return files;

        var infos =
            new List<(string Path, string Relative, string ModrinthHash, uint CurseForgeHash)>();
        foreach (var (file, relative) in candidates)
            try
            {
                var info = new LocalComp.LocalCompFile(file);
                infos.Add((file, relative, info.ModrinthHash, info.CurseForgeHash));
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, $"读取资源文件信息失败：{file}", LauncherLogLevel.Developer);
            }

        if (infos.Count == 0)
            return files;

        var downloads = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        try
        {
            var hashes = infos.Select(info => $"\"{info.ModrinthHash}\"");
            var body = $"{{\"hashes\": [{string.Join(",", hashes)}], \"algorithm\": \"sha1\"}}";
            var response = JsonNode.Parse(
                GameDownload.DlModRequest(
                    "https://api.modrinth.com/v2/version_files",
                    "POST",
                    body,
                    "application/json"));
            if (response is JsonObject mapping)
                foreach (var info in infos)
                {
                    if (mapping[info.ModrinthHash] is not JsonObject version)
                        continue;
                    var url = version["files"]?[0]?["url"]?.ToString();
                    var sha1 = version["files"]?[0]?["hashes"]?["sha1"]?.ToString();
                    if (string.IsNullOrEmpty(url)
                        || !string.Equals(
                            sha1,
                            info.ModrinthHash,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!downloads.TryGetValue(info.ModrinthHash, out var list))
                    {
                        list = [];
                        downloads[info.ModrinthHash] = list;
                    }

                    list.Add(url);
                }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "从 Modrinth 获取本地资源信息失败", LauncherLogLevel.Hint);
        }

        if (!modrinthMode)
            try
            {
                var fingerprints = infos.Select(info => info.CurseForgeHash.ToString());
                var body = $"{{\"fingerprints\": [{string.Join(",", fingerprints)}]}}";
                var response = JsonNode.Parse(
                    GameDownload.DlModRequest(
                        "https://api.curseforge.com/v1/fingerprints/432/",
                        "POST",
                        body,
                        "application/json"));
                var matches = response?["data"]?["exactMatches"] as JsonArray;
                if (matches is not null)
                    foreach (var match in matches)
                    {
                        if (match is not JsonObject matchObject)
                            continue;
                        var fileJson = matchObject["file"] as JsonObject;
                        if (fileJson is null)
                            continue;
                        var url = fileJson["downloadUrl"]?.ToString();
                        if (string.IsNullOrEmpty(url))
                            continue;
                        var fingerprint = fileJson["fileFingerprint"]?.ToObject<uint>();
                        var info = infos.FirstOrDefault(item => item.CurseForgeHash == fingerprint);
                        if (info.Path is null)
                            continue;
                        if (!downloads.TryGetValue(info.ModrinthHash, out var list))
                        {
                            list = [];
                            downloads[info.ModrinthHash] = list;
                        }

                        list.Add(Comp.CompFile.HandleCurseForgeDownloadUrls(url));
                    }
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "从 CurseForge 获取本地资源信息失败", LauncherLogLevel.Hint);
            }

        foreach (var info in infos)
        {
            if (!downloads.TryGetValue(info.ModrinthHash, out var urls) || urls.Count == 0)
                continue;
            var downloadArray = new JsonArray();
            foreach (var url in urls.OrderByDescending(u => u.Contains("modrinth.com")))
                downloadArray.Add(url);
            files.Add(
                new JsonObject
                {
                    {
                        "path", info.Relative
                    },
                    {
                        "hashes", new JsonObject
                        {
                            {
                                "sha1", info.ModrinthHash
                            },
                            {
                                "sha512", ComputeSha512(info.Path)
                            }
                        }
                    },
                    {
                        "downloads", downloadArray
                    },
                    {
                        "fileSize", new FileInfo(info.Path).Length
                    }
                });
            File.Delete(info.Path);
        }

        return files;
    }

    private static string ComputeSha512(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA512.HashData(stream)).ToLowerInvariant();
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var folder in Directory.EnumerateDirectories(source))
            CopyDirectory(folder, Path.Combine(target, Path.GetFileName(folder)));
    }

    #endregion
}