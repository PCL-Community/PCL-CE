using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PCL.Network;

namespace PCL;

/// <summary>
///     解析 Fabric 崩溃报告，自动修补缺失/不兼容的模组前置。
/// </summary>
public static class ModCrashAutoRepair
{
    /// <summary>
    ///     为 true 时抑制崩溃分析弹窗（自动修复进行中）。
    /// </summary>
    internal static bool SuppressCrashPopup;

    /// <summary>
    ///     从日志行中提取缺失模组列表（不下载）。
    /// </summary>
    public static List<MissingDepInfo> FindMissingDeps(IList<string> lines)
    {
        var deps = new List<MissingDepInfo>();
        ParseCrashLines(lines is string[] arr ? arr : lines.ToArray(), deps);
        return deps;
    }

    /// <summary>
    ///     下载指定的缺失模组到实例 mods 文件夹。
    /// </summary>
    public static int DownloadDeps(List<MissingDepInfo> deps, McInstance instance, Action<int, int>? onProgress = null)
    {
        var modsFolder = Path.Combine(instance.PathIndie, "mods");
        Directory.CreateDirectory(modsFolder);
        var total = deps.Count;
        var repaired = 0;
        for (var i = 0; i < deps.Count; i++)
        {
            onProgress?.Invoke(i + 1, total);
            try
            {
                if (DownloadDep(deps[i], modsFolder, instance.Info.VanillaName))
                    repaired++;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[AutoRepair] 下载 {deps[i].MissingName} 失败");
            }
        }
        return repaired;
    }

    private static void ParseCrashLines(string[] lines, List<MissingDepInfo> deps)
    {
        if (lines is null) return;

        // Fabric 中文: 需要 X 的 V [及以上版本]，但没有安装它！
        // X 可以是: 'Fabric API' (fabric-api) 或 glitchcore
        // V 可以是: 26.1.2.0.0 及以上版本 / 任意版本 / any version / any
        var missingPattern = new Regex(
            @"需要\s*(?:模组\s*)?'?([^'^ ^(]+)'?\s*(?:\(([^)]*)\))?\s*的\s*(\S+?(?:\s*(?:及以上版本|或更高版本))?|任意版本|任何版本|any\s*version|any)\s*[，,\s]*但没有安装它",
            RegexOptions.Compiled);

        // Fabric 中文: 需要 X 的 V2 及以上版本，但已经安装了的版本 V3 不对！
        var wrongVersionPattern = new Regex(
            @"需要\s*(?:模组\s*)?'?([^'^ ^(]+)'?\s*(?:\(([^)]*)\))?\s*的\s*([^\s，,]+)\s*及以上版本[，,\s]*但已经安装了的版本\s*(\S+)\s*不对",
            RegexOptions.Compiled);

        // Fabric English: requires X version V or later, which is missing!
        var enMissingPattern = new Regex(
            @"requires\s+(?:mod\s+)?'?([^'^\s]+)'?\s*(?:\(([^)]*)\))?\s+version\s+(\S+)\s+or\s+later[^.]*\bis\s+missing",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Fabric English: requires X version V or later, but V3 is installed!
        var enWrongPattern = new Regex(
            @"requires\s+(?:mod\s+)?'?([^'^\s]+)'?\s*(?:\(([^)]*)\))?\s+version\s+(\S+)\s+or\s+later[^.]*\b(?:but|however)\s+(\S+)\s+is\s+installed",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Fabric English: requires X any version, which is missing!
        var enAnyPattern = new Regex(
            @"requires\s+(?:mod\s+)?'?([^'^\s]+)'?\s*(?:\(([^)]*)\))?\s+(?:any\s+version|any)\s*[^.]*\bis\s+missing",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var logLines = new List<string>();
        foreach (var line in lines)
        {
            if (line is null) continue;
            if (line.Contains("需要") || line.Contains("requires"))
                logLines.Add(line);

            foreach (var pattern in new[] { missingPattern, wrongVersionPattern, enMissingPattern, enWrongPattern, enAnyPattern })
            {
                var m = pattern.Match(line);
                if (!m.Success) continue;

                var missingName = m.Groups[1].Value;
                var missingId = string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[1].Value : m.Groups[2].Value;
                var version = (m.Groups.Count > 3 ? m.Groups[3].Value : "")
                    .Replace("及以上版本", "").Replace("或更高版本", "").Trim();
                if (version is "任意版本" or "任何版本" or "any version" or "any")
                    version = ""; // 不限制版本
                deps.Add(new MissingDepInfo
                {
                    MissingName = missingName,
                    MissingId = missingId,
                    RequiredVersion = version,
                });
                ModBase.Log($"[AutoRepair] 发现缺失前置: {missingName} ({missingId})" +
                    (string.IsNullOrEmpty(version) ? "" : $" >= {version}"));
                break;
            }
        }
        if (deps.Count == 0)
            ModBase.Log($"[AutoRepair] 无匹配，相关行: {string.Join(" | ", logLines)}");
    }

    private static bool DownloadDep(MissingDepInfo dep, string modsFolder, string vanillaVersion)
    {
        var query = dep.MissingId;
        var storage = new ModComp.CompProjectStorage();

        // 构建搜索词列表：精确 modid → 模组名 → 分词
        var queries = new List<string> { query };
        if (dep.MissingName != query && !string.IsNullOrEmpty(dep.MissingName))
            queries.Add(dep.MissingName);
        // 英文分词：forgeconfigapiport → "forge config api port"
        var words = SplitCamelCase(query);
        if (!string.IsNullOrEmpty(words) && words != query)
            queries.Add(words);

        foreach (var q in queries.Distinct())
        {
            foreach (var source in new[] { ModComp.CompSourceType.Modrinth, ModComp.CompSourceType.CurseForge })
            {
                var request = new ModComp.CompProjectRequest(ModComp.CompType.Mod, storage, 10)
                {
                    searchText = q,
                    gameVersion = vanillaVersion,
                    source = source,
                    sort = ModComp.CompSortType.Relevance,
                };

                var sourceName = source == ModComp.CompSourceType.Modrinth ? "Modrinth" : "CurseForge";
                ModBase.Log($"[AutoRepair] {sourceName} 搜索: {q}");

                if (TrySearchAndDownload(request, dep.MissingName, modsFolder, vanillaVersion, storage))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     拆分驼峰式英文单词：forgeconfigapiport → "forge config api port"
    /// </summary>
    private static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            if (i > 0 && char.IsUpper(input[i]) && !char.IsUpper(input[i - 1]))
                sb.Append(' ');
            else if (i > 0 && i < input.Length - 1 && char.IsUpper(input[i]) &&
                     char.IsUpper(input[i - 1]) && !char.IsUpper(input[i + 1]))
                sb.Append(' ');
            sb.Append(char.ToLower(input[i]));
        }
        return sb.ToString();
    }

    private static bool TrySearchAndDownload(ModComp.CompProjectRequest request, string fallbackName,
        string modsFolder, string vanillaVersion, ModComp.CompProjectStorage storage)
    {
        var task = new ModLoader.LoaderTask<ModComp.CompProjectRequest, int>(
            $"AutoRepair-{request.searchText}", ModComp.CompProjectsGet, () => request);
        task.Start();
        while (task.State == ModBase.LoadState.Loading) System.Threading.Thread.Sleep(200);

        if (task.State != ModBase.LoadState.Finished || storage.results.Count == 0)
        {
            if (request.searchText != fallbackName)
            {
                request.searchText = fallbackName;
                task.Start();
                while (task.State == ModBase.LoadState.Loading) System.Threading.Thread.Sleep(200);
            }
            if (task.State != ModBase.LoadState.Finished || storage.results.Count == 0)
                return false;
        }

        var project = storage.results[0];
        ModBase.Log($"[AutoRepair] 找到: {project.RawName} ({project.Id})");
        var files = ModComp.CompFilesGet(project.Id, project.FromCurseForge);
        if (files is null || files.Count == 0) return false;

        var compatible = files
            .Where(f => f.GameVersions is not null && f.GameVersions.Contains(vanillaVersion))
            .OrderByDescending(f => f.ReleaseDate)
            .FirstOrDefault();

        if (compatible is null) return false;
        if (File.Exists(Path.Combine(modsFolder, compatible.FileName))) return true;

        var localPath = Path.Combine(modsFolder, compatible.FileName);
        FileDownloader.Download(compatible.DownloadUrls, localPath).GetAwaiter().GetResult();
        ModBase.Log($"[AutoRepair] 已下载: {compatible.FileName}");
        return true;
    }

    public class MissingDepInfo
    {
        public string MissingName { get; set; } = "";
        public string MissingId { get; set; } = "";
        public string RequiredVersion { get; set; } = "";
    }
}
