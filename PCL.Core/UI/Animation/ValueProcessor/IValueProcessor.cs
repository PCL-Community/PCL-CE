namespace PCL.Core.UI.Animation.ValueProcessor;

/// <summary>
/// 数值处理器。
/// </summary>
public interface IValueProcessor<T>
{
    /// <summary>
    /// 过滤值。
    /// </summary>
    /// <param name="value">值。</param>
    /// <returns>返回过滤后的值。</returns>
    T Filter(T value);
    
    /// <summary>
    /// 将两个值相加。
    /// </summary>
    /// <param name="value1">第一个值。</param>
    /// <param name="value2">第二个值。</param>
    /// <returns>返回相加后的值。</returns>
    T Add(T value1, T value2);
    
    /// <summary>
    /// 将两个值相减。
    /// </summary>
    /// <param name="value1">第一个值。</param>
    /// <param name="value2">第二个值。</param>
    /// <returns>返回相减后的值。</returns>
    T Subtract(T value1, T value2);
    
    /// <summary>
    /// 将值按比例因子进行缩放。
    /// </summary>
    /// <param name="value">需要缩放的值。</param>
    /// <param name="factor">缩放因子。</param>
    /// <returns>返回缩放后的值。</returns>
    T Scale(T value, double factor);

    /// <summary>
    /// 获取某种类型的初始值。
    /// </summary>
    /// <returns>初始值。</returns>
    T DefaultValue();
    
    /// <summary>
    /// 比较两个值是否相等。
    /// </summary>
    /// <param name="value1">第一个值。</param>
    /// <param name="value2">第二个值。</param>
    /// <returns></returns>
    bool Equal(T value1, T value2);
}