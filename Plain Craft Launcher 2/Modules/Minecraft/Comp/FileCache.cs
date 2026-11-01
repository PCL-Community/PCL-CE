using System.Collections.Concurrent;
using System.IO;
using System.Text;
using PCL.Core.App;

namespace PCL;

public static partial class Comp
{
    /// <summary>
    ///     已知文件信息的缓存。
    /// </summary>
    public static ConcurrentDictionary<string, List<CompFile>> compFilesCache = new();

    /// <summary>
    ///     获取某个工程下的全部文件列表。
    ///     必须在工作线程执行，失败会抛出异常。
    /// </summary>
    public static List<CompFile> CompFilesGet(string projectId, bool fromCurseForge)
    {
        if (!compProjectCache.TryGetValue(projectId, out var targetProject))
            targetProject = _FetchCompProject(projectId, fromCurseForge);

        if (!compFilesCache.TryGetValue(projectId, out var currentFiles))
        {
            Common.Log($"[Comp] 开始获取文件列表：{projectId}");
            currentFiles = _FetchCompFiles(projectId, fromCurseForge, targetProject.Type);
            compFilesCache[projectId] = currentFiles;
        }

        var allRawDeps = currentFiles
            .SelectMany(f => f.RawDependencies.Concat(f.RawOptionalDependencies))
            .Distinct()
            .ToList();

        _LoadMissingDependencyProjects(projectId, allRawDeps, fromCurseForge);

        var availableDeps = allRawDeps
            .Where(id =>
                compProjectCache.ContainsKey(id) && (id ?? "") != (projectId ?? ""))
            .Select(id => compProjectCache[id])
            .ToList();

        foreach (var file in currentFiles)
        foreach (var dep in availableDeps)
        {
            if (file.RawDependencies.Contains(dep.Id) && !file.Dependencies.Contains(dep.Id))
                file.Dependencies.Add(dep.Id);

            if (file.RawOptionalDependencies.Contains(dep.Id)
                && !file.OptionalDependencies.Contains(dep.Id))
                file.OptionalDependencies.Add(dep.Id);
        }

        return currentFiles;
    }

    private static CompProject _FetchCompProject(string projectId, bool fromCurseForge)
    {
        if (fromCurseForge)
        {
            var json = GameDownload.DlModRequest<JsonObject>(
                $"https://api.curseforge.com/v1/mods/{projectId}");
            return new CompProject(
                json["data"] as JsonObject
                ?? throw new InvalidOperationException("CurseForge 工程数据无效。"));
        }

        return new CompProject(
            GameDownload.DlModRequest<JsonObject>(
                $"https://api.modrinth.com/v2/project/{projectId}"));
    }

    private static List<CompFile> _FetchCompFiles(
        string projectId,
        bool fromCurseForge,
        CompType projectType)
    {
        JsonArray resultJsonArray;
        if (fromCurseForge)
        {
            var response = GameDownload.DlModRequest<JsonObject>(
                $"https://api.curseforge.com/v1/mods/{projectId}/files?pageSize=10000");
            resultJsonArray = response["data"] as JsonArray
                ?? throw new InvalidOperationException("CurseForge 文件列表数据无效。");
        }
        else
        {
            resultJsonArray = GameDownload.DlModRequest<JsonArray>(
                $"https://api.modrinth.com/v2/project/{projectId}/version?include_changelog=false");
        }

        return
        [
            .. resultJsonArray
                .OfType<JsonObject>()
                .Select(a => new CompFile(a, projectType))
                .Where(a => a.Available)
                .GroupBy(a => a.Id)
                .Select(g => g.First())
        ];
    }

    private static void _LoadMissingDependencyProjects(
        string projectId,
        List<string> allRawDeps,
        bool fromCurseForge)
    {
        var undoneDeps = allRawDeps
            .Where(id => !compProjectCache.ContainsKey(id))
            .ToList();
        if (undoneDeps.Count == 0) return;

        Common.Log($"[Comp] {projectId} 需要补全信息的依赖项共 {undoneDeps.Count} 个");

        JsonArray projects;
        if (fromCurseForge)
        {
            var response = GameDownload.DlModRequest<JsonObject>(
                "https://api.curseforge.com/v1/mods",
                "POST",
                "{\"modIds\": [" + string.Join(",", undoneDeps) + "]}",
                "application/json");
            projects = response["data"] as JsonArray
                ?? throw new InvalidOperationException("CurseForge 依赖工程数据无效。");
        }
        else
        {
            projects = GameDownload.DlModRequest<JsonArray>(
                $"https://api.modrinth.com/v2/projects?ids=[\"{undoneDeps.Join("\",\"")}\"]");
        }

        foreach (var project in projects.OfType<JsonObject>())
            _ = new CompProject(project);
    }

    public static string CompFileNameGet(CompProject proj, CompFile file)
    {
        var fileName = (proj.TranslatedName ?? "") == (proj.RawName ?? "")
            ? _BuildDefaultFileName(proj, file)
            : _BuildLocalizedFileName(proj, file);

        if (file.Type == CompType.Mod)
            fileName = fileName.Replace("~", "-");
        return CompFileNameSanitize(fileName);
    }

    private static string _BuildDefaultFileName(CompProject proj, CompFile file)
    {
        var hasSourceName = !string.IsNullOrWhiteSpace(file.FileName);
        var defaultExtension = _GetDefaultExtension(file);
        var fallbackBase = !string.IsNullOrWhiteSpace(proj.RawName)
            ? proj.RawName
            : "download";
        return hasSourceName
            ? file.FileName
            : $"{fallbackBase}{defaultExtension}";
    }

    private static string _BuildLocalizedFileName(CompProject proj, CompFile file)
    {
        var hasSourceName = !string.IsNullOrWhiteSpace(file.FileName);
        var defaultExtension = _GetDefaultExtension(file);
        var chineseName = proj
            .TranslatedName
            .BeforeFirst(" (")
            .BeforeFirst(" - ")
            .Replace(@"\", "＼")
            .Replace("/", "／")
            .Replace("|", "｜")
            .Replace(":", "：")
            .Replace("<", "＜")
            .Replace(">", "＞")
            .Replace("*", "＊")
            .Replace("?", "？")
            .Replace("\"", "")
            .Replace("： ", "：");
        var extension = hasSourceName
            ? Path.GetExtension(file.FileName)
            : "";
        var nameWithoutExtension = hasSourceName && !string.IsNullOrEmpty(extension)
            ? file.FileName[..^extension.Length]
            : file.FileName;
        var sourcePart = hasSourceName
            ? file.FileName
            : defaultExtension;
        return Config.Download.Comp.NameFormatV2 switch
        {
            0 => $"【{chineseName}】{sourcePart}",
            1 => $"[{chineseName}] {sourcePart}",
            2 => $"{chineseName}-{sourcePart}",
            3 => !string.IsNullOrWhiteSpace(nameWithoutExtension)
                ? $"{nameWithoutExtension}-{chineseName}{extension}"
                : $"{chineseName}{defaultExtension}",
            _ => hasSourceName
                ? file.FileName
                : $"{chineseName}{defaultExtension}"
        };
    }

    private static string _GetDefaultExtension(CompFile file)
    {
        return file.Type switch
        {
            CompType.Mod => ".jar",
            CompType.ModPack => file.FromCurseForge ? ".zip" : ".mrpack",
            _ => ".zip"
        };
    }

    public static string CompFileNameSanitize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "download";

        var sanitized = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
            sanitized.Append(
                c switch
                {
                    '\\' => '＼',
                    '/' => '／',
                    ':' => '：',
                    '*' => '＊',
                    '?' => '？',
                    '"' => '＂',
                    '<' => '＜',
                    '>' => '＞',
                    '|' => '｜',
                    _ when char.IsControl(c) => '_',
                    _ => c
                });

        var result = sanitized.ToString().Trim();
        return result is "" or "." or ".."
            ? "download"
            : result;
    }
}
