using System.IO;
using System.IO.Compression;
using Dapper;
using Microsoft.Data.Sqlite;
using PCL.Core.App.Localization;
using PCL.Core.Utils.Hash;
using ProtoBuf;

namespace PCL;

public static partial class Comp
{
    private static readonly Lazy<string> DbInitializer = new(InitializeModDbAndGetConnectionString);

    private static string CompDbConnectionString => DbInitializer.Value;

    private static SqliteConnection CompDb
    {
        get
        {
            var conn = new SqliteConnection(CompDbConnectionString);
            conn.Open();
            return conn;
        }
    }

    private static string InitializeModDbAndGetConnectionString()
    {
        Common.Log("[DB] 解压 ModData (SQLite) 中");
        using var compressedDbData = Common.GetResourceStream("Resources/mcmod.buf")
            ?? throw new InvalidOperationException("无法加载内置的模组数据库资源。");
        using var trueDbFile = new GZipStream(compressedDbData, CompressionMode.Decompress);
        using var ms = new MemoryStream();

        trueDbFile.CopyTo(ms);
        ms.Seek(0L, SeekOrigin.Begin);

        var fileHash = Common.GetHexString(SHA1Provider.Instance.ComputeHash(ms));
        var dbDir = Path.Combine(Common.pathTemp, "Cache");
        var dbPath = Path.Combine(dbDir, $"ModData{fileHash}.sqlite");

        if (File.Exists(dbPath) && !IsDatabaseValid(dbPath)) File.Delete(dbPath);
        if (File.Exists(dbPath)) return $"Data Source=\"{dbPath}\"";

        ms.Seek(0L, SeekOrigin.Begin);
        var entries = Serializer.Deserialize<List<CompDatabaseEntry>>(ms);

        Directory.CreateDirectory(dbDir);

        var tempPath = dbPath + ".tmp";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        using (var buildDbConnection =
               new SqliteConnection($"Data Source=\"{tempPath}\";Pooling=False"))
        {
            buildDbConnection.Open();

            using (var transaction = buildDbConnection.BeginTransaction())
            {
                buildDbConnection.Execute(
                    """
                    CREATE TABLE ModTranslation (
                        WikiId INTEGER,
                        ChineseName TEXT,
                        CurseForgeSlug TEXT,
                        ModrinthSlug TEXT
                    );
                    CREATE INDEX idx_curseforge ON ModTranslation (CurseForgeSlug);
                    CREATE INDEX idx_modrinth ON ModTranslation (ModrinthSlug);
                    CREATE INDEX idx_chinesename ON ModTranslation (ChineseName);
                    """);
                const string insertSql = """
                                         INSERT INTO ModTranslation (
                                             WikiId,
                                             ChineseName,
                                             CurseForgeSlug,
                                             ModrinthSlug
                                         )
                                         VALUES (@WikiId, @ChineseName, @CurseForgeSlug, @ModrinthSlug)
                                         """;

                foreach (var entry in entries)
                    buildDbConnection.Execute(insertSql, entry, transaction);

                transaction.Commit();
            }
        }

        File.Move(tempPath, dbPath, true);

        return $"Data Source=\"{dbPath}\"";
    }

    /// <summary>
    ///     验证 SQLite 数据库文件是否包含预期的表且非空
    /// </summary>
    private static bool IsDatabaseValid(string dbPath)
    {
        try
        {
            using var conn =
                new SqliteConnection($"Data Source=\"{dbPath}\";Pooling=False;Mode=ReadOnly");
            conn.Open();
            var tableCheck = conn.ExecuteScalar<int>(
                "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='ModTranslation'");
            if (tableCheck == 0) return false;
            var rowCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM ModTranslation");
            return rowCount > 0;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "检查模组翻译数据库有效性失败");
            return false;
        }
    }

    private static CompDatabaseEntry? GetCompWikiEntryBySlug(string slug)
    {
        try
        {
            using var conn = CompDb;
            return conn.QueryFirstOrDefault<CompDatabaseEntry>(
                "SELECT * FROM ModTranslation WHERE CurseForgeSlug = @s OR ModrinthSlug = @s LIMIT 1",
                new
                {
                    s = slug
                });
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "获取模组翻译信息失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Minecraft.Comp.Error.OperationFailed"));
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
        public string? CurseForgeSlug { get; set; }

        /// <summary>
        ///     Modrinth Slug（例如 advanced-solar-panels）。
        /// </summary>
        [ProtoMember(4)]
        public string? ModrinthSlug { get; set; }

        public override string ToString()
        {
            return $"{CurseForgeSlug ?? ""}&{ModrinthSlug ?? ""}|{WikiId}|{ChineseName}";
        }
    }
}
