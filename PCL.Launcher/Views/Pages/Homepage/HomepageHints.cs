using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;

namespace PCL.Views.Pages.Homepage;

/// <summary>
///     “你知道吗”提示文本的来源：优先外部 <c>PCL/hints.txt</c>，其次内嵌语言资源，
///     两者都不可用时返回本地化占位文本。
/// </summary>
public static class HomepageHints
{
    public static string GetRandomHint(bool enableLengthLimit = false, bool raw = false)
    {
        string[]? lines = null;

        var externalPath = Path.Combine(Paths.DefaultDirectory, "PCL", "hints.txt");
        if (File.Exists(externalPath))
            try
            {
                lines =
                [
                    .. File
                        .ReadAllLines(externalPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                ];
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "Launch", $"读取外部提示文本失败：{externalPath}");
            }

        if (lines is not { Length: > 0 })
            lines = LoadEmbeddedHints(LocalizationService.CurrentLanguage.Code)
                ?? LoadEmbeddedHints(LocalizationService.DefaultLanguageCode);
        if (lines is not { Length: > 0 })
            return Lang.Text("Launch.Status.Trivia.Placeholder");

        if (enableLengthLimit)
        {
            var shortLines = lines.Where(line => line.Length < 50).ToArray();
            if (shortLines.Length > 0)
                lines = shortLines;
        }

        var hint = lines[Random.Shared.Next(lines.Length)];
        return raw
            ? hint
            : hint
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }

    private static string[]? LoadEmbeddedHints(string languageCode)
    {
        try
        {
            using var stream =
                LauncherCommon.GetResourceStream($"Resources/hints/{languageCode}.txt");
            if (stream is null)
                return null;
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return
            [
                .. text
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
            ];
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", "读取内嵌提示文本失败");
            return null;
        }
    }
}