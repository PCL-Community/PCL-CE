using PCL.Core.Platform;

namespace PCL.Core.App.Media;

/// <summary>媒体服务的运行时访问入口。</summary>
public static class MediaService
{
    /// <summary>当前注册的媒体服务。</summary>
    public static IMediaService? Current => ApplicationRuntime.GetService<IMediaService>();
}