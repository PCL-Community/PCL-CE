using System;
using System.Collections.Concurrent;
using LiteDB;

namespace PCL.Core.App.Database;

[LifecycleService(LifecycleState.Loading)]
[LifecycleScope("database", "数据库管理")]
public partial class DatabaseService
{
    private static readonly ConcurrentDictionary<string, LiteDatabase> _Instances = new();

    [LifecycleStop]
    private static void _Stop()
    {
        foreach (var instance in _Instances.Values)
        {
            instance.Dispose();
        }

        _Instances.Clear();
    }

    /// <summary>
    /// Get the database connection from specified connection path.<br/>
    /// If not exists, a new connection will be created and cached.
    /// </summary>
    /// <returns>Got connection instance.</returns>
    /// <exception cref="ArgumentException">Throw if connection path is invalid.</exception>
    public static LiteDatabase GetConnection(string connectionPath)
    {
        if (string.IsNullOrWhiteSpace(connectionPath))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionPath));
        }

        return _Instances.GetOrAdd(connectionPath, cp => new LiteDatabase(cp));
    }
}