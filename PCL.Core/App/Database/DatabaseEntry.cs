using System;
using LiteDB;

namespace PCL.Core.App.Database;

/// <summary>
/// Base database connention entry.
/// </summary>
public abstract class DatabaseEntry : IDisposable
{
    protected readonly LiteDatabase Db;
    private bool _disposed = false;

    /// <exception cref="ArgumentNullException">Throw if connection path is invalid.</exception>
    protected DatabaseEntry(string connPath)
    {
        var db = DatabaseService.GetConnection(connPath);
        Db = db ?? throw new ArgumentNullException(nameof(db), "Database variable can not be null.");
    }

    /// <exception cref="ArgumentNullException">Throw if connection path is invalid.</exception>
    protected DatabaseEntry(LiteDatabase database)
    {
        Db = database ?? throw new ArgumentNullException(nameof(database), "Database variable can not be null.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // NOTE: do not dispose Db, because it is managed by DatabaseService
        }

        _disposed = true;
    }
}