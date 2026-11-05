using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
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
    private bool _instanceNameEdited;
    private bool _isInstalling;
    private bool _loaderCardsBuilt;
    private bool _updatingInstanceName;
    private JsonObject? _version;
    private string _versionIcon = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
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
        TextInstanceName.TextChanged += (_, _) =>
        {
            if (!_updatingInstanceName)
                _instanceNameEdited = true;
            UpdateLocation();
        };
        TextInstanceName.KeyDown += OnInstanceNameKeyDown;
        BtnReselect.Click += (_, _) =>
        {
            if (!_isInstalling)
                ShowSelectStage();
        };
        BtnStart.Click += (_, _) =>
        {
            if (!_isInstalling)
                _ = StartInstallAsync();
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
        GameDownload.DlFabricListLoader.OnStateChangedUi +=
            (_, state, _) => Dispatcher.UIThread.Post(() =>
            {
                if (state is LoadState.Loading or LoadState.Failed or LoadState.Finished)
                    RefreshLoaderCard(DownloadLoaderKind.Fabric);
            });
        GameDownload.DlLegacyFabricListLoader.OnStateChangedUi +=
            (_, state, _) => Dispatcher.UIThread.Post(() =>
            {
                if (state is LoadState.Loading or LoadState.Failed or LoadState.Finished)
                    RefreshLoaderCard(DownloadLoaderKind.LegacyFabric);
            });

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
        GameDownload.DlOptiFineListLoader.Start(null, true);
        GameDownload.DlForgeListLoader.Start(null, true);
        GameDownload.DlNeoForgeListLoader.Start(null, true);
        GameDownload.DlCleanroomListLoader.Start(null, true);
        GameDownload.DlLiteLoaderListLoader.Start(null, true);
        GameDownload.DlFabricListLoader.Start(null, true);
        GameDownload.DlLegacyFabricListLoader.Start(null, true);
        GameDownload.DlLabyModListLoader.Start(null, true);
        CompDownloadLoaders.DlFabricApiLoader.Start(0, true);
        CompDownloadLoaders.DlLegacyFabricApiLoader.Start(0, true);
        CompDownloadLoaders.DlOptiFabricLoader.Start(0, true);
    }

    protected override void RefreshLocalization()
    {
        foreach (var loaderCard in _loaderCards)
        {
            loaderCard.Card.Title = Lang.Text(loaderCard.TitleKey);
            if (loaderCard.Kind is { } kind)
                RefreshLoaderCard(kind, loaderCard);
        }

        if (_versionListBuilt
            && GameDownload.dlClientListLoader.output.Value?["versions"] is JsonArray)
            OnVersionListFinished();

        if (string.IsNullOrEmpty(_versionName))
            return;
        UpdateLocation();
        HintLoaderWarning.Text = GetLoaderWarningText();
        HintLoaderWarning.IsVisible = !string.IsNullOrEmpty(HintLoaderWarning.Text);
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

    /// <summary>退出安装配置阶段，回到版本选择。</summary>
    public void ExitSelectPage()
    {
        if (!_isInstalling)
            ShowSelectStage();
    }

    private void ShowSelectStage()
    {
        var animate = ContainerAnimation.CanAnimate(this)
            && PanSelectStage?.IsLoaded == true
            && PanConfigStage?.IsLoaded == true;
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
        foreach (var loaderCard in _loaderCards)
            loaderCard.SelectedVersion = null;
        RefreshAllLoaderCards();
        RefreshLogo();
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
        BtnStart.IsEnabled = !_isInstalling && !string.IsNullOrEmpty(GameFolders.mcFolderSelected);
        UpdateLocation();
        PanBack?.ScrollToHome();
    }

    private void SelectVersion(JsonObject version)
    {
        _version = version;
        _versionName = version["id"]?.ToString() ?? "";
        _versionUrl = version["url"]?.ToString();
        _versionIcon = GetVersionIcon(version);
        if (string.IsNullOrEmpty(_versionName))
        {
            ShowSelectStage();
            return;
        }

        _instanceNameEdited = false;
        SetDefaultInstanceName();
        RefreshLogo();
        StartLoaderListLoaders();
        RefreshAllLoaderCards();
        ShowConfigStage();
    }

    private static void StartLoaderListLoaders()
    {
        GameDownload.DlFabricListLoader.Start(null, true);
        GameDownload.DlLegacyFabricListLoader.Start(null, true);
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
                    : Path.Combine(GameFolders.mcFolderSelected, "versions"))
        ];
        TextInstanceName.Validate();

        UpdateLocation();
    }

    private string BuildDefaultInstanceName()
    {
        var name = string.IsNullOrEmpty(_versionName) ? "Minecraft" : _versionName;
        var fabric = _loaderCards.FirstOrDefault(card =>
                card.Kind == DownloadLoaderKind.Fabric)
            ?.SelectedVersion;
        if (!string.IsNullOrEmpty(fabric))
            name += $"-Fabric_{fabric.Replace("+build", "")}";
        var legacyFabric = _loaderCards.FirstOrDefault(card =>
                card.Kind == DownloadLoaderKind.LegacyFabric)
            ?.SelectedVersion;
        if (!string.IsNullOrEmpty(legacyFabric))
            name += $"-LegacyFabric_{legacyFabric}";
        return name;
    }

    private void RefreshInstanceNameIfNeeded()
    {
        if (_instanceNameEdited || string.IsNullOrEmpty(_versionName))
            return;
        SetDefaultInstanceName();
    }

    private void RefreshLogo()
    {
        if (ImgLogo is null)
            return;
        var fabricSelected = _loaderCards.Any(card =>
            card is { Kind: DownloadLoaderKind.Fabric, SelectedVersion: not null });
        var legacyFabricSelected = _loaderCards.Any(card =>
            card is { Kind: DownloadLoaderKind.LegacyFabric, SelectedVersion: not null });
        ImgLogo.Source = fabricSelected || legacyFabricSelected
            ? $"{LauncherPaths.ImageRoot}Blocks/Fabric.png"
            : _versionIcon;
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
                HintNoFolder.IsVisible = true;
                BtnStart.IsEnabled = false;
                return;
            }

            HintNoFolder.IsVisible = false;
            BtnStart.IsEnabled = !_isInstalling;
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
        public required MyIconButton ClearButton;
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
                Margin = new Thickness(0d, 12d, 0d, 0d),
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
                header.Children.Add(
                    new MyImage
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
                ClearButton = clearButton,
                Info = info
            };

            var cardKind = kind;
            clearButton.Click += (_, _) =>
            {
                loaderCard.SelectedVersion = null;
                RefreshLoaderCard(cardKind, loaderCard);
                card.IsSwapped = true;
                RefreshInstanceNameIfNeeded();
                RefreshLogo();
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

    private static string? GetLoaderLogo(string titleKey)
    {
        return titleKey switch
        {
            "Common.Installation.Forge" => $"{LauncherPaths.ImageRoot}Blocks/Anvil.png",
            "Common.Installation.Cleanroom" => $"{LauncherPaths.ImageRoot}Blocks/Cleanroom.png",
            "Common.Installation.NeoForge" => $"{LauncherPaths.ImageRoot}Blocks/NeoForge.png",
            "Common.Installation.Fabric" or "Common.Installation.FabricApi" =>
                $"{LauncherPaths.ImageRoot}Blocks/Fabric.png",
            "Common.Installation.LegacyFabric" or "Common.Installation.LegacyFabricApi" =>
                $"{LauncherPaths.ImageRoot}Blocks/Fabric.png",
            "Common.Installation.LabyMod" => $"{LauncherPaths.ImageRoot}Blocks/LabyMod.png",
            "Common.Installation.OptiFine" or "Common.Installation.OptiFabric" =>
                $"{LauncherPaths.ImageRoot}Blocks/GrassPath.png",
            "Common.Installation.LiteLoader" => $"{LauncherPaths.ImageRoot}Blocks/Egg.png",
            _ => null
        };
    }

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
        loaderCard.Info.Text = loaderCard.SelectedVersion is null
            ? Lang.Text("Download.Install.State.CanAdd")
            : $"{Lang.Text(loaderCard.TitleKey)} {loaderCard.SelectedVersion}";
        loaderCard.ClearButton.IsVisible = loaderCard.SelectedVersion is not null;

        var body = loaderCard.Body;
        body.Children.Clear();

        var listLoader = kind == DownloadLoaderKind.Fabric
            ? (Loading.LoaderBase)GameDownload.DlFabricListLoader
            : GameDownload.DlLegacyFabricListLoader;
        switch (listLoader.State)
        {
            case LoadState.Loading:
            case LoadState.Waiting:
                body.Children.Add(
                    new MyLoading
                    {
                        Margin = new Thickness(5d),
                        Text = Lang.Text("Download.Install.State.Getting")
                    });
                return;
            case LoadState.Failed:
                body.Children.Add(
                    new TextBlock
                    {
                        Text = Lang.Text(
                            "Download.Install.State.GetVersionListFailed",
                            listLoader.Error?.Message
                            ?? Lang.Text("Download.Install.Error.OperationFailed")),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(6d, 4d, 6d, 8d)
                    });
                var retryButton = new MyButton
                {
                    Text = Lang.Text("Common.Action.Refresh"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MinWidth = 90d,
                    Padding = new Thickness(13d, 0d, 13d, 0d)
                };
                retryButton.Click += (_, _) => listLoader.Start(null, true);
                body.Children.Add(retryButton);
                return;
            case LoadState.Finished:
            case LoadState.Aborted:
                break;
            default:
                throw new ArgumentOutOfRangeException();
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
                Logo = $"{LauncherPaths.ImageRoot}Blocks/Fabric.png",
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
        RefreshInstanceNameIfNeeded();
        RefreshLogo();
    }

    private static JsonArray? GetLoaderVersions(DownloadLoaderKind kind)
    {
        if (kind == DownloadLoaderKind.Fabric)
        {
            var result = GameDownload.DlFabricListLoader.output;
            return true ? result.Value?["loader"] as JsonArray : null;
        }

        var legacyResult = GameDownload.DlLegacyFabricListLoader.output;
        return true ? legacyResult.Value?["loader"] as JsonArray : null;
    }

    private static JsonArray? GetGameVersions(DownloadLoaderKind kind)
    {
        if (kind == DownloadLoaderKind.Fabric)
        {
            var result = GameDownload.DlFabricListLoader.output;
            return true ? result.Value?["game"] as JsonArray : null;
        }

        var legacyResult = GameDownload.DlLegacyFabricListLoader.output;
        return true ? legacyResult.Value?["game"] as JsonArray : null;
    }

    private string GetLoaderWarningText()
    {
        var fabricSelected = _loaderCards.Any(card =>
            card is { Kind: DownloadLoaderKind.Fabric, SelectedVersion: not null });
        if (fabricSelected)
            return Lang.Text("Download.Install.Warning.FabricApi");
        var legacyFabricSelected = _loaderCards.Any(card =>
            card is { Kind: DownloadLoaderKind.LegacyFabric, SelectedVersion: not null });
        return legacyFabricSelected ? Lang.Text("Download.Install.Warning.LegacyFabricApi") : "";
    }

    #endregion

    #region 安装

    private void OnInstanceNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _isInstalling || !BtnStart.IsEnabled)
            return;
        _ = StartInstallAsync();
        e.Handled = true;
    }

    private async Task StartInstallAsync()
    {
        if (_isInstalling)
            return;

        if (!RestrictedFeatures.Check("minecraft-install"))
            return;

        if (string.IsNullOrEmpty(_versionName))
        {
            ShowSelectStage();
            return;
        }

        var selectedLoader = _loaderCards.Any(card => card.SelectedVersion is not null);
        if (selectedLoader
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
        if (File.Exists(instanceJsonPath)
            || !McInstallRecovery.TryClearIncompleteInstanceFolder(instanceFolder, name))
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
        UpdateLocation();
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