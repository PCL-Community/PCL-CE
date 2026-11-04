using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     Java 设置页：列出本地 Java、启用/禁用、选择全局 Java 与手动添加。
///     列表来自 <c>JavaService.JavaManager</c>，扫描为真实文件系统扫描。
/// </summary>
public partial class PageSetupJava : MyPageRight, IRefreshable
{
    private readonly Loading.LoaderTask<bool, List<JavaEntry>> _loader;
    private bool _loaded;
    private bool _scanning;

    public PageSetupJava()
    {
        InitializeComponent();
        PanScroll = PanBack;

        _loader = new Loading.LoaderTask<bool, List<JavaEntry>>("JavaPageLoader", LoadJavaList);
        BtnAdd.Click += async (_, _) => await AddJavaAsync();

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            PageLoaderInit(PanLoad, CardLoad, PanMain, null, _loader, _ => OnLoadFinished());
        };
    }

    public void Refresh()
    {
        PageLoaderRestart();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        if (!_loaded) return;
        PageLoaderRestart(null, false);
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (_loaded && _loader.State == LoadState.Finished)
            OnLoadFinished();
    }

    private static object LoadInput()
    {
        return false;
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
            await Task.Run(async () =>
            {
                await JavaService.JavaManager.ScanJavaAsync(true);
                JavaService.JavaManager.CheckAllAvailability();
            });
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
            Type = MyListItem.CheckType.RadioBox,
            Title = Lang.Text("Setup.Java.AutoSelect.Title"),
            Info = Lang.Text("Setup.Java.AutoSelect.Info")
        };
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
            Title = BuildTitle(entry),
            Info = entry.Installation.JavaFolder,
            Tags = BuildTags(entry),
            Type = MyListItem.CheckType.RadioBox
        };
        item.Check += (_, args) =>
        {
            if (!entry.Installation.IsStillAvailable)
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.Unavailable")));
                return;
            }

            if (!entry.IsEnabled)
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.EnableBeforeSelect")));
                args.handled = true;
                return;
            }

            Config.Launch.SelectedJava = entry.Installation.JavaExePath;
        };

        var btnOpenFolder = new MyIconButton
        {
            SvgIcon = "lucide/folder-open"
        };
        ToolTip.SetTip(btnOpenFolder, Lang.Text("Common.Action.Open"));
        btnOpenFolder.Click += (_, _) =>
        {
            if (!EnsureAvailable(entry)) return;
            Basics.OpenPath(entry.Installation.JavaFolder);
        };

        var btnInfo = new MyIconButton
        {
            SvgIcon = "lucide/info"
        };
        ToolTip.SetTip(btnInfo, Lang.Text("Setup.Java.Detail.ToolTip"));
        btnInfo.Click += async (_, _) =>
        {
            if (!EnsureAvailable(entry)) return;
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Setup.Java.Info.Title"),
                    Content = Lang.Text(
                        "Setup.Java.Info.Format",
                        entry.Installation.IsJre ? "JRE" : "JDK",
                        entry.Installation.Version.ToString(),
                        entry.Installation.Architecture.ToString(),
                        entry.Installation.Is64Bit ? "64 Bit" : "32 Bit",
                        entry.Installation.Brand.ToString(),
                        entry.Installation.JavaFolder),
                    Buttons = DialogButtons.Ok
                });
        };

        var btnEnableSwitch = new MyIconButton();
        item.Buttons = [btnOpenFolder, btnInfo, btnEnableSwitch];

        void UpdateEnableStyle(bool isEnabled)
        {
            if (!entry.Installation.IsStillAvailable)
            {
                btnEnableSwitch.SvgIcon = "lucide/circle-minus";
                ToolTip.SetTip(btnEnableSwitch, Lang.Text("Setup.Java.Unavailable"));
                return;
            }

            item.LabTitle.TextDecorations = isEnabled ? null : TextDecorations.Strikethrough;
            btnEnableSwitch.SvgIcon = isEnabled ? "lucide/circle-minus" : "lucide/circle-check";
            ToolTip.SetTip(
                btnEnableSwitch,
                Lang.Text(isEnabled ? "Setup.Java.Disable" : "Setup.Java.Enable"));
        }

        btnEnableSwitch.Click += (_, _) =>
        {
            try
            {
                var target = JavaRuntime.Javas.AddOrGet(entry.Installation.JavaExePath);
                if (target is null) return;
                if (target.IsEnabled
                    && Config.Launch.SelectedJava == target.Installation.JavaExePath)
                {
                    HintService.Show(Hints.Info(Lang.Text("Setup.Java.DeselectBeforeDisable")));
                    return;
                }

                target.IsEnabled = !target.IsEnabled;
                entry.IsEnabled = target.IsEnabled;
                JavaRuntime.Javas.SaveConfig();
                UpdateEnableStyle(target.IsEnabled);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    Lang.Text("Setup.Java.EnableFailed"),
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Setup.Java.EnableFailed"));
            }
        };

        UpdateEnableStyle(entry.IsEnabled);
        return item;
    }

    private async Task AddJavaAsync()
    {
        try
        {
            var path = SystemDialogs.SelectFile(
                Lang.Text("Setup.Java.SelectFile.Filter"),
                Lang.Text("Setup.Java.SelectFile.Title"));
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (JavaRuntime.Javas.Exist(path))
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.AlreadyExists")));
                return;
            }

            JavaRuntime.Javas.AddOrGet(path);
            JavaRuntime.Javas.SaveConfig();
            if (JavaRuntime.Javas.Exist(path))
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
    }

    private static string BuildTitle(JavaEntry entry)
    {
        return $"{(entry.Installation.IsJre ? "JRE" : "JDK")} {entry.Installation.MajorVersion}";
    }

    private static string[] BuildTags(JavaEntry entry)
    {
        return
        [
            entry.Installation.Is64Bit ? "64 Bit" : "32 Bit", entry.Installation.Brand.ToString()
        ];
    }

    private static bool EnsureAvailable(JavaEntry entry)
    {
        if (entry.Installation.IsStillAvailable) return true;
        HintService.Show(Hints.Info(Lang.Text("Setup.Java.Unavailable")));
        return false;
    }
}