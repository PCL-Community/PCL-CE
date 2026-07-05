// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Features.Tasks.Views;

public partial class PageSpeedRight : MyPageRight
{
    private readonly StackPanel _panel;
    private readonly Dictionary<string, TaskCardView> _cards = [];

    public PageSpeedRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = Required<MyScrollViewer>("PanBack");
        _panel = Required<StackPanel>("PanMain");
    }

    public event EventHandler<TaskManagerTaskEventArgs>? CancelRequested;

    public int TaskCount => _cards.Count;

    public bool HasActiveTasks => _cards.Values.Any(static card =>
        card.State is TaskManagerTaskState.Waiting or TaskManagerTaskState.Running);

    public void UpsertTask(TaskManagerEntrySnapshot snapshot)
    {
        RunOnUiThread(() =>
        {
            if (!_cards.TryGetValue(snapshot.TaskId, out TaskCardView? card))
            {
                card = CreateTaskCard(snapshot);
                _cards.Add(snapshot.TaskId, card);
                _panel.Children.Insert(0, card.Card);
            }

            card.State = snapshot.State;
            card.Card.Title = snapshot.Title;
            card.CancelButton.IsVisible = snapshot.State is TaskManagerTaskState.Waiting or TaskManagerTaskState.Running;
            UpdateContentRows(card, snapshot);
        });
    }

    public void RemoveTask(string taskId)
    {
        RunOnUiThread(() =>
        {
            if (!_cards.Remove(taskId, out TaskCardView? card))
                return;

            _panel.Children.Remove(card.Card);
        });
    }

    public void Clear()
    {
        RunOnUiThread(() =>
        {
            _cards.Clear();
            _panel.Children.Clear();
        });
    }

    private TaskCardView CreateTaskCard(TaskManagerEntrySnapshot snapshot)
    {
        MyCard card = new()
        {
            Title = snapshot.Title,
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };

        Grid content = new()
        {
            Margin = new Thickness(14d, 40d, 15d, 10d),
            ColumnDefinitions =
            {
                new ColumnDefinition(50d, GridUnitType.Pixel),
                new ColumnDefinition(1d, GridUnitType.Star)
            }
        };

        MyIconButton cancelButton = new()
        {
            Width = 30d,
            Height = 30d,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, 5d, 7d, 0d),
            Opacity = 0.6d,
            SvgIcon = "lucide/x",
            Theme = MyIconButton.Themes.Black,
            ToolTip = "取消任务"
        };
        cancelButton.Click += (_, _) => CancelRequested?.Invoke(this, new TaskManagerTaskEventArgs(snapshot.TaskId));

        card.Children.Add(content);
        card.Children.Add(cancelButton);
        TaskCardView taskCard = new(card, content, cancelButton, snapshot.State);
        UpdateContentRows(taskCard, snapshot);
        return taskCard;
    }

    private void UpdateContentRows(TaskCardView card, TaskManagerEntrySnapshot snapshot)
    {
        card.Content.RowDefinitions.Clear();
        card.Content.Children.Clear();
        if (snapshot.State is TaskManagerTaskState.Failed or TaskManagerTaskState.Canceled)
        {
            AddErrorRow(card.Content, snapshot);
            return;
        }

        IReadOnlyList<TaskManagerSubTaskSnapshot> rows = CreateTaskRows(snapshot);
        for (int i = 0; i < rows.Count; i++)
        {
            TaskManagerSubTaskSnapshot row = rows[i];
            AddTaskRow(
                card.Content,
                i,
                CreateStatusIndicator(row.State, row.Progress),
                BuildTaskRowText(row));
        }
    }

    private static void AddTaskRow(Grid content, int row, Control status, string text)
    {
        content.RowDefinitions.Add(new RowDefinition(26d, GridUnitType.Pixel));
        Grid.SetColumn(status, 0);
        Grid.SetRow(status, row);
        content.Children.Add(status);

        TextBlock name = new()
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13d,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(name, 1);
        Grid.SetRow(name, row);
        content.Children.Add(name);
    }

    private void AddErrorRow(Grid content, TaskManagerEntrySnapshot snapshot)
    {
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Control status = CreateStatusIndicator(snapshot.State, snapshot.Progress);
        Grid.SetColumn(status, 0);
        Grid.SetRow(status, 0);
        content.Children.Add(status);

        TextBlock error = new()
        {
            Text = snapshot.ErrorMessage ?? snapshot.Detail,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, 0d, 0d, 5d)
        };
        ToolTip.SetTip(error, "单击可复制错误信息");
        error.PointerPressed += (_, _) =>
        {
            if (TopLevel.GetTopLevel(error)?.Clipboard is { } clipboard)
                _ = clipboard.SetTextAsync(error.Text ?? string.Empty);
        };
        Grid.SetColumn(error, 1);
        Grid.SetRow(error, 0);
        content.Children.Add(error);
    }

    private Control CreateStatusIndicator(TaskManagerTaskState state, double progress) =>
        state switch
        {
            TaskManagerTaskState.Waiting => CreateWaitingPath(),
            TaskManagerTaskState.Running => new TextBlock
            {
                Text = ToStatusText(state, progress),
                Tag = "Loading",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = Brush("ColorBrush3", "#1370f3")
            },
            TaskManagerTaskState.Finished => CreateFinishedPath(),
            TaskManagerTaskState.Failed or TaskManagerTaskState.Canceled => CreateFailedPath(),
            _ => new TextBlock()
        };

    private PathShape CreateWaitingPath() =>
        new()
        {
            Tag = "Waiting",
            Stretch = Stretch.Uniform,
            Data = Geometry.Parse("F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z"),
            Width = 18d,
            Height = 6d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, 7d, 0d, 0d),
            Fill = Brush("ColorBrush3", "#1370f3")
        };

    private PathShape CreateFinishedPath() =>
        new()
        {
            Tag = "Finished",
            Stretch = Stretch.Uniform,
            Data = Geometry.Parse("F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z"),
            Width = 15d,
            Height = 16d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, 3d, 0d, 0d),
            Fill = Brush("ColorBrush3", "#1370f3")
        };

    private PathShape CreateFailedPath() =>
        new()
        {
            Tag = "Failed",
            Stretch = Stretch.Uniform,
            Data = Geometry.Parse("F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z"),
            Width = 15d,
            Height = 15d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, 1d, 0d, 0d),
            Fill = Brush("ColorBrush3", "#1370f3")
        };

    private static string BuildTaskRowText(TaskManagerSubTaskSnapshot snapshot)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(snapshot.Name))
            parts.Add(snapshot.Name);
        if (!string.IsNullOrWhiteSpace(snapshot.Detail) &&
            !string.Equals(snapshot.Detail, snapshot.Name, StringComparison.Ordinal))
            parts.Add(snapshot.Detail);

        return parts.Count == 0 ? "正在等待任务更新" : string.Join(" · ", parts);
    }

    private static IReadOnlyList<TaskManagerSubTaskSnapshot> CreateTaskRows(TaskManagerEntrySnapshot snapshot)
    {
        if (snapshot.Steps is { Count: > 0 } steps)
            return steps;

        return
        [
            new TaskManagerSubTaskSnapshot(
                snapshot.Stage,
                BuildTaskRowDetail(snapshot),
                snapshot.Progress,
                snapshot.State)
        ];
    }

    private static string BuildTaskRowDetail(TaskManagerEntrySnapshot snapshot)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(snapshot.Detail) &&
            !string.Equals(snapshot.Detail, snapshot.Stage, StringComparison.Ordinal))
            parts.Add(snapshot.Detail);
        if (snapshot.TotalFiles > 0)
            parts.Add($"{Math.Clamp(snapshot.CompletedFiles, 0, snapshot.TotalFiles)} / {snapshot.TotalFiles} 个文件");
        if (snapshot.SpeedBytesPerSecond > 0)
            parts.Add(TaskManagerFormatting.Speed(snapshot.SpeedBytesPerSecond));

        return string.Join(" · ", parts);
    }

    private static string ToStatusText(TaskManagerTaskState state, double progress) =>
        state switch
        {
            TaskManagerTaskState.Waiting => "...",
            TaskManagerTaskState.Running => TaskManagerFormatting.Percent(progress),
            TaskManagerTaskState.Finished => "√",
            TaskManagerTaskState.Failed => "×",
            TaskManagerTaskState.Canceled => "×",
            _ => string.Empty
        };

    private IBrush Brush(string key, string fallback) =>
        LegacyResourceResolver.Brush(this, key, fallback);

    private T Required<T>(string name)
        where T : Control
    {
        return this.FindControl<T>(name)
               ?? throw new InvalidOperationException($"缺少任务管理右栏控件：{name}");
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(action).GetTask().GetAwaiter().GetResult();
    }

    private sealed class TaskCardView(
        MyCard card,
        Grid content,
        MyIconButton cancelButton,
        TaskManagerTaskState state)
    {
        public MyCard Card { get; } = card;
        public Grid Content { get; } = content;
        public MyIconButton CancelButton { get; } = cancelButton;
        public TaskManagerTaskState State { get; set; } = state;
    }
}
