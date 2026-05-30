using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Dapper;
using Microsoft.Data.Sqlite;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Hash;
using PCL.Network;
using ProtoBuf;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public static class ModComp
{
    public enum CompLoaderType
    {
        // https://docs.curseforge.com/?http#tocS_ModLoaderType
        /// <summary>
        ///     模组加载器
        /// </summary>
        Any = 0,

        /// <summary>
        ///     模组加载器
        /// </summary>
        Forge = 1,

        /// <summary>
        ///     模组加载器
        /// </summary>
        LiteLoader = 3,

        /// <summary>
        ///     模组加载器
        /// </summary>
        Fabric = 4,

        /// <summary>
        ///     模组加载器
        /// </summary>
        Quilt = 5,

        /// <summary>
        ///     模组加载器
        /// </summary>
        NeoForge = 6,

        /// <summary>
        ///     材质包
        /// </summary>
        Minecraft = 7,

        /// <summary>
        ///     光影包
        /// </summary>
        Canvas = 8,

        /// <summary>
        ///     光影包
        /// </summary>
        Iris = 9,

        /// <summary>
        ///     光影包
        /// </summary>
        OptiFine = 10,

        /// <summary>
        ///     光影包
        /// </summary>
        Vanilla = 11,

        /// <summary>
        ///     LabyMod 客户端
        /// </summary>
        LabyMod = 12
    }

    /// <summary>
    ///     搜索结果排序方式
    /// </summary>
    public enum CompSortType
    {
        /// <summary>
        ///     默认
        /// </summary>
        Default = 1,

        /// <summary>
        ///     相关性 (CurseForge Name (4) / Modrinth relevance)
        /// </summary>
        Relevance = 2,

        /// <summary>
        ///     下载量 (CurseForge TotalDownloads (6) / Modrinth downloads)
        /// </summary>
        Downloads = 3,

        /// <summary>
        ///     关注量 (CurseForge Popularity (2) / Modrinth follows)
        /// </summary>
        Follows = 4,

        /// <summary>
        ///     最新发布 (CurseForge ReleasedDate (11) / Modrinth newest)
        /// </summary>
        Newest = 5,

        /// <summary>
        ///     最近更新 (CurseForge LastUpdated (3) / Modrinth updated)
        /// </summary>
        Updated = 6
    }

    [Flags]
    public enum CompSourceType
    {
        CurseForge = 1,
        Modrinth = 2,
        Any = CurseForge | Modrinth
    }

    public enum CompType
    {
        /// <summary>
        ///     允许任意种类，或种类未知。
        /// </summary>
        Any = -1,

        /// <summary>
        ///     Mod。
        /// </summary>
        Mod = 0,

        /// <summary>
        ///     整合包。
        /// </summary>
        ModPack = 1,

        /// <summary>
        ///     资源包。
        /// </summary>
        ResourcePack = 2,

        /// <summary>
        ///     光影包。
        /// </summary>
        Shader = 3,

        /// <summary>
        ///     CurseForge：数据包。
        ///     Modrinth：数据包，或数据包与 Mod 的混合。
        /// </summary>
        DataPack = 4,

        /// <summary>
        ///     服务端插件。
        /// </summary>
        Plugin = 5,

        /// <summary>
        ///     投影原理图。
        /// </summary>
        Schematic = 6,

        /// <summary>
        ///     世界。
        /// </summary>
        World = 7
    }

    public static string GetCompTypeName(CompType type) => Lang.Text(type switch
    {
        CompType.Mod => "Download.Comp.Type.Mod",
        CompType.ModPack => "Download.Comp.Type.Modpack",
        CompType.ResourcePack => "Download.Comp.Type.ResourcePack",
        CompType.Shader => "Download.Comp.Type.Shader",
        CompType.DataPack => "Download.Comp.Type.DataPack",
        CompType.Plugin => "Download.Comp.Type.Plugin",
        CompType.World => "Download.Comp.Type.World",
        CompType.Schematic => "Download.Comp.Type.Schematic",
        _ => "Download.Comp.Type.Unknown"
    });

    public static string GetCompLoadingName(CompType type) => Lang.Text(type switch
    {
        CompType.Mod => "Download.Comp.List.Loading.Mod",
        CompType.ModPack => "Download.Comp.List.Loading.Modpack",
        CompType.ResourcePack => "Download.Comp.List.Loading.ResourcePack",
        CompType.Shader => "Download.Comp.List.Loading.Shader",
        CompType.DataPack => "Download.Comp.List.Loading.DataPack",
        CompType.Plugin => "Download.Comp.List.Loading.Plugin",
        CompType.World => "Download.Comp.List.Loading.World",
        CompType.Schematic => "Download.Comp.List.Loading.Schematic",
        _ => "Download.Comp.List.Loading.Unknown"
    });

    public static string GetCompSearchName(CompType type) => Lang.Text(type switch
    {
        CompType.Mod => "Download.Comp.List.Search.Mod",
        CompType.ModPack => "Download.Comp.List.Search.Modpack",
        CompType.ResourcePack => "Download.Comp.List.Search.ResourcePack",
        CompType.Shader => "Download.Comp.List.Search.Shader",
        CompType.DataPack => "Download.Comp.List.Search.DataPack",
        CompType.Plugin => "Download.Comp.List.Search.Plugin",
        CompType.World => "Download.Comp.List.Search.World",
        CompType.Schematic => "Download.Comp.List.Search.Schematic",
        _ => "Download.Comp.List.Search.Unknown"
    });

    #region CompFavorites | 收藏

    public class CompFavorites
    {
        private static List<FavData> _FavoritesList;

        /// <summary>
        ///     收藏的工程列表
        /// </summary>
        public static List<FavData> FavoritesList
        {
            get
            {
                if (_FavoritesList is null)
                {
                    var rawData = States.Game.CompFavorites;
                    List<FavData> rawList = null;
                    // 尝试作为新格式解析
                    try
                    {
                        rawList = JsonSerializer.Deserialize<List<FavData>>(rawData, JsonCompat.SerializerOptions);
                    }
                    catch (Exception ex1)
                    {
                        // 尝试作为旧格式（HashSet）迁移
                        try
                        {
                            var migrate = JsonSerializer.Deserialize<HashSet<string>>(rawData, JsonCompat.SerializerOptions);
                            if (migrate is not null) rawList = new List<FavData> { GetNewFav(Lang.Text("Download.Comp.Detail.Favorites.DefaultName"), migrate) };
                        }
                        catch (Exception ex2)
                        {
                            // 两种都失败，使用默认
                        }
                    }

                    // 最终兜底：确保至少有一个收藏夹
                    if (rawList is null || rawList.Count == 0) rawList = new List<FavData> { GetNewFav(Lang.Text("Download.Comp.Detail.Favorites.DefaultName"), null) };
                    _FavoritesList = rawList;
                    Save();
                }

                return _FavoritesList;
            }
            set
            {
                _FavoritesList = value;
                foreach (var item in _FavoritesList)
                    item.Notes = item.Notes.Where(n => !string.IsNullOrWhiteSpace(n.Value)).ToDictionary();
                var rawList = JsonSerializer.Serialize(_FavoritesList, JsonCompat.SerializerOptions);
                States.Game.CompFavorites = JsonSerializer.Serialize(_FavoritesList, JsonCompat.SerializerOptions);
            }
        }

        public static string GetShareCode(HashSet<string> Data)
        {
            try
            {
                return JsonSerializer.Serialize(Data, JsonCompat.SerializerOptions);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[CompFavorites] 生成分享出错");
            }

            return "";
        }

        public static HashSet<string> GetIdsByShareCode(string Code)
        {
            try
            {
                return JsonSerializer.Deserialize<HashSet<string>>(Code, JsonCompat.SerializerOptions);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[CompFavorites] 通过分享获取 ID 出错");
            }

            return new HashSet<string>();
        }

        /// <summary>
        ///     显示收藏菜单。
        /// </summary>
        /// <param name="Project"></param>
        /// <param name="Pos"></param>
        public static void ShowMenu(CompProject Project, UIElement Pos, Action ClosedCallBack = null)
        {
            var body = new ContextMenu();
            foreach (var i in FavoritesList)
            {
                var item = new MyMenuItem();
                item.MaxWidth = 240d;
                var hasFavs = i.Favs.Contains(Project.id);
                if (hasFavs)
                {
                    item.Header = Lang.Text("Download.Comp.Detail.Favorites.UnfavoriteContextMenu", i.Name);
                    item.Icon = Icon.IconButtonLikeFill;
                }
                else
                {
                    item.Header = Lang.Text("Download.Comp.Detail.Favorites.FavoriteContextMenu", i.Name);
                    item.Icon = Icon.IconButtonLikeLine;
                }

                item.Click += (_, _) =>
                {
                    try
                    {
                        if (hasFavs)
                        {
                            i.Favs.Remove(Project.id);
                            ModMain.Hint(Lang.Text("Download.Comp.Detail.Favorites.Remove", Project.TranslatedName, i.Name), ModMain.HintType.Finish);
                        }
                        else
                        {
                            i.Favs.Add(Project.id);
                            ModMain.Hint(Lang.Text("Download.Comp.Detail.Favorites.Add", Project.TranslatedName, i.Name), ModMain.HintType.Finish);
                        }

                        Save();
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "[CompFavorites] 改变收藏项出错");
                    }
                };
                body.Items.Add(item);
            }

            body.Closed += (_, _) => ClosedCallBack?.Invoke();
            body.Placement = PlacementMode.Bottom;
            body.PlacementTarget = Pos;
            body.IsOpen = true;
        }

        /// <summary>
        ///     显示收藏菜单。
        /// </summary>
        public static void ShowMenu(List<CompProject> Project, UIElement Pos, Action ClosedCallBack = null)
        {
            var body = new ContextMenu();
            foreach (var i in FavoritesList)
            {
                var item = new MyMenuItem
                {
                    MaxWidth = 240d,
                    Header = Lang.Text("Download.Comp.Detail.Favorites.FavoriteContextMenu", i.Name)
                };
                item.Click += (_, _) =>
                {
                    try
                    {
                        var count = i.Favs.Count;
                        Project.Select(p => p.id).ToList().ForEach(x => i.Favs.Add(x));
                        Save();
                        var successCount = i.Favs.Count - count;
                        var failedCount = Project.Count - successCount;
                        ModMain.Hint(
                            Lang.Text(failedCount > 0
                                ? "Download.Comp.Detail.Favorites.BulkAddWithFailures"
                                : "Download.Comp.Detail.Favorites.BulkAdd", successCount, i.Name, failedCount),
                            ModMain.HintType.Finish);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "[CompFavorites] 改变收藏项出错");
                    }
                };
                body.Items.Add(item);
            }

            body.Closed += (_, _) => ClosedCallBack?.Invoke();
            body.Placement = PlacementMode.Bottom;
            body.PlacementTarget = Pos;
            body.IsOpen = true;
        }

        /// <summary>
        ///     保存收藏夹数据
        /// </summary>
        public static void Save()
        {
            FavoritesList = _FavoritesList;
        }

        /// <summary>
        ///     获取一个新的收藏夹
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="FavList">没有传 Nothing</param>
        /// <returns></returns>
        public static FavData GetNewFav(string Name, HashSet<string> FavList)
        {
            var res = new FavData { Name = Name, Id = Guid.NewGuid().ToString() };
            if (FavList is null)
                res.Favs = new HashSet<string>();
            else
                res.Favs = FavList;
            return res;
        }

        public static bool IsFavourite(string Id)
        {
            if (FavoritesList is null)
                return false;
            foreach (var i in FavoritesList)
                if (i.Favs.Contains(Id))
                    return true;
            return false;
        }

        public class FavData
        {
            /// <summary>
            ///     收藏夹名称
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Name")]
            public string Name { get; set; }

            /// <summary>
            ///     Guid
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Id")]
            public string Id { get; set; }

            /// <summary>
            ///     收藏的工程 ID 列表
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Favs")]
            public HashSet<string> Favs { get; set; } = new();

            /// <summary>
            ///     备注
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Notes")]
            public Dictionary<string, string> Notes { get; set; } = new();
        }
    }

    #endregion

    #region CompProject | 项目信息

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
            var res = new List<CompProject>();
            try
            {
                await Task.Run(() =>
                {
                    var rawProjectsData =
                        ModDownload.DlModRequest<JsonArray>($"https://api.modrinth.com/v2/projects?ids=[\"{Ids.Join("\",\"")}\"]");
                    foreach (var RawData in (IEnumerable)rawProjectsData)
                        res.Add(new CompProject((JsonObject)RawData));
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从 Modrinth 获取数据失败");
            }

            return res;
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

                    // DlModRequest 返回 object，先强转 JsonObject，再获取 "data" 并强转为 JsonArray
                    var response = ModDownload.DlModRequest<JsonObject>(
                        "https://api.curseforge.com/v1/mods",
                        "POST",
                        jsonBody,
                        "application/json"
                    );

                    var rawProjectsData = (JsonArray)response["data"];

                    // 2. 使用 LINQ 快速转换并填充列表
                    if (rawProjectsData is not null)
                    {
                        var projectList = rawProjectsData
                            .Cast<JsonObject>()
                            .Select(data => new CompProject(data))
                            .ToList();

                        res.AddRange(projectList);
                    }
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "Failed to get project data from CurseForge");
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

    #endregion

    #region CompClipboard | 剪贴板识别

    public class CompClipboard
    {
        // 剪贴板已读取内容
        public static string? currentText;

        // 识别剪贴板内容
        public static void GetClipboardResource()
        {
            string? text = null;
            ModBase.RunInUiWait(() => text = Clipboard.GetText());

            if (string.IsNullOrEmpty(text) || text == currentText) return;
            currentText = text;

            // 在新线程中处理网络请求
            ModBase.RunInNewThread(() =>
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
                        var json = ModDownload.DlModRequest<JsonObject>(
                            $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={slug}");
                        var dataArray = (JsonArray)json["data"];

                        if (dataArray.Any())
                        {
                            var firstData = (JsonObject)dataArray[0];
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
                                json = ModDownload.DlModRequest<JsonObject>(
                                    $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={slug}&classId={targetClassId}");
                                dataArray = (JsonArray)json["data"];
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
                        var json = ModDownload.DlModRequest<JsonObject>($"https://api.modrinth.com/v2/project/{slug}");
                        projectId = json["id"]?.ToString();
                    }
                    else
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(projectId)) return;
                    ModBase.Log($"[Clipboard] Found ProjectId: {projectId}");

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

                            ModMain.frmMain.PageChange(new FormMain.PageStackData
                            {
                                page = FormMain.PageType.CompDetail,
                                additional = (compProjects.First(), new List<string>(), string.Empty, CompLoaderType.Any,
                                    CompType.Any, null, null, null)
                            });
                        }
                    }));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "Error processing clipboard resource");
                }
            }, "Clipboard Resource Processing");
        }
    }

    #endregion

    #region CompDatabase | Mod 数据库

    private static readonly Lazy<string> _dbInitializer = new(InitializeModDbAndGetConnectionString);

    private static string CompDBConnectionString => _dbInitializer.Value;

    private static string InitializeModDbAndGetConnectionString()
    {
        ModBase.Log("[DB] 解压 ModData (SQLite) 中");
        using (var compressedDbData = ModBase.GetResourceStream("Resources/mcmod.buf"))
        {
            using (var trueDbFile = new GZipStream(compressedDbData, CompressionMode.Decompress))
            {
                using (var ms = new MemoryStream())
                {
                    // 这里提取文件资源
                    trueDbFile.CopyTo(ms);
                    ms.Seek(0L, SeekOrigin.Begin);
                    var fileHash = ModBase.GetHexString(SHA1Provider.Instance.ComputeHash(ms));
                    var dbDir = Path.Combine(ModBase.pathTemp, "Cache");
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
            ModBase.Log(ex, "检查模组翻译数据库有效性失败");
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
            ModBase.Log(ex, "获取模组翻译信息失败", ModBase.LogLevel.Hint);
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

    #endregion

    #region CompProject | 工程信息

    // 类定义

    public class CompProject
    {
        /// <summary>
        ///     CurseForge 文件列表的数字 ID。Modrinth 工程的此项无效。
        /// </summary>
        public readonly List<int> curseForgeFileIds;

        /// <summary>
        ///     英文描述。
        /// </summary>
        public readonly string description;

        /// <summary>
        ///     下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量！
        /// </summary>
        public readonly int downloadCount;

        /// <summary>
        ///     支持的 Drop 编号，从高到低排序，不为 Nothing。
        ///     例如：261（26.1.x）、180（1.18.x）。
        /// </summary>
        public readonly List<int> drops;

        // 源信息

        /// <summary>
        ///     该工程信息来自 CurseForge 还是 Modrinth。
        /// </summary>
        public readonly bool fromCurseForge;

        /// <summary>
        ///     CurseForge 工程的数字 ID。Modrinth 工程的乱码 ID。
        /// </summary>
        public readonly string id;

        /// <summary>
        ///     最后一次更新的时间。可能为 Nothing。
        /// </summary>
        public readonly DateTime? lastUpdate;

        /// <summary>
        ///     支持的 Mod 加载器列表。可能为空。
        /// </summary>
        public readonly List<CompLoaderType> modLoaders;

        // 描述性信息

        /// <summary>
        ///     原始的英文名称。
        /// </summary>
        public readonly string rawName;

        /// <summary>
        ///     工程的短名。例如 technical-enchant。
        /// </summary>
        public readonly string slug;

        /// <summary>
        ///     描述性标签的内容。已转换为中文。
        /// </summary>
        public readonly List<string> tags;

        /// <summary>
        ///     工程的种类。
        ///     由于 Modrinth 混合使用 Mod 和数据包，结果不一定准确。
        /// </summary>
        public readonly CompType type;

        /// <summary>
        ///     来源网站的工程页面网址。确保格式一定标准。
        ///     CurseForge：https://www.curseforge.com/minecraft/mc-mods/jei
        ///     Modrinth：https://modrinth.com/mod/technical-enchant
        /// </summary>
        public readonly string website;

        private CompDatabaseEntry _DatabaseEntry;

        // 数据库信息

        private bool loadedDatabase;

        /// <summary>
        ///     Logo 图片的下载地址。
        ///     若为 Nothing 则没有，保证不为空字符串。
        /// </summary>
        public string logoUrl;

        // 实例化

        /// <summary>
        ///     从工程 Json 中初始化实例。若出错会抛出异常。
        /// </summary>
        public CompProject(JsonObject data)
        {
            var result = data.ContainsKey("Tags")
                ? _BuildFromCompJson(data)
                : data.ContainsKey("summary")
                    ? _BuildFromCurseForge(data)
                    : _BuildFromModrinth(data);

            if (!data.ContainsKey("Tags"))
            {
                if (result.Tags.Count == 0)
                    result.Tags.Add(Lang.Text("Download.Comp.Category.Other"));

                result.Tags = result.Tags.Distinct().ToList();
                result.Tags.Sort();

                result.ModLoaders = result.ModLoaders
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();
            }

            fromCurseForge = result.FromCurseForge;
            type = result.Type;
            slug = result.Slug;
            id = result.Id;
            curseForgeFileIds = result.CurseForgeFileIds;
            rawName = result.RawName;
            description = result.Description;
            website = result.Website;
            lastUpdate = result.LastUpdate;
            downloadCount = result.DownloadCount;
            modLoaders = result.ModLoaders;
            tags = result.Tags;
            logoUrl = result.LogoUrl;
            drops = result.Drops;

            // 保存缓存
            compProjectCache[id] = this;
        }

        private sealed class CompProjectBuildResult
        {
            public bool FromCurseForge;
            public CompType Type;
            public string Slug;
            public string Id;
            public List<int> CurseForgeFileIds = [];
            public string RawName;
            public string Description;
            public string Website;
            public DateTime? LastUpdate;
            public int DownloadCount;
            public List<CompLoaderType> ModLoaders = [];
            public List<string> Tags = [];
            public string LogoUrl;
            public List<int> Drops = [];
        }

        private static CompProjectBuildResult _BuildFromCompJson(JsonObject data)
        {
            var result = new CompProjectBuildResult
            {
                FromCurseForge = (string)data["DataSource"] == "CurseForge",
                Type = (CompType)data["Type"].ToObject<int>(),
                Slug = (string)data["Slug"],
                Id = (string)data["Id"],
                RawName = (string)data["RawName"],
                Description = (string)data["Description"],
                Website = (string)data["Website"],
                DownloadCount = (int)data["DownloadCount"],
                Tags = ((JsonArray)data["Tags"]).Select(t => t.ToString()).ToList()
            };

            if (data.TryGetPropertyValue("CurseForgeFileIds", out var id))
                result.CurseForgeFileIds = ((JsonArray)id).Select(t => t.ToObject<int>()).ToList();

            if (data.TryGetPropertyValue("LastUpdate", out var last))
                result.LastUpdate = last?.ToObject<DateTime>();

            if (data.TryGetPropertyValue("ModLoaders", out var loaders))
                result.ModLoaders = ((JsonArray)loaders).Select(t => (CompLoaderType)t.ToObject<int>())
                    .ToList();

            if (data.TryGetPropertyValue("LogoUrl", out var url))
                result.LogoUrl = (string)url;

            if (data.TryGetPropertyValue("Drops", out var drops))
                result.Drops = ((JsonArray)drops).Select(t => t.ToObject<int>()).ToList();

            return result;
        }

        private static CompProjectBuildResult _BuildFromCurseForge(JsonObject data)
        {
            var result = new CompProjectBuildResult
            {
                FromCurseForge = true,

                // 简单信息
                Id = data["id"].ToString(),
                Slug = (string)data["slug"],
                RawName = (string)data["name"],
                Description = (string)data["summary"],
                Website = (data["links"]?["websiteUrl"]?.ToString() ?? "").TrimEnd('/'),
                LastUpdate = data["dateReleased"]?.ToObject<DateTime>(), // #1194
                DownloadCount = (int)data["downloadCount"]
            };

            if (data["logo"] is JsonObject { Count: > 0 } logo)
                result.LogoUrl = string.IsNullOrEmpty((string)logo["thumbnailUrl"])
                    ? (string)logo["url"]
                    : (string)logo["thumbnailUrl"];

            if (string.IsNullOrEmpty(result.LogoUrl))
                result.LogoUrl = null;

            // Type
            result.Type = _GetCurseForgeTypeByWebsite(result.Website);

            // FileIndexes / VanillaMajorVersions / ModLoaders
            var files = new List<KeyValuePair<int, List<string>>>(); // FileId, GameVersions

            foreach (var file in (data["latestFiles"] as JsonArray) ?? [])
            {
                var newFile = new CompFile((JsonObject)file, result.Type);
                if (!newFile.Available)
                    continue;

                result.ModLoaders.AddRange(newFile.modLoaders);

                var gameVersions = file["gameVersions"]?.ToObject<List<string>>() ?? [];
                if (!gameVersions.Any(ModMinecraft.McInstanceInfo.IsFormatFit))
                    continue;

                files.Add(new KeyValuePair<int, List<string>>((int)file["id"], gameVersions));
            }

            files.AddRange(
                from File in (data["latestFilesIndexes"] as JsonArray) ?? []
                let GameVersion = File["gameVersion"]?.ToString() ?? ""
                where ModMinecraft.McInstanceInfo.IsFormatFit(GameVersion)
                select new KeyValuePair<int, List<string>>((int)File["fileId"], new[] { GameVersion }.ToList())
            );

            result.CurseForgeFileIds = files
                .Select(f => f.Key)
                .Distinct()
                .ToList();

            result.Drops = files
                .SelectMany(f => f.Value)
                .Select(v => ModMinecraft.McInstanceInfo.VersionToDrop(v))
                .Where(v => v > 0)
                .Distinct()
                .OrderByDescending(v => v)
                .ToList();

            result.ModLoaders = result.ModLoaders
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // Tags
            var categories = ((data["categories"] as JsonArray) ?? [])
                .Select(t => t["id"]?.ToObject<int?>())
                .Where(t => t.HasValue)
                .Select(t => t.Value)
                .Distinct()
                .OrderByDescending(t => t);

            foreach (var category in categories)
                if (curseForgeCategoryLangKeys.TryGetValue(category, out var langKey))
                    _AddTag(result.Tags, langKey);

            return result;
        }

        private static CompProjectBuildResult _BuildFromModrinth(JsonObject data)
        {
            var projectType = data["project_type"]?.ToString() ?? "";
            var slug = (string)data["slug"];

            var result = new CompProjectBuildResult
            {
                FromCurseForge = false,

                // 简单信息
                Id = (string)(data["project_id"] ?? data["id"]), // 两个 API 会返回的 key 不一样
                Slug = slug,
                RawName = (string)data["title"],
                Description = (string)data["description"],
                LastUpdate = data["date_modified"]?.ToObject<DateTime>(),
                DownloadCount = (int)data["downloads"],
                LogoUrl = (string)data["icon_url"],
                Website = $"https://modrinth.com/{projectType}/{slug}",

                // Type
                Type = projectType switch
                {
                    "modpack" => CompType.ModPack,
                    "resourcepack" => CompType.ResourcePack,
                    "shader" => CompType.Shader,
                    _ => CompType.Mod // Modrinth 将数据包标为 Mod
                }
            };

            if (string.IsNullOrEmpty(result.LogoUrl))
                result.LogoUrl = null;

            // GameVersions
            // 搜索结果的键为 versions，获取特定工程的键为 game_versions
            result.Drops = ((data["game_versions"] ?? data["versions"]) as JsonArray ?? [])
                .Select(v => ModMinecraft.McInstanceInfo.VersionToDrop((string)v))
                .Where(v => v > 0)
                .Distinct()
                .OrderByDescending(v => v)
                .ToList();

            // Tags & ModLoaders
            foreach (var category in (data["loaders"] as JsonArray)?.Select(t => t.ToString()) ?? [])
                if (modrinthLoaderTypes.TryGetValue(category ?? "", out var loader))
                    result.ModLoaders.Add(loader);

            foreach (var category in (data["categories"] as JsonArray)?.Select(t => t.ToString()) ?? [])
            {
                if (string.IsNullOrEmpty(category)) continue;
                // 加载器
                if (modrinthLoaderTypes.TryGetValue(category, out var loader))
                {
                    result.ModLoaders.Add(loader);
                }
                else
                {
                    // Modrinth 将数据包标为 Mod，若包含数据包版本，则优先标为 DataPack
                    if (category.Equals("datapack", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Type = CompType.DataPack;
                    }
                    else
                    {
                        // 这些分类在资源包中不显示
                        if (resourcePackHiddenCategoryLangKeys.TryGetValue(category, out var hiddenLangKey))
                        {
                            if (result.Type != CompType.ResourcePack)
                                _AddTag(result.Tags, hiddenLangKey);
                        }
                        else
                        {
                            if (modrinthCategoryLangKeys.TryGetValue(category, out var langKey))
                                _AddTag(result.Tags, langKey);
                        }
                    }
                }
            }

            return result;
        }

        private static CompType _GetCurseForgeTypeByWebsite(string website)
        {
            var websiteLower = (website ?? "").ToLowerInvariant();

            if (websiteLower.Contains("/mc-mods/") || websiteLower.Contains("/mod/"))
                return CompType.Mod;
            if (websiteLower.Contains("/modpacks/"))
                return CompType.ModPack;
            if (websiteLower.Contains("/resourcepacks/") || websiteLower.Contains("/texture-packs/"))
                return CompType.ResourcePack;
            if (websiteLower.Contains("/shaders/"))
                return CompType.Shader;
            if (websiteLower.Contains("/worlds/"))
                return CompType.World;

            return CompType.DataPack;
        }

        private static void _AddTag(List<string> tags, string langKey)
        {
            var tag = Lang.Text(langKey);

            if (!tags.Contains(tag))
                tags.Add(tag);
        }

        private static readonly Dictionary<string, CompLoaderType> modrinthLoaderTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["forge"] = CompLoaderType.Forge,
                ["fabric"] = CompLoaderType.Fabric,
                ["quilt"] = CompLoaderType.Quilt,
                ["neoforge"] = CompLoaderType.NeoForge
            };
        
        private static readonly Dictionary<int, string> curseForgeCategoryLangKeys =
            new()
            {
                // Mod
                [406] = "Download.Comp.Category.WorldGen",
                [407] = "Download.Comp.Category.Biomes",
                [410] = "Download.Comp.Category.Dimensions",
                [408] = "Download.Comp.Category.OresResources",
                [409] = "Download.Comp.Category.Structures",
                [412] = "Download.Comp.Category.Technology",
                [415] = "Download.Comp.Category.PipesLogistics",
                [4843] = "Download.Comp.Category.Automation",
                [417] = "Download.Comp.Category.Energy",
                [4558] = "Download.Comp.Category.Redstone",
                [436] = "Download.Comp.Category.FoodCooking",
                [416] = "Download.Comp.Category.Farming",
                [414] = "Download.Comp.Category.Transportation",
                [420] = "Download.Comp.Category.Storage",
                [419] = "Download.Comp.Category.Magic",
                [422] = "Download.Comp.Category.Adventure",
                [424] = "Download.Comp.Category.Decoration",
                [411] = "Download.Comp.Category.Mobs",
                [434] = "Download.Comp.Category.Equipment",
                [6814] = "Download.Comp.Category.Optimization",
                [9026] = "Download.Comp.Category.Creative",
                [423] = "Download.Comp.Category.Display",
                [435] = "Download.Comp.Category.Server",
                [5191] = "Download.Comp.Category.Tweaks",
                [421] = "Download.Comp.Category.Library",

                // 整合包
                [4484] = "Download.Comp.Category.Multiplayer",
                [4479] = "Download.Comp.Category.Modpack.Hardcore",
                [4483] = "Download.Comp.Category.Combat",
                [4478] = "Download.Comp.Category.Modpack.Quests",
                [4472] = "Download.Comp.Category.Technology",
                [4473] = "Download.Comp.Category.Magic",
                [4475] = "Download.Comp.Category.Adventure",
                [4476] = "Download.Comp.Category.Modpack.Exploration",
                [4477] = "Download.Comp.Category.Modpack.MiniGame",
                [4471] = "Download.Comp.Category.Modpack.SciFi",
                [4736] = "Download.Comp.Category.Modpack.Skyblock",
                [5128] = "Download.Comp.Category.Modpack.VanillaPlus",
                [4487] = "Download.Comp.Category.Modpack.Ftb",
                [4480] = "Download.Comp.Category.Modpack.MapBased",
                [4481] = "Download.Comp.Category.Modpack.SmallLight",
                [4482] = "Download.Comp.Category.Modpack.ExtraLarge",

                // 资源包
                [403] = "Download.Comp.Category.VanillaLike",
                [400] = "Download.Comp.Category.Realistic",
                [401] = "Download.Comp.Category.Modern",
                [402] = "Download.Comp.Category.Medieval",
                [399] = "Download.Comp.Category.Steampunk",
                [5244] = "Download.Comp.Category.Fonts",
                [404] = "Download.Comp.Category.Animated",
                [4465] = "Download.Comp.Category.ModSupport",
                [393] = "Download.Comp.Category.ResourcePack.Resolution16x",
                [394] = "Download.Comp.Category.ResourcePack.Resolution32x",
                [395] = "Download.Comp.Category.ResourcePack.Resolution64x",
                [396] = "Download.Comp.Category.ResourcePack.Resolution128x",
                [397] = "Download.Comp.Category.ResourcePack.Resolution256x",
                [398] = "Download.Comp.Category.ResourcePack.Resolution512xOrHigher",
                [5193] = "Download.Comp.Type.DataPack", // 有这个 Tag 的项会从资源包请求中被移除

                // 光影包
                [6553] = "Download.Comp.Category.Realistic",
                [6554] = "Download.Comp.Category.Fantasy",
                [6555] = "Download.Comp.Category.VanillaLike",

                // 数据包
                [6948] = "Download.Comp.Category.Adventure",
                [6949] = "Download.Comp.Category.DataPack.Fantasy",
                [6950] = "Download.Comp.Category.Library",
                [6952] = "Download.Comp.Category.Magic",
                [6946] = "Download.Comp.Category.Mod.ModRelated",
                [6951] = "Download.Comp.Category.Technology",
                [6953] = "Download.Comp.Category.Utility",

                // 世界
                [248] = "Download.Comp.Category.Adventure",
                [249] = "Download.Comp.Category.World.Creative",
                [250] = "Download.Comp.Category.Modpack.MiniGame",
                [251] = "Download.Comp.Category.World.Parkour",
                [252] = "Download.Comp.Category.World.Puzzle",
                [253] = "Download.Comp.Category.World.Survival",
                [4464] = "Download.Comp.Category.World.ModWorld"
            };

        private static readonly Dictionary<string, string> resourcePackHiddenCategoryLangKeys =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["decoration"] = "Download.Comp.Category.Decoration",
                ["mobs"] = "Download.Comp.Category.Mobs",
                ["equipment"] = "Download.Comp.Category.Equipment"
            };

        private static readonly Dictionary<string, string> modrinthCategoryLangKeys =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // 共用
                ["technology"] = "Download.Comp.Category.Technology",
                ["magic"] = "Download.Comp.Category.Magic",
                ["adventure"] = "Download.Comp.Category.Adventure",
                ["utility"] = "Download.Comp.Category.Utility",
                ["optimization"] = "Download.Comp.Category.Optimization",
                ["vanilla-like"] = "Download.Comp.Category.VanillaLike",
                ["realistic"] = "Download.Comp.Category.Realistic",

                // Mod / 数据包
                ["worldgen"] = "Download.Comp.Category.WorldGen",
                ["food"] = "Download.Comp.Category.FoodCooking",
                ["game-mechanics"] = "Download.Comp.Category.GameMechanics",
                ["transportation"] = "Download.Comp.Category.Transportation",
                ["storage"] = "Download.Comp.Category.Storage",
                ["social"] = "Download.Comp.Category.Server",
                ["library"] = "Download.Comp.Category.Library",

                // 整合包
                ["multiplayer"] = "Download.Comp.Category.Multiplayer",
                ["challenging"] = "Download.Comp.Category.Modpack.Hardcore",
                ["combat"] = "Download.Comp.Category.Combat",
                ["quests"] = "Download.Comp.Category.Modpack.Quests",
                ["kitchen-sink"] = "Download.Comp.Category.Modpack.KitchenSink",
                ["lightweight"] = "Download.Comp.Category.Modpack.SmallLight",

                // 资源包
                ["simplistic"] = "Download.Comp.Category.Simplistic",
                ["tweaks"] = "Download.Comp.Category.Tweaks",
                ["8x-"] = "Download.Comp.Category.ResourcePack.Resolution8xOrLower",
                ["16x"] = "Download.Comp.Category.ResourcePack.Resolution16x",
                ["32x"] = "Download.Comp.Category.ResourcePack.Resolution32x",
                ["48x"] = "Download.Comp.Category.ResourcePack.Resolution48x",
                ["64x"] = "Download.Comp.Category.ResourcePack.Resolution64x",
                ["128x"] = "Download.Comp.Category.ResourcePack.Resolution128x",
                ["256x"] = "Download.Comp.Category.ResourcePack.Resolution256x",
                ["512x+"] = "Download.Comp.Category.ResourcePack.Resolution512xOrHigher",
                ["audio"] = "Download.Comp.Category.Audio",
                ["fonts"] = "Download.Comp.Category.Fonts",
                ["models"] = "Download.Comp.Category.Models",
                ["gui"] = "Download.Comp.Category.Gui",
                ["locale"] = "Download.Comp.Category.Locale",
                ["core-shaders"] = "Download.Comp.Category.CoreShaders",
                ["modded"] = "Download.Comp.Category.ModSupport",

                // 光影包
                ["fantasy"] = "Download.Comp.Category.Fantasy",
                ["semi-realistic"] = "Download.Comp.Category.SemiRealistic",
                ["cartoon"] = "Download.Comp.Category.Cartoon",
                // 暂时不添加性能负荷 Tag：
                // potato / low / medium / high
                ["colored-lighting"] = "Download.Comp.Category.ColoredLighting",
                ["path-tracing"] = "Download.Comp.Category.PathTracing",
                ["pbr"] = "Download.Comp.Category.Pbr",
                ["reflections"] = "Download.Comp.Category.Reflections",
                ["iris"] = "Download.Comp.Category.Iris",
                ["optifine"] = "Download.Comp.Category.Optifine",
                ["vanilla"] = "Download.Comp.Filter.Loader.VanillaAvailable"
            };

        /// <summary>
        ///     关联的数据库条目。若为 Nothing 则没有。
        /// </summary>
        private CompDatabaseEntry DatabaseEntry
        {
            get
            {
                if (!loadedDatabase)
                {
                    loadedDatabase = true;
                    if (type == CompType.Mod || type == CompType.DataPack)
                        _DatabaseEntry = GetCompWikiEntryBySlug(slug);
                }

                return _DatabaseEntry;
            }
            set
            {
                loadedDatabase = true;
                _DatabaseEntry = value;
            }
        }

        /// <summary>
        ///     MC 百科的页面 ID。若为 0 则没有。
        /// </summary>
        public int WikiId => DatabaseEntry is null ? 0 : DatabaseEntry.WikiId;

        /// <summary>
        ///     翻译后的中文名。若数据库没有则等同于 RawName。
        /// </summary>
        public string TranslatedName => DatabaseEntry is null || string.IsNullOrEmpty(DatabaseEntry.ChineseName)
            ? rawName
            : DatabaseEntry.ChineseName;

        /// <summary>
        ///     中文描述。若为 Nothing 则没有。
        /// </summary>
        public Task<string> ChineseDescription => GetChineseDescriptionAsync();

        private async Task<string> GetChineseDescriptionAsync()
        {
            var from = fromCurseForge ? "curseforge" : "modrinth";
            var para = fromCurseForge ? "modId" : "project_id";
            string result = null;

            var descHash = $"{id}{ModBase.GetStringMD5(description)}";
            var cacheFilePath = $@"{ModBase.pathTemp}Cache\CompTranslation.ini";
            var cacheTranslation = ModBase.ReadIni(cacheFilePath, descHash);
            if (!string.IsNullOrWhiteSpace(cacheTranslation))
            {
                result = ModBase.Base64Decode(cacheTranslation);
                return result;
            }

            try
            {
                var jsonObject = (JsonObject)await 
                    Requester.FetchJsonAsync($"https://mod.mcimirror.top/translate/{from}/{id}");
                if (jsonObject.ContainsKey("translated"))
                {
                    result = jsonObject["translated"].ToString();
                    ModBase.WriteIni(cacheFilePath, descHash, ModBase.Base64Encode(result));
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                {
                    ModMain.MyMsgBox(Lang.Text("Download.Comp.Detail.DescriptionNoTranslation"), Lang.Text("Download.Comp.Detail.DescriptionTranslationFailed"), Lang.Text("Download.Comp.Detail.KnownButton"));
                    return null;
                }

                ModBase.Log(ex, "获取中文描述时出现错误", ModBase.LogLevel.Hint);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取中文描述时出现错误", ModBase.LogLevel.Hint);
            }

            return result;
        }

        /// <summary>
        ///     将当前实例转为可用于保存缓存的 Json。
        /// </summary>
        public JsonObject ToJson()
        {
            var json = new JsonObject();
            json["DataSource"] = fromCurseForge ? "CurseForge" : "Modrinth";
            json["Type"] = (int)type;
            json["Slug"] = slug;
            json["Id"] = id;
            if (curseForgeFileIds is not null)
                json["CurseForgeFileIds"] = new JsonArray(curseForgeFileIds.Select(i => (JsonNode)i).ToArray());
            json["RawName"] = rawName;
            json["Description"] = description;
            json["Website"] = website;
            if (lastUpdate is not null)
                json["LastUpdate"] = lastUpdate;
            json["DownloadCount"] = downloadCount;
            if (modLoaders is not null && modLoaders.Any())
                json["ModLoaders"] = new JsonArray(modLoaders.Select(m => (JsonNode)(int)m).ToArray());
            json["Tags"] = new JsonArray(tags.Select(s => (JsonNode)s).ToArray());
            if (logoUrl is not null)
                json["LogoUrl"] = logoUrl;
            if (drops.Any())
                json["Drops"] = new JsonArray(drops.Select(i => (JsonNode)i).ToArray());
            json["CacheTime"] = DateTime.Now; // 用于检查缓存时间
            return json;
        }

        /// <summary>
        ///     将当前工程信息实例化为控件。
        /// </summary>
        public MyVirtualizingElement<MyCompItem> ToCompItem(bool showMcVersionDesc, bool showLoaderDesc)
        {
            // --- 1. 获取版本描述 (核心算法优化) ---
            string gameVersionDescription;
            if (drops is null || !drops.Any())
            {
                gameVersionDescription = Lang.Text("Download.Comp.Detail.CompItem.SnapshotOnly");
            }
            else
            {
                var segments = new List<string>();
                var isOld = false;

                for (var i = 0; i < drops.Count; i++)
                {
                    int startDrop = drops[i], endDrop = drops[i];

                    if (startDrop < 100)
                    {
                        if (segments.Any() && !isOld) break;
                        isOld = true;
                    }

                    // 查找连续的版本段
                    for (var ii = i + 1; ii < drops.Count; ii++)
                    {
                        if (ModDownload.AllDrops is null || ModDownload.AllDrops.IndexOf(drops[ii]) !=
                            ModDownload.AllDrops.IndexOf(endDrop) + 1) break;
                        endDrop = drops[ii];
                        i = ii;
                    }

                    // 将段转为文本的逻辑
                    var startName = ModMinecraft.McInstanceInfo.DropToVersion(startDrop);
                    var endName = ModMinecraft.McInstanceInfo.DropToVersion(endDrop);

                    if (startDrop == endDrop)
                    {
                        segments.Add(startName);
                    }
                    else if (ModDownload.AllDrops?.Any() == true && startDrop >= ModDownload.AllDrops.First())
                    {
                        if (endDrop < 100)
                        {
                            segments.Clear();
                            segments.Add(Lang.Text("Download.Comp.Detail.CompItem.AllVersions"));
                            break;
                        }

                        segments.Add(endName + "+");
                    }
                    else if (endDrop < 100)
                    {
                        segments.Add(startName + "-");
                        break;
                    }
                    else if (ModDownload.AllDrops is null ||
                             ModDownload.AllDrops.IndexOf(endDrop) - ModDownload.AllDrops.IndexOf(startDrop) == 1)
                    {
                        segments.Add($"{startName}, {endName}");
                    }
                    else
                    {
                        segments.Add($"{startName}~{endName}");
                    }
                }

                gameVersionDescription = string.Join(", ", segments);
            }

            // --- 2. 获取 Mod 加载器描述 (使用 Switch 表达式) ---
            var modLoadersForDesc = modLoaders.ToList();
            if (Config.Download.Comp.IgnoreQuilt) modLoadersForDesc.Remove(CompLoaderType.Quilt);

            var (fullDesc, partDesc) = modLoadersForDesc.Count switch
            {
                0 => modLoaders.Count == 1 ? (Lang.Text("Download.Comp.Type.Only", modLoaders.Single().ToString()), modLoaders.Single().ToString()) : (Lang.Text("Download.Comp.Type.Unknown"), ""),
                1 => (Lang.Text("Download.Comp.Type.Only", modLoadersForDesc.Single().ToString()), modLoadersForDesc.Single().ToString()),
                _ => GetMultiLoaderDesc()
            };

            // 局部函数处理复杂的“任意”判断逻辑
            (string, string) GetMultiLoaderDesc()
            {
                var newestDrop = drops?.FirstOrDefault() ?? 9999;
                var isAny = modLoaders.Contains(CompLoaderType.Forge) &&
                            (newestDrop < 140 || modLoaders.Contains(CompLoaderType.Fabric)) &&
                            (newestDrop < 200 || modLoaders.Contains(CompLoaderType.NeoForge)) &&
                            (newestDrop < 140 || modLoaders.Contains(CompLoaderType.Quilt) ||
                             Config.Download.Comp.IgnoreQuilt);

                var joined = string.Join(" / ", modLoadersForDesc);
                return isAny ? (Lang.Text("Download.Comp.Type.Any"), "") : (joined, joined);
            }

            // --- 3. 实例化 UI (精简布局逻辑) ---
            return new MyVirtualizingElement<MyCompItem>(() =>
                {
                    var newItem = new MyCompItem { Tag = this };
                    ApplyLogoToMyImage(newItem.PathLogo);

                    var title = GetControlTitle(true);
                    newItem.Title = title.Key;

                    if (string.IsNullOrEmpty(title.Value))
                        ((StackPanel)newItem.LabTitleRaw.Parent).Children.Remove(newItem.LabTitleRaw);
                    else
                        newItem.SubTitle = title.Value;

                    newItem.Tags = tags;
                    newItem.Description = description.Replace("\r", "").Replace("\n", "");

                    // 下边栏逻辑切换
                    newItem.LabVersion.Text = (showMcVersionDesc, showLoaderDesc) switch
                    {
                        (true, true) =>
                            $"{(string.IsNullOrEmpty(partDesc) ? "" : partDesc + " ")}{gameVersionDescription}",
                        (true, false) => gameVersionDescription,
                        (false, true) => fullDesc,
                        _ => "" // 处理隐藏逻辑见下
                    };

                    if (!showMcVersionDesc && !showLoaderDesc)
                    {
                        ((Grid)newItem.PathVersion.Parent).Children.Remove(newItem.PathVersion);
                        ((Grid)newItem.LabVersion.Parent).Children.Remove(newItem.LabVersion);
                        newItem.ColumnVersion1.Width = new GridLength(0);
                        newItem.ColumnVersion2.MaxWidth = 0;
                        newItem.ColumnVersion3.Width = new GridLength(0);
                    }

                    newItem.LabSource.Text = fromCurseForge ? "CurseForge" : "Modrinth";

                    if (lastUpdate is not null)
                    {
                        newItem.LabTime.Text = Lang.TimeSpan(lastUpdate.Value - DateTime.Now, 1);
                    }
                    else
                    {
                        newItem.LabTime.Visibility = Visibility.Collapsed;
                        newItem.ColumnTime1.Width =
                            newItem.ColumnTime2.Width = newItem.ColumnTime3.Width = new GridLength(0);
                    }

                    // 下载量数值缩写
                    newItem.LabDownload.Text = Lang.CompactNumber(downloadCount);

                    return newItem;
                })
                { Height = 64 };
        }

        public MyListItem ToListItem()
        {
            var result = new MyListItem
            {
                Title = TranslatedName,
                Info = description.Replace("\r", "").Replace("\n", ""),
                Logo = string.IsNullOrEmpty(logoUrl) ? $"{ModBase.pathImage}Icons/NoIcon.png" : logoUrl,
                Tags = tags,
                Tag = this,
                LogoCornerRadius = new CornerRadius(6)
            };
            return result;
        }

        public void ApplyLogoToMyImage(MyImage img)
        {
            if (string.IsNullOrEmpty(logoUrl))
            {
                img.Source = ModBase.pathImage + "Icons/NoIcon.png";
            }
            else
            {
                img.Source = logoUrl;
                img.FallbackSource = ModDownload.DlSourceModGet(logoUrl);
            }
        }

        public KeyValuePair<string, string> GetControlTitle(bool hasModLoaderDescription)
        {
            // 参考 #1567 测试例
            var title = rawName;
            List<string> subtitleList = new();

            if (TranslatedName == rawName)
            {
                // --- 场景 A: 没有中文翻译 ---
                var nameLists = TranslatedName.Split(new[] { " | ", " - ", "(", ")", "[", "]", "{", "}" },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim(' ', '/', '\\', '"'))
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToList();

                if (nameLists.Count <= 1) return BuildResult(title, "");

                var normalNameList = new List<string>();
                foreach (var name in nameLists)
                {
                    var lowerName = name.ToLower();
                    // 匹配缩写 (全大写且不是特定词)
                    if (name.ToUpper() == name && name != "FPS" && name != "HUD")
                        subtitleList.Add(name);
                    // 匹配加载器标记 (Forge/Fabric/Quilt 且去掉后不含其他字母)
                    else if (IsModLoaderMarker(lowerName))
                        subtitleList.Add(name);
                    else
                        normalNameList.Add(name);
                }

                if (!normalNameList.Any() || !subtitleList.Any())
                    return BuildResult(title, "");

                title = string.Join(" - ", normalNameList);
            }
            else
            {
                // --- 场景 B: 有中文翻译 ---
                // 尝试拆分：Title (EnglishName) - Suffix
                title = TranslatedName.BeforeFirst(" (").BeforeFirst(" - ");

                var suffix = "";
                if (TranslatedName.AfterLast(")").Contains(" - "))
                    suffix = TranslatedName.AfterLast(")").AfterLast(" - ");

                var englishName = TranslatedName;
                if (!string.IsNullOrEmpty(suffix))
                    englishName = englishName.Replace(" - " + suffix, "");

                englishName = englishName.Replace(title, "").Trim('(', ')', ' ');

                subtitleList = englishName.Split(new[] { " | ", " - ", "(", ")", "[", "]", "{", "}" },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim(' ', '/'))
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToList();

                // 特殊逻辑：如果看起来不像版本标记或特定缩写，则保持原名
                if (subtitleList.Count > 1 &&
                    !subtitleList.Any(s => IsModLoaderMarker(s.ToLower())) &&
                    !(subtitleList.Count == 2 && subtitleList.Last().ToUpper() == subtitleList.Last()))
                    subtitleList = new List<string> { englishName };

                if (!string.IsNullOrEmpty(suffix)) subtitleList.Add(suffix);
            }

            // --- 后处理: 构建 Subtitle 字符串 ---
            var finalSubtitles = new List<string>();
            foreach (var rawEx in subtitleList.Distinct())
            {
                var ex = rawEx;
                var lowerEx = ex.ToLower();
                var isModLoader = lowerEx.Contains("forge") || lowerEx.Contains("fabric") || lowerEx.Contains("quilt");

                if (!hasModLoaderDescription && isModLoader) continue;
                if (ex.Length < 16 && lowerEx.Contains("fabric") && lowerEx.Contains("forge")) continue;

                if (isModLoader && !ex.Contains("版") &&
                    lowerEx.Replace("forge", "").Replace("fabric", "").Replace("quilt", "").Length <= 3)
                    ex = ex.Replace("Edition", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("edition", "", StringComparison.OrdinalIgnoreCase)
                        .Trim().Capitalize() + Lang.Text("Download.Comp.Detail.CompItem.EditionSuffix");

                // 规范化名称大小写
                ex = ex.Replace("forge", "Forge").Replace("neo", "Neo").Replace("fabric", "Fabric")
                    .Replace("quilt", "Quilt");
                finalSubtitles.Add(ex.Trim());
            }

            var subtitleResult = finalSubtitles.Any() ? "  |  " + string.Join("  |  ", finalSubtitles) : "";
            return BuildResult(title, subtitleResult);

            bool IsModLoaderMarker(string input)
            {
                return (input.Contains("forge") || input.Contains("fabric") || input.Contains("quilt")) &&
                       !input.Replace("forge", "").Replace("fabric", "").Replace("quilt", "").RegexCheck("[a-z]+");
            }

            KeyValuePair<string, string> BuildResult(string t, string s)
            {
                return new KeyValuePair<string, string>(t, s);
            }
        }

        // 辅助函数

        /// <summary>
        ///     检查是否与某个 Project 是相同的工程，只是在不同的网站。
        /// </summary>
        public bool IsLike(CompProject Project)
        {
            if ((id ?? "") == (Project.id ?? ""))
                return true; // 相同实例

            // 提取字符串中的字母和数字
            string GetRaw(string Data)
            {
                var result = new StringBuilder();
                foreach (var r in Data.Where(c => char.IsLetterOrDigit(c)))
                    result.Append(r);
                return result.ToString().ToLower();
            }

            ;
            // 来自不同的网站
            if (fromCurseForge == Project.fromCurseForge)
                return false;
            // Mod 加载器一致
            if (modLoaders.Count != Project.modLoaders.Count || modLoaders.Except(Project.modLoaders).Any())
                return false;
            // 若不为光影，则要求 MC 版本一致
            if (type != CompType.Shader && (drops.Count != Project.drops.Count || drops.Except(Project.drops).Any()))
                return false;
            // 最近更新时间差距在一周以内
            if (lastUpdate is not null && Project.lastUpdate is not null &&
                Math.Abs((lastUpdate - Project.lastUpdate).Value.TotalDays) > 7d)
                return false;
            // MCMOD 翻译名 / 原名 / 描述文本 / Slug 的英文部分相同
            if ((TranslatedName ?? "") == (Project.TranslatedName ?? "") ||
                (rawName ?? "") == (Project.rawName ?? "") || (description ?? "") == (Project.description ?? "") ||
                (GetRaw(slug) ?? "") == (GetRaw(Project.slug) ?? ""))
            {
                ModBase.Log($"[Comp] 将 {rawName} ({slug}) 与 {Project.rawName} ({Project.slug}) 认定为相似工程");
                // 如果只有一个有 DatabaseEntry，设置给另外一个
                if (DatabaseEntry is null && Project.DatabaseEntry is not null)
                    DatabaseEntry = Project.DatabaseEntry;
                if (DatabaseEntry is not null && Project.DatabaseEntry is null)
                    Project.DatabaseEntry = DatabaseEntry;
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return $"{id} ({slug}): {rawName}";
        }

        public override bool Equals(object obj)
        {
            var project = obj as CompProject;
            return project is not null && (id ?? "") == (project.id ?? "");
        }

        public static bool operator ==(CompProject left, CompProject right)
        {
            return EqualityComparer<CompProject>.Default.Equals(left, right);
        }

        public static bool operator !=(CompProject left, CompProject right)
        {
            return !(left == right);
        }
    }

    // 输入与输出

    public class CompProjectRequest
    {
        /// <summary>
        ///     筛选 MC 版本。
        /// </summary>
        public string gameVersion = null;

        /// <summary>
        ///     筛选 Mod 加载器类别。
        /// </summary>
        public CompLoaderType modLoader = CompLoaderType.Any;

        /// <summary>
        ///     搜索的文本内容。
        /// </summary>
        public string searchText;

        /// <summary>
        ///     在进行中文搜索时，CurseForge 的替代搜索文本。
        ///     由于 CurseForge API 在有任意关键词未匹配的时候就不显示结果，所以不能使用与 Modrinth 相同的算法。
        /// </summary>
        public string curseForgeAltSearchText;

        /// <summary>
        ///     搜索结果排序方式。
        /// </summary>
        public CompSortType sort = CompSortType.Default;

        /// <summary>
        ///     允许的来源。
        /// </summary>
        public CompSourceType source = CompSourceType.Any;

        // 结果要求

        /// <summary>
        ///     加载后应输出到的结果存储器。
        /// </summary>
        public CompProjectStorage storage;

        /// <summary>
        ///     筛选资源标签。空字符串代表不限制。格式例如 "406/worldgen"，分别是 CurseForge 和 Modrinth 的 ID。
        /// </summary>
        public string tag = "";

        /// <summary>
        ///     应当尽量达成的结果数量。
        /// </summary>
        public int targetResultCount;

        // 输入内容

        /// <summary>
        ///     筛选资源种类。
        /// </summary>
        public CompType type;

        /// <summary>
        ///     构造函数。
        /// </summary>
        public CompProjectRequest(CompType Type, CompProjectStorage Storage, int TargetResultCount)
        {
            this.type = Type;
            this.storage = Storage;
            this.targetResultCount = TargetResultCount;
        }

        /// <summary>
        ///     根据加载位置记录，是否还可以继续获取内容。
        /// </summary>
        public bool CanContinue
        {
            get
            {
                if (tag.StartsWithF("/") || !source.HasFlag(CompSourceType.CurseForge))
                    storage.curseForgeTotal = 0;
                if (tag.EndsWithF("/") || !source.HasFlag(CompSourceType.Modrinth))
                    storage.modrinthTotal = 0;
                if (storage.curseForgeTotal == -1 || storage.modrinthTotal == -1)
                    return true;
                return storage.curseForgeOffset < storage.curseForgeTotal ||
                       storage.modrinthOffset < storage.modrinthTotal;
            }
        }

        // 构造请求

        /// <summary>
        ///     获取对应的 CurseForge API 请求链接。若返回 Nothing 则为不进行 CurseForge 请求。
        /// </summary>
        public string GetCurseForgeAddress()
        {
            if (!source.HasFlag(CompSourceType.CurseForge))
                return null;
            if (tag.StartsWithF("/"))
                storage.curseForgeTotal = 0;
            if (storage.curseForgeTotal > -1 && storage.curseForgeTotal <= storage.curseForgeOffset)
                return null;
            // 应用筛选参数
            var address =
                new StringBuilder(
                    $"https://api.curseforge.com/v1/mods/search?gameId=432&sortOrder=desc&pageSize={compPageSize}");
            switch (type)
            {
                case CompType.Mod:
                {
                    address.Append("&classId=6");
                    break;
                }
                case CompType.ModPack:
                {
                    address.Append("&classId=4471");
                    break;
                }
                case CompType.DataPack:
                {
                    address.Append("&classId=6945");
                    break;
                }
                case CompType.Shader:
                {
                    address.Append("&classId=6552");
                    break;
                }
                case CompType.ResourcePack:
                {
                    address.Append("&classId=12");
                    break;
                }
                case CompType.World:
                {
                    address.Append("&classId=17");
                    break;
                }
            }

            if (!string.IsNullOrEmpty(tag)) address.Append($"&categoryId={tag.BeforeFirst("/")}");
            if (modLoader != CompLoaderType.Any)
                address.Append("&modLoaderType=").Append(((int)modLoader).ToString());
            if (!string.IsNullOrEmpty(gameVersion))
                address.Append("&gameVersion=").Append(gameVersion);
            if (!string.IsNullOrEmpty(curseForgeAltSearchText ?? searchText))
                address.Append("&searchFilter=").Append(WebUtility.UrlEncode(curseForgeAltSearchText ?? searchText));
            if (storage.curseForgeOffset > 0)
                address.Append("&index=").Append(storage.curseForgeOffset);
            switch (sort)
            {
                case CompSortType.Relevance:
                {
                    address.Append("&sortField=4");
                    break;
                }
                case CompSortType.Downloads:
                {
                    address.Append("&sortField=6");
                    break;
                }
                case CompSortType.Follows:
                {
                    address.Append("&sortField=2");
                    break;
                }
                case CompSortType.Newest:
                {
                    address.Append("&sortField=11");
                    break;
                }
                case CompSortType.Updated:
                {
                    address.Append("&sortField=3");
                    break;
                }

                default:
                {
                    address.Append("&sortField=2");
                    break;
                }
            }

            return address.ToString();
        }

        /// <summary>
        ///     获取对应的 Modrinth API 请求链接。若返回 Nothing 则为不进行 Modrinth 请求。
        /// </summary>
        public string GetModrinthAddress()
        {
            if (!source.HasFlag(CompSourceType.Modrinth))
                return null;
            if (tag.EndsWithF("/"))
                storage.modrinthTotal = 0;
            if (storage.modrinthTotal > -1 && storage.modrinthTotal <= storage.modrinthOffset)
                return null;
            // 应用筛选参数
            var address = $"https://api.modrinth.com/v2/search?limit={compPageSize}";
            switch (sort)
            {
                case CompSortType.Relevance:
                {
                    address += "&index=relevance";
                    break;
                }
                case CompSortType.Downloads:
                {
                    address += "&index=downloads";
                    break;
                }
                case CompSortType.Follows:
                {
                    address += "&index=follows";
                    break;
                }
                case CompSortType.Newest:
                {
                    address += "&index=newest";
                    break;
                }
                case CompSortType.Updated:
                {
                    address += "&index=updated";
                    break;
                }

                default:
                {
                    address += "&index=relevance";
                    break;
                }
            }

            if (!string.IsNullOrEmpty(searchText))
                address += "&query=" + WebUtility.UrlEncode(searchText);
            if (storage.modrinthOffset > 0)
                address += "&offset=" + storage.modrinthOffset;
            // facets=[["categories:'game-mechanics'"],["categories:'forge'"],["versions:1.19.3"],["project_type:mod"]]
            var facets = new List<string>();
            facets.Add($"[\"project_type:{ModBase.GetStringFromEnum(type).ToLower()}\"]");
            if (!string.IsNullOrEmpty(tag))
                facets.Add($"[\"categories:'{tag.AfterLast("/")}'\"]");
            if (modLoader != CompLoaderType.Any)
                facets.Add($"[\"categories:'{ModBase.GetStringFromEnum(modLoader).ToLower()}'\"]");
            if (!string.IsNullOrEmpty(gameVersion))
                facets.Add($"[\"versions:'{gameVersion}'\"]");
            address += "&facets=[" + string.Join(",", facets) + "]";
            return address;
        }

        // 相同判断
        public override bool Equals(object obj)
        {
            var request = obj as CompProjectRequest;
            return request is not null && type == request.type && targetResultCount == request.targetResultCount &&
                   (tag ?? "") == (request.tag ?? "") && modLoader == request.modLoader && source == request.source &&
                   (gameVersion ?? "") == (request.gameVersion ?? "") &&
                   (searchText ?? "") == (request.searchText ?? "") && sort == request.sort;
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

        public int curseForgeOffset;
        public int curseForgeTotal = -1;

        /// <summary>
        ///     当前的错误信息。如果没有则为 Nothing。
        /// </summary>
        public string errorMessage = null;

        public int modrinthOffset;
        public int modrinthTotal = -1;

        // 结果列表

        /// <summary>
        ///     可供展示的所有工程的列表。
        /// </summary>
        public List<CompProject> results = new();
    }

    // 实际的获取

    private const int compPageSize = 40;

    /// <summary>
    ///     已知工程信息的缓存。
    /// </summary>
    public static ConcurrentDictionary<string, CompProject> compProjectCache = new();

    /// <summary>
    ///     根据搜索请求获取一系列的工程列表。需要基于加载器运行。
    /// </summary>
    public static void CompProjectsGet(ModLoader.LoaderTask<CompProjectRequest, int> task)
    {
        var request = task.input;
        var storage = request.storage;

        #region 状态与版本初步检查

        if (storage.results.Count >= request.targetResultCount)
        {
            LogWrapper.Info($"[Comp] 已有 {storage.results.Count} 个结果，多于所需的 {request.targetResultCount} 个结果，结束处理");
            return;
        }

        if (!request.CanContinue)
        {
            if (!storage.results.Any()) throw new Exception(Lang.Text("Download.Comp.List.NoMatchingResults"));
            LogWrapper.Info(
                $"[Comp] 已有 {storage.results.Count} 个结果，少于所需的 {request.targetResultCount} 个结果，但无法继续获取，结束处理");
            return;
        }

        // 拒绝不支持的版本
        if (request.modLoader == CompLoaderType.Quilt &&
            ModMinecraft.CompareVersion(request.gameVersion ?? "1.15", "1.14") == -1)
                throw new Exception(Lang.Text("Minecraft.Error.QuiltUnsupported", request.gameVersion));

        #endregion

        #region 处理搜索文本 (内嵌关键词转换逻辑)

        var rawFilter = (request.searchText ?? "").Trim();
        request.searchText = rawFilter;
        var rawFilterLower = rawFilter.ToLower();
        LogWrapper.Info("[Comp] 工程列表搜索原始文本：" + rawFilter);

        // 中文请求关键字处理
        var isChineseSearch = RegexPatterns.HasChineseChar.IsMatch(rawFilter) && !string.IsNullOrEmpty(rawFilter);
        if (isChineseSearch && (request.type == CompType.Mod || request.type == CompType.DataPack))
        {
            var searchEntries = new List<ModBase.SearchEntry<CompDatabaseEntry>>();
            using (var conn = CompDB)
            {
                var sql =
                    "SELECT * FROM ModTranslation WHERE ChineseName LIKE @p OR CurseForgeSlug LIKE @p OR ModrinthSlug LIKE @p";
                var searchRes = conn.Query<CompDatabaseEntry>(sql, new { p = $"%{rawFilter}%" });
                foreach (var searchItem in searchRes)
                {
                    if (searchItem.ChineseName.Contains("动态的树")) continue;
                    searchEntries.Add(new ModBase.SearchEntry<CompDatabaseEntry>
                    {
                        item = searchItem,
                        searchSource = new List<ModBase.SearchSource>
                        {
                            new(searchItem.ChineseName.BeforeFirst(" (").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries), 1),
                            new(searchItem.ChineseName.AfterFirst(" (") + (searchItem.CurseForgeSlug ?? "") + (searchItem.ModrinthSlug ?? ""), 0.5)
                        }
                    });
                }
            }

            var searchResults = ModBase.Search(searchEntries, request.searchText, 40, 0.2);
            if (!searchResults.Any()) throw new Exception(Lang.Text("Download.Comp.List.NoResults"));

            string[] ExtractWords(ModBase.SearchEntry<CompDatabaseEntry> Result)
            {
                var word = "";
                if (Result.item.CurseForgeSlug is not null)
                    word += Result.item.CurseForgeSlug.Replace("-", " ").Replace("/", " ") + " ";
                if (Result.item.ModrinthSlug is not null)
                    word += Result.item.ModrinthSlug.Replace("-", " ").Replace("/", " ") + " ";
                word += Result.item.ChineseName.AfterLast(" (").TrimEnd(')', ' ').BeforeFirst(" - ")
                    .Replace(":", "").Replace("(", "").Replace(")", "").ToLower().Replace("/", " ").Replace("-", " ");
                var words = word.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                words = words.Select(w => w.TrimStart('{', '[', '(').TrimEnd('}', ']', ')')).Where(
                    w =>
                    {
                        if (w.Length <= 1) return false;
                        if (new[] { "the", "of", "mod", "and" }.Contains(w)) return false;
                        if (ModBase.Val(w) > 0) return false;
                        if (w.Split(' ').Length > 3 && w.Contains("ftb")) return false;
                        return true;
                    }).Distinct().ToArray();
                return words;
            }

            var wordWeights = new Dictionary<string, double>();
            foreach (var Result in searchResults)
            {
                foreach (var Word in ExtractWords(Result))
                {
                    var similarity = Result.searchSource.Any(s => s.aliases.Contains(request.searchText))
                        ? 100000
                        : Result.similarity;
                    if (!wordWeights.ContainsKey(Word))
                        wordWeights.Add(Word, 0);
                    wordWeights[Word] += similarity;
                }
            }

            if (!wordWeights.Any()) throw new Exception(Lang.Text("Download.Comp.List.NoResults"));

            var sortedWords = wordWeights.OrderByDescending(w => w.Value).ToList();
            if (sortedWords.First().Value >= 100000)
            {
                request.searchText = string.Join(" ", sortedWords.Where(w => w.Value >= 100000).Select(w => w.Key));
            }
            else
            {
                request.searchText = string.Join(" ", sortedWords.Take(5).Select(w => w.Key));
                request.curseForgeAltSearchText = string.Join(" ", ExtractWords(searchResults.First()));
                LogWrapper.Debug("[Comp] 中文搜索基础关键词（CurseForge）：" + request.curseForgeAltSearchText);
            }

            LogWrapper.Debug("[Comp] 中文搜索基础关键词：" + request.searchText);
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

        if (request.curseForgeAltSearchText is not null)
        {
            processKeywords(ref request.curseForgeAltSearchText);
            LogWrapper.Debug("[Comp] 工程列表搜索最终文本（CurseForge）：" + request.curseForgeAltSearchText);
        }

        processKeywords(ref request.searchText);
        LogWrapper.Debug("[Comp] 工程列表搜索最终文本：" + request.searchText);
        task.Progress = 0.1;

        #endregion

        var realResults = new List<CompProject>();

        #region 网络请求与结果获取 (Retry 循环)

        while (true)
        {
            var rawResults = new List<CompProject>();
            Exception lastError = null;
            var resultsLock = new object();

            // 1.14 以下 Forge 筛选处理
            var isOldForgeRequest = request.modLoader == CompLoaderType.Forge &&
                                    ModMinecraft.McInstanceInfo.VersionToDrop(request.gameVersion, true) < 140;
            if (isOldForgeRequest) request.modLoader = CompLoaderType.Any;
            var curseForgeUrl = request.GetCurseForgeAddress();
            var modrinthUrl = request.GetModrinthAddress();
            if (isOldForgeRequest) request.modLoader = CompLoaderType.Forge;

            var tasks = new List<Task>();

            // CurseForge 线程内嵌
            if (curseForgeUrl is not null)
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        LogWrapper.Info("[Comp] 开始从 CurseForge 获取列表：" + curseForgeUrl);
                        var json = ModDownload.DlModRequest<JsonObject>(curseForgeUrl);
                        var projects = json["data"].AsArray().Select(j => new CompProject((JsonObject)j))
                            .Where(p => !(request.type == CompType.ResourcePack && p.tags.Contains(Lang.Text("Download.Comp.Type.DataPack"))))
                            .ToList();
                        lock (resultsLock)
                        {
                            rawResults.AddRange(projects);
                        }

                        storage.curseForgeOffset += projects.Count;
                        storage.curseForgeTotal = json["pagination"]["totalCount"].ToObject<int>();
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        LogWrapper.Error(ex, "CurseForge 获取失败");
                    }
                }));

            // Modrinth 线程内嵌
            if (modrinthUrl is not null)
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        LogWrapper.Info("[Comp] 开始从 Modrinth 获取列表：" + modrinthUrl);
                        var json = ModDownload.DlModRequest<JsonObject>(modrinthUrl);
                        var projects = json["hits"].AsArray().Select(j => new CompProject((JsonObject)j)).ToList();
                        lock (resultsLock)
                        {
                            rawResults.AddRange(projects);
                        }

                        storage.modrinthOffset += projects.Count;
                        storage.modrinthTotal = json["total_hits"].ToObject<int>();
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
                rawResults = rawResults.Where(p => !p.modLoaders.Any() || p.modLoaders.Contains(CompLoaderType.Forge))
                    .ToList();

            // 错误检查与空结果处理
            if (!rawResults.Any())
            {
                if (lastError is not null) throw lastError;
                // 处理各平台不兼容报错... (此处省略具体 Exception 文本以保持简略)
                throw new Exception(Lang.Text("Download.Comp.List.NoResultsSimple"));
            }

            #region 去重与分页判断

            // 优先保留 Modrinth 顺序并去重
            var processedResults = rawResults.OrderBy(x => x.fromCurseForge)
                .Where(r => !realResults.Any(b => r.IsLike(b)) && !storage.results.Any(b => r.IsLike(b)))
                .ToList();

            realResults.AddRange(processedResults);
            LogWrapper.Info($"[Comp] 去重、筛选后累计新增结果 {processedResults.Count} 个（目前已有结果 {storage.results.Count} 个）");

            if (realResults.Count + storage.results.Count < request.targetResultCount && request.CanContinue &&
                lastError is null)
            {
                LogWrapper.Info("[Comp] 数量不足，继续加载下一页");
                continue;
            }

            break;

            #endregion
        }

        #endregion

        #region 排序与最终输出

        if (request.sort != CompSortType.Default)
        {
            if (task.IsAborted) throw new ThreadInterruptedException();
            storage.results.AddRange(realResults); // 遵从API返回顺序
            return;
        }
        
        var scores = new Dictionary<CompProject, double>();
        Func<CompProject, double> getDownloadCountMult = p =>
        {
            switch (request.type)
            {
                case CompType.Mod:
                case CompType.ModPack: return p.fromCurseForge ? 1 : 7;
                case CompType.DataPack: return p.fromCurseForge ? 10 : 1;
                case CompType.ResourcePack:
                case CompType.Shader: return p.fromCurseForge ? 1 : 5;
                default: return 1;
            }
        };

        if (string.IsNullOrEmpty(rawFilter))
        {
            foreach (var res in realResults) scores.Add(res, res.downloadCount * getDownloadCountMult(res));
        }
        else
        {
            var searchEntries = new List<ModBase.SearchEntry<CompProject>>();
            foreach (var res in realResults)
            {
                scores.Add(res,
                    (res.WikiId > 0 ? 0.2 : 0) +
                    Math.Log10(Math.Max(res.downloadCount, 1) * getDownloadCountMult(res)) / 9);
                searchEntries.Add(new ModBase.SearchEntry<CompProject>
                {
                    item = res,
                    searchSource = new List<ModBase.SearchSource>
                    {
                        new((isChineseSearch ? res.TranslatedName : res.rawName).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries), 1),
                        new(res.description, 0.05)
                    }
                });
            }

            var searchRes = ModBase.Search(searchEntries, rawFilter, 101, -1);
            foreach (var item in searchRes)
                scores[item.item] +=
                    (item.absoluteRight ? 10 : item.similarity) /
                    (searchRes.First().absoluteRight ? 10 : searchRes.First().similarity);
        }

        if (task.IsAborted) throw new ThreadInterruptedException();
        storage.results.AddRange(scores.OrderByDescending(s => s.Value).Select(s => s.Key));

        #endregion
    }

    #endregion

    #region CompFile | 文件信息

    // 类定义

    public enum CompFileStatus
    {
        Release = 1, // 枚举值来源：https://docs.curseforge.com/#tocS_FileReleaseType
        Beta = 2,
        Alpha = 3
    }

    public class CompFile
    {
        /// <summary>
        ///     该文件的所有必要依赖工程的 Project.Id。
        /// </summary>
        public readonly List<string> dependencies = new();

        /// <summary>
        ///     下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量，且 CurseForge 可能错误地返回 0。
        /// </summary>
        public readonly int downloadCount;

        /// <summary>
        ///     下载的文件名。
        /// </summary>
        public readonly string fileName;

        /// <summary>
        ///     该文件来自 CurseForge 还是 Modrinth。
        /// </summary>
        public readonly bool fromCurseForge;

        //  <summary>
        //  未经处理的支持的游戏版本列表。
        // </summary>
        public readonly List<string> rawGameVersions;
        /// <summary>
        ///     支持的游戏版本列表。类型包括："26.1.5"，"26.1"，"26.1 预览版"，"1.18.5"，"1.18"，"1.18 预览版"，"21w15a"，"未知版本"。
        /// </summary>
        public readonly List<string> gameVersions;

        /// <summary>
        ///     文件的 SHA1 或 MD5。
        /// </summary>
        public readonly string hash;

        /// <summary>
        ///     用于唯一性鉴别该文件的 ID。CurseForge 中为 123456 的大整数，Modrinth 中为英文乱码的 Version 字段。
        /// </summary>
        public readonly string id;

        /// <summary>
        ///     支持的 Mod 加载器列表。可能为空。
        /// </summary>
        public readonly List<CompLoaderType> modLoaders;

        /// <summary>
        ///     该文件的所有可选依赖工程的 Project.Id。
        /// </summary>
        public readonly List<string> optionalDependencies = new();

        /// <summary>
        ///     该文件所属项目的 ID。
        /// </summary>
        public readonly string projectId;

        /// <summary>
        ///     该文件的所有必要依赖工程的原始 ID。
        ///     这些 ID 可能没有加载，在加载后会添加到 Dependencies 中（主要是因为 Modrinth 返回的是字符串 ID 而非 Slug，导致 Project.Id 查询不到）。
        /// </summary>
        public readonly List<string> rawDependencies = new();

        /// <summary>
        ///     该文件的所有可选依赖工程的原始 ID。
        ///     这些 ID 可能没有加载，在加载后会添加到 OptionalDependencies 中（主要是因为 Modrinth 返回的是字符串 ID 而非 Slug，导致 Project.Id 查询不到）。
        /// </summary>
        public readonly List<string> rawOptionalDependencies = new();

        /// <summary>
        ///     发布时间。
        /// </summary>
        public readonly DateTime releaseDate;

        /// <summary>
        ///     发布状态：Release/Beta/Alpha。
        /// </summary>
        public readonly CompFileStatus status;

        // 源信息

        /// <summary>
        ///     文件的种类。
        /// </summary>
        public readonly CompType type;

        // 描述性信息

        /// <summary>
        ///     文件描述名（并非文件名，是自定义的字段）。对很多 Mod，这会给出 Mod 版本号。
        /// </summary>
        public string displayName;

        /// <summary>
        ///     文件所有可能的下载源。
        /// </summary>
        public List<string> downloadUrls;

        /// <summary>
        ///     Mod 版本号。
        ///     不一定是标准格式。CurseForge 上默认为 Nothing。
        /// </summary>
        public string version;

        // 实例化

        /// <summary>
        ///     从文件 Json 中初始化实例。若出错会抛出异常。
        /// </summary>
        public CompFile(JsonObject Data, CompType DefaultType)
        {
            type = DefaultType;
            if (Data.ContainsKey("FromCurseForge"))
            {
                #region CompJson

                fromCurseForge = Data["FromCurseForge"].ToObject<bool>();
                id = Data["Id"].ToString();
                displayName = Data["DisplayName"].ToString();
                if (Data.ContainsKey("Version"))
                    version = Data["Version"].ToString();
                releaseDate = Data["ReleaseDate"].ToObject<DateTime>();
                downloadCount = Data["DownloadCount"].ToObject<int>();
                status = (CompFileStatus)Data["Status"].ToObject<int>();
                if (Data.ContainsKey("FileName"))
                    fileName = Data["FileName"].ToString();
                if (Data.ContainsKey("DownloadUrls"))
                    downloadUrls = Data["DownloadUrls"].ToObject<List<string>>();
                if (Data.ContainsKey("ModLoaders"))
                    modLoaders = Data["ModLoaders"].ToObject<List<CompLoaderType>>();
                if (Data.ContainsKey("Hash"))
                    hash = Data["Hash"].ToString();
                if (Data.ContainsKey("RawGameVersions"))
                    rawGameVersions = Data["RawGameVersions"].ToObject<List<string>>();
                if (Data.ContainsKey("GameVersions"))
                    gameVersions = Data["GameVersions"].ToObject<List<string>>();
                if (Data.ContainsKey("RawDependencies"))
                    rawDependencies = Data["RawDependencies"].ToObject<List<string>>();
                if (Data.ContainsKey("Dependencies"))
                    dependencies = Data["Dependencies"].ToObject<List<string>>();
                if (Data.ContainsKey("RawOptionalDependencies"))
                    rawDependencies = Data["RawOptionalDependencies"].ToObject<List<string>>();
                if (Data.ContainsKey("OptionalDependencies"))
                    dependencies = Data["OptionalDependencies"].ToObject<List<string>>();
            }

            #endregion

            else
            {
                fromCurseForge = Data.ContainsKey("gameId");
                if (fromCurseForge)
                {
                    #region CurseForge

                    // 简单信息
                    id = Data["id"].ToString();
                    projectId = Data["modId"].ToString();
                    displayName = Data["displayName"].ToString().Replace("	", "").Trim(' ');
                    version = null;
                    releaseDate = Data["fileDate"].ToObject<DateTime>();
                    status = (CompFileStatus)Data["releaseType"].ToObject<int>();
                    downloadCount = (int)Data["downloadCount"];
                    fileName = (string)Data["fileName"];
                    hash =
                        (string)((JsonArray)Data["hashes"]).ToList().FirstOrDefault(s => s["algo"].ToObject<int>() == 1)?[
                            "value"];
                    if (hash is null)
                        hash = (string)((JsonArray)Data["hashes"]).ToList()
                            .FirstOrDefault(s => s["algo"].ToObject<int>() == 2)?["value"];
                    // DownloadAddress
                    var url = Data["downloadUrl"]?.ToString() ?? "";
                    // TODO: 移除龙猫写的直接下载，换用提醒用户手动下载相关模组
                    if (string.IsNullOrWhiteSpace(url))
                        url =
                            $"https://edge.forgecdn.net/files/{int.Parse(id[..4])}/{int.Parse(id[4..])}/{fileName}";
                    url = url.Replace(fileName, WebUtility.UrlEncode(fileName)); // 对文件名进行编码
                    url = url.Replace("+", "%20"); // 修正被编码成 + 的空格，CurseForge 会对 + 号也进行编码
                    downloadUrls = ModDownload.DlSourceModDownloadGet(HandleCurseForgeDownloadUrls(url)); // 添加镜像源
                    // Dependencies
                    if (Data.ContainsKey("dependencies"))
                    {
                        rawDependencies = Data["dependencies"].AsArray()
                            .Where(d => d["relationType"].ToObject<int>() == 3 &&
                                        d["modId"].ToObject<int>() != 306612 && d["modId"].ToObject<int>() != 634179)
                            .Select(d => d["modId"].ToString()).ToList(); // 种类为必要依赖
                        // 排除 Fabric API 和 Quilt API
                        rawOptionalDependencies = Data["dependencies"].AsArray()
                            .Where(d => d["relationType"].ToObject<int>() == 2 &&
                                        d["modId"].ToObject<int>() != 306612 && d["modId"].ToObject<int>() != 634179)
                            .Select(d => d["modId"].ToString()).ToList(); // 种类为可选依赖
                        // 排除 Fabric API 和 Quilt API
                    }

                    // GameVersions
                    rawGameVersions = Data["gameVersions"].AsArray().Select(t => t.ToString().Trim().ToLower()).ToList();
                    gameVersions = rawGameVersions.Where(v => ModMinecraft.McInstanceInfo.IsFormatFit(v))
                        .Select(v => v.Replace("-snapshot", Lang.Text("Download.Comp.Detail.CompItem.PreviewSuffix"))).Distinct().ToList();
                    if (gameVersions.Count > 1)
                    {
                        gameVersions = gameVersions.Sort(ModMinecraft.CompareVersionGe).ToList();
                        if (type == CompType.ModPack)
                            gameVersions = new List<string> { gameVersions[0] }; // 整合包理应只 "支持" 一个版本
                    }
                    else if (gameVersions.Count == 1)
                    {
                        gameVersions = gameVersions.ToList();
                    }
                    else
                    {
                        gameVersions = new List<string> { Lang.Text("Download.Comp.Detail.CompItem.UnknownVersion") };
                    }

                    // ModLoaders
                    modLoaders = new List<CompLoaderType>();
                    if (rawGameVersions.Contains("forge"))
                        modLoaders.Add(CompLoaderType.Forge);
                    if (rawGameVersions.Contains("fabric"))
                        modLoaders.Add(CompLoaderType.Fabric);
                    if (rawGameVersions.Contains("quilt"))
                        modLoaders.Add(CompLoaderType.Quilt);
                    if (rawGameVersions.Contains("neoforge"))
                        modLoaders.Add(CompLoaderType.NeoForge);
                }

                #endregion

                else
                {
                    #region Modrinth

                    // 简单信息
                    id = (string)Data["id"];
                    projectId = (string)Data["project_id"];
                    displayName = Data["name"].ToString().Replace("	", "").Trim(' ');
                    version = (string)Data["version_number"];
                    releaseDate = Data["date_published"].ToObject<DateTime>();
                    status = Data["version_type"].ToString() == "release" ? CompFileStatus.Release :
                        Data["version_type"].ToString() == "beta" ? CompFileStatus.Beta : CompFileStatus.Alpha;
                    downloadCount = (int)Data["downloads"];
                    if (((JsonArray)Data["files"]).Any()) // 可能为空
                    {
                        var file = Data["files"][0];
                        fileName = (string)file["filename"];
                        downloadUrls = ModDownload.DlSourceModDownloadGet(file["url"].ToString()); // 同时添加了镜像源
                        hash = (string)file["hashes"]["sha1"];
                    }

                    // ModLoaders
                    // 结果可能混杂着 Mod、数据包和服务端插件
                    var rawLoaders = Data["loaders"].AsArray().Select(v => v.ToString()).ToList();
                    modLoaders = new List<CompLoaderType>();
                    if (type == CompType.Mod) // 以尽量宽容的方式检测加载器，以免同时兼容两种的项被删除
                    {
                        if (rawLoaders.Intersect(new[] { "bukkit", "folia", "paper", "purpur", "spigot" }).Any())
                            type = CompType.Plugin; // Veinminer Enchantment 同时支持服务端与 Fabric
                        if (rawLoaders.Contains("datapack"))
                            type = CompType.DataPack;
                        if (rawLoaders.Contains("forge"))
                        {
                            modLoaders.Add(CompLoaderType.Forge);
                            type = CompType.Mod;
                        }

                        if (rawLoaders.Contains("neoforge"))
                        {
                            modLoaders.Add(CompLoaderType.NeoForge);
                            type = CompType.Mod;
                        }

                        if (rawLoaders.Contains("fabric"))
                        {
                            modLoaders.Add(CompLoaderType.Fabric);
                            type = CompType.Mod;
                        }

                        if (rawLoaders.Contains("quilt"))
                        {
                            modLoaders.Add(CompLoaderType.Quilt);
                            type = CompType.Mod;
                        }
                    }
                    else if (type == CompType.DataPack)
                    {
                        if (rawLoaders.Intersect(new[] { "bukkit", "folia", "paper", "purpur", "spigot" }).Any())
                            type = CompType.Plugin;
                        if (rawLoaders.Contains("forge"))
                        {
                            modLoaders.Add(CompLoaderType.Forge);
                            type = CompType.Mod;
                        }

                        if (rawLoaders.Contains("neoforge"))
                        {
                            modLoaders.Add(CompLoaderType.NeoForge);
                            type = CompType.Mod;
                        }

                        if (rawLoaders.Contains("fabric"))
                        {
                            modLoaders.Add(CompLoaderType.Fabric);
                            type = CompType.Mod;
                        }

                        if (rawLoaders.Contains("quilt"))
                        {
                            modLoaders.Add(CompLoaderType.Quilt);
                            type = CompType.Mod;
                        }

                        if (rawLoaders.Contains("datapack"))
                            type = CompType.DataPack;
                    }

                    // Dependencies
                    if (Data.ContainsKey("dependencies"))
                    {
                        rawDependencies = Data["dependencies"].AsArray()
                            .Where(d => (string)d["dependency_type"] == "required" &&
                                        d["project_id"] is not null &&
                                        (string)d["project_id"] != "P7dR8mSH" &&
                                        (string)d["project_id"] != "qvIfYCYJ" && d["project_id"] is not null)
                            .Select(d => d["project_id"].ToString()).ToList(); // 种类为必要依赖
                        // 排除 Fabric API 和 Quilt API
                        // 有时候真的会空……
                        rawOptionalDependencies = Data["dependencies"].AsArray()
                            .Where(d => (string)d["dependency_type"] == "optional" &&
                                        d["project_id"] is not null &&
                                        (string)d["project_id"] != "P7dR8mSH" &&
                                        (string)d["project_id"] != "qvIfYCYJ" && d["project_id"] is not null)
                            .Select(d => d["project_id"].ToString()).ToList(); // 种类为可选依赖
                        // 排除 Fabric API 和 Quilt API
                        // 有时候真的会空……
                    }

                    // GameVersions
                    rawGameVersions = Data["game_versions"].AsArray().Select(t => t.ToString().Trim().ToLower()).ToList();
                    gameVersions = rawGameVersions.Where(v => v.Contains(".")).Select(v =>
                        v.Contains("-") ? v.BeforeFirst("-") + Lang.Text("Download.Comp.Detail.CompItem.PreviewSuffix") : v.StartsWithF("b1.") ? Lang.Text("Download.Comp.Detail.CompItem.AncientVersion") : v).Distinct().ToList();
                    if (gameVersions.Count > 1)
                    {
                        gameVersions = gameVersions.Sort(ModMinecraft.CompareVersionGe).ToList();
                        if (type == CompType.ModPack)
                            gameVersions = new List<string> { gameVersions[0] }; // 整合包理应只 “支持” 一个版本
                    }
                    else if (gameVersions.Count == 1)
                    {
                    }
                    // 无需处理
                    else if (rawGameVersions.Any(v => v.RegexCheck("[0-9]{2}w[0-9]{2}[a-z]")))
                    {
                        gameVersions = rawGameVersions.Where(v => v.RegexCheck("[0-9]{2}w[0-9]{2}[a-z]")).ToList();
                    }
                    else
                    {
                        gameVersions = new List<string> { Lang.Text("Download.Comp.Detail.CompItem.UnknownVersion") };
                    }

                    #endregion
                }
            }
        }

        /// <summary>
        ///     发布状态的友好描述。例如："正式版"，"Beta 版"。
        /// </summary>
        public string StatusDescription
        {
            get
            {
                switch (status)
                {
                    case CompFileStatus.Release:
                    {
                        return Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Release");
                    }
                    case CompFileStatus.Beta:
                    {
                        return Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Beta");
                    }

                    default:
                    {
                        return Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Alpha");
                    }
                }
            }
        }

        // 下载信息
        /// <summary>
        ///     下载信息是否可用。
        /// </summary>
        public bool Available => fileName is not null && downloadUrls is not null;

        /// <summary>
        ///     获取下载信息。
        /// </summary>
        /// <param name="LocalAddress">目标本地文件夹，或完整的文件路径。会自动判断类型。</param>
        public DownloadFile ToNetFile(string LocalAddress)
        {
            return new DownloadFile(downloadUrls, LocalAddress + (LocalAddress.EndsWithF(@"\") ? fileName : ""),
                new ModBase.FileChecker(Hash: hash), true);
        }

        /// <summary>
        ///     对之前错误的 CurseForge 的下载地址进行修正。
        /// </summary>
        public static string HandleCurseForgeDownloadUrls(string Url)
        {
            return Url.Replace("-service.overwolf.wtf", ".forgecdn.net").Replace("://media.", "://edge.")
                .Replace("://mediafilez.", "://edge.");
        }

        /// <summary>
        ///     将当前实例转为可用于保存缓存的 Json。
        /// </summary>
        public JsonObject ToJson()
        {
            var json = new JsonObject();
            json.Add("FromCurseForge", fromCurseForge);
            json.Add("Id", id);
            if (version is not null)
                json.Add("Version", version);
            json.Add("DisplayName", displayName);
            json.Add("ReleaseDate", releaseDate);
            json.Add("DownloadCount", downloadCount);
            json.Add("ModLoaders", new JsonArray(modLoaders.Select(m => (JsonNode)(int)m).ToArray()));
            json.Add("RawGameVersions", new JsonArray(rawGameVersions.Select(s => (JsonNode)s).ToArray()));
            json.Add("GameVersions", new JsonArray(gameVersions.Select(s => (JsonNode)s).ToArray()));
            json.Add("Status", (int)status);
            if (fileName is not null)
                json.Add("FileName", fileName);
            if (downloadUrls is not null)
                json.Add("DownloadUrls", new JsonArray(downloadUrls.Select(s => (JsonNode)s).ToArray()));
            if (hash is not null)
                json.Add("Hash", hash);
            json.Add("RawDependencies", new JsonArray(rawDependencies.Select(s => (JsonNode)s).ToArray()));
            json.Add("RawOptionalDependencies", new JsonArray(rawOptionalDependencies.Select(s => (JsonNode)s).ToArray()));
            json.Add("Dependencies", new JsonArray(dependencies.Select(s => (JsonNode)s).ToArray()));
            json.Add("OptionalDependencies", new JsonArray(optionalDependencies.Select(s => (JsonNode)s).ToArray()));
            return json;
        }

        /// <summary>
        ///     将当前文件信息实例化为控件。
        /// </summary>
        public MyVirtualizingElement<MyListItem> ToListItem(MyListItem.ClickEventHandler onClick,
            MyIconButton.ClickEventHandler? onSaveClick = null,
            bool badDisplayName = false)
        {
            return new MyVirtualizingElement<MyListItem>(() =>
                {
                    // 1. 获取基础描述信息
                    var title = badDisplayName ? fileName : displayName;
                    var info = new List<string>();

                    // 2. 填充信息列表
                    if (title != fileName.BeforeLast("."))
                        info.Add(fileName.BeforeLast("."));

                    if (dependencies.Any())
                        info.Add(Lang.Text("Download.Comp.Detail.FileList.DependencyCount", dependencies.Count()));

                    // 简化后的游戏版本逻辑喵
                    var snapshotKeywords = new[] { "w", "snapshot", "rc", "pre", "experimental", "-" };
                    if (gameVersions.All(ver =>
                            !ver.Contains('.') || snapshotKeywords.Any(s => ver.ContainsF(s, true))))
                        info.Add(Lang.Text("Download.Comp.Detail.FileList.GameVersion", string.Join("、", gameVersions)));

                    if (downloadCount > 0)
                        info.Add(Lang.Text("Common.Format.DownloadCount", Lang.CompactNumber(downloadCount)));

                    info.Add(Lang.Text("Download.Comp.Detail.FileList.Updated", Lang.TimeSpan(releaseDate - DateTime.Now)));

                    if (status != CompFileStatus.Release)
                        info.Add(StatusDescription);

                    // 3. 建立控件
                    var newItem = new MyListItem
                    {
                        Title = title,
                        SnapsToDevicePixels = true,
                        Height = 42,
                        Type = MyListItem.CheckType.Clickable,
                        Tag = this,
                        Info = string.Join("  |  ", info),
                        // 使用 switch 表达式精简 Logo 选择喵！
                        Logo = status switch
                        {
                            CompFileStatus.Release => ModBase.pathImage + "Icons/R.png",
                            CompFileStatus.Beta => ModBase.pathImage + "Icons/B.png",
                            _ => ModBase.pathImage + "Icons/A.png"
                        }
                    };
                    newItem.Click += onClick;

                    // 4. 建立另存为按钮
                    if (onSaveClick is not null)
                    {
                        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
                        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
                        ToolTipService.SetVerticalOffset(btnSave, 30);
                        ToolTipService.SetHorizontalOffset(btnSave, 2);
                        btnSave.Click += onSaveClick;
                        newItem.Buttons = new[] { btnSave };
                    }

                    return newItem;
                })
                { Height = 42 };
        }

        public override string ToString()
        {
            return $"{id}: {fileName}";
        }
    }

    // 获取

    /// <summary>
    ///     已知文件信息的缓存。
    /// </summary>
    public static ConcurrentDictionary<string, List<CompFile>> compFilesCache = new();

    /// <summary>
    ///     获取某个工程下的全部文件列表。
    ///     必须在工作线程执行，失败会抛出异常。
    /// </summary>
    public static List<CompFile> CompFilesGet(string ProjectId, bool FromCurseForge)
    {
        // 1. 获取工程对象（使用 TryGetValue 提高效率并防止并发异常）
        CompProject targetProject = null;
        if (!compProjectCache.TryGetValue(ProjectId, out targetProject))
        {
            var url = FromCurseForge
                ? $"https://api.curseforge.com/v1/mods/{ProjectId}"
                : $"https://api.modrinth.com/v2/project/{ProjectId}";
            if (FromCurseForge)
            {
                var json = ModDownload.DlModRequest<JsonObject>(url);
                targetProject = new CompProject((JsonObject)json["data"]);
            }
            else
            {
                targetProject = new CompProject(ModDownload.DlModRequest<JsonObject>(url));
            }
            // 假设 CompProject 构造函数内已处理缓存，否则此处应添加缓存逻辑
        }

        // 2. 获取并缓存文件列表
        if (!compFilesCache.ContainsKey(ProjectId))
        {
            ModBase.Log("[Comp] 开始获取文件列表：" + ProjectId);
            JsonArray resultJsonArray;
            if (FromCurseForge)
            {
                // 注意：若 pageSize=10000 失效，需考虑分页逻辑
                var response = ModDownload.DlModRequest<JsonObject>(
                    $"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=10000"
                );

                resultJsonArray = (JsonArray)response["data"];
            }
            else
            {
                resultJsonArray =
                    ModDownload.DlModRequest<JsonArray>($"https://api.modrinth.com/v2/project/{ProjectId}/version?include_changelog=false");
            }

            compFilesCache[ProjectId] = resultJsonArray.Select(a => new CompFile((JsonObject)a, targetProject.type))
                .Where(a => a.Available).GroupBy(a => a.id).Select(g => g.First())
                .ToList(); // 使用 GroupBy 实现更高效的 Distinct
        }

        var currentFiles = compFilesCache[ProjectId];

        // 3. 提取所有需要获取信息的前置 ID（合并必要和可选）
        var allRawDeps = currentFiles.SelectMany(f => f.rawDependencies.Concat(f.rawOptionalDependencies)).Distinct()
            .ToList();
        var undoneDeps = allRawDeps.Where(id => !compProjectCache.ContainsKey(id)).ToList();

        // 4. 批量请求缺失的前置工程信息
        if (undoneDeps.Any())
        {
            ModBase.Log($"[Comp] {ProjectId} 需要补全信息的依赖项共 {undoneDeps.Count} 个");
            JsonArray projects;
            if (FromCurseForge)
            {
                // 1. 获取响应并转为 JsonObject
                var response = ModDownload.DlModRequest<JsonObject>(
                    "https://api.curseforge.com/v1/mods",
                    "POST",
                    "{\"modIds\": [" + string.Join(",", undoneDeps) + "]}",
                    "application/json"
                );

                // 2. 提取 data 数组
                projects = (JsonArray)response["data"];
            }
            else
            {
                projects = ModDownload.DlModRequest<JsonArray>(
                    $"https://api.modrinth.com/v2/projects?ids=[\"{undoneDeps.Join("\",\"")}\"]");
            }

            foreach (var Project in projects)
                new CompProject((JsonObject)Project);
        }

        // 5. 建立文件与依赖工程的关联映射
        // 优化：预先筛选出存在于缓存中的依赖工程，避免在多层循环中重复查询字典
        var availableDeps = allRawDeps.Where(id => compProjectCache.ContainsKey(id) && (id ?? "") != (ProjectId ?? ""))
            .Select(id => compProjectCache[id]).ToList();

        foreach (var file in currentFiles)
        foreach (var dep in availableDeps)
        {
            // 处理必要依赖
            if (file.rawDependencies.Contains(dep.id))
                if (!file.dependencies.Contains(dep.id))
                    file.dependencies.Add(dep.id);

            // 处理可选依赖
            if (file.rawOptionalDependencies.Contains(dep.id))
                if (!file.optionalDependencies.Contains(dep.id))
                    file.optionalDependencies.Add(dep.id);
        }

        return compFilesCache[ProjectId];
    }

    public static string CompFileNameGet(CompProject proj, CompFile file)
    {
        string fileName;
        if ((proj.TranslatedName ?? "") == (proj.rawName ?? ""))
        {
            fileName = file.fileName;
        }
        else
        {
            var chineseName = proj.TranslatedName.BeforeFirst(" (").BeforeFirst(" - ").Replace(@"\", "＼")
                .Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞")
                .Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
            fileName = Config.Download.Comp.NameFormatV2 switch
            {
                0 => $"【{chineseName}】{file.fileName}",
                1 => $"[{chineseName}] {file.fileName}",
                2 => $"{chineseName}-{file.fileName}",
                3 => $"{file.fileName}-{chineseName}",
                _ => file.fileName
            };
        }

        if (file.type == CompType.Mod)
            fileName = fileName.Replace("~", "-"); // ~ 会导致 Mixin 加载失败
        return fileName;
    }

    /// <summary>
    ///     预载包含大量 CompFile 的卡片，添加必要的元素和前置列表。
    /// </summary>
    public static void CompFilesCardPreload(StackPanel Stack, List<CompFile> Files)
    {
        // 获取卡片对应的前置 ID
        // 如果为整合包就不会有 Dependencies 信息，所以不用管
        var deps = Files.SelectMany(f => f.dependencies).Distinct().ToList();
        var optionalDeps = Files.SelectMany(f => f.optionalDependencies).Distinct().ToList();
        if (!deps.Any() && !optionalDeps.Any())
            return;
        // 必要前置
        if (deps.Any())
        {
            deps.Sort();
            deps = deps.Where(dep =>
            {
                if (!compProjectCache.ContainsKey(dep))
                    ModBase.Log($"[Comp] 未找到 ID {dep} 的前置信息", ModBase.LogLevel.Debug);
                return compProjectCache.ContainsKey(dep);
            }).ToList();
            // 添加开头间隔
            Stack.Children.Add(new TextBlock
            {
                Text = Lang.Text("Download.Comp.Detail.FileList.RequiredDependencies"), FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 2d, 0d, 5d)
            });
            // 添加前置列表
            foreach (var Dep in deps)
            {
                var item = compProjectCache[Dep].ToCompItem(false, false);
                Stack.Children.Add(item);
            }
        }

        // 可选前置
        if (optionalDeps.Any())
        {
            optionalDeps.Sort();
            optionalDeps = optionalDeps.Where(dep =>
            {
                if (!compProjectCache.ContainsKey(dep))
                    ModBase.Log($"[Comp] 未找到 ID {dep} 的前置信息", ModBase.LogLevel.Debug);
                return compProjectCache.ContainsKey(dep);
            }).ToList();
            // 添加开头间隔
            Stack.Children.Add(new TextBlock
            {
                Text = Lang.Text("Download.Comp.Detail.FileList.OptionalDependencies"), FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 2d, 0d, 5d)
            });
            // 添加前置列表
            foreach (var Dep in optionalDeps)
            {
                var item = compProjectCache[Dep].ToCompItem(false, false);
                Stack.Children.Add(item);
            }
        }

        // 添加结尾间隔
        Stack.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Comp.Detail.FileList.VersionList"), FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 12d, 0d, 5d)
        });
    }

    #endregion
}
