namespace PCL.Core.UI.Background;

/// <summary>背景刷新结果状态。</summary>
public enum BackgroundRefreshStatus
{
    Success,
    NoContent,
    FfmpegMissing,
    Canceled,
    Failed
}

/// <summary>背景刷新结果。</summary>
public sealed record BackgroundRefreshResult(
    BackgroundRefreshStatus Status,
    string? Path = null,
    string? Detail = null);

/// <summary>主窗口背景入口：供设置页等外部调用方请求重新加载背景内容。</summary>
public static class BackgroundHost
{
    /// <summary>当前主窗口的背景层。</summary>
    public static BackgroundLayer? Current { get; private set; }

    /// <summary>背景播放状态变化时触发。</summary>
    public static event Action? StateChanged;

    /// <summary>请求重新加载背景内容。</summary>
    public static Task<BackgroundRefreshResult> RequestRefreshAsync()
    {
        return Current?.RefreshAsync()
            ?? Task.FromResult(
                new BackgroundRefreshResult(BackgroundRefreshStatus.NoContent));
    }

    public static void Attach(BackgroundLayer layer)
    {
        Current = layer;
    }

    public static void Detach(BackgroundLayer layer)
    {
        if (ReferenceEquals(Current, layer)) Current = null;
    }

    internal static void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}