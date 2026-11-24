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
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.Install;

namespace PCL.Views.Pages.Download;

/// <summary>
/// 下载安装页：选择 Minecraft 版本后进入配置阶段，通过 <see cref="InstallLoaderSelector" /> 选择 11
/// 种加载器与附属组件，并执行合并安装。提交后立即回到版本选择，进度与取消在下载任务栏。 安装失败或取消后由 <see cref="McInstallRecovery" />
/// 清理本次安装创建的路径，可用同名实例重试。
/// </summary>
public partial class PageDownloadInstall : MyPageRight, IPageView, IRefreshable
{
    private readonly Loading.LoaderBase.PreviewFinishEventHandler _folderLoaderHandler;
    private readonly InstallLoaderSelector _selector;
    private readonly bool _uiBuilt;
    private readonly PageDownloadInstallViewModel _vm = new();
    private bool _instanceNameEdited;
    private bool _startingInstall;
    private bool _updatingInstanceName;
    private JsonObject? _version;
    private string _versionIcon = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
    private string? _waitingVersionName;

    public PageDownloadInstall()
    {
        InitializeComponent();
        DataContext = _vm;
        PanScroll = PanBack;
        BtnStart.Show = false;
        _selector = new InstallLoaderSelector(
            LoaderCardsView.Host,
            OnSelectionChanged,
            _vm.Selection);
        _uiBuilt = true;

        VersionList.Initialize(DownloadVersionListMode.Install, SelectVersion, false);
        VersionList.Rebuilt += () => TrySelectWaitingVersion();
        VersionList.Activate();

        PageLoaderInit(
            LoadMinecraft,
            PanLoad,
            PanContent,
            null,
            GameDownload.dlClientListLoader);

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
        BtnReselect.Click += (_, _) => ShowSelectStage();
        BtnStart.Click += (_, _) => _ = StartInstallAsync();

        _folderLoaderHandler = OnMcFolderListPreviewFinish;
        GameFolders.McFolderListLoader.PreviewFinish += _folderLoaderHandler;

        ShowSelectStage();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        PanBack?.ScrollToHome();
        _selector.Activate();
        GameFolders.McFolderListLoader.PreviewFinish -= _folderLoaderHandler;
        GameFolders.McFolderListLoader.PreviewFinish += _folderLoaderHandler;
        switch (parameter)
        {
            case JsonObject version:
            {
                _waitingVersionName = null;
                VersionList.Activate();
                SelectVersion(version);
                break;
            }

            case string versionName when !string.IsNullOrWhiteSpace(versionName):
            {
                VersionList.Activate();
                _waitingVersionName = versionName;
                if (!TrySelectWaitingVersion())
                    ShowSelectStage();
                break;
            }

            default:
            {
                _waitingVersionName = null;
                VersionList.Activate();
                ShowSelectStage();
                break;
            }
        }
    }

    public override void OnNavigatedFrom()
    {
        _selector.Deactivate();
        VersionList.Deactivate();
        GameFolders.McFolderListLoader.PreviewFinish -= _folderLoaderHandler;
    }

    public void Refresh()
    {
        if (!_uiBuilt)
            return;
        VersionList.Refresh();
        if (_version is not null)
            _selector.SetMinecraft(_vm.VersionName, _vm.VersionUrl, false);
        else
            _selector.RefreshAll();
    }

    private void OnMcFolderListPreviewFinish(Loading.LoaderBase loader)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateLocation();
            if (VersionList.HasVersions)
                TrySelectWaitingVersion();
        });
    }

    /// <summary>退出安装配置阶段，回到版本选择。</summary>
    public void ExitSelectPage()
    {
        if (_vm.IsConfigStageVisible)
            ShowSelectStage();
    }

    #region 版本选择阶段

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
        if (VersionList.HasVersions)
            return;
        VersionList.Rebuild();
    }

    private void ShowSelectStage()
    {
        var animate = _vm.IsConfigStageVisible
            && PageState is PageStates.ContentEnter or PageStates.ContentStay
            && ContainerAnimation.CanAnimate(this)
            && PanSelectStage?.IsLoaded == true
            && PanConfigStage?.IsLoaded == true;
        AnimationService.CancelAnimationByName("Install Stage Switch");
        _vm.IsConfigStageVisible = false;
        PanConfigStage?.IsVisible = false;
        if (PanSelectStage is not null)
        {
            PanSelectStage.IsVisible = true;
            if (animate)
            {
                PanSelectStage.Opacity = 0d;
                PanSelectStage.RenderTransform = new TranslateTransform(-40d, 0d);
                AnimationService.Run(
                    new List<IAnimation>
                    {
                        new OpacityAnimation(PanSelectStage, 1d, 100),
                        new TranslateXAnimation(
                            PanSelectStage,
                            40d,
                            200,
                            0,
                            new FluentEaseOut(EasePower.ExtraStrong))
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
        _vm.CanStartInstall = false;
        _selector.SetMinecraft(null, null);
        BtnStart.Show = false;
        BuildVersionListWhenReady();
        UpdateLocation();
        PanBack?.ScrollToHome();
    }

    private void ShowConfigStage()
    {
        var animate = !_vm.IsConfigStageVisible
            && PageState is PageStates.ContentEnter or PageStates.ContentStay
            && ContainerAnimation.CanAnimate(this)
            && PanSelectStage?.IsLoaded == true
            && PanConfigStage?.IsLoaded == true;
        AnimationService.CancelAnimationByName("Install Stage Switch");
        PanSelectStage?.IsVisible = false;
        _vm.IsConfigStageVisible = true;
        if (PanConfigStage is not null)
        {
            PanConfigStage.IsVisible = true;
            if (animate)
            {
                PanConfigStage.Opacity = 0d;
                PanConfigStage.RenderTransform = new TranslateTransform(40d, 0d);
                AnimationService.Run(
                    new List<IAnimation>
                    {
                        new OpacityAnimation(PanConfigStage, 1d, 100),
                        new TranslateXAnimation(
                            PanConfigStage,
                            -40d,
                            200,
                            0,
                            new FluentEaseOut(EasePower.ExtraStrong))
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
        _versionIcon = DownloadVersionList.GetVersionIcon(version);
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
        WarningsView.SetWarnings(_selector.GetWarningFlags());
    }

    private void RefreshLogo()
    {
        if (ImgLogo is null)
            return;
        var logo = McInstallFlow.ResolveSelectionIcon(_selector.Selection, _versionIcon);
        ImgLogo.Source = logo;
        _vm.VersionIcon = logo;
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
            var canStart = _version is not null && TextInstanceName.IsValidated;
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
        if (e.Key != Key.Enter || _startingInstall || !BtnStart.IsEnabled)
            return;
        _ = StartInstallAsync();
        e.Handled = true;
    }

    private async Task StartInstallAsync()
    {
        if (_startingInstall)
            return;
        _startingInstall = true;
        try
        {
            if (!RestrictedFeatures.Check("minecraft-install"))
                return;

            if (_version is null || string.IsNullOrEmpty(_vm.VersionName))
            {
                ShowSelectStage();
                return;
            }

            if (_selector.Selection.HasAny
                && Config.Launch.IndieSolutionV2 is 0 or 2
                && !await McInstallFlow.ConfirmInstanceIsolationAsync())
                return;

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

            var installCombo = McInstallFlow.TryStart(
                request,
                overwrite,
                $"{name} {Lang.Text("Download.Install.Title")}",
                instanceFolder,
                "开始安装失败",
                (loader, newState) => OnInstallFinished(loader, newState, name, instanceFolder));
            if (installCombo is null)
                return;
            ShowSelectStage();
        }
        finally
        {
            _startingInstall = false;
        }
    }

    private void OnInstallFinished(
        Loading.LoaderBase loader,
        LoadState newState,
        string instanceName,
        string instanceFolder)
    {
        // 失败或取消时回滚本次安装创建的路径；成功时清理备份
        McInstallRecovery.HandleTerminalState(loader, instanceFolder);
        if (newState == LoadState.Finished)
        {
            McInstallFlow.ApplyFinished(instanceName);
            HintService.Show(
                Hints.Success($"{instanceName} {Lang.Text("Download.Install.Finished")}"));
            VersionList.RefreshInstalledMarks();
            return;
        }

        HintService.Show(
            Hints.Error(
                newState == LoadState.Aborted
                    ? Lang.Text("Minecraft.Download.Error.InstallationCanceled")
                    : loader.Error?.Message
                    ?? Lang.Text("Download.Install.Error.OperationFailed")));
    }

    #endregion
}