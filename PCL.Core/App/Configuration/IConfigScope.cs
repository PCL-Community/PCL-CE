using System.Collections.Generic;

namespace PCL.Core.App.Configuration;

/// <summary>
/// 配置作用域。
/// </summary>
public interface IConfigScope
{
    /// <summary>
    /// 检查指定的多个配置项是否在该作用域中。
    /// </summary>
    /// <param name="keys">配置键</param>
    /// <returns>所有存在于该作用域中的键的集合</returns>
    public IEnumerable<string> CheckScope(IReadOnlySet<string> keys);

    /// <summary>
    /// 重置作用域，将使作用域中的所有值回到默认值状态。
    /// </summary>
    /// <param name="argument">上下文参数</param>
    /// <returns>是否成功重置作用域，若成功则为 <c>true</c></returns>
    public bool Reset(object? argument = null);

    /// <summary>
    /// 检查作用域是否为默认。
    /// </summary>
    /// <param name="argument">上下文参数</param>
    /// <returns>若作用域中的所有值均为默认值，则为 <c>true</c>，否则为 <c>false</c></returns>
    public bool IsDefault(object? argument = null);
}
