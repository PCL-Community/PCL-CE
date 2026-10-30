using System.Collections.Concurrent;
using System.Net;
using Dapper;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL;

public static partial class Comp
{
    private const int CompPageSize = 40;

    /// <summary>
    ///     已知工程信息的缓存。
    /// </summary>
    public static ConcurrentDictionary<string, CompProject> compProjectCache = new();

    /// <summary>
    ///     CurseForge 分类 URL 段 → classId 映射。提为 static 避免每次解析重新分配。
    /// </summary>
    private static readonly Dictionary<string, string> CurseForgeCategoryClassIds = new()
    {
        {
            "mc-mods",
            "6"
        },
        {
            "modpacks",
            "4471"
        },
        {
            "texture-packs",
            "12"
        },
        {
            "shaders",
            "6552"
        }
    };

    /// <summary>
    ///     用 Uri 解析单个 token 是否为受支持的 CurseForge/Modrinth 资源链接。
    ///     成功时输出站点、分类段与 slug（query、fragment 会被自动丢弃）。
    /// </summary>
    private static bool TryParseResourceLink(
        string token,
        out ResourceSite site,
        out string category,
        out string slug)
    {
        site = ResourceSite.None;
        category = string.Empty;
        slug = string.Empty;

        if (!Uri.TryCreate(token, UriKind.Absolute, out var uri)
            && !Uri.TryCreate($"https://{token}", UriKind.Absolute, out uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var segments = uri.AbsolutePath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

        if (IsHostOf(uri.Host, "curseforge.com"))
        {
            if (segments.Length < 3
                || !segments[0].Equals("minecraft", StringComparison.OrdinalIgnoreCase))
                return false;
            site = ResourceSite.CurseForge;
            category = segments[1];
            slug = segments[2];
            return true;
        }

        if (IsHostOf(uri.Host, "modrinth.com"))
        {
            if (segments.Length < 2) return false;
            site = ResourceSite.Modrinth;
            category = segments[0];
            slug = segments[1];
            return true;
        }

        return false;
    }

    /// <summary>host 等于 domain 或为其子域，避免 evilcurseforge.com 之类的伪装域名。</summary>
    private static bool IsHostOf(string host, string domain)
    {
        return host.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>从文本中找出首个可识别的资源链接 token，找不到返回 null。</summary>
    private static string? FindFirstResourceLinkToken(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? null
            : text
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(token => TryParseResourceLink(
                    token,
                    out _,
                    out _,
                    out _));
    }

    /// <summary>
    ///     从单条 CurseForge / Modrinth 资源链接解析出 projectId。无法识别或获取失败时返回 null。
    /// </summary>
    public static string? ResolveLinkToProjectId(string url)
    {
        if (!TryParseResourceLink(
                url,
                out var site,
                out var category,
                out var slug))
        {
            var token = FindFirstResourceLinkToken(url);
            if (token is null || !TryParseResourceLink(token, out site, out category, out slug))
                return null;
        }

        if (site == ResourceSite.CurseForge)
        {
            var encodedSlug = WebUtility.UrlEncode(slug);
            var json = GameDownload.DlModRequest<JsonObject>(
                $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={encodedSlug}");
            var dataArray = json["data"] as JsonArray ?? [];
            if (!dataArray.Any())
                return null;

            var firstData = dataArray[0];
            if (firstData is null) return null;
            var receivedClassId = firstData["classId"]?.ToString();
            if (!CurseForgeCategoryClassIds.TryGetValue(category, out var targetClassId)
                || receivedClassId == targetClassId)
                return firstData["id"]?.ToString();

            var filteredJson = GameDownload.DlModRequest<JsonObject>(
                $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={encodedSlug}&classId={targetClassId}");
            var filteredDatas = filteredJson["data"] as JsonArray ?? [];
            return filteredDatas.Any()
                ? filteredDatas[0]?["id"]?.ToString()
                : null;
        }

        var mr = GameDownload.DlModRequest<JsonObject>(
            $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(slug)}");
        return mr["id"]?.ToString();
    }

    /// <summary>
    ///     若输入文本中恰好含 1 条 CurseForge/Modrinth 资源链接，返回该链接；含 0 条或 ≥2 条时返回 null。
    /// </summary>
    public static string? TryExtractSingleResourceLink(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var tokens = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string? found = null;
        foreach (var token in tokens)
        {
            if (!TryParseResourceLink(token, out _, out _, out _))
                continue;
            if (found is not null)
                return null;
            found = token;
        }

        return found;
    }

    /// <summary>
    ///     根据搜索请求获取一系列的工程列表。需要基于加载器运行。
    /// </summary>
    public static void CompProjectsGet(Loading.LoaderTask<CompProjectRequest, int> task)
    {
        var request = task.input;
        if (request is null) return;
        var storage = request.storage;

        var singleLink = TryExtractSingleResourceLink(request.searchText);
        if (singleLink is not null)
        {
            _AddSingleLinkResult(singleLink, request, storage);
            return;
        }

        if (storage.results.Count >= request.targetResultCount)
        {
            LogWrapper.Info(
                $"[Comp] 已有 {storage.results.Count} 个结果，多于所需的 {request.targetResultCount} 个结果，结束处理");
            return;
        }

        if (!request.CanContinue)
        {
            if (storage.results.Count == 0)
                throw new Exception(Lang.Text("Download.Comp.List.NoMatchingResults"));

            LogWrapper.Info(
                $"[Comp] 已有 {storage.results.Count} 个结果，少于所需的 {request.targetResultCount} 个结果，但无法继续获取，结束处理");
            return;
        }

        if (request.modLoader == CompLoaderType.Quilt
            && McVersionComparer.CompareVersion(request.gameVersion ?? "1.15", "1.14") == -1)
            throw new Exception(Lang.Text("Minecraft.Error.QuiltUnsupported", request.gameVersion));

        var rawFilter = request.searchText.Trim();
        request.searchText = rawFilter;
        _ = rawFilter.ToLower();
        LogWrapper.Info($"[Comp] 工程列表搜索原始文本：{rawFilter}");

        var isChineseSearch = Lang.IsChineseMainland
            && RegexPatterns.HasChineseChar.IsMatch(rawFilter)
            && !string.IsNullOrEmpty(rawFilter);
        if (isChineseSearch && request.type is CompType.Mod or CompType.DataPack)
            _ApplyChineseSearch(request, rawFilter);

        if (request.curseForgeAltSearchText is not null)
        {
            request.curseForgeAltSearchText =
                _ProcessKeywords(request.curseForgeAltSearchText, rawFilter)!;
            LogWrapper.Debug(
                $"[Comp] 工程列表搜索最终文本（CurseForge）：{request.curseForgeAltSearchText}");
        }

        request.searchText = _ProcessKeywords(request.searchText, rawFilter)!;
        LogWrapper.Debug($"[Comp] 工程列表搜索最终文本：{request.searchText}");
        task.Progress = 0.1;

        var realResults = _FetchProjectResults(request, storage, task);
        if (realResults is null)
            return;

        if (request.sort != CompSortType.Default)
        {
            if (task.IsAborted) throw new ThreadInterruptedException();
            storage.results.AddRange(realResults);
            return;
        }

        var scores = _ScoreProjectResults(request, realResults, rawFilter, isChineseSearch);

        if (task.IsAborted) throw new ThreadInterruptedException();
        storage.results.AddRange(
            scores
                .OrderByDescending(s => s.Value)
                .Select(s => s.Key));
    }

    private static void _AddSingleLinkResult(
        string singleLink,
        CompProjectRequest request,
        CompProjectStorage storage)
    {
        if (storage.results.Count != 0) return;

        CompProject? project;
        try
        {
            var projectId = ResolveLinkToProjectId(singleLink);
            project = string.IsNullOrEmpty(projectId)
                ? null
                : CompRequest.GetCompProjectsByIds([projectId]).FirstOrDefault();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Comp] 解析资源链接失败");
            throw new Exception(Lang.Text("Download.Comp.Link.ResolveFailed"));
        }

        if (project is null)
            throw new Exception(Lang.Text("Download.Comp.Link.ResolveFailed"));

        if (request.type != CompType.Any && project.Type != request.type)
            throw new Exception(Lang.Text("Download.Comp.List.NoMatchingResults"));

        storage.results.Add(project);
        storage.curseForgeTotal = 0;
        storage.modrinthTotal = 0;
    }

    private static void _ApplyChineseSearch(CompProjectRequest request, string rawFilter)
    {
        var searchEntries = new List<Common.SearchEntry<CompDatabaseEntry>>();
        using (var conn = CompDb)
        {
            var likeEscaped = rawFilter
                .Replace("\\", @"\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var searchRes = conn
                .Query<CompDatabaseEntry>(
                    @"SELECT * FROM ModTranslation WHERE ChineseName LIKE @p ESCAPE '\' OR CurseForgeSlug LIKE @p ESCAPE '\' OR ModrinthSlug LIKE @p ESCAPE '\'",
                    new
                    {
                        p = $"%{likeEscaped}%"
                    })
                .ToList();
            searchEntries.AddRange(
                from searchItem in searchRes
                where !searchItem.ChineseName.Contains("动态的树")
                select new Common.SearchEntry<CompDatabaseEntry>
                {
                    item = searchItem,
                    searchSource =
                    [
                        new Common.SearchSource(
                            searchItem
                                .ChineseName
                                .BeforeFirst(" (")
                                .Split(['/'], StringSplitOptions.RemoveEmptyEntries)),
                        new Common.SearchSource(
                            searchItem.ChineseName.AfterFirst(" (")
                            + (searchItem.CurseForgeSlug ?? "")
                            + (searchItem.ModrinthSlug ?? ""),
                            0.5)
                    ]
                });
        }

        var searchResults = Common.Search(
            searchEntries,
            request.searchText,
            40,
            0.2);
        if (searchResults.Count == 0)
            throw new Exception(Lang.Text("Download.Comp.List.NoResults"));

        var wordModCount = new Dictionary<string, HashSet<int>>();
        foreach (var result in searchResults)
        foreach (var word in _ExtractSearchWords(result))
        {
            if (result.item is not { } item) continue;
            if (!wordModCount.TryGetValue(word, out var mods))
                wordModCount[word] = mods = [];
            mods.Add(item.WikiId);
        }

        if (wordModCount.Count == 0)
            throw new Exception(Lang.Text("Download.Comp.List.NoResults"));

        var normalizedQuery = _NormalizeSearchName(rawFilter);
        var exactNameEntries = searchResults
            .Where(r => r.item is not null
                && _CanonicalSearchName(r.item.ChineseName) == normalizedQuery)
            .ToList();
        var exactNameMods = exactNameEntries
            .Select(r => r.item)
            .OfType<CompDatabaseEntry>()
            .Select(item => item.WikiId)
            .Distinct()
            .Count();

        if (exactNameMods == 1)
        {
            var canonicalEntry = exactNameEntries
                .OrderByDescending(r => r.absoluteRight)
                .ThenByDescending(r => r.similarity)
                .ThenBy(r =>
                    (r.item?.CurseForgeSlug
                        ?? r.item?.ModrinthSlug
                        ?? r.item?.ChineseName
                        ?? "").Length)
                .First();
            var canonicalWords = _ExtractSearchWords(canonicalEntry);
            request.searchText = canonicalWords.Length != 0
                ? string.Join(" ", canonicalWords)
                : string.Join(
                    " ",
                    wordModCount
                        .OrderByDescending(w => w.Value.Count)
                        .Take(2)
                        .Select(w => w.Key));
            var cfSlugs = exactNameEntries
                .Select(r => r.item?.CurseForgeSlug)
                .OfType<string>()
                .Distinct()
                .ToList();
            if (cfSlugs.Count == 1)
                request.curseForgeAltSearchText = cfSlugs[0];
        }
        else
        {
            var maxCount = wordModCount.Values.Max(mods => mods.Count);
            if (maxCount <= 1)
            {
                var best = searchResults
                    .OrderByDescending(r => r.absoluteRight)
                    .ThenByDescending(r => r.similarity)
                    .ThenBy(r =>
                        (r.item?.CurseForgeSlug
                            ?? r.item?.ModrinthSlug
                            ?? r.item?.ChineseName
                            ?? "")
                        .Length)
                    .First();
                request.searchText = string.Join(" ", _ExtractSearchWords(best));
            }
            else
            {
                var tied = wordModCount
                    .Where(w => w.Value.Count == maxCount)
                    .OrderBy(w => w.Key.Length)
                    .ToList();
                var anchorMods = tied[0].Value;
                request.searchText = string.Join(
                    " ",
                    tied
                        .Where(w => w.Value.Overlaps(anchorMods))
                        .Take(3)
                        .Select(w => w.Key));
            }
        }

        LogWrapper.Debug($"[Comp] 中文搜索基础关键词：{request.searchText}");
    }

    private static string[] _ExtractSearchWords(Common.SearchEntry<CompDatabaseEntry> result)
    {
        if (result.item is not { } item) return [];
        var word = "";
        if (item.CurseForgeSlug is not null)
            word += $"{item.CurseForgeSlug
                .Replace("-", " ")
                .Replace("/", " ")} ";
        if (item.ModrinthSlug is not null)
            word += $"{item.ModrinthSlug
                .Replace("-", " ")
                .Replace("/", " ")} ";
        word += item
            .ChineseName
            .AfterLast(" (")
            .TrimEnd(')', ' ')
            .BeforeFirst(" - ")
            .Replace(":", "")
            .Replace("(", "")
            .Replace(")", "")
            .ToLower()
            .Replace("/", " ")
            .Replace("-", " ");
        var words = word
            .ToLower()
            .Split([' '], StringSplitOptions.RemoveEmptyEntries);
        words =
        [
            .. words
                .Select(w => w
                    .TrimStart('{', '[', '(')
                    .TrimEnd('}', ']', ')'))
                .Where(w =>
                {
                    if (w.Length <= 1)
                        return false;

                    if (!w.Any(char.IsLetterOrDigit))
                        return false;

                    if (new[]
                        {
                            "the",
                            "of",
                            "for",
                            "mod",
                            "and",
                            "forge",
                            "fabric",
                            "quilt",
                            "neoforge"
                        }.Contains(w))
                        return false;

                    if (Common.Val(w) > 0)
                        return false;

                    return w.Split(' ').Length <= 3 || !w.Contains("ftb");
                })
                .Distinct()
        ];
        return words;
    }

    private static string _NormalizeSearchName(string name)
    {
        return new string(
            name
                .Where(c => !char.IsWhiteSpace(c) && !char.IsSurrogate(c))
                .ToArray());
    }

    private static string _CanonicalSearchName(string name)
    {
        return _NormalizeSearchName(name.BeforeFirst(" ("));
    }

    private static string? _ProcessKeywords(string? text, string rawFilter)
    {
        if (text is null) return null;

        text = text.ToLowerInvariant();
        var words = new List<string>();
        foreach (var keyword in text.Split(
                     [' '],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var cleanKeyword = keyword.Trim('[', ']');
            if (string.IsNullOrEmpty(cleanKeyword)) continue;
            if (new[]
                {
                    "forge",
                    "fabric",
                    "for",
                    "mod",
                    "quilt"
                }.Contains(cleanKeyword))
            {
                LogWrapper.Debug($"[Comp] 已跳过搜索关键词：{cleanKeyword}");
                continue;
            }

            words.Add(cleanKeyword);
        }

        text = rawFilter.Length > 0 && words.Count == 0
            ? rawFilter
            : string.Join(" ", words.Distinct());

        var compactFilter = rawFilter.Replace(" ", "");
        if (compactFilter.ContainsF("optiforge", true))
            text = "optiforge";
        if (compactFilter.ContainsF("optifabric", true))
            text = "optifabric";

        return text;
    }

    private static List<CompProject>? _FetchProjectResults(
        CompProjectRequest request,
        CompProjectStorage storage,
        Loading.LoaderTask<CompProjectRequest, int> task)
    {
        var realResults = new List<CompProject>();

        while (true)
        {
            var rawResults = new List<CompProject>();
            Exception? lastError = null;
            var resultsLock = new object();

            var isOldForgeRequest = request.modLoader == CompLoaderType.Forge
                && McInstanceInfo.VersionToDrop(request.gameVersion, true) < 140;
            if (isOldForgeRequest) request.modLoader = CompLoaderType.Any;
            var curseForgeUrl = request.GetCurseForgeAddress();
            var modrinthUrl = request.GetModrinthAddress();
            if (isOldForgeRequest) request.modLoader = CompLoaderType.Forge;

            var tasks = new List<Task>();

            if (curseForgeUrl is not null)
                tasks.Add(
                    Task.Run(() =>
                    {
                        lastError = _FetchCurseForgePage(
                            curseForgeUrl,
                            request,
                            storage,
                            rawResults,
                            resultsLock);
                    }));

            if (modrinthUrl is not null)
                tasks.Add(
                    Task.Run(() =>
                    {
                        lastError = _FetchModrinthPage(
                            modrinthUrl,
                            storage,
                            rawResults,
                            resultsLock);
                    }));

            Task.WaitAll(tasks.ToArray());
            task.Progress += 0.4;
            if (task.IsAborted) return null;

            if (isOldForgeRequest)
                rawResults =
                [
                    .. rawResults.Where(p =>
                        p.ModLoaders.Count == 0 || p.ModLoaders.Contains(CompLoaderType.Forge))
                ];

            if (rawResults.Count == 0)
            {
                if (lastError is not null) throw lastError;
                throw new Exception(Lang.Text("Download.Comp.List.NoResultsSimple"));
            }

            var processedResults = rawResults
                .OrderBy(x => x.FromCurseForge)
                .Where(r => !realResults.Any(r.IsLike) && !storage.results.Any(r.IsLike))
                .ToList();

            realResults.AddRange(processedResults);
            LogWrapper.Info(
                $"[Comp] 去重、筛选后累计新增结果 {processedResults.Count} 个（目前已有结果 {storage.results.Count} 个）");

            if (realResults.Count + storage.results.Count < request.targetResultCount
                && request.CanContinue
                && lastError is null)
            {
                LogWrapper.Info("[Comp] 数量不足，继续加载下一页");
                continue;
            }

            break;
        }

        return realResults;
    }

    private static Exception? _FetchCurseForgePage(
        string curseForgeUrl,
        CompProjectRequest request,
        CompProjectStorage storage,
        List<CompProject> rawResults,
        object resultsLock)
    {
        try
        {
            LogWrapper.Info($"[Comp] 开始从 CurseForge 获取列表：{curseForgeUrl}");
            var json = GameDownload.DlModRequest<JsonObject>(curseForgeUrl);
            var projects = (json["data"]?.AsArray() ?? [])
                .OfType<JsonObject>()
                .Select(j => new CompProject(j))
                .Where(p =>
                    !(request.type == CompType.ResourcePack
                        && p.Tags.Contains(Lang.Text("Download.Comp.Type.DataPack"))))
                .ToList();
            lock (resultsLock)
            {
                rawResults.AddRange(projects);
            }

            storage.curseForgeOffset += projects.Count;
            storage.curseForgeTotal =
                json["pagination"]?["totalCount"]?.ToObject<int>() ?? 0;
            return null;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "CurseForge 获取失败");
            return ex;
        }
    }

    private static Exception? _FetchModrinthPage(
        string modrinthUrl,
        CompProjectStorage storage,
        List<CompProject> rawResults,
        object resultsLock)
    {
        try
        {
            LogWrapper.Info($"[Comp] 开始从 Modrinth 获取列表：{modrinthUrl}");
            var json = GameDownload.DlModRequest<JsonObject>(modrinthUrl);
            var projects = (json["hits"]?.AsArray() ?? [])
                .OfType<JsonObject>()
                .Select(j => new CompProject(j))
                .ToList();
            lock (resultsLock)
            {
                rawResults.AddRange(projects);
            }

            storage.modrinthOffset += projects.Count;
            storage.modrinthTotal = json["total_hits"]?.ToObject<int>() ?? 0;
            return null;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Modrinth 获取失败");
            return ex;
        }
    }

    private static Dictionary<CompProject, double> _ScoreProjectResults(
        CompProjectRequest request,
        List<CompProject> realResults,
        string rawFilter,
        bool isChineseSearch)
    {
        var scores = new Dictionary<CompProject, double>();

        if (string.IsNullOrEmpty(rawFilter))
        {
            foreach (var res in realResults)
                scores.Add(res, res.DownloadCount * _GetDownloadCountMultiplier(request, res));

            return scores;
        }

        var searchEntries = new List<Common.SearchEntry<CompProject>>();
        foreach (var res in realResults)
        {
            scores.Add(
                res,
                (Lang.IsChineseMainland && res.WikiId > 0 ? 0.2 : 0)
                + Math.Log10(
                    Math.Max(res.DownloadCount, 1) * _GetDownloadCountMultiplier(request, res))
                / 9);
            searchEntries.Add(
                new Common.SearchEntry<CompProject>
                {
                    item = res,
                    searchSource =
                    [
                        new Common.SearchSource(
                            (isChineseSearch ? res.TranslatedName : res.RawName)
                            .Split(['/'], StringSplitOptions.RemoveEmptyEntries)),
                        new Common.SearchSource(res.Description, 0.05)
                    ]
                });
        }

        var searchRes = Common.Search(
            searchEntries,
            rawFilter,
            101,
            -1);
        foreach (var item in searchRes)
            if (item.item is not null)
                scores[item.item] +=
                    (item.absoluteRight ? 10 : item.similarity)
                    / (searchRes.First().absoluteRight ? 10 : searchRes.First().similarity);

        return scores;
    }

    private static double _GetDownloadCountMultiplier(
        CompProjectRequest request,
        CompProject project)
    {
        return request.type switch
        {
            CompType.Mod or CompType.ModPack => project.FromCurseForge ? 1 : 7,
            CompType.DataPack => project.FromCurseForge ? 10 : 1,
            CompType.ResourcePack or CompType.Shader => project.FromCurseForge ? 1 : 5,
            _ => 1
        };
    }

    private enum ResourceSite
    {
        None,
        CurseForge,
        Modrinth
    }
}
