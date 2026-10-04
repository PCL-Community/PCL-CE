using System;
using System.Collections.Generic;
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
public sealed class ResourceUpdateRecycleBin
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
        foreach (var folder in new[] { "mods", "resourcepacks", "shaderpacks", "schematics", "datapacks" })
            Directory.CreateDirectory(Path.Combine(_RecycleDirectory, folder));
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
            downloadedPath = Path.GetFullPath(downloadedPath);
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

            var backupPath = Path.Combine(_RecycleDirectory, Path.GetRelativePath(_GameDirectory, originalPath));
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
                UpdatedHash = _HashFile(downloadedPath)
            };
            var recordPath = _GetRecordPath(updatedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);
            var pendingRecordPath = recordPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backedUp = false;
            var installed = false;
            try
            {
                File.WriteAllText(pendingRecordPath, JsonSerializer.Serialize(record));
                File.Move(originalPath, backupPath);
                backedUp = true;
                File.Move(downloadedPath, updatedPath);
                installed = true;
                File.Move(pendingRecordPath, recordPath, true);
            }
            catch (Exception updateError)
            {
                try
                {
                    if (installed) File.Move(updatedPath, downloadedPath);
                    if (backedUp) File.Move(backupPath, originalPath);
                }
                catch (Exception restoreError)
                {
                    throw new AggregateException("更新失败，旧版仍保留在软件内回收站：" + backupPath,
                        updateError, restoreError);
                }
                throw;
            }
            finally
            {
                _TryDeleteRecord(pendingRecordPath);
            }

            // 连续更新只允许撤回最后一次，撤回后按钮应消失。
            var previousRecordPath = _GetRecordPath(originalPath);
            if (!_SamePath(previousRecordPath, recordPath)) _TryDeleteRecord(previousRecordPath);
        }
    }

    public bool CanUndo(string updatedPath)
    {
        lock (_SyncRoot)
            return _TryReadRecord(updatedPath, out _, out _, out _);
    }

    /// <summary>多选时跳过没有备份的资源；单个资源失败不影响其余资源。</summary>
    public UndoBatchResult UndoMany(IEnumerable<string> updatedPaths)
    {
        var result = new UndoBatchResult();
        lock (_SyncRoot)
        {
            foreach (var path in updatedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!CanUndo(path))
                    {
                        result.SkippedPaths.Add(path);
                        continue;
                    }
                    Undo(path);
                    result.RestoredPaths.Add(path);
                }
                catch (Exception ex)
                {
                    result.Failures.Add(path, ex);
                }
            }
        }
        return result;
    }

    public sealed class UndoBatchResult
    {
        public List<string> RestoredPaths { get; } = new();
        public List<string> SkippedPaths { get; } = new();
        public Dictionary<string, Exception> Failures { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 先暂存新版，再还原旧版，最后彻底删除新版；还原失败时恢复新版。
    /// </summary>
    public void Undo(string updatedPath)
    {
        lock (_SyncRoot)
        {
            if (!_TryReadRecord(updatedPath, out var record, out var originalPath, out var backupPath))
                throw new IOException("没有找到该资源对应的旧版，或当前资源已被修改。");
            updatedPath = _GetResourcePath(updatedPath);
            if (!_SamePath(originalPath, updatedPath) && (File.Exists(originalPath) || Directory.Exists(originalPath)))
                throw new IOException("旧版的原文件名已被占用，请先处理同名文件：" + originalPath);

            var stagedPath = Path.Combine(Path.GetDirectoryName(updatedPath)!,
                ".pcl-undo-" + Guid.NewGuid().ToString("N") + ".tmp");
            var staged = false;
            var restored = false;
            try
            {
                File.Move(updatedPath, stagedPath);
                staged = true;
                File.Move(backupPath, originalPath);
                restored = true;
                File.Delete(stagedPath); // 用户要求撤回时彻底删除升级后的资源。
            }
            catch (Exception undoError)
            {
                try
                {
                    if (restored) File.Move(originalPath, backupPath);
                    if (staged) File.Move(stagedPath, updatedPath);
                }
                catch (Exception restoreError)
                {
                    throw new AggregateException("撤回失败，请检查资源目录和软件内回收站。", undoError, restoreError);
                }
                throw;
            }
            _TryDeleteRecord(_GetRecordPath(Path.Combine(_GameDirectory, record.UpdatedPath)));
        }
    }

    private bool _TryReadRecord(string updatedPath, out UpdateRecord record, out string originalPath, out string backupPath)
    {
        record = null!;
        originalPath = backupPath = "";
        try
        {
            updatedPath = _GetResourcePath(updatedPath);
            var recordPath = _GetRecordPath(updatedPath);
            if (!File.Exists(updatedPath) || !File.Exists(recordPath)) return false;
            record = JsonSerializer.Deserialize<UpdateRecord>(File.ReadAllText(recordPath))!;
            if (record is null) return false;
            var recordedUpdatedPath = _GetResourcePath(Path.Combine(_GameDirectory, record.UpdatedPath));
            originalPath = _GetResourcePath(Path.Combine(_GameDirectory, record.OriginalPath));
            backupPath = _GetContainedPath(_RecycleDirectory, Path.Combine(_RecycleDirectory, record.BackupPath));
            var expectedBackupDirectory = Path.Combine(_RecycleDirectory,
                Path.GetRelativePath(_GameDirectory, Path.GetDirectoryName(originalPath)!));
            return _SamePath(_NormalizeResourcePath(updatedPath), _NormalizeResourcePath(recordedUpdatedPath)) &&
                _SamePath(Path.GetDirectoryName(updatedPath)!, Path.GetDirectoryName(originalPath)!) &&
                _SamePath(Path.GetDirectoryName(backupPath)!, expectedBackupDirectory) &&
                File.Exists(backupPath) && _HashFile(updatedPath) == record.UpdatedHash;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return false;
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
        path = Path.GetFullPath(path);
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("资源路径必须位于当前游戏目录内。", nameof(path));
        return path;
    }

    private string _GetRecordPath(string updatedPath)
    {
        var key = Path.GetRelativePath(_GameDirectory, _NormalizeResourcePath(updatedPath)).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_RecycleDirectory, ".records", hash + ".json");
    }

    private static string _NormalizeResourcePath(string path)
    {
        while (path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            path = path[..path.LastIndexOf('.')];
        return path;
    }

    private static bool _SamePath(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string _HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void _TryDeleteRecord(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class UpdateRecord
    {
        public UpdateRecord() { }
        public string OriginalPath { get; set; } = "";
        public string UpdatedPath { get; set; } = "";
        public string BackupPath { get; set; } = "";
        public string UpdatedHash { get; set; } = "";
    }
}
