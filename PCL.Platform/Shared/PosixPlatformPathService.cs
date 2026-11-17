using PCL.Core.App;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Unix 路径规则：不处理 8.3 短路径，使用临时目录作为回退。</summary>
public class PosixPlatformPathService(string officialMinecraftDirectory) : IPlatformPathService
{
    public string GetOfficialMinecraftDirectory()
    {
        return officialMinecraftDirectory;
    }

    public string GetPureAsciiDirectory()
    {
        var dataDirectory = Paths.Data;
        if (dataDirectory.All(c => c < 128))
            return EnsureTrailingSeparator(dataDirectory);

        var temp = Path.GetTempPath();
        if (temp.All(c => c < 128))
            return EnsureTrailingSeparator(temp);

        return EnsureTrailingSeparator(Path.Combine(temp, "PCL"));
    }

    public string ShortenPath(string path)
    {
        return path;
    }

    public string GetFallbackNativesDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "PCLCE", "natives");
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}