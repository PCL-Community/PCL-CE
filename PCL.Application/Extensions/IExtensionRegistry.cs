// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Extensions;

public sealed record ExtensionDescriptor(
    string Id,
    string DisplayName,
    string? Description = null);

public interface IExtensionRegistry
{
    IReadOnlyList<ExtensionDescriptor> Extensions { get; }

    void AddExtension(ExtensionDescriptor descriptor);

    bool RemoveExtension(string id);
}

public sealed class ExtensionRegistry : IExtensionRegistry
{
    private readonly List<ExtensionDescriptor> _extensions = [];

    public IReadOnlyList<ExtensionDescriptor> Extensions => _extensions.ToArray();

    public void AddExtension(ExtensionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id))
            throw new ArgumentException("扩展 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new ArgumentException("扩展名称不能为空。", nameof(descriptor));
        if (_extensions.Any(extension => string.Equals(extension.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"扩展已注册：{descriptor.Id}");

        _extensions.Add(descriptor);
    }

    public bool RemoveExtension(string id)
    {
        int index = _extensions.FindIndex(extension => string.Equals(extension.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        _extensions.RemoveAt(index);
        return true;
    }
}
