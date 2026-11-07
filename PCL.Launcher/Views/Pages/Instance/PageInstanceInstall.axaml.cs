using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Pages.Download;
using static PCL.Animations;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例安装/修改页：展示当前实例摘要，并复用 <see cref="InstallLoaderSelector" /> 提供
///     完整的 Minecraft 版本列表与 11 种加载器/附属组件选择。支持修改、重置、补全文件与跳转下载页。
/// </summary>
public partial class PageInstanceInstall : MyPageRight, IRefreshable
{
    private readonly DispatcherTimer _progressTimer;
    private readonly InstallLoaderSelector _selector = null!;
    private readonly bool _uiBuilt;
    private readonly PageInstanceInstallViewModel _vm = new();
    private Comp.CompFile? _currentFabricApi;
    private LocalComp.LocalCompFile? _currentFabricApiLocal;
    private string? _currentFabricApiPath;
    private Comp.CompFile? _currentLegacyFabricApi;
    private LocalComp.LocalCompFile? _currentLegacyFabricApiLocal;
    private string? _currentLegacyFabricApiPath;
    private Comp.CompFile? _currentOptiFabric;
    private LocalComp.LocalCompFile? _currentOptiFabricLocal;
    private string? _currentOptiFabricPath;
    private Loading.LoaderCombo<string>? _installCombo;
    private McInstance? _instance;
    private string _instanceFolder = "";
    private string _versionIcon = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";

    public PageInstanceInstall()
    {
        InitializeComponent();
        DataContext = _vm;
        PanScroll = PanBack;
        _selector = new InstallLoaderSelector(PanLoaderCards, OnSelectionChanged, _vm.Selection);
        _uiBuilt = true;

        CardMinecraft.SwapControl = PanMinecraftBody;
        CardMinecraft.IsSwapped = true;
        CardMinecraft.PreviewSwap += (_, e) =>
        {
            ExitSelection();
            e.handled = true;
        };

        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _progressTimer.Tick += (_, _) => RefreshProgressText();

        BtnStart.Click += (_, _) => _ = StartInstallAsync();
        BtnCancel.Click += (_, _) => _installCombo?.Abort();
        BtnRetryVersions.Click += (_, _) => RetryVersionList();

        GameDownload.dlClientListLoader.PreviewFinish +=
            _ => Dispatcher.UIThread.Post(BuildVersionList);
        GameDownload.dlClientListLoader.OnStateChangedUi += OnVersionListStateChanged;
        CompDownloadLoaders.DlFabricApiLoader.OnStateChangedUi += OnComponentListStateChanged;
        CompDownloadLoaders.DlLegacyFabricApiLoader.OnStateChangedUi += OnComponentListStateChanged;
        CompDownloadLoaders.DlOptiFabricLoader.OnStateChangedUi += OnComponentListStateChanged;
        GameDownload.DlLabyModListLoader.OnStateChangedUi += OnComponentListStateChanged;

        RefreshStaticText();
        ShowSummaryStage(false);
    }

    private bool CanAnimateStage =>
        ContainerAnimation.CanAnimate(this)
        && PanMinecraft.IsLoaded
        && PanSelect.IsLoaded;

    private string StageAnimationKey => "InstanceInstall Stage " + pageUuid;

    private bool IsInstanceVersionSelected =>
        _instance is not null
        && string.Equals(
            _vm.VersionName,
            _instance.Info.VanillaName,
            StringComparison.Ordinal);

    public void Refresh()
    {
        if (!_uiBuilt || _vm.IsInstalling)
            return;

        var resolved = InstancePageHelper.ResolveInstance(null);
        var instanceChanged = resolved is not null
            && !string.Equals(
                resolved.PathInstance,
                _instance?.PathInstance,
                StringComparison.OrdinalIgnoreCase);
        if (resolved is null)
            _instance = null;
        else if (_instance is null || instanceChanged)
            _instance = resolved;
        else
            _instance = new McInstance(resolved.PathInstance);
        if (_instance is null)
        {
            UpdateSummary(null);
            UpdateActionAvailability();
            ExitSelection();
            return;
        }

        LoadCurrentApiInfo(_instance);
        UpdateSummary(_instance, !_vm.IsSelecting || instanceChanged);
        UpdateActionAvailability();
        if (instanceChanged && _vm.IsSelecting)
        {
            BeginSelection(false);
            return;
        }

        EnsureVersionListLoaded(true);
        if (_vm.IsSelecting)
        {
            TryResolveExistingLabyMod();
            TryApplyCurrentApis();
            OnSelectionChanged();
        }
    }

    public void Reload()
    {
        Refresh();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        var resolved = InstancePageHelper.ResolveInstance(parameter);
        if (resolved is null)
            _instance = null;
        else if (_instance is null
                 || !string.Equals(
                     _instance.PathInstance,
                     resolved.PathInstance,
                     StringComparison.OrdinalIgnoreCase))
            _instance = resolved;
        if (_instance is null)
        {
            UpdateSummary(null);
            UpdateActionAvailability();
            ExitSelection();
            return;
        }

        LoadCurrentApiInfo(_instance);
        UpdateSummary(_instance);
        UpdateActionAvailability();
        if (_vm.IsInstalling)
        {
            _vm.IsSelecting = true;
            PanMinecraft.IsVisible = false;
            PanSelect.IsVisible = false;
            BtnStart.Show = false;
            CardProgress.IsVisible = true;
            PanBack?.ScrollToHome();
            return;
        }

        EnsureVersionListLoaded(false);
        ShowSummaryStage(false);
    }

    protected override void RefreshLocalization()
    {
        if (!_uiBuilt)
            return;

        RefreshStaticText();
        if (_instance is null)
        {
            UpdateSummary(null);
            _vm.ActionText = Lang.Text("Instance.Install.Action.StartModify");
            _vm.HasWarning = false;
            _vm.WarningText = "";
        }
        else
        {
            _vm.CurrentInfo = FormatLoaders(_instance);
            UpdateSummary(_instance, !_vm.IsSelecting);
            _selector.RefreshLocalization();
            if (_vm.IsSelecting)
            {
                OnSelectionChanged();
            }
            else
            {
                _vm.ActionText = Lang.Text("Instance.Install.Action.StartModify");
                _vm.HasWarning = false;
                _vm.WarningText = "";
            }
        }

        BuildVersionList();
    }

    private void RefreshStaticText()
    {
        CardMinecraft.Title = Lang.Text("Common.Installation.Minecraft");
        BtnCancel.Text = Lang.Text("Common.Action.Cancel");
        BtnRetryVersions.Text = Lang.Text("Launch.Homepage.LoadFailed.Retry");
        LoadVersionList.Text = Lang.Text("Download.Version.LoadingList");
    }

    private void UpdateSummary(McInstance? instance, bool resetVersion = true)
    {
        if (instance is null)
        {
            _vm.InstanceName = "";
            _vm.CurrentInfo = "";
            _vm.SelectedInfo = "";
            if (resetVersion)
                _vm.VersionName = "";
            return;
        }

        var info = instance.Info;
        _vm.InstanceName = instance.Name;
        if (resetVersion)
        {
            _vm.VersionName = info.VanillaName;
            _vm.VersionUrl = instance.JsonObject?["url"]?.ToString();
        }

        _vm.CurrentInfo = FormatLoaders(instance);
        if (!_vm.IsSelecting)
            _vm.SelectedInfo = _vm.CurrentInfo;
    }

    private static string FormatLoaders(McInstance instance)
    {
        var info = instance.Info;
        var loaders = new List<string>();
        if (info.HasForge)
            loaders.Add($"Forge {info.Forge ?? string.Empty}");
        if (info.HasNeoForge)
            loaders.Add($"NeoForge {info.NeoForge ?? string.Empty}");
        if (info.HasCleanroom)
            loaders.Add($"Cleanroom {info.Cleanroom ?? string.Empty}");
        if (info.HasFabric)
            loaders.Add($"Fabric {(info.Fabric ?? string.Empty).Replace("+build", "")}");
        if (info.HasLegacyFabric)
            loaders.Add($"Legacy Fabric {info.LegacyFabric ?? string.Empty}");
        if (info.HasLiteLoader)
            loaders.Add("LiteLoader");
        if (info.HasOptiFine)
            loaders.Add($"OptiFine {FormatOptiFine(info)}");
        if (info.HasLabyMod)
        {
            var labyMod = (info.LabyMod ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            loaders.Add($"LabyMod {labyMod}");
        }

        if (info.HasQuilt)
            loaders.Add($"Quilt {info.Quilt ?? string.Empty}");
        return loaders.Count == 0
            ? Lang.Text("Instance.Install.NoExtraInstall")
            : string.Join(", ", loaders);
    }

    private static string FormatOptiFine(McInstanceInfo info)
    {
        var version = info.OptiFine ?? string.Empty;
        if (!version.StartsWith("HD_U", StringComparison.OrdinalIgnoreCase))
            version = $"HD_U_{version}";
        return version.Replace("HD_U_", "").Replace("_", " ");
    }

    private static Grid MakeRow(string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Height = 26d
        };
        grid.Children.Add(
            new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7d,
                Margin = new Thickness(0d, 0d, 25d, 0d)
            });
        var valueBlock = new TextBlock
        {
            Text = value,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
        return grid;
    }

    private void BeginSelection(
        bool reset,
        string? versionName = null,
        string? versionUrl = null,
        string? versionIcon = null)
    {
        if (_instance is null || _vm.IsInstalling)
            return;

        LoadCurrentApiInfo(_instance);
        _vm.IsSelecting = true;
        _vm.IsProgressVisible = false;
        _vm.HasWarning = false;
        _vm.WarningText = "";
        CardProgress.IsVisible = false;

        var info = _instance.Info;
        _vm.InstanceName = _instance.Name;
        _vm.CurrentInfo = FormatLoaders(_instance);
        var selectedName = string.IsNullOrEmpty(versionName) ? info.VanillaName : versionName;
        _vm.VersionName = selectedName;
        _vm.VersionUrl = versionUrl ?? _instance.JsonObject?["url"]?.ToString();
        _versionIcon = versionIcon ?? $"{LauncherPaths.ImageRoot}Blocks/Grass.png";

        _selector.SetMinecraft(selectedName, _vm.VersionUrl);
        if (string.Equals(selectedName, info.VanillaName, StringComparison.Ordinal))
        {
            _selector.ApplyExisting(_instance);
            TryResolveExistingLabyMod();
            TryApplyCurrentApis();
        }

        _vm.ActionText = Lang.Text(
            reset ? "Instance.Install.Action.StartReset" : "Instance.Install.Action.StartModify");
        OnSelectionChanged();
        ShowSelectStage();
    }

    private void ExitSelection()
    {
        if (_vm.IsInstalling)
            return;
        _vm.IsSelecting = false;
        _selector.ClearAll();
        _vm.HasWarning = false;
        _vm.WarningText = "";
        PanWarnings.Children.Clear();
        _vm.SelectedInfo = _vm.CurrentInfo;
        _vm.ActionText = Lang.Text("Instance.Install.Action.StartModify");
        BtnStart.SvgIcon = "lucide/download";
        _vm.CanStartInstall = false;
        ShowSummaryStage();
        UpdateActionAvailability();
    }

    private void ShowSelectStage()
    {
        _vm.IsSelecting = true;
        BtnStart.Show = true;
        if (!CanAnimateStage)
        {
            PanMinecraft.IsVisible = false;
            PanMinecraft.Opacity = 1d;
            PanMinecraft.RenderTransform = null;
            PanSelect.IsVisible = true;
            PanSelect.Opacity = 1d;
            PanSelect.RenderTransform = null;
            PanBack?.ScrollToHome();
            return;
        }

        AniStop(StageAnimationKey);
        PanMinecraft.Opacity = 1d;
        PanMinecraft.RenderTransform = new TranslateTransform();
        PanSelect.IsVisible = true;
        PanSelect.Opacity = 0d;
        PanSelect.RenderTransform = new TranslateTransform();
        AniStart(
            new[]
            {
                AaOpacity(PanMinecraft, 0d, 100, 10),
                AaCode(
                    () =>
                    {
                        PanMinecraft.IsVisible = false;
                        PanBack?.ScrollToHome();
                    },
                    after: true),
                AaOpacity(PanSelect, 1d, 250, 150)
            },
            StageAnimationKey,
            true);
    }

    private void ShowSummaryStage(bool animate = true)
    {
        _vm.IsSelecting = false;
        BtnStart.Show = false;
        if (!animate || !CanAnimateStage)
        {
            PanSelect.IsVisible = false;
            PanSelect.Opacity = 1d;
            PanSelect.RenderTransform = null;
            PanMinecraft.IsVisible = true;
            PanMinecraft.Opacity = 1d;
            PanMinecraft.RenderTransform = null;
            PanBack?.ScrollToHome();
            return;
        }

        AniStop(StageAnimationKey);
        PanMinecraft.IsVisible = true;
        PanMinecraft.Opacity = 0d;
        PanMinecraft.RenderTransform = new TranslateTransform();
        PanSelect.Opacity = 1d;
        PanSelect.RenderTransform = new TranslateTransform();
        AniStart(
            new[]
            {
                AaOpacity(PanSelect, 0d, 90, 10),
                AaCode(() => PanSelect.IsVisible = false, after: true),
                AaOpacity(PanMinecraft, 1d, 150, 100)
            },
            StageAnimationKey,
            true);
        PanBack?.ScrollToHome();
    }

    private void UpdateActionAvailability()
    {
        if (_instance is not null)
            return;
        _vm.CanStartInstall = false;
        _vm.IsSelecting = false;
        PanMinecraft.IsVisible = true;
        PanSelect.IsVisible = false;
        BtnStart.Show = false;
    }

    private void OnSelectionChanged()
    {
        if (_selector is null || _instance is null || !_vm.IsSelecting)
            return;

        var selection = _selector.Selection;
        _vm.SelectedInfo = selection.GetInfo(_vm.VersionName);
        _vm.ActionText = Lang.Text(
            IsCurrentSelection()
                ? "Instance.Install.Action.StartReset"
                : "Instance.Install.Action.StartModify");
        BtnStart.SvgIcon = IsCurrentSelection()
            ? "lucide/rotate-ccw"
            : "lucide/pencil";
        _vm.CanStartInstall = !_vm.IsInstalling && !string.IsNullOrEmpty(_vm.VersionName);
        _vm.VersionIcon = GetSelectionIcon();
        var warnings = _selector.GetWarnings();
        _vm.WarningText = string.Join(Environment.NewLine, warnings);
        _vm.HasWarning = warnings.Count > 0;
        RebuildWarnings();
    }

    private void RebuildWarnings()
    {
        PanWarnings.Children.Clear();
        if (_selector is null)
            return;

        var flags = _selector.GetWarningFlags();
        AddWarning(
            flags.LegacyOptiFabric,
            "Download.Install.Warning.LegacyOptiFabric",
            MyHint.Themes.Yellow,
            new Thickness(0d, 10d, 0d, 0d));
        AddWarning(
            flags.LegacyFabricApi,
            "Download.Install.Warning.LegacyFabricApi",
            MyHint.Themes.Red,
            new Thickness(0d, 10d, 0d, 0d));
        AddWarning(
            flags.FabricApi,
            "Download.Install.Warning.FabricApi",
            MyHint.Themes.Yellow,
            new Thickness(0d, 1d, 0d, 7d));
        AddWarning(
            flags.OptiFabric,
            "Download.Install.Warning.OptiFabric",
            MyHint.Themes.Yellow,
            new Thickness(0d, 1d, 0d, 7d));
        AddWarning(
            flags.OptiFabricOld,
            "Download.Install.Warning.OptiFabricOld",
            MyHint.Themes.Blue,
            new Thickness(0d, 1d, 0d, 7d));
        AddWarning(
            flags.ModOptiFine,
            "Download.Install.Warning.ModOptiFine",
            MyHint.Themes.Blue,
            new Thickness(0d, 1d, 0d, 7d));
    }

    private void AddWarning(
        bool show,
        string key,
        MyHint.Themes theme,
        Thickness margin)
    {
        if (!show)
            return;
        PanWarnings.Children.Add(
            new MyHint
            {
                Text = Lang.Text(key),
                Theme = theme,
                Margin = margin
            });
    }

    private bool IsCurrentSelection()
    {
        if (_instance is null)
            return false;
        return string.Equals(
                _vm.VersionName,
                _instance.Info.VanillaName,
                StringComparison.Ordinal)
            && string.Equals(
                _selector.Selection.GetLoaderInfo(),
                _vm.CurrentInfo,
                StringComparison.Ordinal);
    }

    private string GetSelectionIcon()
    {
        var selection = _selector.Selection;
        if (selection.FabricVersion is not null || selection.LegacyFabricVersion is not null)
            return $"{LauncherPaths.ImageRoot}Blocks/Fabric.png";
        if (selection.Forge is not null)
            return $"{LauncherPaths.ImageRoot}Blocks/Anvil.png";
        if (selection.NeoForge is not null)
            return $"{LauncherPaths.ImageRoot}Blocks/NeoForge.png";
        if (selection.LiteLoader is not null)
            return $"{LauncherPaths.ImageRoot}Blocks/Egg.png";
        if (selection.OptiFine is not null)
            return $"{LauncherPaths.ImageRoot}Blocks/GrassPath.png";
        if (selection.Cleanroom is not null)
            return $"{LauncherPaths.ImageRoot}Blocks/Cleanroom.png";
        if (selection.LabyModCommitRef is not null || selection.LabyModVersion is not null)
            return $"{LauncherPaths.ImageRoot}Blocks/LabyMod.png";
        return _versionIcon;
    }

    private void OnVersionSelected(JsonObject version)
    {
        if (_instance is null)
            return;

        var versionName = version["id"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(versionName))
            return;

        BeginSelection(
            false,
            versionName,
            version["url"]?.ToString(),
            GetVersionIcon(version));
    }

    private static string GetVersionIcon(JsonObject version)
    {
        var file = (version["type"]?.ToString() ?? "") switch
        {
            "release" => "Blocks/Grass.png",
            "snapshot" or "pending" => "Blocks/CommandBlock.png",
            "special" => "Blocks/GoldBlock.png",
            _ => "Blocks/CobbleStone.png"
        };
        return LauncherPaths.ImageRoot + file;
    }

    private void EnsureVersionListLoaded(bool forceReload)
    {
        try
        {
            var state = GameDownload.dlClientListLoader.State;
            var hasData = GameDownload.dlClientListLoader.output.Value?["versions"] is JsonArray
            {
                Count: > 0
            };
            if (state is not LoadState.Loading
                && (forceReload
                    || state is LoadState.Waiting
                    || (state is LoadState.Finished && !hasData)))
                GameDownload.dlClientListLoader.Start(null, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "加载 Minecraft 版本列表失败");
            ShowVersionListStatus(false, ex.Message);
            return;
        }

        BuildVersionList();
    }

    private void RetryVersionList()
    {
        try
        {
            GameDownload.dlClientListLoader.Start(null, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "重试加载 Minecraft 版本列表失败");
            ShowVersionListStatus(false, ex.Message);
            return;
        }

        BuildVersionList();
    }

    private void BuildVersionList()
    {
        if (!_uiBuilt)
            return;

        try
        {
            var state = GameDownload.dlClientListLoader.State;
            if (state is LoadState.Waiting or LoadState.Loading)
            {
                ShowVersionListStatus(true, null);
                return;
            }

            if (state is LoadState.Failed or LoadState.Aborted)
            {
                PanVersions.Children.Clear();
                ShowVersionListStatus(
                    false,
                    state == LoadState.Failed
                        ? GameDownload.dlClientListLoader.Error?.Message
                        ?? Lang.Text("Download.Install.Error.OperationFailed")
                        : Lang.Text("Download.Install.State.UnknownUnloaded"));
                return;
            }

            if (GameDownload.dlClientListLoader.output.Value?["versions"] is JsonArray versions
                && versions.Count > 0)
            {
                PanVersions.Children.Clear();
                DownloadVersionList.Build(PanVersions, versions, OnVersionSelected);
                if (PanVersions.Children.Count == 0)
                {
                    ShowVersionListStatus(
                        false,
                        Lang.Text("Download.Install.State.NoVersion"));
                    return;
                }

                ShowVersionListStatus(false, null);
                return;
            }

            PanVersions.Children.Clear();
            ShowVersionListStatus(
                false,
                Lang.Text("Download.Install.State.NoVersion"));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "构建 Minecraft 版本列表失败");
            PanVersions.Children.Clear();
            ShowVersionListStatus(
                false,
                $"{Lang.Text("Download.Install.Error.OperationFailed")} {ex.Message}");
        }
    }

    private void ShowVersionListStatus(bool loading, string? error)
    {
        var hasError = !string.IsNullOrEmpty(error);
        PanVersionStatus.IsVisible = loading || hasError;
        LoadVersionList.IsVisible = loading || hasError;
        BtnRetryVersions.IsVisible = hasError;
        if (loading)
        {
            LoadVersionList.Text = Lang.Text("Download.Version.LoadingList");
            LoadVersionList.TextError = "";
            LoadVersionList.ShowProgress = false;
            LoadVersionList.State = new MyLoadingStateSimulator
            {
                LoadingState = LoadingState.Run
            };
            return;
        }

        if (hasError)
        {
            LoadVersionList.Text = Lang.Text("Download.Version.VersionList");
            LoadVersionList.TextError = error!;
            LoadVersionList.ShowProgress = false;
            LoadVersionList.State = new MyLoadingStateSimulator
            {
                LoadingState = LoadingState.Error
            };
            return;
        }

        LoadVersionList.State = new MyLoadingStateSimulator();
    }

    private void OnVersionListStateChanged(
        Loading.LoaderBase loader,
        LoadState newState,
        LoadState oldState)
    {
        if (newState is LoadState.Waiting or LoadState.Loading)
            return;
        Dispatcher.UIThread.Post(BuildVersionList);
    }

    private void OnComponentListStateChanged(
        Loading.LoaderBase loader,
        LoadState newState,
        LoadState oldState)
    {
        if (newState is LoadState.Waiting or LoadState.Loading)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_instance is null || _vm.IsInstalling)
                return;
            TryResolveExistingLabyMod();
            TryApplyCurrentApis();
        });
    }

    private void LoadCurrentApiInfo(McInstance instance)
    {
        _currentFabricApiLocal = TryGetLocalComp(
            instance,
            ["fabric-api", "fabric"],
            "fabric",
            "api");
        _currentLegacyFabricApiLocal = TryGetLocalComp(
            instance,
            ["legacy-fabric-api", "legacy-fabric"],
            "legacy-fabric",
            "api");
        _currentOptiFabricLocal = TryGetLocalComp(
            instance,
            ["optifabric"],
            "optifabric",
            "opti");
        _currentFabricApiPath = _currentFabricApiLocal?.path;
        _currentLegacyFabricApiPath = _currentLegacyFabricApiLocal?.path;
        _currentOptiFabricPath = _currentOptiFabricLocal?.path;
        _currentFabricApi = MatchCurrentApi(
            _currentFabricApiLocal,
            CompDownloadLoaders.DlFabricApiLoader.output);
        _currentLegacyFabricApi = MatchCurrentApi(
            _currentLegacyFabricApiLocal,
            CompDownloadLoaders.DlLegacyFabricApiLoader.output);
        _currentOptiFabric = MatchCurrentApi(
            _currentOptiFabricLocal,
            CompDownloadLoaders.DlOptiFabricLoader.output);
    }

    private static LocalComp.LocalCompFile? TryGetLocalComp(
        McInstance instance,
        string[] modIds,
        string mainKeyword,
        params string[] keywords)
    {
        try
        {
            return LocalComp.GetModLocalCompByKeywords(
                instance,
                modIds,
                mainKeyword,
                keywords);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "识别实例本地 API 文件失败");
            return null;
        }
    }

    private static Comp.CompFile? MatchCurrentApi(
        LocalComp.LocalCompFile? local,
        List<Comp.CompFile>? output)
    {
        if (local is null || output is null || output.Count == 0)
            return null;
        try
        {
            var hash = local.ModrinthHash;
            if (string.IsNullOrEmpty(hash))
                return null;
            return output.FirstOrDefault(comp => string.Equals(
                comp.Hash,
                hash,
                StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "匹配实例本地 API 文件失败");
            return null;
        }
    }

    private void TryApplyCurrentApis()
    {
        if (_instance is null || _selector is null || !IsInstanceVersionSelected)
            return;

        var info = _instance.Info;
        var selection = _selector.Selection;
        var changed = false;
        if (info.HasFabric && selection.FabricVersion is not null)
        {
            _currentFabricApi ??= MatchCurrentApi(
                _currentFabricApiLocal,
                CompDownloadLoaders.DlFabricApiLoader.output);
            if (_currentFabricApi is not null && selection.FabricApi is null)
            {
                selection.FabricApi = _currentFabricApi;
                changed = true;
            }
        }

        if (info.HasLegacyFabric && selection.LegacyFabricVersion is not null)
        {
            _currentLegacyFabricApi ??= MatchCurrentApi(
                _currentLegacyFabricApiLocal,
                CompDownloadLoaders.DlLegacyFabricApiLoader.output);
            if (_currentLegacyFabricApi is not null && selection.LegacyFabricApi is null)
            {
                selection.LegacyFabricApi = _currentLegacyFabricApi;
                changed = true;
            }
        }

        if (info.HasFabric && info.HasOptiFine && selection.FabricVersion is not null)
        {
            _currentOptiFabric ??= MatchCurrentApi(
                _currentOptiFabricLocal,
                CompDownloadLoaders.DlOptiFabricLoader.output);
            if (_currentOptiFabric is not null && selection.OptiFabric is null)
            {
                selection.OptiFabric = _currentOptiFabric;
                changed = true;
            }
        }

        if (changed)
            _selector.RefreshAll();
    }

    private void TryResolveExistingLabyMod()
    {
        if (_instance is null
            || !_instance.Info.HasLabyMod
            || _selector is null
            || !IsInstanceVersionSelected)
            return;

        var selection = _selector.Selection;
        if (selection.LabyModVersion is null
            || !string.IsNullOrEmpty(selection.LabyModCommitRef))
            return;
        var existingVersion = _instance.Info.LabyMod;
        if (string.IsNullOrEmpty(existingVersion))
            return;

        var value = GameDownload.DlLabyModListLoader.output.Value;
        if (value is null)
            return;

        foreach (var channel in new[]
                 {
                     "production",
                     "snapshot"
                 })
        {
            if (value[channel] is not JsonObject channelNode)
                continue;
            var minecraftVersions = channelNode["minecraftVersions"] as JsonArray;
            if (minecraftVersions is null
                || !minecraftVersions
                    .OfType<JsonObject>()
                    .Any(version => string.Equals(
                        version["version"]?.ToString(),
                        _instance.Info.VanillaName,
                        StringComparison.OrdinalIgnoreCase)))
                continue;

            var version = channelNode["labyModVersion"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(version)
                || (!string.Equals(version, existingVersion, StringComparison.OrdinalIgnoreCase)
                    && !version.StartsWith(
                        existingVersion,
                        StringComparison.OrdinalIgnoreCase)))
                continue;

            var commitRef = channelNode["commitReference"]?.ToString();
            if (string.IsNullOrEmpty(commitRef))
                continue;

            selection.LabyModChannel = channel;
            selection.LabyModCommitRef = commitRef;
            selection.LabyModVersion =
                $"{version} {(channel == "snapshot" ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable"))}";
            _selector.RefreshAll();
            return;
        }
    }

    private void DeleteReplacedApiFiles()
    {
        var selection = _selector.Selection;
        DeleteReplacedApiFile(
            _currentFabricApiPath,
            _currentFabricApi,
            selection.FabricApi);
        DeleteReplacedApiFile(
            _currentLegacyFabricApiPath,
            _currentLegacyFabricApi,
            selection.LegacyFabricApi);
        DeleteReplacedApiFile(
            _currentOptiFabricPath,
            _currentOptiFabric,
            selection.OptiFabric);
    }

    private static void DeleteReplacedApiFile(
        string? path,
        Comp.CompFile? current,
        Comp.CompFile? selected)
    {
        if (string.IsNullOrEmpty(path) || selected is null)
            return;
        if (IsSameApi(selected, current, path))
            return;
        try
        {
            if (!File.Exists(path))
                return;
            File.Delete(path);
            LauncherCommon.Log($"[Install] 已删除被替换的旧 API 文件：{path}");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"删除旧 API 文件失败：{path}");
        }
    }

    private static bool IsSameApi(
        Comp.CompFile? selected,
        Comp.CompFile? current,
        string? currentPath)
    {
        if (selected is null)
            return false;
        if (current is not null
            && (string.Equals(selected.Hash, current.Hash, StringComparison.OrdinalIgnoreCase)
                || string.Equals(selected.Id, current.Id, StringComparison.Ordinal)))
            return true;
        return !string.IsNullOrEmpty(currentPath)
            && string.Equals(
                selected.FileName,
                Path.GetFileName(currentPath),
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task StartInstallAsync()
    {
        if (_instance is null || _vm.IsInstalling)
            return;
        if (!RestrictedFeatures.Check("minecraft-loader-install"))
            return;
        if (_instance.Info.HasQuilt)
        {
            HintService.Show(
                Hints.Info(Lang.Text("Instance.Overall.Reset.QuiltUnsupported")));
            return;
        }

        if (_instance.Info.Drop != 99
            && McVersionComparer.CompareVersion(_instance.Info.VanillaName, "1.5.2") == -1
            && _instance.Info.HasForge)
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Reset.NotSupported")));
            return;
        }

        if (string.IsNullOrEmpty(_vm.VersionName))
        {
            BeginSelection(false);
            return;
        }

        TryResolveExistingLabyMod();
        var instance = _instance;
        var selection = _selector.Selection;
        var isReset = IsCurrentSelection();

        if (selection.HasMainLoader && Config.Launch.IndieSolutionV2 is 0 or 2)
        {
            var isolation = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Download.Install.InstanceIsolation.Warning.Title"),
                    Content = Lang.Text("Download.Install.InstanceIsolation.Warning.Message"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(
                            Lang.Text("Download.Install.InstanceIsolation.Warning.Cancel")),
                        DialogButton.Create(
                            Lang.Text("Download.Install.InstanceIsolation.Warning.Continue"))),
                    IsWarn = true
                });
            if (isolation.Button != 2)
                return;
        }

        if (isReset)
        {
            var reset = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Install.Reset.Title"),
                    Content = Lang.Text("Instance.Install.Reset.Message"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!reset.IsConfirmed)
                return;
        }

        var request = new McInstallRequest
        {
            targetInstanceName = instance.Name,
            targetInstanceFolder = instance.PathInstance,
            minecraftJson = _vm.VersionUrl ?? instance.JsonObject?["url"]?.ToString(),
            minecraftName = _vm.VersionName
        };
        selection.ApplyTo(request);
        if (IsSameApi(selection.FabricApi, _currentFabricApi, _currentFabricApiPath))
            request.fabricApi = null;
        if (IsSameApi(
                selection.LegacyFabricApi,
                _currentLegacyFabricApi,
                _currentLegacyFabricApiPath))
            request.legacyFabricApi = null;
        if (IsSameApi(selection.OptiFabric, _currentOptiFabric, _currentOptiFabricPath))
            request.optiFabric = null;

        var identifier = McInstallPipeline.GetIdentifier(instance.PathInstance);
        if (Loading.loaderTaskbar.Any(loader => loader.Identifier == identifier))
        {
            HintService.Show(
                Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
            return;
        }

        List<Loading.LoaderBase> loaders;
        try
        {
            loaders = McInstallPipeline.CreateInstallLoader(request, true);
        }
        catch (CancelledException)
        {
            return;
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error(ex.Message));
            LauncherCommon.Log(ex, "开始实例修改失败");
            return;
        }

        _vm.IsInstalling = true;
        _vm.IsProgressVisible = true;
        _vm.CanStartInstall = false;
        BtnStart.Show = false;
        UpdateActionAvailability();
        CardProgress.IsVisible = true;
        _selector.SetInstalling(true);
        LoadInstall.Text = Lang.Text("Download.Install.State.Loading");
        LoadInstall.TextError = "";
        LoadInstall.ShowProgress = true;
        LoadInstall.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
        _progressTimer.Start();

        _instanceFolder = instance.PathInstance;
        _installCombo = new Loading.LoaderCombo<string>(
            $"{instance.Name} {_vm.ActionText}",
            loaders)
        {
            Identifier = identifier
        };
        _installCombo.OnStateChangedUi += OnInstallStateChanged;
        _installCombo.Start(instance.PathInstance);
        Loading.LoaderTaskbarAdd(_installCombo);
        LoadingUi.Current?.RefreshDownloadButton();
        PageRouter.Navigate(PageType.Launch);
    }

    private void OnInstallStateChanged(
        Loading.LoaderBase loader,
        LoadState newState,
        LoadState oldState)
    {
        if (newState is LoadState.Waiting or LoadState.Loading)
            return;

        _progressTimer.Stop();
        _vm.IsInstalling = false;
        _vm.IsProgressVisible = false;
        LoadInstall.ShowProgress = false;
        _selector.SetInstalling(false);
        McInstallRecovery.HandleTerminalState(loader, _instanceFolder);
        if (_installCombo is not null)
        {
            _installCombo.OnStateChangedUi -= OnInstallStateChanged;
            _installCombo = null;
        }

        var finishedName = _instance?.Name ?? "";
        if (newState == LoadState.Finished)
        {
            CardProgress.IsVisible = false;
            LoadInstall.State = new MyLoadingStateSimulator();
            HintService.Show(
                Hints.Success($"{finishedName} {Lang.Text("Download.Install.Finished")}"));
            InstanceList.mcInstanceListForceRefresh = true;
            ApplyFinishedBookkeeping(finishedName);
            DeleteReplacedApiFiles();
            _vm.IsSelecting = false;
            RefreshInstanceAfterInstall();
            return;
        }

        LoadInstall.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Error
        };
        LoadInstall.TextError = newState == LoadState.Aborted
            ? Lang.Text("Minecraft.Download.Error.InstallationCanceled")
            : loader.Error?.Message ?? Lang.Text("Instance.Install.Error.OperationFailed");
        CardProgress.IsVisible = true;
        BtnStart.Show = true;
        UpdateActionAvailability();
        OnSelectionChanged();
        HintService.Show(Hints.Error(LoadInstall.TextError));
    }

    private void RefreshInstanceAfterInstall()
    {
        if (string.IsNullOrEmpty(_instanceFolder))
            return;

        try
        {
            var refreshed = new McInstance(_instanceFolder);
            _instance = refreshed;
            LoadCurrentApiInfo(refreshed);
            UpdateSummary(refreshed);
            UpdateActionAvailability();
            _selector.SetMinecraft(
                refreshed.Info.VanillaName,
                refreshed.JsonObject?["url"]?.ToString());
            _selector.ApplyExisting(refreshed);
            TryResolveExistingLabyMod();
            TryApplyCurrentApis();
            ShowSummaryStage();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "安装完成后刷新实例信息失败");
        }
    }

    private static void ApplyFinishedBookkeeping(string instanceName)
    {
        try
        {
            var mcFolder = GameFolders.mcFolderSelected;
            if (string.IsNullOrEmpty(mcFolder))
                return;
            var iniPath = Path.Combine(mcFolder, "PCL.ini");
            if (!string.IsNullOrEmpty(instanceName) && Config.Download.AutoSelectInstance)
                LauncherCommon.WriteIni(iniPath, "Version", instanceName);
            LauncherCommon.WriteIni(iniPath, "InstanceCache", "");
            var root = mcFolder.EndsWith(Path.DirectorySeparatorChar)
                ? mcFolder
                : mcFolder + Path.DirectorySeparatorChar;
            Loading.LoaderFolderRun(
                InstanceList.mcInstanceListLoader,
                root,
                Loading.LoaderFolderRunType.ForceRun,
                1,
                "versions" + Path.DirectorySeparatorChar);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "安装完成后刷新实例列表失败");
        }
    }

    private void RefreshProgressText()
    {
        if (!_vm.IsInstalling || _installCombo is null)
            return;
        var running = _installCombo
            .GetLoaderList()
            .FirstOrDefault(loader => loader.State == LoadState.Loading);
        _vm.ProgressText = running?.DisplayName ?? Lang.Text("Download.Install.State.Loading");
        LoadInstall.Text = _vm.ProgressText;
    }

    private void RepairFiles()
    {
        if (_instance is null)
            return;
        try
        {
            var instance = _instance;
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
                    }
                }
            };
            loader.Start(instance.Name);
            Loading.LoaderTaskbarAdd(loader);
            LoadingUi.Current?.RefreshDownloadButton();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Install.Error.OperationFailed", ex);
        }
    }

    private void GoToDownloadPage()
    {
        PageRouter.Navigate(PageType.Download, PageSubType.DownloadInstall);
    }
}