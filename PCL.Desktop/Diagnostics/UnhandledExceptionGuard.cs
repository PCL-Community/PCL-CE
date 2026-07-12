// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PCL.Desktop.Views;

namespace PCL.Desktop.Diagnostics;

/// <summary>
/// Captures unhandled exceptions, writes a crash log, and prompts the user to open a GitHub Issue.
/// </summary>
internal static class UnhandledExceptionGuard
{
    public const string IssuesUrl = "https://github.com/MuXue1230-owo/PCL-N/issues/new/choose";
    public const string IssuesNewUrl = "https://github.com/MuXue1230-owo/PCL-N/issues/new";

    private static int _handling;
    private static int _dialogShown;
    private static bool _dispatcherAttached;

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>Call after Avalonia UI thread is available.</summary>
    public static void AttachUiDispatcher()
    {
        if (_dispatcherAttached)
            return;
        _dispatcherAttached = true;

        try
        {
            Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        }
        catch (Exception ex)
        {
            DesktopFileLog.Write("[Crash] 无法挂接 UI 线程异常处理：" + ex.Message);
        }
    }

    public static string BuildReport(Exception exception, string source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "unknown";

        StringBuilder sb = new();
        sb.AppendLine("### 环境");
        sb.AppendLine("- PCL N: " + version);
        sb.AppendLine("- OS: " + Environment.OSVersion.VersionString);
        sb.AppendLine("- Runtime: " + Environment.Version);
        sb.AppendLine("- Process: " + (Environment.ProcessPath ?? "(unknown)"));
        sb.AppendLine("- Source: " + source);
        sb.AppendLine("- Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine();
        sb.AppendLine("### 异常");
        sb.AppendLine("```");
        sb.AppendLine(Flatten(exception));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### 复现步骤");
        sb.AppendLine("1. ");
        sb.AppendLine("2. ");
        sb.AppendLine();
        sb.AppendLine("### 期望行为");
        sb.AppendLine();
        return sb.ToString();
    }

    public static void Report(Exception exception, string source, bool canContinue)
    {
        if (exception is null)
            return;

        // Avoid re-entrancy when the dialog itself throws.
        if (Interlocked.Exchange(ref _handling, 1) == 1)
        {
            try
            {
                DesktopFileLog.Write("[Crash] 二次未处理异常：" + Flatten(exception).ReplaceLineEndings(" | "));
            }
            catch
            {
                // ignore
            }

            return;
        }

        try
        {
            string report = BuildReport(exception, source);
            try
            {
                DesktopFileLog.Initialize();
                DesktopFileLog.Write("[Crash] " + source + " → " + Flatten(exception).ReplaceLineEndings(" | "));
                WriteCrashDump(report);
            }
            catch
            {
                // logging must never throw out of the guard
            }

            ShowCrashUi(exception, report, canContinue);
        }
        finally
        {
            Interlocked.Exchange(ref _handling, 0);
        }
    }

    private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception
            ?? new InvalidOperationException("未知未处理异常：" + (e.ExceptionObject?.ToString() ?? "(null)"));
        // AppDomain unhandled is usually fatal — do not offer "continue".
        Report(ex, "AppDomain.UnhandledException", canContinue: false);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Report(e.Exception.Flatten().InnerException ?? e.Exception, "TaskScheduler.UnobservedTaskException", canContinue: true);
    }

    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Report(e.Exception, "Dispatcher.UIThread.UnhandledException", canContinue: true);
    }

    private static void ShowCrashUi(Exception exception, string report, bool canContinue)
    {
        // Only one modal crash dialog at a time.
        if (Interlocked.Exchange(ref _dialogShown, 1) == 1)
            return;

        void Show()
        {
            try
            {
                CrashReportWindow window = new(exception, report, canContinue, IssuesUrl, IssuesNewUrl);
                Window? owner = TryGetMainWindow();
                if (owner is not null)
                {
                    window.ShowDialog(owner).ContinueWith(
                        _ => Interlocked.Exchange(ref _dialogShown, 0),
                        TaskScheduler.Default);
                }
                else
                {
                    window.Closed += (_, _) => Interlocked.Exchange(ref _dialogShown, 0);
                    window.Show();
                }
            }
            catch (Exception showEx)
            {
                Interlocked.Exchange(ref _dialogShown, 0);
                DesktopFileLog.Write("[Crash] 无法显示崩溃窗口：" + showEx.Message);
                FallbackConsole(exception, report);
            }
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                Show();
            else
                Dispatcher.UIThread.Post(Show, DispatcherPriority.Send);
        }
        catch
        {
            Interlocked.Exchange(ref _dialogShown, 0);
            FallbackConsole(exception, report);
        }
    }

    private static Window? TryGetMainWindow()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static void FallbackConsole(Exception exception, string report)
    {
        try
        {
            Console.Error.WriteLine("PCL N 发生未捕获异常。请提交 Issue：");
            Console.Error.WriteLine(IssuesUrl);
            Console.Error.WriteLine(report);
            Debug.WriteLine(exception);
        }
        catch
        {
            // ignore
        }
    }

    private static void WriteCrashDump(string report)
    {
        try
        {
            string dir = Path.Combine(Path.GetDirectoryName(DesktopFileLog.CurrentLogPath) ?? AppContext.BaseDirectory, "Crashes");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            File.WriteAllText(path, report, Encoding.UTF8);
            DesktopFileLog.Write("[Crash] 崩溃报告已写入 " + path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            DesktopFileLog.Write("[Crash] 写入崩溃报告失败：" + ex.Message);
        }
    }

    private static string Flatten(Exception exception)
    {
        StringBuilder sb = new();
        Exception? current = exception;
        int depth = 0;
        while (current is not null && depth < 8)
        {
            if (depth > 0)
                sb.AppendLine("--- Inner ---");
            sb.Append(current.GetType().FullName);
            sb.Append(": ");
            sb.AppendLine(current.Message);
            if (!string.IsNullOrWhiteSpace(current.StackTrace))
                sb.AppendLine(current.StackTrace);
            current = current.InnerException;
            depth++;
        }

        return sb.ToString().TrimEnd();
    }
}
