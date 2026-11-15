using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PCL.Core.Minecraft;

/// <summary>
/// 保存资源更新前的文件，以及新版与旧版的一次性撤回关系。
/// 普通删除仍由资源管理页面发送到 Windows 回收站。
/// </summary>
public sealed partial class ResourceUpdateRecycleBin
{
    private static readonly object _SyncRoot = new();
    private readonly string _GameDirectory;
    private readonly string _RecycleDirectory;

    public ResourceUpdateRecycleBin(string gameDirectory)
    {
        _GameDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
        _RecycleDirectory = Path.Combine(_GameDirectory, "recycle");
    }

    public void InitializeFolders()
    {
        var folders = new[] { "mods", "resourcepacks", "shaderpacks", "schematics", "datapacks" }
            .Select(folder => _GetContainedPath(_RecycleDirectory, Path.Combine(_RecycleDirectory, folder))).ToArray();
        foreach (var folder in folders) Directory.CreateDirectory(_GetContainedPath(_RecycleDirectory, folder));
    }

    /// <summary>
    /// 下载成功后才移动旧版。新版安装或撤回记录写入失败时，将旧版移回。
    /// </summary>
    public void Replace(string originalPath, string updatedPath, string downloadedPath)
    {
        lock (_SyncRoot)
        {
            originalPath = _GetResourcePath(originalPath);
            updatedPath = _GetResourcePath(updatedPath);
            downloadedPath = _GetUnlinkedPath(downloadedPath);
            if (!_SamePath(Path.GetDirectoryName(originalPath)!, Path.GetDirectoryName(updatedPath)!))
                throw new IOException("更新后的资源必须位于原来的文件夹中。");
            if (!File.Exists(originalPath))
                throw new FileNotFoundException("未找到更新前的资源文件。", originalPath);
            if (!File.Exists(downloadedPath))
                throw new FileNotFoundException("未找到已下载的资源文件。", downloadedPath);
            if (_SamePath(downloadedPath, originalPath) || _SamePath(downloadedPath, updatedPath))
                throw new IOException("下载缓存不能与资源文件使用相同路径。");
            if (!_SamePath(originalPath, updatedPath) && (File.Exists(updatedPath) || Directory.Exists(updatedPath)))
                throw new IOException("更新后的文件名已被其他资源占用，请先处理同名文件：" + updatedPath);

            var backupPath = _GetContainedPath(_RecycleDirectory,
                Path.Combine(_RecycleDirectory, Path.GetRelativePath(_GameDirectory, originalPath)));
            var recordPath = _GetRecordPath(updatedPath);
            var previousRecordPath = _GetRecordPath(originalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            // 同名备份不能覆盖之前保留的旧版。
            while (File.Exists(backupPath) || Directory.Exists(backupPath))
                backupPath = Path.Combine(Path.GetDirectoryName(backupPath)!,
                    Path.GetFileName(originalPath) + "." + Guid.NewGuid().ToString("N"));

            var record = new UpdateRecord
            {
                OriginalPath = Path.GetRelativePath(_GameDirectory, originalPath),
                UpdatedPath = Path.GetRelativePath(_GameDirectory, updatedPath),
                BackupPath = Path.GetRelativePath(_RecycleDirectory, backupPath),
                OriginalHash = _HashFile(originalPath)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);
            var stagedPath = Path.Combine(Path.GetDirectoryName(updatedPath)!, ".pcl-update-" + Guid.NewGuid().ToString("N") + ".tmp");
            var backedUp = false;
            var installed = false;
            try
            {
                // 缓存可能位于其他磁盘，先在资源目录内完成复制，后续移动才能保持在同一卷中。
                using (var source = File.OpenRead(_GetUnlinkedPath(downloadedPath)))
                using (var staged = new FileStream(_GetUnlinkedPath(stagedPath), FileMode.CreateNew, FileAccess.Write))
                {
                    source.CopyTo(staged);
                    staged.Flush(true);
                }
                record.UpdatedHash = _HashFile(stagedPath);
                _Move(originalPath, backupPath);
                backedUp = true;
                _Move(stagedPath, updatedPath);
                installed = true;
                _WriteRecord(recordPath, record);
            }
            catch (Exception updateError)
            {
                try
                {
                    if (installed) _Move(updatedPath, stagedPath);
                    if (backedUp) _Move(backupPath, originalPath);
                    _Delete(stagedPath);
                }
                catch (Exception restoreError)
                {
                    throw new AggregateException("更新失败，旧版仍保留在软件内回收站：" + backupPath,
                        updateError, restoreError);
                }
                throw;
            }
            // 连续更新只允许撤回最后一次，撤回后按钮应消失。
            if (File.Exists(previousRecordPath) && !_SamePath(previousRecordPath, recordPath))
                _Delete(previousRecordPath);
            // 安装已提交，缓存清理失败不能再将成功的更新报告为失败。
            try { _Delete(downloadedPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private string _GetResourcePath(string path)
    {
        path = _GetContainedPath(_GameDirectory, path);
        if (path.StartsWith(_RecycleDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            _SamePath(path, _RecycleDirectory))
            throw new ArgumentException("资源路径不能位于软件内回收站中。", nameof(path));
        return path;
    }

    private static string _GetContainedPath(string directory, string path)
    {
        directory = Path.GetFullPath(directory);
        path = _GetUnlinkedPath(path);
        var prefix = Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("资源路径必须位于当前游戏目录内。", nameof(path));
        return path;
    }

    private string _GetRecordPath(string updatedPath)
    {
        var key = Path.GetRelativePath(_GameDirectory, updatedPath).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return _GetContainedPath(_RecycleDirectory, Path.Combine(_RecycleDirectory, ".records", hash + ".json"));
    }

    private static bool _SamePath(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string _HashFile(string path)
    {
        using var stream = File.OpenRead(_GetUnlinkedPath(path));
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string _GetUnlinkedPath(string path)
    {
        path = Path.GetFullPath(path);
        for (var component = path; component is not null; component = Path.GetDirectoryName(component))
        {
            try
            {
                if ((File.GetAttributes(component) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("资源更新不支持符号链接或目录联接：" + component);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return path;
    }

    private static void _Move(string source, string destination) =>
        File.Move(_GetUnlinkedPath(source), _GetUnlinkedPath(destination));

    private static void _Delete(string path) => File.Delete(_GetUnlinkedPath(path));

    private static void _WriteRecord<T>(string path, T record)
    {
        path = _GetUnlinkedPath(path);
        Directory.CreateDirectory(_GetUnlinkedPath(Path.GetDirectoryName(path)!));
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(_GetUnlinkedPath(temporaryPath), FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, record);
                stream.Flush(true);
            }
            File.Move(_GetUnlinkedPath(temporaryPath), _GetUnlinkedPath(path), true);
        }
        finally { _Delete(temporaryPath); }
    }

    private sealed class UpdateRecord
    {
        public UpdateRecord() { }
        public string OriginalPath { get; set; } = "";
        public string UpdatedPath { get; set; } = "";
        public string BackupPath { get; set; } = "";
        public string UpdatedHash { get; set; } = "";
        public string OriginalHash { get; set; } = "";
    }
}
