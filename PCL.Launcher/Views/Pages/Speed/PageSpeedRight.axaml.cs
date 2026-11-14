using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Speed;

/// <summary>
///     任务管理页右栏：按 <see cref="Loading.loaderTaskbar" /> 中的加载器生成任务卡片，
///     展示子任务的等待/进度/完成/失败状态，并提供取消按钮。
/// </summary>
public partial class PageSpeedRight : MyPageRight, IRefreshable
{
    private const int WatcherInterval = 300;

    private const string WaitingIconData =
        "F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z";

    private const string FinishedIconData =
        "F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z";

    private const string FailedIconData =
        "F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z";


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
            foreach (var loader in loaders.Where(loader => loader.show))
            {
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
                    DisposeFinished(existing);
                    return;

                case LoadState.Loading:
                case LoadState.Waiting:
                    UpdateProgress(existing, loader);
                    return;

                default:
                    return;
            }
        }

        if (loader.State is LoadState.Aborted or LoadState.Finished)
            return;

        var card = new MyCard
        {
            Title = loader.name,
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var grid = new Grid
        {
            Margin = new Thickness(14d, 40d, 15d, 10d),
            ColumnDefinitions = new ColumnDefinitions("50,*")
        };
        var cancel = new MyIconButton
        {
            SvgIcon = "lucide/x",
            Width = 25d,
            Height = 25d,
            Margin = new Thickness(0d, 10d, 10d, 0d),
            LogoScale = 1.1d,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        var taskCard = new TaskCard
        {
            Card = card,
            Grid = grid,
            Cancel = cancel,
            Value = loader.Progress + (double)loader.State
        };
        card.Children.Add(grid);
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
        RebuildGrid(card, subtasks.Count, new GridLength(26d));
        for (var row = 0; row < subtasks.Count; row++)
        {
            var subtask = subtasks[row];
            card.Grid.Children.Add(CreateStateIcon(subtask, row));
            var label = new TextBlock
            {
                Text = subtask.name,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(label, 1);
            Grid.SetRow(label, row);
            card.Grid.Children.Add(label);
        }
    }

    private static void UpdateProgress(TaskCard card, Loading.LoaderBase loader)
    {
        var subtasks = GetSubtasks(loader);
        if (card.Grid.Children.Count < subtasks.Count * 2)
        {
            LauncherCommon.Log(
                $"[Watcher] 刷新任务管理卡片 {loader.name} 失败：卡片中仅有 {card.Grid.Children.Count} 个子项，要求至少有 {subtasks.Count * 2} 个子项",
                LauncherLogLevel.Debug);
            return;
        }

        for (var row = 0; row < subtasks.Count; row++)
        {
            var subtask = subtasks[row];
            var icon = card.Grid.Children[row * 2];
            switch (subtask.State)
            {
                case LoadState.Waiting when icon.Tag as string != "Waiting":
                case LoadState.Finished when icon.Tag as string != "Finished":
                    ReplaceIcon(card, row, CreateStateIcon(subtask, row));
                    break;

                case LoadState.Loading when icon.Tag as string != "Loading":
                    ReplaceIcon(card, row, CreateStateIcon(subtask, row));
                    break;

                case LoadState.Loading:
                    ((TextBlock)icon).Text = Lang.Number(subtask.Progress, "P0");
                    break;
            }
        }
    }

    private static void BuildFailed(TaskCard card, Loading.LoaderBase loader)
    {
        card.Grid.Children.Clear();
        card.Grid.RowDefinitions.Clear();
        var icon = CreatePath(FailedIconData, 15d, 15d, new Thickness(0d, 1d, 0d, 0d), "Failed");
        Grid.SetColumn(icon, 0);
        Grid.SetRow(icon, 0);
        card.Grid.Children.Add(icon);

        var text = new TextBlock
        {
            Text = loader.Error?.ToString() ?? string.Empty,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 0d, 0d, 5d)
        };
        ToolTip.SetTip(text, Lang.Text("Speed.Error.ClickToCopy"));
        text.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(text).Properties.IsLeftButtonPressed)
                return;
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

    private static Control CreateStateIcon(Loading.LoaderBase subtask, int row)
    {
        Control control = subtask.State switch
        {
            LoadState.Waiting => CreatePath(
                WaitingIconData,
                18d,
                6d,
                new Thickness(0d, 7d, 0d, 0d),
                "Waiting"),
            LoadState.Finished => CreatePath(
                FinishedIconData,
                15d,
                16d,
                new Thickness(0d, 3d, 0d, 0d),
                "Finished"),
            LoadState.Failed or LoadState.Aborted => CreatePath(
                FailedIconData,
                15d,
                15d,
                new Thickness(0d, 1d, 0d, 0d),
                "Failed"),
            _ => CreateProgressText(subtask)
        };

        Grid.SetColumn(control, 0);
        Grid.SetRow(control, row);
        return control;
    }

    private static TextBlock CreateProgressText(Loading.LoaderBase subtask)
    {
        var text = new TextBlock
        {
            Text = Lang.Number(subtask.Progress, "P0"),
            Tag = "Loading",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        BindAccent(text, TextBlock.ForegroundProperty);
        return text;
    }

    private static ShapePath CreatePath(
        string data,
        double width,
        double height,
        Thickness margin,
        string tag)
    {
        var path = new ShapePath
        {
            Data = Geometry.Parse(data),
            Width = width,
            Height = height,
            Margin = margin,
            Tag = tag,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        BindAccent(path, Shape.FillProperty);
        return path;
    }

    private static void BindAccent(AvaloniaObject target, AvaloniaProperty property)
    {
        target.Bind(property, new DynamicResourceExtension("ColorBrush3"));
    }

    private static void ReplaceIcon(TaskCard card, int row, Control icon)
    {
        card.Grid.Children.RemoveAt(row * 2);
        card.Grid.Children.Insert(row * 2, icon);
    }

    private static void RebuildGrid(TaskCard card, int rowCount, GridLength rowHeight)
    {
        card.Grid.Children.Clear();
        card.Grid.RowDefinitions.Clear();
        for (var i = 0; i < rowCount; i++)
            card.Grid.RowDefinitions.Add(new RowDefinition(rowHeight));
    }

    private void DisposeFinished(TaskCard card)
    {
        if (card.Closing)
            return;
        card.Closing = true;
        card.Card.AniDispose(true, _ => TryReturnToHome());
    }

    private void CancelLoader(Loading.LoaderBase loader, TaskCard card)
    {
        if (card.Closing)
            return;
        card.Closing = true;
        card.Cancel.AniDispose(false);
        card.Card.AniDispose(true, _ => TryReturnToHome());
        _cards.Remove(loader.name);
        Loading.loaderTaskbar.Remove(loader);
        LauncherCommon.Log($"[Taskbar] 关闭任务管理卡片：{loader.name}，且移出任务列表");
        LauncherCommon.RunInThread(() => loader.Abort());
    }

    private void RemoveCard(string name)
    {
        if (!_cards.Remove(name, out var card))
            return;
        PanMain.Children.Remove(card.Card);
        LauncherCommon.Log($"[Watcher] 移除任务管理卡片：{name}");
    }

    private void TryReturnToHome()
    {
        if (PanMain.Children.Count > 0)
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

        public required MyIconButton Cancel { get; init; }

        public double Value { get; set; }

        public bool Closing { get; set; }
    }
}