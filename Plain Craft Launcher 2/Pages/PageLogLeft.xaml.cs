using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public partial class PageLogLeft
{
    public ModWatcher.Watcher currentLog;
    public int currentUuid;
    public Dictionary<int, FlowDocument> flowDocuments = new();
    public int isLoading;
    public List<KeyValuePair<int, ModWatcher.Watcher>> shownLogs = new();

    public PageLogLeft()
    {
        InitializeComponent();
        Loaded += PageLogLeft_Loaded;
        Unloaded += PageLogLeft_Unloaded;
    }

    private void PageLogLeft_Loaded(object sender, RoutedEventArgs e)
    {
        Reload();
        ModMain.frmMain.BtnExtraLog.ShowRefresh();
    }

    private void PageLogLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        ModMain.frmMain.BtnExtraLog.ShowRefresh();
    }

    private void Reload()
    {
        try
        {
            if (shownLogs.Count == 0)
            {
                ModMain.frmMain.PageChange((FormMain.PageType)ModMain.frmMain.PageCurrentSub);
                return;
            }

            isLoading += 1;

            // 创建 UI
            ModMain.frmLogLeft.PanList.Children.Clear();

            // 测试实例列表
            ModMain.frmLogLeft.PanList.Children.Add(new TextBlock
                { Text = Lang.Text("LogPage.Left.InstancesTitle"), Margin = new Thickness(13d, 18d, 5d, 4d), Opacity = 0.6d, FontSize = 12d });
            foreach (var item in shownLogs)
            {
                // 添加控件
                var uuid = item.Key;
                var version = item.Value.version;
                var proc = item.Value.gameProcess;
                var newItem = new MyListItem
                {
                    IsScaleAnimationEnabled = false, Type = MyListItem.CheckType.RadioBox, MinPaddingRight = 30,
                    Title = version.Name, Info = $"{version.Info} - {Lang.Date(proc.StartTime, "T")}", Height = 40d, Tag = uuid
                };
                newItem.Changed += ModMain.frmLogLeft.Version_Change;
                // Dim KillButton As New MyIconButton With {.Logo = Logo.IconButtonCross, .LogoScale = 0.85}
                var removeButton = new MyIconButton { SvgIcon = "lucide/trash-2", LogoScale = 1.1d };
                // AddHandler KillButton.Click, AddressOf FrmLogLeft.Kill_Click
                removeButton.Click += (a, b) => ModMain.frmLogLeft.Remove_Click(a, (RoutedEventArgs)b);
                newItem.Buttons = new[] { removeButton };
                if (uuid == currentUuid)
                    newItem.Checked = true;
                ModMain.frmLogLeft.PanList.Children.Add(newItem);
            }

            // 通知日志保留设置
            if (!States.Hint.MaxGameLog)
            {
                States.Hint.MaxGameLog = true;
                HintService.Hint(Lang.Text("LogPage.MaxLines.Hint", 500));
            }

            isLoading -= 1;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "构建游戏实时日志 UI 出错", ModBase.LogLevel.Feedback);
        }
    }

    private void OnLogOutput(ModWatcher.Watcher sender, ModWatcher.LogOutputEventArgs e)
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
                ModBase.RunInUi(() =>
                {
                    var paragraph = new Paragraph(new Run(e.logText)) { Foreground = e.color, Margin = margin };
                    flowDocuments[uuid].Blocks.Add(paragraph);
                    var maxLog = (ulong)Config.System.MaxGameLog;
                    switch (maxLog)
                    {
                        case <= 5UL:
                        {
                            maxLog = (ulong)Math.Round(maxLog * 10m + 50m);
                            break;
                        }
                        case <= 13UL:
                        {
                            maxLog = (ulong)Math.Round(maxLog * 50m - 150m);
                            break;
                        }
                        case <= 28UL:
                        {
                            maxLog = (ulong)Math.Round(maxLog * 100m - 800m);
                            break;
                        }
                        default:
                        {
                            maxLog = 18446744073709551615UL;
                            break;
                        }
                    }

                    while (flowDocuments[uuid].Blocks.Count > (decimal)maxLog)
                        flowDocuments[uuid].Blocks.Remove(flowDocuments[uuid].Blocks.FirstBlock);
                });
                return;
            }
    }

    public void Add(ModWatcher.Watcher watcher)
    {
        var uuid = ModBase.GetUuid();
        shownLogs.Add(new KeyValuePair<int, ModWatcher.Watcher>(uuid, watcher));
        watcher.LogOutput += OnLogOutput;
        ModBase.RunInUi(() => flowDocuments.Add(uuid, new FlowDocument())); // TODO：在 UI 线程创建
        SelectionChange(uuid);
        ModMain.frmMain.BtnExtraLog.ShowRefresh();
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

        ModBase.RunInUi(() =>
        {
            ModMain.frmLogRight.Reload();
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
                ModBase.RunInUi(() =>
                {
                    ModMain.frmLogRight.Reload();
                    Reload();
                });
            }

            break;
        }

        ModMain.frmMain.BtnExtraLog.ShowRefresh();
    }

    public void Remove_Click(object sender, RoutedEventArgs e)
    {
        RemoveItem((int)((MyListItem)((MyIconButton)sender).Parent).Tag);
    }

    // 点击选项
    public void Version_Change(object sender, ModBase.RouteEventArgs e)
    {
        SelectionChange((int)((MyListItem)sender).Tag);
    }
}
