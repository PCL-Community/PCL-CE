// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace PCL.Application.Downloads;

public sealed record MinecraftLoaderInstallerArtifact(
    MinecraftLoaderKind Kind,
    string FileName,
    IReadOnlyList<string> Sources);

public sealed record MinecraftExternalLoaderInstallRequest(
    MinecraftLoaderKind Kind,
    string LoaderVersion,
    string GameVersion,
    string JavaExecutablePath,
    string InstallerPath,
    string MinecraftRootDirectory);

public interface IMinecraftExternalLoaderInstaller
{
    Task RunAsync(
        MinecraftExternalLoaderInstallRequest request,
        IProgress<string>? output = null,
        CancellationToken cancellationToken = default);
}

public static class MinecraftLoaderInstallerArtifactResolver
{
    public static MinecraftLoaderInstallerArtifact Resolve(
        MinecraftLoaderKind kind,
        string gameVersion,
        string loaderVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(loaderVersion);

        return kind switch
        {
            MinecraftLoaderKind.Forge => ResolveForge(gameVersion, loaderVersion),
            MinecraftLoaderKind.NeoForge => ResolveNeoForge(gameVersion, loaderVersion),
            MinecraftLoaderKind.Cleanroom => ResolveCleanroom(loaderVersion),
            MinecraftLoaderKind.OptiFine => ResolveOptiFine(gameVersion, loaderVersion),
            _ => throw new NotSupportedException($"{kind} 不使用外部安装器。")
        };
    }

    private static MinecraftLoaderInstallerArtifact ResolveForge(string gameVersion, string loaderVersion)
    {
        string artifactVersion = gameVersion.Replace('-', '_') + "-" + loaderVersion;
        string relative = $"net/minecraftforge/forge/{artifactVersion}/forge-{artifactVersion}-installer.jar";
        return new MinecraftLoaderInstallerArtifact(
            MinecraftLoaderKind.Forge,
            "forge-installer.jar",
            [
                "https://maven.minecraftforge.net/" + relative,
                "https://bmclapi2.bangbang93.com/maven/" + relative
            ]);
    }

    private static MinecraftLoaderInstallerArtifact ResolveNeoForge(string gameVersion, string loaderVersion)
    {
        bool legacy = string.Equals(gameVersion, "1.20.1", StringComparison.OrdinalIgnoreCase);
        string artifact = legacy ? "forge" : "neoforge";
        string artifactVersion = legacy ? gameVersion + "-" + loaderVersion : loaderVersion;
        string relative = $"net/neoforged/{artifact}/{artifactVersion}/{artifact}-{artifactVersion}-installer.jar";
        return new MinecraftLoaderInstallerArtifact(
            MinecraftLoaderKind.NeoForge,
            "neoforge-installer.jar",
            [
                "https://maven.neoforged.net/releases/" + relative,
                "https://bmclapi2.bangbang93.com/maven/" + relative
            ]);
    }

    private static MinecraftLoaderInstallerArtifact ResolveCleanroom(string loaderVersion)
    {
        string fileName = $"cleanroom-{loaderVersion}-installer.jar";
        return new MinecraftLoaderInstallerArtifact(
            MinecraftLoaderKind.Cleanroom,
            fileName,
            [$"https://github.com/CleanroomMC/Cleanroom/releases/download/{Uri.EscapeDataString(loaderVersion)}/{fileName}"]);
    }

    private static MinecraftLoaderInstallerArtifact ResolveOptiFine(string gameVersion, string loaderVersion)
    {
        string prefix = gameVersion + "_";
        if (!loaderVersion.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("OptiFine 版本标识与 Minecraft 版本不匹配。");

        string[] parts = loaderVersion[prefix.Length..].Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            throw new FormatException("OptiFine 版本标识无效。");

        string type = string.Join('_', parts[..2]);
        string patch = string.Join('_', parts[2..]);
        return new MinecraftLoaderInstallerArtifact(
            MinecraftLoaderKind.OptiFine,
            "OptiFine_" + loaderVersion + ".jar",
            [$"https://bmclapi2.bangbang93.com/optifine/{Uri.EscapeDataString(gameVersion)}/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(patch)}"]);
    }
}

public sealed class MinecraftExternalLoaderInstaller : IMinecraftExternalLoaderInstaller
{
    public async Task RunAsync(
        MinecraftExternalLoaderInstallRequest request,
        IProgress<string>? output = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProcessStartInfo startInfo = CreateStartInfo(request);
        using Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        Queue<string> recentOutput = new();

        process.OutputDataReceived += (_, args) => CaptureLine(args.Data, recentOutput, output);
        process.ErrorDataReceived += (_, args) => CaptureLine(args.Data, recentOutput, output);
        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"无法启动 {request.Kind} 安装器。");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException($"无法启动 Java：{request.JavaExecutablePath}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        });

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string detail = recentOutput.Count == 0 ? "没有安装器输出。" : string.Join(Environment.NewLine, recentOutput);
            throw new InvalidOperationException($"{request.Kind} 安装器退出代码为 {process.ExitCode}。{Environment.NewLine}{detail}");
        }
    }

    private static ProcessStartInfo CreateStartInfo(MinecraftExternalLoaderInstallRequest request)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = request.JavaExecutablePath,
            WorkingDirectory = request.MinecraftRootDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (request.Kind == MinecraftLoaderKind.OptiFine)
        {
            string home = Directory.GetParent(request.MinecraftRootDirectory)?.FullName ?? request.MinecraftRootDirectory;
            startInfo.ArgumentList.Add("-Duser.home=" + home);
            startInfo.ArgumentList.Add("-cp");
            startInfo.ArgumentList.Add(request.InstallerPath);
            startInfo.ArgumentList.Add("optifine.Installer");
            startInfo.Environment["APPDATA"] = home;
        }
        else
        {
            startInfo.ArgumentList.Add("-jar");
            startInfo.ArgumentList.Add(request.InstallerPath);
            startInfo.ArgumentList.Add("--installClient");
            startInfo.ArgumentList.Add(request.MinecraftRootDirectory);
        }

        return startInfo;
    }

    private static void CaptureLine(string? line, Queue<string> recentOutput, IProgress<string>? output)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (recentOutput)
        {
            recentOutput.Enqueue(line);
            while (recentOutput.Count > 100)
                recentOutput.Dequeue();
        }
        output?.Report(line);
    }
}
