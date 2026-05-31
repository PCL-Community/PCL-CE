using System;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft;

public static class SaveImportHelper
{
    public static string? GetSaveRootDirectory(string extractedDirectory)
    {
        if (string.IsNullOrWhiteSpace(extractedDirectory) || !Directory.Exists(extractedDirectory))
            return null;

        var rootDirectory = Path.GetFullPath(extractedDirectory);
        if (File.Exists(Path.Combine(rootDirectory, "level.dat")))
            return rootDirectory;

        var rootDirectories = Directory.GetDirectories(rootDirectory);
        if (rootDirectories.Length != 1)
            return null;

        var nestedDirectory = Path.GetFullPath(rootDirectories.Single());
        return File.Exists(Path.Combine(nestedDirectory, "level.dat")) ? nestedDirectory : null;
    }
}
