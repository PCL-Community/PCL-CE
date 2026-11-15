using System.IO;

namespace PCL.Core.Platform;

/// <summary>
///     程序打包资源访问。具体实现由 UI 层注册到 <see cref="PlatformServices.ResourceStore" />。
/// </summary>
public interface IResourceStore
{
    /// <summary>获取声明为 Resource 的打包资源流；资源不存在时返回 <c>null</c>。</summary>
    Stream? GetResourceStream(string path);
}