// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Downloads;

public enum DownloadSourceKind
{
    Metadata,
    Asset,
    Library,
    Launcher,
    CommunityResource
}

public sealed record DownloadSourceDescriptor
{
    public required DownloadSourceId Id { get; init; }

    public required string DisplayName { get; init; }

    public required Uri BaseUri { get; init; }

    public DownloadSourceKind Kind { get; init; }

    public int Order { get; init; }
}

public interface IDownloadSourceRegistry
{
    IReadOnlyList<DownloadSourceDescriptor> Sources { get; }

    void AddSource(DownloadSourceDescriptor descriptor);

    bool RemoveSource(DownloadSourceId id);
}

public sealed class DownloadSourceRegistry : IDownloadSourceRegistry
{
    private readonly List<DownloadSourceDescriptor> _sources = [];
    private readonly Dictionary<string, DownloadSourceDescriptor> _sourceMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<DownloadSourceDescriptor> _snapshot = Array.Empty<DownloadSourceDescriptor>();

    public IReadOnlyList<DownloadSourceDescriptor> Sources => _snapshot;

    public void AddSource(DownloadSourceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id.Value))
            throw new ArgumentException("下载源 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new ArgumentException("下载源名称不能为空。", nameof(descriptor));
        if (!descriptor.BaseUri.IsAbsoluteUri)
            throw new ArgumentException("下载源地址必须是绝对 URI。", nameof(descriptor));
        if (!_sourceMap.TryAdd(descriptor.Id.Value, descriptor))
            throw new InvalidOperationException($"下载源已注册：{descriptor.Id}");

        _sources.Add(descriptor);
        RefreshSnapshot();
    }

    public bool RemoveSource(DownloadSourceId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value) || !_sourceMap.Remove(id.Value))
            return false;

        int index = _sources.FindIndex(source => source.Id.Equals(id.Value));
        if (index < 0)
            return false;

        _sources.RemoveAt(index);
        RefreshSnapshot();
        return true;
    }

    private void RefreshSnapshot() =>
        _snapshot = _sources
            .OrderBy(static source => source.Kind)
            .ThenBy(static source => source.Order)
            .ThenBy(static source => source.Id.Value, StringComparer.Ordinal)
            .ToArray();
}
