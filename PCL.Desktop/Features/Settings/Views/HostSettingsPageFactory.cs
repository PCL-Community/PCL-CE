// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

internal static class HostSettingsPageFactory
{
    public static MyPageRight Create(HostSettingsPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.PageFactory is null)
            return new PageSetupHostModule(descriptor);

        object page = descriptor.PageFactory();
        return page as MyPageRight
            ?? throw new InvalidOperationException(
                $"Host 设置页工厂必须返回 {nameof(MyPageRight)}：{descriptor.Id}");
    }
}
