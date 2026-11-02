using System;

namespace PCL.Core.Platform;

/// <summary>
///     本地化的 UI 后端：负责资源字典加载、资源查找，以及本地化文本在界面线程上的读取。
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
}