namespace PCL.Core.Platform;

/// <summary>本地化的 UI 后端：负责资源字典加载与资源查找。 UI 线程调度由 <see cref="IUiDispatcher" /> 负责。</summary>
public interface ILocalizationBackend
{
    /// <summary>按资源键查找本地化文本。</summary>
    string? TryFindResource(string key);

    /// <summary>
    /// 应用语言资源字典。实现方应先移除上一次由本后端添加的字典，再添加 <paramref name="baseLanguageCode" /> 与可选的
    /// <paramref name="currentLanguageCode" />。
    /// </summary>
    void ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode);
}