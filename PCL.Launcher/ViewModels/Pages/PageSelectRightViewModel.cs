using PCL.Core.Platform;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Shell;

namespace PCL;

/// <summary>实例选择页右栏的视图模型：实例分组、搜索、隐藏、选择与实例操作。</summary>
public partial class PageSelectRightViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string EmptyContent { get; set; } = Lang.Text("Select.Instance.Empty.Message");

    [ObservableProperty] public partial string EmptySearchContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EmptySearchTitle { get; set; } =
        Lang.Text("Select.Instance.EmptySearch.Title");

    [ObservableProperty]
    public partial string EmptyTitle { get; set; } = Lang.Text("Select.Instance.Empty.Title");

    [ObservableProperty] public partial IReadOnlyList<InstanceCardGroup> Groups { get; set; } = [];

    [ObservableProperty] public partial bool IsBackVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsDownloadVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsEmptySearchVisible { get; set; }

    [ObservableProperty] public partial bool IsEmptyVisible { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool IsSearchBoxVisible { get; set; } = true;

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial McInstance? SelectedInstance { get; set; }

    [ObservableProperty] public partial bool ShowHidden { get; set; }

    /// <summary>按实例分类重新分组并计算搜索/隐藏空态。</summary>
    public void RefreshGroups()
    {
        IsLoading = InstanceList.mcInstanceListLoader.State == LoadState.Loading;
        var searchText = SearchText.Trim().ToLowerInvariant();
        var cards = InstanceList.mcInstanceList.ToArray();
        var originalHasInstances = cards.Any(card => card.Value.Count > 0);
        var groups = new List<InstanceCardGroup>();
        var hasAnyResults = false;

        foreach (var card in cards)
        {
            if (card.Key == McInstanceCardType.Hidden != ShowHidden)
                continue;
            var filtered = card
                .Value
                .Where(instance => MatchesSearch(instance, searchText))
                .ToList();
            if (filtered.Count == 0)
                continue;

            hasAnyResults = true;
            groups.Add(
                new InstanceCardGroup(
                    card.Key,
                    GetCardNameKey(card.Key, card.Value),
                    filtered));
        }

        Groups = groups;
        RefreshEmptyState(cards, originalHasInstances, hasAnyResults, searchText);
    }

    public static void RefreshInstances(bool force)
    {
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            GameFolders.mcFolderSelected ?? "",
            force ? Loading.LoaderFolderRunType.ForceRun : Loading.LoaderFolderRunType.RunOnUpdated,
            1,
            $"versions{Path.DirectorySeparatorChar}");
    }

    public bool ToggleHiddenInstances()
    {
        ShowHidden = !ShowHidden;
        RefreshInstances(true);
        return true;
    }

    public static string GetCardTitle(InstanceCardGroup group)
    {
        return $"{Lang.Text(group.NameKey)}"
            + (group.Type == McInstanceCardType.Star
                ? ""
                : $" ({Lang.Number(group.Instances.Count, "N0")})");
    }

    public static bool MatchesSearch(McInstance instance, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return true;
        return (instance.Name ?? "").ToLower().Contains(searchText)
            || (instance.Desc ?? "").ToLower().Contains(searchText)
            || instance
                .GetDefaultDescription()
                .Replace(",", "")
                .ToLower()
                .Trim()
                .Contains(searchText);
    }

    [RelayCommand]
    public static void DownloadPage()
    {
        PageRouter.Navigate(PageType.Download, PageSubType.DownloadInstall);
    }

    [RelayCommand]
    public void SelectInstance(McInstance instance)
    {
        if (new McInstance(instance.PathInstance).Check())
        {
            SelectedInstance = instance;
            InstanceList.McMcInstanceSelected = instance;
            States.Game.SelectedInstance = instance.Name;
            PageRouter.Back();
            return;
        }

        OpenVersionFolder(instance);
    }

    [RelayCommand]
    public static void ToggleFavorite(McInstance instance)
    {
        States.Instance.Starred[instance.PathInstance] = !instance.IsStar;
        InstanceList.mcInstanceListForceRefresh = true;
        RefreshInstances(true);
    }

    public static void OpenVersionFolder(McInstance version)
    {
        Basics.OpenPath(version.PathInstance);
    }

    public static void OpenInstanceSettings(McInstance version)
    {
        PageRouter.Navigate(
            new PageStackData(PageType.InstanceSetup, PageSubType.VersionOverall, version));
    }

    public async Task DeleteVersionAsync(McInstance instance, bool permanent)
    {
        try
        {
            var isHintIndie = instance.state != McInstanceState.Error
                && (instance.PathIndie ?? "") != (GameFolders.mcFolderSelected ?? "");
            // Shift 直接永久删除；否则优先走系统回收站。平台或所在卷没有回收站时，
            // 确认文案必须改写成“永久删除、不可恢复”，不能沿用“已移至回收站”的说法。
            var canRecycle = !permanent && PCL.Core.Platform.PlatformRuntime.Current.RecycleBin.IsSupported(instance.PathInstance);
            var confirmMsg = !permanent && !canRecycle
                ? Lang.Text("Select.Instance.Delete.RecycleBinUnavailable", instance.Name)
                : Lang.Text(
                    permanent
                        ? "Select.Instance.Delete.ConfirmPermanentMessage"
                        : "Select.Instance.Delete.ConfirmMessage",
                    instance.Name);
            var confirmFullMsg = confirmMsg
                + (isHintIndie
                    ? "\n" + Lang.Text("Select.Instance.Delete.IsolatedWarning")
                    : "");
            var dialog = await Dialog.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Select.Instance.Delete.ConfirmTitle"),
                    Content = confirmFullMsg,
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            try
            {
                if (ConfigService.GetProvider(ConfigSource.GameInstance) is
                    DynamicCacheConfigStorage storage)
                    storage.InvalidateCache(instance.PathInstance);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "清除实例配置缓存失败");
            }

            LaunchLogCache(instance);
            LauncherCommon.IniClearCache(Path.Combine(instance.PathIndie ?? "", "options.txt"));
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        if (!permanent && TryRecycleInstance(instance)) return;
                        LauncherCommon.DeleteDirectory(instance.PathInstance);
                        HintService.Show(
                            Hints.Success(
                                Lang.Text(
                                    "Select.Instance.Delete.PermanentSuccess",
                                    instance.Name)));
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(
                            ex,
                            Lang.Text("Select.Instance.Error.Delete", instance.Name),
                            LauncherLogLevel.Feedback,
                            userSummary: Lang.Text("Select.Instance.Error.Delete", instance.Name));
                    }
                    finally
                    {
                        RefreshInstances(true);
                    }
                },
                "Instance Delete " + LauncherCommon.GetUuid(),
                ThreadPriority.BelowNormal);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Instance.Error.Delete", instance.Name),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Instance.Error.Delete", instance.Name));
        }
    }

    /// <summary>重命名实例；完整复用实例设置页的文件、JSON 与快捷方式处理规则。</summary>
    [RelayCommand]
    public async Task RenameInstanceAsync(McInstance instance)
    {
        var oldName = instance.Name;
        var oldPath = instance.PathInstance;
        var versionsFolder = Path.Combine(
            GameFolders.mcFolderSelected ?? string.Empty,
            "versions");
        try
        {
            var dialog = await Dialog.ShowAsync(
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
            var tempName = $"{newName}_temp";
            var tempPath = Path.Combine(versionsFolder, tempName);
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

            var reloaded = new McInstance(newName).Load();
            if (InstanceList.McMcInstanceSelected is not null
                && InstanceList.McMcInstanceSelected.PathInstance == oldPath)
            {
                InstanceList.McMcInstanceSelected = reloaded;
                States.Game.SelectedInstance = newName;
            }

            HintService.Show(Hints.Success(Lang.Text("Instance.Overall.Name.RenameSuccess")));
            RefreshInstances(true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Overall.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
        }
    }

    private void RefreshEmptyState(
        IReadOnlyList<KeyValuePair<McInstanceCardType, List<McInstance>>> cards,
        bool originalHasInstances,
        bool hasAnyResults,
        string searchText)
    {
        if (hasAnyResults)
        {
            IsBackVisible = true;
            IsSearchBoxVisible = true;
            IsEmptyVisible = false;
            IsEmptySearchVisible = false;
            return;
        }

        // 完全没有实例：显示空态；隐藏实例模式下提示无隐藏实例。
        if (!originalHasInstances)
        {
            IsBackVisible = false;
            IsSearchBoxVisible = false;
            IsEmptySearchVisible = false;
            IsEmptyVisible = true;
            if (ShowHidden)
            {
                EmptyTitle = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                EmptyContent = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                IsDownloadVisible = false;
            }
            else
            {
                EmptyTitle = Lang.Text("Select.Instance.Empty.Title");
                EmptyContent = Lang.Text("Select.Instance.Empty.Message");
                IsDownloadVisible =
                    HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
            }

            return;
        }

        if (ShowHidden)
        {
            var hasHiddenInstances = cards.Any(card =>
                card.Key == McInstanceCardType.Hidden && card.Value.Count > 0);
            if (hasHiddenInstances)
            {
                IsBackVisible = true;
                IsSearchBoxVisible = true;
                IsEmptyVisible = false;
                IsEmptySearchVisible = true;
                EmptySearchTitle = Lang.Text("Select.Instance.Hidden.EmptySearchTitle");
                EmptySearchContent = string.IsNullOrWhiteSpace(searchText)
                    ? Lang.Text("Select.Instance.Search.EmptyInput")
                    : Lang.Text("Select.Instance.Search.NoHiddenResult", searchText);
            }
            else
            {
                IsBackVisible = false;
                IsSearchBoxVisible = false;
                IsEmptyVisible = true;
                IsEmptySearchVisible = false;
                EmptyTitle = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                EmptyContent = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                IsDownloadVisible = false;
            }

            return;
        }

        IsBackVisible = true;
        IsSearchBoxVisible = true;
        IsEmptyVisible = false;
        IsEmptySearchVisible = true;
        EmptySearchTitle = Lang.Text("Select.Instance.EmptySearch.Title");
        EmptySearchContent = string.IsNullOrWhiteSpace(searchText)
            ? Lang.Text("Select.Instance.Search.EmptyInput")
            : Lang.Text("Select.Instance.Search.NoResult", searchText);
    }

    private static string GetCardNameKey(McInstanceCardType type, List<McInstance> instances)
    {
        return type switch
        {
            McInstanceCardType.OriginalLike => "Select.Instance.Card.Regular",
            McInstanceCardType.API => GetApiCardNameKey(instances),
            McInstanceCardType.Error => "Select.Instance.Card.Error",
            McInstanceCardType.Hidden => "Select.Instance.Card.Hidden",
            McInstanceCardType.Rubbish => "Select.Instance.Card.LessUsed",
            McInstanceCardType.Star => "Select.Instance.Card.Favorites",
            McInstanceCardType.Fool => "Select.Instance.Card.AprilFools",
            McInstanceCardType.Auto or _ => throw new ArgumentException($"未知的卡片种类（{(int)type}）")
        };
    }

    private static string GetApiCardNameKey(List<McInstance> instances)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);

        foreach (var instance in instances)
        {
            if (!instance.IsLoaded)
                instance.Load();

            if (instance.Info.HasFabric) flags.Add("Fabric");
            if (instance.Info.HasQuilt) flags.Add("Quilt");
            if (instance.Info.HasLiteLoader) flags.Add("LiteLoader");
            if (instance.Info.HasForge) flags.Add("Forge");
            if (instance.Info.HasNeoForge) flags.Add("NeoForge");
            if (instance.Info.HasCleanroom) flags.Add("Cleanroom");
            if (instance.Info.HasLabyMod) flags.Add("LabyMod");
        }

        return flags.Count switch
        {
            > 1 => "Select.Instance.Card.Modable",
            _ when flags.Contains("Forge") => "Select.Instance.Card.Forge",
            _ when flags.Contains("NeoForge") => "Select.Instance.Card.NeoForge",
            _ when flags.Contains("Cleanroom") => "Select.Instance.Card.Cleanroom",
            _ when flags.Contains("LabyMod") => "Select.Instance.Card.LabyMod",
            _ when flags.Contains("LiteLoader") => "Select.Instance.Card.LiteLoader",
            _ when flags.Contains("Quilt") => "Select.Instance.Card.Quilt",
            _ => "Select.Instance.Card.Fabric"
        };
    }

    private static bool TryRecycleInstance(McInstance instance)
    {
        var outcome = PCL.Core.Platform.PlatformRuntime.Current.RecycleBin.Move(instance.PathInstance, out var error);
        switch (outcome)
        {
            case RecycleBinOutcome.Moved:
                HintService.Show(
                    Hints.Success(
                        Lang.Text("Select.Instance.Delete.RecycleBinSuccess", instance.Name)));
                return true;
            
            case RecycleBinOutcome.Failed:
                throw new IOException(
                    error ?? Lang.Text("Select.Instance.Error.Delete", instance.Name));
            
            case RecycleBinOutcome.Unsupported:
            default:
                return false;
        }
    }

    private static void LaunchLogCache(McInstance instance)
    {
        LauncherCommon.Log($"[Minecraft] 删除实例 {instance.Name}");
    }
}

/// <summary>实例选择页中的一个卡片分组。</summary>
public sealed record InstanceCardGroup(
    McInstanceCardType Type,
    string NameKey,
    IReadOnlyList<McInstance> Instances);