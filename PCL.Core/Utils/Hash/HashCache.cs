using Microsoft.Data.Sqlite;
using PCL.Core.Utils.Exts;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Hash;

public class HashCache
{
    private readonly string _dbPath;

    public HashCache(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        var dir = Path.GetDirectoryName(Path.GetFullPath(_dbPath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        _Initialize();
    }

    private void _Initialize()
    {
        using var connection = _CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS HashCache (
                FilePath TEXT NOT NULL PRIMARY KEY,
                FileSize INTEGER NOT NULL,
                LastWriteTime TEXT NOT NULL,
                MD5 TEXT NULL,
                SHA1 TEXT NULL,
                SHA256 TEXT NULL,
                SHA512 TEXT NULL,
                MurmurHash2 TEXT NULL
            )
            """;
        cmd.ExecuteNonQuery();

        using var setCmd = connection.CreateCommand();
        setCmd.CommandText = "PRAGMA journal_mode=WAL";
        setCmd.ExecuteNonQuery();
    }

    private SqliteConnection _CreateConnection()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=True");
        connection.Open();
        return connection;
    }

    public Task<string> GetMD5Async(string filePath) =>
        _GetHashWithPending(filePath, MD5Provider.Instance, "MD5");

    public Task<string> GetSHA1Async(string filePath) =>
        _GetHashWithPending(filePath, SHA1Provider.Instance, "SHA1");

    public Task<string> GetSHA256Async(string filePath) =>
        _GetHashWithPending(filePath, SHA256Provider.Instance, "SHA256");

    public Task<string> GetSHA512Async(string filePath) =>
        _GetHashWithPending(filePath, SHA512Provider.Instance, "SHA512");

    public Task<string> GetMurmurHash2Async(string filePath) =>
        _GetHashWithPending(filePath, MurmurHash2Provider.Instance, "MurmurHash2");

    private static readonly ConcurrentDictionary<string, Task<string>> _FileHashComputePending = new();

    public async Task<string> GetHashAsync(string filePath, IHashProvider provider)
    {
        var algoName = provider switch
        {
            MD5Provider => "MD5",
            SHA1Provider => "SHA1",
            SHA256Provider => "SHA256",
            SHA512Provider => "SHA512",
            MurmurHash2Provider => "MurmurHash2",
            _ => throw new ArgumentException($"不支持的哈希算法: {provider.GetType().Name}")
        };
        var computeKey = $"{filePath}:{algoName}";
        return await _GetHashWithPending(filePath, provider, algoName).ConfigureAwait(false);
    }

    private Task<string> _GetHashWithPending(string filePath, IHashProvider provider, string algoName)
    {
        var computeKey = $"{filePath}:{algoName}";
        return _FileHashComputePending.GetOrAdd(computeKey, key =>
        {
            var computeTask = _GetHashAsync(filePath, provider, algoName);
            _ = computeTask.ContinueWith(t =>
            {
                _FileHashComputePending.TryRemove(computeKey, out _);
            }, TaskContinuationOptions.ExecuteSynchronously);
            return computeTask;
        });
    }

    private async Task<string> _GetHashAsync(string filePath, IHashProvider provider, string algoName)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        try
        {
            var fileInfo = new FileInfo(fullPath);
            var fileSize = fileInfo.Length;
            var lastWrite = fileInfo.LastWriteTimeUtc.ToString("O");

            var cached = await _FindCacheEntryAsync(fullPath).ConfigureAwait(false);

            if (cached != null)
            {
                if (cached.FileSize == fileSize && cached.LastWriteTime == lastWrite)
                {
                    var hash = _GetHashFromEntry(cached, algoName);
                    if (hash != null)
                        return hash;

                    var computedHash = await _ComputeHashAsync(fullPath, provider).ConfigureAwait(false);
                    await _InsertOrUpdateHashAsync(fullPath, fileSize, lastWrite, algoName, computedHash).ConfigureAwait(false);
                    return computedHash;
                }
                else
                {
                    await _DeleteCacheEntryAsync(fullPath).ConfigureAwait(false);
                }
            }

            var computed = await _ComputeHashAsync(fullPath, provider).ConfigureAwait(false);
            await _InsertOrUpdateHashAsync(fullPath, fileSize, lastWrite, algoName, computed).ConfigureAwait(false);
            return computed;
        }
        catch (FileNotFoundException)
        {
            await _DeleteCacheEntryAsync(fullPath).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<string> _ComputeHashAsync(string fullPath, IHashProvider provider)
    {
        using FileStream fs = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (await provider.ComputeHashAsync(fs).ConfigureAwait(false)).ToHexString();
    }

    private async Task<CacheEntry?> _FindCacheEntryAsync(string fullPath)
    {
        using var conn = _CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT FilePath, FileSize, LastWriteTime, MD5, SHA1, SHA256, SHA512, MurmurHash2 FROM HashCache WHERE FilePath = @FilePath";
        cmd.Parameters.AddWithValue("@FilePath", fullPath);

        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
            return null;

        return new CacheEntry
        {
            FilePath = reader.GetString(0),
            FileSize = reader.GetInt64(1),
            LastWriteTime = reader.GetString(2),
            MD5 = reader.IsDBNull(3) ? null : reader.GetString(3),
            SHA1 = reader.IsDBNull(4) ? null : reader.GetString(4),
            SHA256 = reader.IsDBNull(5) ? null : reader.GetString(5),
            SHA512 = reader.IsDBNull(6) ? null : reader.GetString(6),
            MurmurHash2 = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }

    private static string? _GetHashFromEntry(CacheEntry entry, string algoName) => algoName switch
    {
        "MD5" => entry.MD5,
        "SHA1" => entry.SHA1,
        "SHA256" => entry.SHA256,
        "SHA512" => entry.SHA512,
        "MurmurHash2" => entry.MurmurHash2,
        _ => null
    };

    private async Task _InsertOrUpdateHashAsync(string fullPath, long fileSize, string lastWrite, string algoName, string hash)
    {
        if (hash.IsNullOrWhiteSpace()) return;
        using var conn = _CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO HashCache (FilePath, FileSize, LastWriteTime, MD5, SHA1, SHA256, SHA512, MurmurHash2)
            VALUES (@FilePath, @FileSize, @LastWriteTime, @MD5, @SHA1, @SHA256, @SHA512, @MurmurHash2)
            ON CONFLICT(FilePath) DO UPDATE SET
                FileSize = excluded.FileSize,
                LastWriteTime = excluded.LastWriteTime,
                MD5 = COALESCE(excluded.MD5, HashCache.MD5),
                SHA1 = COALESCE(excluded.SHA1, HashCache.SHA1),
                SHA256 = COALESCE(excluded.SHA256, HashCache.SHA256),
                SHA512 = COALESCE(excluded.SHA512, HashCache.SHA512),
                MurmurHash2 = COALESCE(excluded.MurmurHash2, HashCache.MurmurHash2)
            """;
        cmd.Parameters.AddWithValue("@FilePath", fullPath);
        cmd.Parameters.AddWithValue("@FileSize", fileSize);
        cmd.Parameters.AddWithValue("@LastWriteTime", lastWrite);
        cmd.Parameters.AddWithValue("@MD5", algoName == "MD5" ? hash : DBNull.Value);
        cmd.Parameters.AddWithValue("@SHA1", algoName == "SHA1" ? hash : DBNull.Value);
        cmd.Parameters.AddWithValue("@SHA256", algoName == "SHA256" ? hash : DBNull.Value);
        cmd.Parameters.AddWithValue("@SHA512", algoName == "SHA512" ? hash : DBNull.Value);
        cmd.Parameters.AddWithValue("@MurmurHash2", algoName == "MurmurHash2" ? hash : DBNull.Value);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task _DeleteCacheEntryAsync(string fullPath)
    {
        using var conn = _CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM HashCache WHERE FilePath = @FilePath";
        cmd.Parameters.AddWithValue("@FilePath", fullPath);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private sealed class CacheEntry
    {
        public string FilePath { get; init; } = "";
        public long FileSize { get; init; }
        public string LastWriteTime { get; init; } = "";
        public string? MD5 { get; init; }
        public string? SHA1 { get; init; }
        public string? SHA256 { get; init; }
        public string? SHA512 { get; init; }
        public string? MurmurHash2 { get; init; }
    }
}
