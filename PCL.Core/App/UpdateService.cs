using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App;

[LifecycleService(LifecycleState.BeforeLoading)]
[LifecycleScope("update", "处理更新参数")]
public sealed partial class UpdateService
{
    [LifecycleStart]
    private static async Task _Start()
    {
        var args = Basics.CommandLineArguments;
        
        if (args is not ["update", _, _, _, _])
        {
            switch (args)
            {
                case ["update_finished", _]:
                {
                    var toDelete = args[1];
                    File.Delete(toDelete);
                    Context.Debug("更新来源文件已删除");
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
                await oldProcess.WaitForExitAsync();
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
            var ex = Replace(source, target);
            if (ex == null) Context.Trace("替换完成");
            else Context.Error("替换文件出错", ex);

            var restart = args[4].Convert<bool>();
            if (restart)
            {
                var restartArgs = ex == null ? $"finished \"{source}\"" : $"failed \"{ex.Message}\"";
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
    /// 更新启动器（替换文件）
    /// </summary>
    /// <param name="source">用于替换的来源文件路径</param>
    /// <param name="target">目标文件路径</param>
    public static Exception? Replace(string source, string target)
    {
        var backup = $"{target}.bak.{DateTime.Now:yyyyMMddHHmmss}";
        Exception? lastEx = null;
        try
        {
            source = Path.GetFullPath(source);
            target = Path.GetFullPath(target);
            // 备份目标文件
            File.Copy(target, backup);
            if (!File.Exists(backup)) throw new FileNotFoundException("备份目标文件失败", backup);
            // 删除原文件并等待文件删除事件
            var watcher = new FileSystemWatcher(Basics.GetParentPathOrEmpty(target), Path.GetFileName(target));
            var deletedEvent = new ManualResetEventSlim(false);
            watcher.Deleted += (_, _) => deletedEvent.Set();
            watcher.EnableRaisingEvents = true;
            File.Delete(target);
            if (!deletedEvent.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("删除目标文件失败");
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            // 复制到目标文件
            File.Copy(source, target);
            if (!File.Exists(target)) throw new FileNotFoundException("复制到目标文件失败", target);
        }
        catch (Exception ex)
        {
            // 出错：恢复原文件并返回异常
            if (File.Exists(backup) && !File.Exists(target)) File.Move(backup, target);
            lastEx = ex;
        }
        if (File.Exists(backup)) File.Delete(backup); // 删除备份文件
        return lastEx;
    }
}