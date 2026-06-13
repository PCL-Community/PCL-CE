// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace PCL.Core.IO;

public readonly record struct FileSnapshot(
    string FullPath,
    string Name,
    long Length,
    DateTime LastWriteTimeUtc);

/// <summary>
/// 将没有原生异步 API 的目录操作调度到后台，并为大目录提供有界流式枚举。
/// </summary>
public static class FileSystemService
{
    public static async IAsyncEnumerable<FileSnapshot> EnumerateFilesAsync(
        string directory,
        string searchPattern = "*",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        var channel = Channel.CreateBounded<FileSnapshot>(
            new BoundedChannelOptions(64)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        using var enumerationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var producer = Task.Run(
            () => ProduceFileSnapshotsAsync(
                directory,
                searchPattern,
                channel.Writer,
                enumerationCancellation.Token),
            CancellationToken.None);

        try
        {
            await foreach (var snapshot in channel.Reader
                               .ReadAllAsync(cancellationToken)
                               .ConfigureAwait(false))
                yield return snapshot;
        }
        finally
        {
            await enumerationCancellation.CancelAsync().ConfigureAwait(false);
            await producer.ConfigureAwait(false);
        }
    }

    public static async Task<IReadOnlyList<FileSnapshot>> GetFilesAsync(
        string directory,
        string searchPattern = "*",
        CancellationToken cancellationToken = default)
    {
        var files = new List<FileSnapshot>();
        await foreach (var snapshot in EnumerateFilesAsync(
                           directory,
                           searchPattern,
                           cancellationToken).ConfigureAwait(false))
            files.Add(snapshot);
        return files;
    }

    public static Task CreateZipAsync(
        string destinationPath,
        IEnumerable<string> sourceFiles,
        CompressionLevel compressionLevel = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(sourceFiles);

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var fullSourcePaths = sourceFiles.Select(Path.GetFullPath).ToArray();
        var destinationDirectory = Path.GetDirectoryName(fullDestinationPath)
                                   ?? throw new ArgumentException(
                                       "Destination path has no parent directory.",
                                       nameof(destinationPath));

        return Task.Run(
            () => CreateZipCoreAsync(
                fullDestinationPath,
                destinationDirectory,
                fullSourcePaths,
                compressionLevel,
                cancellationToken),
            CancellationToken.None);
    }

    private static async Task CreateZipCoreAsync(
        string fullDestinationPath,
        string destinationDirectory,
        IReadOnlyList<string> fullSourcePaths,
        CompressionLevel compressionLevel,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        File.Delete(fullDestinationPath);
        try
        {
            await using var destination = new FileStream(
                fullDestinationPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 64 * 1024,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                });
            using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

            foreach (var fullSourcePath in fullSourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.CreateEntry(Path.GetFileName(fullSourcePath), compressionLevel);
                await using var source = new FileStream(
                    fullSourcePath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.Open,
                        Access = FileAccess.Read,
                        Share = FileShare.ReadWrite | FileShare.Delete,
                        BufferSize = 64 * 1024,
                        Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                    });
                await using var entryStream = entry.Open();
                await source.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            File.Delete(fullDestinationPath);
            throw;
        }
    }

    public static Task DeleteFilesExceptAsync(
        string directory,
        IReadOnlyCollection<string> excludedPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(excludedPaths);

        return Task.Run(
            () =>
            {
                var comparer = OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
                var excluded = new HashSet<string>(
                    excludedPaths.Select(Path.GetFullPath),
                    comparer);

                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!excluded.Contains(Path.GetFullPath(path)))
                        File.Delete(path);
                }
            },
            cancellationToken);
    }

    public static Task WriteAllLinesAsync(
        string path,
        IEnumerable<string> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(lines);
        return File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    private static async Task ProduceFileSnapshotsAsync(
        string directory,
        string searchPattern,
        ChannelWriter<FileSnapshot> writer,
        CancellationToken cancellationToken)
    {
        Exception? error = null;
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, searchPattern))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                await writer.WriteAsync(
                        new FileSnapshot(
                            info.FullName,
                            info.Name,
                            info.Length,
                            info.LastWriteTimeUtc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            writer.TryComplete(error);
        }
    }
}
