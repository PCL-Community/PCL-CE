// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views.Launch;

public sealed record AuthLoginRequest(string Server, string Username, string Password);

public partial class PageLoginAuth : Grid, PageLaunchLeft.ILoginPage
{
    private static readonly Dictionary<string, string> PredefinedAuthServers = new(StringComparer.Ordinal)
    {
        ["LittleSkin"] = "https://littleskin.cn/api/yggdrasil",
        ["自定义"] = string.Empty
    };

    private bool _isLoginRunning;
    private bool _isRegisterMode = true;

    public PageLoginAuth()
    {
        AvaloniaXamlLoader.Load(this);
        if (this.FindControl<MyComboBox>("TextServer") is { } server)
            server.TextChanged += TextServerTextChanged;
        if (this.FindControl<MyTextBox>("TextName") is { } name)
            name.TextChanged += (_, _) => RefreshRegisterButtonText();
        AttachedToVisualTree += (_, _) => Reload();
    }

    public event EventHandler? BackRequested;

    public event EventHandler<AuthLoginRequest>? LoginRequested;

    public event EventHandler<string>? ValidationFailed;

    public event EventHandler<bool>? RegisterLinkRequested;

    public void Reload()
    {
        if (this.FindControl<MyComboBox>("TextServer") is { } server)
        {
            server.Items.Clear();
            foreach (string name in PredefinedAuthServers.Keys)
                server.Items.Add(new MyComboBoxItem { Content = name });
        }
        RefreshRegisterButtonText();
    }

    public void FinishLogin()
    {
        _isLoginRunning = false;
        SetLoginEnabled(true);
        if (this.FindControl<MyButton>("BtnLogin") is { } login)
            login.Text = "登录";
    }

    private void BtnBackClick(object? sender, EventArgs e)
    {
        if (_isLoginRunning)
            return;

        if (this.FindControl<MyComboBox>("TextServer") is { } server)
            server.Text = string.Empty;
        if (this.FindControl<MyTextBox>("TextName") is { } name)
            name.Text = string.Empty;
        if (this.FindControl<MyTextBox>("TextPass") is { } password)
            password.Text = string.Empty;
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnLoginClick(object? sender, EventArgs e)
    {
        if (_isLoginRunning)
            return;

        string server = this.FindControl<MyComboBox>("TextServer")?.Text?.Trim() ?? string.Empty;
        string username = this.FindControl<MyTextBox>("TextName")?.Text?.Trim() ?? string.Empty;
        string password = this.FindControl<MyTextBox>("TextPass")?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ValidationFailed?.Invoke(this, "请填写认证服务器、邮箱和密码。");
            return;
        }

        if (!Uri.TryCreate(server, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ValidationFailed?.Invoke(this, "认证服务器地址无效。");
            return;
        }

        _isLoginRunning = true;
        SetLoginEnabled(false);
        if (this.FindControl<MyButton>("BtnLogin") is { } login)
            login.Text = "0 %";
        LoginRequested?.Invoke(this, new AuthLoginRequest(server, username, password));
    }

    private void BtnLinkClick(object? sender, RoutedEventArgs e) =>
        RegisterLinkRequested?.Invoke(this, _isRegisterMode);

    private void TextServerTextChanged(object sender, TextChangedEventArgs? e)
    {
        if (sender is not MyComboBox comboBox)
            return;

        if (PredefinedAuthServers.TryGetValue(comboBox.Text, out string? server) && !string.IsNullOrEmpty(server))
            comboBox.Text = server;
    }

    private void RefreshRegisterButtonText()
    {
        string name = this.FindControl<MyTextBox>("TextName")?.Text ?? string.Empty;
        _isRegisterMode = string.IsNullOrWhiteSpace(name);
        if (this.FindControl<MyTextButton>("BtnLink") is { } link)
            link.Text = _isRegisterMode ? "注册" : "忘记密码";
    }

    private void SetLoginEnabled(bool enabled)
    {
        if (this.FindControl<MyButton>("BtnLogin") is { } login)
            login.IsEnabled = enabled;
        if (this.FindControl<MyButton>("BtnBack") is { } back)
            back.IsEnabled = enabled;
    }
}
