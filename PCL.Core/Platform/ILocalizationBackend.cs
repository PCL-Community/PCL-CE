using System;
using PCL.Core.App.Localization;

namespace PCL.Core.Platform;

/// <summary>
///     本地化的 UI 后端。WPF/Avalonia 分别实现资源字典加载、资源查找与字体应用。
/// </summary>
public interface ILocalizationBackend
{
    /// <summary>当前线程是否为 UI 线程。</summary>
    bool CheckAccess();

    /// <summary>在 UI 线程执行操作。</summary>
    void Invoke(Action action);

    /// <summary>按资源键查找本地化文本。</summary>
    string? TryFindResource(string key);

    /// <summary>
    ///     应用语言资源字典。实现方应先移除上一次由本后端添加的字典，再添加
    ///     <paramref name="baseLanguageCode" /> 与可选的 <paramref name="currentLanguageCode" />。
    /// </summary>
    void ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode);

    /// <summary>应用启动器 UI 字体。</summary>
    void ApplyLaunchFont(string? customFontName, LocalizationLanguage? language);
}