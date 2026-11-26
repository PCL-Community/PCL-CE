using PCL.Core.App;
using PCL.Core.IO;

namespace PCL;

/// <summary>记录本次安装新建的路径及覆写前的文件。实例内只保存编号，清单和原件保存在启动器的数据目录。</summary>
internal sealed class InstallOutputs(string folder) : IDisposable
{
    // 旧清单未记录目录归属，按已有目录处理，保留未登记的文件。
    internal sealed record Origin(string Folder, bool HadJson, bool CreatedFolder = false);
    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly HashSet<string> _paths = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private string? _previous;
    private StreamWriter? _writer;
    private string Marker => Path.Combine(folder, ".pcl-install");
    private static string Journal(string id) => Path.Combine(Paths.SharedLocalData, "InstallRecovery", id);

    // 锁文件名独立于每次安装的编号。释放时保留文件，避免另一个进程锁住新建的同名文件。
    public static FileStream AcquireLock(string folder)
    {
        var target = new DirectoryInfo(folder);
        if (target.LinkTarget is not null)
            target = (DirectoryInfo)target.ResolveLinkTarget(returnFinalTarget: true)!;
        var parent = target.Parent ?? throw new IOException($"实例目录缺少上级目录：{folder}");
        parent.Create();
        var lockFile = Path.Combine(parent.FullName, $".{target.Name}.pcl-install.lock");
        return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private Origin? ReadOrigin(string journal, bool pending = false)
    {
        using var reader = File.OpenText(journal);
        var header = reader.ReadLine();
        if (pending && (reader.Peek() != -1 || Directory.Exists(journal + ".files"))) return null;
        Origin? origin;
        try
        {
            origin = JsonSerializer.Deserialize<Origin>(header ?? "");
        }
        catch (JsonException) when (pending)
        {
            return null;
        }
        return origin?.Folder is { } recorded && MatchesOriginFolder(recorded) ? origin : null;
    }

    private bool MatchesOriginFolder(string recorded)
    {
        if (recorded == folder) return true;
        if (!Directory.Exists(recorded) || !Files.ArePathsEqual(recorded, folder)) return false;

        // macOS 也支持区分大小写的 APFS 卷。若同一父目录里有两个不同的拼写，
        // 即使用户复制了标记，也不能用另一个实例的清单回滚当前实例。
        var original = new DirectoryInfo(recorded);
        var requested = new DirectoryInfo(folder);
        while (original.FullName != requested.FullName
               && original.Parent is { } parent && requested.Parent is { } requestedParent)
        {
            if (original.Name != requested.Name && parent.EnumerateDirectories()
                    .Where(dir => string.Equals(dir.Name, original.Name, StringComparison.OrdinalIgnoreCase))
                    .Skip(1).Any())
                return false;
            original = parent;
            requested = requestedParent;
        }
        return true;
    }

    public Origin? ReadInterruptedOrigin(bool recoverPending = false)
    {
        if (!Directory.Exists(folder)) return null;
        Origin? origin = null;
        var marker = new FileInfo(Marker);
        if (marker.Exists)
        {
            if (marker.Length != 32 || !Guid.TryParseExact(File.ReadAllText(Marker), "N", out var id))
                throw new IOException($"安装恢复标记无效：{Marker}");
            var journal = Journal(id.ToString("N"));
            var committed = journal + ".committed";
            var finished = File.Exists(committed);
            var recorded = ReadOrigin(finished ? committed : journal)
                ?? throw new IOException($"恢复清单与实例目录不匹配：{folder}");
            if (!finished)
            {
                _previous = journal;
                origin = recorded;
                if (!recoverPending) return origin;
            }
            else if (recoverPending)
            {
                File.Delete(Path.Combine(folder, ".pclignore"));
                File.Delete(Marker);
                DeleteJournal(journal);
                File.Delete(committed);
            }
        }

        // 清单在临时标记之前落盘。即使写标记时退出，也能按文件名校验清单归属。
        const string prefix = ".pcl-install.";
        foreach (var pending in Directory.EnumerateFiles(folder, prefix + "*"))
        {
            var name = Path.GetFileName(pending);
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length != prefix.Length + 32) continue;
            var suffix = name[prefix.Length..];
            if (!Guid.TryParseExact(suffix, "N", out var id)) continue;
            var journal = Journal(id.ToString("N"));
            var info = new FileInfo(pending);
            if (!File.Exists(journal) || info.LinkTarget is not null || info.Length > 32
                || !suffix.StartsWith(File.ReadAllText(pending), StringComparison.Ordinal)) continue;
            if (journal == _previous)
            {
                if (recoverPending) File.Delete(pending);
                continue;
            }
            if (ReadOrigin(journal, pending: true) is not { } candidate) continue;
            if (origin is null)
            {
                if (!recoverPending) return candidate;
                File.WriteAllText(pending, id.ToString("N"));
                File.Move(pending, Marker);
                _previous = journal;
                origin = candidate;
            }
            else if (recoverPending)
            {
                File.Delete(pending);
                DeleteJournal(journal);
            }
        }
        return origin;
    }

    public static bool? TryReadInterruptedOrigin(string folder)
    {
        try
        {
            if (!Directory.Exists(folder) || (!File.Exists(Path.Combine(folder, ".pcl-install"))
                && !Directory.EnumerateFiles(folder, ".pcl-install.*").Any())) return null;
            using var lease = AcquireLock(folder);
            return new InstallOutputs(folder).ReadInterruptedOrigin()?.HadJson;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Begin(bool hadJson, bool createdFolder)
    {
        if (_previous is not null)
        {
            using var reader = File.OpenText(_previous);
            reader.ReadLine();
            // 记录先于对应的写入完成。最后一条未写完的记录没有对应产物。
            var records = reader.ReadToEnd().Split('\n').SkipLast(1);
            Restore(_previous, records.Select(line => JsonSerializer.Deserialize<string>(line)!));
        }

        Directory.CreateDirectory(folder);
        var journal = Journal(_id);
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        var writer = new StreamWriter(new FileStream(journal, FileMode.CreateNew, FileAccess.Write, FileShare.None));
        var pendingMarker = Marker + "." + _id;
        try
        {
            writer.WriteLine(JsonSerializer.Serialize(new Origin(folder, hadJson, createdFolder)));
            writer.Flush();
            File.WriteAllText(pendingMarker, _id);
            // 还原成功后才替换旧标记。进程在此之前退出，下次仍可重放完整的旧清单。
            File.Move(pendingMarker, Marker, overwrite: _previous is not null);
            _writer = writer;
        }
        finally
        {
            if (_writer is null)
            {
                writer.Dispose();
                File.Delete(journal);
            }
            File.Delete(pendingMarker);
        }
        if (_previous is not null) DeleteJournal(_previous);
        var ignore = Path.Combine(folder, ".pclignore");
        File.Delete(ignore);
        File.WriteAllText(ignore, "");
    }

    public void Register(IEnumerable<string> paths, bool backUpExisting, bool savingCoreBackup = false)
    {
        foreach (var path in paths)
        {
            if (!Files.IsPathWithinDirectory(path, folder)) continue;
            var relative = Path.GetRelativePath(folder, path);
            var top = relative.Split(Path.DirectorySeparatorChar, 2)[0];
            if (string.Equals(top, ".pcl-install", StringComparison.OrdinalIgnoreCase)
                || string.Equals(top, ".pclignore", StringComparison.OrdinalIgnoreCase)
                || (!savingCoreBackup && string.Equals(top, McInstallRecovery.BackupFolderName, StringComparison.OrdinalIgnoreCase)))
                throw new IOException($"安装文件与恢复数据重名：{relative}");
            if (HasLinkedParent(path) || new FileInfo(path).LinkTarget is not null)
                throw new IOException($"无法通过链接登记安装文件：{path}");
            if (File.Exists(path))
            {
                if (backUpExisting && !_paths.Contains(relative))
                    BackUpFile(path, relative);
                continue;
            }
            if (Directory.Exists(path)) continue;
            Record(relative);
            var parent = Path.GetDirectoryName(relative);
            while (!string.IsNullOrEmpty(parent) && !Directory.Exists(Path.Combine(folder, parent)))
            {
                if (!Record(parent + Path.DirectorySeparatorChar)) break;
                parent = Path.GetDirectoryName(parent);
            }
        }
        _writer!.Flush();
    }

    private void BackUpFile(string path, string relative)
    {
        var journal = Journal(_id);
        var copy = Path.Combine(journal + ".files", relative);
        if (File.Exists(copy)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        // 只有复制完毕的文件才能用于恢复。
        var temp = journal + ".tmp";
        try
        {
            File.Copy(path, temp, true);
            File.Move(temp, copy);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private bool Record(string relative)
    {
        if (!_paths.Add(relative)) return false;
        _writer!.WriteLine(JsonSerializer.Serialize(relative));
        return true;
    }

    public string? OriginalFile(string path)
    {
        var copy = Path.Combine(Journal(_id) + ".files", Path.GetRelativePath(folder, path));
        return File.Exists(copy) ? copy : null;
    }

    private void SaveCoreBackup(IEnumerable<string> coreFiles)
    {
        var destination = Path.Combine(folder, McInstallRecovery.BackupFolderName);
        var staging = Path.Combine(destination, "." + Path.GetRandomFileName());
        var pending = Path.Combine(staging, "pending");
        var published = new List<string>();
        try
        {
            foreach (var path in coreFiles)
            {
                if (OriginalFile(path) is not { } original) continue;
                var target = Path.Combine(destination, Path.GetFileName(path));
                var prior = Path.Combine(staging, Path.GetFileName(path));
                Register([target, pending, prior], backUpExisting: true, savingCoreBackup: true);
                Directory.CreateDirectory(staging);
                File.Copy(original, pending);
                if (File.Exists(target))
                {
                    if (File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly))
                        throw new UnauthorizedAccessException($"备份文件为只读：{target}");
                    File.Replace(pending, target, prior);
                }
                else
                    File.Move(pending, target);
                published.Add(target);
            }
        }
        catch
        {
            // 两个核心文件属于同一套备份。如果第二个发布失败，撤回第一个。
            foreach (var target in published)
            {
                var prior = Path.Combine(staging, Path.GetFileName(target));
                if (File.Exists(prior)) File.Move(prior, target, overwrite: true);
                else File.Delete(target);
            }
            Directories.DeleteDirectory(staging);
            throw;
        }
        Directories.DeleteDirectory(staging);
    }

    public bool Finish(bool rollback, IEnumerable<string>? coreBackup = null)
    {
        if (_writer is null) return false;
        var journal = Journal(_id);
        var ignore = Path.Combine(folder, ".pclignore");
        try
        {
            if (rollback)
            {
                Restore(journal, _paths);
                File.Delete(ignore);
            }
            else if (coreBackup is not null)
            {
                try
                {
                    SaveCoreBackup(coreBackup);
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, $"安装已完成，未能保留核心备份：{folder}", LauncherLogLevel.Hint);
                }
            }
        }
        finally
        {
            _writer.Dispose();
            _writer = null;
        }
        try
        {
            File.Delete(Marker);
        }
        catch (Exception ex) when (!rollback && ex is (IOException or UnauthorizedAccessException))
        {
            // 实例目录被设为只读或标记被占用时，在启动器自己的目录里完成提交。
            File.Move(journal, journal + ".committed");
            LauncherCommon.Log(ex, $"安装已完成，稍后清理恢复标记：{Marker}");
        }
        if (!rollback)
        {
            try
            {
                File.Delete(ignore);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LauncherCommon.Log(ex, $"安装已完成，未能清理忽略标记：{ignore}", LauncherLogLevel.Hint);
            }
        }
        DeleteJournal(journal);
        return true;
    }

    public void Dispose() => _writer?.Dispose();

    private static void DeleteJournal(string journal)
    {
        Directories.DeleteDirectory(journal + ".files");
        File.Delete(journal + ".tmp");
        File.Delete(journal);
    }

    private void Restore(string journal, IEnumerable<string> paths)
    {
        // 子项先于父目录删除。用户后来放入的文件会阻止删除其父目录。
        foreach (var relative in paths.OrderByDescending(path => path.Length))
        {
            var path = Path.TrimEndingDirectorySeparator(Path.Combine(folder, relative));
            if (!Files.IsPathWithinDirectory(path, folder)) continue;
            if (HasLinkedParent(path)) continue;
            if (Path.EndsInDirectorySeparator(relative))
            {
                if (Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is null
                    && !Directory.EnumerateFileSystemEntries(path).Any())
                    Directory.Delete(path);
            }
            else if (File.Exists(path))
                File.Delete(path);
        }
        var backups = journal + ".files";
        if (!Directory.Exists(backups)) return;
        foreach (var copy in Directory.EnumerateFiles(backups, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(folder, Path.GetRelativePath(backups, copy));
            if (HasLinkedParent(target) || new FileInfo(target).LinkTarget is not null)
                throw new IOException($"无法通过链接还原文件：{target}");
            Files.CopyFile(copy, target);
        }
    }

    private bool HasLinkedParent(string path)
    {
        var parent = new DirectoryInfo(Path.GetDirectoryName(path)!);
        while (!string.Equals(parent.FullName, folder,
                   OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (parent.LinkTarget is not null || parent.Parent is null) return true;
            parent = parent.Parent;
        }
        return false;
    }
}
