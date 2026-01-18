using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.App.Configuration;

public interface IConfigProvider
{
    /// <summary>
    /// 获取一个值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">返回值，若不存在则返回默认值</param>
    /// <param name="argument">上下文参数</param>
    /// <typeparam name="T">值的类型</typeparam>
    /// <returns>值是否存在，若存在则为 <c>true</c></returns>
    public bool GetValue<T>(string key, [NotNullWhen(true)] out T? value, object? argument = null);
    
    /// <summary>
    /// 设置一个值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">新值</param>
    /// <param name="argument">上下文参数</param>
    /// <typeparam name="T">值的类型</typeparam>
    public void SetValue<T>(string key, T value, object? argument = null);
    
    /// <summary>
    /// 删除一个值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="argument">上下文参数</param>
    public void Delete(string key, object? argument = null);
    
    /// <summary>
    /// 判断一个值是否存在。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="argument">上下文参数</param>
    /// <returns>值是否存在，若存在则为 <c>true</c></returns>
    public bool Exists(string key, object? argument = null);
}
