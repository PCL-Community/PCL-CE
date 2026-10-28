using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupJava : IPageView
{
    private readonly PageSetupJavaViewModel _vm = new();
    private bool _refreshed;
    private bool _scanning;

    public ModLoader.LoaderTask<bool, List<JavaEntry>> loader;

    public PageSetupJava()
    {
        InitializeComponent();
        loader = new ModLoader.LoaderTask<bool, List<JavaEntry>>("JavaPageLoader", Load_GetJavaList);
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

    private void Load_GetJavaList(ModLoader.LoaderTask<bool, List<JavaEntry>> task)
    {
        if ((task.input || task.isForceRestarting) && !_scanning)
        {
            _scanning = true;
            _ = ScanThenRefreshAsync();
        }

        task.output = ModJava.Javas.GetSortedJavaList();
    }

    private async Task ScanThenRefreshAsync()
    {
        try
        {
            await _vm.ScanJavaAsync();
            ModBase.RunInUi(OnLoadFinished);
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
            Type = MyListItem.CheckType.RadioBox,
            Title = Lang.Text("Setup.Java.AutoSelect.Title"),
            Info = Lang.Text("Setup.Java.AutoSelect.Info")
        };
        itemAuto.Check += (sender, e) => Config.Launch.SelectedJava = "";
        PanContent.Children.Add(itemAuto);
        var currentSetJava = Config.Launch.SelectedJava;
        foreach (var entry in ModJava.Javas.GetSortedJavaList())
        {
            var item = ItemBuild(entry);
            PanContent.Children.Add(item);
            if (entry.Installation.JavaExePath == currentSetJava)
                item.SetChecked(true, false, false);
        }

        if (string.IsNullOrEmpty(currentSetJava))
            itemAuto.SetChecked(true, false, false);
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
        btnOpenFolder.ToolTip = Lang.Text("Common.Action.Open");
        btnOpenFolder.Click += (sender, e) =>
        {
            if (!PageSetupJavaViewModel.EnsureAvailable(j))
                return;

            ModBase.OpenExplorer(j.Installation.JavaFolder);
        };
        var btnInfo = new MyIconButton();
        btnInfo.SvgIcon = "lucide/info";
        btnInfo.ToolTip = Lang.Text("Setup.Java.Detail.ToolTip");
        btnInfo.Click += async (sender, e) =>
        {
            var control = sender as Control;
            if (control is not null)
                control.IsEnabled = false;
            try
            {
                if (!PageSetupJavaViewModel.EnsureAvailable(j))
                    return;

                await ModMain.MyMsgBoxAsync(
                    PageSetupJavaViewModel.BuildInfoMessage(j),
                    Lang.Text("Setup.Java.Info.Title"));
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    Lang.Text("Setup.Error.OperationFailed"),
                    ModBase.LogLevel.Feedback,
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
                HintService.Hint(Lang.Text("Setup.Java.Unavailable"));
                return;
            }

            if (isCurEnable)
            {
                item.LabTitle.TextDecorations = null;
                item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
                btnEnableSwitch.SvgIcon = "lucide/circle-minus";
                btnEnableSwitch.ToolTip = Lang.Text("Setup.Java.Disable");
            }
            else
            {
                item.LabTitle.TextDecorations = TextDecorations.Strikethrough;
                item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4");
                btnEnableSwitch.SvgIcon = "lucide/circle-check";
                btnEnableSwitch.ToolTip = Lang.Text("Setup.Java.Enable");
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

    private async void BtnAdd_Click(object sender, ModBase.RouteEventArgs e)
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
            if (ModJava.Javas.Exist(ret))
            {
                HintService.Hint(Lang.Text("Setup.Java.AlreadyExists"));
                return;
            }

            if (await _vm.AddJavaAsync(ret))
            {
                HintService.Hint(Lang.Text("Setup.Java.Added"), HintType.Success);
                loader.Start(true, true);
            }
            else
            {
                HintService.Hint(Lang.Text("Setup.Java.AddFailed"), HintType.Error);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Java.AddFailed"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Java.AddFailed"));
        }
        finally
        {
            if (control is not null)
                control.IsEnabled = true;
        }
    }
}
