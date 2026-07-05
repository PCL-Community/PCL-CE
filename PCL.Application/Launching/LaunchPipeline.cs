// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Launching;

public sealed record LaunchRequest(string InstanceId);

public sealed class LaunchContext(LaunchRequest request, IServiceProvider services)
{
    public LaunchRequest Request { get; } = request;

    public IServiceProvider Services { get; } = services;

    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}

public delegate ValueTask LaunchPipelineNext(
    LaunchContext context,
    CancellationToken cancellationToken);

public interface ILaunchMiddleware
{
    ValueTask InvokeAsync(
        LaunchContext context,
        LaunchPipelineNext nextMiddleware,
        CancellationToken cancellationToken);
}

public sealed record LaunchMiddlewareDescriptor(
    Type MiddlewareType,
    Func<IServiceProvider, ILaunchMiddleware> CreateMiddleware);

public interface ILaunchPipelineBuilder
{
    IReadOnlyList<LaunchMiddlewareDescriptor> Middleware { get; }

    IReadOnlyList<Type> MiddlewareTypes { get; }

    void Use<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
        where TMiddleware : notnull, ILaunchMiddleware;

    void Use<TMiddleware>()
        where TMiddleware : notnull, ILaunchMiddleware, new();
}

public sealed class LaunchPipelineBuilder : ILaunchPipelineBuilder
{
    private readonly List<LaunchMiddlewareDescriptor> _middleware = [];
    private IReadOnlyList<LaunchMiddlewareDescriptor> _middlewareSnapshot = Array.Empty<LaunchMiddlewareDescriptor>();
    private IReadOnlyList<Type> _middlewareTypesSnapshot = Array.Empty<Type>();

    public IReadOnlyList<LaunchMiddlewareDescriptor> Middleware => _middlewareSnapshot;

    public IReadOnlyList<Type> MiddlewareTypes => _middlewareTypesSnapshot;

    public void Use<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
        where TMiddleware : notnull, ILaunchMiddleware
    {
        ArgumentNullException.ThrowIfNull(factory);
        _middleware.Add(new LaunchMiddlewareDescriptor(
            typeof(TMiddleware),
            services => factory(services)));
        RefreshSnapshot();
    }

    public void Use<TMiddleware>()
        where TMiddleware : notnull, ILaunchMiddleware, new() =>
        Use(static _ => new TMiddleware());

    private void RefreshSnapshot()
    {
        _middlewareSnapshot = _middleware.ToArray();
        _middlewareTypesSnapshot = _middleware
            .Select(static middleware => middleware.MiddlewareType)
            .ToArray();
    }
}
