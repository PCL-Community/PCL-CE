using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

public static partial class MinecraftCrashUi
{
    public static string Text(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var result = Lang.Text(key);
        if (parameters is null) return result;

        foreach (var pair in parameters)
        {
            var value = pair.Value;
            if (value.StartsWith("Crash.", StringComparison.Ordinal))
                value = Text(value);
            result = result.Replace("{" + pair.Key + "}", value, StringComparison.Ordinal);
        }

        return result;
    }

    public static string LocalizeMarkdown(string key, IReadOnlyDictionary<string, string> parameters)
    {
        return Text(key, parameters);
    }

    public static MyCard CreateCard(string titleKey, UIElement content)
    {
        var card = new MyCard
        {
            Title = Text(titleKey),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        if (content is FrameworkElement element)
            element.Margin = element.Margin == default ? new Thickness(20d, 40d, 20d, 18d) : element.Margin;
        card.Children.Add(content);
        return card;
    }

    public static TextBlock TextBlock(string text, double fontSize = 14, FontWeight? weight = null)
    {
        return MinecraftCrashVisualFactory.Text(text, fontSize, weight);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "-";
        var units = new[] { "B", "KB", "MB", "GB" };
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0 ? bytes + " " + units[unit] : value.ToString("0.#") + " " + units[unit];
    }

    public static string ConfidenceText(CrashDiagnosisConfidence confidence)
    {
        return Text("Crash.Confidence." + confidence);
    }

    public static void ExecuteAction(CrashPresentationActionKind kind)
    {
        ExecuteAction(new CrashPresentationAction
        {
            Kind = kind,
            TitleKey = CrashDiagnosisLocalizer.ActionTitleKey(kind)
        });
    }

    public static void ExecuteAction(CrashPresentationAction action)
    {
        var session = MinecraftCrashSessionStore.TryGetCurrent();
        if (session is null) return;
        switch (action.Kind)
        {
            case CrashPresentationActionKind.OpenLog:
                if (!string.IsNullOrWhiteSpace(action.TargetPath))
                    OpenPathOrText(action.TargetPath, null, "CrashAnalysis.log");
                else
                    _OpenPreferredLog(session);
                break;
            case CrashPresentationActionKind.ExportMarkdown:
                MinecraftCrashMarkdownExportService.ExportCurrent();
                break;
            case CrashPresentationActionKind.ExportReport:
                MinecraftCrashReportExportService.ExportCurrent();
                break;
            case CrashPresentationActionKind.CopyDiagnosisSummary:
                MinecraftCrashMarkdownExportService.CopyCurrentSummary();
                break;
            case CrashPresentationActionKind.PreviewMarkdown:
                MinecraftCrashMarkdownPreviewService.PreviewCurrent();
                break;
            case CrashPresentationActionKind.OpenJavaSettings:
                ModMain.frmMain?.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupJava);
                break;
            case CrashPresentationActionKind.OpenMemorySettings:
            case CrashPresentationActionKind.OpenInstanceSettings:
                if (session.Instance is not null)
                {
                    PageInstanceLeft.instance = session.Instance;
                    ModMain.frmMain?.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.VersionSetup);
                }

                break;
            case CrashPresentationActionKind.OpenInstanceModsFolder:
                if (session.Instance is not null)
                    ModBase.OpenExplorer(Path.Combine(session.Instance.PathIndie, "mods"));
                break;
            case CrashPresentationActionKind.OpenResourcePackFolder:
                if (session.Instance is not null)
                    ModBase.OpenExplorer(Path.Combine(session.Instance.PathIndie, "resourcepacks"));
                break;
        }
    }

    public static void OpenLog(CrashPresentationLogSource log)
    {
        OpenPathOrText(log.FullPath, log.Preview, log.Name);
    }

    public static void CopyLogPreview(CrashPresentationLogSource log)
    {
        try
        {
            Clipboard.SetText(log.Preview);
            ModMain.Hint(Text("Crash.Logs.PreviewCopied"), ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "复制崩溃日志预览失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void _OpenPreferredLog(MinecraftCrashSession session)
    {
        var document = session.Result.LogBundle.PreferredOpenDocument;
        if (document is null) return;
        OpenPathOrText(document.FullPath, document.Text, document.Name);
    }

    private static void OpenPathOrText(string? fullPath, string? fallbackText, string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
        {
            ModBase.ShellOnly(fullPath);
            return;
        }

        if (string.IsNullOrWhiteSpace(fallbackText)) return;
        var safeName = string.Join("_",
            fallbackName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(ModBase.pathTemp, string.IsNullOrWhiteSpace(safeName) ? "CrashAnalysis.log" : safeName);
        ModBase.WriteFile(path, fallbackText);
        ModBase.ShellOnly(path);
    }

    [GeneratedRegex(@"\{(?<name>[A-Za-z][A-Za-z0-9_.-]*)\}")]
    private static partial Regex _NamedPlaceholderRegex();
}