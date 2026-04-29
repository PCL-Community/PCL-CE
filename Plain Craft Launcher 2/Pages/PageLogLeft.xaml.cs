using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using PCL.Core.App;

namespace PCL;

public partial class PageLogLeft
{
    public ModWatcher.Watcher CurrentLog;
    public int CurrentUuid;
    public Dictionary<int, FlowDocument> FlowDocuments = new();
    public int IsLoading;
    public List<KeyValuePair<int, ModWatcher.Watcher>> ShownLogs = new();

    public PageLogLeft()
    {
        InitializeComponent();
        Loaded += PageLogLeft_Loaded;
        Unloaded += PageLogLeft_Unloaded;
    }

    private void PageLogLeft_Loaded(object sender, RoutedEventArgs e)
    {
        Reload();
        ModMain.FrmMain.BtnExtraLog.ShowRefresh();
    }

    private void PageLogLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        ModMain.FrmMain.BtnExtraLog.ShowRefresh();
    }

    private void Reload()
    {
        try
        {
            if (ShownLogs.Count == 0)
            {
                ModMain.FrmMain.PageChange((FormMain.PageType)ModMain.FrmMain.PageCurrentSub);
                return;
            }

            IsLoading += 1;

            // 创建 UI
            ModMain.FrmLogLeft.PanList.Children.Clear();

            // 测试实例列表
            // TODO(i18n): 文本 @ PageLog 左侧 - 列表标题
            ModMain.FrmLogLeft.PanList.Children.Add(new TextBlock
                { Text = "测试实例列表", Margin = new Thickness(13d, 18d, 5d, 4d), Opacity = 0.6d, FontSize = 12d });
            foreach (var item in ShownLogs)
            {
                // 添加控件
                var Uuid = item.Key;
                var Version = item.Value.Version;
                var Proc = item.Value.GameProcess;
                var NewItem = new MyListItem
                {
                    IsScaleAnimationEnabled = false, Type = MyListItem.CheckType.RadioBox, MinPaddingRight = 30,
                    Title = Version.Name, Info = $"{Version.Info} - {Proc.StartTime:HH:mm:ss}", Height = 40d, Tag = Uuid
                };
                NewItem.Changed += ModMain.FrmLogLeft.Version_Change;
                // Dim KillButton As New MyIconButton With {.Logo = Logo.IconButtonCross, .LogoScale = 0.85}
                var RemoveButton = new MyIconButton { Logo = ModBase.Logo.IconButtonDelete, LogoScale = 1.1d };
                // AddHandler KillButton.Click, AddressOf FrmLogLeft.Kill_Click
                RemoveButton.Click += (a, b) => ModMain.FrmLogLeft.Remove_Click(a, (RoutedEventArgs)b);
                NewItem.Buttons = new[] { RemoveButton };
                if (Uuid == CurrentUuid)
                    NewItem.Checked = true;
                ModMain.FrmLogLeft.PanList.Children.Add(NewItem);
            }

            // 通知日志保留设置
            // TODO(i18n): 文本 @ PageLog 左侧 - 日志保留设置通知
            if (!States.Hint.MaxGameLog)
            {
                States.Hint.MaxGameLog = true;
                ModMain.Hint("实时日志默认只保留 500 行，你可以在 实时日志行数 设置中修改！");
            }

            IsLoading -= 1;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "构建游戏实时日志 UI 出错", ModBase.LogLevel.Feedback);
        }
    }

    private void OnLogOutput(ModWatcher.Watcher sender, ModWatcher.LogOutputEventArgs e)
    {
        foreach (var Item in ShownLogs)
            if (Item.Value.GameProcess.Id == sender.GameProcess.Id)
            {
                var Uuid = Item.Key;
                Thickness Margin;
                if (Item.Value.GameProcess.HasExited)
                    Margin = new Thickness(0d, 12d, 0d, 0d);
                else
                    Margin = new Thickness(0d);
                ModBase.RunInUi(() =>
                {
                    var Paragraph = new Paragraph(new Run(e.LogText)) { Foreground = e.Color, Margin = Margin };
                    FlowDocuments[Uuid].Blocks.Add(Paragraph);
                    var MaxLog = (ulong)Config.System.MaxGameLog;
                    switch (MaxLog)
                    {
                        case <= 5UL:
                        {
                            MaxLog = (ulong)Math.Round(MaxLog * 10m + 50m);
                            break;
                        }
                        case <= 13UL:
                        {
                            MaxLog = (ulong)Math.Round(MaxLog * 50m - 150m);
                            break;
                        }
                        case <= 28UL:
                        {
                            MaxLog = (ulong)Math.Round(MaxLog * 100m - 800m);
                            break;
                        }
                        default:
                        {
                            MaxLog = 18446744073709551615UL;
                            break;
                        }
                    }

                    while (FlowDocuments[Uuid].Blocks.Count > (decimal)MaxLog)
                        FlowDocuments[Uuid].Blocks.Remove(FlowDocuments[Uuid].Blocks.FirstBlock);
                });
                return;
            }
    }

    public void Add(ModWatcher.Watcher watcher)
    {
        var uuid = ModBase.GetUuid();
        ShownLogs.Add(new KeyValuePair<int, ModWatcher.Watcher>(uuid, watcher));
        watcher.LogOutput += OnLogOutput;
        ModBase.RunInUi(() => FlowDocuments.Add(uuid, new FlowDocument())); // TODO：在 UI 线程创建
        SelectionChange(uuid);
        ModMain.FrmMain.BtnExtraLog.ShowRefresh();
    }

    public void SelectionChange(int Uuid)
    {
        if (IsLoading > 0)
            return;
        // If CurrentUuid > 0 Then FlowDocuments(CurrentUuid) = FrmLogRight.PanLog.Document
        if (Uuid <= 0)
        {
            CurrentUuid = -1;
            CurrentLog = null;
        }
        else
        {
            foreach (var item in ShownLogs)
                if (item.Key == Uuid)
                {
                    CurrentUuid = Uuid;
                    CurrentLog = item.Value;
                    break;
                }
        }

        ModBase.RunInUi(() =>
        {
            ModMain.FrmLogRight.Reload();
            Reload();
        });
    }

    public void RemoveItem(int Uuid)
    {
        for (int i = 0, loopTo = ShownLogs.Count - 1; i <= loopTo; i++)
        {
            var item = ShownLogs[i];
            if (item.Key != Uuid)
                continue;
            ShownLogs.RemoveAt(i);
            if (CurrentUuid == item.Key)
            {
                if (ShownLogs.Count == 0)
                    // 没有可以显示的了
                    SelectionChange(-1);
                else
                    SelectionChange(ShownLogs[new[] { new[] { i, ShownLogs.Count - 1 }.Min(), 0 }.Max()].Key);
            }
            else
            {
                ModBase.RunInUi(() =>
                {
                    ModMain.FrmLogRight.Reload();
                    Reload();
                });
            }

            break;
        }

        ModMain.FrmMain.BtnExtraLog.ShowRefresh();
    }

    // Public Sub Kill_Click(sender As Object, e As RoutedEventArgs)
    // Dim Uuid As Integer = (CType(CType(sender, MyIconButton).Parent, MyListItem).Tag)
    // For Each item In ShownLogs
    // If item.Key = Uuid Then
    // item.Value.proc.Kill()
    // End If
    // Next
    // End Sub
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
