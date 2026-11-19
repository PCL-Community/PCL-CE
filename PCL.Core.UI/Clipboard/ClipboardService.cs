using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>基于 Avalonia <see cref="IClipboard" /> 的剪贴板实现。</summary>
public sealed class ClipboardService : IClipboardService
{
    public Task<string[]?> GetFileDropListAsync(
        CancellationToken cancellationToken = default)
    {
        return RunOnUiAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clipboard = _GetClipboard();
            if (clipboard is null) return null;

            try
            {
                var files = await clipboard.TryGetFilesAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (files is null) return null;

                var paths = files
                    .Select(item => item?.Path is { IsAbsoluteUri: true, Scheme: "file" } uri
                        ? uri.LocalPath
                        : null)
                    .OfType<string>()
                    .Where(path => !string.IsNullOrEmpty(path))
                    .ToArray();
                return paths.Length == 0 ? null : paths;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[PCL.Core.UI] 读取剪贴板文件失败：{ex.Message}");
                return null;
            }
        });
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        return RunOnUiAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clipboard = _GetClipboard();
            if (clipboard is null) return null;

            try
            {
                var text = await clipboard.TryGetTextAsync();
                cancellationToken.ThrowIfCancellationRequested();
                return text;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[PCL.Core.UI] 读取剪贴板文本失败：{ex.Message}");
                return null;
            }
        });
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return RunOnUiAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clipboard = _GetClipboard();
            if (clipboard is null) return;

            try
            {
                await clipboard.SetTextAsync(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[PCL.Core.UI] 写入剪贴板文本失败：{ex.Message}");
            }
        });
    }

    private static async Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
    {
        return Dispatcher.UIThread.CheckAccess()
            ? await action()
            : await Dispatcher.UIThread.InvokeAsync(action);
    }

    private static async Task RunOnUiAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            await action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    private static IClipboard? _GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            lifetime)
            return null;

        var window = lifetime.MainWindow ?? lifetime.Windows.FirstOrDefault();
        return window is null ? null : TopLevel.GetTopLevel(window)?.Clipboard;
    }
}