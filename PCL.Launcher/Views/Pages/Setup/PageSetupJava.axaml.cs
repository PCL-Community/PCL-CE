using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Models;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
/// Java 设置页：列出本地 Java、启用/禁用、选择全局 Java 与手动添加。 列表操作由
/// <see cref="PageSetupJavaViewModel" /> 负责，页面负责列表控件构建与滚动位置保持。
/// </summary>
public partial class PageSetupJava : MyPageRight, IRefreshable
{
    private readonly Loading.LoaderTask<bool, List<JavaEntry>> _loader;
    private bool _loaded;
    private bool _scanning;

    public PageSetupJava(PageSetupJavaViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        PanScroll = PanBack;

        _loader = new Loading.LoaderTask<bool, List<JavaEntry>>("JavaPageLoader", LoadJavaList);
        BtnAdd.Click += async (_, _) => await AddJavaAsync();
        BtnDownload.Click += async (_, _) => await DownloadJavaAsync();

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            PageLoaderInit(
                PanLoad,
                CardLoad,
                PanMain,
                null,
                _loader,
                _ => OnLoadFinished());
        };
    }

    public PageSetupJavaViewModel ViewModel { get; }

    public void Refresh()
    {
        PageLoaderRestart();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        if (!_loaded) return;
        PageLoaderRestart(null, false);
    }

    public override void OnNavigatedFrom()
    {
    }

    private void LoadJavaList(Loading.LoaderTask<bool, List<JavaEntry>> task)
    {
        if ((task.input || task.isForceRestarting) && !_scanning)
        {
            _scanning = true;
            _ = ScanThenRefreshAsync();
        }

        task.output = JavaRuntime.Javas.GetSortedJavaList();
    }

    private async Task ScanThenRefreshAsync()
    {
        try
        {
            await ViewModel.ScanJavaAsync();
            LauncherCommon.RunInUi(OnLoadFinished);
        }
        finally
        {
            _scanning = false;
        }
    }

    private void OnLoadFinished()
    {
        PanContent.Children.Clear();

        var autoItem = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox
        };
        autoItem.SetLocalizedValue(
            MyListItem.TitleProperty,
            "Setup.Java.AutoSelect.Title");
        autoItem.SetLocalizedValue(
            MyListItem.InfoProperty,
            "Setup.Java.AutoSelect.Info");
        autoItem.Check += (_, _) =>
        {
            if (autoItem.Checked)
                Config.Launch.SelectedJava = "";
        };
        PanContent.Children.Add(autoItem);

        var currentSetJava = Config.Launch.SelectedJava;
        foreach (var entry in JavaRuntime.Javas.GetSortedJavaList())
        {
            var item = BuildItem(entry);
            PanContent.Children.Add(item);
            if (entry.Installation.JavaExePath == currentSetJava)
                item.SetChecked(true, false, false);
        }

        if (string.IsNullOrEmpty(currentSetJava))
            autoItem.SetChecked(true, false, false);
    }

    private MyListItem BuildItem(JavaEntry entry)
    {
        var item = new MyListItem
        {
            Title = PageSetupJavaViewModel.BuildTitle(entry),
            Info = PageSetupJavaViewModel.BuildInfo(entry),
            PlainTags = PageSetupJavaViewModel.BuildTags(entry),
            Type = MyListItem.CheckType.RadioBox
        };
        item.Check += (_, args) =>
        {
            if (!ViewModel.TrySelectJava(entry))
                args.handled = true;
        };

        var btnOpenFolder = new MyIconButton
        {
            SvgIcon = "lucide/folder-open"
        };
        btnOpenFolder.SetLocalizedValue(ToolTip.TipProperty, "Common.Action.Open");
        btnOpenFolder.Click += (_, _) =>
        {
            if (!PageSetupJavaViewModel.EnsureAvailable(entry)) return;
            Basics.OpenPath(entry.Installation.JavaFolder);
        };

        var btnInfo = new MyIconButton
        {
            SvgIcon = "lucide/info"
        };
        btnInfo.SetLocalizedValue(ToolTip.TipProperty, "Setup.Java.Detail.ToolTip");
        btnInfo.Click += async (_, _) =>
        {
            // 防止重复点击打开多个详情弹窗。
            if (!btnInfo.IsEnabled) return;
            if (!PageSetupJavaViewModel.EnsureAvailable(entry)) return;
            btnInfo.IsEnabled = false;
            try
            {
                await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Setup.Java.Info.Title"),
                        Content = PageSetupJavaViewModel.BuildInfoMessage(entry),
                        Buttons = DialogButtons.Ok
                    });
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    Lang.Text("Setup.Error.OperationFailed"),
                    LauncherLogLevel.Feedback,
                    userSummary: Lang.Text("Setup.Error.OperationFailed"));
            }
            finally
            {
                btnInfo.IsEnabled = true;
            }
        };

        var btnEnableSwitch = new MyIconButton();
        item.Buttons = [btnOpenFolder, btnInfo, btnEnableSwitch];

        btnEnableSwitch.Click += (_, _) =>
        {
            var enabled = ViewModel.ToggleJavaEnabled(entry);
            if (enabled is not null)
            {
                entry.IsEnabled = enabled.Value;
                UpdateEnableStyle(enabled.Value);
            }
        };

        UpdateEnableStyle(entry.IsEnabled);
        return item;

        void UpdateEnableStyle(bool isEnabled)
        {
            if (!entry.Installation.IsStillAvailable)
            {
                btnEnableSwitch.SvgIcon = "lucide/circle-minus";
                btnEnableSwitch.SetLocalizedValue(
                    ToolTip.TipProperty,
                    "Setup.Java.Unavailable");
                return;
            }

            item.LabTitle.TextDecorations = isEnabled ? null : TextDecorations.Strikethrough;
            btnEnableSwitch.SvgIcon = isEnabled ? "lucide/circle-minus" : "lucide/circle-check";
            btnEnableSwitch.SetLocalizedValue(
                ToolTip.TipProperty,
                isEnabled ? "Setup.Java.Disable" : "Setup.Java.Enable");
        }
    }

    private async Task AddJavaAsync()
    {
        BtnAdd.IsEnabled = false;
        try
        {
            var path = await SystemDialogs.SelectFileAsync(
                Lang.Text(
                    "Setup.Java.SelectFile.Filter",
                    JavaExecutableLayout.JavaExecutableName),
                Lang.Text("Setup.Java.SelectFile.Title"));
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (JavaRuntime.Javas.Exist(path))
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.AlreadyExists")));
                return;
            }

            if (await ViewModel.AddJavaAsync(path))
            {
                HintService.Show(Hints.Success(Lang.Text("Setup.Java.Added")));
                _loader.Start(true, true);
            }
            else
            {
                HintService.Show(Hints.Error(Lang.Text("Setup.Java.AddFailed")));
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Java.AddFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Java.AddFailed"));
        }
        finally
        {
            BtnAdd.IsEnabled = true;
        }
    }

    private async Task DownloadJavaAsync()
    {
        BtnDownload.IsEnabled = false;
        var loaderStarted = false;
        try
        {
            var distributions = await Task.Run(JavaRuntime.GetFoojayDistributions);

            var sources = distributions
                .Select(distribution => new DialogOption
                {
                    Text = distribution.Name,
                    Tag = distribution
                })
                .ToList();
            sources.Add(new DialogOption
            {
                Text = Lang.Text("Setup.Java.Download.Mojang"),
                Tag = "mojang"
            });

            var sourceResult = await DialogService.Instance.ShowAsync(new SelectDialogRequest
            {
                Title = Lang.Text("Setup.Java.Download.SelectSource"),
                Options = sources,
                Buttons = CreateContinueCancelButtons(),
                HighLight = false
            });
            if (!sourceResult.IsConfirmed
                || sourceResult.SelectedIndex is not { } sourceIndex
                || sourceIndex < 0
                || sourceIndex >= sources.Count)
                return;

            JavaDownloadSelection selection;
            if (sources[sourceIndex].Tag is FoojayDistributionModel distribution)
            {
                var packages = await Task.Run(
                    () => JavaRuntime.GetFoojayLtsPackages(distribution.ApiParameter));
                if (packages.Count == 0)
                {
                    HintService.Show(Hints.Info(Lang.Text("Setup.Java.Download.NoVersions")));
                    return;
                }

                var versions = packages
                    .Select(package => new DialogOption
                    {
                        Text = $"Java {package.MajorVersion}",
                        Description = package.JavaVersion,
                        Tag = package
                    })
                    .ToList();
                var versionResult = await DialogService.Instance.ShowAsync(new SelectDialogRequest
                {
                    Title = Lang.Text("Setup.Java.Download.SelectVersion"),
                    Options = versions,
                    Buttons = CreateContinueCancelButtons(),
                    HighLight = false
                });
                if (!versionResult.IsConfirmed
                    || versionResult.SelectedIndex is not { } versionIndex
                    || versionIndex < 0
                    || versionIndex >= versions.Count
                    || versions[versionIndex].Tag is not FoojayVersionModel package)
                    return;

                selection = new JavaDownloadSelection(
                    JavaDownloadSource.Foojay,
                    distribution.ApiParameter,
                    FoojayPackage: package,
                    DistributionName: distribution.Name);
            }
            else
            {
                var runtimeOptions = await Task.Run(JavaRuntime.GetMojangJavaVersions);
                if (runtimeOptions.Count == 0)
                {
                    HintService.Show(Hints.Info(Lang.Text("Setup.Java.Download.NoVersions")));
                    return;
                }

                var versions = runtimeOptions
                    .Select(version => new DialogOption
                    {
                        Text = $"Java {version.MajorVersion}",
                        Description = version.VersionName,
                        Tag = version
                    })
                    .ToList();
                var versionResult = await DialogService.Instance.ShowAsync(new SelectDialogRequest
                {
                    Title = Lang.Text("Setup.Java.Download.SelectVersion"),
                    Options = versions,
                    Buttons = CreateContinueCancelButtons(),
                    HighLight = false
                });
                if (!versionResult.IsConfirmed
                    || versionResult.SelectedIndex is not { } versionIndex
                    || versionIndex < 0
                    || versionIndex >= versions.Count
                    || versions[versionIndex].Tag is not MojangJavaVersionOption version)
                    return;

                selection = new JavaDownloadSelection(
                    JavaDownloadSource.Mojang,
                    MojangVersion: version,
                    DistributionName: Lang.Text("Setup.Java.Download.Mojang"));
            }

            var loader = JavaRuntime.GetJavaInstallLoader(selection);
            loader.OnStateChangedThread += (_, state, _) =>
            {
                if (state is not (LoadState.Finished or LoadState.Failed or LoadState.Aborted))
                    return;

                var error = loader.Error;
                LauncherCommon.RunInUi(() =>
                {
                    BtnDownload.IsEnabled = true;
                    switch (state)
                    {
                        case LoadState.Finished:
                            HintService.Show(Hints.Success(Lang.Text("Setup.Java.Download.Success")));
                            _loader.Start(true, true);
                            break;
                        case LoadState.Failed:
                            LauncherCommon.Log(
                                error ?? new Exception("Java download failed"),
                                Lang.Text("Setup.Java.Download.Failed"),
                                LauncherLogLevel.Hint,
                                userSummary: Lang.Text("Setup.Java.Download.Failed"));
                            HintService.Show(Hints.Error(Lang.Text("Setup.Java.Download.Failed")));
                            break;
                        case LoadState.Aborted:
                            HintService.Show(Hints.Info(Lang.Text("Setup.Java.Download.Cancelled")));
                            break;
                    }
                });
            };
            loader.hasOnStateChangedThread = true;
            Loading.LoaderTaskbarAdd(loader);
            loader.Start(selection, true);
            loaderStarted = true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Java.Download.Failed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Java.Download.Failed"));
            HintService.Show(Hints.Error(Lang.Text("Setup.Java.Download.Failed")));
        }
        finally
        {
            if (!loaderStarted)
                BtnDownload.IsEnabled = true;
        }
    }

    private static DialogButtons CreateContinueCancelButtons()
    {
        return DialogButtons.From(
            DialogButton.Create(Lang.Text("Common.Action.Continue")),
            DialogButton.Create(Lang.Text("Common.Action.Cancel")));
    }
}
