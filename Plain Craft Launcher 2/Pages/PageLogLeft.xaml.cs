using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI;

namespace PCL;

public partial class PageLogLeft : IPageView
{
    public GameWatcher.Watcher currentLog;
    public int currentUuid;
    public Dictionary<int, FlowDocument> flowDocuments = new();
    public int isLoading;
    public List<KeyValuePair<int, GameWatcher.Watcher>> shownLogs = new();

    private readonly PageLogLeftViewModel _vm = new();

    private bool _refreshed;

    public PageLogLeft()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += PageLogLeft_Loaded;
        Unloaded += PageLogLeft_Unloaded;
    }

    public PageLogLeftViewModel ViewModel => _vm;

    private void PageLogLeft_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Reload();
        Launcher.frmMain.BtnExtraLog.ShowRefresh();
    }

    private void PageLogLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshed = false;
        Launcher.frmMain.BtnExtraLog.ShowRefresh();
    }

    private void Reload()
    {
        try
        {
            if (!_vm.HasLogs)
            {
                Launcher.frmMain.PageChange((FormMain.PageType)Launcher.frmMain.PageCurrentSub);
                return;
            }

            isLoading += 1;

            // 创建 UI
            Launcher.frmLogLeft.PanList.Children.Clear();

            // 测试实例列表
            Launcher.frmLogLeft.PanList.Children.Add(new TextBlock
                { Text = _vm.InstancesTitle, Margin = new Thickness(13d, 18d, 5d, 4d), Opacity = 0.6d, FontSize = 12d });
            foreach (var item in shownLogs)
            {
                // 添加控件
                var uuid = item.Key;
                var version = item.Value.version;
                var newItem = new MyListItem
                {
                    IsScaleAnimationEnabled = false, Type = MyListItem.CheckType.RadioBox, MinPaddingRight = 30,
                    Title = version.Name, Info = _vm.GetItemInfo(item.Value), Height = 40d, Tag = uuid
                };
                newItem.Changed += Launcher.frmLogLeft.Version_Change;
                // Dim KillButton As New MyIconButton With {.Logo = Logo.IconButtonCross, .LogoScale = 0.85}
                var removeButton = new MyIconButton { SvgIcon = "lucide/trash-2", LogoScale = 1.1d };
                // AddHandler KillButton.Click, AddressOf FrmLogLeft.Kill_Click
                removeButton.Click += (a, b) => Launcher.frmLogLeft.Remove_Click(a, (RoutedEventArgs)b);
                newItem.Buttons = new[] { removeButton };
                if (uuid == currentUuid)
                    newItem.Checked = true;
                Launcher.frmLogLeft.PanList.Children.Add(newItem);
            }

            // 通知日志保留设置
            if (_vm.ShouldShowMaxLinesHint)
            {
                States.Hint.MaxGameLog = true;
                HintService.Show(Hints.Info(Lang.Text("LogPage.MaxLines.Hint", 500)));
            }

            isLoading -= 1;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "构建游戏实时日志 UI 出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("LogPage.Error.OperationFailed"));
        }
    }

    private void OnLogOutput(GameWatcher.Watcher sender, GameWatcher.LogOutputEventArgs e)
    {
        foreach (var Item in shownLogs)
            if (Item.Value.gameProcess.Id == sender.gameProcess.Id)
            {
                var uuid = Item.Key;
                Thickness margin;
                if (Item.Value.gameProcess.HasExited)
                    margin = new Thickness(0d, 12d, 0d, 0d);
                else
                    margin = new Thickness(0d);
                Common.RunInUi(() =>
                {
                    var paragraph = new Paragraph(new Run(e.logText)) { Foreground = e.color, Margin = margin };
                    flowDocuments[uuid].Blocks.Add(paragraph);
                    var maxLog = _vm.GetMaxLogCount();

                    while (flowDocuments[uuid].Blocks.Count > (decimal)maxLog)
                        flowDocuments[uuid].Blocks.Remove(flowDocuments[uuid].Blocks.FirstBlock);
                });
                return;
            }
    }

    public void Add(GameWatcher.Watcher watcher)
    {
        var uuid = Common.GetUuid();
        shownLogs.Add(new KeyValuePair<int, GameWatcher.Watcher>(uuid, watcher));
        watcher.LogOutput += OnLogOutput;
        Common.RunInUi(() => flowDocuments.Add(uuid, new FlowDocument())); // TODO：在 UI 线程创建
        SelectionChange(uuid);
        Launcher.frmMain.BtnExtraLog.ShowRefresh();
    }

    public void SelectionChange(int uuid)
    {
        if (isLoading > 0)
            return;
        // If CurrentUuid > 0 Then FlowDocuments(CurrentUuid) = FrmLogRight.PanLog.Document
        if (uuid <= 0)
        {
            currentUuid = -1;
            currentLog = null;
        }
        else
        {
            foreach (var item in shownLogs)
                if (item.Key == uuid)
                {
                    currentUuid = uuid;
                    currentLog = item.Value;
                    break;
                }
        }

        Common.RunInUi(() =>
        {
            Launcher.frmLogRight.Reload();
            Reload();
        });
    }

    public void RemoveItem(int uuid)
    {
        for (int i = 0, loopTo = shownLogs.Count - 1; i <= loopTo; i++)
        {
            var item = shownLogs[i];
            if (item.Key != uuid)
                continue;
            shownLogs.RemoveAt(i);
            if (currentUuid == item.Key)
            {
                if (shownLogs.Count == 0)
                    // 没有可以显示的了
                    SelectionChange(-1);
                else
                    SelectionChange(shownLogs[new[] { new[] { i, shownLogs.Count - 1 }.Min(), 0 }.Max()].Key);
            }
            else
            {
                Common.RunInUi(() =>
                {
                    Launcher.frmLogRight.Reload();
                    Reload();
                });
            }

            break;
        }

        Launcher.frmMain.BtnExtraLog.ShowRefresh();
    }

    public void Remove_Click(object sender, RoutedEventArgs e)
    {
        RemoveItem((int)((MyListItem)((MyIconButton)sender).Parent).Tag);
    }

    // 点击选项
    public void Version_Change(object sender, Common.RouteEventArgs e)
    {
        SelectionChange((int)((MyListItem)sender).Tag);
    }
}
