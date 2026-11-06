namespace PCL;

/// <summary>下载业务需要 UI 层提供的交互。</summary>
public interface IDownloadUserInteraction
{
    /// <summary>提示 Minecraft 客户端版本更新，并允许用户跳转到下载页。</summary>
    void ShowClientUpdateHint(string versionName, JsonObject json);
}

/// <summary>UI 层注册的下载交互实现。</summary>
public static class DownloadUserInteraction
{
    public static IDownloadUserInteraction? Current { get; set; }
}