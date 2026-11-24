namespace PCL.Core.Platform;

/// <summary>与平台路径语义相关的操作。业务代码通过该服务处理路径差异，不直接判断平台。</summary>
public interface IPlatformPathService
{
    /// <summary>官方启动器使用的 Minecraft 目录。</summary>
    string GetOfficialMinecraftDirectory();

    /// <summary>可用于启动器临时资源的纯 ASCII 目录。</summary>
    string GetPureAsciiDirectory();

    /// <summary>按平台规则缩短路径；平台不支持时返回原路径。</summary>
    string ShortenPath(string path);

    /// <summary>Natives 解压失败时的平台回退目录。</summary>
    string GetFallbackNativesDirectory();
}