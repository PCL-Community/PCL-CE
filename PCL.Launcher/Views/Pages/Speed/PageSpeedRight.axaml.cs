using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.Controls.SvgIcon;

namespace PCL.Views.Pages.Speed;

/// <summary>
///     任务管理页右栏：按 <see cref="Loading.loaderTaskbar" /> 中的加载器生成任务卡片，
///     展示子任务的等待/进度/完成/失败状态，并提供取消按钮。
/// </summary>
public partial class PageSpeedRight : MyPageRight, IRefreshable
{
    private const int WatcherInterval = 300;

    private readonly Dictionary<string, TaskCard> _cards = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _timer;
    private bool _hadTasks;

    public PageSpeedRight()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Current = this;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(WatcherInterval)
        };
        _timer.Tick += (_, _) => SyncTasks();
    }

    public static PageSpeedRight? Current { get; private set; }

    public void Refresh()
    {
        RefreshNow();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        SyncTasks();
        _timer.Start();
    }

    public override void OnNavigatedFrom()
    {
        _timer.Stop();
    }

    protected override void RefreshLocalization()
    {
        _cards.Clear();
        PanMain.Children.Clear();
        SyncTasks();
    }

    /// <summary>立即同步任务卡片；可在任意线程调用。</summary>
    public void RefreshNow()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(SyncTasks);
            return;
        }

        SyncTasks();
    }

    public void RemoveTask(Loading.LoaderBase loader)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RemoveTask(loader));
            return;
        }

        RemoveCard(loader.name);
        TryReturnToHome();
    }

    private void SyncTasks()
    {
        try
        {
            var loaders = Loading.loaderTaskbar.ToList();
            var alive = new HashSet<string>(StringComparer.Ordinal);
            foreach (var loader in loaders)
            {
                if (!loader.show)
                    continue;
                alive.Add(loader.name);
                UpdateOrCreate(loader);
            }

            foreach (var name in _cards.Keys.Where(name => !alive.Contains(name)).ToList())
                RemoveCard(name);

            if (_cards.Count > 0)
                _hadTasks = true;
            else if (_hadTasks)
                TryReturnToHome();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "刷新任务管理显示失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Speed.Error.OperationFailed"));
        }
    }

    private void UpdateOrCreate(Loading.LoaderBase loader)
    {
        if (_cards.TryGetValue(loader.name, out var existing))
        {
            var value = loader.Progress + (double)loader.State;
            if (Math.Abs(existing.Value - value) < 1e-9d)
                return;
            existing.Value = value;

            switch (loader.State)
            {
                case LoadState.Failed:
                    BuildFailed(existing, loader);
                    return;

                case LoadState.Finished:
                    RemoveCard(loader.name);
                    return;

                default:
                    BuildProgress(existing, loader);
                    return;
            }
        }

        if (loader.State is LoadState.Aborted or LoadState.Finished)
            return;

        var card = new MyCard
        {
            Title = loader.DisplayName,
            Margin = new Thickness(0d, 0d, 0d, 15d),
            UseAnimation = false,
            HasMouseAnimation = false
        };
        var grid = new Grid
        {
            Margin = new Thickness(14d, MyCard.SwapedHeight, 15d, 12d),
            ColumnDefinitions = new ColumnDefinitions("40,*")
        };
        var taskCard = new TaskCard
        {
            Card = card,
            Grid = grid,
            Value = loader.Progress + (double)loader.State
        };
        card.Children.Add(grid);

        var cancel = new MyIconButton
        {
            SvgIcon = "lucide/x",
            Width = 25d,
            Height = 25d,
            Margin = new Thickness(0d, 9d, 9d, 0d),
            LogoScale = 1.1d,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        ToolTip.SetTip(cancel, Lang.Text("Common.Action.Cancel"));
        cancel.Click += (_, _) => CancelLoader(loader, taskCard);
        card.Children.Add(cancel);

        PanMain.Children.Insert(0, card);
        _cards[loader.name] = taskCard;
        LauncherCommon.Log($"[Watcher] 新建任务管理卡片：{loader.name}");

        if (loader.State == LoadState.Failed)
            BuildFailed(taskCard, loader);
        else
            BuildProgress(taskCard, loader);
    }

    private static void BuildProgress(TaskCard card, Loading.LoaderBase loader)
    {
        var subtasks = GetSubtasks(loader);
        RebuildGrid(card, subtasks.Count);
        for (var row = 0; row < subtasks.Count; row++)
        {
            var subtask = subtasks[row];
            card.Grid.Children.Add(CreateSubtaskStateIcon(subtask, row));
            var label = new TextBlock
            {
                Text = subtask.DisplayName,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);
            Grid.SetRow(label, row);
            card.Grid.Children.Add(label);
        }
    }

    private static void BuildFailed(TaskCard card, Loading.LoaderBase loader)
    {
        RebuildGrid(card, 1);
        var icon = new SvgIcon
        {
            Icon = "lucide/x",
            Width = 15d,
            Height = 15d,
            Margin = new Thickness(0d, 1d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Stretch = Stretch.Uniform
        };
        Grid.SetColumn(icon, 0);
        Grid.SetRow(icon, 0);
        card.Grid.Children.Add(icon);

        var text = new SelectableTextBlock
        {
            Text = loader.Error?.ToString() ?? string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 0d, 0d, 5d)
        };
        ToolTip.SetTip(text, Lang.Text("Speed.Error.ClickToCopy"));
        text.PointerPressed += (_, _) =>
        {
            if (TopLevel.GetTopLevel(text)?.Clipboard is { } clipboard)
                _ = clipboard.SetTextAsync(text.Text ?? string.Empty);
            HintService.Show(Hints.Success(Lang.Text("Speed.Error.Copied")));
        };
        Grid.SetColumn(text, 1);
        Grid.SetRow(text, 0);
        card.Grid.Children.Add(text);
    }

    private static List<Loading.LoaderBase> GetSubtasks(Loading.LoaderBase loader)
    {
        return loader is Loading.LoaderCombo combo ? combo.GetLoaderList() : [loader];
    }

    private static Control CreateSubtaskStateIcon(Loading.LoaderBase subtask, int row)
    {
        Control control;
        switch (subtask.State)
        {
            case LoadState.Waiting:
                control = new SvgIcon
                {
                    Icon = "lucide/circle",
                    Width = 9d,
                    Height = 9d,
                    Stretch = Stretch.Uniform
                };
                break;
            case LoadState.Finished:
                control = new SvgIcon
                {
                    Icon = "lucide/circle-check",
                    Width = 16d,
                    Height = 16d,
                    Stretch = Stretch.Uniform
                };
                break;
            case LoadState.Failed:
                control = new SvgIcon
                {
                    Icon = "lucide/x",
                    Width = 15d,
                    Height = 15d,
                    Stretch = Stretch.Uniform
                };
                break;
            default:
                control = new TextBlock
                {
                    Text = Lang.Number(subtask.Progress, "P0"),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                break;
        }

        control.HorizontalAlignment = HorizontalAlignment.Center;
        control.VerticalAlignment = VerticalAlignment.Top;
        control.Margin = new Thickness(0d, 6d, 0d, 0d);
        Grid.SetColumn(control, 0);
        Grid.SetRow(control, row);
        return control;
    }

    private static void RebuildGrid(TaskCard card, int rowCount)
    {
        card.Grid.Children.Clear();
        card.Grid.RowDefinitions.Clear();
        for (var i = 0; i < rowCount; i++)
            card.Grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
    }

    private void CancelLoader(Loading.LoaderBase loader, TaskCard card)
    {
        card.Card.AniDispose(true);
        _cards.Remove(loader.name);
        Loading.loaderTaskbar.Remove(loader);
        LauncherCommon.Log($"[Taskbar] 关闭任务管理卡片：{loader.name}，且移出任务列表");
        LauncherCommon.RunInThread(() => loader.Abort());
        TryReturnToHome();
    }

    private void RemoveCard(string name)
    {
        if (!_cards.Remove(name, out var card))
            return;
        card.Card.AniDispose(true);
        LauncherCommon.Log($"[Watcher] 移除任务管理卡片：{name}");
    }

    private void TryReturnToHome()
    {
        if (_cards.Count > 0)
            return;
        if (PageRouter.Current.Page != PageType.TaskManager)
            return;
        _hadTasks = false;
        PageRouter.Back();
    }

    private sealed class TaskCard
    {
        public required MyCard Card { get; init; }

        public required Grid Grid { get; init; }

        public double Value { get; set; }
    }
}