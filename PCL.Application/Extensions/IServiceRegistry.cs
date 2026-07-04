// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Extensions;

public interface IServiceRegistry : IServiceProvider
{
    IReadOnlyDictionary<Type, object> Singletons { get; }

    void AddSingleton<TService>(TService instance)
        where TService : notnull;

    bool Remove(Type serviceType);
}

public sealed class ServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, object> _singletons = [];

    public IReadOnlyDictionary<Type, object> Singletons => _singletons;

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return _singletons.GetValueOrDefault(serviceType);
    }

    public void AddSingleton<TService>(TService instance)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(instance);
        Type serviceType = typeof(TService);
        if (_singletons.ContainsKey(serviceType))
            throw new InvalidOperationException($"服务已注册：{serviceType.FullName}");

        _singletons.Add(serviceType, instance);
    }

    public bool Remove(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return _singletons.Remove(serviceType);
    }
}
