// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Tasks.Views;

public partial class PageSpeedLeft : MyPageLeft
{
    private readonly TextBlock _progress;
    private readonly TextBlock _speed;
    private readonly TextBlock _file;
    private readonly TextBlock _thread;

    public PageSpeedLeft()
    {
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this;
        _progress = Required<TextBlock>("LabProgress");
        _speed = Required<TextBlock>("LabSpeed");
        _file = Required<TextBlock>("LabFile");
        _thread = Required<TextBlock>("LabThread");
        SetIdle();
    }

    public void SetIdle()
    {
        UpdateSummary(new TaskManagerSummary(
            1d,
            0,
            0,
            0,
            Math.Max(1, Environment.ProcessorCount)));
    }

    public void UpdateSummary(TaskManagerSummary summary)
    {
        RunOnUiThread(() =>
        {
            _progress.Text = TaskManagerFormatting.Percent(summary.Progress, twoDecimals: true);
            _speed.Text = TaskManagerFormatting.Speed(summary.SpeedBytesPerSecond);
            _file.Text = Math.Max(0, summary.RemainingFiles).ToString(CultureInfo.CurrentCulture);
            _thread.Text = Math.Max(0, summary.ActiveThreads).ToString(CultureInfo.CurrentCulture) +
                           " / " +
                           Math.Max(1, summary.ThreadLimit).ToString(CultureInfo.CurrentCulture);
        });
    }

    private T Required<T>(string name)
        where T : Control
    {
        return this.FindControl<T>(name)
               ?? throw new InvalidOperationException($"缺少任务管理左栏控件：{name}");
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
}
