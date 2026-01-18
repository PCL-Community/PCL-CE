namespace PCL.Core.App.Configuration.Impl;

// ReSharper disable once InconsistentNaming
public interface IKeyValueFileProvider
{
    /// <summary>
    /// 文件路径。
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// 获取一个值。
    /// </summary>
    public T Get<T>(string key);

    /// <summary>
    /// 设置一个值。
    /// </summary>
    public void Set<T>(string key, T value);

    /// <summary>
    /// 判断一个值是否存在。
    /// </summary>
    public bool Exists(string key);

    /// <summary>
    /// 移除一个值。
    /// </summary>
    public void Remove(string key);

    /// <summary>
    /// 写入文件。
    /// </summary>
    public void Sync();
}
