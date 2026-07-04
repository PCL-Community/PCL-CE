// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using PCL.UI.Abstractions.Pages;

namespace PCL.Desktop.Hosting;

internal interface IDesktopPageAdapter
{
    ValueTask<DesktopMainPage> CreateMainPageAsync(
        IPageProvider provider,
        PageCreateContext context,
        CancellationToken cancellationToken);
}

internal sealed class DesktopPageAdapter : IDesktopPageAdapter
{
    public async ValueTask<DesktopMainPage> CreateMainPageAsync(
        IPageProvider provider,
        PageCreateContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(context);

        object page = await provider.CreatePageAsync(context, cancellationToken).ConfigureAwait(true);
        return page switch
        {
            DesktopMainPage mainPage => mainPage,
            Control control => new DesktopMainPage(null, control),
            _ => throw new InvalidOperationException(
                $"页面 Provider '{context.Route}' 返回了 Desktop 无法承载的页面类型：{page.GetType().FullName}")
        };
    }
}
