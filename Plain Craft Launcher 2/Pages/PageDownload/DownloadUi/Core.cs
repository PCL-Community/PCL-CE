using System.IO;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     如果 OptiFine 与 Forge 同时开始安装，就会导致 Forge 安装失败。
    /// </summary>
    private static readonly object InstallSyncLock = new();

    /// <summary>
    ///     如果 OptiFine 与 Forge 同时复制原版 Jar，就会导致复制文件时冲突。
    /// </summary>
    private static readonly object VanillaSyncLock = new();

    private static readonly string McDownloadClientLibName =
        Lang.Text("Minecraft.Download.Stage.VanillaLibrariesDownload");

    private static readonly string McDownloadClientJsonName =
        Lang.Text("Minecraft.Download.Stage.VanillaJsonDownload");

    /// <summary>
    ///     将远程元数据提供的名称作为单个目录名拼接到缓存目录下，并阻止路径穿越。
    /// </summary>
    private static string CombineCacheSubfolder(string parentFolder, string childFolderName)
    {
        if (string.IsNullOrWhiteSpace(childFolderName)
            || childFolderName is "." or ".."
            || childFolderName.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0']) >= 0
            || Path.IsPathRooted(childFolderName))
            CancelUnsafeCacheSubfolder(childFolderName, "包含非法路径字符");

        var parentFullPath =
            Path
                .GetFullPath(parentFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var combinedFullPath = Path.GetFullPath(Path.Combine(parentFullPath, childFolderName));
        if (!combinedFullPath.StartsWith(parentFullPath, StringComparison.OrdinalIgnoreCase))
            CancelUnsafeCacheSubfolder(childFolderName, "导致缓存路径越界");
        return combinedFullPath;
    }

    private static void CancelUnsafeCacheSubfolder(string childFolderName, string reason)
    {
        var message = $"远程版本名{reason}：{childFolderName}";
        Common.Log($"[Download] {message}");
        HintService.Hint(message, HintType.Error);
        throw new Common.CancelledException();
    }
}