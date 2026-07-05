// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

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
                _panel.Children.Add(card.Card);
            }

            card.State = snapshot.State;
            card.Card.Title = snapshot.Title;
            card.Stage.Text = snapshot.Stage;
            card.Detail.Text = BuildDetail(snapshot);
            card.Progress.Text = ToStatusText(snapshot);
            card.Progress.Foreground = StatusBrush(snapshot.State);
            card.CancelButton.IsVisible = snapshot.State is TaskManagerTaskState.Waiting or TaskManagerTaskState.Running;
            card.Error.Text = snapshot.ErrorMessage ?? string.Empty;
            card.Error.IsVisible = snapshot.State is TaskManagerTaskState.Failed or TaskManagerTaskState.Canceled;
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
            },
            RowDefinitions =
            {
                new RowDefinition(26d, GridUnitType.Pixel),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };

        TextBlock progress = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13d,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            Width = 48d
        };
        Grid.SetColumn(progress, 0);
        Grid.SetRow(progress, 0);
        content.Children.Add(progress);

        TextBlock stage = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13d,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("ColorBrush1", "#343d4a")
        };
        Grid.SetColumn(stage, 1);
        Grid.SetRow(stage, 0);
        content.Children.Add(stage);

        TextBlock detail = new()
        {
            Margin = new Thickness(0d, 2d, 0d, 0d),
            FontSize = 12d,
            Opacity = 0.65d,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ColorBrush1", "#343d4a")
        };
        Grid.SetColumn(detail, 1);
        Grid.SetRow(detail, 1);
        content.Children.Add(detail);

        TextBlock error = new()
        {
            Margin = new Thickness(0d, 6d, 0d, 0d),
            FontSize = 12d,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ColorBrushRedLight", "#ff6b6b"),
            IsVisible = false
        };
        Grid.SetColumn(error, 1);
        Grid.SetRow(error, 2);
        content.Children.Add(error);

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
        return new TaskCardView(card, progress, stage, detail, error, cancelButton, snapshot.State);
    }

    private static string BuildDetail(TaskManagerEntrySnapshot snapshot)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
            parts.Add(snapshot.Detail);
        if (snapshot.TotalFiles > 0)
            parts.Add($"{Math.Clamp(snapshot.CompletedFiles, 0, snapshot.TotalFiles)} / {snapshot.TotalFiles} 个文件");
        if (snapshot.SpeedBytesPerSecond > 0)
            parts.Add(TaskManagerFormatting.Speed(snapshot.SpeedBytesPerSecond));

        return parts.Count == 0 ? "正在等待任务更新" : string.Join(" · ", parts);
    }

    private static string ToStatusText(TaskManagerEntrySnapshot snapshot) =>
        snapshot.State switch
        {
            TaskManagerTaskState.Waiting => "...",
            TaskManagerTaskState.Running => TaskManagerFormatting.Percent(snapshot.Progress),
            TaskManagerTaskState.Finished => "√",
            TaskManagerTaskState.Failed => "×",
            TaskManagerTaskState.Canceled => "×",
            _ => string.Empty
        };

    private IBrush StatusBrush(TaskManagerTaskState state) =>
        state switch
        {
            TaskManagerTaskState.Failed or TaskManagerTaskState.Canceled => Brush("ColorBrushRedLight", "#ff6b6b"),
            TaskManagerTaskState.Finished => Brush("ColorBrush3", "#1370f3"),
            _ => Brush("ColorBrush1", "#343d4a")
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
        TextBlock progress,
        TextBlock stage,
        TextBlock detail,
        TextBlock error,
        MyIconButton cancelButton,
        TaskManagerTaskState state)
    {
        public MyCard Card { get; } = card;
        public TextBlock Progress { get; } = progress;
        public TextBlock Stage { get; } = stage;
        public TextBlock Detail { get; } = detail;
        public TextBlock Error { get; } = error;
        public MyIconButton CancelButton { get; } = cancelButton;
        public TaskManagerTaskState State { get; set; } = state;
    }
}
