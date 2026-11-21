using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.Install;
using PCL.Views.Pages.Download;

namespace PCL.Views.Pages.Instance;

/// <summary>
/// 实例安装/修改页：进入后直接展示当前实例的加载器选择，点击 Minecraft 卡片才回到版本列表。 加载器选择复用
/// <see cref="InstallLoaderSelector" />。
/// </summary>
public partial class PageInstanceInstall : MyPageRight, IRefreshable
{
    private readonly InstallLoaderSelector _selector = null!;
    private readonly bool _uiBuilt;
    private readonly PageInstanceInstallViewModel _vm = new();
    private string _baselineInfo = "";
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
        _selector = new InstallLoaderSelector(
            LoaderCardsView.Host,
            OnSelectionChanged,
            _vm.Selection);
        _vm.SetSelectedInfoProvider(BuildSelectedInfo);
        _uiBuilt = true;

        VersionList.Initialize(DownloadVersionListMode.Install, OnVersionSelected, true);
        VersionList.Activate();

        disabledPageAnimControls.Add(BtnStart);
        CardMinecraft.PreviewSwap += (_, e) =>
        {
            ExitSelection();
            e.handled = true;
        };

        BtnStart.Click += (_, _) => _ = StartInstallAsync();

        CompDownloadLoaders.DlFabricApiLoader.OnStateChangedUi += OnComponentListStateChanged;
        CompDownloadLoaders.DlLegacyFabricApiLoader.OnStateChangedUi += OnComponentListStateChanged;
        CompDownloadLoaders.DlOptiFabricLoader.OnStateChangedUi += OnComponentListStateChanged;
        GameDownload.DlLabyModListLoader.OnStateChangedUi += OnComponentListStateChanged;

        PageLoaderInit(
            LoadMinecraft,
            PanLoad,
            PanAllBack,
            null,
            GameDownload.dlClientListLoader,
            _ => EnterCurrentSelection(false));
        PanMinecraft.IsVisible = false;
        PanSelect.IsVisible = false;
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

        ReloadInstance(InstancePageHelper.ResolveInstance(null));
        if (_instance is null)
        {
            UpdateSummary(null);
            UpdateActionAvailability();
            ExitSelection();
            return;
        }

        RestartVersionCatalogs();
        PageLoaderRestart();
        if (GameDownload.dlClientListLoader.State == LoadState.Finished)
            EnterCurrentSelection(true);
    }

    public void Reload()
    {
        Refresh();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        _selector.Activate();
        VersionList.Activate();
        ReloadInstance(InstancePageHelper.ResolveInstance(parameter));
        if (_instance is null)
        {
            UpdateSummary(null);
            UpdateActionAvailability();
            ExitSelection();
            return;
        }

        _vm.IsInstalling = false;
        _selector.SetInstalling(false);
        if (GameDownload.dlClientListLoader.State != LoadState.Finished)
            return;

        EnterCurrentSelection(false);
    }

    private void UpdateSummary(McInstance? instance, bool resetVersion = true)
    {
        if (instance is null)
        {
            _vm.InstanceName = "";
            _vm.CurrentInfo = "";
            _vm.RefreshSelectedInfo();
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
            _vm.RefreshSelectedInfo();
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

    private void EnterCurrentSelection(bool forceRestartLoaders)
    {
        if (!_uiBuilt || _instance is null)
            return;

        var info = _instance.Info;
        BeginSelection(
            false,
            info.VanillaName,
            _instance.JsonObject?["url"]?.ToString(),
            $"{LauncherPaths.ImageRoot}Blocks/Grass.png",
            forceRestartLoaders,
            true);
    }

    private void ReloadInstance(McInstance? resolved)
    {
        _instance = resolved is null ? null : new McInstance(resolved.PathInstance);
    }

    private static void RestartVersionCatalogs()
    {
        try
        {
            GameDownload.DlForgeListLoader.Start(0, true);
            GameDownload.DlOptiFineListLoader.Start(null, true);
            GameDownload.DlLiteLoaderListLoader.Start(null, true);
            GameDownload.DlNeoForgeListLoader.Start(null, true);
            GameDownload.DlCleanroomListLoader.Start(null, true);
            GameDownload.DlFabricListLoader.Start(null, true);
            GameDownload.DlLegacyFabricListLoader.Start(null, true);
            GameDownload.DlLabyModListLoader.Start(null, true);
            CompDownloadLoaders.DlFabricApiLoader.Start(0, true);
            CompDownloadLoaders.DlLegacyFabricApiLoader.Start(0, true);
            CompDownloadLoaders.DlOptiFabricLoader.Start(0, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新实例修改页加载器列表失败");
        }
    }

    private void BeginSelection(
        bool reset,
        string? versionName = null,
        string? versionUrl = null,
        string? versionIcon = null,
        bool forceRestartLoaders = false,
        bool captureBaseline = false)
    {
        if (_instance is null || _vm.IsInstalling)
            return;

        LoadCurrentApiInfo(_instance);
        _vm.IsSelecting = true;

        var info = _instance.Info;
        _vm.InstanceName = _instance.Name;
        var selectedName = string.IsNullOrEmpty(versionName) ? info.VanillaName : versionName;
        _vm.VersionName = selectedName;
        _vm.VersionUrl = versionUrl ?? _instance.JsonObject?["url"]?.ToString();
        _versionIcon = versionIcon ?? $"{LauncherPaths.ImageRoot}Blocks/Grass.png";
        _vm.MinecraftIcon = _versionIcon;

        _selector.SetMinecraft(
            selectedName,
            _vm.VersionUrl,
            true,
            forceRestartLoaders);
        if (string.Equals(selectedName, info.VanillaName, StringComparison.Ordinal))
        {
            _selector.ApplyExisting(_instance);
            TryResolveExistingLabyMod();
            TryApplyCurrentApis();
        }

        if (captureBaseline)
        {
            _baselineInfo = FormatWpfSelectInfo(_selector.Selection, selectedName);
            _vm.CurrentInfo = _baselineInfo;
        }

        _vm.SetAction(
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
        WarningsView.SetWarnings(new InstallLoaderSelector.InstallLoaderWarningState());
        _vm.RefreshSelectedInfo();
        _vm.SetAction("Instance.Install.Action.StartModify");
        BtnStart.SvgIcon = "lucide/download";
        _vm.CanStartInstall = false;
        ShowSummaryStage();
        UpdateActionAvailability();
    }

    /// <summary>切换到加载器选择阶段，对应 WPF 的 EnterSelectPage。</summary>
    private void ShowSelectStage()
    {
        _vm.IsSelecting = true;
        disabledPageAnimControls.Remove(BtnStart);
        BtnStart.Show = true;
        ShowBackHint();
        if (PanSelect.IsVisible && !PanMinecraft.IsVisible)
            return;

        var wasSelectVisible = PanSelect.IsVisible;
        AnimationService.CancelAnimationByName(StageAnimationKey);
        PanSelect.IsVisible = true;
        PanBack?.ScrollToHome();
        // 与 WPF 相同：进入选择页时折叠所有卡片
        _selector.CollapseAll();
        // 选择页的内容可能被刷新/切页动画隐藏过，但不会因加载结束而再次显示，这里手动恢复
        ResetAnimControls(PanSelect);

        if (!CanAnimateStage)
        {
            PanMinecraft.IsVisible = false;
            PanMinecraft.Opacity = 1d;
            PanSelect.Opacity = 1d;
            PanBack?.IsHitTestVisible = true;
            return;
        }

        if (!wasSelectVisible)
            PanSelect.Opacity = 0d;
        PanBack?.IsHitTestVisible = false;
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(PanMinecraft, -PanMinecraft.Opacity, 100, 10),
                new ActionAnimation(() => PanBack?.ScrollToHome(), true),
                new OpacityAnimation(PanSelect, 1d - PanSelect.Opacity, 250, 150),
                new ActionAnimation(
                    () =>
                    {
                        PanMinecraft.IsVisible = false;
                        PanMinecraft.Opacity = 1d;
                        PanBack?.IsHitTestVisible = true;
                    },
                    true)
            },
            StageAnimationKey,
            true);
    }

    /// <summary>
    /// 切换到 Minecraft 版本列表阶段，对应 WPF 的 ExitSelectPage。 与 WPF
    /// 一样每次都重新生成版本列表：页面创建前版本列表可能早已加载完成， 此时不会再收到加载完成事件，若不在这里生成就会出现空白页。
    /// </summary>
    private void ShowSummaryStage(bool animate = true)
    {
        _vm.IsSelecting = false;
        if (!disabledPageAnimControls.Contains(BtnStart))
            disabledPageAnimControls.Add(BtnStart);
        BtnStart.Show = false;
        VersionList.Rebuild();
        if (PanMinecraft.IsVisible && !PanSelect.IsVisible)
            return;

        var wasMinecraftVisible = PanMinecraft.IsVisible;
        AnimationService.CancelAnimationByName(StageAnimationKey);
        PanMinecraft.IsVisible = true;
        PanBack?.ScrollToHome();
        ResetAnimControls(PanMinecraft);

        if (!animate || !CanAnimateStage)
        {
            PanSelect.IsVisible = false;
            PanSelect.Opacity = 1d;
            PanMinecraft.Opacity = 1d;
            PanBack?.IsHitTestVisible = true;
            return;
        }

        if (!wasMinecraftVisible)
            PanMinecraft.Opacity = 0d;
        PanBack?.IsHitTestVisible = false;
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(PanSelect, -PanSelect.Opacity, 90, 10),
                new ActionAnimation(() => PanBack?.ScrollToHome(), true),
                new OpacityAnimation(PanMinecraft, 1d - PanMinecraft.Opacity, 150, 100),
                new ActionAnimation(
                    () =>
                    {
                        PanSelect.IsVisible = false;
                        PanSelect.Opacity = 1d;
                        PanBack?.IsHitTestVisible = true;
                    },
                    true)
            },
            StageAnimationKey,
            true);
    }

    /// <summary>把面板内参与页面动画的控件恢复为完全显示（页面退出动画会把它们设为透明且不可点击）。</summary>
    private void ResetAnimControls(Control panel)
    {
        foreach (var control in GetAllAnimControls(panel))
        {
            if (control is MyExtraTextButton)
                continue;
            if (control is not MyListItem)
                control.IsHitTestVisible = true;
            control.Opacity = 1d;
            if (control.RenderTransform is TranslateTransform)
                control.RenderTransform = null;
        }
    }

    private static void ShowBackHint()
    {
        if (States.Hint.InstallPageBack)
            return;
        States.Hint.InstallPageBack = true;
        HintService.Show(Hints.Info(Lang.Text("Download.Install.Hint.MinecraftBack")));
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

        var isReset = IsCurrentSelection();
        _vm.RefreshSelectedInfo();
        _vm.SetAction(
            isReset
                ? "Instance.Install.Action.StartReset"
                : "Instance.Install.Action.StartModify");
        BtnStart.SvgIcon = isReset
            ? "lucide/rotate-ccw"
            : "lucide/pencil";
        _vm.CanStartInstall = !_vm.IsInstalling && !string.IsNullOrEmpty(_vm.VersionName);
        _vm.VersionIcon = McInstallFlow.ResolveSelectionIcon(_selector.Selection, _versionIcon);
        WarningsView.SetWarnings(_selector.GetWarningFlags());
    }

    private bool IsCurrentSelection()
    {
        if (_instance is null || string.IsNullOrEmpty(_baselineInfo))
            return false;
        return string.Equals(
            FormatWpfSelectInfo(_selector.Selection, _vm.VersionName),
            _baselineInfo,
            StringComparison.Ordinal);
    }

    private string BuildSelectedInfo()
    {
        if (_instance is null)
            return string.Empty;
        if (!_vm.IsSelecting)
            return _vm.CurrentInfo;

        var selectedInfo = FormatWpfSelectInfo(_selector.Selection, _vm.VersionName);
        return IsCurrentSelection() || string.IsNullOrEmpty(_baselineInfo)
            ? selectedInfo
            : $"{_baselineInfo} → {selectedInfo}";
    }

    private static string FormatWpfSelectInfo(
        InstallLoaderSelection selection,
        string? vanillaName)
    {
        var parts = new List<string>
        {
            vanillaName ?? ""
        };
        AddLoader(
            parts,
            "Common.Installation.Fabric",
            selection.FabricVersion?.Replace("+build", ""));
        AddLoader(parts, "Common.Installation.LegacyFabric", selection.LegacyFabricVersion);
        AddLoader(parts, "Common.Installation.Forge", selection.Forge?.VersionName);
        AddLoader(parts, "Common.Installation.NeoForge", selection.NeoForge?.VersionName);
        AddLoader(parts, "Common.Installation.Cleanroom", selection.Cleanroom?.VersionName);
        AddLoader(parts, "Common.Installation.LabyMod", selection.LabyModVersion);
        if (selection.OptiFine is not null)
        {
            var display = selection.OptiFine.DisplayName ?? "";
            if (!string.IsNullOrEmpty(vanillaName))
                display = display.Replace(vanillaName + " ", "");
            AddLoader(parts, "Common.Installation.OptiFine", display);
        }

        if (selection.LiteLoader is not null)
            parts.Add(Lang.Text("Common.Installation.LiteLoader"));
        if (parts.Count == 1)
            parts.Add(Lang.Text("Instance.Install.NoExtraInstall"));
        return string.Join("  |  ", parts);
    }

    private static void AddLoader(List<string> parts, string nameKey, string? version)
    {
        if (!string.IsNullOrWhiteSpace(version))
            parts.Add($"{Lang.Text(nameKey)} {version}");
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
            DownloadVersionList.GetVersionIcon(version));
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

        if (info is { HasFabric: true, HasOptiFine: true } && selection.FabricVersion is not null)
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

        foreach (
            var channel in new[]
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
            if (string.IsNullOrEmpty(selection.LabyModVersion))
            {
                selection.LabyModVersion =
                    $"{version} {(channel == "snapshot"
                        ? Lang.Text("Download.Version.Type.Snapshot")
                        : Lang.Text("Download.Version.Type.Stable"))}";
                _selector.RefreshAll();
            }

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


        if (string.IsNullOrEmpty(_vm.VersionName))
        {
            BeginSelection(false);
            return;
        }

        TryResolveExistingLabyMod();
        var instance = _instance;
        var selection = _selector.Selection;
        var isReset = IsCurrentSelection();

        if (selection.HasMainLoader
            && Config.Launch.IndieSolutionV2 is 0 or 2
            && !await McInstallFlow.ConfirmInstanceIsolationAsync())
            return;

        if (isReset)
        {
            var reset = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Install.Reset.Title"),
                    Content = Lang.Text("Instance.Install.Reset.Message"),
                    Buttons = DialogButtons.ContinueCancel
                });
            if (reset.Button == 2)
                return;
        }

        DeleteNonIsolatedLabyModFolder(instance);
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

        _instanceFolder = instance.PathInstance;
        _installCombo = McInstallFlow.TryStart(
            request,
            true,
            $"{instance.Name} {_vm.ActionText}",
            instance.PathInstance,
            "开始实例修改失败",
            OnInstallStateChanged);
        if (_installCombo is null)
            return;
        _vm.IsInstalling = true;
        _selector.SetInstalling(true);
        _vm.CanStartInstall = false;
        DeleteReplacedApiFiles();
        PageRouter.Navigate(PageIds.Launch);
    }

    private static void DeleteNonIsolatedLabyModFolder(McInstance instance)
    {
        if (!instance.Info.HasLabyMod
            || string.Equals(
                instance.PathIndie,
                instance.PathInstance,
                StringComparison.OrdinalIgnoreCase))
            return;

        var folder = Path.Combine(instance.PathIndie, "labymod-neo");
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除非隔离实例的 LabyMod 目录失败");
        }
    }

    private void OnInstallStateChanged(
        Loading.LoaderBase loader,
        LoadState newState)
    {
        if (newState is LoadState.Waiting or LoadState.Loading)
            return;

        _vm.IsInstalling = false;
        _selector.SetInstalling(false);
        McInstallRecovery.HandleTerminalState(loader, _instanceFolder, true);
        _installCombo = null;

        var finishedName = _instance?.Name ?? "";
        if (newState == LoadState.Finished)
        {
            HintService.Show(
                Hints.Success($"{finishedName} {Lang.Text("Download.Install.Finished")}"));
            InstanceList.mcInstanceListForceRefresh = true;
            McInstallFlow.ApplyFinished(finishedName);
            RefreshInstanceAfterInstall();
            return;
        }

        var error = newState == LoadState.Aborted
            ? Lang.Text("Minecraft.Download.Error.InstallationCanceled")
            : loader.Error?.Message ?? Lang.Text("Instance.Install.Error.OperationFailed");
        BtnStart.Show = true;
        UpdateActionAvailability();
        OnSelectionChanged();
        HintService.Show(Hints.Error(error));
    }

    private void RefreshInstanceAfterInstall()
    {
        if (string.IsNullOrEmpty(_instanceFolder))
            return;

        try
        {
            _instance = new McInstance(_instanceFolder);
            EnterCurrentSelection(false);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "安装完成后刷新实例信息失败");
        }
    }
}