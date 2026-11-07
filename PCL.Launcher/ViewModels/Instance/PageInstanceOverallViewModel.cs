using PCL.Core.Platform;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>实例总览页的视图模型。</summary>
public partial class PageInstanceOverallViewModel : ViewModelBase
{
    private int _infoRevision;

    [ObservableProperty] public partial int CardTypeIndex { get; set; }

    [ObservableProperty] public partial bool IsCustomLogo { get; set; }

    [ObservableProperty] public partial string LogoTag { get; set; } = string.Empty;

    [ObservableProperty] public partial bool PatchButtonEnabled { get; set; } = true;

    [ObservableProperty] public partial string InfoLoadingText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool InfoLoadingVisible { get; set; }

    [ObservableProperty] public partial bool HasInfoError { get; set; }

    [ObservableProperty] public partial bool ModsFolderVisible { get; set; } = true;

    [ObservableProperty] public partial bool ModpackVisible { get; set; }

    [ObservableProperty] public partial string StarText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CardTypeComboEnabled { get; set; } = true;

    [ObservableProperty] public partial bool DescriptionButtonEnabled { get; set; } = true;

    [ObservableProperty] public partial bool RenameButtonEnabled { get; set; } = true;

    [ObservableProperty] public partial bool RestoreButtonEnabled { get; set; } = true;

    [ObservableProperty] public partial bool DeleteButtonEnabled { get; set; } = true;

    public McInstance? Instance { get; private set; }

    public Comp.CompProject? ModpackProject { get; private set; }

    public List<InstanceInfoRow> InfoRows { get; } = [];

    public event Action? InstanceChanged;

    public void SetInstance(McInstance? instance)
    {
        Instance = instance;
    }

    public void RefreshDisplay()
    {
        if (Instance is null)
            return;
        var path = Instance.PathInstance;
        CardTypeIndex = Math.Clamp(States.Instance.CardType[path], 0, 5);
        StarText = Instance.IsStar
            ? Lang.Text("Instance.Overall.Unfavorite")
            : Lang.Text("Instance.Overall.Favorite");
        ModsFolderVisible = Instance.Modable;
        LogoTag = States.Instance.LogoPath[path] ?? string.Empty;
        IsCustomLogo = States.Instance.IsLogoCustom[path];
    }

    [RelayCommand]
    public async Task LoadInfoAsync()
    {
        if (Instance is null)
            return;
        var instance = Instance;
        var revision = ++_infoRevision;
        InfoLoadingVisible = true;
        HasInfoError = false;
        ModpackVisible = false;
        InfoRows.Clear();

        Comp.CompProject? modpackProject = null;
        try
        {
            var modpackId = States.Instance.ModpackId[instance.PathInstance];
            if (!string.IsNullOrWhiteSpace(modpackId))
                modpackProject = await Task.Run(() =>
                {
                    try
                    {
                        var projects = Comp.CompRequest.GetCompProjectsByIds([modpackId]);
                        return projects.Count > 0 ? projects[0] : null;
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(ex, "加载实例整合包信息失败", LauncherLogLevel.Developer);
                        return null;
                    }
                });
        }
        finally
        {
            if (revision == _infoRevision && ReferenceEquals(Instance, instance))
            {
                ModpackProject = modpackProject;
                ModpackVisible = modpackProject is not null;
                InfoRows.Clear();
                InfoRows.AddRange(BuildInfoRows(instance));
                InfoLoadingVisible = false;
                HasInfoError = !string.IsNullOrEmpty(Error);
            }
        }
    }

    public static IReadOnlyList<InstanceInfoRow> BuildInfoRows(McInstance instance)
    {
        var rows = new List<InstanceInfoRow>();
        var instanceInfo = instance.Info;
        var launchCount = States.Instance.LaunchCount[instance.PathInstance];
        rows.Add(
            new InstanceInfoRow(
                Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
                launchCount == 0
                    ? Lang.Text("Instance.Overall.Info.LaunchCount.Never")
                    : Lang.Text("Instance.Overall.Info.LaunchCount.Count", launchCount),
                $"{LauncherPaths.ImageRoot}Blocks/{(launchCount == 0 ? "RedstoneLampOff" : "RedstoneLampOn")}.png"));

        if (!string.IsNullOrWhiteSpace(States.Instance.ModpackVersion[instance.PathInstance]))
            rows.Add(
                new InstanceInfoRow(
                    Lang.Text("Instance.Overall.Info.ModpackVersion"),
                    States.Instance.ModpackVersion[instance.PathInstance],
                    $"{LauncherPaths.ImageRoot}Blocks/CommandBlock.png"));
        rows.Add(
            new InstanceInfoRow(
                "Minecraft",
                McInstanceInfo.GetVanillaNameDisplay(instanceInfo.VanillaName),
                $"{LauncherPaths.ImageRoot}Blocks/Grass.png"));
        if (instanceInfo.HasForge)
            rows.Add(CreateLoaderRow("Forge", instanceInfo.Forge, "Anvil"));
        if (instanceInfo.HasNeoForge)
            rows.Add(CreateLoaderRow("NeoForge", instanceInfo.NeoForge, "NeoForge"));
        if (instanceInfo.HasCleanroom)
            rows.Add(CreateLoaderRow("Cleanroom", instanceInfo.Cleanroom, "Cleanroom"));
        if (instanceInfo.HasFabric)
            rows.Add(CreateLoaderRow("Fabric", instanceInfo.Fabric, "Fabric"));
        if (instanceInfo.HasQuilt)
            rows.Add(CreateLoaderRow("Quilt", instanceInfo.Quilt, "Quilt"));
        if (instanceInfo.HasOptiFine)
            rows.Add(CreateLoaderRow("OptiFine", instanceInfo.OptiFine, "GrassPath"));
        if (instanceInfo.HasLiteLoader)
            rows.Add(
                new InstanceInfoRow(
                    "LiteLoader",
                    Lang.Text("Instance.Overall.Info.Installed"),
                    $"{LauncherPaths.ImageRoot}Blocks/Egg.png"));
        if (instanceInfo.HasLegacyFabric)
            rows.Add(CreateLoaderRow("Legacy Fabric", instanceInfo.LegacyFabric, "Fabric"));
        if (instanceInfo.HasLabyMod)
            rows.Add(CreateLoaderRow("LabyMod", instanceInfo.LabyMod, "LabyMod"));
        return rows;
    }

    private static InstanceInfoRow CreateLoaderRow(string name, string? version, string logo)
    {
        return new InstanceInfoRow(
            name,
            string.IsNullOrEmpty(version) ? Lang.Text("Minecraft.Version.Unknown") : version,
            $"{LauncherPaths.ImageRoot}Blocks/{logo}.png");
    }

    [RelayCommand]
    private void OpenInstanceFolder()
    {
        if (Instance is not null)
            InstancePageHelper.OpenFolder(Instance.PathInstance);
    }

    [RelayCommand]
    private void OpenSavesFolder()
    {
        if (Instance is null)
            return;
        var path = InstancePageHelper.SavesPath(Instance);
        InstancePageHelper.EnsureDirectory(path);
        InstancePageHelper.OpenFolder(path);
    }

    [RelayCommand]
    private void OpenModsFolder()
    {
        if (Instance is null)
            return;
        var path = InstancePageHelper.ModsPath(Instance);
        InstancePageHelper.EnsureDirectory(path);
        InstancePageHelper.OpenFolder(path);
    }

    public async Task<bool> ApplyCardTypeAsync(int index)
    {
        if (Instance is null || index < 0)
            return false;
        var path = Instance.PathInstance;
        var previous = Math.Clamp(States.Instance.CardType[path], 0, 5);
        CardTypeComboEnabled = false;
        try
        {
            if (index != (int)McInstanceCardType.Hidden)
            {
                States.Instance.CardType[path] = index;
                Instance.displayType = (McInstanceCardType)index;
                InstanceList.mcInstanceListForceRefresh = true;
                PageInstanceLeft.Current?.RefreshForInstanceChange();
                RefreshInstanceList();
                return true;
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
                    CardTypeIndex = previous;
                    return false;
                }

                States.Hint.HideGameInstance = true;
            }

            States.Instance.CardType[path] = (int)McInstanceCardType.Hidden;
            InstanceList.mcInstanceListForceRefresh = true;
            PageInstanceLeft.Current?.RefreshForInstanceChange();
            RefreshInstanceList();
            return true;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return false;
        }
        finally
        {
            CardTypeComboEnabled = true;
        }
    }

    public async Task<bool> SelectLogoAsync(string tag, bool isCustom)
    {
        if (Instance is null)
            return false;
        var instance = Instance;
        var path = instance.PathInstance;
        var customLogoFile = Path.Combine(path, "PCL", "Logo.png");
        string? newLogo;
        try
        {
            if (isCustom)
            {
                var fileName = SystemDialogs.SelectFile(
                    Lang.Text("Instance.Overall.Icon.SelectFile.Filter"),
                    Lang.Text("Instance.Overall.Icon.SelectFile.Title"));
                if (string.IsNullOrEmpty(fileName))
                {
                    RefreshDisplay();
                    return false;
                }

                InstancePageHelper.EnsureDirectory(
                    Path.GetDirectoryName(customLogoFile)!);
                File.Copy(fileName, customLogoFile, true);
                newLogo = Path.Combine("PCL", "Logo.png");
            }
            else
            {
                if (File.Exists(customLogoFile))
                    File.Delete(customLogoFile);
                newLogo = tag;
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
            RefreshDisplay();
            return false;
        }

        try
        {
            States.Instance.LogoPath[path] = newLogo ?? string.Empty;
            States.Instance.IsLogoCustom[path] = !string.IsNullOrEmpty(newLogo);
            ReloadInstance(instance.Name);
            RefreshInstanceList();
            RefreshDisplay();
            InstanceChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return false;
        }
    }

    public async Task RenameAsync()
    {
        if (Instance is null)
            return;
        RenameButtonEnabled = false;
        try
        {
            var instance = Instance;
            var oldName = instance.Name;
            var oldPath = instance.PathInstance;
            var versionsFolder = Path.Combine(
                GameFolders.mcFolderSelected ?? string.Empty,
                "versions");
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
            var tempPath = Path.Combine(versionsFolder, $"{newName}_temp");
            var isCaseChangedOnly = string.Equals(
                newName.ToLowerInvariant(),
                oldName.ToLowerInvariant(),
                StringComparison.Ordinal);

            JsonObject jsonObject;
            try
            {
                var jsonPath = Path.Combine(oldPath, $"{oldName}.json");
                jsonObject = JsonNode.Parse(LauncherCommon.ReadFile(jsonPath)) as JsonObject
                    ?? (JsonObject)instance.JsonObject.DeepClone();
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "重命名读取 Json 时失败", LauncherLogLevel.Developer);
                jsonObject = (JsonObject)instance.JsonObject.DeepClone();
            }

            try
            {
                if (ConfigService.GetProvider(ConfigSource.GameInstance) is
                    DynamicCacheConfigStorage storage)
                    storage.InvalidateCache(oldPath);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "清理重命名前的实例配置缓存失败", LauncherLogLevel.Developer);
            }

            Directory.Move(oldPath, tempPath);
            Directory.Move(tempPath, newPath);
            LauncherCommon.IniClearCache(Path.Combine(instance.PathIndie, "options.txt"));

            var oldNatives = Path.Combine(newPath, $"{oldName}-natives");
            if (Directory.Exists(oldNatives))
            {
                var newNatives = Path.Combine(newPath, $"{newName}-natives");
                if (isCaseChangedOnly)
                {
                    var middle = Path.Combine(newPath, $"{oldName}natives_temp");
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

            var oldJar = Path.Combine(newPath, $"{oldName}.jar");
            if (File.Exists(oldJar))
            {
                var newJar = Path.Combine(newPath, $"{newName}.jar");
                if (isCaseChangedOnly)
                {
                    var middle = Path.Combine(newPath, $"{oldName}_temp.jar");
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
                LauncherCommon.WriteFile(
                    Path.Combine(newPath, $"{newName}.json"),
                    jsonObject.ToString());
                if (!isCaseChangedOnly)
                {
                    var oldJson = Path.Combine(newPath, $"{oldName}.json");
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
            Instance = reloaded;
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
            InstanceChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            RenameButtonEnabled = true;
        }
    }

    public async Task EditDescriptionAsync()
    {
        if (Instance is null)
            return;
        DescriptionButtonEnabled = false;
        try
        {
            var instance = Instance;
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
            ReloadInstance(instance.Name);
            RefreshInstanceList();
            InstanceChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            DescriptionButtonEnabled = true;
        }
    }

    [RelayCommand]
    public void ToggleStar()
    {
        if (Instance is null)
            return;
        try
        {
            var instance = Instance;
            States.Instance.Starred[instance.PathInstance] = !instance.IsStar;
            ReloadInstance(instance.Name);
            InstanceList.mcInstanceListForceRefresh = true;
            RefreshInstanceList();
            InstanceChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    public void ExportLaunchScript()
    {
        if (Instance is null)
            return;
        try
        {
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Overall.Script.SelectSaveTitle"),
                $"启动 {Instance.Name}.bat",
                Lang.Text("Instance.Overall.Script.FileFilter"));
            if (string.IsNullOrEmpty(savePath))
                return;
            if (GameLaunch.mcLaunchLoader.State == LoadState.Loading)
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Instance.Overall.Script.WaitForLaunchTask")));
                return;
            }

            if (GameLaunch.McLaunchStart(
                    new GameLaunch.McLaunchOptions
                    {
                        SaveBatch = savePath,
                        instance = Instance
                    }))
                HintService.Show(
                    ProfileService.Current?.ProfileType == ProfileType.Offline
                        ? Hints.Info(Lang.Text("Instance.Overall.Script.Exporting"))
                        : Hints.Info(Lang.Text("Instance.Overall.Script.ExportingWarning")));
            else
                HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Script.ExportFailed")));
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    public void TestGame()
    {
        if (Instance is null)
            return;
        try
        {
            if (GameLaunch.McLaunchStart(
                    new GameLaunch.McLaunchOptions
                    {
                        instance = Instance,
                        IsTest = true
                    }))
                PageRouter.Navigate(PageType.Launch);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    public void CheckFiles()
    {
        if (Instance is null || !RestrictedFeatures.Check("minecraft-repair"))
            return;
        try
        {
            var instance = Instance;
            if (Libraries.ShouldIgnoreFileCheck(instance))
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Instance.Overall.Repair.DisableVerificationHint")));
                return;
            }

            var taskName = $"{instance.Name} {Lang.Text("Instance.Overall.Repair.TaskName")}";
            if (Loading.loaderTaskbar.Any(loader => loader.name == taskName))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Repair.Processing")));
                return;
            }

            var loader = new Loading.LoaderCombo<string>(
                taskName,
                GameDownload.DlClientFix(
                    instance,
                    true,
                    GameDownload.AssetsIndexExistsBehaviour.AlwaysDownload))
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                            HintService.Show(
                                Hints.Success(
                                    Lang.Text(
                                        "Instance.Overall.Repair.Success.WithTaskName",
                                        taskName)));
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
                                    Lang.Text(
                                        "Instance.Overall.Repair.Cancelled.WithTaskName",
                                        taskName)));
                            break;

                        case LoadState.Waiting:
                        case LoadState.Loading:
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            };
            loader.Start(instance.Name);
            Loading.LoaderTaskbarAdd(loader);
            LoadingUi.Current?.RefreshDownloadButton();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    public async Task RestoreAsync()
    {
        if (Instance is null || !RestrictedFeatures.Check("minecraft-reset"))
            return;
        RestoreButtonEnabled = false;
        try
        {
            var instance = Instance;
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

            try
            {
                var backupFolder = Path.Combine(instance.PathInstance, "PCLInstallBackups");
                InstancePageHelper.EnsureDirectory(backupFolder);
                var json = Path.Combine(instance.PathInstance, $"{instance.Name}.json");
                var jar = Path.Combine(instance.PathInstance, $"{instance.Name}.jar");
                if (File.Exists(json))
                    File.Copy(json, Path.Combine(backupFolder, $"{instance.Name}.json"), true);
                if (File.Exists(jar))
                    File.Copy(jar, Path.Combine(backupFolder, $"{instance.Name}.jar"), true);
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
                        DisplayName = $"{currentVersion.VanillaName} {currentVersion.OptiFine}",
                        IsPreview = currentVersion.OptiFine?.Contains(
                                "pre",
                                StringComparison.OrdinalIgnoreCase)
                            == true
                    }
                    : null,
                forgeEntry = currentVersion.HasForge
                    ? new GameDownload.DlForgeVersionEntry(
                        currentVersion.Forge,
                        null,
                        currentVersion.VanillaName)
                    {
                        Category = "installer"
                    }
                    : null,
                forgeVersion = currentVersion.HasForge ? currentVersion.Forge : null,
                neoForgeVersion = currentVersion.HasNeoForge ? currentVersion.NeoForge : null,
                cleanroomVersion = currentVersion.HasCleanroom ? currentVersion.Cleanroom : null,
                fabricVersion = currentVersion.HasFabric ? currentVersion.Fabric : null,
                liteLoaderEntry = currentVersion.HasLiteLoader
                    ? new GameDownload.DlLiteLoaderListEntry
                    {
                        Inherit = currentVersion.VanillaName
                    }
                    : null,
                legacyFabricVersion =
                    currentVersion.HasLegacyFabric ? currentVersion.LegacyFabric : null,
                labyModChannel = currentVersion.HasLabyMod
                    ? instance.JsonObject?["labymod_data"]?["channelType"]?.ToString()
                    : null,
                labyModCommitRef = currentVersion.HasLabyMod
                    ? instance.JsonObject?["labymod_data"]?["commitReference"]?.ToString()
                    : null
            };

            if (currentVersion.HasLabyMod && string.IsNullOrEmpty(request.labyModCommitRef))
            {
                HintService.Show(
                    Hints.Warning(
                        Lang.Text("Download.Install.State.GetVersionListFailed", "LabyMod")));
                return;
            }

            var combinationError = McInstallPipeline.GetCombinationError(request);
            if (combinationError is not null)
            {
                HintService.Show(Hints.Warning(combinationError));
                return;
            }

            var identifier = McInstallPipeline.GetIdentifier(instance.PathInstance);
            if (Loading.loaderTaskbar.Any(loader => loader.Identifier == identifier))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
                return;
            }

            var loaders = McInstallPipeline.CreateInstallLoader(request, true);
            if (loaders.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Install.Error.OperationFailed")));
                return;
            }

            var combo = new Loading.LoaderCombo<object>(Lang.Text("Common.Action.Reset"), loaders)
            {
                Identifier = identifier,
                OnStateChanged = OnInstallLoaderStateChanged(instance)
            };
            combo.Start(instance.PathInstance);
            Loading.LoaderTaskbarAdd(combo);
            LoadingUi.Current?.RefreshDownloadButton();
            PageRouter.Navigate(PageType.Launch);
        }
        catch (CancelledException)
        {
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            RestoreButtonEnabled = true;
        }
    }

    [RelayCommand]
    public async Task DeleteAsync(bool permanent)
    {
        if (Instance is null)
            return;
        DeleteButtonEnabled = false;
        try
        {
            var instance = Instance;
            var instancePath = instance.PathInstance;
            var instanceName = instance.Name;
            var isIsolatedInstance =
                instance.state != McInstanceState.Error
                && !string.Equals(
                    instance.PathIndie,
                    GameFolders.mcFolderSelected,
                    StringComparison.OrdinalIgnoreCase);
            var recycleSupported = PCL.Core.Platform.PlatformRuntime.Current.RecycleBin.IsSupported(instancePath);
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
            try
            {
                if (ConfigService.GetProvider(ConfigSource.GameInstance) is
                    DynamicCacheConfigStorage storage)
                    storage.InvalidateCache(instancePath);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "清理实例配置缓存失败", LauncherLogLevel.Developer);
            }

            if (effectivePermanent)
            {
                LauncherCommon.DeleteDirectory(instancePath);
                HintService.Show(
                    Hints.Success(
                        Lang.Text("Instance.Overall.Delete.PermanentSuccess", instanceName)));
            }
            else
            {
                var outcome = PCL.Core.Platform.PlatformRuntime.Current.RecycleBin.Move(instancePath, out var error);
                switch (outcome)
                {
                    case RecycleBinOutcome.Moved:
                        HintService.Show(
                            Hints.Success(
                                Lang.Text(
                                    "Instance.Overall.Delete.RecycleBinSuccess",
                                    instanceName)));
                        break;

                    case RecycleBinOutcome.Unsupported:
                        LauncherCommon.DeleteDirectory(instancePath);
                        HintService.Show(
                            Hints.Success(
                                Lang.Text(
                                    "Instance.Overall.Delete.PermanentSuccess",
                                    instanceName)));
                        break;

                    case RecycleBinOutcome.Failed:
                    default:
                        LauncherCommon.Log(
                            $"将实例移入回收站失败：{error}",
                            LauncherLogLevel.Feedback);
                        HintService.Show(
                            Hints.Error(Lang.Text("Instance.Overall.Error.OperationFailed")));
                        return;
                }
            }

            InstanceList.mcInstanceListForceRefresh = true;
            RefreshInstanceList();
            PageRouter.Back();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            DeleteButtonEnabled = true;
        }
    }

    [RelayCommand]
    public async Task PatchCoreAsync()
    {
        if (Instance is null)
            return;
        PatchButtonEnabled = false;
        try
        {
            var instance = Instance;
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
            var corePath = Path.Combine(instance.PathInstance, $"{instance.Name}.jar");
            var path = instance.PathInstance;
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        var core = new GameCore(corePath);
                        core.AddToCore(jarPath);
                        Config.Instance.DisableAssetVerifyV2[path] = true;
                        LauncherCommon.RunInUi(() => HintService.Show(
                            Hints.Success(Lang.Text("Instance.Overall.Patch.Success"))));
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(ex, "修补核心失败", LauncherLogLevel.Feedback);
                        LauncherCommon.RunInUi(() => HintService.Show(
                            Hints.Error(Lang.Text("Instance.Overall.Error.OperationFailed"))));
                    }
                },
                "Patch Instance Core",
                ThreadPriority.BelowNormal);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            PatchButtonEnabled = true;
        }
    }

    private void ReloadInstance(string name)
    {
        var reloaded = new McInstance(name).Load();
        Instance = reloaded;
        PageInstanceLeft.SetCurrentInstance(reloaded);
        if (InstanceList.McMcInstanceSelected is not null
            && InstanceList.McMcInstanceSelected.PathInstance == reloaded.PathInstance)
            InstanceList.McMcInstanceSelected = reloaded;
    }

    private static Action<Loading.LoaderBase> OnInstallLoaderStateChanged(McInstance instance)
    {
        return loader =>
        {
            if (loader.State is LoadState.Finished or LoadState.Failed or LoadState.Aborted)
                McInstallRecovery.HandleTerminalState(loader, instance.PathInstance);
            switch (loader.State)
            {
                case LoadState.Finished:
                    RefreshInstanceList();
                    HintService.Show(
                        Hints.Success($"{Lang.Text("Common.Status.Success")} {instance.Name}"));
                    break;

                case LoadState.Failed:
                    HintService.Show(
                        Hints.Error(
                            $"{Lang.Text("Instance.Install.Error.OperationFailed")} {loader.Error?.Message ?? string.Empty}"));
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
        };
    }

    public static void RefreshInstanceList()
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
            $"versions{Path.DirectorySeparatorChar}");
    }

    private static void ReportError(Exception ex)
    {
        LauncherCommon.Log(
            ex,
            Lang.Text("Instance.Overall.Error.OperationFailed"),
            LauncherLogLevel.Feedback,
            userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        HintService.Show(Hints.Error(Lang.Text("Instance.Error.OperationFailed")));
    }
}

/// <summary>实例信息表中的一行。</summary>
public sealed class InstanceInfoRow(
    string title,
    string info,
    string logo)
{
    public string Title { get; } = title;
    public string Info { get; } = info;
    public string Logo { get; } = logo;
}