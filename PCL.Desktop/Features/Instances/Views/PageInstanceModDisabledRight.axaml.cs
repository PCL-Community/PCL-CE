// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceModDisabledRight : MyPageRight
{
    public PageInstanceModDisabledRight()
    {
        AvaloniaXamlLoader.Load(this);
        if (this.FindControl<MyButton>("BtnDownload") is { } download)
            download.Click += (_, _) => DownloadRequested?.Invoke(this, EventArgs.Empty);
        if (this.FindControl<MyButton>("BtnVersion") is { } version)
            version.Click += (_, _) => InstanceSelectRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? DownloadRequested;

    public event EventHandler? InstanceSelectRequested;
}
