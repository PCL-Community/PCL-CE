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
        string backup, staging;
        try
        {
            source = Path.GetFullPath(source);
            target = Path.GetFullPath(target);
            // 备份名必须基于已归一化的目标路径，否则相对路径会算出错误的备份位置
            backup = $"{target}.bak.{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
            // 先写到临时名，确认完整后再原子改名，避免磁盘写满时留下半截文件被当成有效备份
            staging = $"{backup}.tmp";
        }
        catch (Exception ex)
        {
            return ex;
        }

        // 备份阶段与替换阶段必须分开处理：
        // 备份失败时原目标文件仍然完好，此时若走回滚分支，_TryRestoreBackup 会删掉完好的原文件
        // 并把半截备份移到它的位置——比不更新更糟。
        try
        {
            File.Copy(target, staging, true);
            if (new FileInfo(staging).Length != new FileInfo(target).Length)
                throw new IOException("备份文件大小与源文件不一致，可能写入不完整");
            File.Move(staging, backup);
            if (!File.Exists(backup)) throw new FileNotFoundException("备份目标文件失败", backup);
        }
        catch (Exception ex)
        {
            _TryDeleteFile(staging);
            return ex;
        }

        try
        {
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
            // 此处备份一定已完整生成，回滚是安全的
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
    /// 删除可能残留的中间文件，失败仅记录。
    /// </summary>
    private static void _TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[UpdateHelper] 临时文件 '{path}' 清理失败：{ex.Message}");
        }
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
