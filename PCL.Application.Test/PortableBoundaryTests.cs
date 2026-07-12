// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Application.Test;

[TestClass]
public sealed class PortableBoundaryTests
{
    // Word-boundary match so setting keys like ToolDownloadClipboard are not false positives.
    private static readonly (string Name, Regex Pattern)[] ForbiddenPatterns =
    [
        ("System.Windows", new Regex(@"System\.Windows", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("Avalonia.", new Regex(@"Avalonia\.", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("System.Management", new Regex(@"System\.Management", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("Microsoft.Win32", new Regex(@"Microsoft\.Win32", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("WindowInteropHelper", new Regex(@"\bWindowInteropHelper\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("HwndSource", new Regex(@"\bHwndSource\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("MessageBox", new Regex(@"\bMessageBox\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("Dispatcher", new Regex(@"\bDispatcher\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("Clipboard", new Regex(@"\bClipboard\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("OpenFileDialog", new Regex(@"\bOpenFileDialog\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("DllImport", new Regex(@"\bDllImport\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("LibraryImport", new Regex(@"\bLibraryImport\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("kernel32", new Regex(@"\bkernel32\b", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("user32", new Regex(@"\buser32\b", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("shell32", new Regex(@"\bshell32\b", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase))
    ];

    [TestMethod]
    public void PortableBusinessProjects_ShouldNotReferenceUiOrPlatformApis()
    {
        string root = FindRepositoryRoot();
        string[] projectDirectories =
        [
            Path.Combine(root, "PCL.Domain"),
            Path.Combine(root, "PCL.Application"),
            Path.Combine(root, "PCL.Platform.Abstractions")
        ];

        List<string> violations = [];
        foreach (string file in projectDirectories.SelectMany(EnumerateSourceFiles))
        {
            string text = File.ReadAllText(file);
            foreach ((string name, Regex pattern) in ForbiddenPatterns)
            {
                if (pattern.IsMatch(text))
                    violations.Add($"{Path.GetRelativePath(root, file)} contains {name}");
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
            if (File.Exists(Path.Combine(current.FullName, "PCL-N.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
