using System;
using System.IO;

namespace PCL.Core.Utils.OS;

/// <summary>Linux XDG 基础目录解析。</summary>
public static class XdgPaths
{
    public static bool IsLinux => OperatingSystem.IsLinux();

    public static string Home => _GetHome();
    public static string DataHome => _GetBaseDirectory("XDG_DATA_HOME", ".local/share");
    public static string ConfigHome => _GetBaseDirectory("XDG_CONFIG_HOME", ".config");
    public static string CacheHome => _GetBaseDirectory("XDG_CACHE_HOME", ".cache");
    public static string StateHome => _GetBaseDirectory("XDG_STATE_HOME", ".local/state");

    public static string DesktopDirectory
    {
        get
        {
            var explicitPath = Environment.GetEnvironmentVariable("XDG_DESKTOP_DIR");
            var configured = _IsAbsolute(explicitPath)
                ? Path.GetFullPath(explicitPath!)
                : ReadUserDirectory("XDG_DESKTOP_DIR");
            if (string.IsNullOrWhiteSpace(configured))
                return Path.Combine(Home, "Desktop");

            configured = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
            return string.Equals(configured, Home, StringComparison.Ordinal)
                ? Path.Combine(Home, "Desktop")
                : configured;
        }
    }

    /// <summary>读取 <c>user-dirs.dirs</c> 中指定的用户目录。</summary>
    public static string? ReadUserDirectory(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var file = Path.Combine(ConfigHome, "user-dirs.dirs");
        try
        {
            if (!File.Exists(file)) return null;
            foreach (var rawLine in File.ReadLines(file))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var separator = line.IndexOf('=');
                if (separator <= 0) continue;
                if (!string.Equals(line[..separator].Trim(), key, StringComparison.Ordinal))
                    continue;

                var value = _ExpandHome(_Unquote(line[(separator + 1)..].Trim()));
                return !_IsAbsolute(value) ? null : Path.GetFullPath(value);
            }
        }
        catch (Exception)
        {
            // user-dirs.dirs 不可读时使用默认目录。
        }

        return null;
    }

    private static string _GetBaseDirectory(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return _IsAbsolute(value)
            ? Path.GetFullPath(value!)
            : Path.Combine(Home, fallback);
    }

    private static string _GetHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? Path.DirectorySeparatorChar.ToString() : home;
    }

    private static bool _IsAbsolute(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value);
    }

    private static string _Unquote(string value)
    {
        if (value is ['"', _, ..] && value[^1] == '"')
            value = value[1..^1];
        return value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string _ExpandHome(string value)
    {
        var home = Home;
        return value switch
        {
            "$HOME" or "${HOME}"
                => home,
            _ when value.StartsWith("$HOME/", StringComparison.Ordinal)
                => Path.Combine(home, value[6..]),
            _ when value.StartsWith("${HOME}/", StringComparison.Ordinal)
                => Path.Combine(home, value[8..]),
            _ => value
        };
    }
}