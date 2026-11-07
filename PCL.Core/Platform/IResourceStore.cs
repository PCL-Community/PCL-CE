using System.IO;

namespace PCL.Core.Platform;

/// <summary>
///     程序打包资源访问。具体实现由 UI 层注册到 <see cref="PlatformServices.ResourceStore" />。
/// </summary>
public interface IResourceStore
{
    /// <summary>获取声明为 Resource 的打包资源流；资源不存在时返回 <c>null</c>。</summary>
    Stream? GetResourceStream(string path);

    /// <summary>获取程序内嵌图片的根 URI（以 <c>/</c> 结尾），具体 URI 方案由 UI 层实现决定。</summary>
    string GetImageRootUri();

    /// <summary>获取程序内嵌图片的完整 URI。</summary>
    /// <param name="imageName">相对于图片根目录的图片路径，例如 "Icons/NoIcon.png"</param>
    string GetImageUri(string imageName)
    {
        return GetImageRootUri() + imageName;
    }
}