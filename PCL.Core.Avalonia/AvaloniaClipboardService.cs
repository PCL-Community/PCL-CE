using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>基于 Avalonia <see cref="IClipboard" /> 的剪贴板实现。</summary>
public sealed class AvaloniaClipboardService : IClipboardService
{
    public string[]? GetFileDropList()
    {
        var clipboard = _GetClipboard();
        if (clipboard is null) return null;

        try
        {
            var files = AvaloniaSyncHelper.Wait(clipboard.TryGetFilesAsync());
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
        catch (Exception ex)
        {
            Trace.WriteLine($"[PCL.Core.Avalonia] 读取剪贴板文件失败：{ex.Message}");
            return null;
        }
    }

    public string? GetText()
    {
        var clipboard = _GetClipboard();
        if (clipboard is null) return null;

        try
        {
            return AvaloniaSyncHelper.Wait(clipboard.TryGetTextAsync());
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PCL.Core.Avalonia] 读取剪贴板文本失败：{ex.Message}");
            return null;
        }
    }

    public void SetText(string text)
    {
        var clipboard = _GetClipboard();
        if (clipboard is null) return;

        try
        {
            AvaloniaSyncHelper.Wait(clipboard.SetTextAsync(text));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PCL.Core.Avalonia] 写入剪贴板文本失败：{ex.Message}");
        }
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