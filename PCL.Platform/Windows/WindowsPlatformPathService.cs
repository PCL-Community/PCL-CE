using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.App;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Windows 路径规则：官方目录、纯 ASCII 目录与 8.3 短路径。</summary>
public sealed class WindowsPlatformPathService : IPlatformPathService
{
    private const int MaxPath = 260;

    public string GetOfficialMinecraftDirectory()
    {
        return EnsureTrailingSeparator(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft"));
    }

    public string GetPureAsciiDirectory()
    {
        var dataDirectory = Paths.Data;
        if (dataDirectory.All(c => c < 128))
            return EnsureTrailingSeparator(dataDirectory);

        var temp = Path.GetTempPath();
        if (temp.All(c => c < 128))
            return EnsureTrailingSeparator(temp);

        return EnsureTrailingSeparator(
            Path.Combine(
                Path.GetPathRoot(Environment.SystemDirectory)
                ?? Path.DirectorySeparatorChar.ToString(),
                "ProgramData",
                "PCL"));
    }

    public string ShortenPath(string path)
    {
        if (path.Length <= 247) return path;

        var buffer = new StringBuilder(MaxPath);
        _ = GetShortPathName(path, buffer, buffer.Capacity);
        var shortened = buffer.ToString();
        return string.IsNullOrEmpty(shortened) ? path : shortened;
    }

    public string GetFallbackNativesDirectory()
    {
        return Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory)
            ?? Path.DirectorySeparatorChar.ToString(),
            "ProgramData",
            "PCL",
            "natives");
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    [DllImport("kernel32", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode)]
    private static extern int GetShortPathName(
        string longPath,
        StringBuilder shortPath,
        int bufferLength);
}