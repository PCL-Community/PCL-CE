using System.IO;

namespace PCL;

internal sealed class CrashLogImporter(CrashAnalysisContext context)
{
    public void Import(string filePath)
    {
        ModBase.Log("[Crash] 步骤 1：自主导入日志文件");

        if (!_TryExtractArchive(filePath))
        {
            ModBase.CopyFile(filePath, Path.Combine(
                context.TempFolder,
                "Temp",
                ModBase.GetFileNameFromPath(filePath)));
            ModBase.Log("[Crash] 已复制导入的日志文件：" + filePath);
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
                        ModBase.ReadFile(targetFile.FullName).Split("\r\n".ToCharArray())));
                else
                    File.Delete(targetFile.FullName);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "导入单个日志文件失败");
            }

        ModBase.Log("[Crash] 步骤 1：自主导入日志文件，收集到 " + context.RawFiles.Count + " 个文件");
    }

    private bool _TryExtractArchive(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists || info.Length <= 0L || filePath.EndsWithF(".jar", true))
                return false;

            ModBase.ExtractFile(filePath, Path.Combine(context.TempFolder, "Temp"));
            ModBase.Log("[Crash] 已解压导入的日志文件：" + filePath);
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "尝试解压导入文件失败，将按普通文件处理（" + filePath + "）");
            return false;
        }
    }
}