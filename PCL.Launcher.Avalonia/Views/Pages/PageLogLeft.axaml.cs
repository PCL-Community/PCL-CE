using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.Views.Pages;

// TODO: 右侧日志正文页（PageLogRight）尚未迁移，当前只暴露 SelectionChanged/EmptyRequested 事件，主窗口接线后再联动。
/// <summary>
///     游戏实时日志左侧栏：列出当前正在监控的 Minecraft 进程。
///     与 WPF 版本一致，由外部调用 <see cref="Add" /> 登记监控对象；
///     右侧日志正文属于 PageLogRight，待其迁移后通过 <see cref="SelectionChanged" /> 联动。
/// </summary>
public partial class PageLogLeft : PageBase
{
    public readonly List<KeyValuePair<int, GameWatcher.Watcher>> shownLogs = [];

    private readonly StackPanel? _panList;
    private bool _refreshed;

    public GameWatcher.Watcher? currentLog;
    public int currentUuid;

    public PageLogLeft()
    {
        InitializeComponent();
        _panList = this.FindControl<StackPanel>("PanList");
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) => _refreshed = false;
    }

    /// <summary>当前选中的日志编号变化，供右侧日志页或主窗口联动。</summary>
    public event Action<int>? SelectionChanged;

    /// <summary>已经没有可显示的日志，主窗口可据此退出日志页面。</summary>
    public event Action? EmptyRequested;

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Reload();
    }

    public void Add(GameWatcher.Watcher watcher)
    {
        var uuid = LauncherCommon.GetUuid();
        shownLogs.Add(new KeyValuePair<int, GameWatcher.Watcher>(uuid, watcher));
        SelectionChange(uuid);
    }

    public void SelectionChange(int uuid)
    {
        if (uuid <= 0)
        {
            currentUuid = -1;
            currentLog = null;
        }
        else
        {
            foreach (var pair in shownLogs)
                if (pair.Key == uuid)
                {
                    currentUuid = uuid;
                    currentLog = pair.Value;
                    break;
                }
        }

        SelectionChanged?.Invoke(currentUuid);
        Reload();
    }

    public void RemoveItem(int uuid)
    {
        for (var i = shownLogs.Count - 1; i >= 0; i--)
        {
            var item = shownLogs[i];
            if (item.Key != uuid)
                continue;

            shownLogs.RemoveAt(i);
            if (currentUuid == uuid)
            {
                if (shownLogs.Count == 0)
                    SelectionChange(-1);
                else
                    SelectionChange(shownLogs[Math.Clamp(i, 0, shownLogs.Count - 1)].Key);
            }
            else
            {
                Reload();
                SelectionChanged?.Invoke(currentUuid);
            }

            break;
        }
    }

    public void Reload()
    {
        if (_panList is null)
            return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Reload);
            return;
        }

        _panList.Children.Clear();
        if (shownLogs.Count == 0)
        {
            EmptyRequested?.Invoke();
            return;
        }

        _panList.Children.Add(_CreateSectionTitle(Lang.Text("LogPage.Left.InstancesTitle")));
        foreach (var pair in shownLogs)
            _panList.Children.Add(_CreateLogItem(pair.Key, pair.Value));

        if (States.Hint.MaxGameLog)
            return;
        States.Hint.MaxGameLog = true;
        HintService.Show(Hints.Info(Lang.Text("LogPage.MaxLines.Hint", 500)));
    }

    private static TextBlock _CreateSectionTitle(string text)
    {
        var title = new TextBlock
        {
            Text = text
        };
        title.Classes.Add("SectionTitle");
        return title;
    }

    private Control _CreateLogItem(int uuid, GameWatcher.Watcher watcher)
    {
        var radio = new RadioButton
        {
            GroupName = "LogLeftItems",
            IsChecked = uuid == currentUuid,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        radio.IsCheckedChanged += (_, _) =>
        {
            if (radio.IsChecked == true && uuid != currentUuid)
                SelectionChange(uuid);
        };

        var title = new TextBlock
        {
            Text = watcher.version.Name
        };
        title.Classes.Add("RowTitle");
        var info = new TextBlock
        {
            Text = GetItemInfo(watcher)
        };
        info.Classes.Add("RowInfo");
        var textPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        textPanel.Children.Add(title);
        textPanel.Children.Add(info);

        var remove = new Button
        {
            Content = "\u00D7"
        };
        remove.Classes.Add("PageIconButton");
        ToolTip.SetTip(remove, Lang.Text("Common.Action.Delete"));
        remove.Click += (_, _) => RemoveItem(uuid);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };
        Grid.SetColumn(radio, 0);
        Grid.SetColumn(textPanel, 1);
        Grid.SetColumn(remove, 2);
        grid.Children.Add(radio);
        grid.Children.Add(textPanel);
        grid.Children.Add(remove);

        var border = new Border
        {
            Child = grid
        };
        border.Classes.Add("PageListItem");
        border.Classes.Set("Checked", uuid == currentUuid);
        return border;
    }

    private static string GetItemInfo(GameWatcher.Watcher watcher)
    {
        return $"{watcher.version.Info} - {Lang.Date(watcher.gameProcess.StartTime, "T")}";
    }
}