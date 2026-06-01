using System.Globalization;
using System.IO;
using System.IO.Compression;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.UI;

namespace PCL;

/// <summary>
///     <p>UI 层错误报告导出服务。</p>
///     <p>
///         Core 的 <see cref="CrashReportBuilder" /> 只构建内存条目；本类负责让用户选择保存路径、写入 zip、
///         显示提示并打开资源管理器。这样报告内容生成和用户交互保持解耦。
///     </p>
/// </summary>
public sealed class MinecraftCrashReportExportService
{
    /// <summary>
    ///     将 Core 生成的报告条目写入用户选择的 zip 文件。
    /// </summary>
    public static void Export(CrashReportPackage package)
    {
        string? filePath = null;
        try
        {
            ModBase.RunInUiWait(() =>
            {
                filePath = SystemDialogs.SelectSaveFile(
                    Lang.Text("Crash.Export.SelectSaveTitle"),
                    Lang.Text("Crash.Export.DefaultFileName",
                        DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss", CultureInfo.InvariantCulture)),
                    Lang.Text("Crash.Export.FileFilter"));
            });

            if (string.IsNullOrWhiteSpace(filePath)) return;

            Directory.CreateDirectory(ModBase.GetPathFromFullPath(filePath));
            if (File.Exists(filePath)) File.Delete(filePath);

            using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
            foreach (var entry in package.Entries)
            {
                var zipEntry = archive.CreateEntry(entry.FileName);
                using var stream = zipEntry.Open();
                stream.Write(entry.Content);
            }

            ModMain.Hint(Lang.Text("Crash.Export.Success"), ModMain.HintType.Finish);
            ModBase.OpenExplorer(filePath);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Crash.Export.Failed"), ModBase.LogLevel.Feedback);
        }
    }
}