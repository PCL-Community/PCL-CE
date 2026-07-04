// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupPlugin : MyPageRight
{
    public PageSetupPlugin()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
    }
}
