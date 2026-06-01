using System.Globalization;
using System.IO;
using System.IO.Compression;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.UI;

namespace PCL;

public sealed class MinecraftCrashReportExportService
{
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