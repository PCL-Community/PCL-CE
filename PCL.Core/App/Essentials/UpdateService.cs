using System;
using System.Diagnostics;
using System.IO;
using PCL.Core.App.IoC;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.BeforeLoading)]
public sealed class UpdateService : GeneralService
{
    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;

    private UpdateService() : base("update", "更新", false) { _context = ServiceContext; }

    public override void Start()
    {
        var args = Basics.CommandLineArguments;
        
        if (args is not ["update", _, _, _, _])
        {
            switch (args)
            {
                case ["update_finished", _]:
                {
                    var toDelete = args[1];
                    // 该参数来自命令行，不可信：若不校验，一个 update_finished "C:\Windows\..." 就是任意文件删除。
                    if (!_IsDeletableUpdateSource(toDelete))
                    {
                        Context.Warn($"已拒绝删除更新来源文件，路径不在允许范围内: {toDelete}");
                        break;
                    }

                    try
                    {
                        File.Delete(toDelete);
                        Context.Debug("更新来源文件已删除");
                    }
                    catch (Exception ex)
                    {
                        Context.Warn("更新来源文件删除失败", ex);
                    }

                    break;
                }
                case ["update_failed", _]:
                {
                    var reason = args[1];
                    Context.Error(
                        $"更新失败: {reason}\n你可以手动将 exe 文件替换为 PCL 目录中的新版本" +
                        $"或再次尝试更新，若再次尝试仍然失败，请尽快反馈这个问题");
                    break;
                }
                default: Context.Debug("无更新任务"); break;
            }
            Context.DeclareStopped();
            return;
        }

        try
        {
            Context.Info("开始更新");
            Lifecycle.PendingLogDirectory = Path.Combine(Basics.ExecutableDirectory, "Log");
            Lifecycle.PendingLogFileName = "LastPending_Update.log";

            var oldProcessId = args[1].Convert<int>();
            Context.Debug($"旧版本进程 ID: {oldProcessId}");
            try
            {
                var oldProcess = Process.GetProcessById(oldProcessId);
                Context.Debug("正在等待旧版本进程退出");
                oldProcess.WaitForExit();
                Context.Trace("旧版本进程已退出");
            }
            catch
            {
                /* ignored */
            }

            Context.Debug("正在替换文件");
            var target = args[2];
            Context.Trace($"目标: {target}");
            var source = args[3];
            Context.Trace($"来源: {source}");
            var ex = UpdateHelper.Replace(source, target);
            if (ex is null) Context.Trace("替换完成");
            else Context.Error("替换文件出错", ex);

            var restart = args[4].Convert<bool>();
            if (restart)
            {
                var restartArgs = (ex is null) ? $"finished \"{source}\"" : $"failed \"{ex.Message}\"";
                restartArgs = $"update_{restartArgs}";
                Context.Debug($"重启中，使用参数: {restartArgs}");
                Process.Start(target, restartArgs);
            }
        }
        catch (Exception ex)
        {
            Context.Error("更新过程出错", ex);
        }
        
        Context.RequestExit();
    }

    /// <summary>
    /// 判断更新来源文件是否位于允许删除的位置：启动器目录或临时目录。
    /// </summary>
    /// <param name="path">命令行传入的路径</param>
    /// <returns>可以安全删除则为 <see langword="true"/></returns>
    private static bool _IsDeletableUpdateSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) return false;
            return _IsUnderDirectory(fullPath, Basics.ExecutableDirectory)
                   || _IsUnderDirectory(fullPath, Path.GetTempPath());
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool _IsUnderDirectory(string fullPath, string directory)
    {
        var root = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
