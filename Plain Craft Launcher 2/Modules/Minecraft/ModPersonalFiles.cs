using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public static class ModPersonalFiles
{
    private static string _ArchiveRoot =>
        Path.GetFullPath(Path.Combine(ModFolder.mcFolderSelected, "PCL", "PersonalFiles"));

    private static (int FileCount, string TargetFolder) _Backup(McInstance instance)
    {
        var targetFolder = Path.Combine(_ArchiveRoot, instance.Name);
        var instancePath = Path.GetFullPath(instance.PathInstance);
        if (!instancePath.EndsWith(Path.DirectorySeparatorChar)) instancePath += Path.DirectorySeparatorChar;
        if (targetFolder.StartsWith(instancePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("回忆文件备份目录不能位于实例目录中");

        var fileCount = _CopyDirectory(Path.Combine(instance.PathIndie, "screenshots"),
            Path.Combine(targetFolder, "screenshots"));
        fileCount += _CopyDirectory(Path.Combine(instance.PathIndie, "schematics"),
            Path.Combine(targetFolder, "schematics"));

        if (fileCount > 0)
            ModBase.Log($"[PersonalFiles] 已备份实例 {instance.Name} 的 {fileCount} 个回忆文件到 {targetFolder}");
        return (fileCount, targetFolder);
    }

    public static bool TryBackupBeforeDelete(McInstance instance)
    {
        try
        {
            var result = _Backup(instance);
            if (result.FileCount > 0)
                HintService.Hint(Lang.Text("Instance.PersonalFiles.Backup.Success", result.FileCount,
                    result.TargetFolder), HintType.Success);
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"备份实例 {instance.Name} 的回忆文件失败", ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.PersonalFiles.Backup.Failed"));
            return false;
        }
    }

    private static int _CopyDirectory(string sourceFolder, string targetFolder)
    {
        if (!Directory.Exists(sourceFolder)) return 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        var fileCount = 0;
        foreach (var sourceFile in Directory.EnumerateFiles(sourceFolder, "*", options))
        {
            var targetFile = Path.Combine(targetFolder, Path.GetRelativePath(sourceFolder, sourceFile));
            ModBase.CopyFile(sourceFile, targetFile);
            File.SetCreationTimeUtc(targetFile, File.GetCreationTimeUtc(sourceFile));
            File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
            fileCount += 1;
        }

        return fileCount;
    }
}
