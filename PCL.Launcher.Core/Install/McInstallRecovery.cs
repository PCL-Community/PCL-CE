using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.IO;

namespace PCL;

/// <summary>
/// 同一实例的安装与回滚共用一把跨进程锁。覆盖前保存核心文件和安装输出，工作线程退出后提交或还原。
/// </summary>
public static class McInstallRecovery
{
    public const string BackupFolderName = "PCLInstallBackups";

    private static readonly Lock SyncRoot = new();

    private static readonly Dictionary<string, InstallSession> Sessions =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

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

    /// <summary>在外层加载器组装完毕后占用目标目录；同一目录的回滚结束前不能开始下一次安装。</summary>
    public static void Start(
        Loading.LoaderBase loader,
        McInstallRequest request,
        string input,
        bool overwrite = false,
        bool requireNewFolder = false,
        string? modpackFile = null)
    {
        var folder = NormalizeFolder(request.targetInstanceFolder);
        var name = request.targetInstanceName;
        var session = new InstallSession(loader, folder, name);
        lock (SyncRoot)
        {
            if (!Sessions.TryAdd(folder, session))
                throw new Exception(Lang.Text("Minecraft.Download.Error.InstanceDownloading"));
        }
        try
        {
            session.Lease = InstallOutputs.AcquireLock(folder);
            var folderExisted = Directory.Exists(folder);
            var json = GetJsonPath(folder, name);
            var interrupted = session.Outputs.ReadInterruptedOrigin(recoverPending: true);
            var retry = interrupted?.HadJson == false;
            var hadJson = interrupted?.HadJson ?? File.Exists(json);
            session.CreatedFolder = interrupted?.CreatedFolder ?? !folderExisted;
            if (!overwrite && hadJson)
                throw new Exception(Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", name, ""));

            if (folderExisted && requireNewFolder && !retry)
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"));

            // 旧版本没有输出清单。无法区分上次解压的文件和用户数据时，避免将两个整合包叠加。
            if (modpackFile is not null && folderExisted && interrupted is null
                && HasUntrackedModpackOutputs(folder, name, modpackFile))
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"));

            session.Outputs.Begin(hadJson, session.CreatedFolder);
            session.Outputs.Register(session.CoreFiles, backUpExisting: true);
            if (modpackFile is not null)
            {
                var settings = Path.Combine(folder, "PCL");
                session.Outputs.Register(new[] { "Logo.png", "config.v1.yml", "config.v1.yml.bak" }
                    .Select(file => Path.Combine(settings, file)), backUpExisting: true);
            }
            var jar = GetJarPath(folder, name);
            request.OriginalMmcCoreJar = request.mmcPackInfo?.isMinecraftOverrided == true && hadJson
                ? session.Outputs.OriginalFile(jar) : null;
            if (!hadJson && File.Exists(jar))
                File.Delete(jar);
            loader.Workers = new Loading.LoaderWorkers();
            loader.Start(input);
        }
        catch
        {
            try
            {
                var rolledBack = session.Outputs.Finish(true);
                if (session.CreatedFolder)
                {
                    if (rolledBack) DeleteFolderQuietly(folder);
                    else TryDeleteEmptyFolder(folder);
                }
            }
            finally
            {
                Release(session);
            }
            throw;
        }
    }

    public static bool CanRetry(string folder) => TryReadInterruptedOrigin(folder) == false;

    // 下载器可能已移走原 JSON；覆盖确认应与 Start 使用同一份安装前状态。
    public static bool RequiresOverwrite(string folder, string name) =>
        TryReadInterruptedOrigin(folder) ?? File.Exists(GetJsonPath(folder, name));

    private static bool? TryReadInterruptedOrigin(string folder)
    {
        folder = NormalizeFolder(folder);
        lock (SyncRoot)
        {
            if (Sessions.ContainsKey(folder)) return null;
        }
        return InstallOutputs.TryReadInterruptedOrigin(folder);
    }

    private static bool HasUntrackedModpackOutputs(string folder, string name, string source)
    {
        var zip = Path.Combine(folder, "原始整合包.zip");
        var mrpack = Path.Combine(folder, "原始整合包.mrpack");
        var marker = Path.Combine(folder, ".pclignore");
        if (!File.Exists(zip) && !File.Exists(mrpack) && !File.Exists(marker)) return false;
        var known = new HashSet<string>(Sessions.Comparer)
        {
            zip, mrpack, marker, GetJarPath(folder, name), Path.GetFullPath(source)
        };
        return Directory.EnumerateFileSystemEntries(folder).Any(path => !known.Contains(path));
    }

    private static void Release(InstallSession session)
    {
        session.Outputs.Dispose();
        session.Lease?.Dispose();
        lock (SyncRoot)
        {
            if (Sessions.GetValueOrDefault(session.Folder) == session)
                Sessions.Remove(session.Folder);
        }
    }

    /// <summary>登记当前加载器的实例内输出；即将覆写用户文件时先保存原件。</summary>
    public static void TrackOutputs(Loading.LoaderBase loader, IEnumerable<string> paths, bool backUpExisting = false)
    {
        var owner = loader.RealParent ?? loader;
        InstallSession? session;
        lock (SyncRoot) session = Sessions.Values.FirstOrDefault(candidate => candidate.Owner == owner);
        if (session is null) return;
        lock (session.Outputs) session.Outputs.Register(paths, backUpExisting);
    }

    public static async Task HandleTerminalStateAsync(
        Loading.LoaderBase loader,
        string instanceFolder,
        bool keepBackupOnSuccess = false)
    {
        var state = loader.State;
        if (state is not (LoadState.Finished or LoadState.Failed or LoadState.Aborted))
            return;
        var folder = NormalizeFolder(instanceFolder);
        InstallSession? session;
        lock (SyncRoot)
        {
            if (!Sessions.TryGetValue(folder, out session) || session.Owner != loader || session.Finishing)
                return;
            session.Finishing = true;
        }
        try
        {
            if (loader.Workers is { } workers) await workers.FinishAsync().ConfigureAwait(false);
            var rollback = state != LoadState.Finished;
            session.Outputs.Finish(rollback, keepBackupOnSuccess ? session.CoreFiles : null);
            if (rollback)
            {
                if (session.CreatedFolder)
                    DeleteFolderQuietly(folder);
                else
                    TryDeleteEmptyFolder(folder);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"恢复实例目录失败：{folder}");
        }
        finally
        {
            InvalidateInstanceCache(folder);
            Release(session);
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

    private static void DeleteFolderQuietly(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;
            Directories.DeleteDirectory(folder);
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

    private sealed class InstallSession(Loading.LoaderBase owner, string folder, string name)
    {
        public Loading.LoaderBase Owner => owner;
        public string Folder => folder;
        public bool CreatedFolder;
        public bool Finishing;

        public FileStream? Lease;
        public string[] CoreFiles { get; } = [GetJsonPath(folder, name), GetJarPath(folder, name)];
        public InstallOutputs Outputs { get; } = new(folder);
    }
}
