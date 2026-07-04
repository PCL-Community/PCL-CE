// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Application.Downloads;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Downloads.Views;

public partial class PageDownloadProgress : MyPageRight
{
    private TextBlock? _title;
    private TextBlock? _stage;
    private TextBlock? _percent;
    private Grid? _progressBar;
    private MyButton? _cancelButton;
    private MyButton? _installButton;
    private MyButton? _launchButton;

    public PageDownloadProgress()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        EnsureProgressCard();
        SetText("暂无下载任务", "选择一个 Minecraft 版本后，安装进度会显示在这里。");
        SetProgress(0d);
        SetActions(ProgressActionState.Idle);
    }

    public event EventHandler? CancelRequested;

    public event EventHandler? InstallPageRequested;

    public event EventHandler? LaunchPageRequested;

    public void Begin(string versionId)
    {
        RunOnUiThread(() =>
        {
            EnsureProgressCard();
            SetText("正在安装 " + versionId, "准备安装文件。");
            SetProgress(0d);
            SetActions(ProgressActionState.Running);
        });
    }

    public void Update(MinecraftInstallProgress progress)
    {
        RunOnUiThread(() =>
        {
            EnsureProgressCard();
            string detail = string.IsNullOrWhiteSpace(progress.Detail) ? string.Empty : " · " + progress.Detail;
            SetText(null, progress.Stage + detail);
            SetProgress(progress.Progress);
        });
    }

    public void Complete(string versionId)
    {
        RunOnUiThread(() =>
        {
            EnsureProgressCard();
            SetText(versionId + " 安装完成", "你可以回到启动页选择并启动这个版本。");
            SetProgress(1d);
            SetActions(ProgressActionState.Completed);
        });
    }

    public void Fail(string message)
    {
        RunOnUiThread(() =>
        {
            EnsureProgressCard();
            SetText("安装失败", message);
            SetActions(ProgressActionState.Failed);
        });
    }

    private void EnsureProgressCard()
    {
        if (_title is not null)
            return;

        if (this.FindControl<StackPanel>("PanMain") is not { } panel)
            return;

        MyCard card = new()
        {
            Title = "下载进度",
            Margin = new Thickness(0, 0, 0, 15)
        };
        StackPanel stack = new()
        {
            Margin = new Thickness(25, 38, 23, 16)
        };
        _title = new TextBlock
        {
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };
        _stage = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        Grid progressRoot = CreateProgressBar(out _progressBar);
        _percent = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            FontSize = 12,
            Foreground = LegacyResourceResolver.Brush(card, "ColorBrushGray2", "#737373")
        };
        StackPanel actions = new()
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Spacing = 8
        };
        _installButton = new MyButton
        {
            Text = "继续选择版本",
            MinWidth = 120,
            Height = 35,
            Padding = new Thickness(13, 0)
        };
        _installButton.Click += (_, _) => InstallPageRequested?.Invoke(this, EventArgs.Empty);
        _cancelButton = new MyButton
        {
            Text = "取消任务",
            MinWidth = 96,
            Height = 35,
            Padding = new Thickness(13, 0)
        };
        _cancelButton.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        _launchButton = new MyButton
        {
            Text = "回到启动页",
            MinWidth = 110,
            Height = 35,
            Padding = new Thickness(13, 0),
            ColorType = MyButton.ColorState.Highlight
        };
        _launchButton.Click += (_, _) => LaunchPageRequested?.Invoke(this, EventArgs.Empty);
        actions.Children.Add(_installButton);
        actions.Children.Add(_cancelButton);
        actions.Children.Add(_launchButton);

        stack.Children.Add(_title);
        stack.Children.Add(_stage);
        stack.Children.Add(progressRoot);
        stack.Children.Add(_percent);
        stack.Children.Add(actions);
        card.Children.Add(stack);
        panel.Children.Add(card);
    }

    private static Grid CreateProgressBar(out Grid bar)
    {
        Grid root = new()
        {
            Height = 7,
            Margin = new Thickness(0, 16, 0, 0)
        };
        root.Background = LegacyResourceResolver.Brush(root, "ColorBrush6", "#d5e6fd");
        bar = new Grid
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        bar.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0, GridUnitType.Star)));
        bar.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        Border fill = new()
        {
            CornerRadius = new CornerRadius(3.5)
        };
        fill.Background = LegacyResourceResolver.Brush(fill, "ColorBrush3", "#1370f3");
        Grid.SetColumn(fill, 0);
        bar.Children.Add(fill);
        root.Children.Add(bar);
        return root;
    }

    private void SetText(string? title, string stage)
    {
        if (title is not null && _title is not null)
            _title.Text = title;
        if (_stage is not null)
            _stage.Text = stage;
    }

    private void SetProgress(double value)
    {
        value = Math.Clamp(value, 0d, 1d);
        if (_progressBar is { ColumnDefinitions.Count: >= 2 })
        {
            _progressBar.ColumnDefinitions[0].Width = new GridLength(value, GridUnitType.Star);
            _progressBar.ColumnDefinitions[1].Width = new GridLength(1d - value, GridUnitType.Star);
        }

        if (_percent is not null)
            _percent.Text = value.ToString("P0", System.Globalization.CultureInfo.CurrentCulture);
    }

    private void SetActions(ProgressActionState state)
    {
        if (_installButton is not null)
            _installButton.IsVisible = state is ProgressActionState.Idle or ProgressActionState.Completed or ProgressActionState.Failed;
        if (_cancelButton is not null)
            _cancelButton.IsVisible = state == ProgressActionState.Running;
        if (_launchButton is not null)
            _launchButton.IsVisible = state == ProgressActionState.Completed;
    }

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.InvokeAsync(action).GetTask().GetAwaiter().GetResult();
    }

    private enum ProgressActionState
    {
        Idle,
        Running,
        Completed,
        Failed
    }
}
