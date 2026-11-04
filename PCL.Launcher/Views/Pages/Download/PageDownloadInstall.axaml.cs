using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>
///     下载安装页：选择 Minecraft 版本后进入配置阶段，可选装当前安装管线支持的加载器
///     （Fabric / Legacy Fabric）并执行合并安装。
///     安装失败或取消后由 <see cref="McInstallRecovery" /> 清理本次安装创建的路径，可用同名实例重试。
/// </summary>
public partial class PageDownloadInstall : MyPageRight, IPageView, IRefreshable
{
    private static readonly (string TitleKey, DownloadLoaderKind Kind)[] LoaderDefinitions =
    [
        ("Common.Installation.Fabric", DownloadLoaderKind.Fabric),
        ("Common.Installation.LegacyFabric", DownloadLoaderKind.LegacyFabric)
    ];

    private readonly List<LoaderCard> _loaderCards = [];
    private readonly DispatcherTimer _progressTimer;
    private Loading.LoaderCombo<string>? _installCombo;
    private string _instanceFolder = "";
    private string _instanceName = "";
    private bool _isInstalling;
    private bool _loaderCardsBuilt;
    private JsonObject? _version;
    private bool _versionListBuilt;
    private string _versionName = "";
    private string? _versionUrl;

    public PageDownloadInstall()
    {
        InitializeComponent();
        PanScroll = PanBack;
        BtnStart.IsVisible = false;
        BtnStart.IsHitTestVisible = false;

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
        BtnReselect.Click += (_, _) =>
        {
            if (!_isInstalling)
                ShowSelectStage();
        };
        BtnStart.Click += (_, _) =>
        {
            if (!_isInstalling)
                StartInstall();
        };
        BtnCancel.Click += (_, _) => _installCombo?.Abort();

        GameFolders.McFolderListLoader.PreviewFinish +=
            _ => Dispatcher.UIThread.Post(() =>
            {
                UpdateLocation();
                if (_versionListBuilt)
                    OnVersionListFinished();
            });

        GameDownload.DlFabricListLoader.PreviewFinish +=
            _ => Dispatcher.UIThread.Post(() => RefreshLoaderCard(DownloadLoaderKind.Fabric));
        GameDownload.DlLegacyFabricListLoader.PreviewFinish +=
            _ => Dispatcher.UIThread.Post(() => RefreshLoaderCard(DownloadLoaderKind.LegacyFabric));

        BuildLoaderCards();
        ShowSelectStage();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack?.ScrollToHome();
        if (_isInstalling)
            return;
        if (parameter is JsonObject version)
            SelectVersion(version);
        else
            ShowSelectStage();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        if (_isInstalling)
            return;
        _versionListBuilt = false;
        GameDownload.dlClientListLoader.Start(null, true);
        GameDownload.DlFabricListLoader.Start(null, true);
        GameDownload.DlLegacyFabricListLoader.Start(null, true);
    }

    protected override void RefreshLocalization()
    {
        foreach (var loaderCard in _loaderCards)
        {
            loaderCard.Card.Title = Lang.Text(loaderCard.TitleKey);
            if (loaderCard.Kind is { } kind)
                RefreshLoaderCard(kind, loaderCard);
        }

        if (string.IsNullOrEmpty(_versionName))
            return;
        var formatted = McFormatter.FormatVersion(_versionName).Replace("_", " ");
        LabVersionName.Text = formatted;
        var category = McVersionClassifier.ClassifyVersion(_version!);
        LabVersionInfo.Text =
            $"{Lang.Text(McVersionClassifier.GetCategoryKey(category))} | "
            + Lang.Text(
                "Download.Version.ReleaseDate",
                Lang.Date(McVersionClassifier.GetReleaseTime(_version!), "g"));
        UpdateLocation();
        HintLoaderWarning.Text = GetLoaderWarningText();
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
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "可视化安装页版本列表出错");
        }
    }

    private void ShowSelectStage()
    {
        if (PanSelectStage is not null) PanSelectStage.IsVisible = true;
        if (PanConfigStage is not null) PanConfigStage.IsVisible = false;
        BtnStart.Show = false;
        if (!_versionListBuilt)
            OnVersionListFinished();
        UpdateLocation();
        PanBack?.ScrollToHome();
    }

    private void ShowConfigStage()
    {
        if (PanSelectStage is not null) PanSelectStage.IsVisible = false;
        if (PanConfigStage is not null) PanConfigStage.IsVisible = true;
        BtnStart.Show = true;
        BtnStart.IsEnabled = !_isInstalling;
        PanBack?.ScrollToHome();
    }

    private void SelectVersion(JsonObject version)
    {
        _version = version;
        _versionName = version["id"]?.ToString() ?? "";
        _versionUrl = version["url"]?.ToString();
        if (string.IsNullOrEmpty(_versionName))
        {
            ShowSelectStage();
            return;
        }

        var formatted = McFormatter.FormatVersion(_versionName).Replace("_", " ");
        LabVersionName.Text = formatted;
        var category = McVersionClassifier.ClassifyVersion(version);
        LabVersionInfo.Text =
            $"{Lang.Text(McVersionClassifier.GetCategoryKey(category))} | "
            + Lang.Text(
                "Download.Version.ReleaseDate",
                Lang.Date(McVersionClassifier.GetReleaseTime(version), "g"));
        IconVersion.Icon = (version["type"]?.ToString() ?? "") switch
        {
            "release" => "lucide/box",
            "snapshot" or "pending" => "lucide/git-pull-request-arrow",
            "special" => "lucide/sparkles",
            _ => "lucide/file-archive"
        };

        SetDefaultInstanceName(formatted);
        StartLoaderListLoaders();
        RefreshAllLoaderCards();
        ShowConfigStage();
    }

    private void StartLoaderListLoaders()
    {
        GameDownload.DlFabricListLoader.Start(null, true);
        GameDownload.DlLegacyFabricListLoader.Start(null, true);
    }

    private void SetDefaultInstanceName(string baseName)
    {
        var name = baseName;
        try
        {
            var folder = GameFolders.mcFolderSelected;
            if (!string.IsNullOrEmpty(folder))
            {
                var versionsFolder = Path.Combine(folder, "versions");
                var index = 2;
                while (Directory.Exists(Path.Combine(versionsFolder, name)))
                {
                    name = $"{baseName} ({index})";
                    index++;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "生成默认实例名称失败");
        }

        TextInstanceName.Text = name;
        TextInstanceName.ValidateRules =
        [
            new FolderNameValidator(
                string.IsNullOrEmpty(GameFolders.mcFolderSelected)
                    ? null
                    : Path.Combine(GameFolders.mcFolderSelected, "versions"))
        ];
        TextInstanceName.Validate();

        UpdateLocation();
    }

    private void UpdateLocation()
    {
        try
        {
            var folder = GameFolders.mcFolderSelected;
            if (string.IsNullOrEmpty(folder))
            {
                LabLocation.Text = "";
                HintNoFolder.IsVisible = true;
                BtnStart.IsEnabled = false;
                return;
            }

            HintNoFolder.IsVisible = false;
            BtnStart.IsEnabled = !_isInstalling;
            var name = TextInstanceName.Text?.Trim() ?? "";
            LabLocation.Text = Path.Combine(folder, "versions", name);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新安装位置失败");
        }
    }

    #endregion

    #region 加载器卡片

    private sealed class LoaderCard
    {
        public required StackPanel Body;
        public required MyCard Card;
        public required TextBlock Info;
        public DownloadLoaderKind? Kind;
        public string? SelectedVersion;
        public required string TitleKey;
    }

    private void BuildLoaderCards()
    {
        if (_loaderCardsBuilt || PanLoaderCards is null)
            return;
        _loaderCardsBuilt = true;

        foreach (var (titleKey, kind) in LoaderDefinitions)
        {
            var card = new MyCard
            {
                Title = Lang.Text(titleKey),
                Margin = new Thickness(0),
                UseAnimation = false
            };
            var info = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12d,
                Opacity = 0.7d,
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsHitTestVisible = false
            };
            var header = new Grid
            {
                Height = 40d,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(132d, 0d, 44d, 0d)
            };
            if (GetLoaderLogo(titleKey) is { } logoPath)
                header.Children.Add(new MyImage
                {
                    Source = logoPath,
                    Width = 50d,
                    Height = 50d,
                    Margin = new Thickness(-112d, -5d, 0d, 0d),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false
                });
            header.Children.Add(info);
            card.Children.Add(header);

            var clearButton = new MyIconButton
            {
                Width = 30d,
                Height = 30d,
                Margin = new Thickness(0d, 5d, 32d, 0d),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Theme = MyIconButton.Themes.Custom,
                SvgIcon = "lucide/x",
                IsVisible = false
            };
            card.Children.Add(clearButton);

            var body = new StackPanel
            {
                Margin = new Thickness(20d, 40d, 18d, 15d),
                Spacing = 2d
            };
            card.Children.Add(body);
            card.SwapControl = body;
            card.IsSwapped = true;

            var loaderCard = new LoaderCard
            {
                TitleKey = titleKey,
                Kind = kind,
                Card = card,
                Body = body,
                Info = info
            };

            var cardKind = kind;
            clearButton.Click += (_, _) =>
            {
                loaderCard.SelectedVersion = null;
                RefreshLoaderCard(cardKind, loaderCard);
                card.IsSwapped = true;
            };
            card.Swap += (_, _) =>
            {
                if (!card.IsSwapped)
                    RefreshLoaderCard(cardKind, loaderCard);
            };
            info.Text = Lang.Text("Download.Install.State.CanAdd");

            _loaderCards.Add(loaderCard);
            PanLoaderCards.Children.Add(card);
        }
    }

    private static string? GetLoaderLogo(string titleKey) =>
        titleKey switch
        {
            "Common.Installation.Forge" => LauncherPaths.ImageRoot + "Blocks/Anvil.png",
            "Common.Installation.Cleanroom" => LauncherPaths.ImageRoot + "Blocks/Cleanroom.png",
            "Common.Installation.NeoForge" => LauncherPaths.ImageRoot + "Blocks/NeoForge.png",
            "Common.Installation.Fabric" or "Common.Installation.FabricApi" =>
                LauncherPaths.ImageRoot + "Blocks/Fabric.png",
            "Common.Installation.LegacyFabric" or "Common.Installation.LegacyFabricApi" =>
                LauncherPaths.ImageRoot + "Blocks/Fabric.png",
            "Common.Installation.LabyMod" => LauncherPaths.ImageRoot + "Blocks/LabyMod.png",
            "Common.Installation.OptiFine" or "Common.Installation.OptiFabric" =>
                LauncherPaths.ImageRoot + "Blocks/GrassPath.png",
            "Common.Installation.LiteLoader" => LauncherPaths.ImageRoot + "Blocks/Egg.png",
            _ => null
        };

    private void RefreshAllLoaderCards()
    {
        foreach (var loaderCard in _loaderCards)
            if (loaderCard.Kind is { } kind)
                RefreshLoaderCard(kind, loaderCard);
        HintLoaderWarning.Text = GetLoaderWarningText();
        HintLoaderWarning.IsVisible = !string.IsNullOrEmpty(HintLoaderWarning.Text);
    }

    private void RefreshLoaderCard(DownloadLoaderKind? kind)
    {
        if (kind is not { } kindValue)
            return;
        foreach (var loaderCard in _loaderCards)
            if (loaderCard.Kind == kindValue)
                RefreshLoaderCard(kindValue, loaderCard);
    }

    private void RefreshLoaderCard(DownloadLoaderKind kind, LoaderCard loaderCard)
    {
        var card = loaderCard;
        card.Info.Text = card.SelectedVersion is null
            ? Lang.Text("Download.Install.State.CanAdd")
            : $"{Lang.Text(card.TitleKey)} {card.SelectedVersion}";

        var body = card.Body;
        body.Children.Clear();

        var listLoader = kind == DownloadLoaderKind.Fabric
            ? (Loading.LoaderBase)GameDownload.DlFabricListLoader
            : GameDownload.DlLegacyFabricListLoader;
        if (listLoader.State != LoadState.Finished)
        {
            body.Children.Add(
                new MyLoading
                {
                    Margin = new Thickness(5d),
                    Text = Lang.Text("Download.Install.State.Getting")
                });
            return;
        }

        var gameVersions = GetGameVersions(kind);
        if (gameVersions is null || !gameVersions.Contains(_versionName))
        {
            body.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.NoVersion"),
                    Margin = new Thickness(6d, 4d, 0d, 4d),
                    Opacity = 0.7d
                });
            return;
        }

        var versions = GetLoaderVersions(kind);
        if (versions is null || versions.Count == 0)
        {
            body.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.NoAvailableVersion"),
                    Margin = new Thickness(6d, 4d, 0d, 4d),
                    Opacity = 0.7d
                });
            return;
        }

        foreach (var node in versions.OfType<JsonObject>())
        {
            var version = (node["version"]?.ToString() ?? "").Replace("+build", "");
            if (string.IsNullOrEmpty(version))
                continue;
            var isStable = node["stable"]?.GetValue<bool>() == true;
            var item = new MyListItem
            {
                Title = version,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Logo = LauncherPaths.ImageRoot + "Blocks/Fabric.png",
                LogoScale = 1d,
                Info = Lang.Text(
                    isStable
                        ? "Download.Version.Type.Stable"
                        : "Download.Version.Type.Preview")
            };
            item.Click += (_, _) => SelectLoaderVersion(kind, loaderCard, version);
            body.Children.Add(item);
        }

        if (body.Children.Count == 0)
            body.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.NoAvailableVersion"),
                    Margin = new Thickness(6d, 4d, 0d, 4d),
                    Opacity = 0.7d
                });
    }

    private void SelectLoaderVersion(
        DownloadLoaderKind kind,
        LoaderCard loaderCard,
        string version)
    {
        if (_isInstalling)
            return;

        foreach (var other in _loaderCards)
            if (!ReferenceEquals(other, loaderCard) && other.Kind is not null)
                other.SelectedVersion = null;

        loaderCard.SelectedVersion = version;
        loaderCard.Card.IsSwapped = true;
        RefreshAllLoaderCards();
    }

    private JsonArray? GetLoaderVersions(DownloadLoaderKind kind)
    {
        if (kind == DownloadLoaderKind.Fabric)
        {
            var result = GameDownload.DlFabricListLoader.output;
            return result is { } data ? data.Value?["loader"] as JsonArray : null;
        }

        var legacyResult = GameDownload.DlLegacyFabricListLoader.output;
        return legacyResult is { } legacyData ? legacyData.Value?["loader"] as JsonArray : null;
    }

    private JsonArray? GetGameVersions(DownloadLoaderKind kind)
    {
        if (kind == DownloadLoaderKind.Fabric)
        {
            var result = GameDownload.DlFabricListLoader.output;
            return result is { } data ? data.Value?["game"] as JsonArray : null;
        }

        var legacyResult = GameDownload.DlLegacyFabricListLoader.output;
        return legacyResult is { } legacyData ? legacyData.Value?["game"] as JsonArray : null;
    }

    private string GetLoaderWarningText()
    {
        var fabricSelected = _loaderCards.Any(card =>
            card.Kind == DownloadLoaderKind.Fabric && card.SelectedVersion is not null);
        if (fabricSelected)
            return Lang.Text("Download.Install.Warning.FabricApi");
        var legacyFabricSelected = _loaderCards.Any(card =>
            card.Kind == DownloadLoaderKind.LegacyFabric && card.SelectedVersion is not null);
        if (legacyFabricSelected)
            return Lang.Text("Download.Install.Warning.LegacyFabricApi");
        return "";
    }

    #endregion

    #region 安装

    private void StartInstall()
    {
        if (_isInstalling)
            return;

        if (string.IsNullOrEmpty(_versionName))
        {
            ShowSelectStage();
            return;
        }

        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(folder))
        {
            HintService.Show(Hints.Error(Lang.Text("Download.Install.NoFolder")));
            HintNoFolder.IsVisible = true;
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

        var instanceFolder =
            Path.Combine(folder, "versions", name) + Path.DirectorySeparatorChar;
        var instanceJsonPath = Path.Combine(instanceFolder, $"{name}.json");
        if (File.Exists(instanceJsonPath))
        {
            HintService.Show(
                Hints.Error(
                    Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", name, "")));
            return;
        }

        if (!McInstallRecovery.TryClearIncompleteInstanceFolder(instanceFolder, name))
        {
            HintService.Show(
                Hints.Error(
                    Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", name, "")));
            return;
        }

        var request = new McInstallRequest
        {
            targetInstanceName = name,
            targetInstanceFolder = instanceFolder,
            minecraftJson = _versionUrl,
            minecraftName = _versionName,
            fabricVersion = _loaderCards
                .FirstOrDefault(card => card.Kind == DownloadLoaderKind.Fabric)
                ?.SelectedVersion,
            legacyFabricVersion = _loaderCards
                .FirstOrDefault(card => card.Kind == DownloadLoaderKind.LegacyFabric)
                ?.SelectedVersion
        };

        List<Loading.LoaderBase> loaders;
        try
        {
            loaders = McInstallPipeline.CreateInstallLoader(request);
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
        _isInstalling = true;
        BtnStart.IsEnabled = false;
        BtnStart.Show = false;
        BtnReselect.IsEnabled = false;
        CardProgress.IsVisible = true;
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
            loaders);
        _installCombo.OnStateChangedUi += OnInstallStateChanged;
        _installCombo.Start(instanceFolder);
        Loading.LoaderTaskbarAdd(_installCombo);
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
        _isInstalling = false;
        BtnReselect.IsEnabled = true;
        BtnStart.IsEnabled = true;
        foreach (var loaderCard in _loaderCards)
        {
            if (loaderCard.Kind is null)
                continue;
            loaderCard.Card.IsEnabled = true;
        }

        // 失败或取消时回滚本次安装创建的路径；成功时清理备份
        McInstallRecovery.HandleTerminalState(loader, _instanceFolder);
        if (_installCombo is not null)
        {
            _installCombo.OnStateChangedUi -= OnInstallStateChanged;
            _installCombo = null;
        }

        if (newState == LoadState.Finished)
        {
            CardProgress.IsVisible = false;
            LoadInstall.State = new MyLoadingStateSimulator();
            ApplyFinishedBookkeeping(_instanceName);
            HintService.Show(
                Hints.Success($"{_instanceName} {Lang.Text("Download.Install.Finished")}"));
            _versionListBuilt = false;
            OnVersionListFinished();
            RefreshAllLoaderCards();
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
        HintService.Show(Hints.Error(LoadInstall.TextError));
        RefreshAllLoaderCards();
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
            Loading.LoaderFolderRun(
                InstanceList.mcInstanceListLoader,
                Path.Combine(mcFolder, "versions") + Path.DirectorySeparatorChar,
                Loading.LoaderFolderRunType.ForceRun);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "安装完成后刷新实例列表失败");
        }
    }

    private void RefreshProgressText()
    {
        if (!_isInstalling || _installCombo is null)
            return;
        var running = _installCombo
            .GetLoaderList()
            .FirstOrDefault(loader => loader.State == LoadState.Loading);
        LoadInstall.Text = running?.DisplayName ?? Lang.Text("Download.Install.State.Loading");
    }

    #endregion
}