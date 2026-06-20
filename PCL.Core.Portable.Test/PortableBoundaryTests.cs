// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Portable.Test;

[TestClass]
public sealed class PortableBoundaryTests
{
    private static readonly string[] ForbiddenPatterns =
    [
        "System.Windows",
        "Avalonia.",
        "System.Management",
        "Microsoft.Win32",
        "WindowsIdentity",
        "WindowsPrincipal",
        "DllImport",
        "LibraryImport",
        "kernel32",
        "user32",
        "shell32"
    ];

    [TestMethod]
    public void PortableCore_ShouldNotReferenceUiOrOperatingSystemSpecificApis()
    {
        string root = FindRepositoryRoot();
        string projectDirectory = Path.Combine(root, "PCL.Core.Portable");
        List<string> violations = [];

        foreach (string file in EnumerateSourceFiles(projectDirectory))
        {
            string text = File.ReadAllText(file);
            foreach (string pattern in ForbiddenPatterns)
            {
                if (text.Contains(pattern, StringComparison.Ordinal))
                    violations.Add($"{Path.GetRelativePath(root, file)} contains {pattern}");
            }
        }

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(static file =>
                !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PCL.Portable.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
