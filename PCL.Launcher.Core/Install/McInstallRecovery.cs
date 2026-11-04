using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;

namespace PCL;

/// <summary>
///     安装会话的目录状态记录与失败恢复。
///     安装开始前记录实例目录中哪些路径是本次安装会创建或覆盖的，失败或取消后只回滚这些路径；
///     安装前已存在的实例核心文件会先备份到 <c>PCLInstallBackups</c>，失败时还原、成功时清理。
/// </summary>
public static class McInstallRecovery
{
    public const string BackupFolderName = "PCLInstallBackups";

    private static readonly Lock SyncRoot = new();

    private static readonly Dictionary<string, InstallSession> Sessions =
        new(StringComparer.OrdinalIgnoreCase);

    public static string NormalizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return "";
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "规范化实例目录失败");
            return folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    public static string GetJsonPath(string instanceFolder, string instanceName)
    {
        return Path.Combine(instanceFolder, $"{instanceName}.json");
    }

    public static string GetJarPath(string instanceFolder, string instanceName)
    {
        return Path.Combine(instanceFolder, $"{instanceName}.jar");
    }

    /// <summary>安装开始前调用：记录目录与实例文件的原始状态，供失败清理判断回滚范围。</summary>
    public static void RegisterSession(string instanceFolder, string instanceName)
    {
        var folder = NormalizeFolder(instanceFolder);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(instanceName))
            return;

        var session = new InstallSession
        {
            InstanceFolder = folder,
            InstanceName = instanceName,
            FolderExisted = Directory.Exists(folder),
            JsonExisted = File.Exists(GetJsonPath(folder, instanceName)),
            JarExisted = File.Exists(GetJarPath(folder, instanceName))
        };
        lock (SyncRoot)
        {
            Sessions[folder] = session;
        }

        LauncherCommon.Log(
            $"[Install] 记录安装会话：{folder}（目录已存在：{session.FolderExisted}，Json 已存在：{session.JsonExisted}）",
            LauncherLogLevel.Developer);
    }

    /// <summary>安装开始前调用：把已存在的实例核心文件备份到 <c>PCLInstallBackups</c>。</summary>
    public static void PrepareBackup(string instanceFolder, string instanceName)
    {
        var folder = NormalizeFolder(instanceFolder);
        var jsonPath = GetJsonPath(folder, instanceName);
        var jarPath = GetJarPath(folder, instanceName);
        if (!File.Exists(jsonPath) && !File.Exists(jarPath))
            return;

        var backupFolder = Path.Combine(folder, BackupFolderName);
        try
        {
            Directory.CreateDirectory(backupFolder);
            if (File.Exists(jsonPath))
                LauncherCommon.CopyFile(
                    jsonPath,
                    Path.Combine(backupFolder, Path.GetFileName(jsonPath)));
            if (File.Exists(jarPath))
                LauncherCommon.CopyFile(
                    jarPath,
                    Path.Combine(backupFolder, Path.GetFileName(jarPath)));
            LauncherCommon.Log($"[Install] 已备份实例核心文件：{backupFolder}");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "备份实例核心文件失败");
        }
    }

    public static void UnregisterSession(string instanceFolder)
    {
        var folder = NormalizeFolder(instanceFolder);
        lock (SyncRoot)
        {
            Sessions.Remove(folder);
        }
    }

    /// <summary>
    ///     安装加载器进入结束状态后调用：成功时删除备份，失败或取消时回滚到安装前状态。
    /// </summary>
    public static void HandleTerminalState(Loading.LoaderBase loader, string instanceFolder)
    {
        if (loader.State == LoadState.Finished)
        {
            var folder = NormalizeFolder(instanceFolder);
            DeleteBackupQuietly(folder);
            UnregisterSession(folder);
            return;
        }

        if (loader.State is not (LoadState.Failed or LoadState.Aborted))
            return;

        RestoreOrCleanup(instanceFolder);
    }

    public static bool TryClearIncompleteInstanceFolder(string instanceFolder, string instanceName)
    {
        var folder = NormalizeFolder(instanceFolder);
        if (!Directory.Exists(folder))
            return true;
        if (File.Exists(GetJsonPath(folder, instanceName)))
            return false;

        var backupFolder = Path.Combine(folder, BackupFolderName);
        if (Directory.Exists(backupFolder))
            try
            {
                LauncherCommon.DeleteDirectory(backupFolder);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "清理安装备份目录失败");
            }

        DeleteFileQuietly(GetJarPath(folder, instanceName));
        DeleteFileQuietly(Path.Combine(folder, ".pclignore"));
        TryDeleteEmptyFolder(folder);
        InvalidateInstanceCache(folder);
        LauncherCommon.Log($"[Install] 已清理未完成的实例目录：{folder}", LauncherLogLevel.Developer);
        return true;
    }

    private static void RestoreOrCleanup(string instanceFolder)
    {
        var folder = NormalizeFolder(instanceFolder);
        if (string.IsNullOrEmpty(folder))
            return;

        // 安装前已存在的核心文件备份：优先还原，绝不删除用户已有实例
        var backupFolder = Path.Combine(folder, BackupFolderName);
        if (Directory.Exists(backupFolder))
            try
            {
                LauncherCommon.CopyDirectory(backupFolder, folder);
                DeleteFileQuietly(Path.Combine(folder, ".pclignore"));
                LauncherCommon.DeleteDirectory(backupFolder);
                InvalidateInstanceCache(folder);
                LauncherCommon.Log($"[Install] 已从备份还原实例目录：{folder}");
                return;
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "还原实例备份失败");
            }

        InstallSession? session;
        lock (SyncRoot)
        {
            Sessions.TryGetValue(folder, out session);
        }

        var instanceName = session?.InstanceName ?? Path.GetFileName(folder);
        LauncherCommon.Log(
            $"[Install] 由于安装失败或取消，清理本次安装创建的路径：{folder}",
            LauncherLogLevel.Developer);

        DeleteFileQuietly(Path.Combine(folder, ".pclignore"));
        DeleteFileQuietly(GetJsonPath(folder, instanceName));
        DeleteFileQuietly(GetJarPath(folder, instanceName));

        var folderExistedBefore = session?.FolderExisted ?? false;
        if (!folderExistedBefore)
            DeleteFolderQuietly(folder);
        else
            TryDeleteEmptyFolder(folder);

        InvalidateInstanceCache(folder);
        UnregisterSession(folder);
    }

    private static void DeleteBackupQuietly(string folder)
    {
        var backupFolder = Path.Combine(folder, BackupFolderName);
        if (!Directory.Exists(backupFolder))
            return;
        try
        {
            LauncherCommon.DeleteDirectory(backupFolder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "清理安装备份目录失败");
        }
    }

    private static void InvalidateInstanceCache(string folder)
    {
        try
        {
            if (ConfigService.GetProvider(ConfigSource.GameInstance)
                is DynamicCacheConfigStorage storage)
                storage.InvalidateCache(folder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新实例配置缓存失败");
        }
    }

    private static void DeleteFileQuietly(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"删除安装残留文件失败：{path}");
        }
    }

    private static void DeleteFolderQuietly(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;
            LauncherCommon.DeleteDirectory(folder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"删除安装残留目录失败：{folder}");
        }
    }

    private static void TryDeleteEmptyFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"删除空实例目录失败：{folder}");
        }
    }

    private sealed class InstallSession
    {
        public bool FolderExisted;

        public required string InstanceFolder;
        public required string InstanceName;

        /// <summary>安装开始前实例核心 Jar 是否已存在（需要备份）。</summary>
        public bool JarExisted;

        /// <summary>安装开始前实例 Json 是否已存在（需要备份）。</summary>
        public bool JsonExisted;
    }
}