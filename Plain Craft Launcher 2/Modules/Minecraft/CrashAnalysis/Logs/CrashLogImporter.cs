using System.IO;
using PCL.Core.Logging;

namespace PCL;

internal sealed class CrashLogImporter(CrashAnalysisContext context)
{
    public void Import(string filePath)
    {
        LogWrapper.Info("Crash", "步骤 1：自主导入日志文件");

        if (!_TryExtractArchive(filePath))
        {
            CrashFileIO.CopyFile(filePath, Path.Combine(
                context.TempFolder,
                "Temp",
                Path.GetFileName(filePath)));
            LogWrapper.Info("Crash", "已复制导入的日志文件：" + filePath);
        }

        foreach (var targetFile in new DirectoryInfo(Path.Combine(context.TempFolder, "Temp"))
                     .EnumerateFiles()
                     .ToList())
            try
            {
                if (!targetFile.Exists || targetFile.Length == 0L)
                    continue;

                var ext = targetFile.Extension.ToLowerInvariant();
                if (ext is ".log" or ".txt")
                    context.RawFiles.Add(new CrashLogEntry(targetFile.FullName,
                        CrashFileIO.ReadText(targetFile.FullName).Split("\r\n".ToCharArray())));
                else
                    File.Delete(targetFile.FullName);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Crash", "导入单个日志文件失败");
            }

        LogWrapper.Info("Crash", "步骤 1：自主导入日志文件，收集到 " + context.RawFiles.Count + " 个文件");
    }

    private bool _TryExtractArchive(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists || info.Length <= 0L)
                return false;

            if (!filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return false;

            CrashFileIO.ExtractFile(filePath, Path.Combine(context.TempFolder, "Temp"));
            LogWrapper.Info("Crash", "已解压导入的日志文件：" + filePath);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "尝试解压导入文件失败，将按普通文件处理（" + filePath + "）");
            return false;
        }
    }
}