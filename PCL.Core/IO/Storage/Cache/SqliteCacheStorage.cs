using Microsoft.Data.Sqlite;
using PCL.Core.IO.Storage.Cache.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Storage.Cache;

public class SqliteCacheStorage(string dbPath) : IDisposable
{
    private readonly string _connectionString = $"Data Source={dbPath};Pooling=True";
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    private async Task<SqliteConnection> _CreateConnectionAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SqliteCacheStorage));
        }

        var conn = new SqliteConnection(_connectionString);

        // NOTE: WAL mode is managed in SchemaManager, so we don't set it here.
        // but new connection may need re-enable some session-level pragmas
        // but we don't have any for now, so we just return the connection.
        await conn.OpenAsync().ConfigureAwait(false);

        return conn;
    }

    public async Task CleanupStartupAsync()
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM cache_entries WHERE expires_at IS NOT NULL AND expires_at < datetime('now')";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        await using var walCmd = conn.CreateCommand();
        walCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
        await walCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    #region cache_entries

    public async Task UpsertAsync(CacheEntry entry, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO cache_entries (
                    cache_key, entry_type, content_type, data, data_size, file_hash, file_path,
                    content_hash, content_version, created_at, last_access_at, expires_at,
                    hit_count, tags, group_name, priority
                ) VALUES (
                    @cache_key, @entry_type, @content_type, @data, @data_size, @file_hash, @file_path,
                    @content_hash, @content_version, COALESCE((SELECT created_at FROM cache_entries WHERE cache_key = @cache_key), datetime('now')),
                    datetime('now'), @expires_at, 0, @tags, @group_name, @priority
                )
                """;
            _BindEntryParams(cmd, entry);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<CacheEntry?> LookupAsync(string cacheKey, CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM cache_entries WHERE cache_key = @cache_key";
        cmd.Parameters.AddWithValue("@cache_key", cacheKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? _ReadEntry(reader) : null;
    }

    public async Task TouchAsync(string key, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = """
                              UPDATE cache_entries SET last_access_at = datetime('now'), hit_count = hit_count + 1 WHERE cache_key = @cache_key
                              """;
            cmd.Parameters.AddWithValue("@cache_key", key);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string cacheKey, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM cache_entries WHERE cache_key = @cache_key";
            cmd.Parameters.AddWithValue("@cache_key", cacheKey);
            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return affected > 0;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM cache_entries WHERE expires_at IS NOT NULL AND datetime(expires_at) < datetime('now')";
            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return affected;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> DeleteByGroupAsync(string groupName, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM cache_entries WHERE group_name = @group_name";
            cmd.Parameters.AddWithValue("@group_name", groupName);
            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return affected;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<string?>> GetFileHashesByGroupAsync(string groupName, CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT file_hash FROM cache_entries WHERE group_name = @group_name AND file_hash IS NOT NULL";
        cmd.Parameters.AddWithValue("@group_name", groupName);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var hashes = new List<string?>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            hashes.Add(reader.GetString(0));
        }
        return hashes;
    }

    public async Task<List<string?>> GetAllFileHashesAsync(CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT file_hash FROM cache_entries WHERE file_hash IS NOT NULL";

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var hashes = new List<string?>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            hashes.Add(reader.GetString(0));
        }
        return hashes;
    }

    public async Task<int> DeleteByTagAsync(string tag, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM cache_entries WHERE tags = @t OR tags LIKE @p1 OR tags LIKE @p2 OR tags LIKE @p3";
            cmd.Parameters.AddWithValue("@t", tag);
            cmd.Parameters.AddWithValue("@p1", $"{tag},%");
            cmd.Parameters.AddWithValue("@p2", $"%,{tag},%");
            cmd.Parameters.AddWithValue("@p3", $"%,{tag}");
            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return affected;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<CacheStatsRow> GetStatsAsync(CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) AS total, COALESCE(SUM(data_size), 0) AS total_size, COALESCE(SUM(CASE WHEN expires_at < datetime('now') THEN 1 ELSE 0 END), 0) AS expired, COALESCE(SUM(CASE WHEN entry_type = 0 THEN 1 ELSE 0 END), 0) AS inline, COALESCE(SUM(CASE WHEN entry_type = 1 THEN 1 ELSE 0 END), 0) AS file_ref FROM cache_entries";
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new CacheStatsRow(
                TotalEntries: reader.GetInt64(0),
                TotalSizeBytes: reader.GetInt64(1),
                ExpiredEntries: reader.GetInt64(2),
                InlineEntries: reader.GetInt64(3),
                FileEntries: reader.GetInt64(4)
            );
        }

        // none
        return new CacheStatsRow(0, 0, 0, 0, 0);
    }

    public async Task<List<EvictionCandidate>> GetEvictionCandidatesAsync(int limit, CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
                          SELECT cache_key, entry_type, file_hash, data_size, priority, hit_count
                          FROM cache_entries
                          WHERE priority < 3        -- 不淘汰 NeverEvict
                          ORDER BY priority ASC, hit_count ASC, last_access_at ASC
                          LIMIT @limit
                          """;
        cmd.Parameters.AddWithValue("@limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var list = new List<EvictionCandidate>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new EvictionCandidate(
                CacheKey: reader.GetString(0),
                EntryType: (EntryType)reader.GetInt32(1),
                FileHash: reader.IsDBNull(2) ? null : reader.GetString(2),
                DataSize: reader.GetInt64(3),
                Priority: reader.GetInt32(4),
                HitCount: reader.GetInt64(5)
            ));
        }
        return list;
    }

    public async Task CompactAsync(CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "VACUUM";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    #endregion

    #region instance_cache

    public async Task UpsertInstanceAsync(InstanceCacheRow row, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """

                                              INSERT OR REPLACE INTO instance_cache
                                                  (instance_path, instance_name, instance_state, card_type, is_starred,
                                                   logo, description, release_time,
                                                   vanilla_name, vanilla_version, drop_number, reliable,
                                                   loaders_json,
                                                   main_class, assets_index, inherits_from, java_version,
                                                   source_json_hash, format_version, cached_at, last_loaded_at)
                                              VALUES
                                                  (@p, @n, @st, @cty, @is,
                                                   @lo, @de, @rt,
                                                   @vn, @vv, @dn, @re,
                                                   @lj,
                                                   @mc, @ai, @if, @jv,
                                                   @sh, @fv, datetime('now'), datetime('now'))
                              """;
            _BindInstanceParams(cmd, row);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<InstanceCacheRow?> LookupInstanceAsync(string instancePath, CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM instance_cache WHERE instance_path = @p";
        cmd.Parameters.AddWithValue("@p", instancePath);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? _ReadInstanceRow(reader) : null;
    }

    public async Task<int> DeleteInstanceAsync(string instancePath, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM instance_cache WHERE instance_path = @p";
            cmd.Parameters.AddWithValue("@p", instancePath);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    #endregion

    #region component_cache

    public async Task UpsertComponentAsync(ComponentCacheRow row, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO component_cache
                    (instance_path, comp_type, file_name, relative_path,
                     file_hash, file_size, last_modified, enabled,
                     mod_name, mod_version, mod_author, mod_description,
                     mod_loader, mod_deps,
                     cache_version, scanned_at, scan_hash)
                VALUES
                    (@ip, @ct, @fn, @rp,
                     @fh, @fs, @lm, @en,
                     @mn, @mv, @ma, @md,
                     @ml, @mdeps,
                     @cv, datetime('now'), @sh)";
            _BindComponentParams(cmd, row);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<ComponentCacheRow>> GetComponentsByInstanceAsync(
        string instancePath, string compType, CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT * FROM component_cache WHERE instance_path = @ip AND comp_type = @ct";
        cmd.Parameters.AddWithValue("@ip", instancePath);
        cmd.Parameters.AddWithValue("@ct", compType);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        var list = new List<ComponentCacheRow>();

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(_ReadComponentRow(reader));
        }

        return list;
    }

    public async Task<ComponentCacheRow?> GetComponentAsync(
        string instancePath, string compType, string fileName, CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT * FROM component_cache WHERE instance_path = @ip AND comp_type = @ct AND file_name = @fn";
        cmd.Parameters.AddWithValue("@ip", instancePath);
        cmd.Parameters.AddWithValue("@ct", compType);
        cmd.Parameters.AddWithValue("@fn", fileName);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        return await reader.ReadAsync(ct).ConfigureAwait(false) ? _ReadComponentRow(reader) : null;
    }

    public async Task<int> DeleteComponentsByInstanceAsync(string instancePath, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "DELETE FROM component_cache WHERE instance_path = @ip";
            cmd.Parameters.AddWithValue("@ip", instancePath);

            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<string?> GetComponentScanHashAsync(
        string instancePath, string compType, CancellationToken ct)
    {
        await using var conn = await _CreateConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """

                                      SELECT DISTINCT scan_hash
                                      FROM component_cache
                                      WHERE instance_path = @ip AND comp_type = @ct AND scan_hash IS NOT NULL
                                      ORDER BY scanned_at DESC
                                      LIMIT 1
                          """;
        cmd.Parameters.AddWithValue("@ip", instancePath);
        cmd.Parameters.AddWithValue("@ct", compType);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

        return result as string;
    }

    #endregion

    #region Arg Binding

    private static void _BindEntryParams(SqliteCommand cmd, CacheEntry e)
    {
        cmd.Parameters.AddWithValue("@cache_key", e.CacheKey);
        cmd.Parameters.AddWithValue("@entry_type", e.EntryType);
        cmd.Parameters.AddWithValue("@content_type", e.ContentType);
        cmd.Parameters.AddWithValue("@data", (object?)e.Data ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@data_size", e.DataSize);
        cmd.Parameters.AddWithValue("@file_hash", (object?)e.FileHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@file_path", (object?)e.FilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@content_hash", (object?)e.ContentHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@content_version", e.ContentVersion);
        cmd.Parameters.AddWithValue("@expires_at", e.ExpiresAt is not null ? ((DateTime)e.ExpiresAt).ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@tags", e.Tags);
        cmd.Parameters.AddWithValue("@group_name", e.GroupName);
        cmd.Parameters.AddWithValue("@priority", e.Priority);
    }

    private static CacheEntry _ReadEntry(SqliteDataReader r) =>
        new()
        {
            CacheKey = r.GetString(0),
            EntryType = (EntryType)r.GetInt32(1),
            ContentType = r.GetString(2),
            Data = r.IsDBNull(3) ? null : (byte[])r.GetValue(3),
            DataSize = r.GetInt64(4),
            FileHash = r.IsDBNull(5) ? null : r.GetString(5),
            FilePath = r.IsDBNull(6) ? null : r.GetString(6),
            ContentHash = r.IsDBNull(7) ? null : r.GetString(7),
            ContentVersion = r.GetInt32(8),
            CachedAt = DateTime.Parse(r.GetString(9)),
            LastAccessAt = DateTime.Parse(r.GetString(10)),
            ExpiresAt = r.IsDBNull(11) ? null : DateTime.Parse(r.GetString(11)),
            HitCount = r.GetInt64(12),
            Tags = r.GetString(13),
            GroupName = r.GetString(14),
            Priority = r.GetInt32(15),
        };

    private static void _BindInstanceParams(SqliteCommand cmd, InstanceCacheRow r)
    {
        cmd.Parameters.AddWithValue("@p", r.InstancePath);
        cmd.Parameters.AddWithValue("@n", r.InstanceName);
        cmd.Parameters.AddWithValue("@st", r.InstanceState);
        cmd.Parameters.AddWithValue("@cty", r.CardType);
        cmd.Parameters.AddWithValue("@is", r.IsStarred ? 1 : 0);
        cmd.Parameters.AddWithValue("@lo", (object?)r.Logo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@de", (object?)r.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rt", (object?)r.ReleaseTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@vn", (object?)r.VanillaName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@vv", (object?)r.VanillaVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dn", r.DropNumber);
        cmd.Parameters.AddWithValue("@re", r.Reliable ? 1 : 0);
        cmd.Parameters.AddWithValue("@lj", r.LoaderJson);
        cmd.Parameters.AddWithValue("@mc", (object?)r.MainClass ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ai", (object?)r.AssetsIndex ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@if", (object?)r.InheritsFrom ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@jv", (object?)r.JavaVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sh", r.SourceJsonHash);
        cmd.Parameters.AddWithValue("@fv", r.FormatVersion);
    }

    private static InstanceCacheRow _ReadInstanceRow(SqliteDataReader r)
    {
        return new InstanceCacheRow
        {
            InstancePath = r.GetString(0),
            InstanceName = r.GetString(1),
            InstanceState = r.GetString(2),
            CardType = r.GetInt32(3),
            IsStarred = r.GetInt32(4) != 0,
            Logo = r.IsDBNull(5) ? null : r.GetString(5),
            Description = r.IsDBNull(6) ? null : r.GetString(6),
            ReleaseTime = r.IsDBNull(7) ? null : r.GetString(7),
            VanillaName = r.IsDBNull(8) ? null : r.GetString(8),
            VanillaVersion = r.IsDBNull(9) ? null : r.GetString(9),
            DropNumber = r.GetInt32(10),
            Reliable = r.GetInt32(11) != 0,
            LoaderJson = r.GetString(12),
            MainClass = r.IsDBNull(13) ? null : r.GetString(13),
            AssetsIndex = r.IsDBNull(14) ? null : r.GetString(14),
            InheritsFrom = r.IsDBNull(15) ? null : r.GetString(15),
            JavaVersion = r.IsDBNull(16) ? null : r.GetInt32(16),
            SourceJsonHash = r.GetString(17),
            FormatVersion = r.GetInt32(18),
            CachedAt = DateTime.Parse(r.GetString(19)),
            LastLoadedAt = r.IsDBNull(20) ? null : DateTime.Parse(r.GetString(20)),
        };
    }

    private static void _BindComponentParams(SqliteCommand cmd, ComponentCacheRow r)
    {
        cmd.Parameters.AddWithValue("@instance_path", r.InstancePath);
        cmd.Parameters.AddWithValue("@comp_type", r.CompType);
        cmd.Parameters.AddWithValue("@file_name", r.FileName);
        cmd.Parameters.AddWithValue("@relative_path", r.RelativePath);
        cmd.Parameters.AddWithValue("@file_hash", (object?)r.FileHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@file_size", r.FileSize);
        cmd.Parameters.AddWithValue("@last_modified", r.LastModified.ToString("O"));
        cmd.Parameters.AddWithValue("@enabled", r.Enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@mod_name", (object?)r.ModName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mod_version", (object?)r.ModVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mod_author", (object?)r.ModAuthor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mod_description", (object?)r.ModDescription ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mod_loader", (object?)r.ModLoader ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mod_dependencies", (object?)r.ModDependencies ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cache_version", r.CacheVersion);
        cmd.Parameters.AddWithValue("@scan_hash", (object?)r.ScanHash ?? DBNull.Value);
    }

    private static ComponentCacheRow _ReadComponentRow(SqliteDataReader r)
    {
        return new ComponentCacheRow
        {
            InstancePath = r.GetString(0),
            CompType = r.GetString(1),
            FileName = r.GetString(2),
            RelativePath = r.GetString(3),
            FileHash = r.IsDBNull(4) ? null : r.GetString(4),
            FileSize = r.GetInt64(5),
            LastModified = DateTime.Parse(r.GetString(6)),
            Enabled = r.GetInt32(7) != 0,
            ModName = r.IsDBNull(8) ? null : r.GetString(8),
            ModVersion = r.IsDBNull(9) ? null : r.GetString(9),
            ModAuthor = r.IsDBNull(10) ? null : r.GetString(10),
            ModDescription = r.IsDBNull(11) ? null : r.GetString(11),
            ModLoader = r.IsDBNull(12) ? null : r.GetString(12),
            ModDependencies = r.IsDBNull(13) ? null : r.GetString(13),
            CacheVersion = r.GetInt32(14),
            ScannedAt = DateTime.Parse(r.GetString(15)),
            ScanHash = r.IsDBNull(16) ? null : r.GetString(16),
        };
    }


    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeLock.Dispose();
    }
}

public record CacheStatsRow(long TotalEntries, long TotalSizeBytes, long ExpiredEntries, long InlineEntries, long FileEntries);

public record EvictionCandidate(string? CacheKey, EntryType EntryType, string? FileHash, long DataSize, int Priority, long HitCount);