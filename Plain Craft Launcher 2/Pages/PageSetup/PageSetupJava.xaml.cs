using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Dialogs;

namespace PCL;

public partial class PageSetupJava : IPageView
{
    private readonly PageSetupJavaViewModel _vm = new();
    private bool _refreshed;
    private bool _scanning;

    public Loading.LoaderTask<bool, List<JavaEntry>> loader;

    public PageSetupJava()
    {
        InitializeComponent();
        loader = new Loading.LoaderTask<bool, List<JavaEntry>>("JavaPageLoader", Load_GetJavaList);
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += PageSetupLaunch_Unloaded;
    }

    public PageSetupJavaViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e) => Refresh();

    private void PageSetupLaunch_Unloaded(object sender, RoutedEventArgs e) => _refreshed = false;

    private void Refresh()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        PageLoaderInit(PanLoad, CardLoad, PanMain, null, loader, _ => OnLoadFinished(), Load_Input);
    }

    private object Load_Input()
    {
        return false;
    }

    private void Load_GetJavaList(Loading.LoaderTask<bool, List<JavaEntry>> task)
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
            await _vm.ScanJavaAsync();
            Common.RunInUi(OnLoadFinished);
        }
        finally
        {
            _scanning = false;
        }
    }

    private void OnLoadFinished()
    {
        PanContent.Children.Clear();
        var itemAuto = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox
        };
        itemAuto.TrySetLocalizedValue("Title", "Setup.Java.AutoSelect.Title");
        itemAuto.TrySetLocalizedValue("Info", "Setup.Java.AutoSelect.Info");
        itemAuto.Check += (sender, e) => Config.Launch.SelectedJava = "";
        PanContent.Children.Add(itemAuto);
        var currentSetJava = Config.Launch.SelectedJava;
        foreach (var entry in JavaRuntime.Javas.GetSortedJavaList())
        {
            var item = ItemBuild(entry);
            PanContent.Children.Add(item);
            if (entry.Installation.JavaExePath == currentSetJava)
                item.SetChecked(true, false, false);
        }

        if (string.IsNullOrEmpty(currentSetJava))
            itemAuto.SetChecked(true, false, false);
    }

    protected override void RefreshLocalization()
    {
        if (loader.State == Common.LoadState.Finished)
        {
            var savedOffset = PanBack.VerticalOffset;
            OnLoadFinished();
            Dispatcher.BeginInvoke(new Action(() => PanBack.ScrollToVerticalOffset(savedOffset)));
        }
    }

    private MyListItem ItemBuild(JavaEntry j)
    {
        var item = new MyListItem();
        item.Title = PageSetupJavaViewModel.BuildTitle(j);
        item.Info = PageSetupJavaViewModel.BuildInfo(j);
        item.Tags = PageSetupJavaViewModel.BuildTags(j);

        item.Type = MyListItem.CheckType.RadioBox;
        item.Check += (sender, e) =>
        {
            if (!_vm.TrySelectJava(j))
                e.handled = true;
        };
        var btnOpenFolder = new MyIconButton();
        btnOpenFolder.SvgIcon = "lucide/folder-open";
        btnOpenFolder.TrySetLocalizedValue("ToolTip", "Common.Action.Open");
        btnOpenFolder.Click += (sender, e) =>
        {
            if (!PageSetupJavaViewModel.EnsureAvailable(j))
                return;

            Common.OpenExplorer(j.Installation.JavaFolder);
        };
        var btnInfo = new MyIconButton();
        btnInfo.SvgIcon = "lucide/info";
        btnInfo.TrySetLocalizedValue("ToolTip", "Setup.Java.Detail.ToolTip");
        btnInfo.Click += async (sender, e) =>
        {
            var control = sender as Control;
            if (control is not null)
                control.IsEnabled = false;
            try
            {
                if (!PageSetupJavaViewModel.EnsureAvailable(j))
                    return;

                await DialogService.Instance.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Setup.Java.Info.Title"),
                    Content = PageSetupJavaViewModel.BuildInfoMessage(j),
                    Buttons = DialogButtons.Ok,
                    ForceWait = false
                });
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    Lang.Text("Setup.Error.OperationFailed"),
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Setup.Error.OperationFailed"));
            }
            finally
            {
                if (control is not null)
                    control.IsEnabled = true;
            }
        };
        var btnEnableSwitch = new MyIconButton();

        item.Buttons = [btnOpenFolder, btnInfo, btnEnableSwitch];

        void UpdateEnableStyle(bool isCurEnable)
        {
            if (!j.Installation.IsStillAvailable)
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.Unavailable")));
                return;
            }

            if (isCurEnable)
            {
                item.LabTitle.TextDecorations = null;
                item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
                btnEnableSwitch.SvgIcon = "lucide/circle-minus";
                btnEnableSwitch.TrySetLocalizedValue("ToolTip", "Setup.Java.Disable");
            }
            else
            {
                item.LabTitle.TextDecorations = TextDecorations.Strikethrough;
                item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4");
                btnEnableSwitch.SvgIcon = "lucide/circle-check";
                btnEnableSwitch.TrySetLocalizedValue("ToolTip", "Setup.Java.Enable");
            }
        }

        btnEnableSwitch.Click += (_, _) =>
        {
            var enabled = _vm.ToggleJavaEnabled(j);
            if (enabled is not null)
                UpdateEnableStyle(enabled.Value);
        };
        UpdateEnableStyle(j.IsEnabled);

        return item;
    }

    private async void BtnAdd_Click(object sender, Common.RouteEventArgs e)
    {
        var control = sender as Control;
        if (control is not null)
            control.IsEnabled = false;
        try
        {
            var ret = SystemDialogs.SelectFile(Lang.Text("Setup.Java.SelectFile.Filter"),
                Lang.Text("Setup.Java.SelectFile.Title"));
            if (string.IsNullOrEmpty(ret) || !File.Exists(ret))
                return;
            if (JavaRuntime.Javas.Exist(ret))
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.AlreadyExists")));
                return;
            }

            if (await _vm.AddJavaAsync(ret))
            {
                HintService.Show(Hints.Success(Lang.Text("Setup.Java.Added")));
                loader.Start(true, true);
            }
            else
            {
                HintService.Show(Hints.Error(Lang.Text("Setup.Java.AddFailed")));
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Java.AddFailed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Java.AddFailed"));
        }
        finally
        {
            if (control is not null)
                control.IsEnabled = true;
        }
    }
}
