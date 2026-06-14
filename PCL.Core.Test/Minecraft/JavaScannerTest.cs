using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Java.Scanner;
using System;
using System.Collections.Generic;
using System.IO;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class JavaScannerTest
{
    private string _tempRoot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PCLTest", "JavaScanner", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    [TestMethod]
    public void Scan_ShouldFindJetBrainsAndGraalVmManagedJdks()
    {
        var jetBrainsJava = CreateJava(".jdks", "graalvm-jdk-21", "bin");
        var gradleJava = CreateJava(".gradle", "jdks", "temurin-17", "bin");
        var results = new List<string>();

        new DefaultPathsScanner([_tempRoot]).Scan(results);

        CollectionAssert.Contains(results, jetBrainsJava);
        CollectionAssert.Contains(results, gradleJava);
    }

    [TestMethod]
    public void Scan_ShouldSkipDependencyAndReparseNoise()
    {
        CreateJava("node_modules", "fake-java", "bin");
        var actualJava = CreateJava("vendors", "zulu-21", "bin");
        var results = new List<string>();

        new DefaultPathsScanner([_tempRoot]).Scan(results);

        Assert.HasCount(1, results);
        Assert.IsTrue(string.Equals(actualJava, results[0], StringComparison.OrdinalIgnoreCase));
    }

    private string CreateJava(params string[] pathParts)
    {
        var folder = _tempRoot;
        foreach (var part in pathParts)
            folder = Path.Combine(folder, part);
        Directory.CreateDirectory(folder);

        var javaPath = Path.Combine(folder, "java.exe");
        File.WriteAllBytes(javaPath, []);
        return javaPath;
    }
}
