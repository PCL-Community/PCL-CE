using System.Collections.Generic;

namespace PCL.Core.App.Configuration.Impl;

public interface IEnumerableKeyProvider
{
    /// <summary>
    /// 获取文件包含的所有键。通常是一个耗时操作，慎用。
    /// </summary>
    public IEnumerable<string> Keys { get; }
}
