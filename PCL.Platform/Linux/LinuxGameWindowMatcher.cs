namespace PCL.Platform;

/// <summary>Linux 游戏窗口识别评分，供 X11 与 Wayland 后端共用。</summary>
internal static class LinuxGameWindowMatcher
{
    /// <summary>达到该分值才认为窗口与游戏相关。</summary>
    public const int GameWindowThreshold = 3;

    private static readonly string[] ApplicationKeywords =
    [
        "minecraft",
        "java",
        "glfw",
        "lwjgl"
    ];

    private static readonly string[] TitleKeywords =
    [
        "minecraft",
        "fml",
        "forge",
        "quilt",
        "glfw",
        "lwjgl"
    ];

    public static int Score(string? applicationId, string? title)
    {
        var appId = applicationId ?? string.Empty;
        var windowTitle = title ?? string.Empty;

        return 1
            + ApplicationKeywords.Count(keyword =>
                appId.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            * 2
            + TitleKeywords.Count(keyword =>
                windowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            * 2;
    }
}