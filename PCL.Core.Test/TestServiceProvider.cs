using System;
using System.Collections.Generic;

namespace PCL.Core.Test;

/// <summary>测试用的最小服务容器。</summary>
internal sealed class TestServiceProvider : IServiceProvider, IDisposable
{
    private readonly Dictionary<Type, object> _services = [];

    public int DisposeCount { get; private set; }

    public void Dispose()
    {
        DisposeCount++;
    }

    public object? GetService(Type serviceType)
    {
        return _services.GetValueOrDefault(serviceType);
    }

    public TestServiceProvider Add<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
        return this;
    }
}