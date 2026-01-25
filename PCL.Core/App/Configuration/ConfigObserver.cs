namespace PCL.Core.App.Configuration;

/// <summary>
/// 配置事件观察器。
/// </summary>
/// <param name="Event">观察的事件。</param>
/// <param name="Handler">事件处理委托。</param>
/// <param name="IsPreview">指定是否预览事件处理，预览事件可覆盖原有值或取消事件处理过程。</param>
public record ConfigObserver(
    ConfigEvent Event,
    ConfigEventHandler Handler,
    bool IsPreview = false
);
