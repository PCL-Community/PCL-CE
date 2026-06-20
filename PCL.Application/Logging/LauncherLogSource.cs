// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.Logging;

namespace PCL.Application.Logging;

public readonly record struct LauncherLogEntry(
    DateTimeOffset Timestamp,
    PortableLogLevel Level,
    string Module,
    string Message,
    string? ExceptionText)
{
    public string ToDisplayText()
    {
        string line =
            $"[{Timestamp.ToLocalTime():HH:mm:ss.fff}] [{Level}] [{Module}] {Message}";
        return string.IsNullOrWhiteSpace(ExceptionText)
            ? line
            : $"{line}{Environment.NewLine}{ExceptionText}";
    }
}

public interface ILauncherLogSource : IDisposable
{
    event Action<LauncherLogEntry>? EntryAdded;

    IReadOnlyList<LauncherLogEntry> GetSnapshot();

    void Clear();
}

public sealed class PortableLauncherLogSource : ILauncherLogSource
{
    private readonly object _syncRoot = new();
    private readonly Queue<LauncherLogEntry> _entries;
    private readonly int _capacity;
    private bool _disposed;

    public PortableLauncherLogSource(int capacity = 2_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _entries = new Queue<LauncherLogEntry>(
            Math.Min(capacity, 256));
        PortableLog.Written += OnPortableLogWritten;
    }

    public event Action<LauncherLogEntry>? EntryAdded;

    public IReadOnlyList<LauncherLogEntry> GetSnapshot()
    {
        lock (_syncRoot)
            return _entries.ToArray();
    }

    public void Clear()
    {
        lock (_syncRoot)
            _entries.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        PortableLog.Written -= OnPortableLogWritten;
    }

    private void OnPortableLogWritten(PortableLogEntry entry)
    {
        if (_disposed)
            return;

        LauncherLogEntry launcherEntry = new(
            entry.Timestamp,
            entry.Level,
            entry.Module,
            entry.Message,
            entry.Exception?.ToString());

        lock (_syncRoot)
        {
            while (_entries.Count >= _capacity)
                _entries.Dequeue();
            _entries.Enqueue(launcherEntry);
        }

        EntryAdded?.Invoke(launcherEntry);
    }
}
