using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>导出选项：标题、说明、匹配规则与显示/勾选条件。</summary>
public class ExportOption
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Rules { get; init; } = string.Empty;

    public string ShowRules { get; init; } = string.Empty;

    public bool DefaultChecked { get; init; }

    public bool RequireModLoader { get; init; }

    public bool RequireOptiFine { get; init; }

    public bool RequireModLoaderOrOptiFine { get; init; }

    public bool PropagateCheckToChildren { get; init; }

    public bool IsOtherFolders { get; init; }

    public bool IsLauncher { get; init; }

    public bool IsLauncherCustom { get; init; }

    public bool IsEnabled { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    public bool IsChecked { get; set; }

    public bool IsIndented { get; init; }

    public ObservableCollection<ExportOption> Children { get; } = [];
}

/// <summary>实例导出页的视图模型：配置项树、动态子项、配置文件与整合包导出。</summary>
public partial class PageInstanceExportViewModel : ViewModelBase
{
    private const string Separator =
        "==============================================================";

    private static readonly string[] SubOptionBlackList =
        ["Quark Programmer Art.zip", "+ EuphoriaPatches_"];

    private readonly List<string> _extraFiles = [];

    private readonly List<string> _rulesOverrides = [];

    private string _configPackPath = string.Empty;
    private string _lastConfigPath = string.Empty;

    [ObservableProperty] public partial string PackName { get; set; } = string.Empty;

    [ObservableProperty] public partial string PackVersion { get; set; } = string.Empty;

    [ObservableProperty] public partial bool BundleFiles { get; set; }

    [ObservableProperty] public partial bool Modrinth { get; set; }

    [ObservableProperty] public partial bool IncludePcl { get; set; }

    [ObservableProperty] public partial bool IncludePclCustom { get; set; } = true;

    [ObservableProperty] public partial bool HasOverrides { get; set; }

    [ObservableProperty] public partial bool HasOptiFine { get; set; }

    [ObservableProperty] public partial bool IsExporting { get; set; }

    [ObservableProperty] public partial bool HasOtherFolders { get; set; }

    [ObservableProperty] public partial bool ResetOverridesVisible { get; set; }

    [ObservableProperty]
    public partial string OptionsTitle { get; set; } = Lang.Text("Instance.Export.OptionListTitle");

    [ObservableProperty] public partial string? ToastMessage { get; set; }

    [ObservableProperty] public partial bool ToastIsError { get; set; }

    [ObservableProperty] public partial int ToastVersion { get; set; }

    public McInstance? Instance { get; private set; }

    public string LastInstancePath { get; private set; } = string.Empty;

    public bool ShowBundleHint => BundleFiles;

    public bool BundleEnabled => !Modrinth;

    public bool ModrinthEnabled => !BundleFiles;

    public bool PclEnabled => !Modrinth && PlatformRuntime.Current.Capabilities.SupportsBundledLauncher;

    public bool OptionsVisible => !HasOverrides;

    public ObservableCollection<ExportOption> Options { get; } = [];

    public bool AttachInstance(McInstance? instance)
    {
        var changed = !string.Equals(
            Instance?.PathInstance,
            instance?.PathInstance,
            StringComparison.OrdinalIgnoreCase);
        Instance = instance;
        return changed;
    }

    /// <summary>重置包名、配置覆盖与选项状态，并重新构建整棵配置项树。</summary>
    public void RefreshAll()
    {
        if (Instance is null)
            return;
        LastInstancePath = Instance.PathInstance ?? string.Empty;
        _lastConfigPath = States.System.ExportConfigPath ?? string.Empty;
        _rulesOverrides.Clear();
        _extraFiles.Clear();
        _configPackPath = string.Empty;
        PackName = string.Empty;
        PackVersion = string.Empty;
        IncludePcl = false;
        IncludePclCustom = true;
        HasOptiFine = Instance.Info.HasOptiFine;
        ResetConfigOverrides();
        BuildOptions();
        RefreshAllOptionsUi();
    }

    /// <summary>只重建动态子项与显隐状态，保留当前包名/版本输入。</summary>
    public void RefreshSubOptions()
    {
        if (Instance is null)
            return;
        BuildOptions();
        RefreshAllOptionsUi();
    }

    public void SetOptionChecked(ExportOption option, bool value)
    {
        option.IsChecked = value;
        if (option.IsLauncher)
            IncludePcl = value;
        if (option.IsLauncherCustom)
            IncludePclCustom = value;
        if (!option.PropagateCheckToChildren)
            return;
        foreach (var child in option.Children)
            child.IsChecked = value;
    }

    public void ResetConfigOverrides()
    {
        _rulesOverrides.Clear();
        _extraFiles.Clear();
        _configPackPath = string.Empty;
        HasOverrides = false;
        ResetOverridesVisible = false;
        OptionsTitle = Lang.Text("Instance.Export.OptionListTitle");
    }

    partial void OnBundleFilesChanged(bool value)
    {
        if (value)
            Modrinth = false;
        OnPropertyChanged(nameof(ShowBundleHint));
        OnPropertyChanged(nameof(BundleEnabled));
        OnPropertyChanged(nameof(ModrinthEnabled));
    }

    partial void OnModrinthChanged(bool value)
    {
        if (value)
        {
            BundleFiles = false;
            IncludePcl = false;
        }

        OnPropertyChanged(nameof(BundleEnabled));
        OnPropertyChanged(nameof(ModrinthEnabled));
        OnPropertyChanged(nameof(PclEnabled));
    }

    partial void OnHasOverridesChanged(bool value)
    {
        OnPropertyChanged(nameof(OptionsVisible));
    }

    partial void OnIncludePclChanged(bool value)
    {
        SyncLauncherOptions();
    }

    partial void OnIncludePclCustomChanged(bool value)
    {
        SyncLauncherOptions();
    }

    public bool ExportConfig()
    {
        try
        {
            var configPath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Export.SelectFileLocation"),
                "export_config.txt",
                Lang.Text("Instance.Export.Config.FileFilter"),
                _lastConfigPath);
            if (string.IsNullOrEmpty(configPath))
                return false;
            _lastConfigPath = configPath;
            States.System.ExportConfigPath = configPath;

            var lines = new List<string>
            {
                $"Name:{PackName}",
                $"Version:{PackVersion}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.IncludeLauncher")}",
                $"IncludeLauncher:{IncludePcl}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.IncludeLauncherCustom")}",
                $"IncludeLauncherCustom:{IncludePclCustom}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.BundleFiles")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.BundleFiles2")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.BundleFiles3")}",
                $"DontCheckHostedAssets:{BundleFiles}",
                string.Empty,
                $"# {Lang.Text("Instance.Export.Config.Comment.Modrinth")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.Modrinth2")}",
                $"# {Lang.Text("Instance.Export.Config.Comment.Modrinth3")}",
                $"ModrinthUploadMode:{Modrinth}",
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
            ShowToast(Lang.Text("Instance.Export.SaveSuccess", configPath), false);
            InstancePageHelper.OpenFolder(configPath);
            return true;
        }
        catch (Exception ex)
        {
            ReportError("Instance.Export.Error.OperationFailed", ex);
            return false;
        }
    }

    public bool ImportConfig()
    {
        try
        {
            var configPath = SystemDialogs.SelectFile(
                Lang.Text("Instance.Export.Config.FileFilter"),
                Lang.Text("Instance.Export.SelectConfigFile"),
                _lastConfigPath);
            return !string.IsNullOrEmpty(configPath) && ReadConfigFile(configPath);
        }
        catch (Exception ex)
        {
            ReportError("Instance.Export.Error.OperationFailed", ex);
            return false;
        }
    }

    public bool ReadConfigFile(string configPath)
    {
        try
        {
            _lastConfigPath = configPath;
            States.System.ExportConfigPath = configPath;
            var content = LauncherCommon.ReadFile(configPath);
            var segments = content.Split(Separator);
            if (segments.Length < 2)
            {
                ShowToast(Lang.Text("Instance.Export.Config.Invalid"), true);
                return false;
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

            PackName = ini.GetValueOrDefault("Name", string.Empty);
            PackVersion = ini.GetValueOrDefault("Version", string.Empty);
            IncludePcl = ReadBool(ini, "IncludeLauncher", false);
            IncludePclCustom = ReadBool(ini, "IncludeLauncherCustom", true);
            Modrinth = ReadBool(ini, "ModrinthUploadMode", false);
            BundleFiles = ReadBool(ini, "DontCheckHostedAssets", false);
            _configPackPath = ini.GetValueOrDefault("PackPath", string.Empty);

            _rulesOverrides.Clear();
            _rulesOverrides.AddRange(
                segments[1].Replace("\r", string.Empty).Split('\n'));
            _extraFiles.Clear();
            if (segments.Length > 2)
                _extraFiles.AddRange(
                    segments[2].Replace("\r", string.Empty).Split('\n'));

            HasOverrides = true;
            ResetOverridesVisible = true;
            OptionsTitle =
                $"{Lang.Text("Instance.Export.OptionListTitle")}: {Lang.Text("Instance.Export.OptionList.FromConfig")}";
            ShowToast(Lang.Text("Instance.Export.ReadSuccess", configPath), false);
            return true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"读取导出配置失败：{configPath}", LauncherLogLevel.Feedback);
            ReportError("Instance.Export.Error.OperationFailed", ex);
            return false;
        }
    }

    /// <summary>开始导出整合包；返回是否成功启动导出任务。</summary>
    public async Task<bool> StartExportAsync()
    {
        if (IsExporting || Instance is null)
            return false;
        IsExporting = true;
        try
        {
            var instance = Instance;
            var packName = string.IsNullOrWhiteSpace(PackName)
                ? instance.Name
                : PackName.Trim();
            var packVersion = string.IsNullOrWhiteSpace(PackVersion)
                ? "1.0.0"
                : PackVersion.Trim();
            var loaderName = Lang.Text("Instance.Export.ExportTask.Prefix") + packName;
            if (Loading.loaderTaskbar.Any(loader => loader.name == loaderName))
            {
                PageRouter.Navigate(PageType.TaskManager);
                return false;
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
                if (!Modrinth)
                    extensions.Add(Lang.Text("Instance.Export.ZipFilter"));
                if (!IncludePcl)
                    extensions.Add(Lang.Text("Instance.Export.MrpackFilter"));
                packPath = SystemDialogs.SelectSaveFile(
                    Lang.Text("Instance.Export.SelectSaveLocation"),
                    packName
                    + (string.IsNullOrWhiteSpace(PackVersion)
                        ? string.Empty
                        : $" {packVersion}"),
                    string.Join('|', extensions));
            }

            if (string.IsNullOrEmpty(packPath))
                return false;

            var includePcl = IncludePcl;
            var includePclCustom = includePcl && IncludePclCustom;
            var bundleFiles = BundleFiles;
            var modrinthMode = Modrinth;
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
                            ShowToast(
                                loaderName + Lang.Text("Common.Status.Success"),
                                false);
                            InstancePageHelper.OpenFolder(
                                Path.GetDirectoryName(targetPath) ?? string.Empty);
                            break;
                        case LoadState.Failed:
                            ShowToast(
                                Lang.Text("Instance.Export.Error.OperationFailed"),
                                true);
                            break;
                        case LoadState.Aborted:
                            ShowToast(Lang.Text("Common.Status.Cancelled"), false);
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
            return true;
        }
        catch (Exception ex)
        {
            ReportError("Instance.Export.Error.OperationFailed", ex);
            return false;
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void BuildOptions()
    {
        Options.Clear();
        if (Instance is null)
            return;

        var basic = AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Game"),
                DefaultChecked = true
            });
        basic.IsEnabled = false;

        AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.GameSettings"),
                Description = Lang.Text("Instance.Export.Option.GameSettings.Desc"),
                Rules = "options.txt|configureddefaults/",
                DefaultChecked = true
            });
        AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.GameProfile"),
                Description = Lang.Text("Instance.Export.Option.GameProfile.Desc"),
                Rules = "hotbar.nbt|command_history.txt"
            });
        AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.OptiFine"),
                Rules = "optionsof.txt|optionsshaders.txt",
                DefaultChecked = true,
                RequireOptiFine = true
            });

        var mod = AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Mod"),
                Description = Lang.Text("Instance.Export.Option.Mod.Desc"),
                Rules =
                    "mods/|!mods/*.disabled|!mods/*.old|!mods/.connector/|coremods/|lib/|!mods/mcef-libraries/|!mods/mcef-cache/",
                DefaultChecked = true,
                RequireModLoader = true
            });
        AddOption(
            mod.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.DisabledMod"),
                Rules = "mods/*.disabled|mods/*.old"
            });
        AddOption(
            mod.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ImportantData"),
                Description = Lang.Text("Instance.Export.Option.ImportantData.Desc"),
                Rules =
                    "addons/|multiblocked/|modpack-update-checker/|global_packs/|global_resource_packs/|global_data_packs/|optional_data_packs/|maps/|icon.png|mods-resourcepacks/|matmos/|resource_assorts/|resource_assorts.json|patchouli_books/|datapacks/|kubejs*/|!kubejs*/probe/|!kubejs*/exported/|!kubejs*/jsconfig.json|!kubejs*/README.txt|openloader/|worldshape/|resources/|scripts/|structures/|fontfiles/|oresources/|packmenu/|craftpresence/|pointblanks/|template*/|!template*/playerdata/|!template*/stats/",
                DefaultChecked = true
            });
        AddOption(
            mod.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ModSettings"),
                Rules =
                    "config/|!config/accountsx/|!config/jei/world/|!config/worldedit/|config/worldedit/worldedit.properties|!config/spark/|config/spark/config.json|defaultconfigs/|journeymap/config/|journeymap/server/|TrashSlotSaveState.json|customfov.txt|gg.essential.mod/|essential/|!essential/*/|!essential/*.jar*|!essential/screenshot-checksum-caches.json|!essential/microsoft_accounts.json|paragliderSettings.nbt|local/client_config.json|local/ftbl.json|local/client/sidebar_buttons.json|local/client/ftbutilities.cfg|local/client/ftblib.cfg|local/client/xencraft.cfg|liteloader.properties|default_reference.xml|CustomSkinLoader/CustomSkinLoader.json",
                DefaultChecked = true
            });
        AddOption(
            mod.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Maps"),
                Description = Lang.Text("Instance.Export.Option.Maps.Desc"),
                Rules = "journeymap/data/|xaero/|XaeroWaypoints/|XaeroWorldMap/"
            });
        AddOption(
            mod.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Jei"),
                Description = Lang.Text("Instance.Export.Option.Jei.Desc"),
                Rules = "config/jei/world/"
            });
        AddOption(
            mod.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Emi"),
                Description = Lang.Text("Instance.Export.Option.Emi.Desc"),
                Rules = "emi.json"
            });
        AddOption(
            mod.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Patchouli"),
                Description = Lang.Text("Instance.Export.Option.Patchouli.Desc"),
                Rules = "patchouli_data.json"
            });

        var resourcePacks = AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ResourcePacks"),
                Description = Lang.Text("Instance.Export.Option.ResourcePacks.Desc"),
                ShowRules = "resourcepacks/|texturepacks/",
                DefaultChecked = true
            });
        LoadSubOptions(resourcePacks, true, true, "resourcepacks", "texturepacks");

        var shaderPacks = AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.ShaderPacks"),
                ShowRules = "shaderpacks/",
                DefaultChecked = true,
                RequireModLoaderOrOptiFine = true
            });
        LoadSubOptions(shaderPacks, true, true, "shaderpacks");

        AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Screenshots"),
                Rules = "screenshots/"
            });
        AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Structures"),
                Description = Lang.Text("Instance.Export.Option.Structures.Desc"),
                Rules = "schematics/"
            });
        AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Replay"),
                Description = Lang.Text("Instance.Export.Option.Replay.Desc"),
                Rules = "replay_recordings/|replay_videos/",
                RequireModLoader = true
            });

        var saves = AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Saves"),
                Description = Lang.Text("Instance.Export.Option.Saves.Desc"),
                ShowRules = "saves/"
            });
        LoadSubOptions(saves, false, true, "saves");

        AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Servers"),
                Rules = "servers.dat"
            });

        var otherFolders = AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.OtherFolders"),
                Description = Lang.Text("Instance.Export.Option.OtherFolders.Desc"),
                PropagateCheckToChildren = true,
                IsOtherFolders = true
            });
        LoadOtherFolders(otherFolders);
        HasOtherFolders = otherFolders.Children.Count > 0;

        var launcher = AddOption(
            Options,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.Launcher"),
                Description = Lang.Text("Instance.Export.Option.Launcher.Desc"),
                IsLauncher = true
            });
        AddOption(
            launcher.Children,
            new ExportOption
            {
                Title = Lang.Text("Instance.Export.Option.LauncherCustom"),
                Description = Lang.Text("Instance.Export.Option.LauncherCustom.Desc"),
                DefaultChecked = true,
                IsLauncherCustom = true
            });
    }

    private void LoadSubOptions(
        ExportOption parent,
        bool acceptCompressedFile,
        bool acceptFolder,
        params string[] folders)
    {
        if (Instance is null)
            return;
        parent.Children.Clear();
        foreach (var folder in folders)
        {
            var targetFolder = new DirectoryInfo(Path.Combine(Instance.PathIndie, folder));
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
                    parent.Children.Add(
                        CreateSubOption(
                            file.Name,
                            EscapePattern($"{folder}/{file.Name}"),
                            true));
                    if (folder != "shaderpacks")
                        continue;
                    var shaderConfig = new FileInfo(
                        Path.Combine(
                            file.Directory!.FullName,
                            $"{file.Name}.txt"));
                    if (shaderConfig.Exists)
                        parent.Children.Add(
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
                var description = folder == "saves"
                    ? Lang.Date(subFolder.LastWriteTime, "g")
                    : string.Empty;
                parent.Children.Add(
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
                    parent.Children.Add(
                        CreateSubOption(
                            shaderConfig.Name,
                            EscapePattern($"{folder}/{shaderConfig.Name}"),
                            true,
                            Lang.Text("Instance.Export.Config.ShaderConfigSuffix"),
                            true));
            }
        }
    }

    private static ExportOption CreateSubOption(
        string title,
        string rules,
        bool defaultChecked,
        string description = "",
        bool indent = false)
    {
        return new ExportOption
        {
            Title = title,
            Rules = rules,
            DefaultChecked = defaultChecked,
            Description = description,
            IsIndented = indent
        };
    }

    private void LoadOtherFolders(ExportOption parent)
    {
        if (Instance is null)
            return;
        parent.Children.Clear();

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

        var rootDir = new DirectoryInfo(Instance.PathIndie);
        if (!rootDir.Exists)
            return;

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

            parent.Children.Add(
                new ExportOption
                {
                    Title = subDir.Name,
                    Rules = EscapePattern($"{subDir.Name}/")
                });
        }
    }

    private static ExportOption AddOption(
        ObservableCollection<ExportOption> parent,
        ExportOption option)
    {
        parent.Add(option);
        return option;
    }

    private void RefreshAllOptionsUi()
    {
        if (Instance is null)
            return;

        var allEntries = new List<string>();
        var pathIndie = Instance.PathIndie;
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
                            .Select(f => $"{subFolder.Name}\\{f.Name}"));
                    allEntries.AddRange(
                        subFolder
                            .EnumerateDirectories()
                            .Where(IsValidDirectory)
                            .Select(d => $@"{subFolder.Name}\{d.Name}\"));
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "扫描实例目录以判断导出选项失败", LauncherLogLevel.Developer);
        }

        foreach (var option in EnumerateOptions(Options))
        {
            if (option.IsOtherFolders)
            {
                option.IsVisible = option.Children.Count > 0;
                option.IsChecked = option.IsVisible && option.DefaultChecked;
                continue;
            }

            if (string.IsNullOrEmpty(option.Rules) && string.IsNullOrEmpty(option.ShowRules))
            {
                option.IsVisible = true;
                if (option.IsEnabled)
                    option.IsChecked = option.DefaultChecked;
                continue;
            }

            var pass = IsOptionVisible(option, allEntries, pathIndie);
            option.IsVisible = pass;
            if (option.IsEnabled)
                option.IsChecked = option.DefaultChecked && pass;
        }

        SyncLauncherOptions();
    }

    private void SyncLauncherOptions()
    {
        var launcher = EnumerateOptions(Options).FirstOrDefault(option => option.IsLauncher);
        if (launcher is null)
            return;

        var canIncludeLauncher = !Modrinth && PlatformRuntime.Current.Capabilities.SupportsBundledLauncher;
        launcher.IsEnabled = canIncludeLauncher;
        if (!canIncludeLauncher && IncludePcl)
            IncludePcl = false;
        launcher.IsChecked = IncludePcl;
        var custom = launcher.Children.FirstOrDefault(option => option.IsLauncherCustom);
        custom?.IsChecked = IncludePclCustom;
    }

    private bool IsOptionVisible(
        ExportOption option,
        List<string> allEntries,
        string pathIndie)
    {
        if (Instance is null)
            return false;
        if (option.RequireOptiFine && !Instance.Info.HasOptiFine)
            return false;
        if (option.RequireModLoader && !Instance.Modable)
            return false;
        if (option.RequireModLoaderOrOptiFine
            && !Instance.Info.HasOptiFine
            && !Instance.Modable)
            return false;

        var rules = string.IsNullOrEmpty(option.Rules) ? option.ShowRules : option.Rules;
        return StandardizeRules(rules.Split('|'), true)
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

    private static IEnumerable<ExportOption> EnumerateOptions(
        IEnumerable<ExportOption> options)
    {
        foreach (var option in options)
        {
            yield return option;
            foreach (var child in EnumerateOptions(option.Children))
                yield return child;
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
        foreach (var option in EnumerateOptions(Options))
        {
            if (!option.IsChecked || string.IsNullOrEmpty(option.Rules))
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
                switch (ch)
                {
                    case '*' when i + 1 < rule.Length && rule[i + 1] == '*':
                        pattern.Append(".*");
                        i++;
                        break;

                    case '*':
                        pattern.Append(@"[^\\]*");
                        break;

                    case '?':
                        pattern.Append(@"[^\\]");
                        break;

                    case '#':
                        pattern.Append("[0-9]");
                        break;

                    default:
                        pattern.Append(Regex.Escape(ch.ToString()));
                        break;
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
                .Replace(@"\*\*", ".*")
                .Replace("\\*", @"[^\\]*")
                .Replace("\\?", @"[^\\]")
                .Replace("#", "[0-9]")}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return regex.IsMatch(input.Replace('/', '\\'));
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> ini, string key, bool fallback)
    {
        return ini.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private void CopyContent(
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
                    ShowToast(
                        Lang.Text("Instance.Export.Config.FolderNotFound", line),
                        true);
                }
            }
            else if (File.Exists(normalized))
            {
                File.Copy(normalized, Path.Combine(baseFolder, Path.GetFileName(normalized)), true);
            }
            else
            {
                ShowToast(
                    Lang.Text("Instance.Export.Config.FileNotFound", line),
                    true);
            }
        }

        var pclFolder = Path.Combine(instance.PathInstance, "PCL");
        if (Directory.Exists(pclFolder))
            CopyDirectory(pclFolder, Path.Combine(overridesFolder, "PCL"));

        if (includePcl && PlatformRuntime.Current.Capabilities.SupportsBundledLauncher)
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
        return;

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

    private void ShowToast(string message, bool isError)
    {
        ToastMessage = message;
        ToastIsError = isError;
        ToastVersion++;
        HintService.Show(isError ? Hints.Error(message) : Hints.Success(message));
    }

    private static void ReportError(string titleKey, Exception ex)
    {
        InstancePageHelper.ReportError(titleKey, ex);
    }
}