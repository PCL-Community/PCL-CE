using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PCL.Core.IO.Net.Http.Cache.Models;
using PCL.Core.IO.Storage;
using PCL.Core.Logging;
using PCL.Core.Utils.Hash;

namespace PCL.Core.IO.Net.Http.Cache;

/// <summary>
/// HTTP 缓存储存库，支持 LazyGC
/// </summary>
/// <param name="dbPath">SQLite 数据库路径</param>
/// <param name="destLocation">存储位置</param>
public class HttpCacheRepository(string dbPath,string destLocation)
{

    #region "预设 SQL 命令"

    private const string FindTable = "SELECT * FROM HttpCache WHERE RequestUri = @Uri";
    
    private const string CreateTable = """

                                           CREATE TABLE IF NOT EXISTS HttpCache (
                                           RequestUri TEXT NOT NULL PRIMARY KEY,
                                           Tag TEXT NULL,
                                           LastModify TEXT NULL,
                                           ExpiredAt INTEGER NOT NULL,
                                           EnsureValidate INTEGER NOT NULL DEFAULT 0,
                                           Status INTEGER NOT NULL DEFAULT 0,
                                           LastUpdate TEXT NOT NULL,
                                           Hash TEXT NULL
                                       )
                                       """;
    
    private const string InsertTable = """
                                       INSERT OR REPLACE INTO HttpCache (
                                           RequestUri, Tag, LastModify, ExpiredAt, EnsureValidate, Status, LastUpdate, Hash
                                       ) VALUES (
                                           @Uri, @Tag, @LastModify, @ExpiredAt, @EnsureValidate, @Status, @LastUpdate, @Hash
                                       )
                                       """;
    
    
    private const string DeleteTable = "DELETE FROM HttpCache WHERE RequestUri = @Uri";


    #endregion

    #region "配置"


    private readonly Func<SqliteConnection> _connectionFactory = () =>
    {
        var c = new SqliteConnection($"Data Source={dbPath}");
        c.Open();
        return c;
    };

    private readonly HashStorage _store = new(destLocation, SHA256Provider.Instance, true);
    
    #endregion

    #region "HTTP 缓存处理"
    
    /// <summary>
    /// 初始化数据库
    /// </summary>
    public void Initialize()
    {
        try
        {
            if (!Directory.Exists(destLocation)) Directory.CreateDirectory(destLocation);
        }
        catch (IOException)
        {
            File.Delete(destLocation);
            Directory.CreateDirectory(destLocation);
        }

        using var connection = _connectionFactory.Invoke();
        var cmd = connection.CreateCommand();
        cmd.CommandText = CreateTable;
        cmd.ExecuteNonQuery();
    }
    
    /// <summary>
    /// 获取缓存数据
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="details"></param>
    /// <returns></returns>
    public bool TryGetCacheData(string uri,[NotNullWhen(true)] out HttpCacheDetails? details)
    {
        details = null;
        using var conn = _connectionFactory.Invoke();
        using var cmd = _FindTableWithUri(uri, conn);
        using var result = cmd.ExecuteReader();
        if (!result.Read()) return false;
        if ((HttpCacheStatus)result.GetInt16(6) is HttpCacheStatus.Invalid or HttpCacheStatus.Expired)
        {
            _DeleteTable(result.GetString(0), conn);
            return false;
        }
        details = new HttpCacheDetails(this)
        {
            RequestUri = result.GetString(0),
            Tag = result.GetString(1),
            LastModify = result.GetString(2),
            ExpiredAt = result.GetInt32(3),
            EnsureValidate = result.GetBoolean(4),
            Status = (HttpCacheStatus)result.GetInt16(5),
            LastUpdate = DateTimeOffset.Parse(result.GetString(6))
        };
        return true;
    }
    
    /// <summary>
    /// 获取已缓存的响应 
    /// </summary>
    /// <param name="request">发出的 HTTP 请求</param>
    /// <param name="response">缓存响应</param>
    /// <returns>如果缓存存在，返回 true</returns>
    public bool TryGetCacheResponse(HttpRequestMessage request,[NotNullWhen(true)] out HttpResponseMessage? response)
    {
        response = null;
        if (request.RequestUri is null) return false;
        if (!TryGetCacheData(request.RequestUri.ToString(), out var details)) return false;
        if (details.Status is HttpCacheStatus.Updating)
            return false; 
        response = new HttpResponseMessage 
        { 
            StatusCode = HttpStatusCode.OK, 
            Content = new StreamContent(_store.Get(details.Hash!) ?? throw new NullReferenceException("Hash Storage return null.")), 
            RequestMessage = request
        };
        response.Headers.TryAddWithoutValidation("X-Cache-Repository-Status", "Hit");
        
        return true;
    }

    /// <summary>
    /// 获取缓存更新句柄
    /// </summary>
    /// <param name="uri">URL</param>
    /// <returns></returns>
    public async ValueTask<HttpCacheUpdateHandle?> TryBeginUpdateAsync(string uri)
    {
        await using var conn = _connectionFactory.Invoke();
        if (!TryGetCacheData(uri, out var details))
        {
            Span<byte> buffer = stackalloc byte[16];
            Random.Shared.NextBytes(buffer);
            details = new HttpCacheDetails(this)
            {
                LastUpdate = DateTimeOffset.Now,
                RequestUri = uri,
                EnsureValidate = false,
                ExpiredAt = null,
                Tag = null,
                Status = HttpCacheStatus.Updating,
                Hash = null
            };
            await using var cmd = _InsertDatabase(details, conn);
            cmd.ExecuteNonQuery();
            // 互斥锁，避免线程冲突
        }else if (details.Status == HttpCacheStatus.Updating) return null;
        
        var handle = details.GetUpdateHandle();
        await _store.PutAsync(handle.GetOutputStream());
        return handle;
    }
    /// <summary>
    /// 异步结束更新并设置缓存状态
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public async ValueTask<bool> TryEndUpdateAsync(HttpCacheUpdateHandle handle)
    {
        await using var conn = _connectionFactory.Invoke();
        var details = handle.Details;
        if (details is null) return false;
        details.Status = HttpCacheStatus.Ok;
        await using var cmd = _UpdateTable(details,conn);
        if (cmd is null) return true;
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    
    /// <summary>
    /// 删除一个缓存
    /// </summary>
    /// <param name="request">请求</param>
    /// <returns></returns>
    public bool TryRemove(HttpRequestMessage request)
    {
        using var conn = _connectionFactory.Invoke();
        try
        {
            if (!TryGetCacheData(request.RequestUri!.ToString(), out var details) && details?.Hash is null) return false;
            if (request.RequestUri is null) return false;
            using var cmd = _DeleteTable(request.RequestUri.ToString(), conn);
            cmd.ExecuteNonQuery();
            _store.DeleteAsync(details.Hash!).GetAwaiter().GetResult();
            return true;
        }
        catch(Exception ex)
        {
            LogWrapper.Error(ex,"Http", "删除缓存文件失败");
        }

        return false;
    }
    
    /// <summary>
    /// 删除一个缓存
    /// </summary>
    /// <param name="request">请求</param>
    /// <returns></returns>
    public async ValueTask<bool> TryRemoveAsync(HttpRequestMessage request)
    {
        await using var conn = _connectionFactory.Invoke();
        try
        {
            if (!TryGetCacheData(request.RequestUri!.ToString(), out var details) && details?.Hash is null) return false;
            if (request.RequestUri is null) return false;
            await using var cmd = _DeleteTable(request.RequestUri.ToString(), conn);
            await cmd.ExecuteNonQueryAsync();
            await _store.DeleteAsync(details.Hash!).ConfigureAwait(false);
            return true;
        }
        catch(Exception ex)
        {
            LogWrapper.Error(ex,"Http", "删除缓存文件失败");
        }

        return false;
    }
    
    /// <summary>
    /// 将全部对象标记为过期
    /// </summary>
    public void MarkAllObjectAsExpired()
    {
        using var conn = _connectionFactory.Invoke();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE HttpCache SET Status = 2";
        cmd.ExecuteNonQuery();
    }
    
    

    #endregion

    #region "SQL 执行函数"

    
    private static SqliteCommand _InsertDatabase(HttpCacheDetails details, SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = InsertTable;
        cmd.Parameters.AddWithValue("@Uri", details.RequestUri);
        cmd.Parameters.AddWithValue("@Tag", details.Tag);
        cmd.Parameters.AddWithValue("@LastModify", details.LastModify);
        cmd.Parameters.AddWithValue("@ExpiredAt", details.ExpiredAt);
        cmd.Parameters.AddWithValue("@EnsureValidate", details.EnsureValidate);
        cmd.Parameters.AddWithValue("@Status", (int)details.Status);
        cmd.Parameters.AddWithValue("@Hash", details.Hash);
        return cmd;
    }

    private static SqliteCommand _DeleteTable(string uri, SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = DeleteTable;
        cmd.Parameters.AddWithValue("@Uri", uri);
        return cmd;
    }

    private static SqliteCommand _FindTableWithUri(string uri,  SqliteConnection conn)
    {
        var queryCmd = conn.CreateCommand();
        queryCmd.CommandText = FindTable;
        queryCmd.Parameters.AddWithValue("@Uri", uri);
        return queryCmd;
    }
    
    private SqliteCommand? _UpdateTable(HttpCacheDetails details, SqliteConnection conn)
    {
        using var queryCmd = _FindTableWithUri(details.RequestUri, conn);
        // 获取用于比较的原始内容
        using var reader = queryCmd.ExecuteReader();
        if (!reader.Read())
        {
            // 可能已经被删掉了，添加就好
            return _InsertDatabase(details,conn);
            
        }
        var sb = new StringBuilder();
        sb.Append("UPDATE HttpCache ");
        var writeCmd = conn.CreateCommand();
        writeCmd.Disposed += (_, _) => conn.Dispose(); 
        var setCount = 0;
        // 按需更新以减少开销
        if (reader.GetString(0) != details.RequestUri)
        {
            setCount++;
            sb.Append("SET RequestUri = @Uri, ");
            writeCmd.Parameters.AddWithValue("@Uri", details.RequestUri);
        }

        if (reader.GetString(1) != details.Tag)
        {
            setCount++;
            sb.Append("SET Tag = @Tag, ");
            writeCmd.Parameters.AddWithValue("@Tag", details.Tag);
        }
        if (reader.GetString(2) != details.LastModify)
        {
            setCount++;
            sb.Append("SET LastModify = @LastModify, ");
            writeCmd.Parameters.AddWithValue("@LastModify", details.LastModify);
        }
        if (reader.GetInt32(3) != details.ExpiredAt)
        {
            setCount++;
            sb.Append("SET ExpiredAt = @ExpiredAt, ");
            writeCmd.Parameters.AddWithValue("@ExpiredAt", details.ExpiredAt);
        }

        if (reader.GetBoolean(4) != details.EnsureValidate)
        {
            setCount++;
            sb.Append("SET EnsureValidate = @EnsureValidate,");
            writeCmd.Parameters.AddWithValue("@EnsureValidate", details.EnsureValidate);
        }
        if ((HttpCacheStatus)reader.GetInt16(5) != details.Status)
        {
            setCount++;
            sb.Append("SET Status = @Status,");
            writeCmd.Parameters.AddWithValue("@Status", (int)details.Status);
        }

        if (reader.GetString(6) != details.LastUpdate.ToString())
        {
            setCount++;
            sb.Append("SET LastUpdate = @LastUpdate,");
            writeCmd.Parameters.AddWithValue("@LastUpdate", details.LastUpdate.ToString());
        }

        if (reader.GetString(7) != details.Hash)
        {
            setCount++;
            sb.Append("SET Hash = @Hash");
            writeCmd.Parameters.AddWithValue("@Hash", details.Hash);

        }

        if (setCount == 0) return null;
        sb.Append("WHERE RequestUri = @Uri");
        writeCmd.CommandText = sb.ToString();
        return writeCmd;
    }
    
    
    #endregion
}