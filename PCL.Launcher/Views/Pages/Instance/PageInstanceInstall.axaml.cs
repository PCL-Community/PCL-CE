using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例安装/修改页：展示当前实例的游戏版本与已装加载器，并提供更新、补全文件、重置与跳转下载页的入口。
///     合并安装管线由下载页与安装成员维护；本页在管线不可用时明确报错，不留“可点击但无反应”的死路。
/// </summary>
public class PageInstanceInstall : MyPageRight, IRefreshable
{
    private readonly StackPanel _panMain;
    private McInstance? _instance;
    private MyButton _btnModify = null!;
    private MyButton _btnRepair = null!;
    private MyButton _btnReset = null!;

    public PageInstanceInstall()
    {
        var panel = new StackPanel { Margin = new Thickness(25d, 25d, 25d, 25d) };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        Child = scroll;
        BuildUi();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        Reload();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
    }

    public void Refresh()
    {
        Reload();
    }

    private void BuildUi()
    {
        _panMain.Children.Clear();

        var infoCard = new MyCard
        {
            Title = Lang.Text("Download.Install.Title"),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var infoBody = new StackPanel { Margin = new Thickness(25d, 40d, 25d, 15d), Spacing = 6d };
        infoCard.BorderChild = infoBody;
        _panMain.Children.Add(infoCard);

        var actionCard = new MyCard
        {
            Title = Lang.Text("Instance.Install.Action.ModifyLabel"),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var actionBody = new StackPanel { Margin = new Thickness(25d, 40d, 25d, 15d) };
        var buttons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 0d, 0d, 5d)
        };
        _btnModify = NewActionButton("Instance.Install.Action.StartModify", () => _ = ModifyAsync());
        buttons.Children.Add(_btnModify);
        _btnRepair = NewActionButton("Instance.Overall.Manage.RepairFiles", RepairFiles);
        ToolTip.SetTip(_btnRepair, Lang.Text("Instance.Overall.Manage.RepairFiles.ToolTip"));
        buttons.Children.Add(_btnRepair);
        _btnReset = NewActionButton(
            "Instance.Install.Action.StartReset",
            () => _ = ResetAsync(),
            MyButton.ColorState.Red);
        ToolTip.SetTip(_btnReset, Lang.Text("Instance.Overall.Manage.Reinstall.ToolTip"));
        buttons.Children.Add(_btnReset);
        buttons.Children.Add(NewActionButton("Common.Action.Download", GoToDownloadPage));
        buttons.Children.Add(NewActionButton("Common.Action.Refresh", Reload));
        actionBody.Children.Add(buttons);
        actionCard.BorderChild = actionBody;
        _panMain.Children.Add(actionCard);

        Reload();
    }

    private static MyButton NewActionButton(
        string textKey,
        Action onClick,
        MyButton.ColorState color = MyButton.ColorState.Normal)
    {
        var button = new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 140d,
            Height = 35d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 7d, 12d, 0d),
            ColorType = color
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    public void Reload()
    {
        ResetSummary(_instance);

        var enabled = _instance is not null;
        if (_btnModify is not null)
            _btnModify.IsEnabled = enabled;
        if (_btnRepair is not null)
            _btnRepair.IsEnabled = enabled;
        if (_btnReset is not null)
            _btnReset.IsEnabled = enabled;
    }

    private void ResetSummary(McInstance? instance)
    {
        var body = _panMain.Children.Count > 0 && _panMain.Children[0] is MyCard { BorderChild: StackPanel panel }
            ? panel
            : null;
        if (body is null)
            return;
        body.Children.Clear();

        if (instance is null)
        {
            body.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Instance.Saves.Empty.Message"),
                    TextWrapping = TextWrapping.Wrap
                });
            return;
        }

        var info = instance.Info;
        body.Children.Add(MakeRow(Lang.Text("Download.Install.InstanceName"), instance.Name));
        body.Children.Add(MakeRow(Lang.Text("Download.Install.InstanceLocation"), instance.PathInstance));
        body.Children.Add(
            MakeRow(
                Lang.Text("Download.Install.Section.Version"),
                McInstanceInfo.GetVanillaNameDisplay(info.VanillaName)));
        body.Children.Add(MakeRow(Lang.Text("Download.Install.Section.Loader"), FormatLoaders(instance)));
    }

    private static string FormatLoaders(McInstance instance)
    {
        var info = instance.Info;
        var loaders = new List<string>();
        if (info.HasForge)
            loaders.Add("Forge " + (info.Forge ?? string.Empty));
        if (info.HasNeoForge)
            loaders.Add("NeoForge " + (info.NeoForge ?? string.Empty));
        if (info.HasCleanroom)
            loaders.Add("Cleanroom " + (info.Cleanroom ?? string.Empty));
        if (info.HasFabric)
            loaders.Add("Fabric " + (info.Fabric ?? string.Empty));
        if (info.HasQuilt)
            loaders.Add("Quilt " + (info.Quilt ?? string.Empty));
        if (info.HasLegacyFabric)
            loaders.Add("Legacy Fabric " + (info.LegacyFabric ?? string.Empty));
        if (info.HasLiteLoader)
            loaders.Add("LiteLoader");
        if (info.HasOptiFine)
            loaders.Add("OptiFine " + (info.OptiFine ?? string.Empty));
        if (info.HasLabyMod)
            loaders.Add("LabyMod " + (info.LabyMod ?? string.Empty));
        return loaders.Count == 0
            ? Lang.Text("Instance.Install.NoExtraInstall")
            : string.Join(", ", loaders);
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

    private static Action<Loading.LoaderBase> OnInstallLoaderStateChanged(McInstance instance)
    {
        return loader =>
        {
            switch (loader.State)
            {
                case LoadState.Finished:
                    InstanceList.mcInstanceListForceRefresh = true;
                    var folder = GameFolders.mcFolderSelected;
                    if (!string.IsNullOrEmpty(folder))
                        Loading.LoaderFolderRun(
                            InstanceList.mcInstanceListLoader,
                            folder,
                            Loading.LoaderFolderRunType.ForceRun,
                            1,
                            "versions" + Path.DirectorySeparatorChar);
                    HintService.Show(Hints.Success(Lang.Text("Common.Status.Success") + " " + instance.Name));
                    break;
                case LoadState.Failed:
                    HintService.Show(
                        Hints.Error(
                            Lang.Text("Instance.Install.Error.OperationFailed")
                            + " " + (loader.Error?.Message ?? string.Empty)));
                    break;
                case LoadState.Aborted:
                    HintService.Show(Hints.Info(Lang.Text("Common.Status.Cancelled")));
                    break;
            }
        };
    }

    private void GoToDownloadPage()
    {
        PageRouter.Navigate(PageType.Download, PageSubType.DownloadInstall);
    }

    private static McInstallRequest BuildInstallRequest(McInstance instance)
    {
        var info = instance.Info;
        return new McInstallRequest
        {
            targetInstanceName = instance.Name,
            targetInstanceFolder = instance.PathInstance,
            minecraftName = info.VanillaName,
            optiFineEntry = info.HasOptiFine
                ? new GameDownload.DlOptiFineListEntry
                {
                    Inherit = info.VanillaName,
                    DisplayName = info.VanillaName + " " + info.OptiFine
                }
                : null,
            forgeEntry = info.HasForge
                ? new GameDownload.DlForgeVersionEntry(info.Forge!, null, info.VanillaName)
                {
                    Category = "installer"
                }
                : null,
            forgeVersion = info.HasForge ? info.Forge : null,
            neoForgeVersion = info.HasNeoForge ? info.NeoForge : null,
            cleanroomVersion = info.HasCleanroom ? info.Cleanroom : null,
            fabricVersion = info.HasFabric ? info.Fabric : null,
            liteLoaderEntry = info.HasLiteLoader
                ? new GameDownload.DlLiteLoaderListEntry { Inherit = info.VanillaName }
                : null,
            legacyFabricVersion = info.HasLegacyFabric ? info.LegacyFabric : null
        };
    }

    private async Task ModifyAsync()
    {
        if (_instance is null)
            return;
        _btnModify.IsEnabled = false;
        try
        {
            var instance = _instance;
            var request = BuildInstallRequest(instance);
            var unsupportedLoader = McInstallPipeline.GetUnsupportedLoaderName(request);
            if (unsupportedLoader is not null)
            {
                HintService.Show(
                    Hints.Warning(Lang.Text("Download.Install.Loader.Unsupported", unsupportedLoader)));
                return;
            }

            var loaders = McInstallPipeline.CreateInstallLoader(request, ignoreDump: true);
            if (loaders.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Install.Error.OperationFailed")));
                return;
            }

            var combo = new Loading.LoaderCombo<object>(
                Lang.Text("Instance.Install.Action.ModifyLabel") + " " + instance.Name,
                loaders)
            {
                OnStateChanged = OnInstallLoaderStateChanged(instance)
            };
            combo.Start();
            Loading.LoaderTaskbarAdd(combo);
            LoadingUi.Current?.RefreshDownloadButton();
            PageRouter.Navigate(PageType.Launch);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Install.Error.OperationFailed", ex);
        }
        finally
        {
            _btnModify.IsEnabled = true;
        }
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
                HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Repair.DisableVerificationHint")));
                return;
            }

            var taskName = instance.Name + " " + Lang.Text("Instance.Overall.Repair.TaskName");
            if (Loading.loaderTaskbar.Any(loader => loader.name == taskName))
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Overall.Repair.Processing")));
                return;
            }

            var loader = new Loading.LoaderCombo<string>(
                taskName,
                GameDownload.DlClientFix(instance, true, GameDownload.AssetsIndexExistsBehaviour.AlwaysDownload))
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                            HintService.Show(
                                Hints.Success(
                                    Lang.Text("Instance.Overall.Repair.Success.WithTaskName", taskName)));
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
                                    Lang.Text("Instance.Overall.Repair.Cancelled.WithTaskName", taskName)));
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

    private async Task ResetAsync()
    {
        if (_instance is null)
            return;
        _btnReset.IsEnabled = false;
        try
        {
            var instance = _instance;
            var dialog = await PCL.Core.UI.Dialogs.DialogService.Instance.ShowAsync(
                new PCL.Core.UI.Dialogs.TextDialogRequest
                {
                    Title = Lang.Text("Instance.Install.Reset.Title"),
                    Content = Lang.Text("Instance.Install.Reset.Message"),
                    Buttons = PCL.Core.UI.Dialogs.DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!dialog.IsConfirmed)
                return;

            // 备份实例核心文件，便于安装失败后还原
            try
            {
                var backupFolder = Path.Combine(instance.PathInstance, "PCLInstallBackups");
                InstancePageHelper.EnsureDirectory(backupFolder);
                var json = Path.Combine(instance.PathInstance, instance.Name + ".json");
                var jar = Path.Combine(instance.PathInstance, instance.Name + ".jar");
                if (File.Exists(json))
                    File.Copy(json, Path.Combine(backupFolder, instance.Name + ".json"), true);
                if (File.Exists(jar))
                    File.Copy(jar, Path.Combine(backupFolder, instance.Name + ".jar"), true);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "备份实例核心文件失败", LauncherLogLevel.Developer);
            }

            var request = BuildInstallRequest(instance);
            var unsupportedLoader = McInstallPipeline.GetUnsupportedLoaderName(request);
            if (unsupportedLoader is not null)
            {
                HintService.Show(
                    Hints.Warning(Lang.Text("Download.Install.Loader.Unsupported", unsupportedLoader)));
                return;
            }

            var loaders = McInstallPipeline.CreateInstallLoader(request, ignoreDump: true);
            if (loaders.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Install.Error.OperationFailed")));
                return;
            }

            var combo = new Loading.LoaderCombo<object>(
                Lang.Text("Instance.Install.Action.StartReset") + " " + instance.Name,
                loaders)
            {
                OnStateChanged = OnInstallLoaderStateChanged(instance)
            };
            combo.Start();
            Loading.LoaderTaskbarAdd(combo);
            LoadingUi.Current?.RefreshDownloadButton();
            PageRouter.Navigate(PageType.Launch);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Install.Error.OperationFailed", ex);
        }
        finally
        {
            _btnReset.IsEnabled = true;
        }
    }
}
