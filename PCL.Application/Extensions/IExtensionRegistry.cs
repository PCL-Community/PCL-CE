// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Extensions;

public sealed record ExtensionDescriptor(
    ExtensionId Id,
    string DisplayName,
    string? Description = null);

public interface IExtensionRegistry
{
    IReadOnlyList<ExtensionDescriptor> Extensions { get; }

    void AddExtension(ExtensionDescriptor descriptor);

    bool RemoveExtension(ExtensionId id);
}

public sealed class ExtensionRegistry : IExtensionRegistry
{
    private readonly List<ExtensionDescriptor> _extensions = [];
    private readonly Dictionary<string, ExtensionDescriptor> _extensionMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ExtensionDescriptor> _snapshot = Array.Empty<ExtensionDescriptor>();

    public IReadOnlyList<ExtensionDescriptor> Extensions => _snapshot;

    public void AddExtension(ExtensionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id.Value))
            throw new ArgumentException("扩展 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new ArgumentException("扩展名称不能为空。", nameof(descriptor));
        if (!_extensionMap.TryAdd(descriptor.Id.Value, descriptor))
            throw new InvalidOperationException($"扩展已注册：{descriptor.Id}");

        _extensions.Add(descriptor);
        RefreshSnapshot();
    }

    public bool RemoveExtension(ExtensionId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value) || !_extensionMap.Remove(id.Value))
            return false;

        int index = _extensions.FindIndex(extension => extension.Id.Equals(id.Value));
        if (index < 0)
            return false;

        _extensions.RemoveAt(index);
        RefreshSnapshot();
        return true;
    }

    private void RefreshSnapshot() =>
        _snapshot = _extensions.ToArray();
}
