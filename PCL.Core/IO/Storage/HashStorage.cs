using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Utils.Hash;

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
    public async Task<string?> PutAsync(string fromPath, string? hash = null)
    {
        //参数检查
        ArgumentNullException.ThrowIfNull(fromPath);
        var filePath = Path.GetFullPath(fromPath);
        if (!File.Exists(filePath)) return null;
        //必要数据准备
        await using var originalFs = File.Open(fromPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await PutAsync(originalFs, hash);
    }

    public async Task<string?> PutAsync(Stream input, string? hash = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (hash is not null && hash.Length != hashProvider.Length)
            throw new ArgumentException("Provide hash is not correct", nameof(hash));

        if (input.CanSeek) input.Position = 0;

        var fileHash = hash ?? hashProvider.ComputeHash(input);
        var destPath = _getDestPath(fileHash);
        //纠正: 由于之前错误设计导致的文件访问效率低下的文件结构
        if (correctMisplacedFile && _correctMisplacedFile(fileHash)) LogWrapper.Info("HashStorage", "Move misplaced file into correct folder");
        //检查是否已存在保存的文件
        if (File.Exists(destPath)) return fileHash;
        await using var destinationFs = _getSaveStream(destPath);
        await input.CopyToAsync(destinationFs);
        return fileHash;
    }

    public async Task<bool> DeleteAsync(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        var filePath = _getDestPath(hash);
        if (!File.Exists(filePath) && correctMisplacedFile)
            filePath = _getMisplacedFilePath(hash);

        if (!File.Exists(filePath)) return false;

        try
        {
            await Task.Run(() => File.Delete(filePath));
        }
        catch (FileNotFoundException) { /* 忽略此错误 */ }
        catch (DirectoryNotFoundException ex)
        {
            LogWrapper.Error(ex, "HashStorage", $"Unexpected directory not found {filePath}");
            return false;
        }
        catch (IOException ex)
        {
            LogWrapper.Error(ex, "HashStorage", $"Failed to delete file {filePath}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogWrapper.Error(ex, "HashStorage", $"Access denied when deleting file {filePath}");
            return false;
        }
        return true;
    }

    public Stream? Get(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var destPath = _getDestPath(hash);
        if (correctMisplacedFile && _correctMisplacedFile(hash))
            LogWrapper.Info("HashStorage", $"Move misplaced file into correct folder: {hash}");

        return File.Exists(destPath) ? _getReadStream(destPath) : null;
    }

    public bool Exists(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        return File.Exists(_getDestPath(hash)) || (correctMisplacedFile && File.Exists(_getMisplacedFilePath(hash)));
    }

    private Stream _getSaveStream(string destPath)
    {
        var fs = File.Open(destPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        if (compressObjects) return new DeflateStream(fs, CompressionMode.Compress);
        return fs;
    }

    private Stream _getReadStream(string destPath)
    {
        var fs = File.Open(destPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (compressObjects) return new DeflateStream(fs, CompressionMode.Decompress);
        return fs;
    }

    private string _getDestPath(string hash)
    {
        return Path.Combine(folder, _getPrefixFolder(hash), hash);
    }

    private bool _correctMisplacedFile(string hash)
    {
        var misplacedPath = _getMisplacedFilePath(hash);
        if (!File.Exists(misplacedPath)) return false;
        var correctPath = _getDestPath(hash);
        File.Move(misplacedPath, correctPath);
        return true;
    }

    private string _getMisplacedFilePath(string hash)
    {
        return Path.Combine(folder, hash);
    }

    private string _getPrefixFolder(string hash)
    {
        if (hash.Length < prefixLength)
            throw new ArgumentException($"Hash length({hash.Length}) is shorter than required prefix length({prefixLength})", nameof(hash));

        var folderName = hash[..prefixLength];
        var folderPath = Path.Combine(folder, folderName);
        Directory.CreateDirectory(folderPath);
        return folderName;
    }
}