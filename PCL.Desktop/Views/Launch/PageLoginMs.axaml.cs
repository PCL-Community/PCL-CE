// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views.Launch;

public partial class PageLoginMs : StackPanel, PageLaunchLeft.ILoginPage
{
    public PageLoginMs()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public bool IsLoggingIn { get; private set; }

    public event EventHandler? BackRequested;

    public event EventHandler? LoginRequested;

    public event EventHandler? PurchaseRequested;

    public event EventHandler? WebsiteRequested;

    public void Reload()
    {
        if (!IsLoggingIn)
            ResetLoginButton();
    }

    public void StartLogin()
    {
        IsLoggingIn = true;
        if (this.FindControl<MyButton>("BtnLogin") is { } login)
        {
            login.IsEnabled = false;
            login.Text = "0 %";
        }
        if (this.FindControl<MyTextButton>("BtnBack") is { } back)
            back.IsVisible = false;
    }

    public void UpdateProgress(double progress)
    {
        if (this.FindControl<MyButton>("BtnLogin") is { } login)
            login.Text = Math.Clamp(progress, 0d, 1d).ToString("P0", System.Globalization.CultureInfo.CurrentCulture);
    }

    public void FinishLogin()
    {
        IsLoggingIn = false;
        ResetLoginButton();
    }

    private void BtnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private void BtnLoginClick(object? sender, EventArgs e)
    {
        if (IsLoggingIn)
            return;

        StartLogin();
        LoginRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnPurchaseClick(object? sender, RoutedEventArgs e) => PurchaseRequested?.Invoke(this, EventArgs.Empty);

    private void BtnWebsiteClick(object? sender, RoutedEventArgs e) => WebsiteRequested?.Invoke(this, EventArgs.Empty);

    private void ResetLoginButton()
    {
        if (this.FindControl<MyButton>("BtnLogin") is { } login)
        {
            login.IsEnabled = true;
            login.Text = "开始登录";
        }
        if (this.FindControl<MyTextButton>("BtnBack") is { } back)
            back.IsVisible = true;
    }
}
