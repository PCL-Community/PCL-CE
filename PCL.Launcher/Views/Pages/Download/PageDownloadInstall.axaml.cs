using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Download;

/// <summary>
///     下载安装页：选择 Minecraft 版本后进入配置阶段，通过 <see cref="InstallLoaderSelector" />
///     选择 11 种加载器与附属组件，并执行合并安装。
///     安装失败或取消后由 <see cref="McInstallRecovery" /> 清理本次安装创建的路径，可用同名实例重试。
/// </summary>
public partial class PageDownloadInstall : MyPageRight, IPageView, IRefreshable
{
    private readonly Loading.LoaderBase.PreviewFinishEventHandler _folderLoaderHandler;
    private readonly DispatcherTimer _progressTimer;
    private readonly InstallLoaderSelector _selector;
    private readonly bool _uiBuilt;
    private readonly PageDownloadInstallViewModel _vm = new();
    private Loading.LoaderCombo<string>? _installCombo;
    private string _instanceFolder = "";
    private string _instanceName = "";
    private bool _instanceNameEdited;
    private bool _updatingInstanceName;
    private JsonObject? _version;
    private string _versionIcon = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
    private bool _versionListBuilt;
    private string? _waitingVersionName;

    public PageDownloadInstall()
    {
        InitializeComponent();
        DataContext = _vm;
        PanScroll = PanBack;
        BtnStart.Show = false;
        _selector = new InstallLoaderSelector(PanLoaderCards, OnSelectionChanged, _vm.Selection);
        _uiBuilt = true;

        PageLoaderInit(
            LoadMinecraft,
            PanLoad,
            PanContent,
            null,
            GameDownload.dlClientListLoader,
            _ => OnVersionListFinished());

        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _progressTimer.Tick += (_, _) => RefreshProgressText();

        TextInstanceName.ValidatedTextChanged += (_, _) => UpdateLocation();
        TextInstanceName.TextChanged += (_, _) =>
        {
            var text = TextInstanceName.Text?.Trim() ?? "";
            if (!_updatingInstanceName
                && !string.Equals(text, _vm.InstanceName, StringComparison.Ordinal))
                _instanceNameEdited = true;
            _vm.InstanceName = text;
            UpdateLocation();
        };
        TextInstanceName.KeyDown += OnInstanceNameKeyDown;
        BtnReselect.Click += (_, _) =>
        {
            if (!_vm.IsInstalling)
                ShowSelectStage();
        };
        BtnStart.Click += (_, _) =>
        {
            if (!_vm.IsInstalling)
                _ = StartInstallAsync();
        };
        BtnCancel.Click += (_, _) => _installCombo?.Abort();

        _folderLoaderHandler = OnMcFolderListPreviewFinish;
        GameFolders.McFolderListLoader.PreviewFinish += _folderLoaderHandler;

        BuildVersionListWhenReady();
        ShowSelectStage();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack?.ScrollToHome();
        _selector.Activate();
        GameFolders.McFolderListLoader.PreviewFinish -= _folderLoaderHandler;
        GameFolders.McFolderListLoader.PreviewFinish += _folderLoaderHandler;
        if (_vm.IsInstalling)
            return;
        if (parameter is JsonObject version)
        {
            _waitingVersionName = null;
            SelectVersion(version);
        }
        else if (parameter is string versionName && !string.IsNullOrWhiteSpace(versionName))
        {
            _waitingVersionName = versionName;
            if (!TrySelectWaitingVersion())
                ShowSelectStage();
        }
        else
        {
            _waitingVersionName = null;
            ShowSelectStage();
        }
    }

    public override void OnNavigatedFrom()
    {
        _selector.Deactivate();
        GameFolders.McFolderListLoader.PreviewFinish -= _folderLoaderHandler;
    }

    public void Refresh()
    {
        if (!_uiBuilt || _vm.IsInstalling)
            return;
        _versionListBuilt = false;
        GameDownload.dlClientListLoader.Start(null, true);
        if (_version is not null)
            _selector.SetMinecraft(_vm.VersionName, _vm.VersionUrl, false);
        else
            _selector.RefreshAll();
        BuildVersionListWhenReady();
    }

    private void OnMcFolderListPreviewFinish(Loading.LoaderBase loader)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateLocation();
            if (_versionListBuilt)
                OnVersionListFinished();
        });
    }

    protected override void RefreshLocalization()
    {
        if (!_uiBuilt)
            return;
        _vm.StartButtonText = Lang.Text("Download.Install.StartDownload");
        _selector.RefreshLocalization();
        if (_version is not null)
        {
            UpdateWarnings();
            OnSelectionChanged();
        }

        if (_versionListBuilt
            && GameDownload.dlClientListLoader.output.Value?["versions"] is JsonArray)
            OnVersionListFinished();
    }

    /// <summary>退出安装配置阶段，回到版本选择。</summary>
    public void ExitSelectPage()
    {
        if (!_vm.IsInstalling)
            ShowSelectStage();
    }

    #region 版本选择阶段

    private void OnVersionListFinished()
    {
        try
        {
            if (PanVersions is null)
                return;
            if (GameDownload.dlClientListLoader.output.Value?["versions"] is not JsonArray versions)
                return;
            DownloadVersionList.Build(PanVersions, versions, SelectVersion);
            _versionListBuilt = true;
            TrySelectWaitingVersion();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "可视化安装页版本列表出错");
        }
    }

    private bool TrySelectWaitingVersion()
    {
        if (string.IsNullOrEmpty(_waitingVersionName))
            return false;
        if (GameDownload.dlClientListLoader.output.Value?["versions"] is not JsonArray versions)
            return false;
        var version = versions
            .OfType<JsonObject>()
            .FirstOrDefault(entry =>
                string.Equals(
                    entry["id"]?.ToString(),
                    _waitingVersionName,
                    StringComparison.Ordinal));
        if (version is null)
        {
            _waitingVersionName = null;
            return false;
        }

        _waitingVersionName = null;
        SelectVersion(version);
        return true;
    }

    private void BuildVersionListWhenReady()
    {
        if (_versionListBuilt)
            return;
        OnVersionListFinished();
    }

    private void ShowSelectStage()
    {
        var animate = ContainerAnimation.CanAnimate(this)
            && PanSelectStage?.IsLoaded == true
            && PanConfigStage?.IsLoaded == true;
        _vm.IsConfigStageVisible = false;
        PanConfigStage?.IsVisible = false;
        if (PanSelectStage is not null)
        {
            PanSelectStage.IsVisible = true;
            if (animate)
            {
                PanSelectStage.Opacity = 0d;
                PanSelectStage.RenderTransform = new TranslateTransform(-40d, 0d);
                AniStart(
                    new[]
                    {
                        AaOpacity(PanSelectStage, 1d, 100),
                        AaTranslateX(
                            PanSelectStage,
                            40d,
                            200,
                            0,
                            new AniEaseOutFluent(AniEasePower.ExtraStrong))
                    },
                    "Install Stage Switch");
            }
            else
            {
                PanSelectStage.Opacity = 1d;
                PanSelectStage.RenderTransform = null;
            }
        }

        PanContent?.Margin = new Thickness(25d, 10d, 25d, 25d);
        _instanceNameEdited = false;
        _version = null;
        _versionIcon = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
        _vm.VersionName = "";
        _vm.VersionUrl = null;
        _vm.VersionIcon = _versionIcon;
        _vm.IsProgressVisible = false;
        _vm.CanStartInstall = false;
        _selector.SetMinecraft(null, null);
        BtnStart.Show = false;
        if (!_versionListBuilt)
            OnVersionListFinished();
        UpdateLocation();
        PanBack?.ScrollToHome();
    }

    private void ShowConfigStage()
    {
        var animate = ContainerAnimation.CanAnimate(this)
            && PanSelectStage?.IsLoaded == true
            && PanConfigStage?.IsLoaded == true;
        PanSelectStage?.IsVisible = false;
        _vm.IsConfigStageVisible = true;
        if (PanConfigStage is not null)
        {
            PanConfigStage.IsVisible = true;
            if (animate)
            {
                PanConfigStage.Opacity = 0d;
                PanConfigStage.RenderTransform = new TranslateTransform(40d, 0d);
                AniStart(
                    new[]
                    {
                        AaOpacity(PanConfigStage, 1d, 100),
                        AaTranslateX(
                            PanConfigStage,
                            -40d,
                            200,
                            0,
                            new AniEaseOutFluent(AniEasePower.ExtraStrong))
                    },
                    "Install Stage Switch");
            }
            else
            {
                PanConfigStage.Opacity = 1d;
                PanConfigStage.RenderTransform = null;
            }
        }

        PanContent?.Margin = new Thickness(25d, 10d, 25d, 40d);
        BtnStart.Show = true;
        UpdateLocation();
        PanBack?.ScrollToHome();
    }

    private void SelectVersion(JsonObject version)
    {
        _waitingVersionName = null;
        _version = version;
        _vm.VersionName = version["id"]?.ToString() ?? "";
        _vm.VersionUrl = version["url"]?.ToString();
        _versionIcon = GetVersionIcon(version);
        _vm.VersionIcon = _versionIcon;
        if (string.IsNullOrEmpty(_vm.VersionName))
        {
            ShowSelectStage();
            return;
        }

        _instanceNameEdited = false;
        SetDefaultInstanceName();
        _selector.SetMinecraft(_vm.VersionName, _vm.VersionUrl);
        RefreshLogo();
        UpdateWarnings();
        ShowConfigStage();
    }

    private void SetDefaultInstanceName()
    {
        var name = BuildDefaultInstanceName();
        try
        {
            var folder = GameFolders.mcFolderSelected;
            if (!string.IsNullOrEmpty(folder))
            {
                var versionsFolder = Path.Combine(folder, "versions");
                var index = 2;
                while (Directory.Exists(Path.Combine(versionsFolder, name)))
                {
                    name = $"{BuildDefaultInstanceName()} ({index})";
                    index++;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "生成默认实例名称失败");
        }

        _updatingInstanceName = true;
        try
        {
            _vm.InstanceName = name;
            TextInstanceName.Text = name;
        }
        finally
        {
            _updatingInstanceName = false;
        }

        TextInstanceName.ValidateRules =
        [
            new FolderNameValidator(
                string.IsNullOrEmpty(GameFolders.mcFolderSelected)
                    ? null
                    : Path.Combine(GameFolders.mcFolderSelected, "versions"),
                ignoreSameNameInParentFolder: true)
        ];
        TextInstanceName.Validate();
        UpdateLocation();
    }

    private string BuildDefaultInstanceName()
    {
        var name = string.IsNullOrEmpty(_vm.VersionName) ? "Minecraft" : _vm.VersionName;
        return name + _selector.Selection.GetSuffix();
    }

    private void RefreshInstanceNameIfNeeded()
    {
        if (_instanceNameEdited || string.IsNullOrEmpty(_vm.VersionName))
            return;
        SetDefaultInstanceName();
    }

    private void OnSelectionChanged()
    {
        if (_selector is null)
            return;
        RefreshInstanceNameIfNeeded();
        RefreshLogo();
        UpdateWarnings();
        UpdateLocation();
    }

    private void UpdateWarnings()
    {
        var warnings = _selector.GetWarnings();
        var flags = _selector.GetWarningFlags();
        _vm.LoaderWarning = string.Join(Environment.NewLine, warnings);
        _vm.HasLoaderWarning = warnings.Count > 0;
        _vm.ShowHintFabricApi = flags.FabricApi;
        _vm.ShowHintLegacyFabricApi = flags.LegacyFabricApi;
        _vm.ShowHintOptiFabric = flags.OptiFabric;
        _vm.ShowHintOptiFabricOld = flags.OptiFabricOld;
        _vm.ShowHintLegacyOptiFabric = flags.LegacyOptiFabric;
        _vm.ShowHintModOptiFine = flags.ModOptiFine;
    }

    private void RefreshLogo()
    {
        if (ImgLogo is null)
            return;
        var selection = _selector.Selection;
        var logo = selection.FabricVersion is not null || selection.LegacyFabricVersion is not null
            ? $"{LauncherPaths.ImageRoot}Blocks/Fabric.png"
            : selection.Forge is not null
                ? $"{LauncherPaths.ImageRoot}Blocks/Anvil.png"
                : selection.NeoForge is not null
                    ? $"{LauncherPaths.ImageRoot}Blocks/NeoForge.png"
                    : selection.LiteLoader is not null
                        ? $"{LauncherPaths.ImageRoot}Blocks/Egg.png"
                        : selection.OptiFine is not null
                            ? $"{LauncherPaths.ImageRoot}Blocks/GrassPath.png"
                            : selection.Cleanroom is not null
                                ? $"{LauncherPaths.ImageRoot}Blocks/Cleanroom.png"
                                : selection.LabyModCommitRef is not null
                                    ? $"{LauncherPaths.ImageRoot}Blocks/LabyMod.png"
                                    : _versionIcon;
        ImgLogo.Source = logo;
        _vm.VersionIcon = logo;
    }

    private static string GetVersionIcon(JsonObject version)
    {
        var file = (version["type"]?.ToString() ?? string.Empty) switch
        {
            "release" => "Blocks/Grass.png",
            "snapshot" or "pending" => "Blocks/CommandBlock.png",
            "special" => "Blocks/GoldBlock.png",
            _ => "Blocks/CobbleStone.png"
        };
        return LauncherPaths.ImageRoot + file;
    }

    private void UpdateLocation()
    {
        try
        {
            var folder = GameFolders.mcFolderSelected;
            if (string.IsNullOrEmpty(folder))
            {
                _vm.HasNoFolder = true;
                _vm.CanStartInstall = false;
                BtnStart.IsEnabled = false;
                return;
            }

            _vm.HasNoFolder = false;
            var canStart = !_vm.IsInstalling
                && _version is not null
                && TextInstanceName.IsValidated;
            _vm.CanStartInstall = canStart;
            BtnStart.IsEnabled = canStart;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新安装位置失败");
        }
    }

    #endregion

    #region 安装

    private void OnInstanceNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm.IsInstalling || !BtnStart.IsEnabled)
            return;
        _ = StartInstallAsync();
        e.Handled = true;
    }

    private async Task StartInstallAsync()
    {
        if (_vm.IsInstalling)
            return;

        if (!RestrictedFeatures.Check("minecraft-install"))
            return;

        if (_version is null || string.IsNullOrEmpty(_vm.VersionName))
        {
            ShowSelectStage();
            return;
        }

        if (_selector.Selection.HasAny
            && Config.Launch.IndieSolutionV2 is 0 or 2)
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

        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(folder))
        {
            HintService.Show(Hints.Error(Lang.Text("Download.Install.NoFolder")));
            _vm.HasNoFolder = true;
            return;
        }

        var name = TextInstanceName.Text?.Trim() ?? "";
        TextInstanceName.Validate();
        if (!TextInstanceName.IsValidated)
        {
            HintService.Show(
                Hints.Error(
                    string.IsNullOrEmpty(TextInstanceName.ValidateResult)
                        ? Lang.Text("Download.Install.NameInvalid")
                        : TextInstanceName.ValidateResult));
            return;
        }

        var instanceFolder = Path.Combine(folder, "versions", name)
            + Path.DirectorySeparatorChar;
        var instanceJsonPath = Path.Combine(instanceFolder, $"{name}.json");
        var overwrite = false;
        if (File.Exists(instanceJsonPath))
        {
            var exists = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                    Content = Lang.Text(
                        "Minecraft.Download.Error.InstanceAlreadyExists",
                        name,
                        Environment.NewLine),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!exists.IsConfirmed)
                return;
            overwrite = true;
        }

        var request = new McInstallRequest
        {
            targetInstanceName = name,
            targetInstanceFolder = instanceFolder,
            minecraftJson = _vm.VersionUrl,
            minecraftName = _vm.VersionName
        };
        _selector.Selection.ApplyTo(request);

        var identifier = McInstallPipeline.GetIdentifier(instanceFolder);
        if (Loading.loaderTaskbar.Any(loader => loader.Identifier == identifier))
        {
            HintService.Show(
                Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
            return;
        }

        List<Loading.LoaderBase> loaders;
        try
        {
            loaders = McInstallPipeline.CreateInstallLoader(request, overwrite);
        }
        catch (CancelledException)
        {
            return;
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error(ex.Message));
            LauncherCommon.Log(ex, "开始安装失败");
            return;
        }

        _instanceName = name;
        _instanceFolder = instanceFolder;
        _vm.IsInstalling = true;
        _vm.CanStartInstall = false;
        _vm.IsProgressVisible = true;
        _selector.SetInstalling(true);
        BtnStart.Show = false;
        BtnReselect.IsEnabled = false;
        LoadInstall.Text = Lang.Text("Download.Install.State.Loading");
        LoadInstall.TextError = "";
        LoadInstall.ShowProgress = true;
        LoadInstall.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
        _progressTimer.Start();

        _installCombo = new Loading.LoaderCombo<string>(
            $"{name} {Lang.Text("Download.Install.Title")}",
            loaders)
        {
            Identifier = identifier
        };
        _installCombo.OnStateChangedUi += OnInstallStateChanged;
        _installCombo.Start(instanceFolder);
        Loading.LoaderTaskbarAdd(_installCombo);
        LoadingUi.Current?.RefreshDownloadButton();
    }

    private void OnInstallStateChanged(
        Loading.LoaderBase loader,
        LoadState newState,
        LoadState oldState)
    {
        if (newState is LoadState.Waiting or LoadState.Loading)
            return;

        _progressTimer.Stop();
        LoadInstall.ShowProgress = false;
        _vm.IsInstalling = false;
        _selector.SetInstalling(false);
        BtnReselect.IsEnabled = true;
        UpdateLocation();

        // 失败或取消时回滚本次安装创建的路径；成功时清理备份
        McInstallRecovery.HandleTerminalState(loader, _instanceFolder);
        if (_installCombo is not null)
        {
            _installCombo.OnStateChangedUi -= OnInstallStateChanged;
            _installCombo = null;
        }

        if (newState == LoadState.Finished)
        {
            _vm.IsProgressVisible = false;
            LoadInstall.State = new MyLoadingStateSimulator();
            ApplyFinishedBookkeeping(_instanceName);
            HintService.Show(
                Hints.Success($"{_instanceName} {Lang.Text("Download.Install.Finished")}"));
            _versionListBuilt = false;
            OnVersionListFinished();
            _selector.RefreshAll();
            ShowSelectStage();
            return;
        }

        LoadInstall.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Error
        };
        LoadInstall.TextError = newState == LoadState.Aborted
            ? Lang.Text("Minecraft.Download.Error.InstallationCanceled")
            : loader.Error?.Message ?? Lang.Text("Download.Install.Error.OperationFailed");
        BtnStart.Show = true;
        UpdateLocation();
        HintService.Show(Hints.Error(LoadInstall.TextError));
        _selector.RefreshAll();
    }

    private static void ApplyFinishedBookkeeping(string instanceName)
    {
        try
        {
            var mcFolder = GameFolders.mcFolderSelected;
            if (string.IsNullOrEmpty(mcFolder))
                return;
            var iniPath = Path.Combine(mcFolder, "PCL.ini");
            if (Config.Download.AutoSelectInstance)
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
                $"versions{Path.DirectorySeparatorChar}");
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

    #endregion
}