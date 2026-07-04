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

public interface ILaunchPipelineBuilder
{
    IReadOnlyList<Type> MiddlewareTypes { get; }

    void Use(Type middlewareType);

    void Use<TMiddleware>()
        where TMiddleware : ILaunchMiddleware;
}

public sealed class LaunchPipelineBuilder : ILaunchPipelineBuilder
{
    private readonly List<Type> _middlewareTypes = [];

    public IReadOnlyList<Type> MiddlewareTypes => _middlewareTypes.ToArray();

    public void Use(Type middlewareType)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);
        if (!typeof(ILaunchMiddleware).IsAssignableFrom(middlewareType))
            throw new ArgumentException("启动中间件必须实现 ILaunchMiddleware。", nameof(middlewareType));

        _middlewareTypes.Add(middlewareType);
    }

    public void Use<TMiddleware>()
        where TMiddleware : ILaunchMiddleware =>
        Use(typeof(TMiddleware));
}
