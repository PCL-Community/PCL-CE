using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;

namespace PCL.Core.IO.Storage.Cache;

public class SchemaManager(string connectionString)
{
    private const int CurrentSchemaVersion = 1;

    /// <exception cref="InvalidOperationException">Invalid schema version</exception>
    public async Task EnsureCurrentSchemaAsync()
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        await _ExecutePragmaAsync(conn, "journal_mode", "WAL").ConfigureAwait(false);
        await _ExecutePragmaAsync(conn, "synchronous", "NORMAL").ConfigureAwait(false);
        await _ExecutePragmaAsync(conn, "cache_size", "-8000").ConfigureAwait(false); // 8MB page cache

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS cache_meta (
                                  key   TEXT NOT NULL PRIMARY KEY,
                                  value TEXT NOT NULL
                              )
                              """;
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var currentVersion = 0;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT value FROM cache_meta WHERE key = 'schema_version'";
            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            if (result is not null)
            {
                currentVersion = int.Parse(result.ToString() ?? throw new InvalidOperationException("Invalid schema version"));
            }
        }

        if (currentVersion < CurrentSchemaVersion)
        {
            // NOTE: this method is ApplyMigrations, not _ApplyMigrationAsync. see the 's' difference
            await _ApplyMigrationsAsync(conn, currentVersion).ConfigureAwait(false);
        }

        await _EnsureAllTablesExistAsync(conn).ConfigureAwait(false);
    }

    private static async Task<int> _ExecutePragmaAsync(SqliteConnection conn, string pragma, string value)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA {pragma} = {value}";
        return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    #region Version Upgrade

    private async Task _ApplyMigrationsAsync(SqliteConnection conn, int fromVersion)
    {
        var v = fromVersion;
        while (v < CurrentSchemaVersion)
        {
            v++;
            await using var tx = conn.BeginTransaction();
            try
            {
                await _ApplyMigrationAsync(conn, v).ConfigureAwait(false);
                await using var setVer = conn.CreateCommand();
                setVer.CommandText = "INSERT OR REPLACE INTO cache_meta (key, value) VALUES ('schema_version', @v)";
                setVer.Parameters.AddWithValue("@v", v);
                await setVer.ExecuteNonQueryAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    private async Task _ApplyMigrationAsync(SqliteConnection conn, int targetVersion)
    {
        switch (targetVersion)
        {
            case 1:
                await _ExecuteDdlAsync(conn, DdlV1CreateAllTables).ConfigureAwait(false);
                await _ExecuteDdlAsync(conn, DdlV1CreateIndexes).ConfigureAwait(false);
                break;

                // NOTE: add future migrations here
                // e.g.
                // case 2:
                //     await _ExecuteDdlAsync(conn, "some sql").
        }
    }

    #endregion

    private async Task<int> _EnsureAllTablesExistAsync(SqliteConnection conn)
    {
        await _ExecuteDdlAsync(conn, DdlV1CreateAllTables).ConfigureAwait(false);
        await _ExecuteDdlAsync(conn, DdlV1CreateIndexes).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO cache_meta (key, value) VALUES ('schema_version', @v)";
        cmd.Parameters.AddWithValue("@v", CurrentSchemaVersion);
        return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<int> _ExecuteDdlAsync(SqliteConnection conn, string ddl)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = ddl;
        return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    #region DDL Text

    private const string DdlV1CreateAllTables = """

                                                          CREATE TABLE IF NOT EXISTS cache_entries (
                                                              cache_key       TEXT NOT NULL PRIMARY KEY,
                                                              entry_type      INTEGER NOT NULL DEFAULT 0,
                                                              content_type    TEXT NOT NULL DEFAULT '',
                                                              data            BLOB,
                                                              data_size       INTEGER NOT NULL DEFAULT 0,
                                                              file_hash       TEXT,
                                                              file_path       TEXT,
                                                              content_hash    TEXT,
                                                              content_version INTEGER NOT NULL DEFAULT 1,
                                                              created_at      TEXT NOT NULL DEFAULT (datetime('now')),
                                                              last_access_at  TEXT NOT NULL DEFAULT (datetime('now')),
                                                              expires_at      TEXT,
                                                              hit_count       INTEGER NOT NULL DEFAULT 0,
                                                              tags            TEXT NOT NULL DEFAULT '',
                                                              group_name      TEXT NOT NULL DEFAULT '',
                                                              priority        INTEGER NOT NULL DEFAULT 1
                                                          );

                                                          CREATE TABLE IF NOT EXISTS instance_cache (
                                                              instance_path   TEXT NOT NULL PRIMARY KEY,
                                                              instance_name   TEXT NOT NULL,
                                                              instance_state  TEXT NOT NULL DEFAULT 'Error',
                                                              card_type       INTEGER NOT NULL DEFAULT 0,
                                                              is_starred      INTEGER NOT NULL DEFAULT 0,
                                                              logo            TEXT,
                                                              description     TEXT,
                                                              release_time    TEXT,
                                                              vanilla_name    TEXT,
                                                              vanilla_version TEXT,
                                                              drop_number     INTEGER NOT NULL DEFAULT 0,
                                                              reliable        INTEGER NOT NULL DEFAULT 1,
                                                              loaders_json    TEXT NOT NULL DEFAULT '[]',
                                                              main_class      TEXT,
                                                              assets_index    TEXT,
                                                              inherits_from   TEXT,
                                                              java_version    INTEGER,
                                                              source_json_hash TEXT NOT NULL,
                                                              format_version  INTEGER NOT NULL DEFAULT 1,
                                                              cached_at       TEXT NOT NULL DEFAULT (datetime('now')),
                                                              last_loaded_at  TEXT
                                                          );

                                                          CREATE TABLE IF NOT EXISTS component_cache (
                                                              instance_path   TEXT NOT NULL,
                                                              comp_type       TEXT NOT NULL,
                                                              file_name       TEXT NOT NULL,
                                                              relative_path   TEXT NOT NULL,
                                                              file_hash       TEXT,
                                                              file_size       INTEGER NOT NULL DEFAULT 0,
                                                              last_modified   TEXT NOT NULL,
                                                              enabled         INTEGER NOT NULL DEFAULT 1,
                                                              mod_name        TEXT,
                                                              mod_version     TEXT,
                                                              mod_author      TEXT,
                                                              mod_description TEXT,
                                                              mod_loader      TEXT,
                                                              mod_deps        TEXT,
                                                              cache_version   INTEGER NOT NULL DEFAULT 7,
                                                              scanned_at      TEXT NOT NULL DEFAULT (datetime('now')),
                                                              scan_hash       TEXT,
                                                              PRIMARY KEY (instance_path, comp_type, file_name)
                                                          );
                                                          
                                                          CREATE TABLE IF NOT EXISTS cache_stats (
                                                              stat_name  TEXT NOT NULL PRIMARY KEY,
                                                              stat_value INTEGER NOT NULL DEFAULT 0
                                                          );
                                                      
                                                  """;

    private const string DdlV1CreateIndexes = """

                                                      CREATE INDEX IF NOT EXISTS idx_ce_expires  ON cache_entries(expires_at);
                                                      CREATE INDEX IF NOT EXISTS idx_ce_group    ON cache_entries(group_name);
                                                      CREATE INDEX IF NOT EXISTS idx_ce_tags     ON cache_entries(tags);
                                                      CREATE INDEX IF NOT EXISTS idx_ce_access   ON cache_entries(last_access_at);
                                                      CREATE INDEX IF NOT EXISTS idx_ce_priority ON cache_entries(priority);
                                                      CREATE INDEX IF NOT EXISTS idx_ce_hits     ON cache_entries(hit_count);
                                                      CREATE INDEX IF NOT EXISTS idx_cc_instance ON component_cache(instance_path);
                                                      CREATE INDEX IF NOT EXISTS idx_cc_scanhash ON component_cache(instance_path, comp_type, scan_hash);
                                                  
                                              """;

    #endregion
}