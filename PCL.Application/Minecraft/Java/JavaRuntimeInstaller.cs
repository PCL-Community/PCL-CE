// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PCL.Platform.Abstractions.Java;
using PCL.Platform.Abstractions.Paths;

namespace PCL.Application.Minecraft.Java;

public sealed record JavaRuntimeInstallProgress(
    string Stage,
    double Progress,
    int CompletedFiles,
    int TotalFiles,
    string? Detail = null);

public sealed class JavaRuntimeInstaller
{
    private readonly JavaRuntimeDownloadPlanService _planService;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public JavaRuntimeInstaller(IJavaRuntimeMetadataProvider metadataProvider)
        : this(new JavaRuntimeDownloadPlanService(metadataProvider), new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, ownsHttpClient: true)
    {
    }

    public JavaRuntimeInstaller(
        JavaRuntimeDownloadPlanService planService,
        HttpClient httpClient,
        bool ownsHttpClient = false)
    {
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<string> InstallAsync(
        string requestedComponent,
        string runtimeRootDirectory,
        IProgress<JavaRuntimeInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedComponent);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRootDirectory);

        JavaRuntimePlatform platform = DetectPlatform();
        progress?.Report(new JavaRuntimeInstallProgress("准备 Java 下载计划", 0.02d, 0, 1));
        JavaRuntimeDownloadPlan plan = await _planService.CreatePlanAsync(
                requestedComponent,
                platform,
                runtimeRootDirectory,
                cancellationToken)
            .ConfigureAwait(false);

        Directory.CreateDirectory(plan.TargetDirectory);
        int total = Math.Max(plan.Files.Count, 1);
        int completed = 0;
        progress?.Report(new JavaRuntimeInstallProgress(
            "下载 Java " + plan.VersionName,
            0.05d,
            0,
            total,
            plan.Files.Count + " 个文件"));

        foreach (JavaRuntimeDownloadFile file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);
            if (File.Exists(file.TargetPath) &&
                !string.IsNullOrWhiteSpace(file.Sha1) &&
                string.Equals(await ComputeSha1Async(file.TargetPath, cancellationToken).ConfigureAwait(false), file.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                completed++;
                progress?.Report(new JavaRuntimeInstallProgress(
                    "校验已有文件",
                    0.05d + (0.9d * completed / total),
                    completed,
                    total,
                    file.RelativePath));
                continue;
            }

            await DownloadFileAsync(file, cancellationToken).ConfigureAwait(false);
            completed++;
            progress?.Report(new JavaRuntimeInstallProgress(
                "下载 Java 文件",
                0.05d + (0.9d * completed / total),
                completed,
                total,
                file.RelativePath));
        }

        string? javaExecutable = FindJavaExecutable(plan.TargetDirectory);
        if (javaExecutable is null)
            throw new InvalidOperationException("Java 运行时已下载，但未找到 java 可执行文件：" + plan.TargetDirectory);

        progress?.Report(new JavaRuntimeInstallProgress("Java 安装完成", 1d, total, total, javaExecutable));
        return javaExecutable;
    }

    public static string GetDefaultRuntimeRoot(IPlatformPathProvider paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "runtime");
    }

    public static JavaRuntimePlatform DetectPlatform()
    {
        JavaRuntimeOperatingSystem os =
            OperatingSystem.IsWindows() ? JavaRuntimeOperatingSystem.Win32 :
            OperatingSystem.IsMacOS() ? JavaRuntimeOperatingSystem.MacOs :
            JavaRuntimeOperatingSystem.Linux;

        JavaRuntimeArchitecture arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => JavaRuntimeArchitecture.X86,
            Architecture.Arm64 => JavaRuntimeArchitecture.Arm64,
            _ => JavaRuntimeArchitecture.X64
        };
        return new JavaRuntimePlatform(os, arch);
    }

    private async Task DownloadFileAsync(JavaRuntimeDownloadFile file, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
                file.Url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string tempPath = file.TargetPath + ".download";
        await using (Stream network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (FileStream output = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await network.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(file.Sha1))
        {
            string actual = await ComputeSha1Async(tempPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actual, file.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                throw new InvalidOperationException("Java 文件校验失败：" + file.RelativePath);
            }
        }

        if (File.Exists(file.TargetPath))
            File.Delete(file.TargetPath);
        File.Move(tempPath, file.TargetPath);
    }

    private static async Task<string> ComputeSha1Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
#pragma warning disable CA5350 // Mojang runtime manifests provide SHA-1 digests only.
        byte[] hash = await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA5350
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? FindJavaExecutable(string root)
    {
        string exeName = OperatingSystem.IsWindows() ? "java.exe" : "java";
        try
        {
            return Directory.EnumerateFiles(root, exeName, SearchOption.AllDirectories)
                .OrderBy(static path => path.Length)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
