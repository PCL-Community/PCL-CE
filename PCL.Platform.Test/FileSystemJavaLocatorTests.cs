// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Domain.Minecraft.Java;
using PCL.Platform.Java;

namespace PCL.Platform.Test;

[TestClass]
public sealed class FileSystemJavaLocatorTests
{
    [TestMethod]
    public async Task FindAllAsync_ShouldReadReleaseFileWithoutStartingJava()
    {
        using TemporaryJavaHome javaHome = TemporaryJavaHome.Create(
            """
            JAVA_VERSION="17.0.11+9"
            IMPLEMENTOR="Eclipse Adoptium"
            OS_ARCH="x86_64"
            """,
            includeJavac: true);

        FileSystemJavaLocator locator = new([javaHome.Directory]);
        IReadOnlyList<JavaRuntimeCandidate> candidates = await locator.FindAllAsync(CancellationToken.None);

        Assert.AreEqual(1, candidates.Count);
        JavaInstallation installation = candidates[0].Installation;
        Assert.AreEqual(new Version(17, 0, 11, 0), installation.Version);
        Assert.AreEqual(JavaBrand.EclipseTemurin, installation.Brand);
        Assert.AreEqual(JavaArchitecture.X64, installation.Architecture);
        Assert.IsFalse(installation.IsJre);
        Assert.AreEqual(javaHome.JavaExecutablePath, installation.JavaExecutablePath);
    }

    [TestMethod]
    public async Task FindAllAsync_ShouldParseJava8UpdateVersion()
    {
        using TemporaryJavaHome javaHome = TemporaryJavaHome.Create(
            """
            JAVA_VERSION="1.8.0_412"
            IMPLEMENTOR="Microsoft"
            OS_ARCH="amd64"
            """,
            includeJavac: false);

        FileSystemJavaLocator locator = new([javaHome.Directory]);
        JavaInstallation installation = (await locator.FindAllAsync(CancellationToken.None))[0].Installation;

        Assert.AreEqual(new Version(1, 8, 0, 412), installation.Version);
        Assert.AreEqual(JavaBrand.Microsoft, installation.Brand);
        Assert.IsTrue(installation.IsJre);
    }

    private sealed class TemporaryJavaHome : IDisposable
    {
        private TemporaryJavaHome(string directory, string javaExecutablePath)
        {
            Directory = directory;
            JavaExecutablePath = javaExecutablePath;
        }

        public string Directory { get; }
        public string JavaExecutablePath { get; }

        public static TemporaryJavaHome Create(string releaseFileContent, bool includeJavac)
        {
            string root = Path.Combine(Path.GetTempPath(), "pcl-java-locator-" + Guid.NewGuid().ToString("N"));
            string bin = Path.Combine(root, "bin");
            global::System.IO.Directory.CreateDirectory(bin);

            string javaName = OperatingSystem.IsWindows() ? "java.exe" : "java";
            string javacName = OperatingSystem.IsWindows() ? "javac.exe" : "javac";
            string javaExecutable = Path.Combine(bin, javaName);
            File.WriteAllText(javaExecutable, string.Empty);
            if (includeJavac)
                File.WriteAllText(Path.Combine(bin, javacName), string.Empty);

            File.WriteAllText(Path.Combine(root, "release"), releaseFileContent);
            return new TemporaryJavaHome(root, javaExecutable);
        }

        public void Dispose()
        {
            if (global::System.IO.Directory.Exists(Directory))
                global::System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
