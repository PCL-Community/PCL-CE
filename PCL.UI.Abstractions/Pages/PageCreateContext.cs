// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Pages;

public enum PageRegion
{
    Main,
    Left,
    Right,
    Settings,
    Dialog
}

public sealed record PageCreateContext(
    string Route,
    IServiceProvider Services,
    object? Parameter = null);

public interface IPageProvider
{
    ValueTask<object> CreatePageAsync(
        PageCreateContext context,
        CancellationToken cancellationToken);
}

public sealed class DelegatePageProvider : IPageProvider
{
    private readonly Func<PageCreateContext, CancellationToken, ValueTask<object>> _factory;

    public DelegatePageProvider(Func<PageCreateContext, CancellationToken, ValueTask<object>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public ValueTask<object> CreatePageAsync(
        PageCreateContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _factory(context, cancellationToken);
    }
}
