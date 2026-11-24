using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Platform;
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
    private string _emptySearchText = string.Empty;
    private EmptyStateKind _emptyState;

    public string EmptyContent =>
        _emptyState switch
        {
            EmptyStateKind.NoInstances when ShowHidden => Lang.Text(
                "Select.Instance.Hidden.EmptyMessage"),
            EmptyStateKind.NoInstances => Lang.Text("Select.Instance.Empty.Message"),
            EmptyStateKind.AllHidden => Lang.Text("Select.Instance.AllHidden.Message"),
            _ => string.Empty
        };

    public string EmptySearchContent =>
        _emptyState switch
        {
            EmptyStateKind.HiddenSearch => string.IsNullOrWhiteSpace(_emptySearchText)
                ? Lang.Text("Select.Instance.Search.EmptyInput")
                : Lang.Text("Select.Instance.Search.NoHiddenResult", _emptySearchText),
            EmptyStateKind.Search => string.IsNullOrWhiteSpace(_emptySearchText)
                ? Lang.Text("Select.Instance.Search.EmptyInput")
                : Lang.Text("Select.Instance.Search.NoResult", _emptySearchText),
            _ => string.Empty
        };

    public string EmptySearchTitle =>
        _emptyState switch
        {
            EmptyStateKind.HiddenSearch => Lang.Text("Select.Instance.Hidden.EmptySearchTitle"),
            EmptyStateKind.Search => Lang.Text("Select.Instance.EmptySearch.Title"),
            _ => string.Empty
        };

    public string EmptyTitle =>
        _emptyState switch
        {
            EmptyStateKind.NoInstances when ShowHidden =>
                Lang.Text("Select.Instance.Hidden.EmptyTitle"),
            EmptyStateKind.NoInstances => Lang.Text("Select.Instance.Empty.Title"),
            EmptyStateKind.AllHidden => Lang.Text("Select.Instance.AllHidden.Title"),
            _ => string.Empty
        };

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
        SelectedInstance = InstanceList.McMcInstanceSelected;
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
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var root = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;

        if (force)
        {
            Ini.Write(Path.Combine(root, "PCL.ini"), "InstanceCache", "");
            InstanceList.mcInstanceListForceRefresh = true;
        }

        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            root,
            force
                ? Loading.LoaderFolderRunType.ForceRun
                : Loading.LoaderFolderRunType.RunOnUpdated,
            1,
            $"versions{Path.DirectorySeparatorChar}");
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
        return string.IsNullOrEmpty(searchText)
            || (instance.Name ?? "").Contains(
                searchText,
                StringComparison.InvariantCultureIgnoreCase)
            || (instance.Desc ?? "").Contains(
                searchText,
                StringComparison.InvariantCultureIgnoreCase)
            || instance
                .GetDefaultDescription()
                .Replace(",", "")
                .ToLowerInvariant()
                .Trim()
                .Contains(searchText);
    }

    [RelayCommand]
    public static void DownloadPage()
    {
        PageRouter.Navigate(PageIds.Download, SubPageIds.DownloadInstall);
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
            new PageRoute(PageIds.InstanceSetup, SubPageIds.VersionOverall, version));
    }

    public async Task DeleteVersionAsync(McInstance instance, bool permanent)
    {
        try
        {
            var isHintIndie = instance.state != McInstanceState.Error
                && (instance.PathIndie ?? "") != (GameFolders.mcFolderSelected ?? "");
            var canRecycle = !permanent
                && ApplicationRuntime.Current.Platform.RecycleBin.IsSupported(
                    instance.PathInstance);
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
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        if (!permanent && TryRecycleInstance(instance)) return;
                        Directories.DeleteDirectory(instance.PathInstance);
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

            InstanceRenameService.Rename(
                instance,
                newName,
                GameFolders.mcFolderSelected ?? string.Empty);

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
            SetEmptyState(EmptyStateKind.None, searchText);
            return;
        }

        if (!originalHasInstances)
        {
            IsBackVisible = false;
            IsSearchBoxVisible = false;
            IsEmptySearchVisible = false;
            IsEmptyVisible = true;
            IsDownloadVisible = !ShowHidden
                && (HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload);
            SetEmptyState(EmptyStateKind.NoInstances, searchText);
            return;
        }

        switch (ShowHidden)
        {
            case false when string.IsNullOrWhiteSpace(searchText)
                && cards.Any(card => card.Key == McInstanceCardType.Hidden && card.Value.Count > 0):
            {
                IsBackVisible = false;
                IsSearchBoxVisible = false;
                IsEmptyVisible = true;
                IsEmptySearchVisible = false;
                IsDownloadVisible = false;
                SetEmptyState(EmptyStateKind.AllHidden, searchText);
                return;
            }

            case true:
            {
                var hasHiddenInstances = cards.Any(card =>
                    card.Key == McInstanceCardType.Hidden && card.Value.Count > 0);
                if (hasHiddenInstances)
                {
                    IsBackVisible = true;
                    IsSearchBoxVisible = true;
                    IsEmptyVisible = false;
                    IsEmptySearchVisible = true;
                    SetEmptyState(EmptyStateKind.HiddenSearch, searchText);
                }
                else
                {
                    IsBackVisible = false;
                    IsSearchBoxVisible = false;
                    IsEmptyVisible = true;
                    IsEmptySearchVisible = false;
                    IsDownloadVisible = false;
                    SetEmptyState(EmptyStateKind.NoInstances, searchText);
                }

                return;
            }
        }

        IsBackVisible = true;
        IsSearchBoxVisible = true;
        IsEmptyVisible = false;
        IsEmptySearchVisible = true;
        SetEmptyState(EmptyStateKind.Search, searchText);
    }

    private void SetEmptyState(EmptyStateKind state, string searchText)
    {
        _emptyState = state;
        _emptySearchText = searchText;
        OnPropertyChanged(nameof(EmptyContent));
        OnPropertyChanged(nameof(EmptySearchContent));
        OnPropertyChanged(nameof(EmptySearchTitle));
        OnPropertyChanged(nameof(EmptyTitle));
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
            // 列表中的实例要么已在扫描线程完整 Load，要么从缓存恢复了 Info。
            // 这里位于 UI 线程，不能再调用 Load() 去读 Json 和写配置。
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
        var outcome = ApplicationRuntime.Current.Platform.RecycleBin.Move(
            instance.PathInstance,
            out var error);
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

    private enum EmptyStateKind
    {
        None,
        NoInstances,
        AllHidden,
        HiddenSearch,
        Search
    }
}

/// <summary>实例选择页中的一个卡片分组。</summary>
public sealed record InstanceCardGroup(
    McInstanceCardType Type,
    string NameKey,
    IReadOnlyList<McInstance> Instances)
{
    public string Title => PageSelectRightViewModel.GetCardTitle(this);
}