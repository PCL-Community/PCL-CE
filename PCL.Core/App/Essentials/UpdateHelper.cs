using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace PCL.Core.App.Essentials;

public static class UpdateHelper
{
    /// <summary>
    /// 更新启动器（替换文件）
    /// </summary>
    /// <param name="source">用于替换的来源文件路径</param>
    /// <param name="target">目标文件路径</param>
    public static Exception? Replace(string source, string target)
    {
        string backup;
        try
        {
            source = Path.GetFullPath(source);
            target = Path.GetFullPath(target);
            // 备份名必须基于已归一化的目标路径，否则相对路径会算出错误的备份位置
            backup = $"{target}.bak.{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
        }
        catch (Exception ex)
        {
            return ex;
        }

        try
        {
            // 备份目标文件
            File.Copy(target, backup);
            if (!File.Exists(backup)) throw new FileNotFoundException("备份目标文件失败", backup);

            // 删除原文件并等待文件删除事件
            using (var watcher = new FileSystemWatcher(Basics.GetParentPathOrEmpty(target), Path.GetFileName(target)))
            using (var deletedEvent = new ManualResetEventSlim(false))
            {
                watcher.Deleted += (_, _) => deletedEvent.Set();
                watcher.EnableRaisingEvents = true;
                File.Delete(target);
                if (!deletedEvent.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("删除目标文件失败");
            }

            // 复制到目标文件
            File.Copy(source, target);
            if (!File.Exists(target)) throw new FileNotFoundException("复制到目标文件失败", target);
        }
        catch (Exception ex)
        {
            _TryRestoreBackup(backup, target);
            return ex;
        }

        // 只在替换确实成功时清理备份。
        // 原先无论成功失败都会执行 File.Delete(backup)：一旦 File.Copy 写到一半失败，
        // 目标文件已是半成品、而回滚条件 !File.Exists(target) 又不成立，
        // 备份再被删掉就彻底无法恢复了。
        try
        {
            if (File.Exists(backup)) File.Delete(backup);
        }
        catch (Exception ex)
        {
            // 清理备份失败不影响替换结果，仅记录
            Trace.WriteLine($"[UpdateHelper] 备份文件 '{backup}' 清理失败：{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 失败回滚：先清掉可能只写了一半的目标文件，再把备份移回原位。
    /// 回滚本身失败时保留备份，交由用户手工恢复。
    /// </summary>
    private static void _TryRestoreBackup(string backup, string target)
    {
        try
        {
            if (!File.Exists(backup)) return;

            if (File.Exists(target)) File.Delete(target);
            File.Move(backup, target);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[UpdateHelper] 回滚失败，备份保留在 '{backup}'：{ex.Message}");
        }
    }
}
