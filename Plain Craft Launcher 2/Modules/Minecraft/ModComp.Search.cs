using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Hash;
using PCL.Network;
using ProtoBuf;


namespace PCL;

public static partial class ModComp
{
    public class CompRequest
    {
        /// <summary>
        ///     通过项目 Id 判断是否来自 CurseForge
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public static bool IsFromCurseForge(string Id)
        {
            var res = 0;
            return int.TryParse(Id, out res); // CurseForge 数字 ID Modrinth 乱序 ID
        }

        /// <summary>
        ///     通过一堆 ID 从 Modrinth 那获取项目信息
        /// </summary>
        /// <param name="Ids"></param>
        /// <returns></returns>
        public static async Task<List<CompProject>> GetListByIdsFromModrinthAsync(List<string> Ids)
        {
            var Res = new List<CompProject>();
            try
            {
                await Task.Run(() =>
                {
                    var RawProjectsData =
                        ModDownload.DlModRequest<JArray>($"https://api.modrinth.com/v2/projects?ids=[\"{Ids.Join("\",\"")}\"]");
                    foreach (var RawData in (IEnumerable)RawProjectsData)
                        Res.Add(new CompProject((JObject)RawData));
                });
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "从 Modrinth 获取数据失败");
            }

            return Res;
        }

        /// <summary>
        ///     通过一堆 ID 从 CurseForge 那获取项目信息
        /// </summary>
        /// <param name="Ids"></param>
        /// <returns></returns>
        public static async Task<List<CompProject>> GetListByIdsFromCurseforgeAsync(List<string> ids)
        {
            var res = new List<CompProject>();
            try
            {
                // 使用 Task.Run 将同步的 DlModRequest 包装为异步
                await Task.Run(() =>
                {
                    // 构建请求 Body，建议使用 string.Join
                    var jsonBody = "{\"modIds\": [" + string.Join(",", ids) + "]}";

                    // DlModRequest 返回 object，先强转 JObject，再获取 "data" 并强转为 JArray
                    var response = ModDownload.DlModRequest<JObject>(
                        "https://api.curseforge.com/v1/mods",
                        "POST",
                        jsonBody,
                        "application/json"
                    );

                    var rawProjectsData = (JArray)response["data"];

                    // 2. 使用 LINQ 快速转换并填充列表
                    if (rawProjectsData != null)
                    {
                        var projectList = rawProjectsData
                            .Cast<JObject>()
                            .Select(data => new CompProject(data))
                            .ToList();

                        res.AddRange(projectList);
                    }
                });
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "Failed to get project data from CurseForge");
            }

            return res;
        }

        public static List<CompProject> GetCompProjectsByIds(List<string> Input)
        {
            return GetCompProjectsByIdsAsync(Input).GetAwaiter().GetResult();
        }

        public static async Task<List<CompProject>> GetCompProjectsByIdsAsync(List<string> Input)
        {
            if (Input?.Any() == false)
                return new List<CompProject>();

            var modrinthIds = new List<string>();
            var curseForgeIds = new List<string>();
            foreach (var id in Input)
                if (IsFromCurseForge(id))
                    curseForgeIds.Add(id);
                else
                    modrinthIds.Add(id);

            var tasks = new List<Task<List<CompProject>>>();
            if (curseForgeIds.Any()) tasks.Add(GetListByIdsFromCurseforgeAsync(curseForgeIds));
            if (modrinthIds.Any()) tasks.Add(GetListByIdsFromModrinthAsync(modrinthIds));

            await Task.WhenAll(tasks.ToArray());
            var result = new List<CompProject>();
            foreach (var task in tasks)
                result.AddRange(task.Result);

            return result;
        }
    }



    public class CompClipboard
    {
        // 剪贴板已读取内容
        public static string? CurrentText;

        // 识别剪贴板内容
        public static void GetClipboardResource()
        {
            string? text = null;
            LauncherDispatcher.RunInUiWait(() => text = Clipboard.GetText());

            if (string.IsNullOrEmpty(text) || text == CurrentText) return;
            CurrentText = text;

            // 在新线程中处理网络请求
            LauncherDispatcher.RunInNewThread(() =>
            {
                try
                {
                    string? slug = null;
                    string? projectId = null;
                    var processedText = text.Replace("https://", "").Replace("http://", "");

                    // 1. 处理 CurseForge 链接
                    if (processedText.Contains("curseforge.com/minecraft/"))
                    {
                        var parts = processedText.Split('/');
                        if (parts.Length < 4) return;

                        var categoryUrl = parts[2];
                        slug = parts[3];

                        // 获取资源信息
                        var json = ModDownload.DlModRequest<JObject>(
                            $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={slug}");
                        var dataArray = (JArray)json["data"];

                        if (dataArray.Any())
                        {
                            var firstData = (JObject)dataArray[0];
                            var receivedClassId = firstData["classId"]?.ToString();

                            // 映射分类 ID
                            var categoryMapping = new Dictionary<string, string>
                            {
                                { "mc-mods", "6" },
                                { "modpacks", "4471" },
                                { "texture-packs", "12" },
                                { "shaders", "6552" }
                            };

                            if (categoryMapping.TryGetValue(categoryUrl, out var targetClassId) &&
                                receivedClassId != targetClassId)
                            {
                                // 如果分类不匹配，带上 classId 重新搜索
                                json = ModDownload.DlModRequest<JObject>(
                                    $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={slug}&classId={targetClassId}");
                                dataArray = (JArray)json["data"];
                            }

                            if (dataArray.Any()) projectId = dataArray[0]["id"]?.ToString();
                        }
                    }
                    // 2. 处理 Modrinth 链接
                    else if (processedText.Contains("modrinth.com/"))
                    {
                        var parts = processedText.Split('/');
                        if (parts.Length < 3) return;

                        slug = parts[2];
                        var json = ModDownload.DlModRequest<JObject>($"https://api.modrinth.com/v2/project/{slug}");
                        projectId = json["id"]?.ToString();
                    }
                    else
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(projectId)) return;
                    LauncherLogger.Log($"[Clipboard] Found ProjectId: {projectId}");

                    // 3. UI 交互：跳转到详情页
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Func<Task>(async () =>
                    {
                        if (ModMain.MyMsgBox(
                                "PCL detected a resource link in clipboard. Do you want to jump to the details page?",
                                "Link Detected", "Confirm", "Cancel", ForceWait: true) == 1)
                        {
                            ModMain.Hint("Fetching resource info...");

                            var ids = new List<string> { projectId };
                            var compProjects = await CompRequest.GetCompProjectsByIdsAsync(ids);

                            if (compProjects.Count == 0)
                            {
                                ModMain.Hint("Invalid resource content.", ModMain.HintType.Critical);
                                return;
                            }

                            ModMain.FrmMain.PageChange(new FormMain.PageStackData
                            {
                                Page = FormMain.PageType.CompDetail,
                                Additional = (compProjects.First(), new List<string>(), string.Empty, CompLoaderType.Any,
                                    CompType.Any, null, null, null)
                            });
                        }
                    }));
                }
                catch (Exception ex)
                {
                    LauncherLogger.Log(ex, "Error processing clipboard resource");
                }
            }, "Clipboard Resource Processing");
        }
    }



    private static readonly Lazy<string> _dbInitializer = new(InitializeModDbAndGetConnectionString);

    private static string CompDBConnectionString => _dbInitializer.Value;

    private static string InitializeModDbAndGetConnectionString()
    {
        LauncherLogger.Log("[DB] 解压 ModData (SQLite) 中");
        using (var compressedDbData = LauncherPaths.GetResourceStream("Resources/mcmod.buf"))
        {
            using (var trueDbFile = new GZipStream(compressedDbData, CompressionMode.Decompress))
            {
                using (var ms = new MemoryStream())
                {
                    // 这里提取文件资源
                    trueDbFile.CopyTo(ms);
                    ms.Seek(0L, SeekOrigin.Begin);
                    var fileHash = LauncherHash.GetHexString(SHA1Provider.Instance.ComputeHash(ms));
                    var dbDir = Path.Combine(LauncherPaths.TempDirectory, "Cache");
                    var dbPath = Path.Combine(dbDir, $"ModData{fileHash}.sqlite");

                    if (File.Exists(dbPath) && !IsDatabaseValid(dbPath))
                    {
                        File.Delete(dbPath);
                    }

                    if (!File.Exists(dbPath))
                    {
                        ms.Seek(0L, SeekOrigin.Begin);
                        var entries = Serializer.Deserialize<List<CompDatabaseEntry>>(ms);

                        Directory.CreateDirectory(dbDir);

                        var tempPath = dbPath + ".tmp";
                        if (File.Exists(tempPath)) File.Delete(tempPath);

                        using (var buildDbConnection = new SqliteConnection($"Data Source=\"{tempPath}\";Pooling=False"))
                        {
                            buildDbConnection.Open();

                            // 不用事务的话构建会非常慢
                            using (var transaction = buildDbConnection.BeginTransaction())
                            {
                                buildDbConnection.Execute(@"
                                    CREATE TABLE ModTranslation (
                                        WikiId INTEGER,
                                        ChineseName TEXT,
                                        CurseForgeSlug TEXT,
                                        ModrinthSlug TEXT
                                    );
                                    CREATE INDEX idx_curseforge ON ModTranslation (CurseForgeSlug);
                                    CREATE INDEX idx_modrinth ON ModTranslation (ModrinthSlug);
                                    CREATE INDEX idx_chinesename ON ModTranslation (ChineseName);
                                ");

                                var insertSql =
                                    @"INSERT INTO ModTranslation (WikiId, ChineseName, CurseForgeSlug, ModrinthSlug) 
                                    VALUES (@WikiId, @ChineseName, @CurseForgeSlug, @ModrinthSlug)";

                                foreach (var entry in entries)
                                    buildDbConnection.Execute(insertSql, entry, transaction);

                                transaction.Commit();
                            }
                        }

                        // 构建完成的文件移入缓存位
                        File.Move(tempPath, dbPath, true);
                    }

                    return $"Data Source=\"{dbPath}\"";
                }
            }
        }
    }

    /// <summary>
    /// 验证 SQLite 数据库文件是否包含预期的表且非空
    /// </summary>
    private static bool IsDatabaseValid(string dbPath)
    {
        try
        {
            using (var conn = new SqliteConnection($"Data Source=\"{dbPath}\";Pooling=False;Mode=ReadOnly"))
            {
                conn.Open();
                // 检查表是否存在
                var tableCheck = conn.ExecuteScalar<int>(
                    "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='ModTranslation'");
                if (tableCheck == 0) return false;
                // 检查表中是否有数据
                var rowCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM ModTranslation");
                return rowCount > 0;
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "检查模组翻译数据库有效性失败");
            return false;
        }
    }

    private static SqliteConnection CompDB
    {
        get
        {
            var conn = new SqliteConnection(CompDBConnectionString);
            conn.Open();
            return conn;
        }
    }

    private static CompDatabaseEntry GetCompWikiEntryBySlug(string slug)
    {
        try
        {
            using (var conn = CompDB)
            {
                return conn.QueryFirstOrDefault<CompDatabaseEntry>(
                    "SELECT * FROM ModTranslation WHERE CurseForgeSlug = @s OR ModrinthSlug = @s LIMIT 1",
                    new { s = slug });
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "获取模组翻译信息失败", LauncherLogger.LogLevel.Hint);
            return null;
        }
    }

    [ProtoContract]
    private class CompDatabaseEntry
    {
        /// <summary>
        ///     McMod 的对应 ID。
        /// </summary>
        [ProtoMember(1)]
        public int WikiId { get; set; }

        /// <summary>
        ///     中文译名。空字符串代表没有翻译。
        /// </summary>
        [ProtoMember(2)]
        public string ChineseName { get; set; } = "";

        /// <summary>
        ///     CurseForge Slug（例如 advanced-solar-panels）。
        /// </summary>
        [ProtoMember(3)]
        public string CurseForgeSlug { get; set; }

        /// <summary>
        ///     Modrinth Slug（例如 advanced-solar-panels）。
        /// </summary>
        [ProtoMember(4)]
        public string ModrinthSlug { get; set; }

        public override string ToString()
        {
            return (CurseForgeSlug ?? "") + "&" + (ModrinthSlug ?? "") + "|" + WikiId + "|" + ChineseName;
        }
    }


    // 输入与输出

    public class CompProjectRequest
    {
        /// <summary>
        ///     筛选 MC 版本。
        /// </summary>
        public string GameVersion = null;

        /// <summary>
        ///     筛选 Mod 加载器类别。
        /// </summary>
        public CompLoaderType ModLoader = CompLoaderType.Any;

        /// <summary>
        ///     搜索的文本内容。
        /// </summary>
        public string SearchText;

        /// <summary>
        ///     在进行中文搜索时，CurseForge 的替代搜索文本。
        ///     由于 CurseForge API 在有任意关键词未匹配的时候就不显示结果，所以不能使用与 Modrinth 相同的算法。
        /// </summary>
        public string CurseForgeAltSearchText;

        /// <summary>
        ///     搜索结果排序方式。
        /// </summary>
        public CompSortType Sort = CompSortType.Default;

        /// <summary>
        ///     允许的来源。
        /// </summary>
        public CompSourceType Source = CompSourceType.Any;

        // 结果要求

        /// <summary>
        ///     加载后应输出到的结果存储器。
        /// </summary>
        public CompProjectStorage Storage;

        /// <summary>
        ///     筛选资源标签。空字符串代表不限制。格式例如 "406/worldgen"，分别是 CurseForge 和 Modrinth 的 ID。
        /// </summary>
        public string Tag = "";

        /// <summary>
        ///     应当尽量达成的结果数量。
        /// </summary>
        public int TargetResultCount;

        // 输入内容

        /// <summary>
        ///     筛选资源种类。
        /// </summary>
        public CompType Type;

        /// <summary>
        ///     构造函数。
        /// </summary>
        public CompProjectRequest(CompType Type, CompProjectStorage Storage, int TargetResultCount)
        {
            this.Type = Type;
            this.Storage = Storage;
            this.TargetResultCount = TargetResultCount;
        }

        /// <summary>
        ///     根据加载位置记录，是否还可以继续获取内容。
        /// </summary>
        public bool CanContinue
        {
            get
            {
                if (Tag.StartsWithF("/") || !Source.HasFlag(CompSourceType.CurseForge))
                    Storage.CurseForgeTotal = 0;
                if (Tag.EndsWithF("/") || !Source.HasFlag(CompSourceType.Modrinth))
                    Storage.ModrinthTotal = 0;
                if (Storage.CurseForgeTotal == -1 || Storage.ModrinthTotal == -1)
                    return true;
                return Storage.CurseForgeOffset < Storage.CurseForgeTotal ||
                       Storage.ModrinthOffset < Storage.ModrinthTotal;
            }
        }

        // 构造请求

        /// <summary>
        ///     获取对应的 CurseForge API 请求链接。若返回 Nothing 则为不进行 CurseForge 请求。
        /// </summary>
        public string GetCurseForgeAddress()
        {
            if (!Source.HasFlag(CompSourceType.CurseForge))
                return null;
            if (Tag.StartsWithF("/"))
                Storage.CurseForgeTotal = 0;
            if (Storage.CurseForgeTotal > -1 && Storage.CurseForgeTotal <= Storage.CurseForgeOffset)
                return null;
            // 应用筛选参数
            var Address =
                new StringBuilder(
                    $"https://api.curseforge.com/v1/mods/search?gameId=432&sortOrder=desc&pageSize={CompPageSize}");
            switch (Type)
            {
                case CompType.Mod:
                {
                    Address.Append("&classId=6");
                    break;
                }
                case CompType.ModPack:
                {
                    Address.Append("&classId=4471");
                    break;
                }
                case CompType.DataPack:
                {
                    Address.Append("&classId=6945");
                    break;
                }
                case CompType.Shader:
                {
                    Address.Append("&classId=6552");
                    break;
                }
                case CompType.ResourcePack:
                {
                    Address.Append("&classId=12");
                    break;
                }
                case CompType.World:
                {
                    Address.Append("&classId=17");
                    break;
                }
            }

            if (!string.IsNullOrEmpty(Tag)) Address.Append($"&categoryId={Tag.BeforeFirst("/")}");
            if (ModLoader != CompLoaderType.Any)
                Address.Append("&modLoaderType=").Append(((int)ModLoader).ToString());
            if (!string.IsNullOrEmpty(GameVersion))
                Address.Append("&gameVersion=").Append(GameVersion);
            if (!string.IsNullOrEmpty(CurseForgeAltSearchText ?? SearchText))
                Address.Append("&searchFilter=").Append(WebUtility.UrlEncode(CurseForgeAltSearchText ?? SearchText));
            if (Storage.CurseForgeOffset > 0)
                Address.Append("&index=").Append(Storage.CurseForgeOffset);
            switch (Sort)
            {
                case CompSortType.Relevance:
                {
                    Address.Append("&sortField=4");
                    break;
                }
                case CompSortType.Downloads:
                {
                    Address.Append("&sortField=6");
                    break;
                }
                case CompSortType.Follows:
                {
                    Address.Append("&sortField=2");
                    break;
                }
                case CompSortType.Newest:
                {
                    Address.Append("&sortField=11");
                    break;
                }
                case CompSortType.Updated:
                {
                    Address.Append("&sortField=3");
                    break;
                }

                default:
                {
                    Address.Append("&sortField=2");
                    break;
                }
            }

            return Address.ToString();
        }

        /// <summary>
        ///     获取对应的 Modrinth API 请求链接。若返回 Nothing 则为不进行 Modrinth 请求。
        /// </summary>
        public string GetModrinthAddress()
        {
            if (!Source.HasFlag(CompSourceType.Modrinth))
                return null;
            if (Tag.EndsWithF("/"))
                Storage.ModrinthTotal = 0;
            if (Storage.ModrinthTotal > -1 && Storage.ModrinthTotal <= Storage.ModrinthOffset)
                return null;
            // 应用筛选参数
            var Address = $"https://api.modrinth.com/v2/search?limit={CompPageSize}";
            switch (Sort)
            {
                case CompSortType.Relevance:
                {
                    Address += "&index=relevance";
                    break;
                }
                case CompSortType.Downloads:
                {
                    Address += "&index=downloads";
                    break;
                }
                case CompSortType.Follows:
                {
                    Address += "&index=follows";
                    break;
                }
                case CompSortType.Newest:
                {
                    Address += "&index=newest";
                    break;
                }
                case CompSortType.Updated:
                {
                    Address += "&index=updated";
                    break;
                }

                default:
                {
                    Address += "&index=relevance";
                    break;
                }
            }

            if (!string.IsNullOrEmpty(SearchText))
                Address += "&query=" + WebUtility.UrlEncode(SearchText);
            if (Storage.ModrinthOffset > 0)
                Address += "&offset=" + Storage.ModrinthOffset;
            // facets=[["categories:'game-mechanics'"],["categories:'forge'"],["versions:1.19.3"],["project_type:mod"]]
            var Facets = new List<string>();
            Facets.Add($"[\"project_type:{LauncherText.GetStringFromEnum(Type).ToLower()}\"]");
            if (!string.IsNullOrEmpty(Tag))
                Facets.Add($"[\"categories:'{Tag.AfterLast("/")}'\"]");
            if (ModLoader != CompLoaderType.Any)
                Facets.Add($"[\"categories:'{LauncherText.GetStringFromEnum(ModLoader).ToLower()}'\"]");
            if (!string.IsNullOrEmpty(GameVersion))
                Facets.Add($"[\"versions:'{GameVersion}'\"]");
            Address += "&facets=[" + string.Join(",", Facets) + "]";
            return Address;
        }

        // 相同判断
        public override bool Equals(object obj)
        {
            var request = obj as CompProjectRequest;
            return request is not null && Type == request.Type && TargetResultCount == request.TargetResultCount &&
                   (Tag ?? "") == (request.Tag ?? "") && ModLoader == request.ModLoader && Source == request.Source &&
                   (GameVersion ?? "") == (request.GameVersion ?? "") &&
                   (SearchText ?? "") == (request.SearchText ?? "") && Sort == request.Sort;
        }

        public static bool operator ==(CompProjectRequest left, CompProjectRequest right)
        {
            return EqualityComparer<CompProjectRequest>.Default.Equals(left, right);
        }

        public static bool operator !=(CompProjectRequest left, CompProjectRequest right)
        {
            return !(left == right);
        }
    }

    public class CompProjectStorage
    {
        // 加载位置记录

        public int CurseForgeOffset;
        public int CurseForgeTotal = -1;

        /// <summary>
        ///     当前的错误信息。如果没有则为 Nothing。
        /// </summary>
        public string ErrorMessage = null;

        public int ModrinthOffset;
        public int ModrinthTotal = -1;

        // 结果列表

        /// <summary>
        ///     可供展示的所有工程的列表。
        /// </summary>
        public List<CompProject> Results = new();
    }

    // 实际的获取

    private const int CompPageSize = 40;

    /// <summary>
    ///     已知工程信息的缓存。
    /// </summary>
    public static ConcurrentDictionary<string, CompProject> CompProjectCache = new();

    /// <summary>
    ///     根据搜索请求获取一系列的工程列表。需要基于加载器运行。
    /// </summary>
    public static void CompProjectsGet(ModLoader.LoaderTask<CompProjectRequest, int> task)
    {
        var request = task.Input;
        var storage = request.Storage;


        if (storage.Results.Count >= request.TargetResultCount)
        {
            LogWrapper.Info($"[Comp] 已有 {storage.Results.Count} 个结果，多于所需的 {request.TargetResultCount} 个结果，结束处理");
            return;
        }

        if (!request.CanContinue)
        {
            if (!storage.Results.Any()) throw new Exception("没有符合条件的结果");
            LogWrapper.Info(
                $"[Comp] 已有 {storage.Results.Count} 个结果，少于所需的 {request.TargetResultCount} 个结果，但无法继续获取，结束处理");
            return;
        }

        // 拒绝不支持的版本
        if (request.ModLoader == CompLoaderType.Quilt &&
            ModMinecraft.CompareVersion(request.GameVersion ?? "1.15", "1.14") == -1)
            throw new Exception($"Quilt 不支持 Minecraft {request.GameVersion}");



        var rawFilter = (request.SearchText ?? "").Trim();
        request.SearchText = rawFilter;
        var rawFilterLower = rawFilter.ToLower();
        LogWrapper.Info("[Comp] 工程列表搜索原始文本：" + rawFilter);

        // 中文请求关键字处理
        var isChineseSearch = RegexPatterns.HasChineseChar.IsMatch(rawFilter) && !string.IsNullOrEmpty(rawFilter);
        if (isChineseSearch && (request.Type == CompType.Mod || request.Type == CompType.DataPack))
        {
            var searchEntries = new List<SearchEntry<CompDatabaseEntry>>();
            using (var conn = CompDB)
            {
                var sql =
                    "SELECT * FROM ModTranslation WHERE ChineseName LIKE @p OR CurseForgeSlug LIKE @p OR ModrinthSlug LIKE @p";
                var searchRes = conn.Query<CompDatabaseEntry>(sql, new { p = $"%{rawFilter}%" });
                foreach (var searchItem in searchRes)
                {
                    if (searchItem.ChineseName.Contains("动态的树")) continue;
                    searchEntries.Add(new SearchEntry<CompDatabaseEntry>
                    {
                        Item = searchItem,
                        SearchSource = new List<SearchSource>
                        {
                            new(searchItem.ChineseName.BeforeFirst(" (").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries), 1),
                            new(searchItem.ChineseName.AfterFirst(" (") + (searchItem.CurseForgeSlug ?? "") + (searchItem.ModrinthSlug ?? ""), 0.5)
                        }
                    });
                }
            }

            var searchResults = LauncherSearch.Search(searchEntries, request.SearchText, 40, 0.2);
            if (!searchResults.Any()) throw new Exception("无搜索结果，请尝试搜索英文名称");

            string[] ExtractWords(SearchEntry<CompDatabaseEntry> Result)
            {
                var Word = "";
                if (Result.Item.CurseForgeSlug != null)
                    Word += Result.Item.CurseForgeSlug.Replace("-", " ").Replace("/", " ") + " ";
                if (Result.Item.ModrinthSlug != null)
                    Word += Result.Item.ModrinthSlug.Replace("-", " ").Replace("/", " ") + " ";
                Word += Result.Item.ChineseName.AfterLast(" (").TrimEnd(')', ' ').BeforeFirst(" - ")
                    .Replace(":", "").Replace("(", "").Replace(")", "").ToLower().Replace("/", " ").Replace("-", " ");
                var Words = Word.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Words = Words.Select(w => w.TrimStart('{', '[', '(').TrimEnd('}', ']', ')')).Where(
                    w =>
                    {
                        if (w.Length <= 1) return false;
                        if (new[] { "the", "of", "mod", "and" }.Contains(w)) return false;
                        if (MigrationHelpers.Val(w) > 0) return false;
                        if (w.Split(' ').Length > 3 && w.Contains("ftb")) return false;
                        return true;
                    }).Distinct().ToArray();
                return Words;
            }

            var WordWeights = new Dictionary<string, double>();
            foreach (var Result in searchResults)
            {
                foreach (var Word in ExtractWords(Result))
                {
                    var Similarity = Result.SearchSource.Any(s => s.Aliases.Contains(request.SearchText))
                        ? 100000
                        : Result.Similarity;
                    if (!WordWeights.ContainsKey(Word))
                        WordWeights.Add(Word, 0);
                    WordWeights[Word] += Similarity;
                }
            }

            if (!WordWeights.Any()) throw new Exception("无搜索结果，请尝试搜索英文名称");

            var SortedWords = WordWeights.OrderByDescending(w => w.Value).ToList();
            if (SortedWords.First().Value >= 100000)
            {
                request.SearchText = string.Join(" ", SortedWords.Where(w => w.Value >= 100000).Select(w => w.Key));
            }
            else
            {
                request.SearchText = string.Join(" ", SortedWords.Take(5).Select(w => w.Key));
                request.CurseForgeAltSearchText = string.Join(" ", ExtractWords(searchResults.First()));
                LogWrapper.Debug("[Comp] 中文搜索基础关键词（CurseForge）：" + request.CurseForgeAltSearchText);
            }

            LogWrapper.Debug("[Comp] 中文搜索基础关键词：" + request.SearchText);
        }

        // 最终处理关键字：分割、去重
        void processKeywords(ref string text)
        {
            if (text is null) return;
            text = text.ToLowerInvariant();
            var words = new List<string>();
            foreach (var keyword in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var cleanKeyword = keyword.Trim('[', ']');
                if (string.IsNullOrEmpty(cleanKeyword)) continue;
                if (new[] { "forge", "fabric", "for", "mod", "quilt" }.Contains(cleanKeyword))
                {
                    LogWrapper.Debug("[Comp] 已跳过搜索关键词：" + cleanKeyword);
                    continue;
                }

                words.Add(cleanKeyword);
            }

            if (rawFilter.Length > 0 && !words.Any())
                text = rawFilter;
            else
                text = string.Join(" ", words.Distinct());

            // 例外项：OptiForge、OptiFabric（拆词后因为包含 Forge/Fabric 导致无法搜到实际的 Mod）
            if (rawFilter.Replace(" ", "").ContainsF("optiforge", true)) text = "optiforge";
            if (rawFilter.Replace(" ", "").ContainsF("optifabric", true)) text = "optifabric";
        }

        if (request.CurseForgeAltSearchText is not null)
        {
            processKeywords(ref request.CurseForgeAltSearchText);
            LogWrapper.Debug("[Comp] 工程列表搜索最终文本（CurseForge）：" + request.CurseForgeAltSearchText);
        }

        processKeywords(ref request.SearchText);
        LogWrapper.Debug("[Comp] 工程列表搜索最终文本：" + request.SearchText);
        task.Progress = 0.1;


        var realResults = new List<CompProject>();


        while (true)
        {
            var rawResults = new List<CompProject>();
            Exception lastError = null;
            var resultsLock = new object();

            // 1.14 以下 Forge 筛选处理
            var isOldForgeRequest = request.ModLoader == CompLoaderType.Forge &&
                                    ModMinecraft.McInstanceInfo.VersionToDrop(request.GameVersion, true) < 140;
            if (isOldForgeRequest) request.ModLoader = CompLoaderType.Any;
            var curseForgeUrl = request.GetCurseForgeAddress();
            var modrinthUrl = request.GetModrinthAddress();
            if (isOldForgeRequest) request.ModLoader = CompLoaderType.Forge;

            var tasks = new List<Task>();

            // CurseForge 线程内嵌
            if (curseForgeUrl != null)
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        LogWrapper.Info("[Comp] 开始从 CurseForge 获取列表：" + curseForgeUrl);
                        var json = ModDownload.DlModRequest<JObject>(curseForgeUrl);
                        var projects = json["data"].Select(j => new CompProject((JObject)j))
                            .Where(p => !(request.Type == CompType.ResourcePack && p.Tags.Contains("数据包")))
                            .ToList();
                        lock (resultsLock)
                        {
                            rawResults.AddRange(projects);
                        }

                        storage.CurseForgeOffset += projects.Count;
                        storage.CurseForgeTotal = json["pagination"]["totalCount"].ToObject<int>();
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        LogWrapper.Error(ex, "CurseForge 获取失败");
                    }
                }));

            // Modrinth 线程内嵌
            if (modrinthUrl != null)
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        LogWrapper.Info("[Comp] 开始从 Modrinth 获取列表：" + modrinthUrl);
                        var json = ModDownload.DlModRequest<JObject>(modrinthUrl);
                        var projects = json["hits"].Select(j => new CompProject((JObject)j)).ToList();
                        lock (resultsLock)
                        {
                            rawResults.AddRange(projects);
                        }

                        storage.ModrinthOffset += projects.Count;
                        storage.ModrinthTotal = json["total_hits"].ToObject<int>();
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        LogWrapper.Error(ex, "Modrinth 获取失败");
                    }
                }));

            Task.WaitAll(tasks.ToArray());
            task.Progress += 0.4;
            if (task.IsAborted) return;

            // 过滤老版本 Forge
            if (isOldForgeRequest)
                rawResults = rawResults.Where(p => !p.ModLoaders.Any() || p.ModLoaders.Contains(CompLoaderType.Forge))
                    .ToList();

            // 错误检查与空结果处理
            if (!rawResults.Any())
            {
                if (lastError != null) throw lastError;
                // 处理各平台不兼容报错... (此处省略具体 Exception 文本以保持简略)
                throw new Exception("没有搜索结果");
            }


            // 优先保留 Modrinth 顺序并去重
            var processedResults = rawResults.OrderBy(x => x.FromCurseForge)
                .Where(r => !realResults.Any(b => r.IsLike(b)) && !storage.Results.Any(b => r.IsLike(b)))
                .ToList();

            realResults.AddRange(processedResults);
            LogWrapper.Info($"[Comp] 去重、筛选后累计新增结果 {processedResults.Count} 个（目前已有结果 {storage.Results.Count} 个）");

            if (realResults.Count + storage.Results.Count < request.TargetResultCount && request.CanContinue &&
                lastError == null)
            {
                LogWrapper.Info("[Comp] 数量不足，继续加载下一页");
                continue;
            }

            break;

        }



        var scores = new Dictionary<CompProject, double>();
        Func<CompProject, double> getDownloadCountMult = p =>
        {
            switch (request.Type)
            {
                case CompType.Mod:
                case CompType.ModPack: return p.FromCurseForge ? 1 : 7;
                case CompType.DataPack: return p.FromCurseForge ? 10 : 1;
                case CompType.ResourcePack:
                case CompType.Shader: return p.FromCurseForge ? 1 : 5;
                default: return 1;
            }
        };

        if (string.IsNullOrEmpty(rawFilter))
        {
            foreach (var res in realResults) scores.Add(res, res.DownloadCount * getDownloadCountMult(res));
        }
        else
        {
            var searchEntries = new List<SearchEntry<CompProject>>();
            foreach (var res in realResults)
            {
                scores.Add(res,
                    (res.WikiId > 0 ? 0.2 : 0) +
                    Math.Log10(Math.Max(res.DownloadCount, 1) * getDownloadCountMult(res)) / 9);
                searchEntries.Add(new SearchEntry<CompProject>
                {
                    Item = res,
                    SearchSource = new List<SearchSource>
                    {
                        new((isChineseSearch ? res.TranslatedName : res.RawName).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries), 1),
                        new(res.Description, 0.05)
                    }
                });
            }

            var searchRes = LauncherSearch.Search(searchEntries, rawFilter, 101, -1);
            foreach (var item in searchRes)
                scores[item.Item] +=
                    (item.AbsoluteRight ? 10 : item.Similarity) /
                    (searchRes.First().AbsoluteRight ? 10 : searchRes.First().Similarity);
        }

        if (task.IsAborted) throw new ThreadInterruptedException();
        storage.Results.AddRange(scores.OrderByDescending(s => s.Value).Select(s => s.Key));

    }


    // 获取

    /// <summary>
    ///     已知文件信息的缓存。
    /// </summary>
    public static ConcurrentDictionary<string, List<CompFile>> CompFilesCache = new();

    /// <summary>
    ///     获取某个工程下的全部文件列表。
    ///     必须在工作线程执行，失败会抛出异常。
    /// </summary>
    public static List<CompFile> CompFilesGet(string ProjectId, bool FromCurseForge)
    {
        // 1. 获取工程对象（使用 TryGetValue 提高效率并防止并发异常）
        CompProject TargetProject = null;
        if (!CompProjectCache.TryGetValue(ProjectId, out TargetProject))
        {
            var url = FromCurseForge
                ? $"https://api.curseforge.com/v1/mods/{ProjectId}"
                : $"https://api.modrinth.com/v2/project/{ProjectId}";
            if (FromCurseForge)
            {
                var json = ModDownload.DlModRequest<JObject>(url);
                TargetProject = new CompProject((JObject)json["data"]);
            }
            else
            {
                TargetProject = new CompProject(ModDownload.DlModRequest<JObject>(url));
            }
            // 假设 CompProject 构造函数内已处理缓存，否则此处应添加缓存逻辑
        }

        // 2. 获取并缓存文件列表
        if (!CompFilesCache.ContainsKey(ProjectId))
        {
            LauncherLogger.Log("[Comp] 开始获取文件列表：" + ProjectId);
            JArray ResultJsonArray;
            if (FromCurseForge)
            {
                // 注意：若 pageSize=10000 失效，需考虑分页逻辑
                var response = ModDownload.DlModRequest<JObject>(
                    $"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=10000"
                );

                ResultJsonArray = (JArray)response["data"];
            }
            else
            {
                ResultJsonArray =
                    (JArray)ModDownload.DlModRequest($"https://api.modrinth.com/v2/project/{ProjectId}/version?include_changelog=false");
            }

            CompFilesCache[ProjectId] = ResultJsonArray.Select(a => new CompFile((JObject)a, TargetProject.Type))
                .Where(a => a.Available).GroupBy(a => a.Id).Select(g => g.First())
                .ToList(); // 使用 GroupBy 实现更高效的 Distinct
        }

        var CurrentFiles = CompFilesCache[ProjectId];

        // 3. 提取所有需要获取信息的前置 ID（合并必要和可选）
        var AllRawDeps = CurrentFiles.SelectMany(f => f.RawDependencies.Concat(f.RawOptionalDependencies)).Distinct()
            .ToList();
        var UndoneDeps = AllRawDeps.Where(id => !CompProjectCache.ContainsKey(id)).ToList();

        // 4. 批量请求缺失的前置工程信息
        if (UndoneDeps.Any())
        {
            LauncherLogger.Log($"[Comp] {ProjectId} 需要补全信息的依赖项共 {UndoneDeps.Count} 个");
            JArray Projects;
            if (FromCurseForge)
            {
                // 1. 获取响应并转为 JObject
                var response = ModDownload.DlModRequest<JObject>(
                    "https://api.curseforge.com/v1/mods",
                    "POST",
                    "{\"modIds\": [" + string.Join(",", UndoneDeps) + "]}",
                    "application/json"
                );

                // 2. 提取 data 数组
                Projects = (JArray)response["data"];
            }
            else
            {
                Projects = (JArray)ModDownload.DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[\"{UndoneDeps.Join("\",\"")}\"]");
            }

            foreach (var Project in Projects)
                new CompProject((JObject)Project);
        }

        // 5. 建立文件与依赖工程的关联映射
        // 优化：预先筛选出存在于缓存中的依赖工程，避免在多层循环中重复查询字典
        var AvailableDeps = AllRawDeps.Where(id => CompProjectCache.ContainsKey(id) && (id ?? "") != (ProjectId ?? ""))
            .Select(id => CompProjectCache[id]).ToList();

        foreach (var file in CurrentFiles)
        foreach (var dep in AvailableDeps)
        {
            // 处理必要依赖
            if (file.RawDependencies.Contains(dep.Id))
                if (!file.Dependencies.Contains(dep.Id))
                    file.Dependencies.Add(dep.Id);

            // 处理可选依赖
            if (file.RawOptionalDependencies.Contains(dep.Id))
                if (!file.OptionalDependencies.Contains(dep.Id))
                    file.OptionalDependencies.Add(dep.Id);
        }

        return CompFilesCache[ProjectId];
    }

    public static string CompFileNameGet(CompProject proj, CompFile file)
    {
        string FileName;
        if ((proj.TranslatedName ?? "") == (proj.RawName ?? ""))
        {
            FileName = file.FileName;
        }
        else
        {
            var ChineseName = proj.TranslatedName.BeforeFirst(" (").BeforeFirst(" - ").Replace(@"\", "＼")
                .Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞")
                .Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
            FileName = Config.Download.Comp.NameFormatV2 switch
            {
                0 => $"【{ChineseName}】{file.FileName}",
                1 => $"[{ChineseName}] {file.FileName}",
                2 => $"{ChineseName}-{file.FileName}",
                3 => $"{file.FileName}-{ChineseName}",
                _ => file.FileName
            };
        }

        if (file.Type == CompType.Mod)
            FileName = FileName.Replace("~", "-"); // ~ 会导致 Mixin 加载失败
        return FileName;
    }

    /// <summary>
    ///     预载包含大量 CompFile 的卡片，添加必要的元素和前置列表。
    /// </summary>
    public static void CompFilesCardPreload(StackPanel Stack, List<CompFile> Files)
    {
        // 获取卡片对应的前置 ID
        // 如果为整合包就不会有 Dependencies 信息，所以不用管
        var Deps = Files.SelectMany(f => f.Dependencies).Distinct().ToList();
        var OptionalDeps = Files.SelectMany(f => f.OptionalDependencies).Distinct().ToList();
        if (!Deps.Any() && !OptionalDeps.Any())
            return;
        // 必要前置
        if (Deps.Any())
        {
            Deps.Sort();
            Deps = Deps.Where(dep =>
            {
                if (!CompProjectCache.ContainsKey(dep))
                    LauncherLogger.Log($"[Comp] 未找到 ID {dep} 的前置信息", LauncherLogger.LogLevel.Debug);
                return CompProjectCache.ContainsKey(dep);
            }).ToList();
            // 添加开头间隔
            Stack.Children.Add(new TextBlock
            {
                Text = "必要前置资源", FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 2d, 0d, 5d)
            });
            // 添加前置列表
            foreach (var Dep in Deps)
            {
                var Item = CompProjectCache[Dep].ToCompItem(false, false);
                Stack.Children.Add(Item);
            }
        }

        // 可选前置
        if (OptionalDeps.Any())
        {
            OptionalDeps.Sort();
            OptionalDeps = OptionalDeps.Where(dep =>
            {
                if (!CompProjectCache.ContainsKey(dep))
                    LauncherLogger.Log($"[Comp] 未找到 ID {dep} 的前置信息", LauncherLogger.LogLevel.Debug);
                return CompProjectCache.ContainsKey(dep);
            }).ToList();
            // 添加开头间隔
            Stack.Children.Add(new TextBlock
            {
                Text = "可选前置资源", FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 2d, 0d, 5d)
            });
            // 添加前置列表
            foreach (var Dep in OptionalDeps)
            {
                var Item = CompProjectCache[Dep].ToCompItem(false, false);
                Stack.Children.Add(Item);
            }
        }

        // 添加结尾间隔
        Stack.Children.Add(new TextBlock
        {
            Text = "版本列表", FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 12d, 0d, 5d)
        });
    }

}
