namespace PCL;

public static partial class DownloadUi
{
    private static void _RemoveConflictingLwjgl2(JsonObject outputJson)
    {
        if (outputJson["libraries"] is not JsonArray finalLibs || !finalLibs.Any(IsLwjgl3Library))
            return;

        for (var i = finalLibs.Count - 1; i >= 0; i--)
            if (IsLwjgl2Library(finalLibs[i]))
            {
                Common.Log($"[Download] 已移除与 LWJGL3 冲突的 LWJGL2 支持库：{finalLibs[i]?["name"]}");
                finalLibs.RemoveAt(i);
            }
    }

    /// <summary>
    ///     判断支持库是否为 LWJGL 3.x（Maven 坐标形如 org.lwjgl:lwjgl:3.x.y）。
    /// </summary>
    private static bool IsLwjgl3Library(JsonNode? library)
    {
        if (library is not JsonObject lib)
            return false;
        var segments = (lib["name"]?.ToString() ?? "").Split(':');
        return segments is ["org.lwjgl", _, _, ..]
            && segments[2].StartsWith("3.", StringComparison.Ordinal);
    }

    /// <summary>
    ///     判断支持库是否为与 LWJGL 3 冲突的 LWJGL 2.x。
    /// </summary>
    private static bool IsLwjgl2Library(JsonNode? library)
    {
        if (library is not JsonObject lib)
            return false;
        var segments = (lib["name"]?.ToString() ?? "").Split(':');
        return segments.Length >= 3
            && (segments[0] == "org.lwjgl.lwjgl"
                || (segments[0] == "org.lwjgl"
                    && segments[2].StartsWith("2.", StringComparison.Ordinal)));
    }
}