// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Launch.Natives;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftNativeExtractionServiceTests
{
    [TestMethod]
    public void Extract_ShouldSelectWindowsNativeFiles()
    {
        using TemporaryWorkspace workspace = TemporaryWorkspace.Create();
        string archivePath = workspace.CreateZip(
            ("lwjgl.dll", [1, 2, 3]),
            ("liblwjgl.so", [4, 5, 6]),
            ("liblwjgl.dylib", [7, 8, 9]));

        MinecraftNativeExtractionResult result = MinecraftNativeExtractionService.Extract(
            new MinecraftNativeExtractionRequest
            {
                ArchivePaths = [archivePath],
                TargetDirectory = workspace.TargetDirectory,
                OperatingSystem = MinecraftNativeOperatingSystem.Win32
            });

        Assert.AreEqual(1, result.ExtractedFiles.Count);
        Assert.IsTrue(File.Exists(Path.Combine(workspace.TargetDirectory, "lwjgl.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(workspace.TargetDirectory, "liblwjgl.so")));
        Assert.IsFalse(File.Exists(Path.Combine(workspace.TargetDirectory, "liblwjgl.dylib")));
    }

    [TestMethod]
    public void Extract_ShouldSelectLinuxVersionedSharedObjects()
    {
        using TemporaryWorkspace workspace = TemporaryWorkspace.Create();
        string archivePath = workspace.CreateZip(
            ("libopenal.so.1", [1]),
            ("native.dll", [2]));

        MinecraftNativeExtractionService.Extract(
            new MinecraftNativeExtractionRequest
            {
                ArchivePaths = [archivePath],
                TargetDirectory = workspace.TargetDirectory,
                OperatingSystem = MinecraftNativeOperatingSystem.Linux
            });

        Assert.IsTrue(File.Exists(Path.Combine(workspace.TargetDirectory, "libopenal.so.1")));
        Assert.IsFalse(File.Exists(Path.Combine(workspace.TargetDirectory, "native.dll")));
    }

    [TestMethod]
    public void Extract_ShouldSelectMacNativeFiles()
    {
        using TemporaryWorkspace workspace = TemporaryWorkspace.Create();
        string archivePath = workspace.CreateZip(
            ("libglfw.dylib", [1]),
            ("legacy.jnilib", [2]),
            ("native.dll", [3]));

        MinecraftNativeExtractionResult result = MinecraftNativeExtractionService.Extract(
            new MinecraftNativeExtractionRequest
            {
                ArchivePaths = [archivePath],
                TargetDirectory = workspace.TargetDirectory,
                OperatingSystem = MinecraftNativeOperatingSystem.MacOs
            });

        Assert.AreEqual(2, result.ExtractedFiles.Count);
        Assert.IsTrue(File.Exists(Path.Combine(workspace.TargetDirectory, "libglfw.dylib")));
        Assert.IsTrue(File.Exists(Path.Combine(workspace.TargetDirectory, "legacy.jnilib")));
        Assert.IsFalse(File.Exists(Path.Combine(workspace.TargetDirectory, "native.dll")));
    }

    [TestMethod]
    public void Extract_ShouldRejectPathTraversal()
    {
        using TemporaryWorkspace workspace = TemporaryWorkspace.Create();
        string archivePath = workspace.CreateZip(("../escape.dll", [1]));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            MinecraftNativeExtractionService.Extract(
                new MinecraftNativeExtractionRequest
                {
                    ArchivePaths = [archivePath],
                    TargetDirectory = workspace.TargetDirectory,
                    OperatingSystem = MinecraftNativeOperatingSystem.Win32
                }));

        Assert.IsFalse(File.Exists(Path.Combine(workspace.RootDirectory, "escape.dll")));
    }

    [TestMethod]
    public void Extract_ShouldDeleteUnknownFiles()
    {
        using TemporaryWorkspace workspace = TemporaryWorkspace.Create();
        Directory.CreateDirectory(workspace.TargetDirectory);
        string staleFile = Path.Combine(workspace.TargetDirectory, "stale.dll");
        File.WriteAllBytes(staleFile, [9]);
        string archivePath = workspace.CreateZip(("fresh.dll", [1, 2]));

        MinecraftNativeExtractionResult result = MinecraftNativeExtractionService.Extract(
            new MinecraftNativeExtractionRequest
            {
                ArchivePaths = [archivePath],
                TargetDirectory = workspace.TargetDirectory,
                OperatingSystem = MinecraftNativeOperatingSystem.Win32
            });

        Assert.IsFalse(File.Exists(staleFile));
        CollectionAssert.Contains(result.DeletedFiles.ToList(), staleFile);
        Assert.IsTrue(File.Exists(Path.Combine(workspace.TargetDirectory, "fresh.dll")));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string rootDirectory)
        {
            RootDirectory = rootDirectory;
            TargetDirectory = Path.Combine(rootDirectory, "natives");
        }

        public string RootDirectory { get; }
        public string TargetDirectory { get; }

        public static TemporaryWorkspace Create() =>
            new(Path.Combine(Path.GetTempPath(), "pcl-natives-" + Guid.NewGuid().ToString("N")));

        public string CreateZip(params (string EntryName, byte[] Content)[] entries)
        {
            Directory.CreateDirectory(RootDirectory);
            string archivePath = Path.Combine(RootDirectory, "natives.jar");
            using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
            foreach ((string entryName, byte[] content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(entryName);
                using Stream stream = entry.Open();
                stream.Write(content);
            }

            return archivePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
                Directory.Delete(RootDirectory, recursive: true);
        }
    }
}
