using System;

namespace PCL.Core.Minecraft;

/// <summary>
/// 用于解析 Maven 包 ID 为 Uri 或 Path
/// </summary>
/// <param name="mavenId"></param>
public class MavenArtifact(string mavenId)
{
    /// <summary>
    /// 将 Maven 包 ID 转换为 Uri 或 Path
    /// </summary>
    /// <param name="uriOrPath"></param>
    /// <returns></returns>
    public string Resolve(string uriOrPath)
    {
        return $"{uriOrPath.TrimEnd('/')}{_GetMavenPath(mavenId)}";
    }

    /// <summary>
    /// 解析 Maven 包 ID
    /// </summary>
    /// <param name="packageId">包 ID</param>
    /// <returns>Maven 包相对路径 (以 / 开头)</returns>
    /// <exception cref="FormatException">给定的 Maven 包 ID 长度过长或过短</exception>
    private static string _GetMavenPath(string packageId)
    {
        var packageIds = packageId.Split(":");
        switch (packageIds.Length)
        {
            case 3:
                return $"/{packageIds[0].Replace(".","/")}/{packageIds[1]}/{packageIds[1]}-{packageIds[2]}.jar";
            case 4:
                if (_IsCommonPackaging(packageIds[2]))
                {
                    return $"/{packageIds[0].Replace(".","/")}/{packageIds[1]}/{packageIds[1]}-{packageIds[3]}.{packageIds[2]}";
                }
                // 此处原为 packageId[1]，取到的是原始字符串的第二个「字符」而非第二个坐标段
                return $"/{packageIds[0].Replace(".","/")}/{packageIds[1]}/{packageIds[1]}-{packageIds[2]}-{packageIds[3]}.jar";
            case 5:
                // 此处原为 packageId[2] / packageId[4]（同样是字符索引），且缺少前导斜杠，
                // 导致拼接本地路径时缺少分隔符
                return $"/{packageIds[0].Replace(".","/")}/{packageIds[1]}/{packageIds[1]}-{packageIds[3]}-{packageIds[4]}.{packageIds[2]}";
            default:
                throw new FormatException($"Invalid maven package id: Length is {packageIds.Length}");
        }
    }
    /// <summary>
    /// 用于检查是否是 Packaging
    /// </summary>
    private static bool _IsCommonPackaging(string name)
    {
        return name == "jar" || name == "zip" || name == "pom";
    }
}