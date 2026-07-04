// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Settings.Views;

public interface IRefreshableSettingsPage
{
    void RefreshPage();
}

public interface ISettingsPageInteractionSource
{
    event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;
}

public sealed class SettingsPathRequestedEventArgs(string path) : EventArgs
{
    public string Path { get; } = path;
}

public sealed class SettingsUrlRequestedEventArgs(string url) : EventArgs
{
    public string Url { get; } = url;
}

public sealed class SettingsMessageRequestedEventArgs(
    string title,
    string message,
    string primaryButton = "确定") : EventArgs
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string PrimaryButton { get; } = primaryButton;
}

public sealed class SettingsConfirmRequestedEventArgs(
    string title,
    string message,
    Action<bool> complete,
    string primaryButton = "确定",
    string secondaryButton = "取消",
    bool isWarn = false) : EventArgs
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public Action<bool> Complete { get; } = complete;

    public string PrimaryButton { get; } = primaryButton;

    public string SecondaryButton { get; } = secondaryButton;

    public bool IsWarn { get; } = isWarn;
}
