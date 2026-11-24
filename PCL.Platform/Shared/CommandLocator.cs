namespace PCL.Platform;

internal static class CommandLocator
{
    public static bool ExistsOnPath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        if (Path.IsPathRooted(command))
            return File.Exists(command);

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [""];

        foreach (
            var directory in path.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (File.Exists(Path.Combine(directory, command)))
                return true;

            if (!OperatingSystem.IsWindows())
                continue;

            if (extensions
                .Select(extension => Path.Combine(directory, command + extension))
                .Any(File.Exists))
                return true;
        }

        return false;
    }
}