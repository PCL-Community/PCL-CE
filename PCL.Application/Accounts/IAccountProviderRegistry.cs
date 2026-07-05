// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Accounts;

public sealed record AccountProviderDescriptor
{
    public required AccountProviderId Id { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public int Order { get; init; }

    public required Type ProviderType { get; init; }
}

public interface IAccountProviderRegistry
{
    IReadOnlyList<AccountProviderDescriptor> Providers { get; }

    void AddProvider(AccountProviderDescriptor descriptor);

    bool RemoveProvider(AccountProviderId id);
}

public sealed class AccountProviderRegistry : IAccountProviderRegistry
{
    private readonly List<AccountProviderDescriptor> _providers = [];
    private readonly Dictionary<string, AccountProviderDescriptor> _providerMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<AccountProviderDescriptor> _snapshot = Array.Empty<AccountProviderDescriptor>();

    public IReadOnlyList<AccountProviderDescriptor> Providers => _snapshot;

    public void AddProvider(AccountProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id.Value))
            throw new ArgumentException("账号提供者 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new ArgumentException("账号提供者名称不能为空。", nameof(descriptor));
        if (!_providerMap.TryAdd(descriptor.Id.Value, descriptor))
            throw new InvalidOperationException($"账号提供者已注册：{descriptor.Id}");

        _providers.Add(descriptor);
        RefreshSnapshot();
    }

    public bool RemoveProvider(AccountProviderId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value) || !_providerMap.Remove(id.Value))
            return false;

        int index = _providers.FindIndex(provider => provider.Id.Equals(id.Value));
        if (index < 0)
            return false;

        _providers.RemoveAt(index);
        RefreshSnapshot();
        return true;
    }

    private void RefreshSnapshot() =>
        _snapshot = _providers
            .OrderBy(static provider => provider.Order)
            .ThenBy(static provider => provider.Id.Value, StringComparer.Ordinal)
            .ToArray();
}
