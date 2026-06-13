using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Storage;

public class HashStorage(string folder, IHashProvider hashProvider, bool compressObjects = false, bool correctMisplacedFile = true, int prefixLength = 2)
{
    /// <summary>
    /// 保存文件到哈希存储库中
    /// </summary>
    /// <param name="fromPath">欲存储的文件位置</param>
    /// <param name="hash">欲存储的文件的哈希，请确保与哈希存储库指定的哈希计算方法所用算法一致</param>
    /// <returns>成功返回文件的哈希，失败返回 null</returns>
    /// <exception cref="ArgumentNullException">提供的参数不正确</exception>
    public async Task<string?> PutAsync(
        string fromPath,
        string? hash = null,
        CancellationToken cancellationToken = default)
    {
        //参数检查
        ArgumentNullException.ThrowIfNull(fromPath);
        var filePath = Path.GetFullPath(fromPath);
        if (!File.Exists(filePath)) return null;
        //必要数据准备
        await using var originalFs = new FileStream(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 64 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });
        return await PutAsync(originalFs, hash, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> PutAsync(
        Stream input,
        string? hash = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (hash is not null && hash.Length != hashProvider.Length)
            throw new ArgumentException("Provide hash is not correct", nameof(hash));

        Stream source = input;
        FileStream? bufferedSource = null;
        try
        {
            if (!source.CanSeek)
            {
                Directory.CreateDirectory(folder);
                bufferedSource = new FileStream(
                    Path.Combine(folder, $".incoming-{Guid.NewGuid():N}.tmp"),
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        BufferSize = 64 * 1024,
                        Options = FileOptions.Asynchronous |
                                  FileOptions.SequentialScan |
                                  FileOptions.DeleteOnClose
                    });
                await source.CopyToAsync(bufferedSource, cancellationToken).ConfigureAwait(false);
                source = bufferedSource;
            }

            source.Position = 0;
            var fileHash = hash ??
                           (await hashProvider
                               .ComputeHashAsync(source, cancellationToken)
                               .ConfigureAwait(false))
                           .ToHexString();
            source.Position = 0;

            var destPath = _GetDestPath(fileHash);
            if (correctMisplacedFile && _CorrectMisplacedFile(fileHash))
                LogWrapper.Info("HashStorage", "Move misplaced file into correct folder");
            if (File.Exists(destPath)) return fileHash;

            var tempPath = $"{destPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var destinationFs = _GetSaveStream(tempPath))
                {
                    await source
                        .CopyToAsync(destinationFs, cancellationToken)
                        .ConfigureAwait(false);
                }

                try
                {
                    File.Move(tempPath, destPath, overwrite: false);
                }
                catch (IOException) when (File.Exists(destPath))
                {
                    // Another writer committed the same content first.
                }
            }
            finally
            {
                File.Delete(tempPath);
            }

            return fileHash;
        }
        finally
        {
            if (bufferedSource is not null)
                await bufferedSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    public Task<bool> DeleteAsync(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        var filePath = _GetDestPath(hash);
        if (!File.Exists(filePath) && correctMisplacedFile)
            filePath = _GetMisplacedFilePath(hash);

        if (!File.Exists(filePath)) return Task.FromResult(false);

        try
        {
            File.Delete(filePath);
        }
        catch (FileNotFoundException) { /* 忽略此错误 */ }
        catch (DirectoryNotFoundException ex)
        {
            LogWrapper.Error(ex, "HashStorage", $"Unexpected directory not found {filePath}");
            return Task.FromResult(false);
        }
        catch (IOException ex)
        {
            LogWrapper.Error(ex, "HashStorage", $"Failed to delete file {filePath}");
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogWrapper.Error(ex, "HashStorage", $"Access denied when deleting file {filePath}");
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    public Stream? Get(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var destPath = _GetDestPath(hash);
        if (correctMisplacedFile && _CorrectMisplacedFile(hash))
            LogWrapper.Info("HashStorage", $"Move misplaced file into correct folder: {hash}");

        return File.Exists(destPath) ? _GetReadStream(destPath) : null;
    }

    public bool Exists(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        return File.Exists(_GetDestPath(hash)) || (correctMisplacedFile && File.Exists(_GetMisplacedFilePath(hash)));
    }

    private Stream _GetSaveStream(string destPath)
    {
        var fs = new FileStream(
            destPath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 64 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });
        if (compressObjects) return new DeflateStream(fs, CompressionMode.Compress);
        return fs;
    }

    private Stream _GetReadStream(string destPath)
    {
        var fs = File.Open(destPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (compressObjects) return new DeflateStream(fs, CompressionMode.Decompress);
        return fs;
    }

    private string _GetDestPath(string hash)
    {
        return Path.Combine(folder, _GetPrefixFolder(hash), hash);
    }

    private bool _CorrectMisplacedFile(string hash)
    {
        var misplacedPath = _GetMisplacedFilePath(hash);
        if (!File.Exists(misplacedPath)) return false;
        var correctPath = _GetDestPath(hash);
        File.Move(misplacedPath, correctPath);
        return true;
    }

    private string _GetMisplacedFilePath(string hash)
    {
        return Path.Combine(folder, hash);
    }

    private string _GetPrefixFolder(string hash)
    {
        if (hash.Length < prefixLength)
            throw new ArgumentException($"Hash length({hash.Length}) is shorter than required prefix length({prefixLength})", nameof(hash));

        var folderName = hash[..prefixLength];
        var folderPath = Path.Combine(folder, folderName);
        Directory.CreateDirectory(folderPath);
        return folderName;
    }
}
