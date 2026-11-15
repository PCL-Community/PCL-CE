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
            RecoverPendingOperations();
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
            _TryReadRecord(originalPath, out _, out _, out _, out var previousRecordPath);
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
            var transactionPath = "";
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
                transactionPath = _WriteTransaction(new PendingOperation
                {
                    Operation = "Replace", Record = record, StagedPath = Path.GetRelativePath(_GameDirectory, stagedPath),
                    PreviousRecordFile = Path.GetFileName(previousRecordPath)
                });
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
                    if (transactionPath.Length > 0) _Delete(transactionPath);
                }
                catch (Exception restoreError)
                {
                    throw new AggregateException("更新失败，旧版仍保留在软件内回收站：" + backupPath,
                        updateError, restoreError);
                }
                throw;
            }
            // 连续更新只允许撤回最后一次，撤回后按钮应消失。
            if (!string.IsNullOrEmpty(previousRecordPath) && !_SamePath(previousRecordPath, recordPath))
                _Delete(previousRecordPath);
            _Delete(transactionPath);
            // 安装已提交，缓存清理失败不能再将成功的更新报告为失败。
            try { _Delete(downloadedPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public bool CanUndo(string updatedPath)
    {
        lock (_SyncRoot)
        {
            return _TryReadRecord(updatedPath, out _, out _, out _, out _);
        }
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

    public UpdateBatchResult ReplaceMany(IEnumerable<(string Original, string Downloaded, string Updated)> replacements)
    {
        var result = new UpdateBatchResult();
        foreach (var replacement in replacements)
        {
            try
            {
                Replace(replacement.Original, replacement.Updated, replacement.Downloaded);
                result.UpdatedPaths.Add(replacement.Updated);
            }
            catch (Exception ex)
            {
                result.Failures[replacement.Original] = ex;
            }
        }
        return result;
    }

    public sealed class UpdateBatchResult
    {
        public List<string> UpdatedPaths { get; } = new();
        public Dictionary<string, Exception> Failures { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 先暂存新版，再还原旧版，最后彻底删除新版；还原失败时恢复新版。
    /// </summary>
    public void Undo(string updatedPath)
    {
        lock (_SyncRoot)
        {
            RecoverPendingOperations();
            if (!_TryReadRecord(updatedPath, out var record, out var originalPath, out var backupPath, out var recordPath))
                throw new IOException("没有找到该资源对应的旧版，或当前资源或其备份已被修改。");
            updatedPath = _GetResourcePath(updatedPath);
            if (!_SamePath(originalPath, updatedPath) && (File.Exists(originalPath) || Directory.Exists(originalPath)))
                throw new IOException("旧版的原文件名已被占用，请先处理同名文件：" + originalPath);

            var stagedPath = Path.Combine(Path.GetDirectoryName(updatedPath)!,
                ".pcl-undo-" + Guid.NewGuid().ToString("N") + ".tmp");
            // 仅旧记录可能缺少旧版哈希，不能用当前备份覆盖更新时记录的校验值。
            if (string.IsNullOrEmpty(record.OriginalHash))
                record.OriginalHash = _HashFile(backupPath);
            else if (!_Matches(backupPath, record.OriginalHash))
                throw new IOException("资源旧版备份已被修改，无法撤回。");
            var transactionPath = _WriteTransaction(new PendingOperation
            {
                Operation = "Undo", Record = record, ResourcePath = Path.GetRelativePath(_GameDirectory, updatedPath),
                StagedPath = Path.GetRelativePath(_GameDirectory, stagedPath),
                UndoRecordFile = Path.GetFileName(recordPath)
            });
            var staged = false;
            var restored = false;
            try
            {
                _Move(updatedPath, stagedPath);
                staged = true;
                _Move(backupPath, originalPath);
                restored = true;
                _Delete(stagedPath);
            }
            catch (Exception undoError)
            {
                try
                {
                    if (restored) _Move(originalPath, backupPath);
                    if (staged) _Move(stagedPath, updatedPath);
                    _Delete(transactionPath);
                }
                catch (Exception restoreError)
                {
                    throw new AggregateException("撤回失败，请检查资源目录和软件内回收站。", undoError, restoreError);
                }
                throw;
            }
            _Delete(recordPath);
            _Delete(transactionPath);
        }
    }

    private bool _TryReadRecord(string updatedPath, out UpdateRecord record, out string originalPath, out string backupPath,
        out string recordPath)
    {
        record = null!;
        originalPath = backupPath = recordPath = "";
        try
        {
            updatedPath = _GetResourcePath(updatedPath);
            if (!File.Exists(updatedPath)) return false;
            var exactPath = _GetRecordPath(updatedPath);
            var candidates = new List<(UpdateRecord Record, string Path, string Original, string Backup)>();
            var recordsDirectory = _GetContainedPath(_RecycleDirectory, Path.GetDirectoryName(exactPath)!);
            if (!Directory.Exists(recordsDirectory)) return false;
            var paths = File.Exists(exactPath)
                ? new[] { exactPath }.Concat(Directory.EnumerateFiles(recordsDirectory, "*.json").Where(path => !_SamePath(path, exactPath)))
                : Directory.EnumerateFiles(recordsDirectory, "*.json");
            foreach (var candidatePath in paths)
            {
                UpdateRecord? candidate;
                string original, recordedUpdated, backup;
                try
                {
                    candidate = JsonSerializer.Deserialize<UpdateRecord>(File.ReadAllText(_GetUnlinkedPath(candidatePath)));
                    if (candidate is null) continue;
                    _ValidateRecord(candidate, out original, out recordedUpdated, out backup);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
                {
                    continue;
                }
                if (!_SamePath(updatedPath, recordedUpdated))
                {
                    // 只有原记录的文件已消失且没有同名兄弟副本时，才将名称变化解释为启用/禁用切换。
                    if (!_SamePath(_NormalizeResourcePath(updatedPath), _NormalizeResourcePath(recordedUpdated)) ||
                        File.Exists(recordedUpdated) || Directory.EnumerateFiles(Path.GetDirectoryName(updatedPath)!)
                            .Any(path => !_SamePath(path, updatedPath) &&
                                _SamePath(_NormalizeResourcePath(path), _NormalizeResourcePath(updatedPath)))) continue;
                }
                if (!File.Exists(backup)) continue;
                if (_SamePath(candidatePath, exactPath) && _SamePath(updatedPath, recordedUpdated))
                {
                    candidates.Clear();
                    candidates.Add((candidate, candidatePath, original, backup));
                    break;
                }
                candidates.Add((candidate, candidatePath, original, backup));
            }
            if (candidates.Count != 1) return false;
            (record, recordPath, originalPath, backupPath) = candidates[0];
            return _HashFile(updatedPath) == record.UpdatedHash &&
                (string.IsNullOrEmpty(record.OriginalHash) || _Matches(backupPath, record.OriginalHash));
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

    private void _ValidateRecord(UpdateRecord record, out string original, out string updated, out string backup)
    {
        if (string.IsNullOrWhiteSpace(record.OriginalPath) || string.IsNullOrWhiteSpace(record.UpdatedPath) ||
            string.IsNullOrWhiteSpace(record.BackupPath) || record.UpdatedHash is not { Length: 64 })
            throw new IOException("资源撤回记录不完整。");
        original = _GetResourcePath(Path.Combine(_GameDirectory, record.OriginalPath));
        updated = _GetResourcePath(Path.Combine(_GameDirectory, record.UpdatedPath));
        backup = _GetContainedPath(_RecycleDirectory, Path.Combine(_RecycleDirectory, record.BackupPath));
        var expected = Path.Combine(_RecycleDirectory, Path.GetRelativePath(_GameDirectory, Path.GetDirectoryName(original)!));
        if (!_SamePath(Path.GetDirectoryName(original)!, Path.GetDirectoryName(updated)!) ||
            !_SamePath(Path.GetDirectoryName(backup)!, expected) ||
            !(Path.GetFileName(backup) == Path.GetFileName(original) ||
                Path.GetFileName(backup).StartsWith(Path.GetFileName(original) + ".", StringComparison.OrdinalIgnoreCase)) ||
            record.UpdatedHash.Length != 64)
            throw new IOException("资源撤回记录的路径或校验信息无效。");
    }

    private string _WriteTransaction(PendingOperation operation)
    {
        var path = _GetContainedPath(_RecycleDirectory,
            Path.Combine(_RecycleDirectory, ".transactions", Guid.NewGuid().ToString("N") + ".json"));
        Directory.CreateDirectory(_GetUnlinkedPath(Path.GetDirectoryName(path)!));
        _WriteRecord(path, operation);
        return path;
    }

    /// <summary>在后台加载资源列表前恢复中断的文件移动；冲突时保留日志和所有文件。</summary>
    public void RecoverPendingOperations()
    {
        lock (_SyncRoot)
        {
            var directory = _GetContainedPath(_RecycleDirectory, Path.Combine(_RecycleDirectory, ".transactions"));
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                var operation = JsonSerializer.Deserialize<PendingOperation>(File.ReadAllText(_GetUnlinkedPath(path)))
                    ?? throw new IOException("资源恢复日志无效：" + path);
                _RecoverOperation(operation);
                _Delete(path);
            }
        }
    }

    private void _RecoverOperation(PendingOperation operation)
    {
        var record = operation.Record;
        _ValidateRecord(record, out var original, out var updated, out var backup);
        if (record.OriginalHash is not { Length: 64 }) throw new IOException("资源恢复日志缺少旧版校验信息。");
        if (operation.Operation == "Replace")
        {
            var recordPath = _GetRecordPath(updated);
            var previous = string.IsNullOrEmpty(operation.PreviousRecordFile) ? "" :
                _GetRecordedFile(operation.PreviousRecordFile);
            if (previous.Length > 0 && !_SamePath(previous, recordPath) && File.Exists(previous))
            {
                var previousRecord = JsonSerializer.Deserialize<UpdateRecord>(File.ReadAllText(_GetUnlinkedPath(previous)))
                    ?? throw new IOException("上一次资源撤回记录无效。");
                _ValidateRecord(previousRecord, out _, out var previousUpdated, out _);
                if (!_SamePath(_NormalizeResourcePath(previousUpdated), _NormalizeResourcePath(original)) ||
                    previousRecord.UpdatedHash != record.OriginalHash)
                    throw new IOException("上一次资源撤回记录与恢复日志不符。");
            }
            var staged = string.IsNullOrEmpty(operation.StagedPath) ? "" :
                _GetStagedPath(operation.StagedPath, updated, ".pcl-update-");
            if (staged.Length > 0 && File.Exists(staged) && !_Matches(staged, record.UpdatedHash))
                throw new IOException("更新暂存文件已被修改。");
            if (_Matches(backup, record.OriginalHash) && _Matches(updated, record.UpdatedHash))
            {
                _WriteRecord(recordPath, record);
                if (previous.Length > 0 && !_SamePath(previous, recordPath)) _Delete(previous);
                if (staged.Length > 0) _Delete(staged);
                return;
            }
            if (_Matches(backup, record.OriginalHash) && !File.Exists(original) && !File.Exists(updated))
            {
                _Move(backup, original);
                if (staged.Length > 0) _Delete(staged);
                return;
            }
            if (!File.Exists(backup) && _Matches(original, record.OriginalHash))
            {
                if (staged.Length > 0) _Delete(staged);
                return;
            }
        }
        else if (operation.Operation == "Undo")
        {
            updated = _GetResourcePath(Path.Combine(_GameDirectory, operation.ResourcePath));
            var staged = _GetStagedPath(operation.StagedPath, updated, ".pcl-undo-");
            if (!_SamePath(_NormalizeResourcePath(updated), _NormalizeResourcePath(
                    _GetResourcePath(Path.Combine(_GameDirectory, record.UpdatedPath)))))
                throw new IOException("资源撤回暂存路径无效。");
            var recordPath = _GetRecordedFile(operation.UndoRecordFile);
            if (File.Exists(recordPath))
            {
                var undoRecord = JsonSerializer.Deserialize<UpdateRecord>(File.ReadAllText(_GetUnlinkedPath(recordPath)))
                    ?? throw new IOException("资源撤回记录无效。");
                _ValidateRecord(undoRecord, out var recordedOriginal, out var recordedUpdated, out var recordedBackup);
                if (!_SamePath(recordedOriginal, original) || !_SamePath(recordedBackup, backup) ||
                    !_SamePath(recordedUpdated, _GetResourcePath(Path.Combine(_GameDirectory, record.UpdatedPath))) ||
                    undoRecord.UpdatedHash != record.UpdatedHash)
                    throw new IOException("资源撤回记录与恢复日志不符。");
            }
            if (_Matches(staged, record.UpdatedHash) && _Matches(backup, record.OriginalHash) && !File.Exists(original))
                _Move(backup, original);
            if (_Matches(original, record.OriginalHash) && !File.Exists(backup))
            {
                if (File.Exists(staged))
                {
                    if (!_Matches(staged, record.UpdatedHash)) throw new IOException("撤回暂存文件已被修改。");
                    _Delete(staged);
                }
                _Delete(recordPath);
                return;
            }
            if (!File.Exists(staged) && _Matches(updated, record.UpdatedHash) && _Matches(backup, record.OriginalHash))
                return;
        }
        throw new IOException("资源恢复遇到文件冲突，已保留文件和恢复日志：" + original);
    }

    private string _GetRecordedFile(string fileName)
    {
        if (Path.GetFileName(fileName) != fileName || fileName.Length != 69 ||
            !fileName.EndsWith(".json", StringComparison.Ordinal) || !fileName[..64].All(Uri.IsHexDigit))
            throw new IOException("资源记录文件名无效。");
        return _GetContainedPath(_RecycleDirectory, Path.Combine(_RecycleDirectory, ".records", fileName));
    }

    private string _GetStagedPath(string relativePath, string updated, string prefix)
    {
        var path = _GetResourcePath(Path.Combine(_GameDirectory, relativePath));
        var name = Path.GetFileName(path);
        if (!_SamePath(Path.GetDirectoryName(path)!, Path.GetDirectoryName(updated)!) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".tmp", StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..^4], "N", out _))
            throw new IOException("资源暂存路径无效。");
        return path;
    }

    private static bool _Matches(string path, string hash) => File.Exists(_GetUnlinkedPath(path)) && _HashFile(path) == hash;

    private sealed class PendingOperation
    {
        public PendingOperation() { }
        public string Operation { get; set; } = "";
        public UpdateRecord Record { get; set; } = new();
        public string ResourcePath { get; set; } = "";
        public string StagedPath { get; set; } = "";
        public string PreviousRecordFile { get; set; } = "";
        public string UndoRecordFile { get; set; } = "";
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
