using System.Linq;

namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// Chunkbase 地图工具辅助类
/// </summary>
public static class ChunkbaseHelper
{
    private const string BaseUrl = "https://www.chunkbase.com/apps/seed-map";
    
    /// <summary>
    /// 获取 Chunkbase 支持的版本字符串
    /// </summary>
    public static string? GetSupportedVersion(string? versionName)
    {
        if (string.IsNullOrEmpty(versionName))
            return null;
        
        // 预览版不支持
        if (versionName.Any(char.IsLetter))
            return null;
        
        if (versionName.StartsWith("1.21"))
            return versionName.Replace(".", "_");
        
        if (versionName.Contains('.'))
        {
            var parts = versionName.Split('.');
            return $"{parts[0]}_{parts[1]}";
        }
        
        return versionName.Replace(".", "_");
    }
    
    /// <summary>
    /// 构建 Chunkbase URL
    /// </summary>
    public static string? BuildUrl(string seed, string? versionName)
    {
        var version = GetSupportedVersion(versionName);
        if (version == null)
            return null;
        
        return $"{BaseUrl}#seed={seed}&platform=java_{version}&dimension=overworld";
    }
}