// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupGameManage : MyPageRight, ISettingsPageInteractionSource
{
    public PageSetupGameManage()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        SliderLoad();
        LauncherSettingsPageBinder.Attach(this);
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    private void CheckBoxChange(object sender, bool user)
    {
    }

    private void ComboChange(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void SliderChange(object sender, bool user)
    {
    }

    private void SliderDownloadThread_PreviewChange(object sender, RouteEventArgs e)
    {
        if (!e.RaiseByMouse)
            return;

        int value = sender is MySlider slider ? slider.Value : SliderDownloadThread?.Value ?? 0;
        if (value < 100)
            return;

        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        if (settings.GetBooleanOption(LauncherSettingKeys.HintDownloadThread))
            return;

        settings.SetBooleanOption(LauncherSettingKeys.HintDownloadThread, true);
        LauncherSettingsPageBinder.SaveSettings(settings);
        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs(
                "下载线程过高",
                "下载线程数过高可能导致下载源限速、连接失败，甚至影响本机网络。通常保持默认值就足够了；只有在网络环境和下载源都稳定时再继续提高。",
                "知道了"));
    }

    private void SliderLoad()
    {
        if (this.FindControl<MySlider>("SliderDownloadThread") is { } downloadThread)
            downloadThread.getHintText = value => value + 1;

        if (this.FindControl<MySlider>("SliderDownloadSpeed") is { } downloadSpeed)
            downloadSpeed.getHintText = FormatSpeedLimit;
    }

    private static string FormatSpeedLimit(int value)
    {
        return value switch
        {
            <= 14 => FormattableString.Invariant($"{(value + 1) * 0.1d:N1} M/s"),
            <= 31 => FormattableString.Invariant($"{(value - 11) * 0.5d:N1} M/s"),
            <= 41 => FormattableString.Invariant($"{value - 21:N0} M/s"),
            _ => "不限速"
        };
    }
}
