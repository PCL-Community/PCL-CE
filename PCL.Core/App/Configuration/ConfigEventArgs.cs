namespace PCL.Core.App.Configuration;

/// <summary>
/// 配置项事件参数。
/// </summary>
/// <param name="Key">配置键。</param>
/// <param name="Event">触发事件。</param>
/// <param name="Argument">上下文参数。</param>
/// <param name="OldValue">旧值。</param>
/// <param name="NewValue">新值。</param>
public record ConfigEventArgs(
    string Key,
    ConfigEvent Event,
    object? Argument,
    object? OldValue,
    object? NewValue
) {
    /// <summary>
    /// 设置一个新值代替原来的值进行对应操作，只在 <see cref="ConfigObserver.IsPreview"/> 为 <c>true</c> 时有效。
    /// </summary>
    public object? NewValueReplacement { get; set; } = null;

    /// <summary>
    /// 是否取消事件，只在 <see cref="ConfigObserver.IsPreview"/> 为 <c>true</c> 时有效。
    /// </summary>
    public bool Cancelled { get; set; } = false;

    /// <summary>
    /// 配置当前的值。
    /// </summary>
    public object? Value => NewValueReplacement ?? NewValue;
}
