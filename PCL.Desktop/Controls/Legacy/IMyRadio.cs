// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Controls.Legacy;

public interface IMyRadio
{
#pragma warning disable CA1711
    public delegate void ChangedEventHandler(object sender, RouteEventArgs e);

    public delegate void CheckEventHandler(object sender, RouteEventArgs e);
#pragma warning restore CA1711

    event CheckEventHandler? Check;

    event ChangedEventHandler? Changed;
}
