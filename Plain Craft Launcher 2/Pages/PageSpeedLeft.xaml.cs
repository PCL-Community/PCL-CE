using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSpeedLeft
{
    private const int WatcherInterval = 300;

    // 定时器任务
    private readonly Dictionary<string, MyCard> RightCards = new();

    // 初始化
    private bool IsLoad;

    public PageSpeedLeft()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // 进入时就刷新一次显示
        Watcher();

        // 如果在页面切换动画的 “上一页消失” 部分已经完成了下载，就直接尝试返回
        TryReturnToHome();

        if (IsLoad)
            return;
        IsLoad = true;

        // 监控定时器
        var timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, WatcherInterval) };
        timer.Tick += (_, _) => Watcher();
        timer.Start();

        // 非调试模式隐藏线程数
        if (!ModBase.ModeDebug)
        {
            RowDefinitions[12].Height = new GridLength(0d);
            RowDefinitions[13].Height = new GridLength(0d);
            RowDefinitions[14].Height = new GridLength(0d);
            RowDefinitions[15].Height = new GridLength(0d);
        }
    }

    private void Watcher()
    {
        if (!(ModMain.FrmMain.PageCurrent == FormMain.PageType.TaskManager))
            return;
        try
        {
            #region 更新左边栏

            if (!ModLoader.LoaderTaskbar.Any())
            {
                // 无任务
                LabProgress.Text = Lang.Number(1d, "P0");
                LabSpeed.Text = ModBase.GetString(0) + "/s";
                LabFile.Text = Lang.Number(0, "N0");
                LabThread.Text = Lang.Number(0, "N0") + " / " + Lang.Number(ModNet.NetTaskThreadLimit, "N0");
            }
            else
            {
                // 有任务，输出基本信息
                var Tasks = ModLoader.LoaderTaskbar.Where(l => l.Show).ToList(); // 筛选掉启动 MC 的任务（#6270）
                var RawPercent = Tasks.Any()
                    ? ModBase.MathClamp(
                        Tasks.Average(l => l.Progress),
                        0, 1)
                    : 1d;
                var PredictText = Lang.Number(RawPercent, "P2");
                LabProgress.Text = RawPercent > 0.999999d ? Lang.Number(1d, "P0") : PredictText;
                LabSpeed.Text = ModBase.GetString(ModNet.NetManager.Speed) + "/s";
                LabFile.Text = ModNet.NetManager.FileRemain < 0 ? "0*" : Lang.Number(ModNet.NetManager.FileRemain, "N0");
                LabThread.Text = Lang.Number(ModNet.NetManager.ThreadCount, "N0") + " / " +
                                 Lang.Number(ModNet.NetTaskThreadLimit, "N0");
            }
        }

        #endregion

        catch (Exception ex)
        {
            ModBase.Log(ex, "任务管理左栏监视出错", ModBase.LogLevel.Feedback);
        }

        if (ModMain.FrmSpeedRight is null || ModMain.FrmSpeedRight.PanMain is null)
            return;
        try
        {
            foreach (var Loader in ModLoader.LoaderTaskbar.ToList())
                TaskRefresh(Loader);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "任务管理右栏监视出错", ModBase.LogLevel.Feedback);
        }
    }

    public void TaskRefresh(ModLoader.LoaderBase Loader)
    {
        if (Loader is null || !Loader.Show)
            return;
        try
        {
            // 获取实际加载器列表
            var LoaderList = ((ModLoader.LoaderCombo)Loader).GetLoaderList();
            if (RightCards.ContainsKey(Loader.Name))
            {
                // 已有此卡片
                Grid Card = RightCards[Loader.Name];
                var NewValue = Loader.Progress + (double)Loader.State;
                if (ModBase.Val(Card.Tag) == NewValue)
                    return;
                Card.Tag = NewValue;
                if (Card.Children.Count <= 3)
                {
                    ModBase.Log("[Watcher] 元素不足的卡片：" + Loader.Name, ModBase.LogLevel.Debug);
                    return;
                }

                Card = (Grid)Card.Children[3];
                try
                {
                    switch (Loader.State)
                    {
                        case ModBase.LoadState.Failed:
                        {
                            #region 失败，更新卡片

                            Card.RowDefinitions.Clear();
                            Card.Children.Clear();
                            Card.Children.Add((UIElement)ModBase.GetObjectFromXML(
                                "<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Stretch=\"Uniform\" Tag=\"Failed\" Data=\"F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z\" Height=\"15\" Width=\"15\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"0\" Fill=\"{DynamicResource ColorBrush3}\" Margin=\"0,1,0,0\" VerticalAlignment=\"Top\"/>"));
                            var Tb = (TextBlock)ModBase.GetObjectFromXML(
                                "<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TextWrapping=\"Wrap\" HorizontalAlignment=\"Left\" ToolTip=\"单击复制错误详情\" Grid.Column=\"1\" Grid.Row=\"0\" Margin=\"0,0,0,5\" />");
                            Tb.Text = Loader.Error.ToString();
                            Tb.MouseLeftButtonDown += (sender, _) =>
                            {
                                ModBase.ClipboardSet(((TextBlock)sender).Text, false);
                                ModMain.Hint("已复制错误详情！", ModMain.HintType.Finish);
                            };
                            Card.Children.Add(Tb);
                            break;
                        }

                        #endregion

                        case ModBase.LoadState.Finished:
                        {
                            #region 完成，销毁卡片并返回

                            ModAnimation.AniDispose((MyCard)Card.Parent, true, _ => TryReturnToHome());
                            break;
                        }

                        #endregion

                        case ModBase.LoadState.Loading:
                        case ModBase.LoadState.Waiting:
                        {
                            #region 进度不同，更新卡片

                            do
                            {
                                try
                                {
                                    if (Card.Children.Count < LoaderList.Count * 2)
                                    {
                                        ModBase.Log(
                                            $"[Watcher] 刷新任务管理卡片 {Loader.Name} 失败：卡片中仅有 {Card.Children.Count} 个子项，要求至少有 {LoaderList.Count * 2} 个子项",
                                            ModBase.LogLevel.Debug);
                                        break;
                                    }

                                    var Row = 0;
                                    foreach (var SubTask in LoaderList)
                                    {
                                        switch (SubTask.State)
                                        {
                                            case ModBase.LoadState.Waiting:
                                            {
                                                if ((string)((FrameworkElement)Card.Children[Row * 2]).Tag != "Waiting")
                                                {
                                                    Card.Children.RemoveAt(Row * 2);
                                                    Card.Children.Insert(Row * 2,
                                                        (UIElement)ModBase.GetObjectFromXML(
                                                            "<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\" Stretch=\"Uniform\" Tag=\"Waiting\" Data=\"F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z\" Width=\"18\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"" +
                                                            Row +
                                                            "\" Fill=\"{DynamicResource ColorBrush3}\" Margin=\"0,7,0,0\" VerticalAlignment=\"Top\" Height=\"6\"/>"));
                                                }

                                                break;
                                            }
                                            case ModBase.LoadState.Loading:
                                            {
                                                if ((string)((FrameworkElement)Card.Children[Row * 2]).Tag != "Loading")
                                                {
                                                    Card.Children.RemoveAt(Row * 2);
                                                    Card.Children.Insert(Row * 2,
                                                        (UIElement)ModBase.GetObjectFromXML(
                                                            $"<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\" Text=\"{Lang.Number(SubTask.Progress, "P0")}\" Tag=\"Loading\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"{Row}\" Foreground=\"{{DynamicResource ColorBrush3}}\"/>"));
                                                }
                                                else
                                                {
                                                    ((TextBlock)Card.Children[Row * 2]).Text =
                                                        $"{Lang.Number(SubTask.Progress, "P0")}";
                                                }

                                                break;
                                            }
                                            case ModBase.LoadState.Finished:
                                            {
                                                if ((string)((FrameworkElement)Card.Children[Row * 2]).Tag != "Finished")
                                                {
                                                    Card.Children.RemoveAt(Row * 2);
                                                    Card.Children.Insert(Row * 2,
                                                        (UIElement)ModBase.GetObjectFromXML(
                                                            $"<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\" Stretch=\"Uniform\" Tag=\"Finished\" Data=\"F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z\" Height=\"16\" Width=\"15\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"{Row}\" Fill=\"{{DynamicResource ColorBrush3}}\" Margin=\"0,3,0,0\" VerticalAlignment=\"Top\"/>"));
                                                }

                                                break;
                                            }
                                        }

                                        Row += 1;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ModBase.Log(ex, $"刷新任务管理卡片 {Loader.Name} 失败", ModBase.LogLevel.Feedback);
                                }
                            } while (false);

                            break;
                        }

                        #endregion
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"更新任务管理显示失败（{Loader.State}）", ModBase.LogLevel.Feedback);
                }
            }
            else if (!(Loader.State == ModBase.LoadState.Aborted || Loader.State == ModBase.LoadState.Finished))
            {
                try
                {
                    #region 没有卡片且未中断或完成，添加新的卡片

                    var CardXAML = $@"
                        <local:MyCard xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2""
                            Tag=""{Loader.Progress + (double)Loader.State}"" Title=""{ModBase.EscapeXML(Loader.Name)}"" Margin=""0,0,0,15"">
                            <Grid Margin=""14,40,15,10"">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""50""/>
                                    <ColumnDefinition/>
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>";
                    foreach (var SubTask in LoaderList)
                        CardXAML += "<RowDefinition Height=\"26\"/>";
                    CardXAML += "</Grid.RowDefinitions>";
                    var Row = 0;
                    foreach (var SubTask in LoaderList)
                    {
                        switch (SubTask.State)
                        {
                            case ModBase.LoadState.Waiting:
                            {
                                CardXAML +=
                                    $"<Path Stretch=\"Uniform\" Tag=\"Waiting\" Data=\"F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z\" Width=\"18\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"{Row}\" Fill=\"{{DynamicResource ColorBrush3}}\" Margin=\"0,7,0,0\" VerticalAlignment=\"Top\" Height=\"6\"/>";
                                break;
                            }
                            case ModBase.LoadState.Loading:
                            {
                                CardXAML += $"<TextBlock Text=\"{Lang.Number(SubTask.Progress, "P0")}\" Tag=\"Loading\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"{Row}\" Foreground=\"{{DynamicResource ColorBrush3}}\" />";
                                break;
                            }
                            case ModBase.LoadState.Finished:
                            {
                                CardXAML +=
                                    $"<Path Stretch=\"Uniform\" Tag=\"Finished\" Data=\"F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z\" Height=\"16\" Width=\"15\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"{Row}\" Fill=\"{{DynamicResource ColorBrush3}}\" Margin=\"0,3,0,0\" VerticalAlignment=\"Top\"/>";
                                break;
                            }

                            default:
                            {
                                CardXAML +=
                                    $"<Path Stretch=\"Uniform\" Tag=\"Failed\" Data=\"F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z\" Height=\"15\" Width=\"15\" HorizontalAlignment=\"Center\" Grid.Column=\"0\" Grid.Row=\"{Row}\" Fill=\"{{DynamicResource ColorBrush3}}\" Margin=\"0,1,0,0\" VerticalAlignment=\"Top\"/>";
                                break;
                            }
                        }

                        CardXAML += $"<TextBlock Text=\"{ModBase.EscapeXML(SubTask.Name)}\" HorizontalAlignment=\"Left\" Grid.Column=\"1\" Grid.Row=\"{Row}\"/>";
                        Row += 1;
                    }

                    CardXAML += "</Grid></local:MyCard>";
                    // 实例化控件
                    MyCard Card;
                    try
                    {
                        Card = (MyCard)ModBase.GetObjectFromXML(CardXAML);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "新建任务管理卡片失败");
                        ModBase.Log($"出错的卡片内容：\r\n{CardXAML}");
                        throw;
                    }

                    ModMain.FrmSpeedRight.PanMain.Children.Insert(0, Card);
                    RightCards.Add(Loader.Name, Card);
                    ModBase.Log($"[Watcher] 新建任务管理卡片：{Loader.Name}");
                    // 添加取消按钮
                    var Cancel = new MyIconButton
                    {
                        Name = "BtnCancel",
                        Logo = "F1 M2,0 L0,2 8,10 0,18 2,20 10,12 18,20 20,18 12,10 20,2 18,0 10,8 2,0Z", Height = 20d,
                        Margin = new Thickness(0d, 10d, 10d, 0d), LogoScale = 1.1d,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
                    };
                    Card.Children.Add(Cancel);
                    Cancel.Click += (sender, e) =>
                    {
                        ModAnimation.AniDispose((MyIconButton)sender, false);
                        ModAnimation.AniDispose(Card, true, _ =>
                        {
                            if (ModMain.FrmSpeedRight.PanMain.Children.Count == 0 &&
                                ModMain.FrmMain.PageCurrent == FormMain.PageType.TaskManager)
                                ModMain.FrmMain.PageBack();
                        });
                        RightCards.Remove(Loader.Name);
                        ModLoader.LoaderTaskbar.Remove(Loader);
                        ModBase.Log($"[Taskbar] 关闭任务管理卡片：{Loader.Name}，且移出任务列表");
                        ModBase.RunInThread(() => Loader.Abort());
                    };
                    // 如果已经失败，再刷新一次，修改成失败的控件
                    if (Loader.State == ModBase.LoadState.Failed)
                    {
                        Card.Tag = null; // 避免重复导致刷新无效
                        TaskRefresh(Loader);
                    }
                }

                #endregion

                catch (Exception ex)
                {
                    ModBase.Log(ex, "添加任务管理卡片失败", ModBase.LogLevel.Feedback);
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新任务管理显示失败", ModBase.LogLevel.Feedback);
        }
    }

    public void TaskRemove(ModLoader.LoaderBase Loader)
    {
        if (RightCards.ContainsKey(Loader.Name))
            ModBase.RunInUiWait(() =>
            {
                // 移除已有的卡片
                Grid Card = RightCards[Loader.Name];
                ModMain.FrmSpeedRight.PanMain.Children.Remove(Card);
                RightCards.Remove(Loader.Name);
                ModBase.Log($"[Watcher] 移除任务管理卡片：{Loader.Name}");
            });
    }

    /// <summary>
    ///     若没有任务，尝试返回主页。
    /// </summary>
    private void TryReturnToHome()
    {
        if (ModMain.FrmSpeedRight.PanMain.Children.Count == 0 &&
            ModMain.FrmMain.PageCurrent == FormMain.PageType.TaskManager) ModMain.FrmMain.PageBack();
    }
}
