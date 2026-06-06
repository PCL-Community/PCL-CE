using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.UI;

namespace PCL;

public static class MinecraftCrashReportExportService
{
    public static void ExportCurrent()
    {
        var session = MinecraftCrashSessionStore.Current;
        var package = CrashReportBuilder.Build(session.Result, new CrashReportBuildOptions
        {
            Markdown = session.Markdown,
            SensitiveValues = _CollectSensitiveValues(session)
        });
        if (session.ExtraReportFiles.Count > 0)
        {
            var entries = package.Entries.ToList();
            foreach (var file in session.ExtraReportFiles)
                try
                {
                    if (!File.Exists(file)) continue;
                    entries.Add(new CrashReportEntry
                    {
                        FileName = "extra/" + Path.GetFileName(file),
                        Content = File.ReadAllBytes(file)
                    });
                }
                catch
                {
                    // 额外报告文件读取失败不应阻断主报告导出。
                }

            package = new CrashReportPackage(entries);
        }

        Export(package);
    }

    public static void Export(CrashReportPackage package)
    {
        string? filePath = null;
        try
        {
            ModBase.RunInUiWait(() =>
            {
                filePath = SystemDialogs.SelectSaveFile(
                    MinecraftCrashUi.Text("Crash.Export.Full.Title"),
                    MinecraftCrashUi.Text("Crash.Export.Full.DefaultFileName", new Dictionary<string, string>
                    {
                        ["0"] = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss", CultureInfo.InvariantCulture)
                    }),
                    MinecraftCrashUi.Text("Crash.Export.Full.Filter"));
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

            ModMain.Hint(MinecraftCrashUi.Text("Crash.Export.Full.Success"), ModMain.HintType.Finish);
            ModBase.OpenExplorer(filePath);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, MinecraftCrashUi.Text("Crash.Export.Full.Failed"), ModBase.LogLevel.Feedback);
        }
    }

    private static IReadOnlyList<string> _CollectSensitiveValues(MinecraftCrashSession session)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(session.Request.RuntimeContext.AccountName))
            values.Add(session.Request.RuntimeContext.AccountName);
        if (!string.IsNullOrWhiteSpace(session.Request.RuntimeContext.InstancePath))
            values.Add(session.Request.RuntimeContext.InstancePath);
        return values;
    }

    public static void ExportCurrentMarkdown()
    {
        var markdown = MinecraftCrashSessionStore.Current.Markdown;
        string? path = null;
        ModBase.RunInUiWait(() =>
        {
            path = SystemDialogs.SelectSaveFile(
                MinecraftCrashUi.Text("Crash.Export.Markdown.Title"),
                markdown.FileName,
                MinecraftCrashUi.Text("Crash.Export.Markdown.Filter"));
        });
        if (string.IsNullOrWhiteSpace(path)) return;
        File.WriteAllText(path, markdown.Content, Encoding.UTF8);
        ModMain.Hint(MinecraftCrashUi.Text("Crash.Export.Markdown.Success"), ModMain.HintType.Finish);
        ModBase.OpenExplorer(path);
    }
}