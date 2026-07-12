// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Diagnostics;

namespace PCL.Desktop.Views;

public sealed partial class CrashReportWindow : Window
{
    private readonly string _report;
    private readonly string _issuesUrl;
    private readonly string _issuesNewUrl;
    private readonly Exception _exception;

    public CrashReportWindow()
        : this(new InvalidOperationException("unknown"), "unknown", canContinue: true,
            UnhandledExceptionGuard.IssuesUrl, UnhandledExceptionGuard.IssuesNewUrl)
    {
    }

    public CrashReportWindow(
        Exception exception,
        string report,
        bool canContinue,
        string issuesUrl,
        string issuesNewUrl)
    {
        _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        _report = report ?? string.Empty;
        _issuesUrl = issuesUrl;
        _issuesNewUrl = issuesNewUrl;
        AvaloniaXamlLoader.Load(this);

        if (this.FindControl<TextBox>("TxtReport") is { } box)
            box.Text = _report;

        if (this.FindControl<Button>("BtnContinue") is { } cont)
            cont.IsVisible = canContinue;

        Title = "PCL N — " + ShortTypeName(_exception);
    }

    private async void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard is not null)
            {
                await Clipboard.SetTextAsync(_report).ConfigureAwait(true);
                if (sender is Button button)
                {
                    string original = button.Content?.ToString() ?? "复制报告";
                    button.Content = "已复制";
                    await Task.Delay(1200).ConfigureAwait(true);
                    button.Content = original;
                }
            }
        }
        catch
        {
            // ignore clipboard failures
        }
    }

    private void BtnOpenIssue_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Prefer chooser page; fall back to prefilled new issue if needed.
            string title = Uri.EscapeDataString("[Crash] " + ShortTypeName(_exception) + ": " + Truncate(_exception.Message, 80));
            string body = Uri.EscapeDataString(Truncate(_report, 3500));
            string url = _issuesNewUrl + "?title=" + title + "&body=" + body;
            if (url.Length > 1800)
                url = _issuesUrl;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DesktopFileLog.Write("[Crash] 打开 Issue 页面失败：" + ex.Message);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _issuesUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // ignore
            }
        }
    }

    private void BtnContinue_Click(object? sender, RoutedEventArgs e) => Close();

    private void BtnExit_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(1);
                return;
            }
        }
        catch
        {
            // ignore
        }

        Environment.Exit(1);
    }

    private static string ShortTypeName(Exception ex)
    {
        string? name = ex.GetType().Name;
        return string.IsNullOrWhiteSpace(name) ? "Exception" : name;
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text;
        return text[..(max - 1)] + "…";
    }
}
