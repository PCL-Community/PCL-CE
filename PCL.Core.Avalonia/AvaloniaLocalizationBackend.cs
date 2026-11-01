using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>
///     基于 Avalonia 应用资源字典与字体的本地化后端。
///     语言资源字典与启动器字体随样式迁移接入，本类先保证资源查找与线程语义可用。
/// </summary>
public sealed class AvaloniaLocalizationBackend : ILocalizationBackend
{
    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }

    public void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Invoke(action);
    }

    public string? TryFindResource(string key)
    {
        if (Application.Current is { } app && app.TryFindResource(key, out var value))
            return value as string;
        return Lifecycle.CurrentApplication?.TryFindResource(key) as string;
    }

    public void ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode)
    {
        // TODO: 语言资源字典尚未迁移到 Avalonia，迁移完成后在此合并基础语言与当前语言字典
        Trace.WriteLine(
            $"[PCL.Core.Avalonia] 语言资源尚未接入 Avalonia：base={baseLanguageCode}, current={currentLanguageCode}");
    }

    public void ApplyLaunchFont(string? customFontName, LocalizationLanguage? language)
    {
        // TODO: 启动器字体随样式迁移接入
        Trace.WriteLine(
            $"[PCL.Core.Avalonia] 启动器字体尚未接入 Avalonia：font={customFontName}, language={language?.Code}");
    }
}